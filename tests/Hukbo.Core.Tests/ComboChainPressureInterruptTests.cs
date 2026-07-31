using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Task C3 of docs/plans/2026-07-31-movement-v7-pressure-interrupt.md: the
/// combo-chain coverage the V7 pressure interrupt needs and that
/// <see cref="ComboChainTests"/> cannot provide.
/// </summary>
/// <remarks>
/// <para>
/// The design document's section 7 states the gap exactly.
/// <see cref="ComboChainTests"/>' fixtures all run under
/// <see cref="MovementPresetId.PersistentContingentsV4"/>, where
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
/// <see langword="false"/>, every agent's <see cref="AgentState.FootworkPhase"/>
/// stays <see cref="FootworkPhase.None"/>, and the footwork stage never runs at
/// all. Under that preset the interrupt is unreachable by construction, so
/// every test in that file would keep passing no matter what the interrupt did
/// to <see cref="AgentState.AttackCooldownRemaining"/>,
/// <see cref="AgentState.ComboStepsRemaining"/>, or
/// <see cref="AgentState.ComboTargetEntityId"/>.
/// </para>
/// <para>
/// The interrupt clears the chain deliberately (design section 4.4): a warrior
/// whose cooldown was just reset to the full normal value is by definition not
/// continuing a chain, and leaving <see cref="AgentState.ComboStepsRemaining"/>
/// above zero would let its next blow claim a chain position across an
/// interruption — an event field reporting a continuity that did not happen.
/// These tests are what makes that clearing observable, and
/// <see cref="TheChainSurvivesUnderEveryPresetThatDoesNotApplyTheInterrupt"/> is
/// the regression test for the version gate that keeps it out of V1 through V6.
/// </para>
/// <para>
/// Every fixture here places one Itak warrior, already mid-chain and already
/// inside the attack lifecycle, against three enemies inside its support ring
/// and none of its own allies. A chain in progress and a
/// <see cref="FootworkPhase.Commit"/> prior phase are both set directly on the
/// constructed <see cref="AgentState"/> before the tick under test, rather than
/// built up over several real rolled blows — the same construction
/// <see cref="ComboChainTests"/> uses, for the same reason: the tick under
/// observation is the interrupting one, and a chain assembled by rolling would
/// make the fixture depend on hash values for a given seed and tick instead of
/// on the behaviour being tested. The combo roll outcomes are likewise pinned
/// to certainty through <see cref="WeaponProfile.ComboOpenChanceBasisPoints"/>
/// and <see cref="WeaponProfile.ComboContinueChanceBasisPoints"/>, and
/// landed-versus-not through <see cref="ClashProfile.Neutral"/>, so no
/// assertion here depends on predicting a mixer's output.
/// </para>
/// <para>
/// The enemies deal no damage (<c>damagePerAttack: 0</c>) and every agent
/// carries a million hit points, so nobody dies, the battle never resolves, and
/// the interrupt's incoming-damage signal stays at zero. The whole weighted sum
/// is therefore driven by the support-pressure signal alone, which is the one
/// signal a fixture can drive by placing bodies.
/// </para>
/// </remarks>
public sealed class ComboChainPressureInterruptTests
{
    private const int AlwaysBasisPoints = ClashProfile.BasisPointScale;
    private const int NeverBasisPoints = 0;

    /// <summary>
    /// The scenario-wide normal attack cooldown, and therefore every agent's
    /// <see cref="AgentState.AttackCooldownTicks"/>. Deliberately different
    /// from <see cref="ComboCooldownTicks"/> so that an assertion reading
    /// <see cref="AgentState.AttackCooldownRemaining"/> can tell the two
    /// apart — the whole point of design section 4.4's choice of which
    /// cooldown the interrupt charges.
    /// </summary>
    private const int AttackCooldownTicks = 5;

    /// <summary>
    /// The Itak profile's shorter chaining cooldown. This is the value
    /// <see cref="ComboChainTests"/> asserts at its own lines 84, 140, and 347,
    /// and it is the value that must never appear on an interrupted warrior.
    /// </summary>
    private const int ComboCooldownTicks = 2;

    /// <summary>
    /// The weapon's chain cap. Held at or below the fixtures' fighter level of
    /// 3, so <c>maxSteps = Math.Min(Level, ComboMaxSteps)</c> is driven by the
    /// weapon rather than by the level, exactly as in
    /// <see cref="ComboChainTests"/>.
    /// </summary>
    private const int ComboMaxSteps = 3;

    /// <summary>The fighter level every agent in these fixtures carries.</summary>
    private const int FighterLevel = 3;

    /// <summary>
    /// Hit points high enough that a five-damage blow never kills, so no agent
    /// dies, the battle never resolves, and the incoming-damage signal's
    /// denominator makes that signal negligible even if a blow did land.
    /// </summary>
    private const int MaximumHitPoints = 1_000_000;

    private const int ActorXRaw = 100 * FixedPoint.Scale;
    private const int ActorYRaw = 100 * FixedPoint.Scale;

    /// <summary>
    /// The number of enemies placed inside the actor's support ring. With no
    /// allies present, <see cref="LocalMovementContext.SupportAllies"/> is 1 —
    /// the accumulator seeds itself with the actor — so the support-pressure
    /// signal is <c>3 * 10,000 / 1</c>, which saturates at
    /// <c>WeaponMovementRules.SignalCeilingBasisPoints</c>.
    /// </summary>
    private const int EnemyCount = 3;

    private const int SupportAlliesInFixture = 1;

    private static readonly CombatLoadout ActorLoadout =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout EnemyLoadout =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// Design section 4.4, first two writes: on the tick the interrupt fires
    /// the warrior's chain is gone — no steps remaining and no bound chain
    /// target — even though nothing else that could have cleared a chain
    /// happened. The target did not change, the target did not die, and the
    /// target did not leave attack range, so
    /// <c>GatherAndCommitAttacks</c>' own three clearing clauses are all
    /// excluded by the assertions below; the interrupt is the only remaining
    /// writer.
    /// </summary>
    [Fact]
    public void AnInterruptedWarrior_LosesItsChainOnTheInterruptingTick()
    {
        var fixture = BuildFixture(MovementPresetId.EquipmentRelativeFootworkV7);
        AssertTheFixtureStillCrossesItsRowThreshold();

        // The state the interrupt is about to act on: mid-chain, bound to the
        // enemy target selection is about to pick again, inside the attack
        // lifecycle, and free to attack this very tick.
        Assert.Equal(ComboMaxSteps - 1, fixture.Actor.ComboStepsRemaining);
        Assert.Equal(fixture.PrimaryEnemy.EntityId, fixture.Actor.ComboTargetEntityId);
        Assert.Equal(FootworkPhase.Commit, fixture.Actor.FootworkPhase);
        Assert.Equal(0, fixture.Actor.AttackCooldownRemaining);

        fixture.Simulation.AdvanceOneTick();

        // The target did not change and is still alive and in reach, so none
        // of GatherAndCommitAttacks' three pre-check clearing clauses could
        // have been what cleared this chain. Asserted before the clearing
        // itself, so that a fixture which drifted into a retarget or an
        // out-of-range case says so rather than passing for the wrong reason.
        Assert.Equal(fixture.PrimaryEnemy.EntityId, fixture.Actor.TargetEntityId);
        Assert.True(fixture.PrimaryEnemy.IsAlive);

        // The two writes design section 4.4 specifies, and the reason this
        // whole file exists.
        Assert.Equal(0, fixture.Actor.ComboStepsRemaining);
        Assert.Null(fixture.Actor.ComboTargetEntityId);

        // Design section 4.4's observable cost: DecrementCooldowns runs before
        // the footwork stage, so the cooldown the interrupt wrote is not
        // decremented on the tick it is written, and GatherAndCommitAttacks
        // then sees a non-zero cooldown and lands nothing.
        Assert.DoesNotContain(
            fixture.Simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack &&
                e.SourceEntityId == fixture.Actor.EntityId);

        // The interrupt's own spectator flag, which nothing but the interrupt
        // ever sets, and the Disengage step 1a returns. Asserted last because
        // these two corroborate the writes above rather than standing in for
        // them: a build that fired the interrupt and then skipped the writes
        // has to fail on the assertions above, not on these.
        Assert.True(fixture.Actor.BrokeOffUnderPressure);
        Assert.Equal(FootworkPhase.Disengage, fixture.Actor.FootworkPhase);
    }

    /// <summary>
    /// Design section 4.4, third write: the interrupt charges
    /// <see cref="AgentState.AttackCooldownTicks"/>, the full normal cooldown,
    /// and never the weapon profile's shorter
    /// <see cref="WeaponProfile.ComboCooldownTicks"/>. The two differ by
    /// construction in this fixture and both are asserted against, so the test
    /// distinguishes them rather than merely agreeing with whichever one
    /// happens to be written.
    /// </summary>
    [Fact]
    public void AnInterruptedWarrior_TakesTheNormalCooldownNotTheComboCooldown()
    {
        var fixture = BuildFixture(MovementPresetId.EquipmentRelativeFootworkV7);
        AssertTheFixtureStillCrossesItsRowThreshold();

        // The fixture precondition that gives the two assertions below their
        // meaning: if these ever became equal the test would pass without
        // discriminating anything.
        Assert.NotEqual(
            fixture.ActorWeapon.ComboCooldownTicks,
            fixture.Actor.AttackCooldownTicks);

        fixture.Simulation.AdvanceOneTick();

        Assert.Equal(
            fixture.Actor.AttackCooldownTicks,
            fixture.Actor.AttackCooldownRemaining);
        Assert.NotEqual(
            fixture.ActorWeapon.ComboCooldownTicks,
            fixture.Actor.AttackCooldownRemaining);

        // Corroboration, asserted after the discrimination above so that a
        // build which fires the interrupt but charges the wrong cooldown fails
        // on the cooldown rather than on the flag.
        Assert.True(fixture.Actor.BrokeOffUnderPressure);
    }

    /// <summary>
    /// Design section 4.4's consequence for the event feed: because the chain
    /// did not survive the interruption, the warrior's next landed blow is an
    /// unchained one and carries no
    /// <see cref="BattleEvent.ComboPosition"/>. The weapon's opening chance is
    /// pinned to never in this fixture, so that blow cannot open a fresh chain
    /// either and the absence of a chain position is unambiguous.
    /// </summary>
    /// <remarks>
    /// The blow lands exactly <see cref="AttackCooldownTicks"/> ticks after the
    /// interrupting one, which is design section 4.4's stated timing: the value
    /// written at the footwork stage is not decremented on the tick it is
    /// written, so the first decrement lands on the next tick and the attack
    /// gate reopens exactly that many ticks later. The interrupt does not fire
    /// again in between, because the transition-only rule of design section 4.3
    /// admits only a prior <see cref="FootworkPhase.Commit"/> or
    /// <see cref="FootworkPhase.Recover"/>, and the warrior spends every one of
    /// those ticks in the <see cref="FootworkPhase.Disengage"/> the interrupt
    /// produced.
    /// </remarks>
    [Fact]
    public void TheNextBlowAnInterruptedWarriorLands_CarriesNoChainPosition()
    {
        var fixture = BuildFixture(MovementPresetId.EquipmentRelativeFootworkV7);
        AssertTheFixtureStillCrossesItsRowThreshold();

        fixture.Simulation.AdvanceOneTick();

        Assert.Equal(0, fixture.Actor.ComboStepsRemaining);
        Assert.DoesNotContain(
            fixture.Simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack &&
                e.SourceEntityId == fixture.Actor.EntityId);
        Assert.True(fixture.Actor.BrokeOffUnderPressure);

        var (attackEvent, tick) = AdvanceUntilTheActorAttacks(
            fixture,
            maximumTicks: 4 * AttackCooldownTicks);

        Assert.Equal(1L + AttackCooldownTicks, tick);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Null(attackEvent.ComboPosition);
        Assert.Equal(
            fixture.Actor.AttackCooldownTicks,
            fixture.Actor.AttackCooldownRemaining);
    }

    /// <summary>
    /// The regression test for the version gate itself, and the last of design
    /// section 7's four required assertions: under every preset whose
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="false"/>, the identical roster at the identical seed
    /// under identical pressure produces none of the behaviour the three tests
    /// above assert. The chain advances by one step, keeps its bound target,
    /// earns the short <see cref="WeaponProfile.ComboCooldownTicks"/>, and the
    /// blow carries chain position 2.
    /// </summary>
    /// <remarks>
    /// Both arms assert exactly the same thing, which is the point: design
    /// section 7 requires that the chain under
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> behave
    /// "exactly as it does under V4", and the cheapest honest way to say that
    /// is to run the same assertions against both presets.
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> is the
    /// interesting arm — it runs the whole equipment-relative footwork stage,
    /// so the only thing keeping the interrupt out of it is the gate — and
    /// <see cref="MovementPresetId.PersistentContingentsV4"/> is the baseline
    /// <see cref="ComboChainTests"/> itself runs on.
    /// </remarks>
    [Theory]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    public void TheChainSurvivesUnderEveryPresetThatDoesNotApplyTheInterrupt(
        MovementPresetId movementPreset)
    {
        Assert.False(
            MovementPresetRegistry.Get(movementPreset).AppliesPressureInterrupt,
            $"{movementPreset} applies the pressure interrupt, so it cannot " +
            "serve as the control arm for this test.");

        var fixture = BuildFixture(movementPreset);

        fixture.Simulation.AdvanceOneTick();

        // None of the interrupt's three writes happened.
        Assert.False(fixture.Actor.BrokeOffUnderPressure);
        Assert.Equal(fixture.PrimaryEnemy.EntityId, fixture.Actor.TargetEntityId);
        Assert.Equal(ComboMaxSteps - 2, fixture.Actor.ComboStepsRemaining);
        Assert.Equal(
            fixture.PrimaryEnemy.EntityId,
            fixture.Actor.ComboTargetEntityId);

        // The chaining cooldown, not the normal one — the exact inversion of
        // what the interrupted warrior above is charged.
        Assert.Equal(
            fixture.ActorWeapon.ComboCooldownTicks,
            fixture.Actor.AttackCooldownRemaining);
        Assert.NotEqual(
            fixture.Actor.AttackCooldownTicks,
            fixture.Actor.AttackCooldownRemaining);

        // And the blow claims its chain position, because the chain it was
        // part of survived the tick.
        var attackEvent = Assert.Single(
            fixture.Simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack &&
                e.SourceEntityId == fixture.Actor.EntityId);
        Assert.Equal(AttackResolution.Landed, attackEvent.Resolution);
        Assert.Equal(2, attackEvent.ComboPosition);
    }

    /// <summary>
    /// Advances the fixture until the actor's next attack event appears,
    /// returning that event and the simulation tick it landed on. Fails the
    /// test rather than returning nothing if the bound is reached, so a
    /// warrior that never attacks again reads as a failure instead of as a
    /// vacuously satisfied assertion.
    /// </summary>
    private static (BattleEvent Event, long Tick) AdvanceUntilTheActorAttacks(
        Fixture fixture,
        int maximumTicks)
    {
        for (var step = 0; step < maximumTicks; step++)
        {
            fixture.Simulation.AdvanceOneTick();
            foreach (var candidate in fixture.Simulation.LastEvents)
            {
                if (candidate.Kind == BattleEventKind.Attack &&
                    candidate.SourceEntityId == fixture.Actor.EntityId)
                {
                    return (candidate, fixture.Simulation.Tick);
                }
            }
        }

        Assert.Fail(
            $"The actor landed no attack within {maximumTicks} ticks of the " +
            "interrupting one.");
        return default;
    }

    /// <summary>
    /// The one place these fixtures touch V7's provisional tuning, asserted
    /// rather than assumed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fixture drives the interrupt through the support-pressure signal
    /// alone: three enemies and no allies inside the support ring, with the
    /// incoming-damage and ally-collapse signals both at zero. That works only
    /// while the actor's row threshold stays at or below what a saturated
    /// support-pressure signal contributes to the weighted average on its own.
    /// It does today — the Itak row's threshold is well under that bar — but
    /// every number involved is a provisional gameplay-tuning value and plan
    /// task <b>E1</b> is going to change all of them.
    /// </para>
    /// <para>
    /// This check therefore reads the live registry rather than restating any
    /// literal, asserts nothing about what the values <em>should</em> be, and
    /// exists purely so that an E1 retune which puts the Itak row out of reach
    /// of a support-pressure-only fixture fails here, with an explanation,
    /// instead of failing three tests further down with a bare
    /// <c>Assert.Equal(0, 2)</c>. If it does fail, the fixture needs more
    /// enemies, a lower-threshold row, or a second signal — not a weaker
    /// assertion.
    /// </para>
    /// </remarks>
    private static void AssertTheFixtureStillCrossesItsRowThreshold()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV7);
        var row = ruleset.ResolveLoadoutProfile(ActorLoadout);

        Assert.True(
            WeaponMovementRules.ShouldPressureInterrupt(
                FootworkPhase.Commit,
                supportAllies: SupportAlliesInFixture,
                supportEnemies: EnemyCount,
                priorSupportAllies: SupportAlliesInFixture,
                damageTakenLastTick: 0,
                maximumHitPoints: MaximumHitPoints,
                ruleset.SupportPressureWeightBasisPoints,
                ruleset.IncomingDamageWeightBasisPoints,
                ruleset.AllyCollapseWeightBasisPoints,
                row.PressureInterruptThresholdBasisPoints),
            $"{EnemyCount} enemies against {SupportAlliesInFixture} supporting " +
            "ally no longer cross the Itak row's pressure-interrupt threshold " +
            $"of {row.PressureInterruptThresholdBasisPoints} basis points " +
            "under V7's current weights. Plan task E1 re-tunes those values, " +
            "and this fixture has to be retuned with them: give the actor " +
            "more enemies, pick a lower-threshold row, or drive a second " +
            "signal. Do not weaken the assertions that depend on this.");
    }

    /// <summary>
    /// One built battle plus the handles the assertions need: the actor, the
    /// enemy its chain is bound to, and the weapon profile whose combo
    /// cooldown the interrupt must not charge.
    /// </summary>
    private sealed record Fixture(
        BattleSimulation Simulation,
        AgentState Actor,
        AgentState PrimaryEnemy,
        WeaponProfile ActorWeapon);

    /// <summary>
    /// Builds the shared fixture under the supplied movement preset: one Itak
    /// warrior mid-chain and mid-commitment, three Wasay enemies inside its
    /// support ring at distinct distances, and no allies at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three enemies sit at 12, 14, and 16 world units. All three are
    /// inside the support ring — six body diameters, so 48 world units for
    /// this scenario's four-unit body radius — which is what drives the
    /// support-pressure signal. The distances are distinct so that target
    /// selection has an unambiguous nearest enemy and the chain can be bound
    /// to it before the first tick; every test asserts the actor's resolved
    /// <see cref="AgentState.TargetEntityId"/> against that prediction, so a
    /// wrong guess fails loudly rather than quietly turning the fixture into
    /// a retarget test. All three are far enough apart that no two bodies
    /// overlap and the collision resolver has nothing to separate.
    /// </para>
    /// <para>
    /// The movement speed is one raw unit per tick — the minimum
    /// <see cref="Scenario.Validate"/> accepts — so that a warrior which
    /// starts disengaging cannot walk out of its own attack range over the
    /// handful of ticks these tests run. Nothing here is testing movement;
    /// the geometry only has to hold still.
    /// </para>
    /// </remarks>
    private static Fixture BuildFixture(MovementPresetId movementPreset)
    {
        var scenario = BuildScenario(movementPreset);
        var itak = BuildItakProfile();
        var rules = BuildRuleset(itak);

        var actor = BuildAgent(
            entityId: 1,
            factionId: 0,
            xRaw: ActorXRaw,
            yRaw: ActorYRaw,
            scenario,
            ActorLoadout,
            damagePerAttack: 5);
        var primaryEnemy = BuildAgent(
            entityId: 2,
            factionId: 1,
            xRaw: ActorXRaw + (12 * FixedPoint.Scale),
            yRaw: ActorYRaw,
            scenario,
            EnemyLoadout,
            damagePerAttack: 0);
        var secondEnemy = BuildAgent(
            entityId: 3,
            factionId: 1,
            xRaw: ActorXRaw,
            yRaw: ActorYRaw + (14 * FixedPoint.Scale),
            scenario,
            EnemyLoadout,
            damagePerAttack: 0);
        var thirdEnemy = BuildAgent(
            entityId: 4,
            factionId: 1,
            xRaw: ActorXRaw - (16 * FixedPoint.Scale),
            yRaw: ActorYRaw,
            scenario,
            EnemyLoadout,
            damagePerAttack: 0);

        // The chain in progress and the attack lifecycle it is being fought
        // inside, both set directly on the constructed state exactly as
        // ComboChainTests sets a chain in progress. Commit is what the
        // transition-only rule of design section 4.3 requires as a prior
        // phase, and it is what an accepted attack would have written at the
        // end of the previous tick.
        actor.ComboStepsRemaining = ComboMaxSteps - 1;
        actor.ComboTargetEntityId = primaryEnemy.EntityId;
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 2;

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            actor,
            primaryEnemy,
            secondEnemy,
            thirdEnemy);

        return new Fixture(simulation, actor, primaryEnemy, itak);
    }

    private static Scenario BuildScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 2_000,
            MapHeight: 2_000,
            AgentsPerFaction: EnemyCount,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = MaximumHitPoints,
            PerceptionRangeRaw = 5_000 * FixedPoint.Scale,
            AttackRangeRaw = 20 * FixedPoint.Scale,
            BodyRadiusRaw = 4 * FixedPoint.Scale,
            MovementSpeedRaw = 1,
            AttackCooldownTicks = AttackCooldownTicks,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV3,
            MovementPreset = movementPreset,
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
            level: FighterLevel);

    /// <summary>
    /// The Itak attribute row these fixtures fight with.
    /// <see cref="WeaponId.Itak"/> rather than
    /// <see cref="WeaponId.Kampilan"/>, which is the weapon
    /// <see cref="ComboChainTests"/> uses, because the movement row the two
    /// resolve to is what carries the pressure-interrupt threshold and the
    /// Itak row's is the lowest of the six — the one a fixture can cross on
    /// the support-pressure signal alone. The combo fields are pinned to
    /// certainty in both directions: a chain can never open, and a chain
    /// already open always continues.
    /// </summary>
    private static WeaponProfile BuildItakProfile() =>
        new(
            DamagePerAttack: 5,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: AttackCooldownTicks,
            ComboOpenChanceBasisPoints: NeverBasisPoints,
            ComboContinueChanceBasisPoints: AlwaysBasisPoints,
            ComboMaxSteps: ComboMaxSteps,
            ComboCooldownTicks: ComboCooldownTicks);

    /// <summary>
    /// Builds a structurally minimal <see cref="CombatRuleset"/> whose roster
    /// is exactly <see cref="CombatPresetId.PrecolonialPhilippinesV3"/>'s
    /// registered four-loadout roster, required because
    /// <c>BattleSimulation.CreateForTesting</c>'s three-argument overload
    /// rejects an injected ruleset whose roster disagrees with the registered
    /// entry for the scenario's combat preset. Only
    /// <see cref="WeaponId.Itak"/>'s profile is exercised; the other three
    /// weapons carry an inert placeholder solely to satisfy
    /// <see cref="CombatRuleset"/>'s "every roster weapon needs an attribute
    /// row" and "a one-handed weapon needs a paired row" construction
    /// invariants. This mirrors
    /// <see cref="ComboChainTests"/>' own helper of the same shape, with the
    /// exercised weapon moved from Kampilan to Itak.
    /// </summary>
    private static CombatRuleset BuildRuleset(WeaponProfile itakProfile)
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
            AttackCooldownTicks: AttackCooldownTicks);

        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(placeholderProfile),
            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(placeholderProfile),
            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                placeholderProfile,
                placeholderProfile),
            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                itakProfile,
                itakProfile),
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
            clashProfile: ClashProfile.Neutral);
    }
}
