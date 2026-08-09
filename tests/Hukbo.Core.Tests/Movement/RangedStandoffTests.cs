using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Ranged-units plan RU-21: <see cref="MovementPresetId.RangedStandoffV8"/>,
/// registered as a verbatim restatement of
/// <see cref="MovementPresetId.PersistentContingentsV4"/>'s field values plus
/// one rule, entirely inside the legacy (non equipment-relative)
/// <c>GatherMovementProposals</c> path. Covers the plan's four acceptance
/// criteria: melee-only equivalence with V4, the hold arm itself, the
/// Moving/Holding boundary under a contested proposal, and the
/// blocked-streak/stall-escape exemption for a holding warrior.
/// Tests hold their own <c>AgentState</c> references, which
/// <c>CreateForTesting</c> retains, so authoritative per-agent state is read
/// directly rather than through the <c>AgentView</c> projection.
/// </summary>
public sealed class RangedStandoffTests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Bangkaw =
        new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None);

    // ----- Criterion 1: melee-only roster is byte-identical to V4 -----

    /// <summary>
    /// A melee-only roster's <see cref="WeaponProfile.StandoffDistanceRaw"/>
    /// is always 0 for every loadout, so the ranged-standoff branch inside
    /// <c>GatherMovementProposals</c> is never entered and every agent falls
    /// through to the same code V4 has always run. <c>Scenario.MovementPreset</c>'s
    /// numeric value folds directly into <c>ComputeStateHash()</c>
    /// (<c>StateHasher.cs</c>), so a V4 run and a V8 run can never produce a
    /// literally equal state hash even with byte-identical agent behaviour;
    /// the operative proof here is per-agent field equality every tick plus
    /// full <c>LastEvents</c> equality, which carries no preset-specific
    /// field and is precedented elsewhere in the suite
    /// (<c>DeterminismTests</c>, <c>BattleSimulationTests</c>).
    /// </summary>
    [Fact]
    public void MeleeOnlyRosterUnderV8MatchesV4TrajectoriesAndEvents()
    {
        var v4Scenario = CreateMeleeScenario(MovementPresetId.PersistentContingentsV4);
        var v8Scenario = CreateMeleeScenario(MovementPresetId.RangedStandoffV8);

        var v4 = BattleSimulation.CreateForTesting(v4Scenario, CreateMeleeRoster(v4Scenario));
        var v8 = BattleSimulation.CreateForTesting(v8Scenario, CreateMeleeRoster(v8Scenario));

        var comparedTicks = 0;
        for (var tick = 0; tick < 600 && v4.Outcome == BattleOutcome.Ongoing; tick++)
        {
            v4.AdvanceOneTick();
            v8.AdvanceOneTick();
            comparedTicks++;

            Assert.Equal(v4.Tick, v8.Tick);
            Assert.Equal(v4.Outcome, v8.Outcome);
            Assert.Equal(v4.LastEvents, v8.LastEvents);

            Assert.Equal(v4.Agents.Count, v8.Agents.Count);
            for (var i = 0; i < v4.Agents.Count; i++)
            {
                var left = v4.Agents[i];
                var right = v8.Agents[i];

                Assert.Equal(left.EntityId, right.EntityId);
                Assert.Equal(left.XRaw, right.XRaw);
                Assert.Equal(left.YRaw, right.YRaw);
                Assert.Equal(left.HitPoints, right.HitPoints);
                Assert.Equal(left.Intent, right.Intent);
                Assert.Equal(left.IsAlive, right.IsAlive);
                Assert.Equal(left.TargetEntityId, right.TargetEntityId);
                Assert.Equal(left.MovementResolution, right.MovementResolution);
            }
        }

        // A trajectory comparison over zero ticks would prove nothing; this
        // is the guard against the loop above silently no-op-ing because the
        // battle resolved (or was already Ongoing == false) before the first
        // AdvanceOneTick call.
        Assert.True(comparedTicks > 50, $"Only compared {comparedTicks} ticks.");
    }

    // ----- Criterion 2: at or inside standoff distance, hold -----

    /// <summary>
    /// A Bangkaw shooter placed exactly at its own
    /// <see cref="WeaponProfile.StandoffDistanceRaw"/> from its target -- the
    /// "at" boundary of "at or inside standoff distance" -- reports
    /// <see cref="AgentIntent.Holding"/>, proposes nothing (the collision
    /// stage resolves the null proposal to
    /// <see cref="MovementResolution.None"/>, never
    /// <see cref="MovementResolution.Blocked"/>), and does not move. Checked
    /// on the first tick, not a later one: Bangkaw's own
    /// <see cref="WeaponProfile.AttackRangeRaw"/> (48 world units) exceeds
    /// its <see cref="WeaponProfile.StandoffDistanceRaw"/> (36 world units),
    /// so a shooter sitting exactly at standoff distance is also within
    /// reach, and <c>GatherAndCommitAttacks</c> launches immediately and
    /// re-marks <c>Intent</c> to <see cref="AgentIntent.Attacking"/> in that
    /// same tick -- the surviving proof that the standoff check, not the
    /// attack gather, decided this tick's movement is that the shooter's
    /// position and <see cref="MovementResolution"/> are unchanged, since no
    /// proposal was ever built. Checked on the first tick specifically,
    /// before <c>PersistentContingentsV4</c>'s own arrival-taper floor --
    /// which nudges even a zero-speed agent by up to one body radius while
    /// snapping to its formation aim point, unrelated to this preset and
    /// unchanged by it -- can perturb the target's position and make this
    /// exact zero-margin distance check flaky on a later tick.
    /// </summary>
    [Fact]
    public void RangedWarriorAtStandoffDistanceHoldsAndDoesNotMove()
    {
        var scenario = CreateRangedScenario(MovementPresetId.RangedStandoffV8);
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        Assert.True(standoffRaw > 0, "Bangkaw must declare a positive standoff distance.");

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var target = CreateAgent(
            2,
            factionId: 1,
            xRaw: standoffRaw,
            yRaw: 0,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, target);

        simulation.AdvanceOneTick(); // Launch tick: Intent re-marked Attacking.

        Assert.Equal(AgentIntent.Attacking, shooter.Intent);
        Assert.Equal(0, shooter.XRaw);
        Assert.Equal(0, shooter.YRaw);
        Assert.Equal(MovementResolution.None, shooter.MovementResolution);
    }

    /// <summary>
    /// Strictly inside standoff distance (not merely at the boundary) holds
    /// the same way.
    /// </summary>
    [Fact]
    public void RangedWarriorInsideStandoffDistanceHoldsAndDoesNotMove()
    {
        var scenario = CreateRangedScenario(MovementPresetId.RangedStandoffV8);
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var target = CreateAgent(
            2,
            factionId: 1,
            xRaw: standoffRaw - (2 * FixedPoint.Scale),
            yRaw: 0,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, target);

        simulation.AdvanceOneTick();
        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Holding, shooter.Intent);
        Assert.Equal(0, shooter.XRaw);
        Assert.Equal(0, shooter.YRaw);
        Assert.Equal(MovementResolution.None, shooter.MovementResolution);
    }

    // ----- Criterion 3: a contested proposal reports Moving, never Holding -----

    /// <summary>
    /// Two Bangkaw shooters, starting in body contact with each other and
    /// both pursuing the same distant target, contest the same ground as
    /// they close in -- proving that a proposal the collision stage
    /// resolves to anything other than <see cref="MovementResolution.Moved"/>
    /// (<see cref="MovementResolution.Blocked"/> or
    /// <see cref="MovementResolution.Separated"/>) still reports
    /// <see cref="AgentIntent.Moving"/>, never <see cref="AgentIntent.Holding"/>.
    /// <see cref="AgentIntent.Holding"/> has exactly one producer in the
    /// codebase -- the standoff-distance check -- and collision resolution
    /// never touches <c>Intent</c> at all, so this is a regression guard on
    /// that separation staying true under contention, not a mechanism this
    /// task's code could plausibly violate by construction.
    /// </summary>
    [Fact]
    public void RangedWarriorsWithContestedProposalsReportMovingNeverHolding()
    {
        var scenario = CreateRangedScenario(MovementPresetId.RangedStandoffV8);
        var rules = RangedRules();

        // A collision move request must start at a non-negative coordinate
        // (CollisionResolver.Validate), so the shooters sit near the origin
        // and the target sits 60 world units further out along X, rather
        // than the shooters sitting at a negative offset from an
        // origin-placed target.
        var farXRaw = 60 * FixedPoint.Scale;
        var target = CreateAgent(
            3,
            factionId: 1,
            xRaw: farXRaw,
            yRaw: 0,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);
        var shooterA = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var shooterB = CreateAgent(
            2,
            factionId: 0,
            xRaw: 0,
            yRaw: 200, // less than one body diameter (1,024 raw): starts overlapping.
            scenario,
            rules,
            Bangkaw);

        var simulation = BattleSimulation.CreateForTesting(
            scenario, rules, shooterA, shooterB, target);

        // 20 ticks at 0.5 world units/tick closes at most 10 world units,
        // leaving the shooters at least 50 world units from the target --
        // beyond both the 36-unit standoff distance and the 48-unit reach,
        // so neither a hold nor a launch (which would re-mark Intent to
        // Attacking, per SelectTargetsAndIntents's comment) complicates the
        // Moving assertion below.
        var sawContestedResolution = false;
        for (var tick = 0; tick < 20; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.NotEqual(AgentIntent.Holding, shooterA.Intent);
            Assert.NotEqual(AgentIntent.Holding, shooterB.Intent);

            if (shooterA.MovementResolution is
                    MovementResolution.Blocked or MovementResolution.Separated ||
                shooterB.MovementResolution is
                    MovementResolution.Blocked or MovementResolution.Separated)
            {
                sawContestedResolution = true;
            }
        }

        Assert.True(
            sawContestedResolution,
            "Expected at least one tick where the two shooters, starting " +
            "in body contact and pursuing the same target, contested the " +
            "same ground.");
        Assert.Equal(AgentIntent.Moving, shooterA.Intent);
        Assert.Equal(AgentIntent.Moving, shooterB.Intent);
    }

    // ----- Criterion 4: a holding warrior never accumulates a blocked streak -----

    /// <summary>
    /// A holding warrior's null movement proposal resolves to
    /// <see cref="MovementResolution.None"/>, not
    /// <see cref="MovementResolution.Blocked"/>, so
    /// <c>CollisionScratch.RecordBlocked</c> never increments its blocked
    /// streak. Run well past <see cref="FormationRules.StallEscapeStreakTicks"/>
    /// so the stall escape would have fired at least once had the streak
    /// been (incorrectly) accumulating. The target starts two body radii
    /// inside standoff distance, the same margin
    /// <see cref="RangedWarriorInsideStandoffDistanceHoldsAndDoesNotMove"/>
    /// uses and for the same reason: <c>PersistentContingentsV4</c>'s own
    /// arrival-taper floor nudges even a zero-speed agent by up to one body
    /// radius while snapping to its formation aim point once, on the first
    /// tick, unrelated to this preset and unchanged by it, and this margin
    /// safely absorbs both that one-time nudge and its own slow single-unit
    /// creep toward the shooter over the run. The distance never leaves the
    /// standoff band, so the position invariant holds every tick regardless
    /// of a launch's occasional, expected re-marking of <c>Intent</c> to
    /// <see cref="AgentIntent.Attacking"/> on the tick a shot is released --
    /// which is why this test asserts position, not <c>Intent</c>, inside
    /// the loop, and only asserts <c>Intent</c> is never
    /// <see cref="AgentIntent.Moving"/>.
    /// </summary>
    [Fact]
    public void HoldingWarriorNeverAccumulatesBlockedStreakAndStallEscapeNeverFires()
    {
        const int ticksToRun = FormationRules.StallEscapeStreakTicks * 2;
        var scenario = CreateRangedScenario(MovementPresetId.RangedStandoffV8) with
        {
            TickLimit = ticksToRun + 10,
        };
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var target = CreateAgent(
            2,
            factionId: 1,
            xRaw: standoffRaw - (2 * FixedPoint.Scale),
            yRaw: 0,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, target);

        for (var tick = 0; tick < ticksToRun; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.NotEqual(AgentIntent.Moving, shooter.Intent);
            Assert.Equal(0, shooter.XRaw);
            Assert.Equal(0, shooter.YRaw);
        }

        Assert.Equal(0L, simulation.LongestBlockedStreakTicks);
    }

    // ----- Helpers -----

    private static CombatRuleset RangedRules() =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);

    /// <summary>
    /// Builds a scenario on the V5 ranged preset with a caller-chosen
    /// movement preset. Mirrors <c>ProjectileTests.CreateRangedTestScenario</c>;
    /// the scenario-level combat fields are never read by any agent
    /// constructed through <see cref="CreateAgent"/> below, because V5
    /// declares weapon profiles for every loadout.
    /// </summary>
    private static Scenario CreateRangedScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 500,
            MapHeight: 500,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 500)
        {
            MaximumHitPoints = 1_000,
            DamagePerAttack = 10,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = movementPreset,
            MaximumProjectilesInFlight = 4,
        };

    /// <summary>
    /// Mirrors <c>BattleSimulation.CreateAgent</c>'s own resolution of
    /// per-agent attack range, damage, and cooldown from the ruleset's
    /// <see cref="WeaponProfile"/>. Takes raw coordinates directly, unlike
    /// <c>ProjectileTests.CreateAgent</c>, so standoff-boundary cases can
    /// place a target at an exact raw distance.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout,
        int? movementSpeedRawOverride = null)
    {
        var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
        return new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            movementSpeedRawOverride ?? scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }

    /// <summary>
    /// A melee-only scenario on <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>,
    /// matching <c>MovementPipelineIntegrationTests.CreateScenario</c>'s
    /// combat-field shape but with a caller-chosen movement preset and a
    /// small roster to exercise contested movement.
    /// </summary>
    private static Scenario CreateMeleeScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 300,
            MapHeight: 200,
            AgentsPerFaction: 3,
            TickRate: 20,
            TickLimit: 4_000)
        {
            MaximumHitPoints = 60,
            DamagePerAttack = 8,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = movementPreset,
        };

    /// <summary>
    /// Three Kampilan-armed warriors per faction, closing from roughly 20
    /// world units apart along three parallel lines -- crowded enough to
    /// exercise contested movement, not just an isolated duel.
    /// </summary>
    private static AgentState[] CreateMeleeRoster(Scenario scenario) =>
        new[]
        {
            CreateMeleeAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario),
            CreateMeleeAgent(2, factionId: 0, xRaw: 0, yRaw: 1_200, scenario),
            CreateMeleeAgent(3, factionId: 0, xRaw: 0, yRaw: 2_400, scenario),
            CreateMeleeAgent(4, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 0, scenario),
            CreateMeleeAgent(5, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 1_200, scenario),
            CreateMeleeAgent(6, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 2_400, scenario),
        };

    private static AgentState CreateMeleeAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            Kampilan);
}
