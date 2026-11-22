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
    /// Resolves the <see cref="WeaponProfile"/> that governs one warrior's
    /// attack-combination attributes at the moment of an attack attempt.
    /// Mirrors the fallback <see cref="CreateAgent"/> uses for damage, reach,
    /// and cooldown: a preset that declares no weapon profiles — version 1 —
    /// falls back to a synthetic profile built from the scenario's global
    /// values, whose combo fields default to the record's own no-op
    /// defaults (see <see cref="WeaponProfile"/>), so version 1 rolls the
    /// same combo checks every other preset does and they simply never
    /// succeed.
    /// </summary>
    /// <remarks>
    /// Resolved fresh on every attack attempt rather than cached on
    /// <see cref="AgentState"/> at spawn, unlike damage/reach/cooldown:
    /// those three are read every tick regardless of whether an attack is
    /// attempted, but the combo fields are read only on the attempts that
    /// pass the pre-check gate, which per-tick is a small fraction of the
    /// full roster.
    /// </remarks>
    private WeaponProfile ResolveAttackerWeaponProfile(CombatLoadout loadout) =>
        _rules.HasWeaponProfiles
            ? _rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                Scenario.DamagePerAttack,
                Scenario.AttackRangeRaw,
                Scenario.AttackCooldownTicks);

    /// <summary>
    /// Whether <paramref name="rules"/> fields at least one ranged weapon —
    /// "this combat ruleset fields at least one ranged weapon" — computed
    /// once at construction and cached on <see cref="_hasRangedWeapon"/>
    /// rather than re-derived per tick. A weapon is ranged exactly when
    /// <see cref="WeaponProfile.StandoffDistanceRaw"/> is non-zero, the same
    /// test <see cref="WeaponProfile.ValidateRangedFields"/> uses; every
    /// registered preset up to and including
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV4"/> declares no
    /// weapon profiles at all, so this returns <see langword="false"/> for
    /// every one of them without resolving a single profile.
    /// </summary>
    private static bool DetermineHasRangedWeapon(CombatRuleset rules)
    {
        if (!rules.HasWeaponProfiles)
        {
            return false;
        }

        foreach (var loadout in rules.Roster)
        {
            var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
            if (profile.StandoffDistanceRaw != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears an attacker's active attack combination. Called by the pre-
    /// check gate in <see cref="GatherAndCommitAttacks"/> per plan section
    /// 3(a): the moment this attacker discovers — on any tick, whether or
    /// not it is itself off cooldown — that it has no living target or that
    /// its target is out of reach, its chain is over. A no-op when no chain
    /// is active, so callers do not need to test
    /// <see cref="AgentState.ComboStepsRemaining"/> first.
    /// </summary>
    private static void ClearActiveComboChain(AgentState attacker)
    {
        if (attacker.ComboStepsRemaining <= 0)
        {
            return;
        }

        attacker.ComboStepsRemaining = 0;
        attacker.ComboTargetEntityId = null;
    }

    /// <summary>
    /// Opens or extends <paramref name="defender"/>'s shield block-recovery
    /// window (shield-projectile-block design section 6.2), called from both
    /// the melee resolution path and the ranged-arrival path in
    /// <see cref="GatherAndCommitAttacks"/> so a defender is covered
    /// regardless of which kind of attack the shield stopped. Takes the
    /// larger of the counter's current value — whether left over from an
    /// earlier tick or already raised earlier this same tick by another
    /// blocked attack against the same defender — and the duration this
    /// defender's shield authors, so two blocks landing on one defender in
    /// one tick take the maximum rather than summing. A no-op under any
    /// preset whose <see cref="MovementRuleset.AppliesShieldBlockRecovery"/>
    /// is <see langword="false"/>, matching every other gate this design
    /// introduces.
    /// </summary>
    private void ApplyShieldBlockRecovery(AgentState defender)
    {
        if (!_movementRules.AppliesShieldBlockRecovery)
        {
            return;
        }

        var duration = _movementRules.ResolveShieldBlockRecoveryTicks(
            defender.Loadout.Shield);
        if (duration > defender.ShieldBlockRecoveryTicksRemaining)
        {
            defender.ShieldBlockRecoveryTicksRemaining = duration;
        }
    }

    private void GatherAndCommitAttacks(List<BattleEvent> events)
    {
        Array.Clear(_damageTotals);
        Array.Clear(_armorSavedTargets);
        Array.Clear(_armorSavedPoints);
        var proposalCount = 0;
        var marksAcceptedAttackers = _movementRules.UsesEquipmentRelativeFootwork;
        if (marksAcceptedAttackers)
        {
            // Reusable scratch (design section 9.6): one bit per agent, set
            // on the accept path below after the existing prechecks pass,
            // read by ApplyEquipmentAttackFootworkAndDeathCleanup after this
            // method returns, and never hashed or snapshotted.
            Array.Clear(_attackAcceptedThisTick);
        }

        // Derived observability counters for this tick. Locals rather than
        // state, folded into the reported value once at the end of the loop,
        // so nothing here can be read by the simulation itself.
        //
        // Each counter is indexed by the attacking agent's faction so the same
        // loop yields both the undivided total and the per-faction split, with
        // no second pass and no new query. Two factions exist, so these are
        // fixed-size stack arrays; the totals are summed at the end rather than
        // tracked separately, which makes the split a partition by
        // construction rather than by assertion.
        Span<int> accepted = stackalloc int[FactionCount];
        Span<int> landed = stackalloc int[FactionCount];
        Span<int> shieldBlocked = stackalloc int[FactionCount];
        Span<int> parried = stackalloc int[FactionCount];
        Span<int> deflected = stackalloc int[FactionCount];
        Span<int> evaded = stackalloc int[FactionCount];

        // Pass A0 (ranged-units plan RU-17): advance every pooled
        // projectile's flight countdown and resolve any arrival, before the
        // melee gather loop below even starts. A projectile's clash and
        // hit-location roll folds its LAUNCH tick, not this arrival tick, so
        // a shot's outcome was already fixed the instant it left its
        // weapon; only whether the result is visible yet was ever in
        // question. Order-preserving compaction: a projectile that survives
        // is copied down to close the gap left by one that resolved this
        // tick, exactly as CollisionScratch's own compaction preserves
        // order elsewhere. _projectileLiveCount is zero, and this loop a
        // single no-op comparison, under any ruleset that fields no ranged
        // weapon.
        var projectileWriteIndex = 0;
        for (var readIndex = 0; readIndex < _projectileLiveCount; readIndex++)
        {
            var projectile = _projectiles[readIndex];
            var ticksRemaining = projectile.TicksRemaining - 1;
            if (ticksRemaining > 0)
            {
                _projectiles[projectileWriteIndex] =
                    projectile with { TicksRemaining = ticksRemaining };
                projectileWriteIndex++;
                continue;
            }

            // Arrived. The launching warrior's FactionId, Loadout, and
            // EntityId never change once created, even after death, so
            // reading them here from a source that may itself have died
            // since launch is exactly as safe as the shared code below
            // already treats a melee source.
            var arrivalSourceIndex = AgentIndexOf(projectile.SourceEntityId);
            var arrivalSource = _agentStates[arrivalSourceIndex];
            var arrivalTargetIndex = AgentIndexOf(projectile.TargetEntityId);
            var arrivalTarget = _agentStates[arrivalTargetIndex];

            if (!arrivalTarget.IsAlive)
            {
                // Phase 1's only miss: the recorded target died between
                // launch and arrival. This shot produces no accepted attack,
                // so it never enters _attackProposals.
                AddEvent(
                    events,
                    BattleEventKind.Miss,
                    projectile.SourceEntityId,
                    projectile.TargetEntityId,
                    0,
                    arrivalSource.FactionId);
                continue;
            }

            var arrivalHitLocation = HitLocationResolver.Resolve(
                _rules,
                arrivalSource.Loadout,
                arrivalTarget.Loadout,
                Scenario.Seed,
                projectile.LaunchTick,
                projectile.SourceEntityId,
                projectile.TargetEntityId);
            var arrivalResolution = ClashResolver.Resolve(
                _rules.ClashProfile,
                Scenario.Seed,
                projectile.LaunchTick,
                projectile.SourceEntityId,
                projectile.TargetEntityId,
                arrivalSource.Loadout.Weapon,
                arrivalTarget.Loadout.Weapon,
                arrivalTarget.Loadout.Shield);

            // An impact needs no new BattleEventKind: it is buffered here
            // exactly as a melee blow is, and pass B below emits it as an
            // ordinary Attack event carrying weapon, shield, hit location,
            // and resolution. Ranged weapons never open a combo chain
            // (PhilippineCombatPresetV5.RangedProfile fixes their
            // combo-open chance at zero), so the buffered combo position is
            // unconditionally null rather than routed through
            // ResolveComboTransition, which would also incorrectly rewrite
            // the cooldown this shot already charged at launch.
            _attackProposals[proposalCount] = (
                arrivalSourceIndex,
                arrivalTargetIndex,
                arrivalHitLocation,
                arrivalResolution,
                null);
            proposalCount++;

            // The damage recorded at launch, not the launcher's current
            // DamagePerAttack, so a later loadout or preset change could
            // never retroactively alter a shot already in flight. The two
            // are bit-identical for every agent CreateAgent ever produces —
            // AgentState.DamagePerAttack is a get-only property, written
            // once at spawn — so this is not a behavioural difference today,
            // only which of two equal sources the code commits to.
            if (arrivalResolution == AttackResolution.Landed)
            {
                var arrivalMitigatedDamage = _rules.ApplyArmorMitigation(
                    projectile.DamageAtLaunch,
                    arrivalTarget.Loadout.Armor,
                    arrivalHitLocation);
                _damageTotals[arrivalTargetIndex] = checked(
                    _damageTotals[arrivalTargetIndex] + arrivalMitigatedDamage);

                // Armor identity: this blow's raw damage alone would have
                // been lethal against arrivalTarget.HitPoints as it stands
                // before the end-of-pass damage-application loop applies
                // it — the same pre-tick baseline the combo-lethality check
                // below uses — but the mitigated damage is not. Recorded
                // only the first time this is true for this target in this
                // tick.
                if (!_armorSavedTargets[arrivalTargetIndex] &&
                    projectile.DamageAtLaunch > 0 &&
                    arrivalMitigatedDamage > 0 &&
                    arrivalTarget.HitPoints - projectile.DamageAtLaunch <= 0 &&
                    arrivalTarget.HitPoints - arrivalMitigatedDamage > 0)
                {
                    _armorSavedTargets[arrivalTargetIndex] = true;
                    _armorSavedPoints[arrivalTargetIndex] =
                        projectile.DamageAtLaunch - arrivalMitigatedDamage;
                }
            }

            // Credited to the launcher's faction, in the same shape the
            // melee loop below credits its own resolutions, so the impact
            // tick — not the launch tick — is what counts toward this
            // tick's CombatMetrics.
            var arrivalFaction = arrivalSource.FactionId;
            accepted[arrivalFaction]++;
            switch (arrivalResolution)
            {
                case AttackResolution.Landed:
                    landed[arrivalFaction]++;
                    break;
                case AttackResolution.ShieldBlocked:
                    shieldBlocked[arrivalFaction]++;
                    ApplyShieldBlockRecovery(arrivalTarget);
                    break;
                case AttackResolution.Parried:
                    parried[arrivalFaction]++;
                    break;
                case AttackResolution.Deflected:
                    deflected[arrivalFaction]++;
                    break;
                default:
                    evaded[arrivalFaction]++;
                    break;
            }
        }

        _projectileLiveCount = projectileWriteIndex;

        for (var sourceIndex = 0;
             sourceIndex < _agentStates.Length;
             sourceIndex++)
        {
            var source = _agentStates[sourceIndex];
            if (!source.IsAlive)
            {
                continue;
            }

            // Plan section 3(a): a chaining attacker's pre-check runs ahead
            // of the cooldown check below, not after it, so that an attacker
            // still mid-cooldown discovers a dead or out-of-reach target — and
            // clears its chain — on the very tick that becomes true, rather
            // than only once its own cooldown next lets it act.
            if (source.TargetEntityId is not { } targetId)
            {
                ClearActiveComboChain(source);
                continue;
            }

            var target = _agentStates[AgentIndexOf(targetId)];
            if (!target.IsAlive)
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (!IsWithinAttackRange(source, target))
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (source.AttackCooldownRemaining != 0)
            {
                continue;
            }

            // Ranged-units plan RU-17: a ranged weapon launches a
            // projectile here instead of resolving immediately. The
            // precheck ladder above -- alive, has a target, target alive,
            // within reach, cooldown ready -- is unchanged and load-bearing
            // for both weapon families; only what happens once every
            // precheck has passed differs. Phase 1: a projectile passes
            // through allies and through every enemy but its target -- there
            // is no line of sight and no interception.
            var weaponProfile = ResolveAttackerWeaponProfile(source.Loadout);
            if (weaponProfile.StandoffDistanceRaw != 0)
            {
                if (_projectileLiveCount >= Scenario.MaximumProjectilesInFlight)
                {
                    // Launch at the ceiling is refused outright: the shot
                    // does not occur, the cooldown is not charged, and the
                    // refusal is counted so it is visible rather than
                    // silently dropped.
                    _projectileLaunchRefusals =
                        checked(_projectileLaunchRefusals + 1);
                    continue;
                }

                source.Intent = AgentIntent.Attacking;
                _projectiles[_projectileLiveCount] = new Projectile(
                    source.EntityId,
                    target.EntityId,
                    Tick,
                    weaponProfile.FlightTickCeiling,
                    source.XRaw,
                    source.YRaw,
                    source.Loadout.Weapon,
                    weaponProfile.DamagePerAttack);
                _projectileLiveCount++;

                // The cooldown charges on launch, not on arrival, mirroring
                // the existing non-landed-attack cooldown-reset behaviour.
                // Every ranged weapon this preset family declares fixes its
                // combo-open chance at zero
                // (PhilippineCombatPresetV5.RangedProfile), so it can never
                // open a chain and this writes the normal cooldown directly
                // rather than routing through ResolveComboTransition.
                source.AttackCooldownRemaining = source.AttackCooldownTicks;

                AddEvent(
                    events,
                    BattleEventKind.Release,
                    source.EntityId,
                    target.EntityId,
                    weaponProfile.FlightTickCeiling,
                    source.FactionId);

                if (marksAcceptedAttackers)
                {
                    _attackAcceptedThisTick[sourceIndex] = true;
                }

                continue;
            }

            // 3(b): resolve the attack. Unchanged from before this feature —
            // the cooldown write that used to happen here moves below, after
            // the combo transition, because which cooldown to write now
            // depends on the combo outcome.
            source.Intent = AgentIntent.Attacking;
            var targetIndex = AgentIndexOf(target.EntityId);
            var hitLocation = HitLocationResolver.Resolve(
                _rules,
                source.Loadout,
                target.Loadout,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId);

            // Resolved inline, immediately after the hit location, in the same
            // pass. A second pass over the proposals would be a second place
            // the attack tuple has to stay consistent, and a per-target buffer
            // would be state whose staleness nothing checks. The clash costs no
            // draw from any generator, so nothing downstream shifts merely
            // because this call was added.
            var resolution = ClashResolver.Resolve(
                _rules.ClashProfile,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                target.Loadout.Weapon,
                target.Loadout.Shield);

            var comboPosition = ResolveComboTransition(source, target, resolution, hitLocation);

            ArmBreakOffIfIntercepted(target, resolution);

            _attackProposals[proposalCount] =
                (sourceIndex, targetIndex, hitLocation, resolution, comboPosition);
            proposalCount++;
            if (marksAcceptedAttackers)
            {
                // Design section 9.6: the accept path is the only place an
                // attack is known to have passed every precheck, so the
                // scratch bit is set here, alongside the proposal buffering,
                // and the accumulation, damage, cooldown, combo, and event
                // work below continues unchanged.
                _attackAcceptedThisTick[sourceIndex] = true;
            }

            // Only a landed blow reaches the damage total. Every other
            // resolution still emitted its attack event above and still burned
            // the attacker's cooldown; it simply carries no damage.
            if (resolution == AttackResolution.Landed)
            {
                var mitigatedDamage = _rules.ApplyArmorMitigation(
                    source.DamagePerAttack,
                    target.Loadout.Armor,
                    hitLocation);
                _damageTotals[targetIndex] = checked(
                    _damageTotals[targetIndex] + mitigatedDamage);

                // Armor identity: this blow's raw damage alone would have
                // been lethal against target.HitPoints as it stands before
                // the end-of-pass damage-application loop applies it — the
                // same pre-tick baseline the combo-lethality check below
                // uses — but the mitigated damage is not. Recorded only the
                // first time this is true for this target in this tick.
                if (!_armorSavedTargets[targetIndex] &&
                    source.DamagePerAttack > 0 &&
                    mitigatedDamage > 0 &&
                    target.HitPoints - source.DamagePerAttack <= 0 &&
                    target.HitPoints - mitigatedDamage > 0)
                {
                    _armorSavedTargets[targetIndex] = true;
                    _armorSavedPoints[targetIndex] =
                        source.DamagePerAttack - mitigatedDamage;
                }
            }

            // Credited to the attacker's faction. Every accepted attack has
            // exactly one attacker, so this is a partition of proposalCount.
            var attackerFaction = source.FactionId;
            accepted[attackerFaction]++;

            switch (resolution)
            {
                case AttackResolution.Landed:
                    landed[attackerFaction]++;
                    break;
                case AttackResolution.ShieldBlocked:
                    shieldBlocked[attackerFaction]++;
                    ApplyShieldBlockRecovery(target);
                    break;
                case AttackResolution.Parried:
                    parried[attackerFaction]++;
                    break;
                case AttackResolution.Deflected:
                    deflected[attackerFaction]++;
                    break;
                default:
                    evaded[attackerFaction]++;
                    break;
            }
        }

        _lastTickCombatByFaction = new FactionCombatMetrics(
            new CombatMetrics(
                accepted[0],
                landed[0],
                shieldBlocked[0],
                parried[0],
                deflected[0],
                evaded[0]),
            new CombatMetrics(
                accepted[1],
                landed[1],
                shieldBlocked[1],
                parried[1],
                deflected[1],
                evaded[1]));

        // Derived from the split rather than counted separately, so the two can
        // never disagree. proposalCount is asserted equal to the summed
        // accepted count below: it is maintained by the loop for indexing
        // _attackProposals, so it is an independent witness that the per-faction
        // credit above missed nothing.
        _lastTickCombat = _lastTickCombatByFaction.Total;

        if (_lastTickCombat.AcceptedAttacks != proposalCount)
        {
            throw new InvalidOperationException(
                "Per-faction attack accounting lost or duplicated an accepted " +
                "attack: the faction split sums to " +
                $"{_lastTickCombat.AcceptedAttacks} but {proposalCount} " +
                "attacks were resolved this tick.");
        }

        for (var index = 0; index < proposalCount; index++)
        {
            var proposal = _attackProposals[index];
            var source = _agentStates[proposal.SourceIndex];
            var target = _agentStates[proposal.TargetIndex];
            AddAttackEvent(
                events,
                source.EntityId,
                target.EntityId,
                proposal.Resolution == AttackResolution.Landed
                    ? source.DamagePerAttack
                    : 0,
                source.FactionId,
                source.Loadout.Weapon,
                source.Loadout.Shield,
                proposal.HitLocation,
                proposal.Resolution,
                proposal.ComboPosition);
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var damage = _damageTotals[index];
            if (damage == 0)
            {
                continue;
            }

            var target = _agentStates[index];
            target.HitPoints = Math.Max(0, target.HitPoints - damage);
            AddEvent(
                events,
                BattleEventKind.Damage,
                target.EntityId,
                target.EntityId,
                damage,
                target.FactionId);

            // Armor identity: emitted in the same position this target's
            // Damage event occupies for this tick, immediately after it,
            // so the pair stays adjacent in the ordered event stream. The
            // flag was set above, in the same fixed iteration order the
            // two damage-accumulation loops already use, so this is a pure
            // read of state already computed — no new random draw.
            if (_armorSavedTargets[index])
            {
                AddEvent(
                    events,
                    BattleEventKind.ArmorSaved,
                    target.EntityId,
                    null,
                    _armorSavedPoints[index],
                    target.FactionId);
            }
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var agent = _agentStates[index];
            if (_damageTotals[index] == 0 || agent.IsAlive)
            {
                continue;
            }

            agent.TargetEntityId = null;
            agent.Intent = AgentIntent.Dead;

            // Cleared here rather than only on the next tick's evasive pass. A
            // warrior can resolve an evasive action at the movement stage and
            // be killed by the attack stage later in the same tick, and the
            // state hash is taken at the end of that tick — so leaving it to
            // the next pass would fold a corpse's last step into the hash.
            agent.EvasiveAction = EvasiveAction.None;
            AddEvent(
                events,
                BattleEventKind.Death,
                agent.EntityId,
                null,
                0,
                agent.FactionId);
        }
    }

    /// <summary>
    /// Plan section 3(c): the full combo-transition algorithm, run
    /// immediately after one attack attempt's <see cref="AttackResolution"/>
    /// is known and immediately before the attack proposal is buffered.
    /// Mutates <paramref name="source"/>'s
    /// <see cref="AgentState.ComboStepsRemaining"/> and
    /// <see cref="AgentState.ComboTargetEntityId"/>, writes the cooldown this
    /// blow earns into <see cref="AgentState.AttackCooldownRemaining"/> —
    /// replacing the unconditional pre-resolution write this preset family
    /// used before attack combinations existed — and returns this attack
    /// event's chain-position value, or <c>null</c> when the blow is not
    /// part of a chain.
    /// </summary>
    /// <remarks>
    /// The six numbered steps below are plan section 3(c)'s six numbered
    /// steps, in the same order, and must not be reordered or simplified:
    /// this is the determinism-critical core of the feature.
    /// <para>
    /// Every branch below that writes the plan's "normal" (non-combo)
    /// cooldown reads it from <see cref="AgentState.AttackCooldownTicks"/>
    /// rather than from <c>weaponProfile.AttackCooldownTicks</c>. The two are
    /// bit-identical for every agent <see cref="CreateAgent"/> ever produces
    /// — both are the same ruleset lookup against the same immutable
    /// loadout — so this is not a behavioural choice, only which of two
    /// equal sources to read. <see cref="AgentState.AttackCooldownTicks"/>
    /// is preferred because it is the single cached value every other tick
    /// stage already reads (see <see cref="CreateAgent"/>'s remarks on why a
    /// second reach path must never exist), rather than opening a second,
    /// redundant profile lookup that could only ever disagree with it by
    /// construction error.
    /// </para>
    /// </remarks>
    private int? ResolveComboTransition(
        AgentState source,
        AgentState target,
        AttackResolution resolution,
        BodyPart hitLocation)
    {
        var weaponProfile = ResolveAttackerWeaponProfile(source.Loadout);

        // Step 1.
        var wasChaining = source.ComboStepsRemaining > 0;

        // Step 2: Question 1's strict target-binding check. The retarget, if
        // any, already happened earlier this same tick in
        // SelectTargetsAndIntents.
        var retargeted =
            wasChaining && source.ComboTargetEntityId != source.TargetEntityId;

        // Step 3.
        if (retargeted)
        {
            source.ComboStepsRemaining = 0;
            source.ComboTargetEntityId = null;
            wasChaining = false;
        }

        int? comboPosition;
        int cooldown;

        if (resolution != AttackResolution.Landed)
        {
            // Step 4: a non-landed attempt is neither a roll nor a break.
            // ComboStepsRemaining and ComboTargetEntityId are left exactly as
            // they are post step 3.
            comboPosition = null;
            cooldown = wasChaining
                ? weaponProfile.ComboCooldownTicks
                : source.AttackCooldownTicks;
        }
        else if (!wasChaining)
        {
            // Step 5: an unchained landed blow, eligible to open a chain.
            var openRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                comboStepsRemaining: 0,
                ComboResolver.ComboOpenTag) % ClashProfile.BasisPointScale);

            if (openRoll < weaponProfile.ComboOpenChanceBasisPoints)
            {
                var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
                comboPosition = 1;
                source.ComboStepsRemaining = maxSteps - 1;
                source.ComboTargetEntityId = target.EntityId;
                cooldown = source.ComboStepsRemaining > 0
                    ? weaponProfile.ComboCooldownTicks
                    : source.AttackCooldownTicks;
            }
            else
            {
                comboPosition = null;
                cooldown = source.AttackCooldownTicks;
            }
        }
        else
        {
            // Step 6: a continuing blow candidate.
            var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
            var thisPosition = maxSteps - source.ComboStepsRemaining + 1;
            var continueRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                source.ComboStepsRemaining,
                ComboResolver.ComboContinueTag) % ClashProfile.BasisPointScale);
            var continuationSucceeded =
                continueRoll < weaponProfile.ComboContinueChanceBasisPoints;

            // Mirrors however _damageTotals's accumulation reads
            // target.HitPoints for this same purpose: against the value as
            // it stands before the end-of-pass-1 damage-application loop
            // applies it, not adjusted for any other attacker's damage
            // accumulated this same tick.
            var mitigatedDamageForKillCheck = _rules.ApplyArmorMitigation(
                source.DamagePerAttack,
                target.Loadout.Armor,
                hitLocation);
            var killedByThisBlow =
                target.HitPoints - mitigatedDamageForKillCheck <= 0;

            // The blow landed, so it always counts, regardless of what
            // happens next.
            comboPosition = thisPosition;

            // Design 3.2's exact order: check 1 (continuation), check 2 (max
            // length reached), check 3 (the target dies on this blow). Check
            // 4 ("target out of reach") cannot fire here — the pre-check
            // above already guaranteed the target is in range for this
            // tick's attempt — it fires on a later tick via that pre-check's
            // clearing clause instead.
            var chainSurvives = continuationSucceeded &&
                thisPosition < maxSteps &&
                !killedByThisBlow;

            if (chainSurvives)
            {
                // Guaranteed > 0 here, since thisPosition < maxSteps was
                // just confirmed.
                source.ComboStepsRemaining -= 1;
                cooldown = weaponProfile.ComboCooldownTicks;
            }
            else
            {
                source.ComboStepsRemaining = 0;
                source.ComboTargetEntityId = null;
                cooldown = source.AttackCooldownTicks;
            }
        }

        // One of exactly two non-decrement writers of
        // AgentState.AttackCooldownRemaining. The other is the V7 pressure
        // interrupt in ResolveEquipmentPosturesAndProvisionalFootwork, which
        // re-charges this same field from the movement stage under
        // MovementRuleset.AppliesPressureInterrupt and is therefore unreachable
        // under every preset from V1 through V6. That site carries the matching
        // comment and the argument for why the inversion is deliberate.
        source.AttackCooldownRemaining = cooldown;
        return comboPosition;
    }

    private void ResolveOutcome(List<BattleEvent> events)
    {
        var faction0Alive = false;
        var faction1Alive = false;

        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                faction0Alive = true;
            }
            else
            {
                faction1Alive = true;
            }
        }

        Outcome = (faction0Alive, faction1Alive) switch
        {
            (false, false) => BattleOutcome.Draw,
            (true, false) => BattleOutcome.Faction0Victory,
            (false, true) => BattleOutcome.Faction1Victory,
            _ when Tick >= Scenario.TickLimit => BattleOutcome.Draw,
            _ => BattleOutcome.Ongoing,
        };

        if (Outcome == BattleOutcome.Ongoing)
        {
            return;
        }

        var winningFaction = Outcome switch
        {
            BattleOutcome.Faction0Victory => 0,
            BattleOutcome.Faction1Victory => 1,
            _ => (int?)null,
        };
        AddEvent(
            events,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: (int)Outcome,
            factionId: winningFaction);
    }
}
