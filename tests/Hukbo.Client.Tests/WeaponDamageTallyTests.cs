using System;
using System.Linq;
using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// The weapon damage tally, per the weapon damage tally design's section 5.
/// Pure: no <c>ArenaGame</c>, no graphics device, no sprite batch.
/// </summary>
public sealed class WeaponDamageTallyTests
{
    private static readonly CombatRuleset Rules = CombatPresetRegistry.Get(
        CombatPresetId.PrecolonialPhilippinesV8);

    [Fact]
    public void LandedBlowAddsDamageAndABlow()
    {
        var tally = new WeaponDamageTally(Rules);

        tally.Ingest(
            [Attack(1, source: 1, target: 2, damage: 10)],
            [Agent(1, 0, WeaponId.Kampilan), Agent(2, 1, WeaponId.Kampilan)]);

        var row = Assert.Single(RowsOf(tally, 0));
        Assert.Equal(10, row.Damage);
        Assert.Equal(1, row.BlowsLanded);
        Assert.Equal(10, tally.GetFactionDamage(0));
        Assert.Equal(0, tally.GetFactionDamage(1));
    }

    [Fact]
    public void DamageIsAfterTheTargetsArmourAtTheHitLocation()
    {
        var tally = new WeaponDamageTally(Rules);

        // Imported iron mitigates 3,200 basis points at the chest, so a
        // blow of 100 deals 68. The literal is the shipped V8 table's value.
        tally.Ingest(
            [Attack(1, source: 1, target: 2, damage: 100, hit: BodyPart.Chest)],
            [
                Agent(1, 0, WeaponId.Kampilan),
                Agent(2, 1, WeaponId.Kampilan, ArmorId.ImportedIron),
            ]);

        Assert.Equal(68, Assert.Single(RowsOf(tally, 0)).Damage);
    }

    [Theory]
    [InlineData(AttackResolution.ShieldBlocked)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    [InlineData(AttackResolution.Evaded)]
    public void NonLandedResolutionAddsNeitherDamageNorABlow(
        AttackResolution resolution)
    {
        var tally = new WeaponDamageTally(Rules);

        tally.Ingest(
            [Attack(1, source: 1, target: 2, damage: 0, resolution: resolution)],
            [Agent(1, 0, WeaponId.Kampilan), Agent(2, 1, WeaponId.Kampilan)]);

        var row = Assert.Single(RowsOf(tally, 0));
        Assert.Equal(0, row.Damage);
        Assert.Equal(0, row.BlowsLanded);
    }

    [Fact]
    public void RangedImpactCountsAtArrivalAndALaunchCountsNothing()
    {
        var tally = new WeaponDamageTally(Rules);
        AgentView[] agents =
        [
            Agent(1, 0, WeaponId.Busog),
            Agent(2, 1, WeaponId.Kampilan),
        ];

        tally.Ingest(
            [BattleEvent.NonAttack(
                1,
                tick: 1,
                BattleEventKind.Release,
                sourceEntityId: 1,
                targetEntityId: 2,
                value: 0,
                factionId: 0)],
            agents);
        Assert.Equal(0, Assert.Single(RowsOf(tally, 0)).BlowsLanded);

        tally.Ingest(
            [Attack(
                2,
                source: 1,
                target: 2,
                damage: 7,
                weapon: WeaponId.Busog)],
            agents);

        var row = Assert.Single(RowsOf(tally, 0));
        Assert.Equal(WeaponId.Busog, row.Weapon);
        Assert.Equal(7, row.Damage);
        Assert.Equal(1, row.BlowsLanded);
    }

    [Fact]
    public void BlowsAreAttributedToTheAttackersFactionWeaponAndShield()
    {
        var tally = new WeaponDamageTally(Rules);

        tally.Ingest(
            [
                Attack(1, source: 1, target: 2, damage: 10, shield: ShieldId.None),
                Attack(2, source: 1, target: 2, damage: 10, shield: ShieldId.TallHardwood),
                Attack(3, source: 2, target: 1, damage: 5, faction: 1),
            ],
            [Agent(1, 0, WeaponId.Kampilan), Agent(2, 1, WeaponId.Kampilan)]);

        Assert.Equal(
            [ShieldId.None, ShieldId.TallHardwood],
            RowsOf(tally, 0).Where(row => row.BlowsLanded == 1).Select(row => row.Shield).Order());
        Assert.Equal(5, tally.GetFactionDamage(1));
    }

    [Fact]
    public void LandedBlowAgainstATargetMissingFromTheRosterIsIgnored()
    {
        var tally = new WeaponDamageTally(Rules);

        tally.Ingest(
            [Attack(1, source: 1, target: 99, damage: 10)],
            [Agent(1, 0, WeaponId.Kampilan)]);

        Assert.Equal(0, Assert.Single(RowsOf(tally, 0)).BlowsLanded);
        Assert.Equal(0, tally.GetFactionDamage(0));
    }

    [Fact]
    public void AliveAndFieldedAreRecountedEveryIngest()
    {
        var tally = new WeaponDamageTally(Rules);
        var living = Agent(1, 0, WeaponId.Wasay);
        var fallen = Agent(2, 0, WeaponId.Wasay) with { IsAlive = false };

        tally.Ingest([], [living, Agent(3, 0, WeaponId.Wasay), fallen]);
        var first = Assert.Single(RowsOf(tally, 0));
        Assert.Equal(2, first.Alive);
        Assert.Equal(3, first.Fielded);

        tally.Ingest([], [living, fallen with { EntityId = 3 }, fallen]);
        var second = Assert.Single(RowsOf(tally, 0));
        Assert.Equal(1, second.Alive);
        Assert.Equal(3, second.Fielded);
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var tally = new WeaponDamageTally(Rules);
        tally.Ingest(
            [Attack(1, source: 1, target: 2, damage: 10)],
            [Agent(1, 0, WeaponId.Kampilan), Agent(2, 1, WeaponId.Kampilan)]);

        tally.Reset();

        Assert.Empty(RowsOf(tally, 0));
        Assert.Empty(RowsOf(tally, 1));
        Assert.Equal(0, tally.GetFactionDamage(0));

        // The armour lookup is cleared too: a blow whose target is no longer
        // listed is ignored rather than resolved against a stale entry.
        tally.Ingest([Attack(2, source: 1, target: 2, damage: 10)], []);
        Assert.Empty(RowsOf(tally, 0));
    }

    [Fact]
    public void RowsSortByDamageDescending()
    {
        var tally = new WeaponDamageTally(Rules);

        tally.Ingest(
            [
                Attack(1, source: 1, target: 9, damage: 5, weapon: WeaponId.Kalis),
                Attack(2, source: 2, target: 9, damage: 30, weapon: WeaponId.Wasay),
                Attack(3, source: 3, target: 9, damage: 12, weapon: WeaponId.Itak),
            ],
            [
                Agent(1, 0, WeaponId.Kalis),
                Agent(2, 0, WeaponId.Wasay),
                Agent(3, 0, WeaponId.Itak),
                Agent(9, 1, WeaponId.Kampilan),
            ]);

        Assert.Equal(
            [WeaponId.Wasay, WeaponId.Itak, WeaponId.Kalis],
            RowsOf(tally, 0).Select(row => row.Weapon));
    }

    [Fact]
    public void EqualDamageSortsByLabel()
    {
        var tally = new WeaponDamageTally(Rules);

        // Fielded in the reverse of label order, with no damage at all.
        tally.Ingest(
            [],
            [
                Agent(1, 0, WeaponId.Wasay),
                Agent(2, 0, WeaponId.Kampilan),
                Agent(3, 0, WeaponId.Kalis),
            ]);

        var labels = RowsOf(tally, 0).Select(row => row.Label).ToArray();
        Assert.Equal(3, labels.Length);
        Assert.Equal(labels.Order(StringComparer.Ordinal), labels);
    }

    [Fact]
    public void EveryRosterAndEnumWeaponProducesALabelWithoutThrowing()
    {
        var tally = new WeaponDamageTally(Rules);
        var roster = Rules.Roster
            .Select((loadout, index) => new AgentView(
                (ulong)index + 1,
                FactionId: 0,
                XRaw: 0,
                YRaw: 0,
                HitPoints: 100,
                MaximumHitPoints: 100,
                TargetEntityId: null,
                Intent: AgentIntent.Idle,
                IsAlive: true,
                Loadout: loadout))
            .ToArray();
        tally.Ingest([], roster);

        var rows = RowsOf(tally, 0);
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Label)));
        Assert.Equal(
            Rules.Roster.Select(loadout => (loadout.Weapon, loadout.Shield)).Distinct().Count(),
            rows.Length);

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                var singleton = new WeaponDamageTally(Rules);
                singleton.Ingest([], [Agent(1, 0, weapon, shield: shield)]);
                Assert.False(
                    string.IsNullOrWhiteSpace(Assert.Single(RowsOf(singleton, 0)).Label));
            }
        }
    }

    [Fact]
    public void GetRowsRejectsAnUnknownFactionAndATooSmallDestination()
    {
        var tally = new WeaponDamageTally(Rules);
        var destination = new WeaponDamageTallyRow[WeaponDamageTally.MaximumRowsPerFaction];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => tally.GetRows(2, destination));
        Assert.Throws<ArgumentException>(
            () => tally.GetRows(0, new WeaponDamageTallyRow[1]));
    }

    [Fact]
    public void IngestAllocatesNothingOnceTheRosterHasBeenSeen()
    {
        var tally = new WeaponDamageTally(Rules);
        var roster = Enumerable.Range(1, 500)
            .Select(id => Agent((ulong)id, id % 2, WeaponId.Kampilan))
            .ToArray();
        BattleEvent[] events =
        [
            Attack(1, source: 1, target: 2, damage: 10),
            Attack(2, source: 2, target: 1, damage: 10, faction: 1),
            Attack(3, source: 1, target: 2, damage: 0, resolution: AttackResolution.Parried),
        ];
        tally.Ingest(events, roster);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < 100; tick++)
        {
            tally.Ingest(events, roster);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void CoordinatorFeedsTheTallyFromIngestTickAndClearsItOnReset()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [Agent(1, 0, WeaponId.Kampilan), Agent(2, 1, WeaponId.Kampilan)];

        coordinator.IngestTick(
            [Attack(1, source: 1, target: 2, damage: 10)],
            agents,
            default);

        Assert.Equal(10, coordinator.DamageTally.GetFactionDamage(0));

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Equal(0, coordinator.DamageTally.GetFactionDamage(0));
    }

    private static WeaponDamageTallyRow[] RowsOf(WeaponDamageTally tally, int faction)
    {
        var buffer = new WeaponDamageTallyRow[WeaponDamageTally.MaximumRowsPerFaction];
        var count = tally.GetRows(faction, buffer);
        return buffer[..count];
    }

    private static AgentView Agent(
        ulong entityId,
        int faction,
        WeaponId weapon,
        ArmorId armor = ArmorId.None,
        ShieldId shield = ShieldId.None) =>
        new(
            entityId,
            faction,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(weapon, armor, shield));

    private static BattleEvent Attack(
        long sequence,
        ulong source,
        ulong target,
        int damage,
        int faction = 0,
        WeaponId weapon = WeaponId.Kampilan,
        ShieldId shield = ShieldId.None,
        BodyPart hit = BodyPart.Chest,
        AttackResolution resolution = AttackResolution.Landed) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            target,
            damage,
            faction,
            weapon,
            shield,
            hit,
            resolution);
}
