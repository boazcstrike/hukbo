using System.Text.Json;
using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

public sealed class AttackContactDispatcherTests
{
    [Fact]
    public void Ingest_PreservesAttackContextAndReleasesByTickThenInsertion()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 4);

        dispatcher.Ingest(
        [
            AttackEvent(
                sequence: 20,
                tick: 2,
                source: 4,
                target: 8,
                damage: 0,
                weapon: WeaponId.Wasay,
                shield: ShieldId.None,
                hitLocation: BodyPart.WeaponArm,
                resolution: AttackResolution.Parried),
            AttackEvent(
                sequence: 10,
                tick: 1,
                source: 2,
                target: 7,
                damage: 13,
                weapon: WeaponId.Kalis,
                shield: ShieldId.TallHardwood,
                hitLocation: BodyPart.Chest,
                resolution: AttackResolution.Landed,
                comboPosition: 3),
            AttackEvent(
                sequence: 11,
                tick: 1,
                source: 3,
                target: 9,
                damage: 0,
                weapon: WeaponId.Itak,
                shield: ShieldId.None,
                hitLocation: BodyPart.Shin,
                resolution: AttackResolution.Evaded),
        ]);

        var contacts = Drain(dispatcher);

        Assert.Equal([10L, 11L, 20L], contacts.Select(contact => contact.Sequence));
        var landed = contacts[0];
        Assert.Equal(1, landed.Tick);
        Assert.Equal(2UL, landed.AttackerEntityId);
        Assert.Equal(7UL, landed.DefenderEntityId);
        Assert.Equal(13, landed.Damage);
        Assert.Equal(0, landed.FactionId);
        Assert.Equal(WeaponId.Kalis, landed.Weapon);
        Assert.Equal(ShieldId.TallHardwood, landed.AttackerShield);
        Assert.Equal(BodyPart.Chest, landed.HitLocation);
        Assert.Equal(AttackResolution.Landed, landed.Resolution);
        Assert.Equal(3, landed.ComboPosition);
        Assert.False(landed.IsLethal);
    }

    [Fact]
    public void Ingest_RetainsFivePerAttackerAndCoalescesTheSixthWholeBundle()
    {
        var writer = new StringWriter();
        using var diagnostics = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.Render, null),
            writer);
        var dispatcher = new AttackContactDispatcher(
            attackerCapacity: 1,
            diagnostics);
        var events = new BattleEvent[6];
        for (var index = 0; index < events.Length; index++)
        {
            events[index] = AttackEvent(
                sequence: index + 1,
                tick: index + 1,
                source: 2,
                target: (ulong)index + 10,
                damage: index == 5 ? 17 : 0,
                weapon: index == 5 ? WeaponId.Itak : WeaponId.Kampilan,
                shield: index == 5 ? ShieldId.TallHardwood : ShieldId.None,
                hitLocation: index == 5 ? BodyPart.Neck : BodyPart.Chest,
                resolution: index == 5
                    ? AttackResolution.Landed
                    : AttackResolution.Evaded,
                comboPosition: index + 1);
        }

        dispatcher.Ingest(events);

        Assert.Equal(AttackContactDispatcher.MaximumPendingContactsPerAttacker, dispatcher.PendingCount);
        Assert.Equal(1, dispatcher.CollapsedContactCount);
        Assert.Equal("render.attackContactCollapsed", LogEvents.RenderAttackContactCollapsed);
        var line = Assert.Single(
            writer.ToString().Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal(
            LogEvents.RenderAttackContactCollapsed,
            root.GetProperty("ev").GetString());
        Assert.Equal(2UL, root.GetProperty("attackerId").GetUInt64());
        Assert.Equal(1, root.GetProperty("collapsedCount").GetInt32());
        Assert.Equal(6, root.GetProperty("sequence").GetInt64());
        Assert.Equal(6, root.GetProperty("tick").GetInt64());

        var contacts = Drain(dispatcher);
        Assert.Equal([1L, 2L, 3L, 4L, 6L], contacts.Select(contact => contact.Sequence));
        var replacement = contacts[^1];
        Assert.Equal(15UL, replacement.DefenderEntityId);
        Assert.Equal(17, replacement.Damage);
        Assert.Equal(WeaponId.Itak, replacement.Weapon);
        Assert.Equal(ShieldId.TallHardwood, replacement.AttackerShield);
        Assert.Equal(BodyPart.Neck, replacement.HitLocation);
        Assert.Equal(AttackResolution.Landed, replacement.Resolution);
        Assert.Equal(6, replacement.ComboPosition);
    }

    [Fact]
    public void Ingest_DamageIsAggregateAndCreatesNoContact()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 2);

        dispatcher.Ingest(
        [
            AttackEvent(1, 1, 2, 7, damage: 12),
            DamageEvent(2, tick: 1, target: 7, damage: 12),
        ]);

        var contact = Assert.Single(Drain(dispatcher));
        Assert.Equal(1, contact.Sequence);
        Assert.Equal(12, contact.Damage);
    }

    [Fact]
    public void Ingest_DeathMarksOnlyTheHighestSequenceSameTickLandedContact()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 4);

        dispatcher.Ingest(
        [
            AttackEvent(1, 5, 2, 7, damage: 10),
            AttackEvent(2, 5, 3, 7, damage: 11),
            AttackEvent(
                3,
                5,
                4,
                7,
                damage: 0,
                resolution: AttackResolution.Parried),
            DamageEvent(4, tick: 5, target: 7, damage: 21),
            DeathEvent(5, tick: 5, target: 7),
        ]);

        var contacts = Drain(dispatcher);

        Assert.Equal(3, contacts.Count);
        Assert.False(contacts.Single(contact => contact.Sequence == 1).IsLethal);
        Assert.True(contacts.Single(contact => contact.Sequence == 2).IsLethal);
        Assert.False(contacts.Single(contact => contact.Sequence == 3).IsLethal);
        Assert.Single(contacts, contact => contact.IsLethal);
    }

    [Fact]
    public void Ingest_AppliesMutualDeathsIndependentlyAcrossCatchUpTicks()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 4);

        dispatcher.Ingest(
        [
            AttackEvent(1, 8, 2, 7, damage: 20),
            AttackEvent(2, 8, 7, 2, damage: 20),
            DamageEvent(3, tick: 8, target: 2, damage: 20),
            DamageEvent(4, tick: 8, target: 7, damage: 20),
            DeathEvent(5, tick: 8, target: 2),
            DeathEvent(6, tick: 8, target: 7),
            AttackEvent(7, 9, 9, 10, damage: 0, resolution: AttackResolution.Evaded),
        ]);

        var contacts = Drain(dispatcher);

        Assert.Equal([1L, 2L, 7L], contacts.Select(contact => contact.Sequence));
        Assert.True(contacts.Single(contact => contact.Sequence == 1).IsLethal);
        Assert.True(contacts.Single(contact => contact.Sequence == 2).IsLethal);
        Assert.False(contacts.Single(contact => contact.Sequence == 7).IsLethal);
    }

    [Fact]
    public void Clear_RemovesPendingAndLatchedContactsAndResetsCollapseCount()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 1);
        dispatcher.Ingest(
        [
            AttackEvent(1, 1, 2, 7),
            AttackEvent(2, 2, 2, 7),
            AttackEvent(3, 3, 2, 7),
            AttackEvent(4, 4, 2, 7),
            AttackEvent(5, 5, 2, 7),
            AttackEvent(6, 6, 2, 7),
        ]);
        Assert.True(dispatcher.TryLatchNext(out _));

        dispatcher.Clear();

        Assert.Equal(0, dispatcher.PendingCount);
        Assert.Equal(0, dispatcher.CollapsedContactCount);
        Assert.False(dispatcher.TryGetLatched(out _));
        Assert.False(dispatcher.TryLatchNext(out _));
    }

    private static List<AttackContactBundle> Drain(AttackContactDispatcher dispatcher)
    {
        var contacts = new List<AttackContactBundle>();
        while (dispatcher.TryLatchNext(out var contact))
        {
            contacts.Add(contact);
            dispatcher.AcknowledgeLatched();
        }

        return contacts;
    }

    private static BattleEvent AttackEvent(
        long sequence,
        long tick,
        ulong source,
        ulong target,
        int damage = 10,
        WeaponId weapon = WeaponId.Kampilan,
        ShieldId shield = ShieldId.None,
        BodyPart hitLocation = BodyPart.Chest,
        AttackResolution resolution = AttackResolution.Landed,
        int? comboPosition = null) =>
        BattleEvent.Attack(
            sequence,
            tick,
            source,
            target,
            damage,
            factionId: 0,
            weapon,
            shield,
            hitLocation,
            resolution,
            comboPosition);

    private static BattleEvent DamageEvent(
        long sequence,
        long tick,
        ulong target,
        int damage) =>
        BattleEvent.NonAttack(
            sequence,
            tick,
            BattleEventKind.Damage,
            target,
            target,
            damage,
            factionId: null);

    private static BattleEvent DeathEvent(long sequence, long tick, ulong target) =>
        BattleEvent.NonAttack(
            sequence,
            tick,
            BattleEventKind.Death,
            target,
            targetEntityId: null,
            value: 0,
            factionId: null);
}
