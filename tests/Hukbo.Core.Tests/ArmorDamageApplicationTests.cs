using System.Linq;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Coverage for armor damage mitigation as wired into
/// <see cref="BattleSimulation"/>'s melee and projectile damage-application
/// call sites, and into <c>ResolveComboTransition</c>'s lethality
/// prediction. The mitigation table itself
/// (<see cref="CombatRuleset.ResolveArmorMitigation"/>,
/// <see cref="CombatRuleset.ApplyArmorMitigation"/>) and the fielded preset
/// (<see cref="CombatPresetId.PrecolonialPhilippinesV7"/>) are built and
/// owned elsewhere; this file only proves the simulation actually applies
/// them where damage is written.
/// </summary>
public sealed class ArmorDamageApplicationTests
{
    /// <summary>
    /// Every accepted attack resolves <see cref="AttackResolution.Landed"/>
    /// with certainty under this profile — the same sanctioned pattern
    /// <c>BattleSimulationTests.PresetWith</c> and <c>ComboChainTests</c>
    /// use to make a battle deterministic without predicting a clash roll.
    /// </summary>
    private static readonly ClashProfile Neutral = ClashProfile.Neutral;

    [Fact]
    public void ArmorMitigation_ReducesDamageTaken_ComparedToNoArmor_OnAChestHit()
    {
        var rules = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV7)
            .WithClashProfile(Neutral);

        // Hit location depends only on the ruleset's weapon/shield weight
        // tables plus seed/tick/entity-ids — never on armor (see
        // HitLocationResolver.Resolve) — so a chest-resolving tuple found
        // with armor held irrelevant here still resolves to Chest once the
        // real simulation runs the same tuple with either target armor.
        var probeAttacker = new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None);
        var probeDefender = new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None);

        ulong sourceId = 0;
        ulong targetId = 0;
        for (ulong candidateSource = 1; candidateSource <= 30 && sourceId == 0; candidateSource++)
        {
            for (ulong candidateTarget = 1; candidateTarget <= 30; candidateTarget++)
            {
                if (candidateTarget == candidateSource)
                {
                    continue;
                }

                var part = HitLocationResolver.Resolve(
                    rules, probeAttacker, probeDefender, seed: 1, tick: 1, candidateSource, candidateTarget);
                if (part == BodyPart.Chest)
                {
                    sourceId = candidateSource;
                    targetId = candidateTarget;
                    break;
                }
            }
        }

        Assert.True(
            sourceId != 0,
            "No (sourceId, targetId) pair in [1,30]x[1,30] resolved to Chest " +
            "at seed 1, tick 1, under V7's Kampilan-vs-unshielded table; " +
            "widen the search range.");

        var scenario = new Scenario(
            Seed: 1, MapWidth: 500, MapHeight: 500, AgentsPerFaction: 1,
            TickRate: 20, TickLimit: 100)
        {
            MaximumHitPoints = 5_000,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV7,
        };

        var unarmoredDamageTaken = RunOneAttackAndMeasureDamageTaken(
            scenario, rules, sourceId, targetId,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None));
        var armoredDamageTaken = RunOneAttackAndMeasureDamageTaken(
            scenario, rules, sourceId, targetId,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.RigidCuirass, ShieldId.None));

        Assert.True(
            armoredDamageTaken < unarmoredDamageTaken,
            $"Expected a RigidCuirass chest hit ({armoredDamageTaken}) to " +
            $"take strictly less damage than no armor ({unarmoredDamageTaken}).");
    }

    [Fact]
    public void ApplyArmorMitigation_UnderAPresetWithNoMitigationTable_LeavesDamageUnchanged()
    {
        var rules = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV6)
            .WithClashProfile(Neutral);

        Assert.False(rules.HasArmorMitigation);

        var scenario = new Scenario(
            Seed: 1, MapWidth: 500, MapHeight: 500, AgentsPerFaction: 1,
            TickRate: 20, TickLimit: 100)
        {
            MaximumHitPoints = 5_000,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV6,
        };

        const int DamagePerAttack = 37;
        var damageTaken = RunOneAttackAndMeasureDamageTaken(
            scenario, rules, sourceId: 1, targetId: 2,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            damagePerAttack: DamagePerAttack);

        Assert.Equal(DamagePerAttack, damageTaken);
    }

    [Fact]
    public void ResolveArmorMitigation_IsZeroForHead_ForEveryFieldedArmor()
    {
        var rules = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV7);

        foreach (var armor in Enum.GetValues<ArmorId>())
        {
            Assert.Equal(0, rules.ResolveArmorMitigation(armor, BodyPart.Head));
            Assert.Equal(100, rules.ApplyArmorMitigation(100, armor, BodyPart.Head));
        }
    }

    /// <summary>
    /// Reproduces the bug Task 3 fixed: an unmitigated lethality prediction
    /// in <c>ResolveComboTransition</c> would call a blow lethal
    /// (<c>target.HitPoints - source.DamagePerAttack &lt;= 0</c>) even
    /// though the mitigated damage the main damage-application loop
    /// actually writes leaves the target alive. Chain state is set directly
    /// on the constructed <see cref="AgentState"/> — the same pattern
    /// <c>ComboChainTests.ContinuationRoll_WhenItSucceedsBelowTheCap_...</c>
    /// uses — so this exercises the continuation branch (the one
    /// <c>killedByThisBlow</c> guards) on the very first tick, without
    /// needing a real opening blow first.
    /// </summary>
    [Fact]
    public void ComboContinuation_WhenMitigatedDamageDoesNotKill_TreatsTheTargetAsSurviving()
    {
        const int RawLethalDamagePerAttack = 50;
        const int MitigationBasisPointsAtChest = 5_000; // 50%, so mitigated damage is 25.
        const int TargetStartingHitPoints = 40; // 40 - 50 <= 0 (raw kill); 40 - 25 > 0 (survives).

        var scenario = BuildComboScenario(TargetStartingHitPoints);
        var kampilanProfile = new WeaponProfile(
            DamagePerAttack: RawLethalDamagePerAttack,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: 5,
            ComboOpenChanceBasisPoints: 0,
            ComboContinueChanceBasisPoints: ClashProfile.BasisPointScale,
            ComboMaxSteps: 3,
            ComboCooldownTicks: 2);
        var rules = BuildChestOnlyArmoredRuleset(
            kampilanProfile, Neutral, MitigationBasisPointsAtChest);

        var attackerLoadout = new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None);
        var targetLoadout = new CombatLoadout(WeaponId.Kalis, ArmorId.RigidCuirass, ShieldId.None);

        var attacker = new AgentState(
            1, 0, 0, 0,
            scenario.MaximumHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, RawLethalDamagePerAttack, scenario.AttackCooldownTicks,
            attackerLoadout,
            // ComboMaxSteps caps at Math.Min(source.Level, weaponProfile.ComboMaxSteps);
            // the weapon's own cap here is 3, and AgentState.Level defaults to
            // 1 when not passed, which would silently collapse maxSteps to 1
            // and make thisPosition compute 0 (an invalid combo position) for
            // a chain already two steps deep. Matches
            // ComboChainTests.BuildAgent's fixed level: 3.
            level: 3);
        var target = new AgentState(
            2, 1, 2 * FixedPoint.Scale, 0,
            TargetStartingHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, 0, scenario.AttackCooldownTicks,
            targetLoadout);

        attacker.ComboStepsRemaining = 2;
        attacker.ComboTargetEntityId = target.EntityId;

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);
        simulation.AdvanceOneTick();

        // Mitigated damage (25) does not kill, so the chain must still be
        // alive here: continuationSucceeded (Always) && thisPosition (2) <
        // maxSteps (3) && !killedByThisBlow. Before Task 3's fix,
        // killedByThisBlow read the raw DamagePerAttack (50 >= 40) and
        // reset the chain to zero here regardless of the armor.
        Assert.True(
            attacker.ComboStepsRemaining > 0,
            "Expected the combo chain to survive a blow the target's " +
            "armor mitigates below lethal, but it was reset to zero.");
        Assert.Equal(target.EntityId, attacker.ComboTargetEntityId);
        Assert.Equal(
            TargetStartingHitPoints - 25,
            target.HitPoints);
        Assert.True(target.HitPoints > 0, "Expected the target to survive the mitigated blow.");
    }

    /// <summary>
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> gates a
    /// call to <c>LoadoutCompositionCounts.Add</c>, which throws for any
    /// equipment triple whose armor is not <see cref="ArmorId.LightOrganic"/>
    /// (see <c>LoadoutCompositionCounts.Add</c>'s six-case switch). V7 fields
    /// no <see cref="ArmorId.LightOrganic"/> row at all — every roster entry
    /// carries one of the five newer armors — so pairing V7 with a movement
    /// preset that turns equipment-relative footwork on would hit that throw
    /// on the very first living agent. This proves, by actually running the
    /// combination for the full 10,000 ticks rather than trusting a comment,
    /// that pairing V7 with <see cref="MovementPresetId.EvasiveFootworkV14"/>
    /// specifically does not: reading
    /// <c>MovementPresetRegistry.EvasiveFootworkV14Ruleset</c> shows it is
    /// constructed with <c>usesEquipmentRelativeFootwork: false</c>, so
    /// <c>BattleSimulation</c>'s <c>derivesLocalContext</c> flag is false
    /// under this preset and <c>LoadoutCompositionCounts.Add</c> is never
    /// called at all — confirmed here by running it, not by reading the flag
    /// alone. (<see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> is
    /// the one preset that does register the flag true, and pairing that one
    /// with V7 would be expected to reproduce the throw; that combination is
    /// outside this task's assignment and is not exercised here.)
    /// </summary>
    [Fact]
    public void V7WithEvasiveFootworkV14_Advances10000TicksWithoutThrowing()
    {
        var scenario = new Scenario(
            Seed: 1, MapWidth: 500, MapHeight: 500, AgentsPerFaction: 4,
            TickRate: 20, TickLimit: 10_000)
        {
            MaximumHitPoints = 200,
            DamagePerAttack = 5,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV7,
            MovementPreset = MovementPresetId.EvasiveFootworkV14,
        };

        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 10_000; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Equal(10_000, simulation.Tick);
    }

    private static Scenario BuildComboScenario(int maximumHitPoints) =>
        new(
            Seed: 1, MapWidth: 2_000, MapHeight: 2_000, AgentsPerFaction: 1,
            TickRate: 20, TickLimit: 1_000)
        {
            MaximumHitPoints = maximumHitPoints,
            PerceptionRangeRaw = 5_000 * FixedPoint.Scale,
            AttackRangeRaw = 20 * FixedPoint.Scale,
            BodyRadiusRaw = 4 * FixedPoint.Scale,
            MovementSpeedRaw = 3 * FixedPoint.Scale,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV3,
        };

    /// <summary>
    /// Places an attacker at the origin and a target exactly
    /// <see cref="Scenario.AttackRangeRaw"/> away on the x-axis, advances one
    /// tick, and returns the target's hit-point loss. The attacker's own
    /// armor is irrelevant to damage taken and is fixed to
    /// <see cref="ArmorId.None"/>.
    /// </summary>
    private static int RunOneAttackAndMeasureDamageTaken(
        Scenario scenario,
        CombatRuleset rules,
        ulong sourceId,
        ulong targetId,
        CombatLoadout targetLoadout,
        int damagePerAttack = 1_000)
    {
        var attacker = new AgentState(
            sourceId, 0, 0, 0,
            scenario.MaximumHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, damagePerAttack, scenario.AttackCooldownTicks,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None));
        var target = new AgentState(
            targetId, 1, scenario.AttackRangeRaw, 0,
            scenario.MaximumHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, 0, scenario.AttackCooldownTicks,
            targetLoadout);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);
        simulation.AdvanceOneTick();

        return scenario.MaximumHitPoints - target.HitPoints;
    }

    /// <summary>
    /// A structurally minimal <see cref="CombatRuleset"/>, built the same
    /// way <c>ComboChainTests.BuildRuleset</c> builds one, but with two
    /// differences: the Kampilan target-weight table gives every weight to
    /// <see cref="BodyPart.Chest"/> (so every landed Kampilan blow resolves
    /// there deterministically, with no search needed) and it carries an
    /// armor mitigation table where every (armor, body part) cell is zero
    /// except <see cref="ArmorId.RigidCuirass"/> at
    /// <see cref="BodyPart.Chest"/>, which is
    /// <paramref name="rigidCuirassChestBasisPoints"/>. The roster is
    /// V3's registered four-loadout roster, required because
    /// <c>BattleSimulation.CreateForTesting</c>'s three-argument overload
    /// rejects a roster that disagrees with the scenario's registered preset.
    /// </summary>
    private static CombatRuleset BuildChestOnlyArmoredRuleset(
        WeaponProfile kampilanProfile,
        ClashProfile clashProfile,
        int rigidCuirassChestBasisPoints)
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV3)
            .Roster;

        var chestOnlyWeights = Enum.GetValues<BodyPart>()
            .Select(part => (part, part == BodyPart.Chest ? 1 : 0))
            .ToArray();
        var flatWeights = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1))
            .ToArray();
        var flatMultipliers = Enum.GetValues<BodyPart>()
            .Select(part => (part, 1_000))
            .ToArray();
        var chestOnlyProfile = new TargetWeightProfile(chestOnlyWeights);
        var flatProfile = new TargetWeightProfile(flatWeights);

        var placeholderProfile = new WeaponProfile(
            DamagePerAttack: 1,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: 5);

        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(kampilanProfile),
            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(placeholderProfile),
            [WeaponId.Kalis] = WeaponAttributes.OneHanded(placeholderProfile, placeholderProfile),
            [WeaponId.Itak] = WeaponAttributes.OneHanded(placeholderProfile, placeholderProfile),
        };

        var armorMitigation = new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>();
        foreach (var armor in Enum.GetValues<ArmorId>())
        {
            var byPart = new Dictionary<BodyPart, int>();
            foreach (var part in Enum.GetValues<BodyPart>())
            {
                byPart[part] =
                    armor == ArmorId.RigidCuirass && part == BodyPart.Chest
                        ? rigidCuirassChestBasisPoints
                        : 0;
            }

            armorMitigation[armor] = byPart;
        }

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV3,
            version: 1,
            generalTargets: flatProfile,
            weaponTargets: new Dictionary<WeaponId, TargetWeightProfile>
            {
                [WeaponId.Kampilan] = chestOnlyProfile,
                [WeaponId.Wasay] = flatProfile,
                [WeaponId.Kalis] = flatProfile,
                [WeaponId.Itak] = flatProfile,
            },
            armors: [ArmorId.LightOrganic, ArmorId.RigidCuirass],
            shieldMultipliers: new Dictionary<ShieldId, TargetWeightProfile>
            {
                [ShieldId.None] = new TargetWeightProfile(flatMultipliers),
            },
            roster: roster,
            weaponAttributes: weaponAttributes,
            clashProfile: clashProfile,
            armorMitigation: armorMitigation);
    }
}
