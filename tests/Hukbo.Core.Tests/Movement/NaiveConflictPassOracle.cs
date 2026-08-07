using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// An independent, deliberately naive implementation of the
/// friendly-clearance conflict pass of the weapon-relative movement design,
/// section 10.6, written against the design text rather than against the
/// production code: sort every proposal by phase safety then ascending
/// <c>EntityId</c>, and greedily accept each one whose endpoint lies at or
/// beyond the larger clearance radius from every already accepted endpoint,
/// with exact equality accepting. Production must match this oracle's
/// accepted set exactly.
/// </summary>
internal static class NaiveConflictPassOracle
{
    /// <summary>
    /// The design's phase-safety order, restated independently so an
    /// accidental production reorder cannot hide behind a shared constant.
    /// </summary>
    private static int SafetyRank(FootworkPhase phase) =>
        phase switch
        {
            FootworkPhase.Disengage => 0,
            FootworkPhase.Recover => 1,
            FootworkPhase.Commit => 2,
            FootworkPhase.Regroup => 3,
            FootworkPhase.Engage => 4,
            FootworkPhase.Approach => 5,
            FootworkPhase.Pursue => 6,
            _ => 7,
        };

    /// <summary>
    /// Returns the set of accepted <c>EntityId</c> values for one faction's
    /// proposals, in any input order.
    /// </summary>
    internal static HashSet<ulong> AcceptedEntityIds(
        IReadOnlyList<FriendlyClearanceProposal> proposals)
    {
        var ordered = proposals
            .OrderBy(proposal => SafetyRank(proposal.Phase))
            .ThenBy(proposal => proposal.EntityId)
            .ToList();
        var accepted = new List<FriendlyClearanceProposal>();

        foreach (var candidate in ordered)
        {
            var clear = true;
            foreach (var extant in accepted)
            {
                var required = Int128.Max(
                    candidate.ClearanceRadiusSquared,
                    extant.ClearanceRadiusSquared);
                var separation = (Int128)CollisionGeometry.SquaredDistance(
                    candidate.EndpointXRaw,
                    candidate.EndpointYRaw,
                    extant.EndpointXRaw,
                    extant.EndpointYRaw);
                if (separation < required)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
            {
                accepted.Add(candidate);
            }
        }

        return accepted.Select(proposal => proposal.EntityId).ToHashSet();
    }
}
