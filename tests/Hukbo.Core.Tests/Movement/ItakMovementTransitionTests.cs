using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Itak footwork transitions, in two layers. First the disengagement
/// hysteresis boundaries of design section 9.2, called directly on
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> with the
/// actual solo and shielded Itak profile thresholds rather than the
/// loadout-agnostic tuning of <see cref="FootworkPhaseRulesTests"/>: entry
/// equality enters, release equality leaves, a ratio strictly between the
/// two thresholds preserves the previous state on both sides of the shield
/// split, and zero living enemies never enters or remains under either row.
/// Then the attack-commit lifecycle of design sections 9.5 and 9.6,
/// observed through whole ticks on Itak duels: an accepted attack changes
/// nothing about its own tick's movement, solo <c>Commit</c> runs exactly
/// two ticks into exactly two of <c>Recover</c> and the shielded row three
/// and three, the committed pace and turn caps bind at the Itak values, no
/// committed or recovering warrior reverses facing in one tick, and an
/// attack accepted during <c>Recover</c> interrupts it with a fresh
/// <c>Commit</c>. Every threshold and duration consumed here is a
/// provisional reconstruction — gameplay tuning, not a historical
/// measurement (docs/research/movement/itak.md, section 7).
/// </summary>
public sealed class ItakMovementTransitionTests
{
    /// <summary>
    /// The solo Itak (<c>IT</c>) row: disengage entry 12,500 basis points,
    /// release 10,000 — an entry ratio of 1.25 enemies per ally and a
    /// release of 1.0.
    /// </summary>
    private static readonly LoadoutMovementProfile SoloRow =
        ItakMovementProfile.Row;

    /// <summary>
    /// The shielded Itak (<c>IS</c>) row: disengage entry 15,000 basis
    /// points, release 11,000 — an entry ratio of 1.5 enemies per ally and
    /// a release of 1.1.
    /// </summary>
    private static readonly LoadoutMovementProfile ShieldedRow =
        TallHardwoodMovementProfiles.ItakRow;

    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        LoadoutMovementProfile profile,
        FootworkPhase priorPhase = FootworkPhase.None,
        int supportAllies = 1,
        int supportEnemies = 0,
        bool hasTarget = false,
        bool targetAtOrInsidePreferredDistance = false) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive: true,
            priorPhase,
            priorTicksRemaining: 0,
            TacticalPosture.Hold,
            supportAllies,
            supportEnemies,
            profile.DisengageEnemyToAllyBasisPoints,
            profile.ReengageEnemyToAllyBasisPoints,
            profile.RecoveryTicks,
            hasTarget,
            targetAtOrInsidePreferredDistance);

    // ----- Solo Itak hysteresis (entry 12500, release 10000) -----

    /// <summary>
    /// Entry equality enters: five enemies against four allies sits exactly
    /// on the solo entry threshold, 5 &#215; 10000 = 4 &#215; 12500.
    /// </summary>
    [Fact]
    public void TheSoloItakEntryEqualityEntersDisengagement() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(SoloRow, supportAllies: 4, supportEnemies: 5));

    /// <summary>
    /// Release equality leaves: four enemies against four allies sits
    /// exactly on the solo release threshold, 4 &#215; 10000 =
    /// 4 &#215; 10000, so an already-disengaging agent falls through — here
    /// to <c>None</c>, having no target and a <c>Hold</c> posture.
    /// </summary>
    [Fact]
    public void TheSoloItakReleaseEqualityLeavesDisengagement() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 4,
                supportEnemies: 4));

    /// <summary>
    /// Six enemies against five allies is ratio 1.2, strictly between the
    /// solo release of 1.0 (60000 &gt; 50000, so a disengaging agent
    /// remains) and the solo entry of 1.25 (60000 &lt; 62500, so an engaged
    /// one does not enter).
    /// </summary>
    [Fact]
    public void StrictlyBetweenTheSoloItakThresholdsADisengagingAgentRemains() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 5,
                supportEnemies: 6));

    [Fact]
    public void StrictlyBetweenTheSoloItakThresholdsAnEngagedAgentDoesNotEnter() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Engage,
                supportAllies: 5,
                supportEnemies: 6,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- Shielded Itak hysteresis (entry 15000, release 11000) -----

    /// <summary>
    /// Entry equality enters: three enemies against two allies sits exactly
    /// on the shielded entry threshold, 3 &#215; 10000 = 2 &#215; 15000.
    /// </summary>
    [Fact]
    public void TheShieldedItakEntryEqualityEntersDisengagement() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(ShieldedRow, supportAllies: 2, supportEnemies: 3));

    /// <summary>
    /// Release equality leaves: eleven enemies against ten allies sits
    /// exactly on the shielded release threshold, 11 &#215; 10000 =
    /// 10 &#215; 11000. The shielded release of 1.1 sits above the solo
    /// 1.0, so this same count keeps a solo warrior disengaging while the
    /// shielded one falls through (design section 13.3).
    /// </summary>
    [Fact]
    public void TheShieldedItakReleaseEqualityLeavesDisengagement() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 10,
                supportEnemies: 11));

    /// <summary>
    /// Six enemies against five allies is ratio 1.2, strictly between the
    /// shielded release of 1.1 (60000 &gt; 55000, so a disengaging agent
    /// remains) and the shielded entry of 1.5 (60000 &lt; 75000, so an
    /// engaged one does not enter).
    /// </summary>
    [Fact]
    public void StrictlyBetweenTheShieldedItakThresholdsADisengagingAgentRemains() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 5,
                supportEnemies: 6));

    [Fact]
    public void StrictlyBetweenTheShieldedItakThresholdsAnEngagedAgentDoesNotEnter() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Engage,
                supportAllies: 5,
                supportEnemies: 6,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- The zero-enemy rule under both Itak rows -----

    /// <summary>
    /// Design section 9.2's zero-enemy rule holds on the Itak thresholds
    /// with no special case: zero living enemies never enters and never
    /// remains in disengagement under either row, on the ratio arithmetic
    /// alone.
    /// </summary>
    [Theory]
    [InlineData(false, FootworkPhase.None)]
    [InlineData(false, FootworkPhase.Disengage)]
    [InlineData(true, FootworkPhase.None)]
    [InlineData(true, FootworkPhase.Disengage)]
    public void ZeroEnemiesNeverEntersAndNeverRemainsUnderEitherItakRow(
        bool shielded,
        FootworkPhase priorPhase) =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                shielded ? ShieldedRow : SoloRow,
                priorPhase: priorPhase,
                supportAllies: 1,
                supportEnemies: 0));

    // ----- The attack-commit lifecycle (design 9.5, 9.6) -----

    // With body radius 512 and movement speed 512, the solo Itak row
    // resolves to: desired forward pace 512 (512 * 10000 / 10000),
    // backward 414, committed 204, acceleration step 358, deceleration
    // step 409, preferred distance 5632 against an Itak
    // (5120 * 11000 / 10000). The shielded row: backward 363, committed
    // 179, acceleration step 332, preferred distance 5120 against a
    // shielded Itak.

    /// <summary>
    /// Attack acceptance does not alter its own tick's movement: two
    /// body-contact solo Itak duels, identical except that the second pins
    /// both cooldowns high before the tick, land on identical positions,
    /// facings, and paces at the end of that tick. Only the footwork phase
    /// differs — the accepted attackers entered <c>Commit</c> with the solo
    /// two-tick duration while the pinned pair stayed in <c>Engage</c> —
    /// and the attack resolution itself ran unchanged: two attack events
    /// and both cooldowns loaded.
    /// </summary>
    [Fact]
    public void AnAcceptedAttackDoesNotAlterItsOwnTicksMovementOrFacing()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var attacking = BattleSimulation.CreateForTesting(
            scenario, west, east);
        var pinnedWest = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var pinnedEast = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var pinned = BattleSimulation.CreateForTesting(
            scenario, pinnedWest, pinnedEast);

        pinnedWest.AttackCooldownRemaining = 100;
        pinnedEast.AttackCooldownRemaining = 100;
        attacking.AdvanceOneTick();
        pinned.AdvanceOneTick();

        Assert.Equal(pinnedWest.XRaw, west.XRaw);
        Assert.Equal(pinnedWest.YRaw, west.YRaw);
        Assert.Equal(pinnedWest.Facing, west.Facing);
        Assert.Equal(pinnedWest.MovementPaceRaw, west.MovementPaceRaw);
        Assert.Equal(pinnedEast.XRaw, east.XRaw);
        Assert.Equal(pinnedEast.YRaw, east.YRaw);
        Assert.Equal(pinnedEast.Facing, east.Facing);
        Assert.Equal(pinnedEast.MovementPaceRaw, east.MovementPaceRaw);

        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(SoloRow.CommitmentTicks, west.FootworkTicksRemaining);
        Assert.Equal(FootworkPhase.Engage, pinnedWest.FootworkPhase);

        Assert.Equal(
            2,
            attacking.LastEvents.Count(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack));
        Assert.DoesNotContain(
            pinned.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack);
        Assert.True(west.AttackCooldownRemaining > 0);
        Assert.True(east.AttackCooldownRemaining > 0);
    }

    /// <summary>
    /// The solo Itak attack lifecycle on a body-contact duel: the accepted
    /// attack enters <c>Commit</c> at the row's two-tick duration counting
    /// its entry tick, <c>Commit</c> decrements once and expires into
    /// <c>Recover</c> at the row's two-tick recovery, and the tick after
    /// recovery expires resolves <c>Engage</c> against the target still
    /// inside the preferred distance. Cooldowns are pinned high between
    /// ticks so no second attack re-enters <c>Commit</c> mid-sequence.
    /// </summary>
    [Fact]
    public void TheSoloItakLifecycleCommitsTwoTicksAndRecoversTwo()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(2, west.FootworkTicksRemaining);
        Assert.Equal(FootworkPhase.Commit, east.FootworkPhase);
        Assert.Equal(2, east.FootworkTicksRemaining);

        var expected = new (FootworkPhase Phase, int Ticks)[]
        {
            (FootworkPhase.Commit, 1),
            (FootworkPhase.Recover, 2),
            (FootworkPhase.Recover, 1),
            (FootworkPhase.Engage, 0),
        };
        foreach (var (phase, ticks) in expected)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            Assert.Equal(phase, west.FootworkPhase);
            Assert.Equal(ticks, west.FootworkTicksRemaining);
        }
    }

    /// <summary>
    /// The shielded Itak lifecycle on the same body-contact duel runs the
    /// tall-hardwood durations instead: three ticks of <c>Commit</c> into
    /// three of <c>Recover</c>, then <c>Engage</c>.
    /// </summary>
    [Fact]
    public void TheShieldedItakLifecycleCommitsThreeTicksAndRecoversThree()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(
            1, factionId: 0, 92_160, 51_200, scenario, ShieldedItak);
        var east = CreateAgent(
            2, factionId: 1, 93_184, 51_200, scenario, ShieldedItak);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(3, west.FootworkTicksRemaining);

        var expected = new (FootworkPhase Phase, int Ticks)[]
        {
            (FootworkPhase.Commit, 2),
            (FootworkPhase.Commit, 1),
            (FootworkPhase.Recover, 3),
            (FootworkPhase.Recover, 2),
            (FootworkPhase.Recover, 1),
            (FootworkPhase.Engage, 0),
        };
        foreach (var (phase, ticks) in expected)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            Assert.Equal(phase, west.FootworkPhase);
            Assert.Equal(ticks, west.FootworkTicksRemaining);
        }
    }

    /// <summary>
    /// An attack accepted mid-<c>Recover</c> interrupts it with a fresh
    /// <c>Commit</c> at the solo Itak two-tick duration: movement recovery
    /// never suppresses an attack the combat gates accepted, and the
    /// accepted attack still emits its event.
    /// </summary>
    [Fact]
    public void AnAttackAcceptedDuringRecoverInterruptsWithAFreshItakCommit()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);
        west.FootworkPhase = FootworkPhase.Recover;
        west.FootworkTicksRemaining = 5;

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(2, west.FootworkTicksRemaining);
        Assert.Contains(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Attack &&
                battleEvent.SourceEntityId == west.EntityId);
    }

    /// <summary>
    /// The committed pace cap binds at the Itak value: a warrior
    /// mid-<c>Commit</c> at full pace 512 wants min(band 10000, committed
    /// 4000) = 204 raw, and the 409 deceleration step reaches that cap in
    /// one tick — each tick's displacement equal to the resulting pace,
    /// because the route is the direct east line toward the threat.
    /// </summary>
    [Fact]
    public void CommitCapsTheItakPaceAtTheCommittedBand()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 61_440, 51_200, scenario);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        actor.MovementPaceRaw = 512;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();
        Assert.Equal(51_404, actor.XRaw);
        Assert.Equal(204, actor.MovementPaceRaw);

        simulation.AdvanceOneTick();
        Assert.Equal(51_608, actor.XRaw);
        Assert.Equal(204, actor.MovementPaceRaw);
    }

    /// <summary>
    /// No instant full reverse while committed: a threat standing directly
    /// behind demands an eight-sector half turn, and a mid-<c>Commit</c>
    /// Itak — whose ordinary budget is two sectors — turns exactly one
    /// sector per tick, clockwise on the exact eight-step tie, never
    /// jumping more.
    /// </summary>
    [Fact]
    public void CommitCapsTheItakTurnToOneSectorAgainstAFullReversal()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 30_720, 51_200, scenario);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        // V6 spawns faction 0 facing East; the threat stands due West.
        Assert.Equal(Facing16.East, actor.Facing);

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.EastSouthEast, actor.Facing);
        Assert.Equal(
            1, FacingRules.SectorSeparation(Facing16.East, actor.Facing));

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.SouthEast, actor.Facing);
        Assert.Equal(
            1,
            FacingRules.SectorSeparation(
                Facing16.EastSouthEast, actor.Facing));
    }

    /// <summary>
    /// <c>Recover</c> never reverses facing either: the recovering Itak
    /// keeps facing East toward the threat while backing away west at the
    /// backward band — the first-tick pace is the 358 acceleration step
    /// toward the 414 backward cap, an exact 358-unit step west.
    /// </summary>
    [Fact]
    public void RecoverRetainsTheItakFacingWhileBackingAway()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 71_680, 51_200, scenario);
        actor.FootworkPhase = FootworkPhase.Recover;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(50_842, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(Facing16.East, actor.Facing);
        Assert.Equal(358, actor.MovementPaceRaw);
        Assert.Equal(FootworkPhase.Recover, actor.FootworkPhase);
        Assert.Equal(4, actor.FootworkTicksRemaining);
    }

    // ----- Helpers -----

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        CreateAgent(entityId, factionId, xRaw, yRaw, scenario, SoloItak);

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout) =>
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
            loadout);
}
