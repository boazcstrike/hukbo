using System;
using System.Linq;
using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// The seams <c>ArenaGame</c> wires the damage card through, per the weapon
/// damage tally design's sections 2 and 4: the hover spotlight predicate, the
/// <c>T</c> command's place in the enum, and the tally's combat ruleset across
/// a reset. Pure: no <c>ArenaGame</c>, no graphics device, no sprite batch.
/// </summary>
public sealed class DamageTallyWiringTests
{
    private const int BlowDamage = 100;

    private static readonly CombatRuleset ArmouredRules = CombatPresetRegistry.Get(
        CombatPresetId.PrecolonialPhilippinesV8);

    private static readonly CombatRuleset UnarmouredRules = CombatPresetRegistry.Get(
        CombatPresetId.PrecolonialPhilippinesV1);

    private static readonly DamageTallyHover Hover = new(
        FactionId: 0,
        WeaponId.Kampilan,
        ShieldId.TallHardwood);

    [Fact]
    public void ToggleDamageTallyIsAppendedAfterEveryExistingCommand()
    {
        Assert.Equal(
            (int)ClientCommand.ToggleInterpolation + 1,
            (int)ClientCommand.ToggleDamageTally);
        Assert.Equal(
            ClientCommand.ToggleDamageTally,
            Enum.GetValues<ClientCommand>().Max());
    }

    [Fact]
    public void SpotlightRingsALivingAgentOfTheHoveredFactionWeaponAndShield()
    {
        Assert.True(DamageTallySpotlight.Matches(Hover, Agent()));
    }

    [Fact]
    public void SpotlightRingsNobodyWhenNoRowIsHovered()
    {
        Assert.False(DamageTallySpotlight.Matches(null, Agent()));
    }

    [Fact]
    public void SpotlightSkipsAFallenAgentOfTheHoveredRow()
    {
        Assert.False(DamageTallySpotlight.Matches(Hover, Agent(isAlive: false)));
    }

    [Theory]
    [InlineData(1, WeaponId.Kampilan, ShieldId.TallHardwood)]
    [InlineData(0, WeaponId.Wasay, ShieldId.TallHardwood)]
    [InlineData(0, WeaponId.Kampilan, ShieldId.None)]
    public void SpotlightSkipsAnAgentThatDiffersInFactionWeaponOrShield(
        int faction,
        WeaponId weapon,
        ShieldId shield)
    {
        Assert.False(
            DamageTallySpotlight.Matches(Hover, Agent(faction, weapon, shield)));
    }

    [Fact]
    public void ResetWithADifferentRulesetMakesTheTallyApplyThatRulesetsArmour()
    {
        var armour = ArmourThatMitigates(ArmouredRules);
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            combatRules: ArmouredRules);

        coordinator.ResetFor(ClientCommand.NextRound, UnarmouredRules);
        IngestOneBlow(coordinator, armour);

        Assert.Equal(BlowDamage, coordinator.DamageTally.GetFactionDamage(0));
    }

    [Fact]
    public void ResetBackToAnArmouredRulesetMitigatesAgain()
    {
        var armour = ArmourThatMitigates(ArmouredRules);
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            combatRules: UnarmouredRules);

        coordinator.ResetFor(ClientCommand.FullReset, ArmouredRules);
        IngestOneBlow(coordinator, armour);

        Assert.Equal(
            ArmouredRules.ApplyArmorMitigation(BlowDamage, armour, BodyPart.Chest),
            coordinator.DamageTally.GetFactionDamage(0));
        Assert.True(coordinator.DamageTally.GetFactionDamage(0) < BlowDamage);
    }

    [Fact]
    public void ResetWithoutARulesetKeepsTheCurrentOneAndClearsTheTally()
    {
        var armour = ArmourThatMitigates(ArmouredRules);
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            combatRules: UnarmouredRules);
        IngestOneBlow(coordinator, armour);
        Assert.Equal(BlowDamage, coordinator.DamageTally.GetFactionDamage(0));

        coordinator.ResetFor(ClientCommand.NextRound);
        Assert.Equal(0, coordinator.DamageTally.GetFactionDamage(0));

        IngestOneBlow(coordinator, armour);
        Assert.Equal(BlowDamage, coordinator.DamageTally.GetFactionDamage(0));
    }

    [Theory]
    [InlineData((int)ClientCommand.NextRound)]
    [InlineData((int)ClientCommand.FullReset)]
    public void EveryRoundResetClearsTheTallyEvenWhenTheRulesetIsSupplied(
        int commandValue)
    {
        var armour = ArmourThatMitigates(ArmouredRules);
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            combatRules: ArmouredRules);
        IngestOneBlow(coordinator, armour);
        Assert.NotEqual(0, coordinator.DamageTally.GetFactionDamage(0));

        coordinator.ResetFor((ClientCommand)commandValue, ArmouredRules);

        Assert.Equal(0, coordinator.DamageTally.GetFactionDamage(0));
        var buffer = new WeaponDamageTallyRow[WeaponDamageTally.MaximumRowsPerFaction];
        Assert.Equal(0, coordinator.DamageTally.GetRows(0, buffer));
    }

    // The test is only meaningful for an armour the ruleset really reduces
    // damage against, so the choice is asserted rather than assumed.
    private static ArmorId ArmourThatMitigates(CombatRuleset rules)
    {
        var armour = Enum.GetValues<ArmorId>().First(candidate =>
            rules.ApplyArmorMitigation(BlowDamage, candidate, BodyPart.Chest) < BlowDamage);
        Assert.Equal(
            BlowDamage,
            UnarmouredRules.ApplyArmorMitigation(BlowDamage, armour, BodyPart.Chest));
        return armour;
    }

    private static void IngestOneBlow(PresentationCoordinator coordinator, ArmorId targetArmour)
    {
        AgentView[] agents =
        [
            Agent(entityId: 1, faction: 0, WeaponId.Kampilan, ShieldId.None),
            Agent(entityId: 2, faction: 1, WeaponId.Kampilan, ShieldId.None, targetArmour),
        ];

        coordinator.IngestTick(
            [BattleEvent.Attack(
                sequence: 1,
                tick: 1,
                sourceEntityId: 1,
                targetEntityId: 2,
                damage: BlowDamage,
                factionId: 0,
                weapon: WeaponId.Kampilan,
                shield: ShieldId.None,
                hitLocation: BodyPart.Chest,
                resolution: AttackResolution.Landed)],
            agents,
            default);
    }

    private static AgentView Agent(
        int faction = 0,
        WeaponId weapon = WeaponId.Kampilan,
        ShieldId shield = ShieldId.TallHardwood,
        bool isAlive = true) =>
        Agent(entityId: 1, faction, weapon, shield, ArmorId.None, isAlive);

    private static AgentView Agent(
        ulong entityId,
        int faction,
        WeaponId weapon,
        ShieldId shield,
        ArmorId armour = ArmorId.None,
        bool isAlive = true) =>
        new(
            entityId,
            faction,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: isAlive,
            Loadout: new CombatLoadout(weapon, armour, shield));
}
