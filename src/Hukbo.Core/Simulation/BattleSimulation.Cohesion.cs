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
    /// One forward scan over the agent array, computing the living count and
    /// the lowest living <see cref="AgentState.EntityId"/> per faction. The
    /// comparison is against <see cref="AgentState.EntityId"/> explicitly, so
    /// the result does not depend on the incidental order of
    /// <see cref="_agentStates"/>. Runs before any intent is assigned, so no
    /// warrior's intent can depend on scan order either.
    /// </summary>
    /// <summary>
    /// Whether a follower that would otherwise be marked
    /// <see cref="AgentIntent.Regrouping"/> yields and keeps pursuing its own
    /// enemy instead, under either of the two conditions
    /// <see cref="MovementPresetId.LastStandEngagementV11"/> introduces: its
    /// faction's rally agent is itself engaged, or the follower's own selected
    /// enemy is already inside the follower's own weapon reach. Always
    /// <see langword="false"/> under every earlier preset, whose last-stand
    /// behaviour is therefore byte-identical to what it was before this
    /// method existed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The yield deliberately does no destination work of its own. Leaving the
    /// intent at <see cref="AgentIntent.Moving"/> routes the follower down the
    /// ordinary pursuit path, which already aims it at the target selection
    /// resolved for it a few lines above — nearest first, ties broken on
    /// <see cref="AgentState.EntityId"/> — so no second scan is written and no
    /// new ordering rule enters the tick. The standoff, cohesion, and
    /// sidestep behaviours that path carries keep applying to a follower that
    /// is now fighting rather than gathering.
    /// </para>
    /// <para>
    /// Both conditions test weapon reach rather than body contact. The
    /// existing override already yields at body contact by construction —
    /// <see cref="AgentIntent.Attacking"/> is assigned at two body radii a few
    /// lines above and beats <see cref="AgentIntent.Regrouping"/> — and it is
    /// precisely the band between reach and contact in which a warrior who can
    /// already strike is dragged away from the enemy it could strike.
    /// </para>
    /// </remarks>
    private bool YieldsRegroupingToEngagement(
        AgentState agent,
        long selectedDistance) =>
        YieldsLastStandEngagement(Scenario.MovementPreset) &&
        (_factionRallyEngaged[agent.FactionId] ||
            IsWithinAttackRange(agent, selectedDistance));

    /// <summary>
    /// The preset gate for both last-stand regroup yields, named once for the
    /// same reason <see cref="UsesBattlefieldRealism"/> is: the last-stand code
    /// it guards is shared and unversioned, so the set of presets that opt into
    /// the new behaviour is stated in one place rather than at each call site.
    /// <see cref="MovementPresetId.ContingentShapeV12"/> is admitted for the
    /// same reason <see cref="UsesBattlefieldRealism"/> admits it: it is
    /// defined as a strict superset of
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>'s behaviour, so
    /// it must keep V11's last-stand regroup yield unchanged before any
    /// contingent-shaping behaviour of its own is layered on top.
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/> is admitted for
    /// the same reason: it is a strict superset of V11's behaviour too, so
    /// it must keep the same last-stand regroup yield unchanged before its
    /// own lateral-riffle cohort traversal is layered on top.
    /// <see cref="MovementPresetId.ContingentCohesionBeforeContactV15"/> is
    /// admitted for the same reason again: it is a strict superset of
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/>'s behaviour, so it
    /// must keep the same last-stand regroup yield unchanged before its own
    /// contingent-cohesion gate is layered on top.
    /// </summary>
    private static bool YieldsLastStandEngagement(MovementPresetId preset) =>
        preset is MovementPresetId.LastStandEngagementV11
            or MovementPresetId.ContingentShapeV12
            or MovementPresetId.CohortLateralSpreadV13
            or MovementPresetId.EvasiveFootworkV14
            or MovementPresetId.ContingentCohesionBeforeContactV15
            or MovementPresetId.SeparatingContactV17;

    /// <summary>
    /// Derives <see cref="_factionRallyEngaged"/> for both factions: whether
    /// each faction's rally agent has a living enemy inside its own weapon
    /// reach. Runs immediately after <see cref="ComputeRallyAgents"/> and
    /// before any intent is assigned, so no follower's intent can depend on
    /// where its own rally agent happens to sit in the agent array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reduction is existential — does any qualifying enemy exist — which
    /// is order-independent by construction and needs no tie-break: which
    /// enemy satisfies it cannot change the answer, so the scan may stop at
    /// the first one it finds.
    /// </para>
    /// <para>
    /// The perception filter matches the one target selection applies, so this
    /// answers exactly "is the rally agent's own selected target within its
    /// reach" rather than a slightly different question about an enemy the
    /// rally agent cannot see. It costs nothing under any preset that does not
    /// opt in, and nothing in a battle with no faction at or below the
    /// last-stand threshold, because both are rejected before the scan.
    /// </para>
    /// </remarks>
    private void ComputeRallyEngagement()
    {
        _factionRallyEngaged[0] = false;
        _factionRallyEngaged[1] = false;

        if (Scenario.LastStandThresholdAgents <= 0 ||
            !YieldsLastStandEngagement(Scenario.MovementPreset))
        {
            return;
        }

        for (var faction = 0; faction < 2; faction++)
        {
            if (_factionLivingCounts[faction] >
                    Scenario.LastStandThresholdAgents ||
                _factionRallyEntityIds[faction] == 0 ||
                !TryGetAgentIndex(
                    _factionRallyEntityIds[faction], out var rallyIndex))
            {
                continue;
            }

            var rallyAgent = _agentStates[rallyIndex];
            var perceptionSquared = checked(
                (long)rallyAgent.PerceptionRangeRaw * rallyAgent.PerceptionRangeRaw);

            foreach (var candidate in _agentStates)
            {
                if (!candidate.IsAlive || candidate.FactionId == faction)
                {
                    continue;
                }

                var distance = SquaredDistance(rallyAgent, candidate);
                if (distance <= perceptionSquared &&
                    IsWithinAttackRange(rallyAgent, distance))
                {
                    _factionRallyEngaged[faction] = true;
                    break;
                }
            }
        }
    }

    private void ComputeRallyAgents()
    {
        _factionLivingCounts[0] = 0;
        _factionLivingCounts[1] = 0;
        _factionRallyEntityIds[0] = 0;
        _factionRallyEntityIds[1] = 0;

        foreach (var candidate in _agentStates)
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            var faction = candidate.FactionId;
            _factionLivingCounts[faction]++;

            if (_factionRallyEntityIds[faction] == 0 ||
                candidate.EntityId < _factionRallyEntityIds[faction])
            {
                _factionRallyEntityIds[faction] = candidate.EntityId;
            }
        }
    }

    /// <summary>
    /// The ninth tick stage: resolves every living contingent's
    /// <see cref="AgentState.ContingentState"/> for this tick under every
    /// persistent-contingent preset, and the two
    /// geometric gates <see cref="GatherMovementProposals"/>'s cohesion
    /// branch reads as array lookups rather than recomputing. Under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/> this returns on
    /// its first line and touches no contingent array at all, which is why
    /// the frozen preset's trajectory is untouched by this stage's
    /// existence. See
    /// the formation and movement realism design
    /// sections 3.4 and 3.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two forward passes, in that order, because the second cannot start
    /// until the first has finished: pass one is
    /// <see cref="MovementRules.ScanContingentLeadersAndLivingCounts"/>,
    /// which every later step needs a leader entity ID from; pass two walks
    /// every living agent once more to accumulate each contingent's
    /// <c>spreadSquared</c>, which needs this tick's leader to already be
    /// known, and its <c>contactCount</c> — a plain count of how many living
    /// members have a selected target within the close radius, not a
    /// minimum over any distance.
    /// </para>
    /// <para>
    /// The two geometric gates are computed once per contingent per tick,
    /// never once per agent: gate 5 (the map-edge open-ground test) in a
    /// single pass over the sixteen slots, and gate 6 (the cross-contingent
    /// overlap test) in a pairwise scan restricted to living same-faction
    /// slots, outer index ascending and inner index ascending from
    /// <c>outer + 1</c>, at most <c>C(8, 2) = 28</c> pairs per faction and 56
    /// in total. A slot with no living member is excluded from the pairwise
    /// scan entirely — its leader, trail base and margin are stale values
    /// from whichever tick it last had a living member, and comparing
    /// against them would deny cohesion on the strength of a square that no
    /// longer exists. Because
    /// <see cref="FormationRules.DoCohesionSquaresOverlap"/> is symmetric,
    /// both contingents of an overlapping pair are flagged together; there is
    /// no tie-break and none is needed.
    /// </para>
    /// <para>
    /// Either geometric denial forces <see cref="ContingentState.Advance"/>
    /// rather than <see cref="ContingentState.Hold"/>, the same way a shut
    /// duty-cycle window does, so the inspector never reports a contingent as
    /// <see cref="ContingentState.Hold"/> while its members are in fact
    /// pursuing independently. Gates 5 and 6 can therefore only ever remove a
    /// cohesion destination from a contingent that would otherwise have
    /// received one; they can never grant one.
    /// </para>
    /// <para>
    /// Every array this method writes is preallocated at construction and
    /// sized to <see cref="ContingentSlotCount"/>, so a warm tick allocates
    /// nothing.
    /// </para>
    /// </remarks>
    private void ResolveContingentStates()
    {
        if (Scenario.MovementPreset == MovementPresetId.IndependentPursuitV1)
        {
            return;
        }

        MovementRules.ScanContingentLeadersAndLivingCounts(
            _agentStates,
            _contingentLeaderEntityIds,
            _contingentLivingCounts,
            _movementRules.SelectsLeaderByRank);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            _contingentSpreadSquared[slot] = 0;
            _contingentContactCounts[slot] = 0;
        }

        // closeRadiusSquared does not vary by slot, so it is derived once
        // here rather than per slot, in the same checked long arithmetic the
        // per-slot cohesionRadiusRaw derivation below already uses.
        var closeRadiusRaw = checked(
            (long)_movementRules.CloseRadiusMultiplier * Scenario.BodyRadiusRaw);
        var closeRadiusSquared = checked(closeRadiusRaw * closeRadiusRaw);

        // Pass two: for every living agent, fold its squared distance to its
        // own contingent's leader into that slot's spread (skipping the
        // leader itself, which is always zero and never the farthest
        // non-leader member), and increment that slot's contact count when
        // its squared distance to its own selected target, if any, is at or
        // under closeRadiusSquared.
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            var leaderEntityId = _contingentLeaderEntityIds[slot];

            if (leaderEntityId != 0 &&
                agent.EntityId != leaderEntityId &&
                TryGetAgentIndex(leaderEntityId, out var leaderIndex))
            {
                var memberSquared = SquaredDistance(agent, _agentStates[leaderIndex]);
                if (memberSquared > _contingentSpreadSquared[slot])
                {
                    _contingentSpreadSquared[slot] = memberSquared;
                }
            }

            if (agent.TargetEntityId is { } targetId &&
                TryGetAgentIndex(targetId, out var targetIndex))
            {
                var distanceSquared = SquaredDistance(agent, _agentStates[targetIndex]);
                if (distanceSquared <= closeRadiusSquared)
                {
                    _contingentContactCounts[slot]++;
                }
            }
        }

        // Gate 5 (map-edge) and the trail-base/jitter/margin geometry gate 6
        // needs, once per living contingent.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            _contingentSquareOverlapsAnother[slot] = false;

            if (_contingentLivingCounts[slot] == 0)
            {
                _contingentSquareFitsMap[slot] = false;
                continue;
            }

            var leader = _agentStates[AgentIndexOf(_contingentLeaderEntityIds[slot])];
            var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                Scenario.BodyRadiusRaw,
                _contingentLivingCounts[slot]);
            var trailRaw = FormationRules.ComputeContingentTrailRaw(
                Scenario.BodyRadiusRaw,
                jitterRaw);
            var direction = ComputeRallyDirection(leader);
            var (trailBaseXRaw, trailBaseYRaw) = ComputeRallyTrailBase(
                leader,
                direction,
                trailRaw);

            _contingentJitterRaw[slot] = jitterRaw;
            _contingentTrailBaseXRaw[slot] = trailBaseXRaw;
            _contingentTrailBaseYRaw[slot] = trailBaseYRaw;

            // The half-side of the square this contingent claims. Without the
            // gate it is the unscaled packing margin -- the jitter radius plus
            // one body radius -- which is what every preset up to V13 claims.
            // With the gate it is a basis-point fraction of that margin, so a
            // contingent claims less ground than the packing bound alone would
            // give it and Hold stays reachable under a crowded deployment. The
            // arithmetic is a long multiply followed by an integer divide, so a
            // registered UnscaledCohesionSquareMarginBasisPoints reproduces the
            // unscaled margin exactly rather than approximately.
            //
            // Only the claimed margin is scaled. _contingentJitterRaw above is
            // left alone deliberately: it feeds the per-member offset in
            // ResolveContingentDestinations, and scaling it would change how
            // far apart members stand, which this change is expressly not
            // allowed to do.
            var unscaledMarginRaw = checked(jitterRaw + Scenario.BodyRadiusRaw);
            var marginRaw = _movementRules.GathersContingentsBeforeContact
                ? (int)((long)unscaledMarginRaw *
                        _movementRules.CohesionSquareMarginBasisPoints /
                    MovementRuleset.UnscaledCohesionSquareMarginBasisPoints)
                : unscaledMarginRaw;

            _contingentMarginRaw[slot] = marginRaw;

            // Gate 5 is handed the same half-side gate 6 reads out of
            // _contingentMarginRaw below, so the map-edge test and the
            // cross-contingent test cannot disagree about the square's size.
            _contingentSquareFitsMap[slot] =
                FormationRules.IsCohesionSquareWithinBoundsForMargin(
                    trailBaseXRaw,
                    trailBaseYRaw,
                    marginRaw,
                    Scenario.BodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw);
        }

        // A slot the narrowed scan excludes is also denied outright, and the
        // excluded set is exactly two states wide: a tick-start state of Close
        // or Break, and nothing else. MovementRules
        // .ParticipatesInCrossContingentScan tests those two values and no
        // other condition, so this loop is not a blanket denial that happens
        // to catch the two states -- it marks Close and Break slots, and there
        // is no third reason a living slot is ever marked here. Such a slot is
        // never tested against anyone, so granting it a cohesion destination
        // would park aim points inside a square no pair ever measured, which
        // is exactly the combined-density statement gate 6 exists to hold.
        // Marking it costs its neighbours nothing: a square absent from the
        // pairwise loop below can only ever produce fewer overlap findings for
        // its neighbours, never more. The denial answers for the excluded
        // slot's own square alone.
        //
        // The denial resolves it to Advance through transition rule 4 -- rules 1
        // and 3 still win first, so a Break stays Break and a latched Close
        // stays Close -- and an Advance takes part in the scan normally on the
        // next tick. Under a preset that does not narrow the scan this loop
        // marks nothing.
        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] != 0 &&
                !TakesPartInCrossContingentScan(slot))
            {
                _contingentSquareOverlapsAnother[slot] = true;
            }
        }

        // Gate 6: pairwise same-faction overlap, restricted to living slots,
        // outer index ascending and inner index ascending from outer + 1.
        // Under a preset that narrows the scan, a living slot whose tick-start
        // state is Close or Break takes no part on either side of the pair.
        for (var faction = 0; faction < 2; faction++)
        {
            var baseSlot = faction * FormationPlanner.MaximumContingents;

            for (var outer = 0; outer < FormationPlanner.MaximumContingents; outer++)
            {
                var outerSlot = baseSlot + outer;
                if (_contingentLivingCounts[outerSlot] == 0 ||
                    !TakesPartInCrossContingentScan(outerSlot))
                {
                    continue;
                }

                for (var inner = outer + 1;
                    inner < FormationPlanner.MaximumContingents;
                    inner++)
                {
                    var innerSlot = baseSlot + inner;
                    if (_contingentLivingCounts[innerSlot] == 0 ||
                        !TakesPartInCrossContingentScan(innerSlot))
                    {
                        continue;
                    }

                    if (!FormationRules.DoCohesionSquaresOverlap(
                        _contingentTrailBaseXRaw[outerSlot],
                        _contingentTrailBaseYRaw[outerSlot],
                        _contingentMarginRaw[outerSlot],
                        _contingentTrailBaseXRaw[innerSlot],
                        _contingentTrailBaseYRaw[innerSlot],
                        _contingentMarginRaw[innerSlot]))
                    {
                        continue;
                    }

                    _contingentSquareOverlapsAnother[outerSlot] = true;
                    _contingentSquareOverlapsAnother[innerSlot] = true;
                }
            }
        }

        // The six priority-ordered transition rules, per living slot. The
        // previous state is read from this slot's own current leader before
        // this loop overwrites it, because the leader is, by construction, a
        // living member and therefore carries the value forward.
        var tick = checked((int)Tick);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] == 0)
            {
                continue;
            }

            var leader = _agentStates[AgentIndexOf(_contingentLeaderEntityIds[slot])];
            var previousState = leader.ContingentState;
            var windowOpen = MovementRules.IsCohesionWindowOpen(
                tick,
                slot,
                _movementRules.CohesionCycleTicks,
                _movementRules.CohesionDutyTicks);
            var geometricGatesPass =
                _contingentSquareFitsMap[slot] && !_contingentSquareOverlapsAnother[slot];
            var cohesionRadiusRaw = checked(
                (long)_movementRules.CohesionRadiusMultiplier * Scenario.BodyRadiusRaw);

            _contingentResolvedStates[slot] = MovementRules.ResolveContingentState(
                previousState,
                _contingentLivingCounts[slot],
                _contingentInitialCounts[slot],
                _contingentSpreadSquared[slot],
                _contingentContactCounts[slot],
                cohesionRadiusRaw,
                _movementRules.CloseFractionNumerator,
                _movementRules.CloseFractionDenominator,
                _movementRules.MinimumCohesiveMembers,
                windowOpen,
                geometricGatesPass);
        }

        // Write each slot's resolved state onto every one of its living
        // members. The authoritative store is per agent; there is no
        // parallel per-contingent array the state hash cannot see.
        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            agent.ContingentState = _contingentResolvedStates[slot];
        }
    }

    /// <summary>
    /// Whether the living contingent in <paramref name="slot"/> takes part in
    /// movement gate 6's cross-contingent scan this tick. Always true under a
    /// preset that does not narrow the scan, so the two frozen
    /// persistent-contingent presets keep the behaviour their own recorded
    /// expectations pin; under a narrowing preset, a contingent whose
    /// tick-start state is Close or Break is skipped, because it can park no
    /// cohesion aim point.
    /// </summary>
    /// <remarks>
    /// The caller has already established that this slot has a living member,
    /// so <c>_contingentLeaderEntityIds[slot]</c> is a real entity id and the
    /// two lookups below cannot miss. The state read is the leader's, which
    /// still carries the previous tick's value: the loop that overwrites every
    /// living member's <c>ContingentState</c> runs after the gate that calls
    /// this.
    /// </remarks>
    /// <param name="slot"><c>FactionId * MaximumContingents + ContingentId</c>.</param>
    /// <returns>
    /// <see langword="true"/> when this contingent's bias square participates.
    /// </returns>
    private bool TakesPartInCrossContingentScan(int slot)
    {
        if (!_movementRules.NarrowsCohesionScanToCohesionCapableContingents)
        {
            return true;
        }

        var leader = _agentStates[AgentIndexOf(_contingentLeaderEntityIds[slot])];
        return MovementRules.ParticipatesInCrossContingentScan(leader.ContingentState);
    }

    /// <summary>
    /// Evaluates the six movement gates of design section 3.5 for one
    /// <see cref="AgentIntent.Moving"/> agent under every
    /// persistent-contingent preset and, when every
    /// gate permits, computes its cohesion aim point — the give-way escape
    /// when the agent stands in its own leader's forward corridor, otherwise
    /// the trail base plus this member's personal offset from
    /// <see cref="ContingentOffset.Compute"/> — matching
    /// <see cref="BuildRegroupingProposal"/>'s own trail-then-offset shape.
    /// </summary>
    /// <remarks>
    /// Gates 5 and 6 are read here as the array values
    /// <see cref="ResolveContingentStates"/> already computed this tick, not
    /// recomputed: gate 6 in particular cannot be evaluated correctly inside
    /// a per-agent loop, because the loop reaches one contingent's members
    /// before it has reached every other contingent whose squares decide the
    /// answer.
    /// </remarks>
    /// <param name="agent">The moving agent to evaluate.</param>
    /// <param name="tick">The current tick, truncated to <see cref="int"/>.</param>
    /// <param name="aimXRaw">
    /// The computed aim point's X, valid only when this method returns
    /// <see langword="true"/>.
    /// </param>
    /// <param name="aimYRaw">
    /// The computed aim point's Y, valid only when this method returns
    /// <see langword="true"/>.
    /// </param>
    /// <param name="leaderEntityId">
    /// This agent's contingent leader's <see cref="AgentState.EntityId"/>,
    /// valid only when this method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all six gates permit a cohesion
    /// destination for this agent this tick.
    /// </returns>
    private bool TryResolveContingentCohesionAimPoint(
        AgentState agent,
        int tick,
        out int aimXRaw,
        out int aimYRaw,
        out ulong leaderEntityId)
    {
        aimXRaw = 0;
        aimYRaw = 0;

        var slot = checked(
            (agent.FactionId * FormationPlanner.MaximumContingents) +
            agent.ContingentId);
        leaderEntityId = _contingentLeaderEntityIds[slot];

        if (leaderEntityId == 0 ||
            !TryGetAgentIndex(leaderEntityId, out var leaderIndex))
        {
            return false;
        }

        var state = agent.ContingentState;
        var isLeader = agent.EntityId == leaderEntityId;
        var windowOpen = MovementRules.IsCohesionWindowOpen(
            tick,
            slot,
            _movementRules.CohesionCycleTicks,
            _movementRules.CohesionDutyTicks);

        var leader = _agentStates[leaderIndex];
        var straggling = false;
        if (state == ContingentState.Advance)
        {
            var memberSquared = SquaredDistance(agent, leader);
            var cohesionRadiusRaw = checked(
                (long)_movementRules.CohesionRadiusMultiplier * Scenario.BodyRadiusRaw);

            // memberSquared is an unscaled squared world distance bounded
            // only by Scenario.MaximumMapDimension, so 16 * memberSquared can
            // exceed long.MaxValue on a wide map. Widening to Int128 makes
            // the comparison exact and total: it reproduces the checked long
            // comparison bit-for-bit whenever that comparison would not have
            // overflowed, and for the inputs that would have overflowed it
            // returns the answer implied by unbounded integer arithmetic — a
            // member that far from its leader is unambiguously straggling.
            if (_movementRules.GathersContingentsBeforeContact)
            {
                // The registered band replaces the hardcoded three-quarters
                // fraction: a member is straggling while its squared distance
                // from its leader exceeds the square of the band fraction of
                // the cohesion radius. Cross-multiplying by the squared
                // denominator keeps the test in exact integers instead of
                // dividing, exactly as the unregistered statement below does.
                // The widening argument above covers this form too: the
                // denominator is an int, so its square is below 2^62, and the
                // product with memberSquared stays far inside Int128's range.
                var bandNumerator = (Int128)_movementRules.CohesionBandNumerator;
                var bandDenominator =
                    (Int128)_movementRules.CohesionBandDenominator;

                straggling = bandDenominator * bandDenominator * memberSquared >
                    bandNumerator * bandNumerator *
                        cohesionRadiusRaw * cohesionRadiusRaw;
            }
            else
            {
                straggling = (Int128)16 * memberSquared >
                    (Int128)9 * cohesionRadiusRaw * cohesionRadiusRaw;
            }
        }

        if (!MovementRules.IsCohesionEligible(
            state,
            isLeader,
            windowOpen,
            straggling,
            _contingentSquareFitsMap[slot],
            _contingentSquareOverlapsAnother[slot]))
        {
            return false;
        }

        var direction = ComputeRallyDirection(leader);
        if (direction.DistanceRaw > 0 &&
            TryComputeGiveWayAimPoint(agent, leader, direction) is
            { XRaw: var giveWayXRaw, YRaw: var giveWayYRaw })
        {
            aimXRaw = giveWayXRaw;
            aimYRaw = giveWayYRaw;
            return true;
        }

        var (offsetXRaw, offsetYRaw) = ContingentOffset.Compute(
            Scenario.Seed,
            agent.EntityId,
            _contingentJitterRaw[slot]);

        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(
                (long)_contingentTrailBaseXRaw[slot] + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(
                (long)_contingentTrailBaseYRaw[slot] + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return true;
    }

    /// <summary>
    /// Builds the movement proposal that closes a regrouping follower on its
    /// faction's rally agent plus that follower's own fixed positional bias
    /// (<see cref="RallyOffset"/>). Returns <c>null</c> — propose no movement
    /// — in two cases: the arrived-guard, when the follower's squared distance
    /// to its aim point is already at or inside
    /// <see cref="CollisionGeometry.ContactSquaredDistance"/>, which stops the
    /// one-raw-unit movement-floor twitch and the resulting <c>Move</c>-event
    /// flood a settled cluster would otherwise emit every tick; and the
    /// defensive case where the rally entity ID is the zero sentinel or the
    /// rally agent cannot be resolved to a living agent, which falls back to
    /// no movement rather than throwing.
    /// </summary>
    /// <remarks>
    /// The follower's aim point trails <see cref="FormationRules.RallyTrailRadiusMultiplier"/>
    /// body radii behind the rally agent, opposite the rally agent's own
    /// direction of travel, before the jitter offset is applied. Without the
    /// trail, a follower whose jitter offset happens to point along the rally
    /// agent's forward arc parks permanently in front of its own leader and
    /// blocks it — the rally agent is exempt from regrouping and never routes
    /// around its own formation, so that block never clears. Two factions
    /// doing this simultaneously deadlock the whole battle at the tick limit.
    /// See the type-level remarks on <see cref="FormationRules"/> for the
    /// clearance derivation.
    /// </remarks>
    /// <remarks>
    /// The trail alone is not enough: a follower can start the tick already
    /// ahead of the rally agent, in which case its trail-behind aim point
    /// sits on the far side of the leader's own body, and reaching it in a
    /// straight line means walking backward through the leader. See
    /// <see cref="TryComputeGiveWayAimPoint"/> for the sideways escape this
    /// method checks first.
    /// </remarks>
    /// <remarks>
    /// A follower whose aim point lies beyond an ally walks to exact tangency
    /// and then pushes against it forever, because tangency is a legal resting
    /// position and closing further is strict penetration. The stall generation
    /// is the escape: once this agent has been blocked for
    /// <see cref="FormationRules.StallEscapeStreakTicks"/> consecutive ticks it
    /// draws a different aim point. It is 0, and therefore inert, in every
    /// battle that is merely crowded.
    /// </remarks>
    /// <summary>
    /// The pursuit-path counterpart of this type's rally stall escape: a
    /// warrior walking at an enemy that a comrade's body has refused for
    /// <see cref="FormationRules.StallEscapeStreakTicks"/> consecutive ticks
    /// aims beside its enemy rather than at its enemy's centre, which puts it
    /// on a different line of approach and out from behind the body that was
    /// refusing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only reached at a non-zero stall generation. The design is in
    /// the 2026-07-29 approach sidestep design, and its section
    /// 2 records why the rally escape alone was not enough: that escape lives in
    /// <see cref="BuildRegroupingProposal"/> and a pursuing warrior never
    /// reaches it, so a locked cluster could contain warriors with a way out and
    /// warriors without one at the same time.
    /// </para>
    /// <para>
    /// The contingent cohesion branch above this one is deliberately left
    /// alone. Its aim point sits inside a bias square whose combined
    /// aim-point density statement movement gate 6 exists to hold, and
    /// displacing an agent out of that square would invalidate the statement.
    /// A contingent that cannot make progress resolves to
    /// <see cref="ContingentState.Advance"/> and its members fall back to this
    /// path anyway.
    /// </para>
    /// </remarks>
    /// <param name="agent">The blocked pursuer.</param>
    /// <param name="target">The enemy it is walking at.</param>
    /// <param name="stallGeneration">
    /// The pursuer's current stall generation, which the caller has already
    /// established is non-zero.
    /// </param>
    /// <param name="stopShortRaw">
    /// Overrides the stopping distance used when the offset resolves to zero
    /// and this falls back to closing straight on <paramref name="target"/>.
    /// <see langword="null"/> keeps the original two-body-radius stop; a
    /// ranged pursuer beyond its standoff distance passes that distance here
    /// instead, so a stalled ranged agent's fallback still stops at standoff
    /// rather than walking in to body contact.
    /// </param>
    /// <returns>The pursuer's movement proposal against the offset aim point.</returns>
    private (int XRaw, int YRaw, ulong TargetId) BuildSidesteppingPursuitProposal(
        AgentState agent,
        AgentState target,
        int stallGeneration,
        int? stopShortRaw = null)
    {
        var deltaXRaw = (long)target.XRaw - agent.XRaw;
        var deltaYRaw = (long)target.YRaw - agent.YRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        var (offsetXRaw, offsetYRaw) = ApproachSidestep.Compute(
            Scenario.Seed,
            agent.EntityId,
            Scenario.BodyRadiusRaw,
            stallGeneration,
            deltaXRaw,
            deltaYRaw,
            distanceRaw);

        if (offsetXRaw == 0 && offsetYRaw == 0)
        {
            return stopShortRaw is { } directStopShortRaw
                ? BuildMovementProposal(
                    agent, target.XRaw, target.YRaw, target.EntityId, directStopShortRaw)
                : BuildMovementProposal(agent, target);
        }

        // Saturated and clamped the same way BuildRegroupingProposal handles its
        // own aim point: the offset is bounded by a few body radii, but the
        // target may already stand against a map edge.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)target.XRaw + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)target.YRaw + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return stopShortRaw is { } aimStopShortRaw
            ? BuildMovementProposal(agent, aimXRaw, aimYRaw, target.EntityId, aimStopShortRaw)
            : BuildMovementProposal(agent, aimXRaw, aimYRaw, target.EntityId);
    }

    private (int XRaw, int YRaw, ulong TargetId)? BuildRegroupingProposal(
        AgentState agent,
        int agentIndex)
    {
        if (TryComputeRegroupingAimPoint(agent, agentIndex) is not
            { } aim)
        {
            return null;
        }

        return BuildMovementProposal(
            agent,
            aim.XRaw,
            aim.YRaw,
            aim.RallyEntityId);
    }

    /// <summary>
    /// The destination half of <see cref="BuildRegroupingProposal"/>,
    /// extracted so the equipment-relative footwork pipeline can constrain
    /// the same last-stand aim point with its own facing-and-pace step
    /// while every legacy preset keeps building the identical proposal
    /// through the wrapper above. All of the original selection logic —
    /// the rally resolution, the give-way corridor escape, the
    /// trail-plus-jitter aim point, and the arrived-guard — lives here
    /// unchanged and in the original order.
    /// </summary>
    private (int XRaw, int YRaw, ulong RallyEntityId)?
        TryComputeRegroupingAimPoint(AgentState agent, int agentIndex)
    {
        var rallyEntityId = _factionRallyEntityIds[agent.FactionId];
        if (rallyEntityId == 0 ||
            !TryGetAgentIndex(rallyEntityId, out var rallyIndex))
        {
            return null;
        }

        var rallyAgent = _agentStates[rallyIndex];
        if (!rallyAgent.IsAlive)
        {
            return null;
        }

        var direction = ComputeRallyDirection(rallyAgent);

        if (direction.DistanceRaw > 0 &&
            TryComputeGiveWayAimPoint(agent, rallyAgent, direction) is
            { XRaw: var giveWayXRaw, YRaw: var giveWayYRaw })
        {
            return (giveWayXRaw, giveWayYRaw, rallyAgent.EntityId);
        }

        var trailRaw = FormationRules.ComputeRallyTrailRaw(Scenario.BodyRadiusRaw);
        var (trailBaseXRaw, trailBaseYRaw) = ComputeRallyTrailBase(
            rallyAgent,
            direction,
            trailRaw);

        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            Scenario.Seed,
            agent.EntityId,
            Scenario.BodyRadiusRaw,
            _collision.StallGeneration(agentIndex));

        // The aim point is computed in long and saturated into int the same
        // way CollisionResolver.ToCoordinate saturates a candidate coordinate
        // before its own boundary clamp: safe, because ClampCenterToBounds
        // immediately pulls anything outside the map back to the edge.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseXRaw + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseYRaw + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
            agent.XRaw,
            agent.YRaw,
            aimXRaw,
            aimYRaw);
        if (squaredDistanceToAim <=
            CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
        {
            return null;
        }

        // The event log names the rally agent as the target, not the enemy
        // the follower would otherwise be chasing, so a spectator reads
        // "entity N moved toward entity <rally>" during a last stand.
        return (aimXRaw, aimYRaw, rallyAgent.EntityId);
    }

    /// <summary>
    /// Computes a leader's direction of travel — the vector from the leader
    /// to its own enemy target, plus the integer distance between them —
    /// used both by <see cref="ComputeRallyTrailBase"/> and by
    /// <see cref="TryComputeGiveWayAimPoint"/> for the corridor test. Returns
    /// a zero <c>DistanceRaw</c> sentinel, meaning "no direction", when the
    /// leader has no target, when the target cannot be resolved to a living
    /// agent, or when the leader is already exactly at its target's
    /// position.
    /// </summary>
    /// <remarks>
    /// Generalised from the last-stand rally agent to any leader: the rally
    /// path calls this with the faction's rally agent, and the
    /// persistent-contingent cohesion path (design section 3.5) calls it
    /// with a contingent's own leader. Neither this method nor the two below
    /// it read the faction rally state directly.
    /// </remarks>
    private (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) ComputeRallyDirection(
        AgentState leader)
    {
        if (leader.TargetEntityId is not { } targetId ||
            !TryGetAgentIndex(targetId, out var targetIndex))
        {
            return (0, 0, 0);
        }

        var target = _agentStates[targetIndex];
        var deltaXRaw = (long)target.XRaw - leader.XRaw;
        var deltaYRaw = (long)target.YRaw - leader.YRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        return (deltaXRaw, deltaYRaw, distanceRaw);
    }

    /// <summary>
    /// Computes the point <paramref name="trailRaw"/> raw units behind the
    /// leader, opposite the leader's own direction of travel — the point a
    /// follower's jitter offset is added to. Falls back to the leader's raw
    /// position (no trail) when <paramref name="direction"/> carries the "no
    /// direction" sentinel (<c>DistanceRaw == 0</c>), since there is no
    /// direction of travel to trail behind. That fallback preserves the
    /// pre-fix behaviour in that corner, which is otherwise untouched by
    /// this method's own logic.
    /// </summary>
    /// <remarks>
    /// The trail distance is a parameter rather than computed here so this
    /// method has no opinion on which formula produced it: the last-stand
    /// rally caller passes <see cref="FormationRules.ComputeRallyTrailRaw"/>
    /// and the persistent-contingent cohesion caller (design section 3.5)
    /// passes <see cref="FormationRules.ComputeContingentTrailRaw"/>.
    /// </remarks>
    private (int XRaw, int YRaw) ComputeRallyTrailBase(
        AgentState leader,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction,
        int trailRaw)
    {
        if (direction.DistanceRaw == 0)
        {
            return (leader.XRaw, leader.YRaw);
        }

        var trailXRaw = SaturateToInt32(checked(
            leader.XRaw - (direction.DeltaXRaw * trailRaw / direction.DistanceRaw)));
        var trailYRaw = SaturateToInt32(checked(
            leader.YRaw - (direction.DeltaYRaw * trailRaw / direction.DistanceRaw)));

        return (trailXRaw, trailYRaw);
    }

    /// <summary>
    /// Checks whether a regrouping follower's tick-start position falls
    /// inside its own leader's forward give-way corridor and, if so, returns
    /// a pure-sideways aim point that clears it. Returns
    /// <see langword="null"/> when the follower is not in the corridor, in
    /// which case <see cref="BuildRegroupingProposal"/> falls back to the
    /// ordinary trail-plus-jitter aim point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Let <c>L</c> be the leader's tick-start position, <c>d</c> the unit
    /// direction from <c>L</c> toward the leader's own target (the same
    /// direction <see cref="ComputeRallyTrailBase"/> trails behind), <c>F</c>
    /// the follower's tick-start position, and <c>r = F - L</c>.
    /// <c>forward</c> is the scalar projection of <c>r</c> onto <c>d</c>;
    /// <c>lateral</c> is the scalar projection of <c>r</c> onto the
    /// perpendicular of <c>d</c>. The follower is in the corridor when
    /// <c>forward &gt; 0</c> (ahead of the leader) and
    /// <c>|lateral| &lt; corridor half-width</c>.
    /// </para>
    /// <para>
    /// The escape point is <c>F</c> plus a step purely along the perpendicular
    /// of <c>d</c>, signed toward the side the follower is already on
    /// (<c>sign(lateral)</c>) so the step can only move it further from the
    /// corridor centre, never across it. A follower sitting exactly on the
    /// leader's axis (<c>lateral == 0</c>) always steps toward the same,
    /// fixed perpendicular side — never decided by iteration or array order.
    /// Because the step has no component along <c>d</c>, the follower's
    /// forward position is unchanged: this is sideways motion only, so the
    /// follower can never re-enter the corridor by taking this step.
    /// </para>
    /// </remarks>
    private (int XRaw, int YRaw)? TryComputeGiveWayAimPoint(
        AgentState agent,
        AgentState leader,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction)
    {
        var relativeXRaw = (long)agent.XRaw - leader.XRaw;
        var relativeYRaw = (long)agent.YRaw - leader.YRaw;

        var forwardRaw = checked(
            (relativeXRaw * direction.DeltaXRaw) +
            (relativeYRaw * direction.DeltaYRaw)) / direction.DistanceRaw;
        if (forwardRaw <= 0)
        {
            return null;
        }

        var lateralRaw = checked(
            (relativeXRaw * direction.DeltaYRaw) -
            (relativeYRaw * direction.DeltaXRaw)) / direction.DistanceRaw;
        var corridorHalfWidthRaw =
            FormationRules.ComputeRallyCorridorHalfWidthRaw(Scenario.BodyRadiusRaw);
        if (Math.Abs(lateralRaw) >= corridorHalfWidthRaw)
        {
            return null;
        }

        // Tie-break: exactly on the leader's axis always steps toward the
        // fixed "+" perpendicular side, so the escape direction never
        // depends on which side of zero a rounding error happened to land
        // on, or on the order agents were supplied in.
        var perpendicularSign = lateralRaw < 0 ? -1L : 1L;
        var stepOutRaw = checked(corridorHalfWidthRaw + Scenario.BodyRadiusRaw);

        var aimXRaw = SaturateToInt32(checked(
            agent.XRaw +
            (perpendicularSign * direction.DeltaYRaw * stepOutRaw /
                direction.DistanceRaw)));
        var aimYRaw = SaturateToInt32(checked(
            agent.YRaw -
            (perpendicularSign * direction.DeltaXRaw * stepOutRaw /
                direction.DistanceRaw)));

        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        aimXRaw = CollisionGeometry.ClampCenterToBounds(
            aimXRaw,
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        aimYRaw = CollisionGeometry.ClampCenterToBounds(
            aimYRaw,
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return (aimXRaw, aimYRaw);
    }

    /// <summary>
    /// Saturates a long coordinate into <see cref="int"/>, mirroring
    /// <c>CollisionResolver.ToCoordinate</c>. Safe because the caller always
    /// runs the result through <see cref="CollisionGeometry.ClampCenterToBounds"/>
    /// immediately afterward.
    /// </summary>
    private static int SaturateToInt32(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);
}
