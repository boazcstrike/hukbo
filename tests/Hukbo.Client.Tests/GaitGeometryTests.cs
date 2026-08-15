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
    public void ResolveMode_BelowCrawlThresholdIsStance()
    {
        Assert.Equal(GaitMode.Stance, GaitGeometry.ResolveMode(1f));
        Assert.Equal(GaitMode.Stance, GaitGeometry.ResolveMode(59f));
    }

    [Fact]
    public void ResolveMode_AtCrawlThresholdIsWalk()
    {
        Assert.Equal(GaitMode.Walk, GaitGeometry.ResolveMode(60f));
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

    /// <summary>
    /// A warrior giving ground keeps its stride and its foot lift and loses
    /// only the forward lean. The lean is signed by the direction of travel, so
    /// without this a warrior stepping backwards leans towards where it is
    /// retreating and reads as routing — the opposite of a measured withdrawal.
    /// </summary>
    [Fact]
    public void ResolvePose_SuppressingTheForwardLeanKeepsStrideAndLift()
    {
        var leaning = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: -1f,
            MotionIntensity.Full);
        var level = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: -1f,
            MotionIntensity.Full,
            suppressForwardLean: true);

        Assert.NotEqual(0f, leaning.TorsoLeanX);
        Assert.Equal(0f, level.TorsoLeanX);

        Assert.Equal(leaning.LeftLegOffsetRatio, level.LeftLegOffsetRatio);
        Assert.Equal(leaning.RightLegOffsetRatio, level.RightLegOffsetRatio);
        Assert.Equal(leaning.LeftFootLiftRatio, level.LeftFootLiftRatio);
        Assert.Equal(leaning.RightFootLiftRatio, level.RightFootLiftRatio);
        Assert.Equal(leaning.DirectionSign, level.DirectionSign);
    }

    /// <summary>
    /// The flag defaults to <see langword="false"/>, so every call site that
    /// existed before it was added resolves exactly the pose it always did.
    /// </summary>
    [Fact]
    public void ResolvePose_DefaultsToLeaningSoExistingCallersAreUnchanged()
    {
        var defaulted = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full);
        var explicitlyLeaning = GaitGeometry.ResolvePose(
            GaitMode.Run,
            phaseTurns: 0.25f,
            directionSign: 1f,
            MotionIntensity.Full,
            suppressForwardLean: false);

        Assert.Equal(defaulted, explicitlyLeaning);
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
