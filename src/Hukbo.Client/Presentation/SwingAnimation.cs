using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One in-flight weapon swing, held for presentation only. Never authoritative
/// and never hashed.
/// </summary>
/// <remarks>
/// The total duration is budgeted at one attack cooldown period rather than at
/// the figure the animation research suggests: this game defaults to a tick
/// rate of 20 and a cooldown of 5 ticks, so a warrior strikes once every 250
/// milliseconds of simulated time at 1x and a longer swing would still be in
/// recovery when the next blow lands.
/// </remarks>
/// <param name="Sequence">The sequence of the attack event that started it.</param>
/// <param name="AttackerEntityId">The swinging warrior.</param>
/// <param name="TargetEntityId">The warrior swung at.</param>
/// <param name="DirectionX">Unit direction from attacker to target, x.</param>
/// <param name="DirectionY">Unit direction from attacker to target, y.</param>
/// <param name="Resolution">How the defender met the blow.</param>
/// <param name="AgeSeconds">Speed-scaled presentation seconds elapsed.</param>
internal readonly record struct SwingAnimation(
    long Sequence,
    ulong AttackerEntityId,
    ulong TargetEntityId,
    float DirectionX,
    float DirectionY,
    AttackResolution Resolution,
    float AgeSeconds)
{
    /// <summary>
    /// PROVISIONAL. One attack cooldown period at the default tick rate.
    /// </summary>
    public const float TotalSeconds = 0.25f;

    /// <summary>
    /// Progress through the total duration, clamped to zero and one. Declared
    /// here rather than in the geometry so that the store, the geometry, and
    /// the renderer all read one formula.
    /// </summary>
    public float Progress =>
        Math.Clamp(AgeSeconds / TotalSeconds, 0f, 1f);
}
