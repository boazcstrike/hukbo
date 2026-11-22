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
    /// The equipment-relative posture-and-provisional-footwork stage of the
    /// weapon-relative movement design, sections 8 and 9, inserted between
    /// <see cref="ResolveContingentStates"/> and
    /// <see cref="GatherMovementProposals"/> per design section 11.2 and
    /// reached only under a preset whose
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
    /// <see langword="true"/>. Postures resolve once per contingent from the
    /// global living totals, this tick's already-resolved
    /// <see cref="ContingentState"/>, and the two factions' role coverage,
    /// and are written to every living member. The provisional phase and
    /// timer stay in scratch: design section 9.4 commits them exactly once,
    /// after route generation and lane clearance finalise them.
    /// </summary>
    /// <remarks>
    /// This is also the one place the V7 pressure interrupt is evaluated, under
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> and therefore
    /// never under any preset from V1 through V6. It is the only stage that
    /// both charges the interrupt's cost and feeds its answer into the footwork
    /// ladder, which is why the answer is computed once, here, rather than
    /// twice — see the V7 design document, sections 3 and 4.4.
    /// </remarks>
    private void ResolveEquipmentPosturesAndProvisionalFootwork()
    {
        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] == 0)
            {
                _contingentPostures[slot] = TacticalPosture.None;
                continue;
            }

            var factionId = slot / FormationPlanner.MaximumContingents;
            _contingentPostures[slot] = WeaponMovementRules.ResolveTacticalPosture(
                _factionLivingCounts[factionId],
                _factionLivingCounts[1 - factionId],
                _contingentResolvedStates[slot],
                _factionSurvivingCompositions[factionId].RoleCoverage,
                _factionSurvivingCompositions[1 - factionId].RoleCoverage);
        }

        var appliesPressureInterrupt = _movementRules.AppliesPressureInterrupt;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                _provisionalFootworkPhases[index] = FootworkPhase.None;
                _provisionalFootworkTicks[index] = 0;
                if (appliesPressureInterrupt)
                {
                    // Both slots are cleared rather than left stale, but
                    // neither the predicate nor ComputeWeightedPressure is
                    // called here: neither of them guards any of its own
                    // arguments, and both divide by counts that only a living
                    // agent is guaranteed to carry.
                    _pressureInterruptFired[index] = false;
                    _pressureBasisPoints[index] = 0;
                }

                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            agent.TacticalPosture = _contingentPostures[slot];

            var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
            var context = _localMovementContexts[index];
            var hasTarget = false;
            var targetAtOrInsidePreferredDistance = false;
            if (agent.TargetEntityId is { } targetId &&
                TryGetAgentIndex(targetId, out var targetIndex))
            {
                var target = _agentStates[targetIndex];
                hasTarget = true;

                // Design section 9.1, step 8: the offset-adjusted preferred
                // distance, compared inclusively on squared values. The
                // target's loadout is read from authoritative state here, at
                // the moment the offset is applied, so no second copy of the
                // target's identity is cached anywhere.
                var preferredRaw =
                    MovementRouteRules.EffectivePreferredDistanceRaw(
                        agent.AttackRangeRaw,
                        profile,
                        MovementRouteRules.CanonicalOpponentIndex(
                            target.Loadout));
                targetAtOrInsidePreferredDistance =
                    (Int128)SquaredDistance(agent, target) <=
                    checked((Int128)preferredRaw * preferredRaw);
            }

            if (appliesPressureInterrupt)
            {
                // Design section 4, the pressure interrupt: one evaluation per
                // living agent per tick, on tick-start authoritative state plus
                // the scratch SelectTargetsAndIntents finalised before this
                // loop began. No agent's answer reads another agent's answer,
                // so the loop order cannot decide an outcome and the no-peeking
                // invariant GatherEquipmentRelativeMovementProposals documents
                // is preserved. No random stream is consulted.
                //
                // ComputeWeightedPressure guards no argument of its own: it
                // divides by supportAllies, by maximumHitPoints, and — when
                // that count is non-zero — by priorSupportAllies.
                // LocalMovementContext's ally count includes the actor itself
                // and Scenario.Validate proves MaximumHitPoints is at least 1,
                // so both divisors are non-zero by construction for a living
                // agent. That is why the call sits in the living-agent path
                // only, and why the dead branch above clears the slot instead
                // of asking.
                //
                // Design section 4.2 requires the weighted sum to be computed
                // exactly once per agent per tick, and this is that one
                // computation. The value it produces feeds both consumers
                // below: the spectator's pressure row and the interrupt
                // predicate, whose single answer the charged cost and the
                // footwork ladder then share. Three long divisions and six
                // multiplications are not worth running twice in a stage the
                // tick budget is measured against.
                long weightedPressure =
                    WeaponMovementRules.ComputeWeightedPressure(
                        context.SupportAllies,
                        context.SupportEnemies,
                        agent.PriorSupportAllies,
                        agent.DamageTakenLastTick,
                        agent.MaximumHitPoints,
                        _movementRules.SupportPressureWeightBasisPoints,
                        _movementRules.IncomingDamageWeightBasisPoints,
                        _movementRules.AllyCollapseWeightBasisPoints);

                // The spectator's pressure row, design section 3, question 8,
                // channel 3. The predicate below answers whether the interrupt
                // fires, which its two guards can decide without ever weighing
                // the signals; the inspector needs the weighed number itself,
                // on every tick, whatever phase the warrior is in. Those are
                // two different questions asked of the one sum above, which is
                // why this write sits outside the predicate and runs for every
                // living agent under the interrupt preset, including one whose
                // guards reject the interrupt.
                //
                // Dividing by the ratio scale converts the weighted sum back
                // into the basis-point unit the profile's threshold is
                // registered in, so the inspector can print the two against
                // each other. The quotient is at most SignalCeilingBasisPoints,
                // because the three weights sum to one whole unit under a
                // preset that applies the interrupt, so the int cast cannot
                // truncate.
                _pressureBasisPoints[index] = (int)(
                    weightedPressure
                    / WeaponMovementRules.RatioBasisPointScale);

                // What is left of the predicate is its two guards and one
                // comparison against this row's own threshold; the number it
                // weighs is the sum above rather than a second copy of it.
                _pressureInterruptFired[index] =
                    WeaponMovementRules.ShouldPressureInterrupt(
                        agent.FootworkPhase,
                        profile.PressureInterruptThresholdBasisPoints,
                        weightedPressure);

                if (_pressureInterruptFired[index])
                {
                    // The cost, design section 4.4. This is the second
                    // non-decrement writer of AgentState.AttackCooldownRemaining;
                    // the other is ResolveComboTransition, which writes it from
                    // the combat stage. That is a deliberate, documented
                    // inversion of the invariant
                    // ApplyEquipmentAttackFootworkAndDeathCleanup's summary
                    // records: combat still reads nothing from movement, and
                    // the post-attack footwork pass still never blocks an
                    // attack the combat gates accepted. What changes is the
                    // reverse direction — the movement stage writing a field
                    // the combat stage owns — and the write is unreachable
                    // under every preset from V1 through V6, because the gate
                    // above is false for all of them.
                    //
                    // AttackCooldownTicks rather than the weapon profile's
                    // value, matching the reasoning ResolveComboTransition's
                    // remarks already record: the two are bit-identical for
                    // every agent CreateAgent produces, and the cached field is
                    // the one every other tick stage reads.
                    //
                    // The timing is exact. DecrementCooldowns runs before this
                    // stage, so the value written here is not decremented on
                    // the tick it is written: the first decrement lands at the
                    // start of the next tick and the attack gate reopens
                    // exactly AttackCooldownTicks ticks later. On this tick
                    // GatherAndCommitAttacks sees a non-zero cooldown and the
                    // warrior lands nothing. That is the cost, and it is the
                    // observable point of the whole feature.
                    agent.AttackCooldownRemaining = agent.AttackCooldownTicks;

                    // The chain is cleared, not preserved, and this is a
                    // decision rather than an oversight: a warrior whose
                    // cooldown was just reset to the full normal value is by
                    // definition not continuing a chain, and leaving
                    // ComboStepsRemaining above zero would let the next blow
                    // claim a chain position across the interruption — an event
                    // field reporting a continuity that did not happen. These
                    // are the same two writes ClearActiveComboChain performs.
                    agent.ComboStepsRemaining = 0;
                    agent.ComboTargetEntityId = null;

                    // The spectator channel of design section 8. It is set here
                    // and cleared by ApplyEquipmentAttackFootworkAndDeathCleanup
                    // the moment this tick's finalised phase is anything but the
                    // Disengage this interrupt produced, so the pawn mark
                    // persists for a readable number of ticks rather than
                    // pulsing for one.
                    agent.BrokeOffUnderPressure = true;
                }
            }

            var (phase, ticksRemaining) =
                WeaponMovementRules.ResolveProvisionalFootwork(
                    isAlive: true,
                    agent.FootworkPhase,
                    agent.FootworkTicksRemaining,
                    agent.TacticalPosture,
                    context.SupportAllies,
                    context.SupportEnemies,
                    profile.DisengageEnemyToAllyBasisPoints,
                    profile.ReengageEnemyToAllyBasisPoints,
                    profile.RecoveryTicks,
                    hasTarget,
                    targetAtOrInsidePreferredDistance,
                    // The same answer the cost above was charged from, read
                    // back from the one slot that holds it. The gate is what
                    // keeps the index off a zero-length array under every
                    // preset that does not apply the interrupt, and the
                    // parameter's own default is the legacy ladder exactly.
                    pressureInterruptFired: appliesPressureInterrupt &&
                        _pressureInterruptFired[index]);
            _provisionalFootworkPhases[index] = phase;
            _provisionalFootworkTicks[index] = ticksRemaining;
        }
    }

    /// <summary>
    /// The equipment-relative proposal-gathering stage, replacing the legacy
    /// stage wholesale under the opt-in flag. Reads tick-start state only,
    /// exactly like the legacy stage: nothing is committed here except each
    /// agent's own facing and finalised footwork phase, neither of which any
    /// other agent's route reads this tick — every route reads tick-start
    /// positions and the scratch the pre-movement stages produced.
    /// </summary>
    private void GatherEquipmentRelativeMovementProposals()
    {
        Array.Clear(_movementProposals);
        Array.Clear(_proposedPaceRaw);
        var tick = checked((int)Tick);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            GatherOneEquipmentProposal(index, agent, tick);
        }
    }

    /// <summary>
    /// Routes one living agent through the destination precedence chain of
    /// design section 11.1 — dead, body-contact Attacking hold, last-stand
    /// rally, V6 disengage route, contingent cohesion, the remaining V6
    /// phase routes, then ordinary pursuit — and commits its footwork phase
    /// and timer exactly once through the two-step finalisation of design
    /// section 9.4. A destination taken from a source above the V6 routes
    /// counts as a surviving candidate: only a route whose own candidates
    /// were exhausted, by absence or by lane clearance, can finalise
    /// <see cref="FootworkPhase.Refuse"/>.
    /// </summary>
    private void GatherOneEquipmentProposal(int index, AgentState agent, int tick)
    {
        var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
        var provisionalPhase = _provisionalFootworkPhases[index];
        var provisionalTicks = _provisionalFootworkTicks[index];
        var threat = ResolveLivingThreat(agent);

        bool phaseSurvives;
        if (agent.Intent == AgentIntent.Attacking)
        {
            // Body-contact Attacking hold: no movement is proposed, but the
            // warrior still turns toward its threat — turning in place is
            // permitted because facing commits independently of movement.
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            phaseSurvives = true;
        }
        else if (agent.Intent == AgentIntent.Regrouping)
        {
            // The existing last-stand destination, pace-constrained per
            // design section 11.1's closing rule.
            if (TryComputeRegroupingAimPoint(agent, index) is { } rally)
            {
                ProposeEquipmentStepTowardPoint(
                    index,
                    agent,
                    profile,
                    provisionalPhase,
                    threat,
                    rally.XRaw,
                    rally.YRaw,
                    rally.RallyEntityId);
            }
            else
            {
                TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            }

            phaseSurvives = true;
        }
        else if (provisionalPhase == FootworkPhase.Disengage)
        {
            phaseSurvives = TryProposeEquipmentRoute(
                index, agent, profile, provisionalPhase, threat);
        }
        else if (agent.Intent == AgentIntent.Moving &&
            agent.TargetEntityId is not null &&
            TryResolveContingentCohesionAimPoint(
                agent,
                tick,
                out var aimXRaw,
                out var aimYRaw,
                out var leaderEntityId))
        {
            // The existing contingent-cohesion destination, with the same
            // arrived-guard the legacy branch applies, pace-constrained.
            var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
                agent.XRaw,
                agent.YRaw,
                aimXRaw,
                aimYRaw);
            if (squaredDistanceToAim <=
                CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
            {
                TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            }
            else
            {
                ProposeEquipmentStepTowardPoint(
                    index,
                    agent,
                    profile,
                    provisionalPhase,
                    threat,
                    aimXRaw,
                    aimYRaw,
                    leaderEntityId);
            }

            phaseSurvives = true;
        }
        else if (provisionalPhase is FootworkPhase.Approach
            or FootworkPhase.Engage
            or FootworkPhase.Commit
            or FootworkPhase.Recover
            or FootworkPhase.Regroup
            or FootworkPhase.Pursue)
        {
            phaseSurvives = TryProposeEquipmentRoute(
                index, agent, profile, provisionalPhase, threat);
        }
        else if (agent.Intent == AgentIntent.Moving && threat is not null)
        {
            // Ordinary pursuit, the chain's floor. Defensive under V6: a
            // Moving agent has a target, so its provisional phase is one of
            // the route phases above and this branch is not reachable by
            // the phase table — it exists so a future phase addition
            // degrades to pursuit instead of to standing still.
            ProposeEquipmentStepTowardPoint(
                index,
                agent,
                profile,
                provisionalPhase,
                threat,
                threat.XRaw,
                threat.YRaw,
                threat.EntityId);
            phaseSurvives = true;
        }
        else
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            phaseSurvives = true;
        }

        // Design section 9.4: FootworkPhase and FootworkTicksRemaining are
        // written exactly once, here, after finalisation.
        var (finalPhase, finalTicks) = WeaponMovementRules.FinalizeFootwork(
            provisionalPhase, provisionalTicks, phaseSurvives);
        agent.FootworkPhase = finalPhase;
        agent.FootworkTicksRemaining = finalTicks;
    }

    /// <summary>
    /// Which of the four route-refusal reasons (ranged-units plan RU-06,
    /// F-A) <c>TryProposeEquipmentRoute</c> recorded for an agent's current
    /// tick, pending <see cref="ReconcileRouteRefusalReasonCounters"/>.
    /// </summary>
    private enum RouteRefusalReason : byte
    {
        None = 0,
        NoCandidatesBuilt = 1,
        StepEndpointRejected = 2,
        DirectCandidateOmitted = 3,
        LaneNotClear = 4,
    }

    /// <summary>
    /// Generates the provisional phase's route candidates per design section
    /// 10.4, clearance-tests them in order per section 10.5, and proposes
    /// the first survivor. Returns whether any candidate survived; on
    /// failure the agent emits no proposal and only turns in place toward
    /// its threat.
    /// </summary>
    private bool TryProposeEquipmentRoute(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat)
    {
        Span<EquipmentRouteCandidate> candidates =
            stackalloc EquipmentRouteCandidate[3];
        var count = BuildEquipmentRouteCandidates(
            index, agent, profile, provisionalPhase, threat, candidates);
        var context = _localMovementContexts[index];
        var actorClearanceSquared = SquaredClearanceRadius(profile);
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        // Hoisted out of the candidate loop below because it depends only on
        // the scenario's registered preset, never on the candidate: RU-30
        // (F-B) reaches the monotone lane-clearance rule under exactly one
        // preset identity, the same way BattleSimulation.cs's ranged-standoff
        // branch gates on Scenario.MovementPreset == RangedStandoffV8.
        var usesMonotoneAllyClearance =
            Scenario.MovementPreset == MovementPresetId.MonotoneAllyClearanceV9;

        // WeaponMovementRules.FinalizeFootwork only turns a failed route into
        // FootworkPhase.Refuse for these three provisional phases; a failed
        // Commit/Recover/Regroup/Disengage route keeps its own phase and
        // timer instead (design section 9.4's "a blocked lane must not erase
        // a safety or attack lifecycle"). The four counters below decompose
        // MovementBehaviorMetrics.RefuseAgentTicks specifically, so they must
        // count only the calls FinalizeFootwork actually turns into Refuse —
        // gating on the identical condition keeps the four-way sum equal to
        // RefuseAgentTicks exactly, never inflated by a non-Refuse failure.
        var finalizesRefuseOnFailure = provisionalPhase is FootworkPhase.Approach
            or FootworkPhase.Engage
            or FootworkPhase.Pursue;

        // No candidate table at all: BuildEquipmentRouteCandidates found no
        // threat, no facing, or no non-zero delta to route toward, so the
        // loop below never runs. Counted here, once, rather than inside the
        // loop, so this reason is mutually exclusive with the three below —
        // together the four counters decompose RefuseAgentTicks with exactly
        // one increment per finalised Refuse, never more (RU-06, F-A).
        if (count == 0)
        {
            if (finalizesRefuseOnFailure)
            {
                _pendingRouteRefusalReasons[index] =
                    RouteRefusalReason.NoCandidatesBuilt;
            }

            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var facing = ResolveCandidateFacing(
                agent,
                profile,
                provisionalPhase,
                threat,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw);
            var paceRaw = ResolveProposedPace(
                agent,
                profile,
                provisionalPhase,
                facing,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw,
                _movementRules);

            // A point destination is somewhere to arrive; the step is the
            // smaller of the resulting pace and the remaining distance
            // (design section 6.5). A direction candidate — an oblique, a
            // facing vector, an escape vector — has no arrival point, so the
            // full pace applies.
            var effectivePaceRaw = paceRaw;
            if (candidate.IsPointDestination)
            {
                var distanceRaw = IntegerSquareRoot(checked(
                    (candidate.DeltaXRaw * candidate.DeltaXRaw) +
                    (candidate.DeltaYRaw * candidate.DeltaYRaw)));
                effectivePaceRaw = (int)Math.Min(paceRaw, distanceRaw);
            }

            if (MovementRouteRules.StepEndpoint(
                agent.XRaw,
                agent.YRaw,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw,
                effectivePaceRaw,
                mapWidthRaw,
                mapHeightRaw,
                Scenario.BodyRadiusRaw) is not { } endpoint)
            {
                // Only the last candidate tried this call decides the
                // reason: an earlier candidate's continue is superseded the
                // moment a later one is attempted, so counting only the
                // final iteration keeps this counter mutually exclusive with
                // the other three and the four-way sum equal to
                // RefuseAgentTicks exactly (never a double count).
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.StepEndpointRejected;
                }

                continue;
            }

            if (candidate.SubjectToSecondThreatOmission &&
                ShouldOmitDirectCandidate(agent, context, endpoint))
            {
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.DirectCandidateOmitted;
                }

                continue;
            }

            if (!IsLaneClearOfAllies(
                index, agent, endpoint, actorClearanceSquared, usesMonotoneAllyClearance))
            {
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.LaneNotClear;
                }

                continue;
            }

            agent.Facing = facing;
            _movementProposals[index] =
                (endpoint.XRaw, endpoint.YRaw, candidate.EventTargetId);
            _proposedPaceRaw[index] = paceRaw;
            return true;
        }

        TurnFacingInPlace(agent, profile, provisionalPhase, threat);
        return false;
    }

    /// <summary>
    /// Resolves every pending route-refusal reason
    /// <c>TryProposeEquipmentRoute</c> recorded this tick against each
    /// agent's now-final <see cref="Movement.FootworkPhase"/>, incrementing
    /// exactly one of the four RU-06 counters per agent whose tick still
    /// finalises as <see cref="Movement.FootworkPhase.Refuse"/>. Must run
    /// after <see cref="ApplyEquipmentAttackFootworkAndDeathCleanup"/>: that
    /// pass can overwrite this same tick's Refuse with Commit for an agent
    /// whose gathered attack the combat stage accepted after the route was
    /// already rejected, and a reason recorded for a tick that never
    /// actually surfaces as Refuse must not be counted.
    /// </summary>
    private void ReconcileRouteRefusalReasonCounters()
    {
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var reason = _pendingRouteRefusalReasons[index];
            if (reason == RouteRefusalReason.None)
            {
                continue;
            }

            _pendingRouteRefusalReasons[index] = RouteRefusalReason.None;

            if (_agentStates[index].FootworkPhase != FootworkPhase.Refuse)
            {
                continue;
            }

            switch (reason)
            {
                case RouteRefusalReason.NoCandidatesBuilt:
                    _routeRefusalNoCandidatesBuilt =
                        checked(_routeRefusalNoCandidatesBuilt + 1);
                    break;
                case RouteRefusalReason.StepEndpointRejected:
                    _routeRefusalStepEndpointRejected =
                        checked(_routeRefusalStepEndpointRejected + 1);
                    break;
                case RouteRefusalReason.DirectCandidateOmitted:
                    _routeRefusalDirectCandidateOmitted =
                        checked(_routeRefusalDirectCandidateOmitted + 1);
                    break;
                case RouteRefusalReason.LaneNotClear:
                    _routeRefusalLaneNotClear =
                        checked(_routeRefusalLaneNotClear + 1);
                    break;
            }
        }
    }
}
