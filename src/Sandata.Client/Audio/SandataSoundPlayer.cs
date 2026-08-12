using System.Collections.Immutable;
using Sandata.Core.Weapons;

namespace Sandata.Client.Audio;

/// <summary>
/// The seam between a resolved shot and the audio device, matching the
/// pattern <c>Hukbo.Client.Audio.ISoundPlayer</c> already sets: everything
/// above this interface is pure and testable, and only a MonoGame-backed
/// implementation touches a real audio device. No such implementation is
/// part of this task; wiring one in is content-loading work for a later
/// task, exactly as <c>MonoGameSoundPlayer</c> is a separate type from
/// <c>ISoundPlayer</c> on the melee side.
/// </summary>
internal interface ISandataSoundOutput
{
    /// <summary>
    /// Requests playback of one specific variant of <paramref name="slot"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the backend declined the cue — on
    /// MonoGame, an exhausted instance pool or source list. The caller
    /// already reserved a <see cref="SandataSoundBudget"/> slot before
    /// calling this, so a decline here means the audio backend's own ceiling
    /// is lower than this budget's, not that the budget was wrong.
    /// </returns>
    bool Play(SoundSlot slot, int variantNumber, ulong shooterEntityId);
}

/// <summary>
/// Resolves a fired shot to a slot and variant through
/// <see cref="ShotSlotResolver"/>, reserves a <see cref="SandataSoundBudget"/>
/// slot for the resolved row's own <see cref="SoundSlot.TailTicks"/>, and
/// requests playback through <see cref="ISandataSoundOutput"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Automatic fire plays one loop instance and one tail instance per
/// shooter, never one instance per round.</b> Design section 10 requires
/// this explicitly, since a loop sample already covers a sustained burst on
/// its own and one instance per round would over-subscribe the pool for no
/// audible benefit. <see cref="HandleShotFired"/> routes every
/// <see cref="FireMode.Auto"/> round through <see cref="HandleAutomaticRound"/>,
/// which plays the loop only on the round that first takes a fresh
/// reservation — every later round in the same burst renews that same
/// reservation's expiry without a second <see cref="ISandataSoundOutput.Play"/>
/// call.
/// </para>
/// <para>
/// <b>The tail reservation is taken during the burst, not just when it
/// ends.</b> This is this task's own architectural choice, not a literal
/// design instruction: <see cref="HandleAutomaticRound"/> also renews a
/// <see cref="SoundFamily.GunTail"/> reservation for the same shooter on
/// every round, without playing it, so the pool slot the tail will need is
/// already held by the time <see cref="HandleAutomaticFireStopped"/> is
/// called. Design section 10 names the instance pool as "the real ceiling"
/// precisely because it saturates fastest during sustained fire from many
/// shooters at once — exactly when a tail reservation taken only at the
/// moment fire stops could find no free slot left. Pre-holding the tail's
/// slot for the whole burst is what keeps the tail audible under that
/// pressure.
/// </para>
/// </remarks>
internal sealed class SandataSoundPlayer
{
    private readonly ISandataSoundOutput _output;
    private readonly SandataSoundBudget _budget;

    /// <summary>
    /// Every shooter whose current burst had its loop cue refused, and is
    /// therefore being carried by one report per round — see
    /// <see cref="PlayAutomaticRoundReport"/>. Cleared for a shooter when its
    /// burst ends, in <see cref="HandleAutomaticFireStopped"/>.
    /// </summary>
    private ImmutableArray<ulong> _loopFallbackShooters = ImmutableArray<ulong>.Empty;

    public SandataSoundPlayer(ISandataSoundOutput output, SandataSoundBudget budget)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(budget);

        _output = output;
        _budget = budget;
    }

    /// <summary>
    /// Handles one fired round. For <see cref="FireMode.Single"/>,
    /// <see cref="FireMode.Burst2"/>, and <see cref="FireMode.Burst3"/>, this
    /// resolves, reserves, and plays a single report immediately. For
    /// <see cref="FireMode.Auto"/>, this delegates to
    /// <see cref="HandleAutomaticRound"/> — see this type's remarks.
    /// </summary>
    /// <param name="caliber">The firing weapon's caliber family.</param>
    /// <param name="mode">The fire mode this round was fired under.</param>
    /// <param name="rangeWu">The engagement range, in world units.</param>
    /// <param name="shooterIsIndoors">Whether the shooter is inside a room.</param>
    /// <param name="suppressorFitted">Whether the firing weapon carries a suppressor.</param>
    /// <param name="tick">The tick this round occurred on.</param>
    /// <param name="shooterEntityId">The shooter's entity id.</param>
    public void HandleShotFired(
        CaliberFamily caliber,
        FireMode mode,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted,
        long tick,
        ulong shooterEntityId)
    {
        if (mode == FireMode.Auto)
        {
            HandleAutomaticRound(caliber, rangeWu, shooterIsIndoors, suppressorFitted, tick, shooterEntityId);
            return;
        }

        var resolution = ShotSlotResolver.Resolve(
            caliber, mode, rangeWu, shooterIsIndoors, suppressorFitted, tick, shooterEntityId);

        if (!_budget.TryReserve(
                shooterEntityId, resolution.Slot.Family, tick, resolution.Slot.TailTicks, out _))
        {
            // The pool is exhausted. The cue is declined, not queued — the
            // same choice Hukbo.Client.Audio.SoundDirector makes when
            // SoundCueBudget.TryConsume returns false.
            return;
        }

        _output.Play(resolution.Slot, resolution.VariantNumber, shooterEntityId);
    }

    /// <summary>
    /// Notifies the player that <paramref name="shooterEntityId"/>'s
    /// automatic burst has ended, so the tail instance that follows a
    /// sustained loop can play. Renews the tail reservation this shooter's
    /// rounds already held — see this type's remarks — to the tail slot's
    /// own <see cref="SoundSlot.TailTicks"/> from <paramref name="tick"/>,
    /// then requests playback.
    /// </summary>
    public void HandleAutomaticFireStopped(
        CaliberFamily caliber,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted,
        long tick,
        ulong shooterEntityId)
    {
        _loopFallbackShooters = _loopFallbackShooters.Remove(shooterEntityId);

        var tailSlot = ShotSlotResolver.ResolveGunTailSlot(
            caliber, rangeWu, shooterIsIndoors, suppressorFitted);
        var variantNumber = ShotSlotResolver.SelectVariantNumber(
            tick, shooterEntityId, tailSlot.VariantCount);

        if (!_budget.TryReserve(shooterEntityId, tailSlot.Family, tick, tailSlot.TailTicks, out _))
        {
            return;
        }

        _output.Play(tailSlot, variantNumber, shooterEntityId);
    }

    /// <summary>
    /// Plays one automatic round's report when the loop cue for that burst was
    /// declined — which, today, is what a burst always gets, because no
    /// <see cref="SoundFamily.GunLoop"/> file exists on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a documented degradation, not the model.</b> Design section
    /// 10's model is one loop instance and one tail instance per shooter, and
    /// <see cref="HandleAutomaticRound"/> implements exactly that. The
    /// authorized audio slice covers four slots — an AK-pattern rifle and a
    /// Glock-pattern pistol firing <em>single</em> shots, close and indoor —
    /// so <see cref="ISandataSoundOutput.Play"/> returns
    /// <see langword="false"/> for every loop cue it is ever handed, and
    /// wiring automatic fire without this fallback would have turned audible
    /// single shots into silence. That is worse than the defect it was fixing.
    /// </para>
    /// <para>
    /// A report per round at 600 rounds per minute is one cue every five ticks
    /// at a tick rate of 50. It is audible and continuous, and it is honestly
    /// not a loop sample. This whole method disappears the day real loop and
    /// tail files exist.
    /// </para>
    /// </remarks>
    private void PlayAutomaticRoundReport(
        CaliberFamily caliber,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted,
        long tick,
        ulong shooterEntityId)
    {
        var reportResolution = ShotSlotResolver.Resolve(
            caliber, FireMode.Single, rangeWu, shooterIsIndoors, suppressorFitted, tick, shooterEntityId);

        if (!_budget.TryReserve(
                shooterEntityId, reportResolution.Slot.Family, tick, reportResolution.Slot.TailTicks, out _))
        {
            return;
        }

        _output.Play(reportResolution.Slot, reportResolution.VariantNumber, shooterEntityId);
    }

    private void HandleAutomaticRound(
        CaliberFamily caliber,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted,
        long tick,
        ulong shooterEntityId)
    {
        var loopResolution = ShotSlotResolver.Resolve(
            caliber, FireMode.Auto, rangeWu, shooterIsIndoors, suppressorFitted, tick, shooterEntityId);

        var loopReserved = _budget.TryReserve(
            shooterEntityId, loopResolution.Slot.Family, tick, loopResolution.Slot.TailTicks,
            out var loopIsNewReservation);

        if (loopReserved && loopIsNewReservation &&
            !_output.Play(loopResolution.Slot, loopResolution.VariantNumber, shooterEntityId))
        {
            // Remembered for the rest of this burst: the loop is attempted
            // once, on the round that takes the reservation, and every later
            // round only renews it. Without this the renewals could not tell a
            // playing loop from a refused one.
            _loopFallbackShooters = _loopFallbackShooters.Add(shooterEntityId);
        }

        if (!loopReserved || _loopFallbackShooters.Contains(shooterEntityId))
        {
            // No loop sample exists for this row, so the burst is carried by
            // one report per round instead — see PlayAutomaticRoundReport.
            PlayAutomaticRoundReport(
                caliber, rangeWu, shooterIsIndoors, suppressorFitted, tick, shooterEntityId);
        }

        var tailSlot = ShotSlotResolver.ResolveGunTailSlot(
            caliber, rangeWu, shooterIsIndoors, suppressorFitted);

        // Renewed every round without playing it — see this type's remarks
        // on why the tail's pool slot is held for the whole burst rather
        // than only claimed once fire stops.
        _budget.TryReserve(shooterEntityId, tailSlot.Family, tick, tailSlot.TailTicks, out _);
    }
}
