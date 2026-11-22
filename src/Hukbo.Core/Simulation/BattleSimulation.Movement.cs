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
    /// Reads tick-start state only. Nothing is committed here, so no agent can
    /// see another agent's move while proposals are still being formed.
    /// </summary>
    /// <remarks>
    /// Under every persistent-contingent preset, a
    /// <see cref="AgentIntent.Moving"/> agent that passes all six movement
    /// gates of design section 3.5
    /// (the formation and movement realism design)
    /// takes a
    /// contingent cohesion destination instead of its ordinary nearest-enemy
    /// pursuit; every agent that fails even one of the six gates takes
    /// ordinary pursuit exactly as it does under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/>. The same-tick
    /// conflict order is unchanged: <c>Dead &gt; Attacking &gt; Regrouping
    /// &gt; contingent cohesion &gt; ordinary pursuit</c>, so this branch is
    /// only ever reached for an agent whose <see cref="AgentIntent"/> is
    /// already <see cref="AgentIntent.Moving"/>.
    /// </remarks>
    private void GatherMovementProposals()
    {
        if (_movementRules.UsesEquipmentRelativeFootwork)
        {
            // The equipment-relative route pipeline (design sections 10 and
            // 11) replaces this whole stage under the opt-in flag; every
            // legacy preset continues below untouched.
            GatherEquipmentRelativeMovementProposals();
            return;
        }

        Array.Clear(_movementProposals);

        // Every persistent-contingent preset takes the cohesion branch. The
        // test is written against IndependentPursuitV1, the one preset that
        // has no contingent behaviour, rather than against a list of the
        // presets that do: ResolveContingentStates returns on its first line
        // under exactly the same condition, and a new persistent-contingent
        // preset must not silently lose cohesion by not being named here.
        var cohesionActive =
            Scenario.MovementPreset != MovementPresetId.IndependentPursuitV1;
        var tick = checked((int)Tick);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.Intent == AgentIntent.Moving &&
                agent.TargetEntityId is { } enemyTargetId)
            {
                var target = _agentStates[AgentIndexOf(enemyTargetId)];

                // Ranged standoff. Confined to RangedStandoffV8 and
                // BattlefieldRealismV10 — battlefield realism design section
                // 5.2 widens the preset equality test from a single value to
                // this two-value predicate so V10 inherits the standoff hold
                // unchanged, and adds its own retreat rung beneath it — so
                // every other registered preset — PersistentContingentsV4
                // included — still takes the unmodified path below regardless
                // of which combat preset supplied the roster. A melee
                // weapon's StandoffDistanceRaw is always 0, so a melee-only
                // roster under V8 or V10 falls through this block untouched
                // and is byte-identical to V4. Checked ahead of contingent
                // cohesion below: cohesion's aim-point pursuit would
                // otherwise close a held ranged warrior straight onto its
                // target's body-contact ring, since that branch runs for every
                // preset other than IndependentPursuitV1 and never consults
                // StandoffDistanceRaw.
                if (Scenario.MovementPreset == MovementPresetId.RangedStandoffV8 ||
                    UsesBattlefieldRealism(Scenario.MovementPreset))
                {
                    var standoffRaw =
                        ResolveAttackerWeaponProfile(agent.Loadout).StandoffDistanceRaw;
                    if (standoffRaw != 0)
                    {
                        // Battlefield realism design section 5.2, rung 1: a
                        // melee enemy inside the threat radius backs the
                        // shooter directly away, ahead of the V8 hold rung
                        // below. _nearestMeleeThreatSquared is populated only
                        // under V10 (task 7), so RangedStandoffV8 never takes
                        // this branch — its scratch row is zero-length and
                        // this whole arm is gated on preset identity again,
                        // not merely on the array being readable.
                        if (UsesBattlefieldRealism(Scenario.MovementPreset))
                        {
                            var nearestMeleeThreatSquared =
                                _nearestMeleeThreatSquared[index];
                            var threatRadiusRaw =
                                RangedRetreatRules.ThreatRadiusRaw(standoffRaw);
                            if (RangedRetreatRules.IsThreatened(
                                nearestMeleeThreatSquared, threatRadiusRaw))
                            {
                                var threat = FindNearestMeleeThreatPosition(agent);
                                if (TryBuildRetreatProposal(
                                        agent,
                                        threat.XRaw,
                                        threat.YRaw,
                                        threat.EntityId,
                                        out var retreatProposal))
                                {
                                    // Design section 5.5, hazard two: the
                                    // stall generation is never consulted on
                                    // this rung. A blocked retreat simply
                                    // fails to move this tick instead of
                                    // starting a blocked streak that would
                                    // route it into the sidestepping-pursuit
                                    // branch below — straight back toward the
                                    // very thing it is running from.
                                    agent.Intent = AgentIntent.BackingAway;
                                    _movementProposals[index] = retreatProposal;
                                }
                                else
                                {
                                    // Design section 5.5, hazard one: the
                                    // reflected destination clamped to the
                                    // map bounds on at least one axis, so the
                                    // shooter cannot actually step back. No
                                    // proposal is written —
                                    // _movementProposals[index] is already
                                    // null from the Array.Clear above — and
                                    // the cornered warrior stands and fights
                                    // instead of reading a "backing away"
                                    // intent that would be a lie the
                                    // spectator can see.
                                    agent.Intent = AgentIntent.Holding;
                                }

                                continue;
                            }
                        }

                        if (SquaredDistance(agent, target) <=
                            checked((long)standoffRaw * standoffRaw))
                        {
                            // At or inside the weapon's standoff distance: the
                            // warrior deliberately holds rather than closing to
                            // body contact. No proposal is written —
                            // _movementProposals[index] is already null from the
                            // Array.Clear above — so the collision stage
                            // resolves it to MovementResolution.None rather than
                            // Blocked, and the blocked streak never starts.
                            agent.Intent = AgentIntent.Holding;
                            continue;
                        }

                        var rangedStallGeneration = _collision.StallGeneration(index);
                        _movementProposals[index] = rangedStallGeneration == 0
                            ? BuildMovementProposal(
                                agent,
                                target.XRaw,
                                target.YRaw,
                                target.EntityId,
                                standoffRaw)
                            : BuildSidesteppingPursuitProposal(
                                agent,
                                target,
                                rangedStallGeneration,
                                standoffRaw);
                        continue;
                    }
                }

                if (cohesionActive &&
                    TryResolveContingentCohesionAimPoint(
                        agent,
                        tick,
                        out var aimXRaw,
                        out var aimYRaw,
                        out var leaderEntityId))
                {
                    var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
                        agent.XRaw,
                        agent.YRaw,
                        aimXRaw,
                        aimYRaw);
                    if (squaredDistanceToAim <=
                        CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
                    {
                        // The arrived-guard: the same one BuildRegroupingProposal
                        // applies. _movementProposals[index] is already null from
                        // the Array.Clear above.
                        continue;
                    }

                    _movementProposals[index] = BuildMovementProposal(
                        agent,
                        aimXRaw,
                        aimYRaw,
                        leaderEntityId);
                    continue;
                }

                // The pursuit-path stall escape. At generation 0 — every agent
                // in every battle that is merely crowded — this is the same
                // call it has always been, reached by the same path, so the aim
                // point is unchanged rather than recomputed to the same value.
                var stallGeneration = _collision.StallGeneration(index);
                if (stallGeneration == 0)
                {
                    _movementProposals[index] = BuildMovementProposal(agent, target);
                    continue;
                }

                _movementProposals[index] = BuildSidesteppingPursuitProposal(
                    agent,
                    target,
                    stallGeneration);
                continue;
            }

            if (agent.Intent == AgentIntent.Regrouping)
            {
                _movementProposals[index] = BuildRegroupingProposal(agent, index);
            }
        }

        ApplyEvasiveFootwork();
    }

    /// <summary>
    /// The in-fight evasive footwork post-pass of the 2026-08-15 design,
    /// section 5. Runs at the tail of <see cref="GatherMovementProposals"/>,
    /// after every ordinary proposal exists and before anything is resolved or
    /// committed, and only under
    /// <see cref="MovementPresetId.EvasiveFootworkV14"/> and
    /// <see cref="MovementPresetId.SeparatingContactV17"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a post-pass rather than a rung inside the loop above because every
    /// branch of that loop <c>continue</c>s out of the body, so there is no
    /// reachable "last rung". Running over the already-built proposals also
    /// means the entire legacy path stays byte-identical for V1 through V13,
    /// proved by the single early return on this method's first line rather
    /// than by reading fourteen branches.
    /// </para>
    /// <para>
    /// Three guards bind every rung, and they are what make this movement
    /// during a fight rather than another retreat. A rung fires only for a
    /// living agent whose tick-start intent is
    /// <see cref="AgentIntent.Moving"/> or <see cref="AgentIntent.Attacking"/>
    /// and whose target names a living enemy, so the ranged retreat and the
    /// last-stand regroup are untouched by construction. No rung writes
    /// <see cref="AgentState.Intent"/> or clears
    /// <see cref="AgentState.TargetEntityId"/>. And a rung whose rebuilt
    /// proposal hits the map bounds yields entirely, leaving the ordinary
    /// proposal in place, which is the convention
    /// <see cref="TryBuildRetreatProposal"/> already follows.
    /// </para>
    /// <para>
    /// No rung draws from a random stream. Lateral direction comes from
    /// <see cref="EvasionRules.DutySign"/>, an integer parity, so the
    /// <c>SplitMix64</c> sequence after this stage is exactly what it was
    /// before this method existed.
    /// </para>
    /// <para>
    /// This pass also owns clearing a dead agent's action. The obvious
    /// precedent, <see cref="ApplyEquipmentAttackFootworkAndDeathCleanup"/>, is
    /// gated in its entirety on
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> and so never
    /// runs under this preset, while dead agents are still folded into the
    /// state hash. Walking every agent rather than only the living ones keeps a
    /// corpse from carrying a stale action forever.
    /// </para>
    /// </remarks>
    private void ApplyEvasiveFootwork()
    {
        if (Scenario.MovementPreset is not (MovementPresetId.EvasiveFootworkV14
            or MovementPresetId.SeparatingContactV17))
        {
            return;
        }

        var contactSquared =
            CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw);
        var stopShortRaw = checked(2 * Scenario.BodyRadiusRaw);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            _evasiveActionScratch[index] = EvasiveAction.None;
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.Intent is not (AgentIntent.Moving or AgentIntent.Attacking))
            {
                continue;
            }

            if (agent.TargetEntityId is not { } targetId ||
                !TryGetAgentIndex(targetId, out var targetIndex))
            {
                continue;
            }

            var target = _agentStates[targetIndex];
            if (!target.IsAlive || target.FactionId == agent.FactionId)
            {
                continue;
            }

            var deltaX = (long)target.XRaw - agent.XRaw;
            var deltaY = (long)target.YRaw - agent.YRaw;
            var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));

            // The ladder is priority-ordered and mutually exclusive: exactly one
            // action per living agent per tick, first match wins. Dodging an
            // arrow already in the air outranks everything, because the other
            // three rungs will still be available next tick and the arrow will
            // not.
            if (TryApplyDodgeIncoming(index, agent))
            {
                _evasiveActionScratch[index] = EvasiveAction.DodgeIncoming;
            }
            else if (TryApplyBreakOff(
                index, agent, target, deltaX, deltaY, distanceSquared, stopShortRaw))
            {
                _evasiveActionScratch[index] = EvasiveAction.BreakOff;
            }
            else if (TryApplyGiveGround(
                index, agent, target, deltaX, deltaY, distanceSquared))
            {
                _evasiveActionScratch[index] = EvasiveAction.GiveGround;
            }
            else if (TryApplySlipLateral(
                index,
                agent,
                target,
                deltaX,
                deltaY,
                distanceSquared,
                contactSquared,
                stopShortRaw))
            {
                _evasiveActionScratch[index] = EvasiveAction.SlipLateral;
            }
        }

        // Committed exactly once, after every decision is made, so the pass is
        // order-independent.
        for (var index = 0; index < _agentStates.Length; index++)
        {
            _agentStates[index].EvasiveAction = _evasiveActionScratch[index];
        }
    }

    /// <summary>
    /// Design section 5.2's arm. An exchange against this defender that did not
    /// land — a shield block, a parry, a deflection, or an evasion — owes the
    /// defender a break step on the following tick.
    /// </summary>
    /// <remarks>
    /// The write happens only when the defender's action is currently
    /// <see cref="EvasiveAction.None"/>, and that single condition does three
    /// things. It keeps the arm from masking an evasive movement the warrior
    /// actually executed this tick, which would corrupt the derived metrics. It
    /// makes the write idempotent and confluent when several attackers strike
    /// one defender in the same tick, since every one of them would write the
    /// same value and only the first is observed. And it caps break-off at once
    /// every other tick, because a warrior that executed a break step this tick
    /// is not holding <see cref="EvasiveAction.None"/> when this runs.
    /// </remarks>
    private void ArmBreakOffIfIntercepted(AgentState target, AttackResolution resolution)
    {
        if (Scenario.MovementPreset is not (MovementPresetId.EvasiveFootworkV14
            or MovementPresetId.SeparatingContactV17))
        {
            return;
        }

        if (resolution is not (AttackResolution.ShieldBlocked
            or AttackResolution.Parried
            or AttackResolution.Deflected
            or AttackResolution.Evaded))
        {
            return;
        }

        if (!target.IsAlive || target.EvasiveAction != EvasiveAction.None)
        {
            return;
        }

        // The arm carries the same engagement guard the rungs themselves
        // carry. Without it a warrior backing away or regrouping could be
        // armed, never become eligible to spend the step, and meanwhile read
        // as "Breaking off" in the inspector and fold that into the state hash
        // — a state the ladder can neither produce nor consume.
        if (target.Intent is not (AgentIntent.Moving or AgentIntent.Attacking) ||
            target.TargetEntityId is null)
        {
            return;
        }

        target.EvasiveAction = EvasiveAction.BreakOffArmed;
    }

    /// <summary>
    /// Design section 5.4, the dodge rung: a warrior with a missile already in
    /// the air toward it steps off the line of the shot. This is the
    /// best-attested evasive movement in the research corpus — two independent
    /// manuscripts record that the men at Mactan would never stand still but
    /// leaped about under missile fire.
    /// </summary>
    /// <remarks>
    /// The sign is taken from the projectile's launch tick rather than the
    /// dodger's own, so it is a property of the shot: two warriors under fire
    /// from the same volley do not all break the same way, and one warrior
    /// under fire from two shots does not oscillate.
    /// <para>
    /// The outcome of the shot was fixed when it was loosed, so a warrior can
    /// visibly leap aside and still be recorded as hit. That is deliberate, and
    /// it is what the source describes.
    /// </para>
    /// </remarks>
    private bool TryApplyDodgeIncoming(int index, AgentState agent)
    {
        var bestLaunchTick = long.MaxValue;
        var bestSourceId = ulong.MaxValue;
        var found = false;
        var originXRaw = 0;
        var originYRaw = 0;

        for (var slot = 0; slot < _projectileLiveCount; slot++)
        {
            var projectile = _projectiles[slot];
            if (projectile.TargetEntityId != agent.EntityId ||
                projectile.TicksRemaining > EvasionRules.DodgeImminenceTicks)
            {
                continue;
            }

            // Total order over a multi-result query: earliest shot first, then
            // the lowest source entity id.
            if (!found ||
                projectile.LaunchTick < bestLaunchTick ||
                (projectile.LaunchTick == bestLaunchTick &&
                    projectile.SourceEntityId < bestSourceId))
            {
                found = true;
                bestLaunchTick = projectile.LaunchTick;
                bestSourceId = projectile.SourceEntityId;
                originXRaw = projectile.OriginXRaw;
                originYRaw = projectile.OriginYRaw;
            }
        }

        if (!found)
        {
            return false;
        }

        var shotX = (long)agent.XRaw - originXRaw;
        var shotY = (long)agent.YRaw - originYRaw;
        var shotDistance = IntegerSquareRoot(
            checked((shotX * shotX) + (shotY * shotY)));
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            shotX,
            shotY,
            shotDistance,
            checked(EvasionRules.DodgeOffsetMultiplier * Scenario.BodyRadiusRaw),
            EvasionRules.DutySign(bestLaunchTick, periodTicks: 1));
        if (offsetXRaw == 0 && offsetYRaw == 0)
        {
            return false;
        }

        var proposal = BuildMovementProposal(
            agent,
            checked(agent.XRaw + offsetXRaw),
            checked(agent.YRaw + offsetYRaw),
            agent.TargetEntityId ?? agent.EntityId,
            stopShortRaw: 0,
            out var boundsClampChanged);
        if (boundsClampChanged)
        {
            return false;
        }

        _movementProposals[index] = proposal;
        return true;
    }

    /// <summary>
    /// Design section 5.2, the break-off rung: a warrior whose last exchange
    /// was turned aside circles its enemy at contact distance rather than
    /// standing in the same line. The aim point is offset perpendicular to the
    /// line to the target, so the warrior works around the opponent instead of
    /// opening the range.
    /// </summary>
    private bool TryApplyBreakOff(
        int index,
        AgentState agent,
        AgentState target,
        long deltaX,
        long deltaY,
        long distanceSquared,
        int stopShortRaw)
    {
        if (agent.EvasiveAction != EvasiveAction.BreakOffArmed)
        {
            return false;
        }

        var distance = IntegerSquareRoot(distanceSquared);
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaX,
            deltaY,
            distance,
            checked(EvasionRules.BreakOffOffsetMultiplier * Scenario.BodyRadiusRaw),
            EvasionRules.DutySign(Tick, periodTicks: 1));
        if (offsetXRaw == 0 && offsetYRaw == 0)
        {
            return false;
        }

        var proposal = BuildMovementProposal(
            agent,
            checked(target.XRaw + offsetXRaw),
            checked(target.YRaw + offsetYRaw),
            target.EntityId,
            stopShortRaw,
            out var boundsClampChanged);
        if (boundsClampChanged)
        {
            return false;
        }

        _movementProposals[index] = proposal;
        return true;
    }

    /// <summary>
    /// Design section 5.5, the give-ground rung: a warrior pinned in the press
    /// yields a short step directly away from its enemy without dropping it.
    /// </summary>
    /// <remarks>
    /// The pressure signal is the warrior's own tick-start
    /// <see cref="MovementResolution"/> being
    /// <see cref="MovementResolution.Blocked"/> — already computed, already
    /// authoritative, already folded into the state hash, and free, where a
    /// per-agent enemy count inside a pressure radius would be quadratic. It is
    /// a proxy: it fires as readily on ally congestion as on enemy pressure,
    /// which is why the step is short and rate-limited.
    /// <para>
    /// The bound is what keeps this from reading as a rout. A warrior stops two
    /// body radii from its target's centre while its reach is longer than that,
    /// so a step of one world unit leaves it inside its own attack range, and
    /// the ordinary pursuit proposal closes the gap again almost immediately.
    /// </para>
    /// </remarks>
    private bool TryApplyGiveGround(
        int index,
        AgentState agent,
        AgentState target,
        long deltaX,
        long deltaY,
        long distanceSquared)
    {
        // Deliberately the warrior's own reach and NOT body contact. A
        // committed position can never sit at or inside
        // CollisionGeometry.ContactSquaredDistance, because the collision
        // resolver's non-penetration invariant is strict while a
        // tangency-inclusive test is satisfied only at exact equality; the
        // closest any warrior came across a twenty-seed matrix was one squared
        // raw unit above it, never on it. Gating this rung on body contact
        // made it dead code, measured at zero agent-ticks over a full
        // ten-thousand-tick battle. Reach is the non-degenerate form of "close
        // enough to be fighting", and it is the same correction design section
        // 8 bar 4 already had to make.
        var reachRaw = (long)agent.AttackRangeRaw;
        if (distanceSquared > checked(reachRaw * reachRaw) ||
            agent.MovementResolution != MovementResolution.Blocked)
        {
            return false;
        }

        if (!EvasionRules.FiresThisTick(
                Tick, agent.EntityId, EvasionRules.GiveGroundPeriodTicks))
        {
            return false;
        }

        var distance = IntegerSquareRoot(distanceSquared);
        if (distance <= 0)
        {
            return false;
        }

        // Directly away from the target: the reverse of the approach vector,
        // scaled to the give-ground step.
        var stepXRaw = checked(-deltaX * EvasionRules.GiveGroundStepRaw) / distance;
        var stepYRaw = checked(-deltaY * EvasionRules.GiveGroundStepRaw) / distance;
        if (stepXRaw == 0 && stepYRaw == 0)
        {
            return false;
        }

        var proposal = BuildMovementProposal(
            agent,
            checked(agent.XRaw + (int)stepXRaw),
            checked(agent.YRaw + (int)stepYRaw),
            target.EntityId,
            stopShortRaw: 0,
            out var boundsClampChanged);
        if (boundsClampChanged)
        {
            return false;
        }

        _movementProposals[index] = proposal;
        return true;
    }

    /// <summary>
    /// Design section 5.3, the slip rung: a warrior still closing on its target
    /// weaves across the line of approach instead of walking straight in. Fires
    /// outside body contact, inside twice the warrior's own reach, and only on
    /// the warrior's own duty tick, so at most one tick in
    /// <see cref="EvasionRules.SlipPeriodTicks"/> carries a slip and
    /// neighbouring warriors do not step in unison.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the proposal was replaced with a slipped
    /// one, <see langword="false"/> when the rung did not apply or yielded.
    /// </returns>
    private bool TryApplySlipLateral(
        int index,
        AgentState agent,
        AgentState target,
        long deltaX,
        long deltaY,
        long distanceSquared,
        long contactSquared,
        int stopShortRaw)
    {
        if (agent.Intent != AgentIntent.Moving || distanceSquared <= contactSquared)
        {
            return false;
        }

        var slipRadiusRaw = checked(
            (long)agent.AttackRangeRaw * EvasionRules.SlipRadiusBasisPoints) /
            EvasionRules.BasisPointDenominator;
        if (distanceSquared > checked(slipRadiusRaw * slipRadiusRaw))
        {
            return false;
        }

        if (!EvasionRules.FiresThisTick(
                Tick, agent.EntityId, EvasionRules.SlipPeriodTicks))
        {
            return false;
        }

        var distance = IntegerSquareRoot(distanceSquared);
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaX,
            deltaY,
            distance,
            checked(EvasionRules.SlipOffsetMultiplier * Scenario.BodyRadiusRaw),
            EvasionRules.DutySign(Tick, EvasionRules.SlipPeriodTicks));
        if (offsetXRaw == 0 && offsetYRaw == 0)
        {
            return false;
        }

        var proposal = BuildMovementProposal(
            agent,
            checked(target.XRaw + offsetXRaw),
            checked(target.YRaw + offsetYRaw),
            target.EntityId,
            stopShortRaw,
            out var boundsClampChanged);
        if (boundsClampChanged)
        {
            // Hazard one of design section 5.0: a warrior against the map edge
            // keeps the ordinary proposal rather than proposing a step the
            // clamp would silently swallow.
            return false;
        }

        _movementProposals[index] = proposal;
        return true;
    }

    /// <summary>
    /// Builds a movement proposal that closes an agent on an enemy it is
    /// fighting. Delegates to the point-taking overload, stopping short by
    /// one body diameter so bodies come to rest touching rather than
    /// overlapping — see the point-taking overload's remarks for why a rally
    /// point is handled differently.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        AgentState target) =>
        BuildMovementProposal(
            agent,
            target.XRaw,
            target.YRaw,
            target.EntityId,
            // Agents close to body contact, not merely to weapon reach.
            // Stopping at reach left four world units of permanent air
            // between opposing front ranks, so bodies never touched and the
            // collision stage only ever saw allies queueing. Attacks still
            // resolve at reach, which is wider than the diameter, so a rank
            // pressed into contact fights and the rank behind it can reach
            // past.
            stopShortRaw: checked(2 * Scenario.BodyRadiusRaw));

    /// <summary>
    /// Builds a movement proposal toward an arbitrary destination point
    /// rather than another agent's live position. Used for last-stand rally
    /// movement, where a follower wants to actually reach its aim point
    /// rather than stop short of it the way it would closing on an enemy
    /// body — so this overload walks all the way in (<c>stopShortRaw: 0</c>)
    /// instead of duplicating the enemy-closing overload's stopping-distance
    /// arithmetic.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId) =>
        BuildMovementProposal(
            agent,
            destinationXRaw,
            destinationYRaw,
            targetId,
            stopShortRaw: 0);

    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId,
        int stopShortRaw) =>
        BuildMovementProposal(
            agent, destinationXRaw, destinationYRaw, targetId, stopShortRaw, out _);

    /// <summary>
    /// The full implementation every other overload delegates to. Identical
    /// arithmetic and the identical <see cref="CollisionGeometry.ClampCenterToBounds"/>
    /// call as before battlefield realism design section 5.5 — the only
    /// addition is capturing the pre-clamp centre so
    /// <paramref name="boundsClampChangedResult"/> can report whether the
    /// bounds clamp changed either coordinate, which the retreat builder
    /// below uses for its hazard-one check. Every pre-existing caller reaches
    /// this through the five-argument overload above, which discards the new
    /// output and is otherwise byte-identical to what it called before.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId,
        int stopShortRaw,
        out bool boundsClampChangedResult)
    {
        var deltaX = (long)destinationXRaw - agent.XRaw;
        var deltaY = (long)destinationYRaw - agent.YRaw;
        var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
        var distance = IntegerSquareRoot(distanceSquared);

        var desiredMovement = Math.Max(1, distance - stopShortRaw);
        // The arrival taper is on under every persistent-contingent preset,
        // and off only under IndependentPursuitV1, whose trajectory is frozen.
        // Testing against the frozen preset rather than naming each preset
        // that has the taper keeps a newly registered preset from silently
        // losing it.
        var movement = Scenario.MovementPreset != MovementPresetId.IndependentPursuitV1
            ? MovementRules.ComputeArrivalStepRaw(
                desiredMovement,
                agent.MovementSpeedRaw,
                checked((long)_movementRules.ArrivalTaperMultiplier * Scenario.BodyRadiusRaw))
            : Math.Min(agent.MovementSpeedRaw, desiredMovement);
        var moveX = checked(deltaX * movement / Math.Max(1, distance));
        var moveY = checked(deltaY * movement / Math.Max(1, distance));

        if (moveX == 0 && moveY == 0)
        {
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                moveX = Math.Sign(deltaX);
            }
            else
            {
                moveY = Math.Sign(deltaY);
            }
        }

        var unclampedNextX = checked(agent.XRaw + (int)moveX);
        var unclampedNextY = checked(agent.YRaw + (int)moveY);
        var nextX = CollisionGeometry.ClampCenterToBounds(
            unclampedNextX,
            checked(Scenario.MapWidth * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        var nextY = CollisionGeometry.ClampCenterToBounds(
            unclampedNextY,
            checked(Scenario.MapHeight * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        boundsClampChangedResult = nextX != unclampedNextX || nextY != unclampedNextY;
        return (nextX, nextY, targetId);
    }

    /// <summary>
    /// Battlefield realism design section 5.4's retreat builder: reflects the
    /// melee threat through the actor — <c>(2 * agentX - threatX, 2 * agentY
    /// - threatY)</c>, the point directly opposite the threat at the same
    /// distance — and reuses the ordinary paced, tapered, clamped
    /// movement-proposal arithmetic toward that reflected point. No new
    /// movement arithmetic and no new clamping path: this only supplies a
    /// different destination to the same <see cref="BuildMovementProposal(AgentState,int,int,ulong,int,out bool)"/>
    /// used everywhere else.
    /// </summary>
    /// <returns>
    /// <see langword="false"/>, writing no <paramref name="proposal"/>, when
    /// the bounds clamp changed either coordinate (design 5.5, hazard one) —
    /// the reflected destination would clamp to where the agent already
    /// stands, so the caller assigns <see cref="AgentIntent.Holding"/>
    /// instead of proposing a step that would silently do nothing.
    /// </returns>
    private bool TryBuildRetreatProposal(
        AgentState agent,
        int threatXRaw,
        int threatYRaw,
        ulong targetId,
        out (int XRaw, int YRaw, ulong TargetId) proposal)
    {
        var destinationXRaw = checked((2 * agent.XRaw) - threatXRaw);
        var destinationYRaw = checked((2 * agent.YRaw) - threatYRaw);

        proposal = BuildMovementProposal(
            agent,
            destinationXRaw,
            destinationYRaw,
            targetId,
            stopShortRaw: 0,
            out var boundsClampChanged);

        if (boundsClampChanged)
        {
            proposal = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Battlefield realism design section 5.3's threat position: the live
    /// position and id of the nearest living, opposing-faction, melee-weapon
    /// agent — the same candidate <c>SelectTargetsAndIntents</c> already
    /// reduced into <see cref="_nearestMeleeThreatSquared"/> by squared
    /// distance alone. That scratch keeps only the distance, not the
    /// position, so the retreat builder re-derives the position here. No
    /// perception-range re-test is needed: perception filtering only ever
    /// excludes a candidate whose distance exceeds
    /// <see cref="AgentState.PerceptionRangeRaw"/>, so any candidate at or
    /// inside the finite scratch distance that made this call reachable was
    /// already perceived, and a plain global minimum over "alive, opposing
    /// faction, melee weapon" reproduces exactly the entity that distance
    /// came from. Ties break on the lower <see cref="AgentState.EntityId"/>,
    /// matching every other multi-result query in this file. Throws if none
    /// is found — reachable only after
    /// <see cref="RangedRetreatRules.IsThreatened"/> has already read a
    /// finite scratch value for this agent, so a miss here is a defect in
    /// that invariant, never a real battlefield state.
    /// </summary>
    private (int XRaw, int YRaw, ulong EntityId) FindNearestMeleeThreatPosition(
        AgentState agent)
    {
        AgentState? nearest = null;
        var nearestSquared = long.MaxValue;
        foreach (var candidate in _agentStates)
        {
            if (!candidate.IsAlive || candidate.FactionId == agent.FactionId)
            {
                continue;
            }

            if (ResolveAttackerWeaponProfile(candidate.Loadout).StandoffDistanceRaw != 0)
            {
                continue;
            }

            var distance = SquaredDistance(agent, candidate);
            if (distance < nearestSquared ||
                (distance == nearestSquared &&
                    (nearest is null || candidate.EntityId < nearest.EntityId)))
            {
                nearest = candidate;
                nearestSquared = distance;
            }
        }

        if (nearest is null)
        {
            throw new InvalidOperationException(
                "No nearest melee threat was found for an agent already " +
                "reported as threatened.");
        }

        return (nearest.XRaw, nearest.YRaw, nearest.EntityId);
    }
}
