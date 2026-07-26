using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

/// <summary>
/// How two living bodies are permitted to share space. Exactly one value is
/// defined; adding another is a new decision record, not an implementation
/// detail. Numeric values are pinned because the policy enters the state hash.
/// </summary>
public enum CollisionPolicy
{
    /// <summary>
    /// Impenetrable discs of one common radius. Penetration between two living
    /// agents is never permitted, in any amount, at the end of any tick.
    /// Exactly touching is clearance, not collision.
    /// </summary>
    Solid = 0,
}

/// <summary>
/// The authoritative reason an agent finished a tick where it did. Written by
/// the collision stage, included in the state hash, and never derived by
/// presentation code. Numeric values are pinned; reordering or renumbering them
/// requires a new preset version and new golden expectations.
/// </summary>
public enum MovementResolution
{
    /// <summary>The agent did not propose movement this tick.</summary>
    None = 0,

    /// <summary>The preferred destination was accepted unchanged.</summary>
    Moved = 1,

    /// <summary>
    /// The agent moved along the preferred direction, shorter than intended.
    /// </summary>
    Truncated = 2,

    /// <summary>The agent moved along one axis only.</summary>
    Slid = 3,

    /// <summary>No legal candidate existed, so the agent held position.</summary>
    Blocked = 4,

    /// <summary>The agent was displaced out of an exact co-location.</summary>
    Separated = 5,
}

/// <summary>
/// Approved constants for the solid-disc contact model. These are game-design
/// inventions, not historical measurements.
/// </summary>
public static class CollisionRules
{
    /// <summary>
    /// The common body radius every living agent uses, in raw fixed-point units.
    /// Four world units, giving an eight-world-unit diameter that fits inside
    /// the twelve-world-unit default attack range with slack to spare.
    /// </summary>
    public const int DefaultBodyRadiusRaw = 4 * FixedPoint.Scale;
}
