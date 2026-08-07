using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers the pure phase-to-pose mathematics <see cref="GaitGeometry"/> owns:
/// mode classification from a per-tick displacement, and pose resolution from
/// a mode, a phase, a direction, and the spectator's motion setting.
/// </summary>
public sealed class GaitGeometryTests
{
    [Fact]
    public void ResolveMode_ZeroDisplacementIsStance()
    {
        Assert.Equal(GaitMode.Stance, GaitGeometry.ResolveMode(0f));
    }

    [Fact]
    public void ResolveMode_BelowRunThresholdIsWalk()
    {
        Assert.Equal(GaitMode.Walk, GaitGeometry.ResolveMode(400f));
    }

    [Fact]
    public void ResolveMode_AtOrAboveRunThresholdIsRun()
    {
        Assert.Equal(GaitMode.Run, GaitGeometry.ResolveMode(1600f));
        Assert.Equal(GaitMode.Run, GaitGeometry.ResolveMode(3000f));
    }

    [Fact]
    public void ResolveMode_RejectsANegativeDisplacement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GaitGeometry.ResolveMode(-1f));
    }

    [Fact]
    public void ResolvePose_StanceResolvesTheNeutralPose()
    {
        var pose = GaitGeometry.ResolvePose(
            GaitMode.Stance,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);

        Assert.Equal(GaitMode.Stance, pose.Mode);
        Assert.Equal(0f, pose.LeftLegOffsetRatio);
        Assert.Equal(0f, pose.RightLegOffsetRatio);
        Assert.Equal(0f, pose.LeftFootLiftRatio);
        Assert.Equal(0f, pose.RightFootLiftRatio);
        Assert.Equal(0f, pose.TorsoLeanX);
        Assert.Equal(0f, pose.TorsoLeanY);
    }

    [Fact]
    public void ResolvePose_WalkProducesANonzeroStride()
    {
        var pose = GaitGeometry.ResolvePose(
            GaitMode.Walk,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);

        Assert.Equal(GaitMode.Walk, pose.Mode);
        Assert.NotEqual(0f, pose.LeftLegOffsetRatio);
        Assert.Equal(-pose.LeftLegOffsetRatio, pose.RightLegOffsetRatio, precision: 5);
        // No forward lean at a walk: the design reserves that channel for a
        // run so the two gaits read as distinct rather than the same cycle at
        // two speeds.
        Assert.Equal(0f, pose.TorsoLeanX);
    }

    [Fact]
    public void ResolvePose_RunHasALongerStrideAndALeanThanWalk()
    {
        var walk = GaitGeometry.ResolvePose(
            GaitMode.Walk,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);
        var run = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);

        // Phase 0.25 is the stride's extremity, where the offset magnitude
        // equals the mode's full stride ratio, so comparing here isolates the
        // stride-length channel from the phase itself.
        Assert.True(run.LeftLegOffsetRatio > walk.LeftLegOffsetRatio);
        Assert.True(run.LeftFootLiftRatio > walk.LeftFootLiftRatio);
        Assert.Equal(0f, walk.TorsoLeanX);
        Assert.NotEqual(0f, run.TorsoLeanX);
    }

    [Fact]
    public void ResolvePose_LeanFollowsTheDirectionSign()
    {
        var leaningRight = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);
        var leaningLeft = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: -1f,
            MotionIntensity.Full);

        Assert.True(leaningRight.TorsoLeanX > 0f);
        Assert.Equal(-leaningRight.TorsoLeanX, leaningLeft.TorsoLeanX, precision: 5);
    }

    [Fact]
    public void ResolvePose_OffResolvesTheNeutralPoseAtEveryDisplacement()
    {
        foreach (var mode in new[] { GaitMode.Stance, GaitMode.Walk, GaitMode.Run })
        {
            var pose = GaitGeometry.ResolvePose(
                mode,
                phaseTurns: 0.25f,
                directionSign: 1f,
                MotionIntensity.Off);

            Assert.Equal(default, pose);
        }
    }

    [Fact]
    public void ResolvePose_ReducedAmplitudeIsStrictlyLessThanFull()
    {
        var full = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);
        var reduced = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Reduced);

        Assert.True(reduced.LeftLegOffsetRatio < full.LeftLegOffsetRatio);
        Assert.True(reduced.LeftFootLiftRatio < full.LeftFootLiftRatio);
        Assert.True(reduced.TorsoLeanX < full.TorsoLeanX);
        Assert.True(reduced.LeftLegOffsetRatio > 0f);
    }

    [Fact]
    public void ResolvePose_RejectsAPhaseOutsideTheHalfOpenUnitRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GaitGeometry.ResolvePose(
                GaitMode.Walk,
                phaseTurns: 1f,
                directionSign: 0f,
                MotionIntensity.Full));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GaitGeometry.ResolvePose(
                GaitMode.Walk,
                phaseTurns: -0.01f,
                directionSign: 0f,
                MotionIntensity.Full));
    }

    [Fact]
    public void ResolvePose_RejectsADirectionSignOutsideUnitRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GaitGeometry.ResolvePose(
                GaitMode.Walk,
                phaseTurns: 0f,
                directionSign: 1.5f,
                MotionIntensity.Full));
    }
}
