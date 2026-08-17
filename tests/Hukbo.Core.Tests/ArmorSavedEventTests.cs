using System.Linq;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Coverage for <see cref="BattleEventKind.ArmorSaved"/>: a blow whose raw,
/// unmitigated damage would have reduced a target's <c>HitPoints</c> to zero
/// or below, but whose armor-mitigated damage did not, must produce exactly
/// one <see cref="BattleEventKind.ArmorSaved"/> event alongside that
/// target's <see cref="BattleEventKind.Damage"/> event for the tick. A blow
/// that is not lethal before mitigation, a target with no armor mitigation
/// at all, and a blow that is still lethal after mitigation must each
/// produce none.
/// </summary>
/// <remarks>
/// The controlled ruleset here is built the same way
/// <c>ArmorDamageApplicationTests.BuildChestOnlyArmoredRuleset</c> builds
/// one: every Kampilan blow is forced to resolve at the chest by giving the
/// chest all of the weapon's target weight, and the armor mitigation table
/// is zero everywhere except a single (armor, body part) cell this file
/// controls directly, so the mitigation percentage applied to any given
/// blow is exact and never a matter of which body part the roll happened to
/// pick.
/// </remarks>
public sealed class ArmorSavedEventTests
{
    private static readonly ClashProfile Neutral = ClashProfile.Neutral;

    [Fact]
    public void LandedBlow_LethalUnmitigated_SurvivesMitigated_EmitsOneArmorSavedEvent()
    {
        const int RawDamage = 50;
        const int MitigationBasisPoints = 5_000; // 50%, so mitigated damage is 25.
        const int TargetStartingHitPoints = 40; // 40 - 50 <= 0 (raw kill); 40 - 25 > 0 (survives).

        var simulation = RunOneChestBlow(
            RawDamage, MitigationBasisPoints, TargetStartingHitPoints);

        var armorSavedEvents = simulation.LastEvents
            .Where(battleEvent => battleEvent.Kind == BattleEventKind.ArmorSaved)
            .ToArray();

        var armorSaved = Assert.Single(armorSavedEvents);
        Assert.Equal(RawDamage - 25, armorSaved.Value);
        Assert.Null(armorSaved.TargetEntityId);

        // A Damage event for the same target must still be present: armor
        // identity narrates a landed blow, it does not silence it.
        Assert.Contains(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.Damage &&
                battleEvent.SourceEntityId == armorSaved.SourceEntityId);
    }

    [Fact]
    public void LandedBlow_NotLethalEvenUnmitigated_EmitsNoArmorSavedEvent()
    {
        const int RawDamage = 10;
        const int MitigationBasisPoints = 5_000; // 50%, so mitigated damage is 5.
        const int TargetStartingHitPoints = 40; // 40 - 10 > 0: never lethal to begin with.

        var simulation = RunOneChestBlow(
            RawDamage, MitigationBasisPoints, TargetStartingHitPoints);

        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.ArmorSaved);
    }

    [Fact]
    public void LandedBlow_LethalUnmitigated_NoMitigationTableEntry_EmitsNoArmorSavedEvent()
    {
        const int RawDamage = 50;
        const int NoMitigation = 0; // The armor blocks nothing at this body part.
        const int TargetStartingHitPoints = 40; // 40 - 50 <= 0, and mitigation changes nothing.

        var simulation = RunOneChestBlow(
            RawDamage, NoMitigation, TargetStartingHitPoints);

        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.ArmorSaved);
    }

    [Fact]
    public void LandedBlow_StillLethalAfterMitigation_EmitsNoArmorSavedEvent()
    {
        const int RawDamage = 50;
        const int MitigationBasisPoints = 1_000; // 10%, so mitigated damage is 45.
        const int TargetStartingHitPoints = 40; // 40 - 50 <= 0 and 40 - 45 <= 0: still lethal.

        var simulation = RunOneChestBlow(
            RawDamage, MitigationBasisPoints, TargetStartingHitPoints);

        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.Kind == BattleEventKind.ArmorSaved);
    }

    /// <summary>
    /// Places one attacker and one target exactly
    /// <see cref="Scenario.AttackRangeRaw"/> apart under a chest-only,
    /// clash-neutral Kampilan ruleset and advances one tick.
    /// </summary>
    private static BattleSimulation RunOneChestBlow(
        int rawDamage,
        int mitigationBasisPointsAtChest,
        int targetStartingHitPoints)
    {
        var scenario = new Scenario(
            Seed: 1, MapWidth: 2_000, MapHeight: 2_000, AgentsPerFaction: 1,
            TickRate: 20, TickLimit: 1_000)
        {
            MaximumHitPoints = targetStartingHitPoints,
            PerceptionRangeRaw = 5_000 * FixedPoint.Scale,
            AttackRangeRaw = 20 * FixedPoint.Scale,
            BodyRadiusRaw = 4 * FixedPoint.Scale,
            MovementSpeedRaw = 3 * FixedPoint.Scale,
            AttackCooldownTicks = 5,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV3,
        };

        var kampilanProfile = new WeaponProfile(
            DamagePerAttack: rawDamage,
            AttackRangeRaw: 20 * FixedPoint.Scale,
            AttackCooldownTicks: 5);
        var rules = BuildChestOnlyArmoredRuleset(
            kampilanProfile, Neutral, mitigationBasisPointsAtChest);

        var attackerLoadout = new CombatLoadout(WeaponId.Kampilan, ArmorId.None, ShieldId.None);
        var targetLoadout = new CombatLoadout(WeaponId.Kalis, ArmorId.RigidCuirass, ShieldId.None);

        var attacker = new AgentState(
            1, 0, 0, 0,
            targetStartingHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, rawDamage, scenario.AttackCooldownTicks,
            attackerLoadout);
        var target = new AgentState(
            2, 1, scenario.AttackRangeRaw, 0,
            targetStartingHitPoints, scenario.MovementSpeedRaw, scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw, 0, scenario.AttackCooldownTicks,
            targetLoadout);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, attacker, target);
        simulation.AdvanceOneTick();
        return simulation;
    }

    /// <summary>
    /// A structurally minimal <see cref="CombatRuleset"/>, built the same
    /// way <c>ArmorDamageApplicationTests.BuildChestOnlyArmoredRuleset</c>
    /// builds one: the Kampilan target-weight table gives every weight to
    /// <see cref="BodyPart.Chest"/> so every landed Kampilan blow resolves
    /// there deterministically, and the armor mitigation table is zero
    /// everywhere except <see cref="ArmorId.RigidCuirass"/> at
    /// <see cref="BodyPart.Chest"/>, which is
    /// <paramref name="rigidCuirassChestBasisPoints"/>. The roster is V3's
    /// registered four-loadout roster, required because
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
