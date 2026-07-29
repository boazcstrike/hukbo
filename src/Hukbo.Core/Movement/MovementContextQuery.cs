using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Movement;

/// <summary>
/// The pure local-context query of the weapon-relative movement design,
/// section 7.4. <see cref="Derive"/> computes one warrior's
/// <see cref="LocalMovementContext"/> from an explicitly supplied span, so a
/// test can permute storage order — <c>BattleSimulation.CreateForTesting</c>
/// canonicalizes agents by <c>EntityId</c> and proves nothing about order
/// independence. The production observation inside
/// <c>BattleSimulation.SelectTargetsAndIntents</c> accumulates through the
/// same <see cref="MovementContextAccumulator"/> this method drives, fused
/// into its existing candidate loop so the already-computed deltas and
/// squared distance are reused (design section 7.3).
/// </summary>
internal static class MovementContextQuery
{
    /// <summary>
    /// The basis-point denominator every body-diameter-relative radius is
    /// scaled through (design section 4.4).
    /// </summary>
    private const long BasisPointDenominator = 10_000;

    /// <summary>
    /// Materializes a context radius from basis points of body diameter as a
    /// raw <see langword="long"/>, truncating toward zero after the widened
    /// multiply (design sections 4.4 and 7.2). Deliberately unsaturated: a
    /// valid large-body scenario can push a radius past <see cref="int"/>
    /// without being invalid.
    /// </summary>
    internal static long ContextRadiusRaw(
        int bodyRadiusRaw,
        int bodyDiametersBasisPoints)
    {
        var bodyDiameterRaw = checked(2L * bodyRadiusRaw);
        return checked(bodyDiameterRaw * bodyDiametersBasisPoints) /
            BasisPointDenominator;
    }

    /// <summary>
    /// Squares a raw radius through <see cref="Int128"/> so the comparison
    /// against a squared distance can never overflow (design section 4.4).
    /// </summary>
    internal static Int128 SquaredContextRadius(long radiusRaw) =>
        checked((Int128)radiusRaw * radiusRaw);

    /// <summary>
    /// Derives <paramref name="actor"/>'s local movement context from
    /// <paramref name="agents"/> in one forward scan. Pure: reads the span,
    /// writes nothing, allocates nothing. The result is independent of the
    /// span's storage order because every multi-result field carries a total
    /// order — squared distance, then lower <c>EntityId</c>.
    /// </summary>
    /// <param name="agents">
    /// The candidate agents, in any order. The actor itself may appear and is
    /// recognized by <c>EntityId</c>.
    /// </param>
    /// <param name="actor">The living warrior whose context is derived.</param>
    /// <param name="selectedTargetEntityId">
    /// The target the tick's selection stage chose for the actor, excluded
    /// from <see cref="LocalMovementContext.SecondThreatEntityId"/>.
    /// </param>
    /// <param name="immediateRadiusSquared">
    /// The squared immediate radius, from <see cref="SquaredContextRadius"/>.
    /// </param>
    /// <param name="supportRadiusSquared">
    /// The squared support radius, from <see cref="SquaredContextRadius"/>.
    /// </param>
    internal static LocalMovementContext Derive(
        ReadOnlySpan<AgentState> agents,
        AgentState actor,
        ulong? selectedTargetEntityId,
        Int128 immediateRadiusSquared,
        Int128 supportRadiusSquared)
    {
        var accumulator = new MovementContextAccumulator(
            actor.Loadout, immediateRadiusSquared, supportRadiusSquared);
        var perceptionSquared = checked(
            (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw);

        foreach (var candidate in agents)
        {
            if (!candidate.IsAlive || candidate.EntityId == actor.EntityId)
            {
                continue;
            }

            var deltaX = (long)candidate.XRaw - actor.XRaw;
            var deltaY = (long)candidate.YRaw - actor.YRaw;
            var squaredDistance = checked(
                (deltaX * deltaX) + (deltaY * deltaY));

            if (candidate.FactionId == actor.FactionId)
            {
                accumulator.ObserveAlly(
                    candidate.EntityId, candidate.Loadout, squaredDistance);
            }
            else if (squaredDistance <= perceptionSquared)
            {
                // The same perception gate target selection applies: an
                // enemy the actor could not have targeted never reaches the
                // context either. Allies carry no perception test, matching
                // the observation stage, which never perceives allies.
                accumulator.ObserveEnemy(
                    candidate.EntityId, candidate.Loadout, squaredDistance);
            }
        }

        return accumulator.Complete(selectedTargetEntityId);
    }
}

/// <summary>
/// Per-actor accumulation state for one <see cref="LocalMovementContext"/>
/// derivation. A stack-local scratch value: constructed fresh per actor per
/// tick, mutated only through <see cref="ObserveAlly"/> and
/// <see cref="ObserveEnemy"/>, drained once by <see cref="Complete"/>, and
/// never stored, shared, or reused across actors. Both the pure
/// <see cref="MovementContextQuery.Derive"/> seam and the fused production
/// loop drive this one type, so the two paths cannot drift apart.
/// </summary>
internal struct MovementContextAccumulator
{
    private readonly Int128 _immediateRadiusSquared;
    private readonly Int128 _supportRadiusSquared;
    private int _immediateAllies;
    private int _immediateEnemies;
    private int _supportAllies;
    private int _supportEnemies;
    private LoadoutCompositionCounts _alliedComposition;
    private LoadoutCompositionCounts _enemyComposition;
    private ulong _nearestAllyEntityId;
    private long _nearestAllySquaredDistance;
    private ulong _nearestImmediateEnemyEntityId;
    private long _nearestImmediateEnemySquaredDistance;
    private ulong _secondImmediateEnemyEntityId;
    private long _secondImmediateEnemySquaredDistance;

    /// <summary>
    /// Seeds the accumulator with the acting warrior itself: design section
    /// 7.1 counts the actor in <see cref="LocalMovementContext.SupportAllies"/>
    /// and its own bucket in
    /// <see cref="LocalMovementContext.AlliedComposition"/>, while both
    /// immediate counts exclude it.
    /// </summary>
    internal MovementContextAccumulator(
        CombatLoadout actorLoadout,
        Int128 immediateRadiusSquared,
        Int128 supportRadiusSquared)
    {
        _immediateRadiusSquared = immediateRadiusSquared;
        _supportRadiusSquared = supportRadiusSquared;
        _supportAllies = 1;
        _alliedComposition = default(LoadoutCompositionCounts)
            .Add(actorLoadout);
        _nearestAllySquaredDistance = long.MaxValue;
        _nearestImmediateEnemySquaredDistance = long.MaxValue;
        _secondImmediateEnemySquaredDistance = long.MaxValue;
    }

    /// <summary>
    /// Accumulates one living ally that is not the actor. Membership in
    /// either ring is inclusive at the exact radius.
    /// </summary>
    internal void ObserveAlly(
        ulong entityId, CombatLoadout loadout, long squaredDistance)
    {
        if ((Int128)squaredDistance <= _immediateRadiusSquared)
        {
            _immediateAllies = checked(_immediateAllies + 1);
        }

        if ((Int128)squaredDistance > _supportRadiusSquared)
        {
            return;
        }

        _supportAllies = checked(_supportAllies + 1);
        _alliedComposition = _alliedComposition.Add(loadout);
        if (IsCloser(
                squaredDistance,
                entityId,
                _nearestAllySquaredDistance,
                _nearestAllyEntityId))
        {
            _nearestAllySquaredDistance = squaredDistance;
            _nearestAllyEntityId = entityId;
        }
    }

    /// <summary>
    /// Accumulates one living perceived enemy. Membership in either ring is
    /// inclusive at the exact radius. Tracks the two nearest immediate
    /// enemies so <see cref="Complete"/> can exclude the selected target
    /// without a second scan.
    /// </summary>
    internal void ObserveEnemy(
        ulong entityId, CombatLoadout loadout, long squaredDistance)
    {
        if ((Int128)squaredDistance <= _supportRadiusSquared)
        {
            _supportEnemies = checked(_supportEnemies + 1);
            _enemyComposition = _enemyComposition.Add(loadout);
        }

        if ((Int128)squaredDistance > _immediateRadiusSquared)
        {
            return;
        }

        _immediateEnemies = checked(_immediateEnemies + 1);
        if (IsCloser(
                squaredDistance,
                entityId,
                _nearestImmediateEnemySquaredDistance,
                _nearestImmediateEnemyEntityId))
        {
            _secondImmediateEnemySquaredDistance =
                _nearestImmediateEnemySquaredDistance;
            _secondImmediateEnemyEntityId = _nearestImmediateEnemyEntityId;
            _nearestImmediateEnemySquaredDistance = squaredDistance;
            _nearestImmediateEnemyEntityId = entityId;
        }
        else if (IsCloser(
                squaredDistance,
                entityId,
                _secondImmediateEnemySquaredDistance,
                _secondImmediateEnemyEntityId))
        {
            _secondImmediateEnemySquaredDistance = squaredDistance;
            _secondImmediateEnemyEntityId = entityId;
        }
    }

    /// <summary>
    /// Produces the finished context. The second threat is the nearest
    /// immediate enemy other than <paramref name="selectedTargetEntityId"/>;
    /// at most one of the two tracked entries can be the target, so the
    /// first non-target entry, in nearness order, is the answer.
    /// </summary>
    internal readonly LocalMovementContext Complete(
        ulong? selectedTargetEntityId)
    {
        ulong? secondThreatEntityId = null;
        if (_nearestImmediateEnemyEntityId != 0 &&
            _nearestImmediateEnemyEntityId != selectedTargetEntityId)
        {
            secondThreatEntityId = _nearestImmediateEnemyEntityId;
        }
        else if (_secondImmediateEnemyEntityId != 0 &&
            _secondImmediateEnemyEntityId != selectedTargetEntityId)
        {
            secondThreatEntityId = _secondImmediateEnemyEntityId;
        }

        return new LocalMovementContext(
            _immediateAllies,
            _immediateEnemies,
            _supportAllies,
            _supportEnemies,
            _alliedComposition,
            _enemyComposition,
            _nearestAllyEntityId == 0 ? null : _nearestAllyEntityId,
            secondThreatEntityId);
    }

    /// <summary>
    /// The one tie-break rule every nearest-entity field uses: strictly
    /// smaller squared distance wins, and an exact squared-distance tie
    /// breaks on lower <c>EntityId</c> — never on scan order. An incumbent
    /// id of zero marks an empty slot; zero is never a valid
    /// <c>EntityId</c>.
    /// </summary>
    private static bool IsCloser(
        long squaredDistance,
        ulong entityId,
        long incumbentSquaredDistance,
        ulong incumbentEntityId) =>
        squaredDistance < incumbentSquaredDistance ||
        (squaredDistance == incumbentSquaredDistance &&
            (incumbentEntityId == 0 || entityId < incumbentEntityId));
}
