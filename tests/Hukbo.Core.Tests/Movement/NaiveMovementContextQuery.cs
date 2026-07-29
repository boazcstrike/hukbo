using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The independent O(n²) oracle of the weapon-relative movement design,
/// section 7.4. Deliberately written from the design prose rather than from
/// the production code: it materializes candidate lists, filters them with
/// LINQ, and sorts by the specified total order — squared distance, then
/// lower entity id — instead of tracking incumbents in one pass, so a
/// production mistake cannot be mirrored here by construction. Allocation
/// and speed are non-goals; this type exists only inside tests.
/// </summary>
internal static class NaiveMovementContextQuery
{
    /// <summary>
    /// One candidate body, decoupled from <c>AgentState</c> so the oracle
    /// can be fed from pre-tick <c>AgentView</c> snapshots as well as from
    /// hand-built states.
    /// </summary>
    internal sealed record Body(
        ulong EntityId,
        int FactionId,
        int XRaw,
        int YRaw,
        bool IsAlive,
        CombatLoadout Loadout,
        int PerceptionRangeRaw);

    /// <summary>
    /// Computes the actor's expected <see cref="LocalMovementContext"/>. The
    /// actor may appear in <paramref name="bodies"/> and is recognized by
    /// entity id. Membership at the exact radius is inclusive; dead bodies
    /// count nowhere; enemies count only inside the actor's perception
    /// range, matching the observation stage's perception gate; allies carry
    /// no perception test.
    /// </summary>
    internal static LocalMovementContext Compute(
        IReadOnlyList<Body> bodies,
        Body actor,
        ulong? selectedTargetEntityId,
        Int128 immediateRadiusSquared,
        Int128 supportRadiusSquared)
    {
        var perceptionSquared = (Int128)checked(
            (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw);
        var living = bodies
            .Where(body => body.IsAlive && body.EntityId != actor.EntityId)
            .ToList();

        var allies = living
            .Where(body => body.FactionId == actor.FactionId)
            .ToList();
        var perceivedEnemies = living
            .Where(body =>
                body.FactionId != actor.FactionId &&
                Squared(actor, body) <= perceptionSquared)
            .ToList();

        var supportAllies = allies
            .Where(body => Squared(actor, body) <= supportRadiusSquared)
            .ToList();
        var supportEnemies = perceivedEnemies
            .Where(body => Squared(actor, body) <= supportRadiusSquared)
            .ToList();
        var immediateEnemies = perceivedEnemies
            .Where(body => Squared(actor, body) <= immediateRadiusSquared)
            .ToList();

        var alliedComposition = supportAllies
            .Select(body => body.Loadout)
            .Append(actor.Loadout)
            .Aggregate(
                default(LoadoutCompositionCounts),
                (counts, loadout) => counts.Add(loadout));
        var enemyComposition = supportEnemies
            .Select(body => body.Loadout)
            .Aggregate(
                default(LoadoutCompositionCounts),
                (counts, loadout) => counts.Add(loadout));

        var nearestAlly = supportAllies
            .OrderBy(body => Squared(actor, body))
            .ThenBy(body => body.EntityId)
            .FirstOrDefault();
        var secondThreat = immediateEnemies
            .Where(body => body.EntityId != selectedTargetEntityId)
            .OrderBy(body => Squared(actor, body))
            .ThenBy(body => body.EntityId)
            .FirstOrDefault();

        return new LocalMovementContext(
            allies.Count(body => Squared(actor, body) <= immediateRadiusSquared),
            immediateEnemies.Count,
            supportAllies.Count + 1,
            supportEnemies.Count,
            alliedComposition,
            enemyComposition,
            nearestAlly?.EntityId,
            secondThreat?.EntityId);
    }

    /// <summary>
    /// The target the observation stage's selection spec picks for the
    /// actor: the nearest living enemy inside perception, ties broken on
    /// lower entity id, or <see langword="null"/> when none is perceived.
    /// Re-derived from the specification here so the oracle never has to
    /// trust production state it is checking.
    /// </summary>
    internal static ulong? ExpectedSelectedTarget(
        IReadOnlyList<Body> bodies,
        Body actor)
    {
        var perceptionSquared = (Int128)checked(
            (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw);
        return bodies
            .Where(body =>
                body.IsAlive &&
                body.FactionId != actor.FactionId &&
                Squared(actor, body) <= perceptionSquared)
            .OrderBy(body => Squared(actor, body))
            .ThenBy(body => body.EntityId)
            .FirstOrDefault()?.EntityId;
    }

    private static Int128 Squared(Body actor, Body candidate)
    {
        var deltaX = (Int128)candidate.XRaw - actor.XRaw;
        var deltaY = (Int128)candidate.YRaw - actor.YRaw;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
