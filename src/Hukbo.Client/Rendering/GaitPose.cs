namespace Hukbo.Client.Rendering;

/// <summary>
/// Which of the three stride states a warrior's legs are drawn in, resolved
/// from the per-tick displacement of its authoritative position
/// (<c>GaitGeometry.ResolveMode</c>). Numeric values are not part of any
/// persisted contract; they exist only so <see cref="GaitPose.Mode"/>'s
/// default, <see cref="Stance"/>, lines up with <c>default(GaitPose)</c>
/// being the neutral standing pose.
/// </summary>
internal enum GaitMode
{
    /// <summary>
    /// Feet planted, legs vertical, no motion. Resolved when a warrior's
    /// per-tick displacement is zero.
    /// </summary>
    Stance = 0,

    /// <summary>
    /// Moderate stride, low foot lift, upright torso. Resolved when a
    /// warrior's per-tick displacement is below the run threshold.
    /// </summary>
    Walk = 1,

    /// <summary>
    /// Longer stride, higher foot lift, forward torso lean. Resolved when a
    /// warrior's per-tick displacement is at or above the run threshold.
    /// </summary>
    Run = 2,
}

/// <summary>
/// The pose one warrior's legs, feet, and torso lean are drawn in at one
/// moment, derived from how far it has actually travelled rather than from
/// elapsed time. The neutral standing pose, which is <c>default</c>, is a
/// pawn standing exactly as it does today: <see cref="Mode"/> at
/// <see cref="GaitMode.Stance"/>, every offset and lift at zero, and no
/// direction of travel.
/// </summary>
/// <remarks>
/// This mirrors <c>SwingPose</c>'s shape deliberately: a pure value produced
/// by <c>GaitGeometry</c> from a store's per-entity state, consumed by
/// <c>PawnGeometry</c> the same way the swing pose already is. Unlike
/// <c>SwingPose</c>, nothing here is driven by a clock. <see cref="PhaseTurns"/>
/// advances only in proportion to ground actually covered, so a paused battle,
/// or a battle whose feed the client is not yet ingesting, leaves every field
/// exactly where it was.
/// </remarks>
/// <param name="Mode">Which stride state the legs are drawn in.</param>
/// <param name="PhaseTurns">
/// Progress through one stride cycle, wrapped into <c>[0, 1)</c>. One full
/// cycle is completed per fixed distance travelled
/// (<c>GaitAnimationSystem.StrideCycleDistanceRaw</c>), never per elapsed
/// second, so playback speed and pause need no special handling anywhere in
/// this type or in <c>GaitGeometry</c>.
/// </param>
/// <param name="LeftLegOffsetRatio">
/// The left leg's fore/aft displacement from the neutral hang, as a ratio of
/// leg length before apparent scale is applied. Positive is forward along the
/// screen-space direction of travel once <see cref="DirectionSign"/> is
/// applied by the caller; negative is backward.
/// </param>
/// <param name="RightLegOffsetRatio">
/// The right leg's fore/aft displacement, same units as
/// <see cref="LeftLegOffsetRatio"/>. The two legs swing in opposition: one
/// forward while the other is back, matching the design's "one leg swings
/// forward while the other swings back" rule.
/// </param>
/// <param name="LeftFootLiftRatio">
/// How far the left foot is lifted off the ground, as a ratio of the maximum
/// lift for the current <see cref="Mode"/>, zero when the left foot is
/// planted. Only the leg swinging forward lifts; the leg swinging back stays
/// planted, matching the design's "the lifted foot rises; the planted foot
/// does not" rule.
/// </param>
/// <param name="RightFootLiftRatio">
/// How far the right foot is lifted off the ground, same units as
/// <see cref="LeftFootLiftRatio"/>.
/// </param>
/// <param name="TorsoLeanX">
/// Forward torso offset in pawn units, before apparent scale, already
/// multiplied by <see cref="DirectionSign"/> so a warrior running left leans
/// left and one running right leans right. Zero at <see cref="GaitMode.Stance"/>
/// and <see cref="GaitMode.Walk"/>; nonzero only at <see cref="GaitMode.Run"/>,
/// which is the third of the three channels — alongside stride length and
/// foot lift — that separate a run from a walk at a glance.
/// </param>
/// <param name="TorsoLeanY">
/// Vertical torso offset in pawn units, before apparent scale. This gait pose
/// carries no vertical lean channel; the field exists so the type can gain one
/// without a breaking shape change, and is always zero today.
/// </param>
/// <param name="DirectionSign">
/// The world-X sign of the warrior's most recent nonzero displacement: -1
/// moving toward decreasing X, +1 moving toward increasing X, 0 when standing
/// still or before any displacement has ever been observed. The camera's
/// world-to-screen transform is an axis-aligned scale and translation with no
/// rotation (<c>SpectatorCamera.WorldToScreen</c>), so this sign is also the
/// screen-space direction a caller multiplies fore/aft offsets by.
/// </param>
internal readonly record struct GaitPose(
    GaitMode Mode,
    float PhaseTurns,
    float LeftLegOffsetRatio,
    float RightLegOffsetRatio,
    float LeftFootLiftRatio,
    float RightFootLiftRatio,
    float TorsoLeanX,
    float TorsoLeanY,
    float DirectionSign);
