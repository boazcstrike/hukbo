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
    /// The candidate table of design section 10.4, one phase per arm. Every
    /// candidate is a delta from the actor's tick-start position; the span
    /// is filled in the phase's own preference order.
    /// </summary>
    private int BuildEquipmentRouteCandidates(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        Span<EquipmentRouteCandidate> candidates)
    {
        var count = 0;
        var sideAClockwise = MovementRouteRules.SideAIsWorldClockwise(
            _factionLocalIndexes[index], agent.FactionId);

        switch (provisionalPhase)
        {
            case FootworkPhase.Approach:
            case FootworkPhase.Engage:
                {
                    if (threat is null)
                    {
                        return 0;
                    }

                    var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                    var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                    if (deltaXRaw == 0 && deltaYRaw == 0)
                    {
                        return 0;
                    }

                    // The preferred distance is not a stop line: both phases
                    // continue toward the target's centre so the existing
                    // post-movement reach test stays authoritative.
                    var direct = new EquipmentRouteCandidate(
                        deltaXRaw,
                        deltaYRaw,
                        IsPointDestination: true,
                        SubjectToSecondThreatOmission: true,
                        threat.EntityId);
                    var (sideAX, sideAY) = MovementRouteRules.RotateOblique(
                        deltaXRaw, deltaYRaw, sideAClockwise);
                    var (sideBX, sideBY) = MovementRouteRules.RotateOblique(
                        deltaXRaw, deltaYRaw, !sideAClockwise);
                    var sideA = new EquipmentRouteCandidate(
                        sideAX,
                        sideAY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    var sideB = new EquipmentRouteCandidate(
                        sideBX,
                        sideBY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);

                    // Engage against two or more occupied enemy loadout buckets
                    // prefers the obliques; a homogeneous composition, and every
                    // approach, goes direct first.
                    var sidesFirst = provisionalPhase == FootworkPhase.Engage &&
                        MovementRouteRules.OccupiedLoadoutBuckets(
                            _localMovementContexts[index].EnemyComposition) >= 2;
                    if (sidesFirst)
                    {
                        candidates[count++] = sideA;
                        candidates[count++] = sideB;
                        candidates[count++] = direct;
                    }
                    else
                    {
                        candidates[count++] = direct;
                        candidates[count++] = sideA;
                        candidates[count++] = sideB;
                    }

                    return count;
                }

            case FootworkPhase.Commit:
                {
                    if (threat is not null)
                    {
                        var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                        var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                        if (deltaXRaw == 0 && deltaYRaw == 0)
                        {
                            return 0;
                        }

                        candidates[count++] = new EquipmentRouteCandidate(
                            deltaXRaw,
                            deltaYRaw,
                            IsPointDestination: true,
                            SubjectToSecondThreatOmission: false,
                            threat.EntityId);
                        return count;
                    }

                    if (agent.Facing == Facing16.None)
                    {
                        return 0;
                    }

                    var (facingX, facingY) = FacingRules.SectorVector(agent.Facing);
                    candidates[count++] = new EquipmentRouteCandidate(
                        facingX,
                        facingY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        agent.EntityId);
                    return count;
                }

            case FootworkPhase.Recover:
                {
                    long baseXRaw;
                    long baseYRaw;
                    ulong eventTargetId;
                    if (agent.Facing != Facing16.None)
                    {
                        var opposite = (Facing16)(((int)agent.Facing + 8) % 16);
                        var (oppositeX, oppositeY) =
                            FacingRules.SectorVector(opposite);
                        baseXRaw = oppositeX;
                        baseYRaw = oppositeY;
                        eventTargetId = threat?.EntityId ?? agent.EntityId;
                    }
                    else if (threat is not null)
                    {
                        baseXRaw = (long)agent.XRaw - threat.XRaw;
                        baseYRaw = (long)agent.YRaw - threat.YRaw;
                        if (baseXRaw == 0 && baseYRaw == 0)
                        {
                            return 0;
                        }

                        eventTargetId = threat.EntityId;
                    }
                    else
                    {
                        return 0;
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        baseXRaw,
                        baseYRaw,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    var (sideAX, sideAY) = MovementRouteRules.RotateOblique(
                        baseXRaw, baseYRaw, sideAClockwise);
                    var (sideBX, sideBY) = MovementRouteRules.RotateOblique(
                        baseXRaw, baseYRaw, !sideAClockwise);
                    candidates[count++] = new EquipmentRouteCandidate(
                        sideAX,
                        sideAY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    candidates[count++] = new EquipmentRouteCandidate(
                        sideBX,
                        sideBY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    return count;
                }

            case FootworkPhase.Disengage:
            case FootworkPhase.Regroup:
                {
                    var context = _localMovementContexts[index];
                    var anchorExists = false;
                    if (context.NearestAllyEntityId is { } allyId &&
                        TryGetAgentIndex(allyId, out var allyIndex))
                    {
                        var ally = _agentStates[allyIndex];
                        if (ally.IsAlive)
                        {
                            anchorExists = true;
                            var deltaXRaw = (long)ally.XRaw - agent.XRaw;
                            var deltaYRaw = (long)ally.YRaw - agent.YRaw;
                            if (deltaXRaw != 0 || deltaYRaw != 0)
                            {
                                candidates[count++] = new EquipmentRouteCandidate(
                                    deltaXRaw,
                                    deltaYRaw,
                                    IsPointDestination: true,
                                    SubjectToSecondThreatOmission: false,
                                    allyId);
                            }
                        }
                    }

                    var slot = checked(
                        (agent.FactionId * FormationPlanner.MaximumContingents) +
                        agent.ContingentId);
                    var leaderEntityId = _contingentLeaderEntityIds[slot];
                    if (leaderEntityId != 0 &&
                        leaderEntityId != agent.EntityId &&
                        TryGetAgentIndex(leaderEntityId, out var leaderIndex))
                    {
                        var leader = _agentStates[leaderIndex];
                        if (leader.IsAlive)
                        {
                            anchorExists = true;
                            var deltaXRaw = (long)leader.XRaw - agent.XRaw;
                            var deltaYRaw = (long)leader.YRaw - agent.YRaw;
                            if (deltaXRaw != 0 || deltaYRaw != 0)
                            {
                                candidates[count++] = new EquipmentRouteCandidate(
                                    deltaXRaw,
                                    deltaYRaw,
                                    IsPointDestination: true,
                                    SubjectToSecondThreatOmission: false,
                                    leaderEntityId);
                            }
                        }
                    }

                    // Regroup stops at ally-then-leader; only Disengage owns the
                    // escape fallback, and only when neither anchor exists at
                    // all (design section 10.4).
                    if (provisionalPhase == FootworkPhase.Regroup || anchorExists)
                    {
                        return count;
                    }

                    if (threat is null)
                    {
                        return 0;
                    }

                    var escapeFromNearestX = (long)agent.XRaw - threat.XRaw;
                    var escapeFromNearestY = (long)agent.YRaw - threat.YRaw;
                    long escapeFromSecondX = 0;
                    long escapeFromSecondY = 0;
                    if (context.SecondThreatEntityId is { } secondThreatId &&
                        TryGetAgentIndex(secondThreatId, out var secondIndex))
                    {
                        var second = _agentStates[secondIndex];
                        if (second.IsAlive)
                        {
                            escapeFromSecondX = (long)agent.XRaw - second.XRaw;
                            escapeFromSecondY = (long)agent.YRaw - second.YRaw;
                        }
                    }

                    var escapeXRaw = checked(escapeFromNearestX + escapeFromSecondX);
                    var escapeYRaw = checked(escapeFromNearestY + escapeFromSecondY);
                    if (escapeXRaw == 0 && escapeYRaw == 0)
                    {
                        if (escapeFromNearestX == 0 && escapeFromNearestY == 0)
                        {
                            return 0;
                        }

                        (escapeXRaw, escapeYRaw) =
                            MovementRouteRules.PerpendicularVector(
                                escapeFromNearestX,
                                escapeFromNearestY,
                                sideAClockwise);
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        escapeXRaw,
                        escapeYRaw,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    return count;
                }

            case FootworkPhase.Pursue:
                {
                    if (threat is null ||
                        !HasAllyWithinPursuitSupport(index, agent, profile))
                    {
                        return 0;
                    }

                    var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                    var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                    if (deltaXRaw == 0 && deltaYRaw == 0)
                    {
                        return 0;
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        deltaXRaw,
                        deltaYRaw,
                        IsPointDestination: true,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    return count;
                }

            default:
                return 0;
        }
    }

    /// <summary>
    /// The second-threat rule of design section 10.4: with two or more
    /// immediate enemies, the direct candidate is omitted only when its
    /// endpoint sits strictly closer to the second threat than the actor's
    /// tick-start position does. Exact equality keeps the direct candidate.
    /// </summary>
    private bool ShouldOmitDirectCandidate(
        AgentState agent,
        in LocalMovementContext context,
        (int XRaw, int YRaw) endpoint)
    {
        if (context.ImmediateEnemies < 2 ||
            context.SecondThreatEntityId is not { } secondThreatId ||
            !TryGetAgentIndex(secondThreatId, out var secondIndex))
        {
            return false;
        }

        var second = _agentStates[secondIndex];
        if (!second.IsAlive)
        {
            return false;
        }

        var endpointSquared = CollisionGeometry.SquaredDistance(
            endpoint.XRaw, endpoint.YRaw, second.XRaw, second.YRaw);
        var startSquared = CollisionGeometry.SquaredDistance(
            agent.XRaw, agent.YRaw, second.XRaw, second.YRaw);
        return endpointSquared < startSquared;
    }

    /// <summary>
    /// The lane-clearance test of design section 10.5: one stable scan over
    /// every living same-faction agent's tick-start position, rejecting a
    /// candidate endpoint that sits at squared distance strictly less than
    /// the square of the larger of the actor's and that ally's clearance
    /// radii. Exact equality is clear, matching the collision stage's
    /// strict-less tangency convention. No neighbours are stored and
    /// nothing allocates.
    ///
    /// <paramref name="isMonotone"/> -- <see langword="true"/> only under
    /// <see cref="MovementPresetId.MonotoneAllyClearanceV9"/> -- narrows that
    /// absolute rule to a monotonicity constraint (design section 10.3, plan
    /// task RU-30, F-B): a candidate whose endpoint is still inside a given
    /// ally's clearance radius is rejected only when it also moves the actor
    /// strictly closer to that same ally than the actor's own tick-start
    /// position already was. An actor already standing inside an ally's
    /// clearance radius may hold, sidestep, or retreat; only a step that
    /// tightens that specific, pre-existing violation is refused. This is
    /// what F-A's root-cause finding named: the absolute rule punished
    /// movement out of a violation, not the violation itself. V1 through V8
    /// are unaffected -- <paramref name="isMonotone"/> is <see langword="false"/>
    /// for every one of them, so this method's decision is byte-identical to
    /// the rule above for every preset except V9.
    /// </summary>
    private bool IsLaneClearOfAllies(
        int selfIndex,
        AgentState agent,
        (int XRaw, int YRaw) endpoint,
        Int128 actorClearanceSquared,
        bool isMonotone)
    {
        var actorXRaw = agent.XRaw;
        var actorYRaw = agent.YRaw;

        for (var otherIndex = 0; otherIndex < _agentStates.Length; otherIndex++)
        {
            if (otherIndex == selfIndex)
            {
                continue;
            }

            var ally = _agentStates[otherIndex];
            if (!ally.IsAlive || ally.FactionId != agent.FactionId)
            {
                continue;
            }

            var required = Int128.Max(
                actorClearanceSquared,
                SquaredClearanceRadius(
                    _movementRules.ResolveLoadoutProfile(ally.Loadout)));
            var separation = (Int128)CollisionGeometry.SquaredDistance(
                endpoint.XRaw, endpoint.YRaw, ally.XRaw, ally.YRaw);
            if (separation >= required)
            {
                continue;
            }

            if (!isMonotone)
            {
                return false;
            }

            // Actor's tick-start separation to this same ally. Depends only
            // on the actor's fixed tick-start position (hoisted above, out
            // of this per-ally loop's repeated field reads) and the ally's
            // own tick-start position -- never on the candidate endpoint --
            // so it is the monotonicity baseline every candidate this call's
            // caller tries is compared against.
            var currentSeparation = (Int128)CollisionGeometry.SquaredDistance(
                actorXRaw, actorYRaw, ally.XRaw, ally.YRaw);
            if (separation < currentSeparation)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether at least one living ally stands within the actor's
    /// pursuit-support distance, inclusive at the exact radius like every
    /// context ring — the condition under which <see cref="FootworkPhase.Pursue"/>
    /// keeps proposing a direct route (design section 10.4).
    /// </summary>
    private bool HasAllyWithinPursuitSupport(
        int selfIndex,
        AgentState agent,
        LoadoutMovementProfile profile)
    {
        var supportSquared = MovementContextQuery.SquaredContextRadius(
            MovementRouteRules.ClearanceRadiusRaw(
                Scenario.BodyRadiusRaw,
                profile.PursuitSupportBodyDiametersBasisPoints));

        for (var otherIndex = 0; otherIndex < _agentStates.Length; otherIndex++)
        {
            if (otherIndex == selfIndex)
            {
                continue;
            }

            var ally = _agentStates[otherIndex];
            if (!ally.IsAlive || ally.FactionId != agent.FactionId)
            {
                continue;
            }

            if ((Int128)SquaredDistance(agent, ally) <= supportSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds one pace-constrained proposal toward a point destination
    /// selected above the V6 routes in the precedence chain — the last-stand
    /// rally aim, the contingent-cohesion aim, or the pursuit floor. Facing
    /// and phase pace constrain the one-tick step per design section 11.1;
    /// no lane clearance applies, because these destinations are the
    /// existing sources the chain preserves.
    /// </summary>
    private void ProposeEquipmentStepTowardPoint(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        int destinationXRaw,
        int destinationYRaw,
        ulong eventTargetId)
    {
        var deltaXRaw = (long)destinationXRaw - agent.XRaw;
        var deltaYRaw = (long)destinationYRaw - agent.YRaw;
        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return;
        }

        var facing = ResolveCandidateFacing(
            agent, profile, provisionalPhase, threat, deltaXRaw, deltaYRaw);
        var paceRaw = ResolveProposedPace(
            agent, profile, provisionalPhase, facing, deltaXRaw, deltaYRaw,
            _movementRules);
        var distanceRaw = IntegerSquareRoot(checked(
            (deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));
        var effectivePaceRaw = (int)Math.Min(paceRaw, distanceRaw);
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        if (MovementRouteRules.StepEndpoint(
            agent.XRaw,
            agent.YRaw,
            deltaXRaw,
            deltaYRaw,
            effectivePaceRaw,
            mapWidthRaw,
            mapHeightRaw,
            Scenario.BodyRadiusRaw) is not { } endpoint)
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return;
        }

        agent.Facing = facing;
        _movementProposals[index] = (endpoint.XRaw, endpoint.YRaw, eventTargetId);
        _proposedPaceRaw[index] = paceRaw;
    }

    /// <summary>
    /// Turns a warrior that proposes no movement this tick toward its
    /// selected living threat. With no threat there is no destination
    /// either, so facing is retained (design section 6.3).
    /// </summary>
    private static void TurnFacingInPlace(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat)
    {
        if (threat is null)
        {
            return;
        }

        var desired = FacingRules.FromDelta(
            (long)threat.XRaw - agent.XRaw,
            (long)threat.YRaw - agent.YRaw,
            agent.FactionId);
        if (desired == Facing16.None)
        {
            return;
        }

        agent.Facing = TurnTowardWithBudget(
            agent, profile, provisionalPhase, desired);
    }

    /// <summary>
    /// The committed facing for one candidate: toward the selected living
    /// threat when one exists, else toward the candidate's own travel
    /// direction, advanced by at most the phase-appropriate turn budget
    /// (design section 6.3).
    /// </summary>
    private static Facing16 ResolveCandidateFacing(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        long travelDeltaXRaw,
        long travelDeltaYRaw)
    {
        var desired = threat is not null
            ? FacingRules.FromDelta(
                (long)threat.XRaw - agent.XRaw,
                (long)threat.YRaw - agent.YRaw,
                agent.FactionId)
            : FacingRules.FromDelta(
                travelDeltaXRaw, travelDeltaYRaw, agent.FactionId);
        if (desired == Facing16.None)
        {
            return agent.Facing;
        }

        return TurnTowardWithBudget(agent, profile, provisionalPhase, desired);
    }

    /// <summary>
    /// One bounded turn: at most the profile's ordinary facing steps, or its
    /// committed facing steps while the phase is <see cref="FootworkPhase.Commit"/>.
    /// A warrior somehow still holding <see cref="Facing16.None"/> — never
    /// the case for a living V6 agent, whose facing is initialised at
    /// creation — snaps directly to the desired sector rather than feeding
    /// the sentinel into sector arithmetic.
    /// </summary>
    private static Facing16 TurnTowardWithBudget(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        Facing16 desired)
    {
        if (agent.Facing == Facing16.None)
        {
            return desired;
        }

        var budget = provisionalPhase == FootworkPhase.Commit
            ? profile.CommittedFacingStepsPerTick
            : profile.MaximumFacingStepsPerTick;
        return FacingRules.TurnToward(
            agent.Facing, desired, budget, agent.FactionId);
    }

    /// <summary>
    /// The direction-band pace pipeline of design sections 6.4 and 6.5: the
    /// separation between the committed facing and the travel direction
    /// selects the band cap, <see cref="FootworkPhase.Commit"/> clamps it
    /// further, the capped basis points convert to a desired raw pace bounded
    /// by <c>MovementSpeedRaw</c>, and the retained pace advances toward it
    /// by one bounded acceleration or deceleration step.
    /// </summary>
    private static int ResolveProposedPace(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        Facing16 committedFacing,
        long travelDeltaXRaw,
        long travelDeltaYRaw,
        MovementRuleset ruleset)
    {
        var travelSector = FacingRules.FromDelta(
            travelDeltaXRaw, travelDeltaYRaw, agent.FactionId);
        int paceCapBasisPoints;
        if (committedFacing == Facing16.None || travelSector == Facing16.None)
        {
            paceCapBasisPoints = profile.ForwardPaceBasisPoints;
        }
        else
        {
            var separation = FacingRules.SectorSeparation(
                committedFacing, travelSector);
            paceCapBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
                profile, separation);
        }

        if (provisionalPhase == FootworkPhase.Commit)
        {
            paceCapBasisPoints = Math.Min(
                paceCapBasisPoints, profile.CommittedPaceBasisPoints);
        }

        // Shield-projectile-block design section 6.2: the same clamp shape
        // as the Commit clamp just above, taking the minimum so the window
        // can only ever lower the cap, never raise it. Gated on the ruleset
        // flag rather than the counter alone so a stray nonzero counter under
        // a preset that does not author the effect — which should never
        // happen, since nothing sets the counter under such a preset — could
        // never silently clamp pace it was never meant to touch.
        if (ruleset.AppliesShieldBlockRecovery &&
            agent.ShieldBlockRecoveryTicksRemaining > 0)
        {
            paceCapBasisPoints = Math.Min(
                paceCapBasisPoints, ruleset.ShieldBlockRecoveryPaceCeilingBasisPoints);
        }

        var desiredPaceRaw = MovementRouteRules.DesiredPaceRaw(
            agent.MovementSpeedRaw, paceCapBasisPoints);
        return MovementRouteRules.AdvanceRetainedPaceRaw(
            agent.MovementPaceRaw,
            desiredPaceRaw,
            MovementRouteRules.PaceStepRaw(
                agent.MovementSpeedRaw,
                profile.AccelerationBasisPointsPerTick),
            MovementRouteRules.PaceStepRaw(
                agent.MovementSpeedRaw,
                profile.DecelerationBasisPointsPerTick));
    }

    /// <summary>
    /// The clearance radius of one profile, squared through
    /// <see cref="Int128"/> so no comparison can overflow.
    /// </summary>
    private Int128 SquaredClearanceRadius(LoadoutMovementProfile profile) =>
        MovementContextQuery.SquaredContextRadius(
            MovementRouteRules.ClearanceRadiusRaw(
                Scenario.BodyRadiusRaw,
                profile.AllyClearanceBodyDiametersBasisPoints));

    /// <summary>
    /// The agent's selected target, resolved to a living state, or
    /// <see langword="null"/> when no living threat is selected.
    /// </summary>
    private AgentState? ResolveLivingThreat(AgentState agent) =>
        agent.TargetEntityId is { } targetId &&
        TryGetAgentIndex(targetId, out var targetIndex) &&
        _agentStates[targetIndex].IsAlive
            ? _agentStates[targetIndex]
            : null;

    /// <summary>
    /// The friendly-clearance conflict pass of design section 10.6, run per
    /// faction between proposal gathering and collision resolution. Every
    /// living proposer enters in ascending <c>EntityId</c> order —
    /// guaranteed by agent storage order — carrying its committed phase; a
    /// rejected proposal becomes a no-move with zero retained pace and
    /// increments the derived denial counter. No reroute, no phase change.
    /// </summary>
    private void ResolveFriendlyClearanceConflicts()
    {
        for (var faction = 0; faction < FactionCount; faction++)
        {
            var count = 0;
            for (var index = 0; index < _agentStates.Length; index++)
            {
                var agent = _agentStates[index];
                if (!agent.IsAlive || agent.FactionId != faction)
                {
                    continue;
                }

                if (_movementProposals[index] is not { } proposal)
                {
                    continue;
                }

                _conflictProposals[count] = new FriendlyClearanceProposal(
                    agent.EntityId,
                    agent.FootworkPhase,
                    proposal.XRaw,
                    proposal.YRaw,
                    SquaredClearanceRadius(
                        _movementRules.ResolveLoadoutProfile(agent.Loadout)));
                _conflictProposalAgentIndexes[count] = index;
                count++;
            }

            MovementRouteRules.AcceptFriendlyClearanceConflicts(
                _conflictProposals.AsSpan(0, count),
                _conflictAccepted.AsSpan(0, count));

            for (var accepted = 0; accepted < count; accepted++)
            {
                if (_conflictAccepted[accepted])
                {
                    continue;
                }

                var agentIndex = _conflictProposalAgentIndexes[accepted];
                _movementProposals[agentIndex] = null;
                _proposedPaceRaw[agentIndex] = 0;
                _movementConflictDenials = checked(_movementConflictDenials + 1);
            }
        }
    }

    /// <summary>
    /// The post-attack footwork pass of design section 9.6, run between
    /// <see cref="GatherAndCommitAttacks"/> and <see cref="ResolveOutcome"/>:
    /// surviving accepted attackers enter <see cref="FootworkPhase.Commit"/>
    /// with the profile's commitment duration — interrupting
    /// <see cref="FootworkPhase.Recover"/>, because movement recovery never
    /// suppresses an attack the combat gates accepted — and every agent the
    /// gathered exchange killed takes death cleanup: pace, posture, phase,
    /// and timer clear atomically before any outcome, hash, or snapshot
    /// work, while the final facing is retained as readable spectator
    /// information. The writes are idempotent for agents that died on an
    /// earlier tick, whose four fields are already clear.
    /// </summary>
    /// <remarks>
    /// Under <see cref="MovementRuleset.AppliesPressureInterrupt"/> this pass
    /// also stamps the two one-tick histories the interrupt reads on the
    /// following tick and maintains the break-off flag it sets, and clears all
    /// three on a dead agent alongside the four above. The stamps land here,
    /// after the footwork stage has already read the previous tick's values,
    /// which is exactly what makes a single integer per signal sufficient (V7
    /// design section 4.5). Nothing here reads the interrupt's decision; it
    /// only records the state the next tick's decision will be made from.
    /// <para>
    /// The summary's invariant survives the interrupt: combat still reads
    /// nothing from movement, and this pass still never suppresses an attack
    /// the combat gates accepted. What V7 adds is the reverse direction — the
    /// movement stage writing <see cref="AgentState.AttackCooldownRemaining"/>,
    /// a field the combat stage owns — at the one gated site in
    /// <see cref="ResolveEquipmentPosturesAndProvisionalFootwork"/>.
    /// </para>
    /// </remarks>
    private void ApplyEquipmentAttackFootworkAndDeathCleanup()
    {
        var appliesPressureInterrupt = _movementRules.AppliesPressureInterrupt;
        var appliesShieldBlockRecovery = _movementRules.AppliesShieldBlockRecovery;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                agent.MovementPaceRaw = 0;
                agent.TacticalPosture = TacticalPosture.None;
                agent.FootworkPhase = FootworkPhase.None;
                agent.FootworkTicksRemaining = 0;
                if (appliesPressureInterrupt)
                {
                    // Idempotent for an agent that died on an earlier tick,
                    // exactly as the four writes above are.
                    agent.DamageTakenLastTick = 0;
                    agent.PriorSupportAllies = 0;
                    agent.BrokeOffUnderPressure = false;
                }

                if (appliesShieldBlockRecovery)
                {
                    // Shield-projectile-block design section 6.2. Idempotent
                    // for an agent that died on an earlier tick, exactly as
                    // the pressure-interrupt clear above is.
                    agent.ShieldBlockRecoveryTicksRemaining = 0;
                }

                continue;
            }

            if (_attackAcceptedThisTick[index])
            {
                var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
                agent.FootworkPhase = FootworkPhase.Commit;
                agent.FootworkTicksRemaining = profile.CommitmentTicks;
            }

            if (appliesPressureInterrupt)
            {
                // Signal B's history: this tick's accumulated damage, read on
                // the next tick as the damage taken on the previous one.
                // _damageTotals is cleared at the top of GatherAndCommitAttacks
                // and is still populated here, so this is a stamp rather than a
                // new query or a second pass.
                agent.DamageTakenLastTick = _damageTotals[index];

                // Signal C's history: this tick's support-ring ally count,
                // including the actor, taken from the context
                // SelectTargetsAndIntents derived at the top of the tick.
                agent.PriorSupportAllies =
                    _localMovementContexts[index].SupportAllies;

                // The break-off flag's lifetime, design section 8, channel 1.
                // It is not a single-tick pulse: it survives every tick the
                // warrior stays in the Disengage the interrupt produced, and
                // clears the moment this tick's finalised phase is anything
                // else — including the Commit an accepted attack writes just
                // above — and on death, in the branch above. That persistence
                // is what makes the pawn mark visible at 1x speed. This runs
                // after the attack-footwork write on purpose, so the flag is
                // always judged against the phase the tick actually ends in.
                agent.BrokeOffUnderPressure = agent.BrokeOffUnderPressure &&
                    agent.FootworkPhase == FootworkPhase.Disengage;
            }
        }
    }

    /// <summary>
    /// One route candidate of design section 10.4: a delta from the actor's
    /// tick-start position, whether it is a point to arrive at or a pure
    /// direction, whether the second-threat rule may omit it, and the entity
    /// the resulting Move event names.
    /// </summary>
    private readonly record struct EquipmentRouteCandidate(
        long DeltaXRaw,
        long DeltaYRaw,
        bool IsPointDestination,
        bool SubjectToSecondThreatOmission,
        ulong EventTargetId);
}
