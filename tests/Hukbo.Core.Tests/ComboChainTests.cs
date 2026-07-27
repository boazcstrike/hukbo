using System.Linq;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Task 4 of docs/plans/2026-07-27-combat-preset-v3-combos.md: unit-level
/// coverage of the section 3 attack-combination state machine, driven
/// through constructed <see cref="AgentState"/>/<see cref="WeaponProfile"/>
/// fixtures rather than a full multi-agent battle.
/// </summary>
/// <remarks>
/// <para>
/// Every scenario places exactly one attacker against one inert target
/// (<c>damagePerAttack: 0</c>, so the target can never harm the attacker
/// back) close enough that both resolve <see cref="AgentIntent.Attacking"/>
/// every tick, so <c>GatherMovementProposals</c> never displaces either body
/// and the fixture's hand-picked positions stay exactly where they were set.
/// </para>
/// <para>
/// A chain already in progress is set directly on the constructed
/// <see cref="AgentState"/> before the tick under test — via
/// <see cref="AgentState.ComboStepsRemaining"/> and
/// <see cref="AgentState.ComboTargetEntityId"/> — rather than built up over
/// several real, rolled blows. Every fixture here pins
/// <see cref="WeaponProfile.ComboOpenChanceBasisPoints"/> and
/// <see cref="WeaponProfile.ComboContinueChanceBasisPoints"/> to either
/// <c>0</c> or <see cref="ClashProfile.BasisPointScale"/>, so a roll's
/// outcome is certain by construction — success or failure never depends on
/// predicting <c>ComboResolver.MixCombo</c>'s actual hash value for a given
/// seed/tick/entity tuple.
/// </para>
/// <para>
/// Landed-versus-not is controlled the same way, through
/// <see cref="ClashProfile"/> rather than an unpredictable roll:
/// <see cref="ClashProfile.Neutral"/> resolves every accepted attack to
/// <see cref="AttackResolution.Landed"/>, and
/// <see cref="BuildAlwaysEvadedClashProfile"/> resolves the Kampilan
/// defender cell to <see cref="AttackResolution.Evaded"/> with certainty.
/// </para>
/// </remarks>
public sealed class ComboChainTests
{
    private const int AlwaysBasisPoints = ClashProfile.BasisPointScale;
    private const int NeverBasisPoints = 0;

    /// <summary>
    /// Well inside the contact distance for <see cref="BuildScenario"/>'s
    /// body radius (contact is at <c>2 * BodyRadiusRaw</c> centre distance),
    /// so both agents resolve <see cref="AgentIntent.Attacking"/> and never
    /// move.
    /// </summary>
    private const int AdjacentOffsetRaw = 2 * FixedPoint.Scale;

    private static readonly CombatLoadout AttackerLoadout =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout TargetLoadout =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    [Fact]
    public void OpeningRoll_WhenItSucceeds_OpensAChainAtPositionOne()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: AlwaysBasisPoints,
            comboContinueChanceBasisPoints: NeverBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        // maxSteps = Math.Min(Level(3), ComboMaxSteps(3)) = 3; a fresh opening
        // sets ComboStepsRemaining = maxSteps - 1.
        Assert.Equal(2, attacker.ComboStepsRemaining);
        Assert.Equal(target.EntityId, attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.ComboCooldownTicks, attacker.AttackCooldownRemaining);

        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Equal(1, attackEvent.ComboPosition);
    }

    [Fact]
    public void OpeningRoll_WhenItFails_LeavesTheAttackerUnchained()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.AttackCooldownTicks, attacker.AttackCooldownRemaining);

        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Null(attackEvent.ComboPosition);
    }

    [Fact]
    public void ContinuationRoll_WhenItSucceedsBelowTheCap_AdvancesTheChainByOneStep()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        // maxSteps = min(3, 3) = 3; thisPosition = 3 - 2 + 1 = 2, below the
        // cap, so a successful roll survives and the chain steps down by one.
        Assert.Equal(1, attacker.ComboStepsRemaining);
        Assert.Equal(target.EntityId, attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.ComboCooldownTicks, attacker.AttackCooldownRemaining);

        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Equal(2, attackEvent.ComboPosition);
    }

    [Fact]
    public void ContinuationRoll_WhenItFails_BreaksTheChainEvenBelowTheCap()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: NeverBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.AttackCooldownTicks, attacker.AttackCooldownRemaining);

        // The blow still landed, so it still counts as chain position 2 even
        // though the chain does not survive past it.
        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Equal(2, attackEvent.ComboPosition);
    }

    [Fact]
    public void ContinuationRoll_WhenTheMaximumLengthIsReached_BreaksTheChainEvenOnASuccessfulRoll()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 2,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 1;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        // maxSteps = min(3, 2) = 2; thisPosition = 2 - 1 + 1 = 2, which
        // already equals maxSteps, so the cap ends the chain even though the
        // continuation roll itself succeeds (basis points pinned to always).
        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.AttackCooldownTicks, attacker.AttackCooldownRemaining);

        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Equal(2, attackEvent.ComboPosition);
    }

    [Fact]
    public void ATargetSwitch_BreaksTheChainBeforeAnyRollIsEvaluated()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);

        // The chain is bound to an entity ID that will never equal
        // SelectTargetsAndIntents' fresh pick this tick (the only living
        // enemy is entity 2). Question 1's strict target-binding check reads
        // ComboTargetEntityId against TargetEntityId by value only -- it does
        // not need a third agent to actually exist at the stale ID to prove
        // the retarget clears the chain.
        const ulong StaleChainTargetEntityId = 999;
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = StaleChainTargetEntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.AttackCooldownTicks, attacker.AttackCooldownRemaining);

        // The retarget clears the old chain before step 5 evaluates an
        // opening roll for the new target; ComboOpenChanceBasisPoints is
        // pinned to never here so no new chain quietly reopens and hides
        // the clearing this test is asserting.
        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Null(attackEvent.ComboPosition);
    }

    [Fact]
    public void TheTargetDying_BreaksTheChainOnTheTickTheAttackerDiscoversIt()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        // Simulates the target dying from a mechanism outside this fixture's
        // one attacker -- some other source of damage between ticks. The
        // target is the attacker's only living enemy, so
        // SelectTargetsAndIntents resolves the attacker's fresh
        // TargetEntityId to null this tick, and the pre-check's "no target"
        // clause is what actually clears the chain (per plan section 3(a)):
        // by the time GatherAndCommitAttacks ever reads a target, a dead one
        // is already excluded from candidacy, so "the bound target has died"
        // and "the attacker now has no target at all" are the same
        // observable outcome for any attacker with exactly one enemy.
        target.HitPoints = 0;

        simulation.AdvanceOneTick();

        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.DoesNotContain(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack && e.SourceEntityId == attacker.EntityId);
    }

    [Fact]
    public void TheTargetLeavingAttackRange_BreaksTheChainWithoutARetargetOrADeath()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: NeverBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, ClashProfile.Neutral);

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        // Moved far outside AttackRangeRaw (20 world units) but still well
        // inside PerceptionRangeRaw, so it stays the attacker's nearest --
        // and only -- living enemy. TargetEntityId therefore does not change
        // this tick, which is what isolates plan section 3(a)'s third
        // clearing clause (!IsWithinAttackRange) from the target-switch
        // clause covered by ATargetSwitch_BreaksTheChainBeforeAnyRollIsEvaluated.
        target.XRaw = 500 * FixedPoint.Scale;

        simulation.AdvanceOneTick();

        Assert.Equal(target.EntityId, attacker.TargetEntityId);
        Assert.Equal(0, attacker.ComboStepsRemaining);
        Assert.Null(attacker.ComboTargetEntityId);
        Assert.DoesNotContain(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack && e.SourceEntityId == attacker.EntityId);
    }

    [Fact]
    public void ANonLandedFollowUp_PreservesTheChainExactlyAsItWas()
    {
        var scenario = BuildScenario();
        var kampilan = BuildKampilanProfile(
            comboOpenChanceBasisPoints: AlwaysBasisPoints,
            comboContinueChanceBasisPoints: AlwaysBasisPoints,
            comboMaxSteps: 3,
            comboCooldownTicks: 2);
        var rules = BuildRuleset(kampilan, BuildAlwaysEvadedClashProfile());

        var attacker = BuildAgent(1, 0, 0, 0, scenario, AttackerLoadout, damagePerAttack: 5);
        var target = BuildAgent(2, 1, AdjacentOffsetRaw, 0, scenario, TargetLoadout, damagePerAttack: 0);
        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;
        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);

        simulation.AdvanceOneTick();

        // Neither the open nor the continue roll is pinned to "never" here
        // -- both are pinned to "always" -- so if a non-landed attempt were
        // mistaken for a roll opportunity this assertion would catch it: an
        // "always" roll succeeding would still have to leave the state
        // unchanged, per plan section 3(c) step 4.
        Assert.Equal(2, attacker.ComboStepsRemaining);
        Assert.Equal(target.EntityId, attacker.ComboTargetEntityId);
        Assert.Equal(kampilan.ComboCooldownTicks, attacker.AttackCooldownRemaining);

        var attackEvent = RequireAttackEvent(simulation, attacker.EntityId);
        Assert.Equal(AttackResolution.Evaded, attackEvent.Resolution);
        Assert.Null(attackEvent.ComboPosition);
    }

    private static BattleEvent RequireAttackEvent(
        BattleSimulation simulation,
        ulong sourceEntityId) =>
        Assert.Single(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack && e.SourceEntityId == sourceEntityId);

    private static Scenario BuildScenario() =>
        new(
            Seed: 1,
            MapWidth: 2_000,
            MapHeight: 2_000,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 1_000_000,
            PerceptionRangeRaw = 5_000 * FixedPoint.Scale,
            AttackRangeRaw = 20 * FixedPoint.Scale,
            BodyRadiusRaw = 4 * FixedPoint.Scale,
            MovementSpeedRaw = 3 * FixedPoint.Scale,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV3,
        };

    private static AgentState BuildAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int damagePerAttack) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            damagePerAttack,
            scenario.AttackCooldownTicks,
            loadout,
            // Level 3 throughout: every fixture's ComboMaxSteps stays at or
            // below 3, so maxSteps = Math.Min(Level, ComboMaxSteps) is driven
            // by the weapon's own cap in every scenario, never by the level
            // -- exactly the "every weapon's cap and the level both bound the
            // chain" case the plan's section 3(c) step 5 commentary expects
            // to be the ordinary one.
            level: 3);

    /// <summary>
    /// The Kampilan attribute row this file's fixtures vary per scenario.
    /// Reach and cooldown are held constant at values already proven safe
    /// against <see cref="CombatRuleset.MinimumProfileReachRawExclusive"/>
    /// and against landing every attack attempt inside one cooldown window;
    /// only the four combo fields move between tests.
    /// </summary>
    private static WeaponProfile BuildKampilanProfile(
        int comboOpenChanceBasisPoints,
        int comboContinueChanceBasisPoints,
        int comboMaxSteps,
        int comboCooldownTicks) =>
        new(
            DamagePerAttack: 5,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: 5,
            comboOpenChanceBasisPoints,
            comboContinueChanceBasisPoints,
            comboMaxSteps,
            comboCooldownTicks);

    /// <summary>
    /// Builds a structurally minimal <see cref="CombatRuleset"/> whose roster
    /// is exactly <see cref="CombatPresetId.PrecolonialPhilippinesV3"/>'s
    /// registered four-loadout roster -- required because
    /// <c>BattleSimulation.CreateForTesting</c>'s three-argument overload
    /// rejects an injected ruleset whose roster disagrees with the
    /// registered entry for the scenario's <see cref="CombatPresetId"/>.
    /// Only <see cref="WeaponId.Kampilan"/>'s profile is exercised by any
    /// fixture in this file; the other three weapons carry an inert
    /// placeholder profile solely to satisfy
    /// <see cref="CombatRuleset"/>'s "every roster weapon needs an attribute
    /// row" and "a one-handed weapon needs a paired row" construction
    /// invariants.
    /// </summary>
    private static CombatRuleset BuildRuleset(
        WeaponProfile kampilanProfile,
        ClashProfile clashProfile)
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV3)
            .Roster;

        var flatWeights = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1))
            .ToArray();
        var flatMultipliers = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1_000))
            .ToArray();
        var flatProfile = new TargetWeightProfile(flatWeights);

        var placeholderProfile = new WeaponProfile(
            DamagePerAttack: 1,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: 5);

        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(kampilanProfile),
            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(placeholderProfile),
            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                placeholderProfile,
                placeholderProfile),
            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                placeholderProfile,
                placeholderProfile),
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV3,
            version: 1,
            generalTargets: flatProfile,
            weaponTargets: new Dictionary<WeaponId, TargetWeightProfile>
            {
                [WeaponId.Kampilan] = flatProfile,
                [WeaponId.Wasay] = flatProfile,
                [WeaponId.Kalis] = flatProfile,
                [WeaponId.Itak] = flatProfile,
            },
            armors: [ArmorId.LightOrganic],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = new TargetWeightProfile(flatMultipliers),
            },
            roster: roster,
            weaponAttributes: weaponAttributes,
            clashProfile: clashProfile);
    }

    /// <summary>
    /// A clash profile that resolves every attack the <see cref="TargetLoadout"/>
    /// defender (<see cref="WeaponId.Wasay"/>, <see cref="ShieldId.None"/>)
    /// faces to <see cref="AttackResolution.Evaded"/>, with certainty: the
    /// void channel for that one cell is set to
    /// <see cref="ClashProfile.BasisPointScale"/> (100%) and every other
    /// channel stays at zero, so the interval walk's cumulative reaches the
    /// scale before any roll in <c>[0, BasisPointScale)</c> can fall short of
    /// it. Resolution is keyed on the <em>defender's</em> weapon and shield,
    /// not the attacker's, so this deliberately targets the loadout every
    /// fixture in this file spawns as the target, not
    /// <see cref="AttackerLoadout"/>'s Kampilan. Every roster weapon still
    /// needs a covering cell -- see <see cref="CombatRuleset"/>'s
    /// <c>ValidateClashProfileCoversTheRoster</c> -- even though this file
    /// only ever spawns one attacker/target pair.
    /// </summary>
    private static ClashProfile BuildAlwaysEvadedClashProfile()
    {
        WeaponId[] weapons =
        [
            WeaponId.Kampilan,
            WeaponId.Wasay,
            WeaponId.Kalis,
            WeaponId.Itak,
        ];

        var weaponIntercept = new Dictionary<(WeaponId, ShieldId, WeaponId), int>();
        var voidChannel = new Dictionary<(WeaponId, ShieldId), int>();
        var hardShareBases = new Dictionary<WeaponId, int>();
        var hardShareMultipliers = new Dictionary<WeaponId, int>();

        foreach (var defender in weapons)
        {
            hardShareBases[defender] = 0;
            hardShareMultipliers[defender] = 1_000;
            voidChannel[(defender, ShieldId.None)] =
                defender == TargetLoadout.Weapon ? ClashProfile.BasisPointScale : 0;

            foreach (var attacker in weapons)
            {
                weaponIntercept[(defender, ShieldId.None, attacker)] = 0;
            }
        }

        return new ClashProfile(
            weaponIntercept: weaponIntercept,
            shieldIntercept: 0,
            voidChannel: voidChannel,
            hardShareBases: hardShareBases,
            hardShareMultipliers: hardShareMultipliers,
            minimumHardShareBasisPoints: 0,
            maximumHardShareBasisPoints: 0,
            maximumInterceptionBasisPoints: ClashProfile.BasisPointScale);
    }
}
