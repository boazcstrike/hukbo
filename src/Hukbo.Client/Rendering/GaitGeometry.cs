using Hukbo.Client.Settings;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from a warrior's per-tick displacement and stride phase to a
/// <see cref="GaitPose"/>, mirroring <see cref="AttackGeometry"/>'s shape: a
/// static class over value types only, with no store, no clock, and no
/// dependency on anything that can fail to be present in a unit test.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ResolveMode"/> classifies a per-tick displacement into one of
/// the three stride states. <see cref="ResolvePose"/> then turns a mode, a
/// phase, and a direction into the drawable pose, folding in the spectator's
/// <see cref="MotionIntensity"/> setting at the same step: <c>Off</c> resolves
/// the neutral standing pose unconditionally, so a caller never needs a second
/// branch to special-case it, and <c>Reduced</c> scales stride and lift by the
/// same factor <see cref="GrassSway"/> already uses for its own reduced
/// amplitude, reusing that constant rather than minting a parallel one.
/// </para>
/// <para>
/// Every amplitude, threshold, and lean value here is a drawing choice, not a
/// measurement, and is marked PROVISIONAL. Nothing here reads a clock, and
/// nothing here can throw for any input a caller following its documented
/// range would ever pass.
/// </para>
/// </remarks>
internal static class GaitGeometry
{
    /// <summary>
    /// PROVISIONAL. Per-tick displacement, in the same raw fixed-point units
    /// <c>AgentView.XRaw</c>/<c>YRaw</c> already use, at or above which a
    /// warrior is classified as running rather than walking. Chosen below the
    /// default <c>Scenario.MovementSpeedRaw</c> cap of 3072 raw units per
    /// tick, so a warrior moving near its top speed reads as running while one
    /// merely repositioning reads as walking.
    /// </summary>
    private const float RunThresholdRawPerTick = 1600f;

    /// <summary>
    /// Per-tick displacement, in the same raw units as
    /// <see cref="RunThresholdRawPerTick"/>, below which a warrior is
    /// classified as standing still rather than walking. This is a legibility
    /// threshold derived from a stated criterion, not a provisional tuning
    /// guess: <c>CrawlThresholdRawPerTick = StrideCycleDistanceRaw /
    /// (MaxLegibleStrideCycleSeconds * TickRate) = 6000 / (5 * 20) = 60</c>.
    /// A stride slower than one full cycle every five seconds is not one a
    /// spectator can read as walking. At 60 raw units per tick a cycle takes
    /// 100 ticks (5 s); at the pinned walk magnitude of 400 it takes 15 ticks
    /// (0.75 s); at the arrival-taper floor of 1 raw unit per tick it would
    /// have taken 6000 ticks (300 s) and now rests in the neutral stance
    /// instead. For scale, full unimpeded speed is
    /// <c>MovementSpeedRaw = 3 * FixedPoint.Scale = 3072</c> raw units per
    /// tick.
    /// </summary>
    private const float CrawlThresholdRawPerTick = 60f;

    /// <summary>
    /// PROVISIONAL. Fore/aft leg swing at <see cref="GaitMode.Walk"/>, as a
    /// ratio of leg length before apparent scale.
    /// </summary>
    private const float WalkStrideRatio = 0.32f;

    /// <summary>
    /// PROVISIONAL. Fore/aft leg swing at <see cref="GaitMode.Run"/>, longer
    /// than <see cref="WalkStrideRatio"/> so the two modes read as distinct
    /// gaits rather than the same stride played faster.
    /// </summary>
    private const float RunStrideRatio = 0.60f;

    /// <summary>PROVISIONAL. Foot lift at <see cref="GaitMode.Walk"/>.</summary>
    private const float WalkFootLiftRatio = 0.15f;

    /// <summary>
    /// PROVISIONAL. Foot lift at <see cref="GaitMode.Run"/>, higher than
    /// <see cref="WalkFootLiftRatio"/> for the same reason the stride is
    /// longer.
    /// </summary>
    private const float RunFootLiftRatio = 0.38f;

    /// <summary>
    /// PROVISIONAL. Forward torso lean at <see cref="GaitMode.Run"/>, in pawn
    /// units before apparent scale. Zero at <see cref="GaitMode.Walk"/> and
    /// <see cref="GaitMode.Stance"/>, per the design's "upright torso" reading
    /// for a walk.
    /// </summary>
    private const float RunTorsoLeanRatio = 0.18f;

    /// <summary>
    /// Classifies a warrior's per-tick displacement into a stride state.
    /// Anything below <see cref="CrawlThresholdRawPerTick"/>, including exactly
    /// zero, resolves <see cref="GaitMode.Stance"/>; anything from there below
    /// <see cref="RunThresholdRawPerTick"/> resolves <see cref="GaitMode.Walk"/>;
    /// anything at or above <see cref="RunThresholdRawPerTick"/> resolves
    /// <see cref="GaitMode.Run"/>.
    /// </summary>
    /// <param name="displacementRawPerTick">
    /// The magnitude of the change in a warrior's raw position between one
    /// ingested tick and the next, in the same raw units as
    /// <c>AgentView.XRaw</c>/<c>YRaw</c>. Never negative, since it is a
    /// magnitude.
    /// </param>
    public static GaitMode ResolveMode(float displacementRawPerTick)
    {
        if (!float.IsFinite(displacementRawPerTick) ||
            displacementRawPerTick < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementRawPerTick));
        }

        if (displacementRawPerTick < CrawlThresholdRawPerTick)
        {
            return GaitMode.Stance;
        }

        return displacementRawPerTick >= RunThresholdRawPerTick
            ? GaitMode.Run
            : GaitMode.Walk;
    }

    /// <summary>
    /// Resolves the drawable pose for one warrior at one moment.
    /// </summary>
    /// <param name="mode">
    /// The stride state, as classified by <see cref="ResolveMode"/>.
    /// </param>
    /// <param name="phaseTurns">
    /// Progress through one stride cycle, wrapped into <c>[0, 1)</c> by the
    /// caller before this is called.
    /// </param>
    /// <param name="directionSign">
    /// The world-X sign of the warrior's direction of travel, in
    /// <c>[-1, 1]</c>. Carried straight into the returned
    /// <see cref="GaitPose.DirectionSign"/> and used here to sign the forward
    /// lean.
    /// </param>
    /// <param name="motionIntensity">
    /// The spectator's ambient-motion setting.
    /// <see cref="MotionIntensity.Off"/> resolves the neutral standing pose —
    /// <c>default(GaitPose)</c> — regardless of every other argument, so the
    /// legs and feet are still drawn (by <c>PawnGeometry</c>, from a
    /// stance-mode pose) but never animate.
    /// <see cref="MotionIntensity.Reduced"/> scales stride, lift, and lean by
    /// <see cref="GrassSway.ReducedAmplitudeFactor"/>. <see cref="MotionIntensity.Full"/>
    /// applies no scaling.
    /// </param>
    /// <param name="suppressForwardLean">
    /// Suppresses the forward lean while leaving stride and lift untouched.
    /// The lean is signed by the direction of travel, so a warrior stepping
    /// backwards leans towards where it is going — which reads as a rout
    /// rather than as a measured withdrawal. The caller sets this for a
    /// warrior that is giving ground deliberately. It is a plain flag rather
    /// than a simulation enum on purpose: this type decides how a body is
    /// drawn and has no business knowing what an evasive action is.
    /// </param>
    public static GaitPose ResolvePose(
        GaitMode mode,
        float phaseTurns,
        float directionSign,
        MotionIntensity motionIntensity,
        bool suppressForwardLean = false)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        if (!float.IsFinite(phaseTurns) || phaseTurns < 0f || phaseTurns >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(phaseTurns));
        }

        if (!float.IsFinite(directionSign) ||
            directionSign < -1f ||
            directionSign > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(directionSign));
        }

        if (!Enum.IsDefined(motionIntensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(motionIntensity),
                motionIntensity,
                "Unknown motion intensity.");
        }

        if (motionIntensity == MotionIntensity.Off)
        {
            return default;
        }

        var amplitudeFactor = motionIntensity == MotionIntensity.Reduced
            ? GrassSway.ReducedAmplitudeFactor
            : 1f;

        var (strideRatio, liftRatio, leanRatio) = mode switch
        {
            GaitMode.Stance => (0f, 0f, 0f),
            GaitMode.Walk => (WalkStrideRatio, WalkFootLiftRatio, 0f),
            GaitMode.Run => (RunStrideRatio, RunFootLiftRatio, RunTorsoLeanRatio),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        var angle = MathF.Tau * phaseTurns;
        var sine = MathF.Sin(angle);
        var leftOffset = sine * strideRatio * amplitudeFactor;
        var rightOffset = -leftOffset;
        var leftLift = MathF.Max(0f, sine) * liftRatio * amplitudeFactor;
        var rightLift = MathF.Max(0f, -sine) * liftRatio * amplitudeFactor;
        // Stride and lift are untouched: the warrior is still walking, and
        // freezing its legs would trade one wrong reading for another. Only
        // the lean goes, because only the lean says which way the body is
        // committed.
        var leanX = suppressForwardLean
            ? 0f
            : leanRatio * amplitudeFactor * directionSign;

        return new GaitPose(
            mode,
            phaseTurns,
            leftOffset,
            rightOffset,
            leftLift,
            rightLift,
            leanX,
            TorsoLeanY: 0f,
            directionSign);
    }
}
