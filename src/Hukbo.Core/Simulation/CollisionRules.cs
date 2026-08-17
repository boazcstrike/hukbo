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
    /// 4.25 world units (4,352 raw), giving an eight-and-a-half-world-unit
    /// diameter that fits inside the twelve-world-unit default attack range with
    /// slack to spare.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The original reason for this value has expired.</b> It was chosen
    /// because 4.5 world units cleared every static validation guard
    /// arithmetically and still reintroduced a follower-trailing mutual-block
    /// deadlock: seed 12 of
    /// <c>LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred</c>
    /// stalled at the tick limit with living counts [9, 9]. Measured 2026-07-28.
    /// The intent-layer stall escape in <c>b9003a9</c> closed that case
    /// afterwards. Re-measured 2026-08-16 with
    /// <c>Hukbo.Tools.DeadlockProbe --survey</c>, 200 seeds and 18 agents per
    /// cell: seed 12 at 4.5 now reaches a faction victory at tick 739, and at
    /// this test's own threshold of
    /// <see cref="FormationRules.MaximumLastStandThresholdAgents"/> both 4.25
    /// and 4.5 stall 0 of 200.
    /// </para>
    /// <para>
    /// <b>What still argues for 4.25 is a different measurement.</b> At the
    /// shipping last-stand threshold of
    /// <see cref="FormationRules.DefaultLastStandThresholdAgents"/>, 4.25 stalls
    /// 0 of 200 seeds and 4.5 stalls 1 of 200 (seed 166). Summed across
    /// thresholds 6 through 9 the totals are 5 stalls for 4.25 and 3 for 4.5, so
    /// neither radius is clean and the radius mostly re-rolls which seeds are
    /// unlucky. Raising this constant is an open tuning question, not a defect
    /// repair; it moves both the state hash and the event hash on every seed, so
    /// it needs its own decision and new golden expectations. Do not raise it
    /// without rerunning that test across every seed.
    /// </para>
    /// </remarks>
    public const int DefaultBodyRadiusRaw = (17 * FixedPoint.Scale) / 4;
}
