using System;
using Sandata.Client.Audio;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// The client half of the 2026-08-12 smoke package
/// (docs/plans/2026-08-12-sandata-order-and-combat-legibility.md), smoke row
/// <c>SD-5</c>: the fire mode the simulation chose has to reach the sound
/// catalog, and a burst whose loop sample does not exist on disk has to stay
/// audible rather than falling silent.
/// </summary>
public sealed class AutomaticFireAudioTests
{
    private const CaliberFamily Rifle = CaliberFamily.Cal762X39;
    private const int CloseRangeWu = 100;

    /// <summary>
    /// An <see cref="ISandataSoundOutput"/> that declines exactly the
    /// families it is told to and records every cue it was handed. Declining
    /// is what a real <c>MonoGameSandataSoundOutput</c> does for a slot with
    /// no file on disk, which today is every <see cref="SoundFamily.GunLoop"/>
    /// slot in the catalog.
    /// </summary>
    private sealed class SelectiveSoundOutput(params SoundFamily[] declinedFamilies) : ISandataSoundOutput
    {
        private readonly int[] _playCounts = new int[Enum.GetValues<SoundFamily>().Length];

        public bool Play(SoundSlot slot, int variantNumber, ulong shooterEntityId)
        {
            if (Array.IndexOf(declinedFamilies, slot.Family) >= 0)
            {
                return false;
            }

            _playCounts[(int)slot.Family]++;
            return true;
        }

        public int PlayCount(SoundFamily family) => _playCounts[(int)family];
    }

    private static SandataSoundPlayer NewPlayer(ISandataSoundOutput output) =>
        new(output, new SandataSoundBudget());

    /// <summary>
    /// The model design section 10 actually specifies, unchanged by this
    /// package: when a loop sample exists, a burst plays one loop instance and
    /// no per-round reports, however many rounds it carries.
    /// </summary>
    [Fact]
    public void AutomaticFire_WithALoopSampleAvailable_PlaysOneLoopAndNoReports()
    {
        var output = new SelectiveSoundOutput();
        var player = NewPlayer(output);

        for (var tick = 0L; tick < 20; tick++)
        {
            player.HandleShotFired(
                Rifle, FireMode.Auto, CloseRangeWu,
                shooterIsIndoors: false, suppressorFitted: false, tick, shooterEntityId: 1UL);
        }

        Assert.Equal(1, output.PlayCount(SoundFamily.GunLoop));
        Assert.Equal(0, output.PlayCount(SoundFamily.GunReport));
    }

    /// <summary>
    /// The degradation this package adds, and the reason it exists: no
    /// <see cref="SoundFamily.GunLoop"/> file is on disk today — the
    /// authorized audio slice is four single-shot rows — so wiring automatic
    /// fire without a fallback would have replaced audible single shots with
    /// silence.
    /// </summary>
    [Fact]
    public void AutomaticFire_WithNoLoopSample_FallsBackToOneReportPerRound()
    {
        var output = new SelectiveSoundOutput(SoundFamily.GunLoop);
        var player = NewPlayer(output);

        for (var tick = 0L; tick < 20; tick++)
        {
            player.HandleShotFired(
                Rifle, FireMode.Auto, CloseRangeWu,
                shooterIsIndoors: false, suppressorFitted: false, tick, shooterEntityId: 1UL);
        }

        Assert.Equal(0, output.PlayCount(SoundFamily.GunLoop));
        Assert.Equal(20, output.PlayCount(SoundFamily.GunReport));
    }

    /// <summary>
    /// The fallback is scoped to one burst: once fire stops, the next burst
    /// attempts the loop again rather than assuming it will fail forever. A
    /// loop file added between two bursts is heard on the second.
    /// </summary>
    [Fact]
    public void AutomaticFire_AfterTheBurstEnds_AttemptsTheLoopAgainOnTheNextBurst()
    {
        var output = new SelectiveSoundOutput(SoundFamily.GunLoop);
        var player = NewPlayer(output);

        player.HandleShotFired(
            Rifle, FireMode.Auto, CloseRangeWu,
            shooterIsIndoors: false, suppressorFitted: false, tick: 0, shooterEntityId: 1UL);
        Assert.Equal(1, output.PlayCount(SoundFamily.GunReport));

        player.HandleAutomaticFireStopped(
            Rifle, CloseRangeWu, shooterIsIndoors: false, suppressorFitted: false,
            tick: 1, shooterEntityId: 1UL);

        // Far enough past the loop slot's own tail for the reservation to have
        // expired, so the next round takes a fresh one and re-attempts the
        // loop rather than renewing a burst that already ended.
        player.HandleShotFired(
            Rifle, FireMode.Auto, CloseRangeWu,
            shooterIsIndoors: false, suppressorFitted: false, tick: 500, shooterEntityId: 1UL);

        Assert.Equal(2, output.PlayCount(SoundFamily.GunReport));
    }

    /// <summary>
    /// A single-shot round is untouched by any of the above: it resolves to a
    /// report directly, with no loop attempt and no fallback path.
    /// </summary>
    [Fact]
    public void SingleFire_PlaysOneReportPerRoundRegardlessOfTheLoopSlot()
    {
        var output = new SelectiveSoundOutput(SoundFamily.GunLoop);
        var player = NewPlayer(output);

        for (var tick = 0L; tick < 3; tick++)
        {
            player.HandleShotFired(
                Rifle, FireMode.Single, CloseRangeWu,
                shooterIsIndoors: false, suppressorFitted: false, tick, shooterEntityId: 1UL);
        }

        Assert.Equal(3, output.PlayCount(SoundFamily.GunReport));
        Assert.Equal(0, output.PlayCount(SoundFamily.GunLoop));
    }

    /// <summary>
    /// The seam between <c>Sandata.Core</c>'s <see cref="FireModeSet"/>, which
    /// the simulation now carries on every <c>ShotFired</c> event, and the
    /// client audio catalog's own <see cref="FireMode"/> axis. Before
    /// 2026-08-12 the client passed the literal <see cref="FireMode.Single"/>
    /// for every shot in the game, so no automatic cue was reachable at all.
    /// </summary>
    [Fact]
    public void ToAudioFireMode_MapsEveryFiringModeAndFallsBackToSingle()
    {
        Assert.Equal(FireMode.Auto, SandataGame.ToAudioFireMode(FireModeSet.Auto));
        Assert.Equal(FireMode.Burst3, SandataGame.ToAudioFireMode(FireModeSet.Burst3));
        Assert.Equal(FireMode.Burst2, SandataGame.ToAudioFireMode(FireModeSet.Burst2));
        Assert.Equal(FireMode.Single, SandataGame.ToAudioFireMode(FireModeSet.Single));

        // Neither of these is reachable through FireModeSelection.SelectMode,
        // which never returns Safe and never returns a combination; they pin
        // the fallback rather than a real simulation outcome.
        Assert.Equal(FireMode.Single, SandataGame.ToAudioFireMode(FireModeSet.Safe));
        Assert.Equal(FireMode.Single, SandataGame.ToAudioFireMode((FireModeSet)0));
    }
}
