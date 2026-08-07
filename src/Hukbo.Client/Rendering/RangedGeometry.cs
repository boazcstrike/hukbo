using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from a weapon, its current <see cref="RangedPhase"/>, and the
/// ticks remaining in that phase to a <see cref="RangedPose"/>. Mirrors
/// <see cref="SwingGeometry"/> and <see cref="GaitGeometry"/>'s shape: a
/// static class over value types only, with no store, no clock, and no
/// dependency on anything a unit test cannot construct.
/// </summary>
/// <remarks>
/// <para>
/// Every constant here is a keyframe chosen for gameplay legibility, per
/// design section 8.4 of <c>docs/plans/2026-08-07-ranged-units-design.md</c>,
/// and is commented PROVISIONAL RECONSTRUCTION per CLAUDE.md section 7 — none
/// of it is a historical measurement.
/// </para>
/// <para>
/// The three weapons are deliberately differentiated rather than sharing one
/// table. The Bangkaw cocks back past the shoulder with a negative torso lean
/// and its weapon line shortens toward vanishing through
/// <see cref="RangedPhase.Recover"/>. The Busog holds a near-vertical stave
/// that barely rotates across the whole sequence while its
/// <see cref="RangedPose.DrawTension"/> channel rises through
/// <see cref="RangedPhase.Draw"/>, holds at its peak through
/// <see cref="RangedPhase.Ready"/>, and snaps to zero — independent of ticks
/// remaining — on <see cref="RangedPhase.Release"/>. The Arquebus's
/// <see cref="RangedPhase.Load"/> reads as a multi-beat ramrod motion and its
/// <see cref="RangedPhase.Release"/> holds one fixed pose regardless of ticks
/// remaining, because the muzzle flash and recoil are the readable content and
/// nothing about the pose itself should compete with them by moving.
/// </para>
/// </remarks>
internal static class RangedGeometry
{
    /// <summary>
    /// PROVISIONAL. Governs how quickly the Busog's <see cref="RangedPhase.Draw"/>
    /// tension rises as its ticks-remaining counts down toward
    /// <see cref="RangedPhase.Ready"/>. Chosen only so the channel is visibly
    /// non-flat across a typical draw span; not a measurement of any real bow.
    /// </summary>
    private const float BusogDrawTensionDecayRate = 1f;

    /// <summary>
    /// Resolves the pose one ranged weapon is in at one phase of its draw
    /// cycle.
    /// </summary>
    /// <param name="weapon">
    /// Must be one of the three ranged <see cref="WeaponId"/> members —
    /// <see cref="WeaponId.Bangkaw"/>, <see cref="WeaponId.Busog"/>, or
    /// <see cref="WeaponId.Arquebus"/>. Any other value throws, because a
    /// melee weapon never reaches a non-<see cref="RangedPhase.None"/> phase
    /// (<see cref="RangedPhaseProjection.Derive"/>), so a caller passing one
    /// here has a bug upstream.
    /// </param>
    /// <param name="phase">
    /// Must not be <see cref="RangedPhase.None"/>; the resolver that calls
    /// this filters that case out before ever reaching here, because
    /// <see cref="RangedPhase.None"/> resolves to no pose entry at all rather
    /// than a neutral one.
    /// </param>
    /// <param name="ticksRemaining">
    /// Ticks remaining in <paramref name="phase"/>, never negative.
    /// </param>
    public static RangedPose ResolvePose(
        WeaponId weapon,
        RangedPhase phase,
        int ticksRemaining)
    {
        if (phase == RangedPhase.None || !Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
        }

        if (ticksRemaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksRemaining));
        }

        return weapon switch
        {
            WeaponId.Bangkaw => ResolveBangkaw(phase),
            WeaponId.Busog => ResolveBusog(phase, ticksRemaining),
            WeaponId.Arquebus => ResolveArquebus(phase, ticksRemaining),
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Not a ranged weapon."),
        };
    }

    // Bangkaw — Long Spear (thrown). PROVISIONAL. Ready carries it diagonally
    // across the body; Load shifts the grip back along the shaft; Draw cocks
    // the arm steeply back past the shoulder with the torso leaning away from
    // the target, the exact precedent being SwingGeometry.PullBackLean; Release
    // sweeps forward past neutral with the largest extension of the three
    // ranged weapons; Recover returns to an empty hand and the weapon line
    // retracts toward vanishing, since the shaft has just been thrown.
    private static RangedPose ResolveBangkaw(RangedPhase phase) => phase switch
    {
        RangedPhase.Ready => new RangedPose(
            phase, WeaponAngleRadians: 0.55f, ExtensionRatio: 0f,
            TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 0f),
        RangedPhase.Load => new RangedPose(
            phase, WeaponAngleRadians: 0.35f, ExtensionRatio: -0.15f,
            TorsoLeanX: -0.10f, TorsoLeanY: 0f, DrawTension: 0f),
        RangedPhase.Draw => new RangedPose(
            phase, WeaponAngleRadians: -1.35f, ExtensionRatio: -0.22f,
            TorsoLeanX: -0.90f, TorsoLeanY: -0.20f, DrawTension: 0f),
        RangedPhase.Release => new RangedPose(
            phase, WeaponAngleRadians: 1.15f, ExtensionRatio: 1.40f,
            TorsoLeanX: 1.00f, TorsoLeanY: 0.20f, DrawTension: 0f),
        RangedPhase.Recover => new RangedPose(
            phase, WeaponAngleRadians: 0.10f, ExtensionRatio: -1.40f,
            TorsoLeanX: -0.05f, TorsoLeanY: 0f, DrawTension: 0f),
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    // Busog — War Bow. PROVISIONAL. The stave stays near-vertical and barely
    // rotates across the whole sequence, per design section 8.4 — every angle
    // below sits within roughly 0.08 radians of the others, in sharp contrast
    // to the Bangkaw's wide swing. DrawTension is the channel that actually
    // carries the readable motion: flat through Load, rising through Draw as
    // ticksRemaining counts down toward Ready, held at its peak through
    // Ready, and snapped to zero on Release independent of ticksRemaining.
    private static RangedPose ResolveBusog(RangedPhase phase, int ticksRemaining) =>
        phase switch
        {
            RangedPhase.Ready => new RangedPose(
                phase, WeaponAngleRadians: 1.49f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 1f),
            RangedPhase.Load => new RangedPose(
                phase, WeaponAngleRadians: 1.46f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 0.10f),
            RangedPhase.Draw => new RangedPose(
                phase, WeaponAngleRadians: 1.43f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f,
                DrawTension: ResolveBusogDrawTension(ticksRemaining)),
            RangedPhase.Release => new RangedPose(
                phase, WeaponAngleRadians: 1.50f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 0f),
            RangedPhase.Recover => new RangedPose(
                phase, WeaponAngleRadians: 1.47f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 0f),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };

    /// <summary>
    /// PROVISIONAL. Monotonically decreasing in <paramref name="ticksRemaining"/>
    /// so the tension is lowest just after entering <see cref="RangedPhase.Draw"/>
    /// (ticks remaining still high) and rises to approach its Ready-phase peak
    /// as ticks remaining falls toward zero. Bounded in <c>(0, 1]</c> for every
    /// non-negative input.
    /// </summary>
    private static float ResolveBusogDrawTension(int ticksRemaining) =>
        1f / (1f + (ticksRemaining * BusogDrawTensionDecayRate));

    // Imported Arquebus. PROVISIONAL. Ready and Draw are the shoulder-and-level
    // motion, a small rotation toward horizontal. Load reads as a multi-beat
    // ramrod motion — the extension channel alternates on the parity of
    // ticksRemaining, one beat pouring and the next ramming, rather than
    // sweeping smoothly — and occupies most of the weapon's very long
    // interval. Release holds one fixed pose regardless of ticksRemaining,
    // deliberately not animated within the phase, because the muzzle flash and
    // recoil pulse are the readable content and this weapon's whole documented
    // purpose was to be noticed.
    private static RangedPose ResolveArquebus(RangedPhase phase, int ticksRemaining) =>
        phase switch
        {
            RangedPhase.Ready => new RangedPose(
                phase, WeaponAngleRadians: 0.05f, ExtensionRatio: 0f,
                TorsoLeanX: 0f, TorsoLeanY: 0f, DrawTension: 0f),
            RangedPhase.Load => new RangedPose(
                phase, WeaponAngleRadians: -0.55f,
                ExtensionRatio: ResolveArquebusLoadBeat(ticksRemaining),
                TorsoLeanX: -0.20f, TorsoLeanY: 0.10f, DrawTension: 0f),
            RangedPhase.Draw => new RangedPose(
                phase, WeaponAngleRadians: -0.15f, ExtensionRatio: 0f,
                TorsoLeanX: 0.15f, TorsoLeanY: 0f, DrawTension: 0f),
            RangedPhase.Release => new RangedPose(
                phase, WeaponAngleRadians: 0.05f, ExtensionRatio: 0.20f,
                TorsoLeanX: -0.35f, TorsoLeanY: 0f, DrawTension: 0f),
            RangedPhase.Recover => new RangedPose(
                phase, WeaponAngleRadians: -0.25f, ExtensionRatio: -0.10f,
                TorsoLeanX: -0.10f, TorsoLeanY: 0f, DrawTension: 0f),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };

    /// <summary>
    /// PROVISIONAL. The alternating pour/ram beat of a multi-beat
    /// <see cref="RangedPhase.Load"/>, keyed off the parity of
    /// <paramref name="ticksRemaining"/> rather than off elapsed time, so it
    /// needs no clock and survives pause exactly as the phase itself does.
    /// </summary>
    private static float ResolveArquebusLoadBeat(int ticksRemaining) =>
        -0.30f + ((ticksRemaining % 2 == 0) ? 0.08f : -0.08f);
}
