using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

public sealed partial class BattleSimulation
{
    /// <summary>
    /// Hands every living agent to the solid-disc resolver. Agents without a
    /// proposal are still submitted: they occupy space, and a stationary body
    /// would otherwise have its ground taken by a mover resolved before it.
    /// Each mover carries this tick's contested-ground priority, which is a
    /// pure hash of the seed, the tick, and the agent rather than a draw from
    /// any stream.
    /// </summary>
    private void ResolveCollisions()
    {
        _collision.BeginTick();

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var proposal = _movementProposals[index];
            _collision.Requests.Add(
                new CollisionMoveRequest(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    proposal?.XRaw ?? agent.XRaw,
                    proposal?.YRaw ?? agent.YRaw,
                    proposal is not null,
                    // Only a mover is ordered by its key, so a standing agent
                    // does not pay for a mix it will never be sorted by.
                    proposal is null
                        ? 0
                        : CollisionPriority.Resolve(
                            Scenario.Seed,
                            Tick,
                            agent.EntityId)));
        }

        _collision.Resolver.Resolve(_collision.Requests);
    }

    /// <summary>
    /// The single position commit for the tick. Resolver results are ordered to
    /// match the living agents in ascending entity ID, which is the same order
    /// they were submitted in.
    /// </summary>
    private void CommitMovement(List<BattleEvent> events)
    {
        var results = _collision.Resolver.Results;
        var resultIndex = 0;
        var commitsRetainedPace = _movementRules.UsesEquipmentRelativeFootwork;

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                agent.MovementResolution = MovementResolution.None;
                _collision.RecordBlocked(index, isBlocked: false);
                continue;
            }

            var result = results[resultIndex];
            resultIndex++;

            var previousX = agent.XRaw;
            var previousY = agent.YRaw;
            agent.XRaw = result.XRaw;
            agent.YRaw = result.YRaw;
            agent.MovementResolution = result.Resolution;
            _collision.RecordBlocked(
                index,
                result.Resolution == MovementResolution.Blocked);

            var deltaX = (long)agent.XRaw - previousX;
            var deltaY = (long)agent.YRaw - previousY;
            if (deltaX == 0 && deltaY == 0)
            {
                if (commitsRetainedPace)
                {
                    // Design section 6.5: a blocked, rejected, or refused
                    // move leaves zero retained pace rather than fictitious
                    // momentum. Turning in place is still permitted, because
                    // facing committed independently during gathering.
                    agent.MovementPaceRaw = 0;
                }

                continue;
            }

            var movedRaw = checked((int)IntegerSquareRoot(
                checked((deltaX * deltaX) + (deltaY * deltaY))));
            if (commitsRetainedPace)
            {
                // Design section 6.5: the retained pace commits as the
                // smaller of the pace this tick's proposal was built on and
                // the distance the collision stage actually granted.
                agent.MovementPaceRaw =
                    Math.Min(_proposedPaceRaw[index], movedRaw);
            }

            AddEvent(
                events,
                BattleEventKind.Move,
                agent.EntityId,
                _movementProposals[index]?.TargetId,
                movedRaw,
                agent.FactionId);
        }
    }

    /// <summary>
    /// The separation pass's weight for one armour weight class. Explicit
    /// literals rather than the enum's numeric value, so renumbering or
    /// reordering <see cref="ArmorWeightClass"/> cannot silently change the
    /// separation under the preset that applies it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="weightClass"/> is not a declared member.
    /// </exception>
    internal static int SeparationWeightFor(ArmorWeightClass weightClass) =>
        weightClass switch
        {
            ArmorWeightClass.Light => 1,
            ArmorWeightClass.Medium => 2,
            ArmorWeightClass.Rigid => 3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(weightClass),
                weightClass,
                "Unknown armour weight class for the separation pass."),
        };

    /// <summary>
    /// Writes the per-agent separation inputs that never change during a
    /// battle, entity ID and weight, so <see cref="ApplySeparatingContact"/>
    /// does not rewrite them every tick. Does nothing when the preset builds no
    /// separation pass.
    /// </summary>
    private void FillSeparationConstants()
    {
        if (_collision.Separation is null)
        {
            return;
        }

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            _collision.SeparationEntityId[index] = agent.EntityId;
            _collision.SeparationWeight[index] = SeparationWeightFor(
                ArmorWeightClasses.Resolve(agent.Loadout.Armor));
        }
    }

    /// <summary>
    /// The one separation pass of the tick, run only under a preset that applies
    /// separating contact (<c>docs/decisions/2026-11-21-separating-contact.md</c>
    /// item 4). It reads the positions <see cref="CommitMovement"/> just
    /// committed and writes the pass's post-clamp positions back, so the state
    /// hash and everything later in the tick see them. It emits no event: each
    /// Move event keeps the distance <see cref="CommitMovement"/> moved the
    /// agent, and the pass's displacement is not reported as movement.
    /// </summary>
    /// <remarks>
    /// Weight is the armour weight class (<see cref="SeparationWeightFor"/>:
    /// light 1, medium 2, rigid 3). Weight and entity ID are fixed for a whole
    /// battle, so <see cref="FillSeparationConstants"/> writes them once at
    /// construction. A body counts as committed to a fight when its
    /// <see cref="AgentIntent"/> is <see cref="AgentIntent.Attacking"/>, which
    /// target selection assigns only to an agent already in body contact with
    /// its target. That is the same hashed state the rest of the tick reads, so
    /// the choice needs no extra query and cannot disagree with it.
    /// </remarks>
    private void ApplySeparatingContact()
    {
        var pass = _collision.Separation
            ?? throw new InvalidOperationException(
                "The separation pass is built whenever the gate is on.");
        var xRaw = _collision.SeparationXRaw;
        var yRaw = _collision.SeparationYRaw;

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            xRaw[index] = agent.XRaw;
            yRaw[index] = agent.YRaw;
            _collision.SeparationIsAlive[index] = agent.IsAlive;
            _collision.SeparationIsCommitted[index] =
                agent.Intent == AgentIntent.Attacking;
        }

        pass.Run(
            xRaw,
            yRaw,
            _collision.SeparationIsAlive,
            _collision.SeparationWeight,
            _collision.SeparationIsCommitted,
            _collision.SeparationEntityId,
            _collision.SeparationSettings);

        var separatedX = pass.OutX;
        var separatedY = pass.OutY;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            agent.XRaw = separatedX[index];
            agent.YRaw = separatedY[index];
        }
    }

    /// <summary>
    /// Derives this tick's collision counters from committed positions. Pure
    /// observation: nothing here writes agent state, and none of it is hashed.
    /// </summary>
    private void MeasureCollision()
    {
        var (candidatePairs, contactPairs, penetrationRaw) =
            _agentIndexes.IsDense
                ? MeasureContactsByCount()
                : MeasureContactsByPairList();

        var minimumX = int.MaxValue;
        var maximumX = int.MinValue;
        var minimumY = int.MaxValue;
        var maximumY = int.MinValue;

        // The front spans agents holding an enemy in reach, not strictly
        // touching bodies. The resolver leaves every living pair at or beyond
        // the contact distance, so strict touching means a squared distance of
        // exactly (2R)^2 — a Pythagorean coincidence on an integer lattice — and
        // a span built on it would read zero through an entire battle. Contact
        // pairs are counted separately, over a proximity band.
        var attackCapableAgents = 0;
        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive || agent.TargetEntityId is not { } targetId)
            {
                continue;
            }

            var target = _agentStates[AgentIndexOf(targetId)];
            if (!target.IsAlive || !IsWithinAttackRange(agent, target))
            {
                continue;
            }

            attackCapableAgents++;
            minimumX = Math.Min(minimumX, agent.XRaw);
            maximumX = Math.Max(maximumX, agent.XRaw);
            minimumY = Math.Min(minimumY, agent.YRaw);
            maximumY = Math.Max(maximumY, agent.YRaw);
        }

        _lastTickCollision = new CollisionTickMetrics(
            candidatePairs,
            contactPairs,
            _collision.Resolver.AcceptedMoveCount,
            _collision.Resolver.BlockedCount,
            attackCapableAgents,
            attackCapableAgents == 0 ? 0 : checked(maximumY - minimumY),
            attackCapableAgents == 0 ? 0 : checked(maximumX - minimumX),
            penetrationRaw);
    }

    /// <summary>
    /// Candidate pairs, cross-faction pairs and deepest penetration over this
    /// tick's bodies, counted by the grid without building a pair list. The
    /// deepest penetration is <c>2R - floor(sqrt(d^2))</c> for the closest pair,
    /// floored at zero: the integer square root never falls as its argument
    /// grows, so the closest pair is the deepest one.
    /// </summary>
    private (int CandidatePairs, int ContactPairs, int PenetrationRaw)
        MeasureContactsByCount()
    {
        _collision.Grid.Clear();
        foreach (var agent in _agentStates)
        {
            _collision.Grid.Insert(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }

        var counts = _collision.Grid.CountContacts(
            _collision.ContactBandRadiusRaw,
            _factionByEntityId);
        var penetrationRaw = 0;

        if (counts.MinimumSquaredDistance is { } minimumSquaredDistance)
        {
            var contactDistanceRaw = checked(2 * Scenario.BodyRadiusRaw);

            penetrationRaw = Math.Max(
                0,
                contactDistanceRaw -
                    checked((int)IntegerSquareRoot(minimumSquaredDistance)));
        }

        return (counts.PairCount, counts.CrossFactionPairCount, penetrationRaw);
    }

    /// <summary>
    /// The same three figures through the sorted pair list, for the agent arrays
    /// whose entity IDs are too sparse for <see cref="_factionByEntityId"/>.
    /// </summary>
    private (int CandidatePairs, int ContactPairs, int PenetrationRaw)
        MeasureContactsByPairList()
    {
        _collision.Bodies.Clear();
        foreach (var agent in _agentStates)
        {
            _collision.Bodies.Add(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }

        _collision.Grid.Rebuild(
            _collision.Bodies,
            _collision.ContactBandRadiusRaw);

        var contactDistanceRaw = checked(2 * Scenario.BodyRadiusRaw);
        var contactPairs = 0;
        var penetrationRaw = 0;

        foreach (var pair in _collision.Grid.PairsList)
        {
            var left = _agentStates[AgentIndexOf(pair.LowEntityId)];
            var right = _agentStates[AgentIndexOf(pair.HighEntityId)];
            var separationRaw = checked((int)IntegerSquareRoot(
                CollisionGeometry.SquaredDistance(
                    left.XRaw,
                    left.YRaw,
                    right.XRaw,
                    right.YRaw)));
            penetrationRaw = Math.Max(
                penetrationRaw,
                contactDistanceRaw - separationRaw);

            if (left.FactionId == right.FactionId)
            {
                continue;
            }

            contactPairs++;
        }

        return (_collision.Grid.Pairs.Count, contactPairs, penetrationRaw);
    }

    /// <summary>
    /// Faction of each agent at the index equal to its entity ID. Only called
    /// when <see cref="AgentIndexMap"/> chose its flat array, so the largest ID
    /// is bounded by a small multiple of the agent count.
    /// </summary>
    private static int[] BuildFactionByEntityId(AgentState[] agents)
    {
        ulong maxId = 0;
        foreach (var agent in agents)
        {
            maxId = Math.Max(maxId, agent.EntityId);
        }

        var factions = new int[checked((int)maxId + 1)];
        foreach (var agent in agents)
        {
            factions[(int)agent.EntityId] = agent.FactionId;
        }

        return factions;
    }
}
