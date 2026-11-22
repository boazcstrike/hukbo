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
    private void DecrementCooldowns()
    {
        foreach (var agent in _agentStates)
        {
            if (agent.IsAlive && agent.AttackCooldownRemaining > 0)
            {
                agent.AttackCooldownRemaining--;
            }

            // Shield-projectile-block design section 6.2. Zero forever under
            // every preset whose AppliesShieldBlockRecovery is false, since
            // nothing ever sets the counter above zero there, so this
            // decrement is a no-op for every such preset and needs no gate
            // of its own.
            if (agent.IsAlive && agent.ShieldBlockRecoveryTicksRemaining > 0)
            {
                agent.ShieldBlockRecoveryTicksRemaining--;
            }
        }
    }

    private void SelectTargetsAndIntents()
    {
        ComputeRallyAgents();
        ComputeRallyEngagement();

        // Equipment-relative local context (weapon-relative movement design,
        // section 7) is derived inside this same observation, fused into the
        // candidate loop below, only under a preset that opts in. Both radii
        // are materialized once per call through the section 4.4 arithmetic;
        // the body radius is scenario-wide, so they are identical for every
        // actor.
        var derivesLocalContext = _movementRules.UsesEquipmentRelativeFootwork;

        // The ranged-retreat threat observation (battlefield realism design,
        // section 5.3) is gated on preset identity, matching
        // _nearestMeleeThreatSquared's own sizing in the constructor: V10's
        // registered ruleset is a verbatim copy of V8's, so no
        // MovementRuleset flag distinguishes it from every other preset that
        // derives no local context either.
        var observesNearestMeleeThreat =
            UsesBattlefieldRealism(Scenario.MovementPreset);
        Int128 immediateRadiusSquared = 0;
        Int128 supportRadiusSquared = 0;
        if (derivesLocalContext)
        {
            immediateRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    Scenario.BodyRadiusRaw,
                    _movementRules.ImmediateRadiusBodyDiametersBasisPoints));
            supportRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    Scenario.BodyRadiusRaw,
                    _movementRules.SupportRadiusBodyDiametersBasisPoints));
            _factionSurvivingCompositions[0] = default;
            _factionSurvivingCompositions[1] = default;
        }

        // Ring-search targeting design, section 3: the equipment-relative
        // local context observes every perceivable enemy and every ally, which
        // is all-pairs by definition, so only a preset that derives none uses
        // the grids. Positions and liveness are final for this whole call, so
        // one rebuild here keeps the grids exact until it returns.
        var usesRingSearch = !derivesLocalContext && !ForcesBruteForceTargeting;
        if (usesRingSearch)
        {
            _targetingGrids[0].Rebuild(_agentStates, 0);
            _targetingGrids[1].Rebuild(_agentStates, 1);
        }

        for (var agentIndex = 0; agentIndex < _agentStates.Length; agentIndex++)
        {
            var agent = _agentStates[agentIndex];
            if (!agent.IsAlive)
            {
                agent.TargetEntityId = null;
                agent.Intent = AgentIntent.Dead;
                if (derivesLocalContext)
                {
                    // Dead agents count nowhere and carry no context; clear
                    // the row so a stale value from the tick this agent died
                    // on can never be read back.
                    _localMovementContexts[agentIndex] = default;
                }

                if (observesNearestMeleeThreat)
                {
                    // Dead agents observe no threat; clear the row so a stale
                    // value from the tick this agent died on can never be
                    // read back, matching the local-context clear just above.
                    _nearestMeleeThreatSquared[agentIndex] = long.MaxValue;
                }

                continue;
            }

            if (derivesLocalContext)
            {
                _contextAccumulator = new MovementContextAccumulator(
                    agent.Loadout, immediateRadiusSquared, supportRadiusSquared);
                _factionSurvivingCompositions[agent.FactionId] =
                    _factionSurvivingCompositions[agent.FactionId]
                        .Add(agent.Loadout);
            }

            // Battlefield realism design, section 5.3: the trigger reads the
            // nearest living enemy whose weapon is melee, observed only when
            // the actor's own weapon is ranged (StandoffDistanceRaw != 0 --
            // the same test GatherMovementProposals' V8 standoff arm already
            // uses). Resolved once per actor, not per candidate, since it
            // does not depend on the candidate.
            var actorObservesMeleeThreats = observesNearestMeleeThreat &&
                ResolveAttackerWeaponProfile(agent.Loadout).StandoffDistanceRaw != 0;

            var search = new TargetSearch(
                this,
                agent,
                actorObservesMeleeThreats,
                derivesLocalContext);
            if (usesRingSearch)
            {
                // Ring-search targeting design: the enemy faction's grid
                // visits candidates nearest ring first and stops once none can
                // beat the best found. TargetSearch.Consider is the same
                // per-candidate body the brute-force scan below runs.
                _targetingGrids[1 - agent.FactionId].Search(
                    agent.XRaw, agent.YRaw, ref search);
            }
            else
            {
                foreach (var candidate in _agentStates)
                {
                    if (!candidate.IsAlive || candidate.FactionId == agent.FactionId)
                    {
                        // A living candidate can only land here as a
                        // same-faction one, so this V6-only branch observes
                        // exactly the living allies. Allies never reach the
                        // perception test in Consider — the observation never
                        // perceives them at all — so their deltas are computed
                        // here, on the actor's own dime, and target selection
                        // reads nothing this accumulation writes.
                        if (derivesLocalContext &&
                            candidate.IsAlive &&
                            candidate.EntityId != agent.EntityId)
                        {
                            var allyDeltaX = (long)candidate.XRaw - agent.XRaw;
                            var allyDeltaY = (long)candidate.YRaw - agent.YRaw;
                            _contextAccumulator.ObserveAlly(
                                candidate.EntityId,
                                candidate.Loadout,
                                checked(
                                    (allyDeltaX * allyDeltaX) +
                                    (allyDeltaY * allyDeltaY)));
                        }

                        continue;
                    }

                    search.Consider(candidate);
                }
            }

            var selectedTarget = search.SelectedTarget;
            var selectedDistance = search.SelectedDistance;
            var nearestMeleeThreatSquared = search.NearestMeleeThreatSquared;

            agent.TargetEntityId = selectedTarget?.EntityId;
            if (derivesLocalContext)
            {
                _localMovementContexts[agentIndex] =
                    _contextAccumulator.Complete(selectedTarget?.EntityId);
                _localMovementContextDerivations = checked(
                    _localMovementContextDerivations + 1);
            }

            if (observesNearestMeleeThreat)
            {
                _nearestMeleeThreatSquared[agentIndex] = nearestMeleeThreatSquared;
            }

            if (selectedTarget is null)
            {
                agent.Intent = AgentIntent.Idle;
                continue;
            }

            // An agent keeps advancing until its body meets the target's, even
            // once the target is already inside reach. Attacking is reserved
            // for an agent that has arrived. One that strikes while still
            // closing is re-marked Attacking by attack gathering, so a
            // spectator still sees it fighting.
            agent.Intent = selectedDistance <= CollisionGeometry
                .ContactSquaredDistance(Scenario.BodyRadiusRaw)
                ? AgentIntent.Attacking
                : AgentIntent.Moving;

            // Regrouping only overrides an intent that would otherwise be
            // Moving: Attacking beats Regrouping (the same-tick conflict
            // rule), and the rally agent itself is exempt and keeps its
            // ordinary nearest-enemy intent.
            if (Scenario.LastStandThresholdAgents > 0 &&
                agent.Intent == AgentIntent.Moving &&
                _factionLivingCounts[agent.FactionId] <=
                    Scenario.LastStandThresholdAgents &&
                agent.EntityId != _factionRallyEntityIds[agent.FactionId] &&
                !YieldsRegroupingToEngagement(agent, selectedDistance))
            {
                agent.Intent = AgentIntent.Regrouping;
            }
        }
    }

    /// <summary>
    /// One actor's nearest-enemy search state, shared by the ring search and
    /// the brute-force scan so the two cannot drift apart: both feed every
    /// candidate through <see cref="Consider"/>, which holds the one copy of
    /// the perception test, the melee-threat minimum, and the target
    /// comparison.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A struct passed by <c>ref</c> to <see cref="TargetingGrid.Search"/>, so
    /// the search allocates nothing. <see cref="Consider"/> expects a living
    /// enemy of the actor.
    /// </para>
    /// <para>
    /// The three search methods skip tiered compilation. A short battle runs
    /// mostly in the unoptimised first tier, and measured at 200 agents the
    /// ring search's extra calls there raised the tick p95 by about 6% over the
    /// brute-force scan, while fully optimised it was about 15% faster. Jitting
    /// them optimised on first call removes that cost; the work they do is
    /// unchanged.
    /// </para>
    /// </remarks>
    private struct TargetSearch : ITargetingVisitor
    {
        private readonly BattleSimulation _simulation;
        private readonly AgentState _actor;
        private readonly long _perceptionSquared;
        private readonly bool _observesMeleeThreats;
        private readonly bool _derivesLocalContext;

        internal AgentState? SelectedTarget;
        internal long SelectedDistance;
        internal long NearestMeleeThreatSquared;

        internal TargetSearch(
            BattleSimulation simulation,
            AgentState actor,
            bool observesMeleeThreats,
            bool derivesLocalContext)
        {
            _simulation = simulation;
            _actor = actor;
            _perceptionSquared = checked(
                (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw);
            _observesMeleeThreats = observesMeleeThreats;
            _derivesLocalContext = derivesLocalContext;
            SelectedTarget = null;
            SelectedDistance = long.MaxValue;
            NearestMeleeThreatSquared = long.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Visit(AgentState candidate) => Consider(candidate);

        // Strict > throughout, as a safety margin. With integer coordinates
        // and half-open cells every candidate in the next ring is strictly
        // farther than lowerBoundSquared, so >= would stop in the same places;
        // strict stays correct even against an equally distant candidate with
        // a lower EntityId. The perception test rejects only
        // distance > perceptionSquared, so the perception bound is strict too.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public readonly bool CanStopBefore(long lowerBoundSquared) =>
            lowerBoundSquared > _perceptionSquared ||
            (lowerBoundSquared > SelectedDistance &&
                (!_observesMeleeThreats ||
                    lowerBoundSquared > NearestMeleeThreatSquared));

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal void Consider(AgentState candidate)
        {
            var agent = _actor;

            // Cheap axis-aligned rejection before the squared-distance
            // check below: if |dx| already exceeds the (unsquared)
            // perception range, then dx*dx alone exceeds range*range,
            // so dx*dx + dy*dy > range*range necessarily (dy*dy is
            // never negative). Every candidate rejected here would also
            // have been rejected by the perception test that follows,
            // so the surviving candidate set and its scan order are
            // unchanged. Written as two comparisons instead of an
            // absolute value so no negation of deltaX/deltaY is needed.
            var perceptionRangeRaw = (long)agent.PerceptionRangeRaw;
            var deltaX = (long)candidate.XRaw - agent.XRaw;
            if (deltaX > perceptionRangeRaw || deltaX < -perceptionRangeRaw)
            {
                return;
            }

            var deltaY = (long)candidate.YRaw - agent.YRaw;
            if (deltaY > perceptionRangeRaw || deltaY < -perceptionRangeRaw)
            {
                return;
            }

            var distance = SquaredDistance(agent, candidate);
            if (distance > _perceptionSquared)
            {
                return;
            }

            // The V6 context hook of design section 7.3: after the
            // perception test passes, before the comparison block, so
            // the already-computed squared distance is reused and only
            // perceivable enemies are ever observed. The comparison
            // block below reads only distance, selectedDistance, and
            // candidate.EntityId, so nothing written here can affect
            // tie-breaking, and target selection stays byte-identical.
            if (_derivesLocalContext)
            {
                _simulation._contextAccumulator.ObserveEnemy(
                    candidate.EntityId, candidate.Loadout, distance);
            }

            // The ranged-retreat threat fuse of design section 5.3, beside
            // the V6 context fuse above: the same already-computed,
            // already-perception-filtered distance is minimised into
            // nearestMeleeThreatSquared when the candidate's weapon is
            // melee (StandoffDistanceRaw == 0). No new scan, no new
            // distance computation -- distance is exactly the value target
            // selection below also reads. A plain minimum over squared
            // distances needs no EntityId tie-break: the reduction is
            // order-independent regardless of scan order.
            if (_observesMeleeThreats &&
                distance < NearestMeleeThreatSquared &&
                _simulation.ResolveAttackerWeaponProfile(candidate.Loadout)
                    .StandoffDistanceRaw == 0)
            {
                NearestMeleeThreatSquared = distance;
            }

            if (distance < SelectedDistance ||
                (distance == SelectedDistance &&
                    (SelectedTarget is null ||
                        candidate.EntityId < SelectedTarget.EntityId)))
            {
                SelectedTarget = candidate;
                SelectedDistance = distance;
            }
        }
    }
}
