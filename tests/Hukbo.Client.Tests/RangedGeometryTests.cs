using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers the pure phase-to-pose mathematics <see cref="RangedGeometry"/>
/// owns for the three ranged weapons, and the per-weapon differentiation
/// design section 8.4 of <c>docs/plans/2026-08-07-ranged-units-design.md</c>
/// requires.
/// </summary>
public sealed class RangedGeometryTests
{
    private static readonly RangedPhase[] AllFivePhases =
    [
        RangedPhase.Ready,
        RangedPhase.Load,
        RangedPhase.Draw,
        RangedPhase.Release,
        RangedPhase.Recover,
    ];

    public static IEnumerable<object[]> RangedWeapons()
    {
        yield return [WeaponId.Bangkaw];
        yield return [WeaponId.Busog];
        yield return [WeaponId.Arquebus];
    }

    [Theory]
    [MemberData(nameof(RangedWeapons))]
    public void ResolvePose_EachOfTheFivePhasesIsVisiblyDistinctPerWeapon(
        WeaponId weapon)
    {
        var poses = AllFivePhases
            .Select(phase => RangedGeometry.ResolvePose(weapon, phase, ticksRemaining: 5))
            .ToArray();

        for (var i = 0; i < poses.Length; i++)
        {
            for (var j = i + 1; j < poses.Length; j++)
            {
                Assert.NotEqual(poses[i], poses[j]);
            }
        }
    }

    [Fact]
    public void ResolvePose_TheSamePhaseDiffersAcrossTheThreeWeapons()
    {
        foreach (var phase in AllFivePhases)
        {
            var bangkaw = RangedGeometry.ResolvePose(WeaponId.Bangkaw, phase, 5);
            var busog = RangedGeometry.ResolvePose(WeaponId.Busog, phase, 5);
            var arquebus = RangedGeometry.ResolvePose(WeaponId.Arquebus, phase, 5);

            Assert.NotEqual(bangkaw, busog);
            Assert.NotEqual(busog, arquebus);
            Assert.NotEqual(bangkaw, arquebus);
        }
    }

    [Fact]
    public void ResolvePose_BangkawDrawCocksBackWithANegativeTorsoLean()
    {
        var pose = RangedGeometry.ResolvePose(WeaponId.Bangkaw, RangedPhase.Draw, 5);

        Assert.True(pose.TorsoLeanX < 0f);
        Assert.True(pose.WeaponAngleRadians < -1f, "expected a steep cocked-back angle past the shoulder");
    }

    [Fact]
    public void ResolvePose_BangkawWeaponLineShortensThroughRecoverMoreThanAnyOtherPhase()
    {
        var recover = RangedGeometry.ResolvePose(WeaponId.Bangkaw, RangedPhase.Recover, 5);

        foreach (var phase in AllFivePhases.Where(p => p != RangedPhase.Recover))
        {
            var other = RangedGeometry.ResolvePose(WeaponId.Bangkaw, phase, 5);
            Assert.True(
                recover.ExtensionRatio < other.ExtensionRatio,
                $"expected Recover's extension ({recover.ExtensionRatio}) to be more retracted than {phase}'s ({other.ExtensionRatio})");
        }
    }

    [Fact]
    public void ResolvePose_BusogStaveStaysNearVerticalAcrossEveryPhase()
    {
        var angles = AllFivePhases
            .Select(phase => RangedGeometry.ResolvePose(WeaponId.Busog, phase, 5).WeaponAngleRadians)
            .ToArray();

        foreach (var angle in angles)
        {
            Assert.InRange(angle, 1.30f, 1.60f);
        }

        // "Barely rotates": the full spread across the five phases stays far
        // inside the Bangkaw's cocked-back-to-follow-through swing.
        Assert.True(angles.Max() - angles.Min() < 0.30f);
    }

    [Fact]
    public void ResolvePose_BusogDrawTensionRisesAsTicksRemainingFallsTowardReady()
    {
        var early = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Draw, ticksRemaining: 20);
        var late = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Draw, ticksRemaining: 2);

        Assert.True(
            late.DrawTension > early.DrawTension,
            "expected tension closer to Ready (fewer ticks remaining) to be higher");
    }

    [Fact]
    public void ResolvePose_BusogTensionHoldsAtItsPeakThroughReady()
    {
        var atFiveTicks = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Ready, 5);
        var atZeroTicks = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Ready, 0);

        Assert.Equal(1f, atFiveTicks.DrawTension);
        Assert.Equal(atFiveTicks, atZeroTicks);
    }

    [Fact]
    public void ResolvePose_BusogTensionSnapsToZeroOnReleaseRegardlessOfTicksRemaining()
    {
        var highTicks = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Release, 40);
        var lowTicks = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Release, 1);

        Assert.Equal(0f, highTicks.DrawTension);
        Assert.Equal(highTicks, lowTicks);
    }

    [Fact]
    public void ResolvePose_ArquebusHoldsItsReleasePoseRegardlessOfTicksRemaining()
    {
        var highTicks = RangedGeometry.ResolvePose(WeaponId.Arquebus, RangedPhase.Release, 50);
        var lowTicks = RangedGeometry.ResolvePose(WeaponId.Arquebus, RangedPhase.Release, 1);

        Assert.Equal(highTicks, lowTicks);
    }

    [Fact]
    public void ResolvePose_ArquebusSpendsItsLoadOnAMultiBeatMotionThatVariesWithTicksRemaining()
    {
        var evenBeat = RangedGeometry.ResolvePose(WeaponId.Arquebus, RangedPhase.Load, 4);
        var oddBeat = RangedGeometry.ResolvePose(WeaponId.Arquebus, RangedPhase.Load, 5);

        Assert.NotEqual(evenBeat.ExtensionRatio, oddBeat.ExtensionRatio);
    }

    [Theory]
    [MemberData(nameof(RangedWeapons))]
    public void ResolvePose_RejectsRangedPhaseNone(WeaponId weapon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedGeometry.ResolvePose(weapon, RangedPhase.None, 0));
    }

    [Theory]
    [MemberData(nameof(RangedWeapons))]
    public void ResolvePose_RejectsAnUndefinedPhase(WeaponId weapon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedGeometry.ResolvePose(weapon, (RangedPhase)99, 0));
    }

    [Theory]
    [MemberData(nameof(RangedWeapons))]
    public void ResolvePose_RejectsNegativeTicksRemaining(WeaponId weapon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedGeometry.ResolvePose(weapon, RangedPhase.Ready, -1));
    }

    [Fact]
    public void ResolvePose_RejectsAMeleeWeapon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangedGeometry.ResolvePose(WeaponId.Kampilan, RangedPhase.Ready, 0));
    }
}
