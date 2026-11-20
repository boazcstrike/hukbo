using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// The reference oracle for nearest-enemy targeting. It reproduces the
/// brute-force loop of <c>BattleSimulation.SelectTargetsAndIntents</c> as an
/// obviously correct O(n^2) scan, so that the ring search of
/// <see cref="TargetingGrid"/> can be compared against it.
/// </summary>
/// <remarks>
/// This type must never call <c>BattleSimulation</c>, <c>TargetingGrid</c>, or
/// any other production helper. Its independence from the code under test is
/// the entire reason it exists, so the squared-distance arithmetic is written
/// out inline here even though the simulation has its own helper. Do not
/// optimise this class; it is a correctness oracle, not a hot path.
/// </remarks>
internal static class NaiveNearestEnemy
{
    /// <summary>The three results the simulation's candidate loop produces.</summary>
    /// <param name="TargetEntityId">
    /// The entity ID of the nearest perceivable enemy, ties broken on the
    /// lower entity ID, or <see langword="null"/> when none is perceivable.
    /// </param>
    /// <param name="SelectedDistance">
    /// That enemy's squared distance, or <see cref="long.MaxValue"/> when
    /// there is none.
    /// </param>
    /// <param name="NearestThreatDistance">
    /// The smallest squared distance among perceivable enemies for which
    /// <c>isThreat</c> holds, or <see cref="long.MaxValue"/>.
    /// </param>
    internal readonly record struct Result(
        ulong? TargetEntityId,
        long SelectedDistance,
        long NearestThreatDistance);

    /// <summary>
    /// Scans every agent for the actor's nearest living, perceivable enemy.
    /// </summary>
    /// <param name="agents">Every agent, living or dead, in any order.</param>
    /// <param name="actor">The searching agent.</param>
    /// <param name="isThreat">
    /// Stands in for "this enemy's weapon is melee": the plain minimum over
    /// squared distance that <c>nearestMeleeThreatSquared</c> takes.
    /// </param>
    internal static Result Find(
        IReadOnlyList<AgentState> agents,
        AgentState actor,
        Func<AgentState, bool> isThreat)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(isThreat);

        var perceptionSquared = checked(
            (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw);
        ulong? targetEntityId = null;
        var selectedDistance = long.MaxValue;
        var nearestThreatDistance = long.MaxValue;

        foreach (var candidate in agents)
        {
            if (!candidate.IsAlive || candidate.FactionId == actor.FactionId)
            {
                continue;
            }

            var deltaX = (long)candidate.XRaw - actor.XRaw;
            var deltaY = (long)candidate.YRaw - actor.YRaw;
            var distance = checked((deltaX * deltaX) + (deltaY * deltaY));
            if (distance > perceptionSquared)
            {
                continue;
            }

            if (isThreat(candidate) && distance < nearestThreatDistance)
            {
                nearestThreatDistance = distance;
            }

            if (distance < selectedDistance ||
                (distance == selectedDistance &&
                    candidate.EntityId < targetEntityId))
            {
                targetEntityId = candidate.EntityId;
                selectedDistance = distance;
            }
        }

        return new Result(targetEntityId, selectedDistance, nearestThreatDistance);
    }
}
