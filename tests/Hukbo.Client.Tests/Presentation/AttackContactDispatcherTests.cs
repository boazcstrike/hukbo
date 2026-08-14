using System.Text.Json;
using Hukbo.Client.Audio;
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
            new LogOptions(LogLevel.Debug, LogChannel.Render, null),
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
        Assert.Equal("dbg", root.GetProperty("lvl").GetString());
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

    /// <summary>
    /// CHARACTERIZATION TEST — records a known defect, not desired
    /// behaviour. It does not fix anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a seventh-attacker-relative sixth contact overflows the
    /// five-slot-per-attacker pool, <c>Add</c> calls <c>ReplacePending</c>
    /// (<c>src/Hukbo.Client/Presentation/AttackContactDispatcher.cs:237,277</c>),
    /// which overwrites the discarded bundle's array slot in place. The
    /// discarded <see cref="AttackContactBundle"/> is never latched, never
    /// drained, and never logged by identity — <c>ReportCollapsed</c> logs
    /// the sequence and tick of the <em>replacing</em> contact, not the
    /// discarded one, so even the diagnostic line keeps no trace of what was
    /// thrown away. Every presentation channel that would have read that
    /// bundle — the weapon sound, the death sound, the blood spray, the
    /// clash spark, the defender's flinch — silently loses its cue for that
    /// contact.
    /// </para>
    /// <para>
    /// A single discarded bundle cannot exercise both the blood channel and
    /// the clash channel at once: <see cref="BloodEffectSystem.StartContact"/>
    /// only starts a burst for <see cref="AttackResolution.Landed"/>, and
    /// <see cref="ClashEffect.FiresFor"/> is documented to fire for neither a
    /// landed blow nor a void. This test therefore overflows two independent
    /// attacker pools — one landed-and-lethal bundle to cover the weapon,
    /// death, blood, and defender-reaction channels, and one shield-blocked
    /// bundle to cover the clash channel — and asserts each channel's loss
    /// as its own, separately failing assertion.
    /// </para>
    /// </remarks>
    [Fact]
    public void Coalesce_SilentlyDropsEveryPresentationCueOfTheDiscardedBundle()
    {
        var writer = new StringWriter();
        using var diagnostics = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Debug, LogChannel.Render, null),
            writer);
        var dispatcher = new AttackContactDispatcher(
            attackerCapacity: 2,
            diagnostics);

        // Attacker 2 fills its five-contact pool, its fifth (newest, about
        // to be evicted) contact lands and is then marked lethal, and a
        // sixth attack forces the eviction.
        // Attacker 3 fills its own five-contact pool, its fifth (newest)
        // contact is shield-blocked, and a sixth attack forces its eviction.
        dispatcher.Ingest(
        [
            AttackEvent(1, 1, 2, 10, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(2, 2, 2, 11, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(3, 3, 2, 12, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(4, 4, 2, 13, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(
                5,
                5,
                2,
                14,
                damage: 17,
                weapon: WeaponId.Wasay,
                hitLocation: BodyPart.Neck,
                resolution: AttackResolution.Landed),
            DeathEvent(6, 5, 14),
            AttackEvent(
                7,
                6,
                2,
                20,
                damage: 0,
                weapon: WeaponId.Itak,
                resolution: AttackResolution.Evaded),
            AttackEvent(11, 1, 3, 30, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(12, 2, 3, 31, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(13, 3, 3, 32, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(14, 4, 3, 33, damage: 0, resolution: AttackResolution.Evaded),
            AttackEvent(
                15,
                5,
                3,
                34,
                damage: 0,
                weapon: WeaponId.Kalis,
                shield: ShieldId.TallHardwood,
                hitLocation: BodyPart.WeaponArm,
                resolution: AttackResolution.ShieldBlocked),
            AttackEvent(
                16,
                6,
                3,
                40,
                damage: 0,
                weapon: WeaponId.Itak,
                resolution: AttackResolution.Evaded),
        ]);

        Assert.Equal(2, dispatcher.CollapsedContactCount);

        // The discarded bundles are reconstructed here from the dispatcher's
        // own, already-verified construction rule (TryCreateBundle plus
        // MarkLethal) because the dispatcher provides no way to recover
        // them once overwritten — that inability is exactly the defect.
        var discardedLandedLethal = new AttackContactBundle(
            Sequence: 5,
            Tick: 5,
            AttackerEntityId: 2,
            DefenderEntityId: 14,
            Damage: 17,
            FactionId: 0,
            Weapon: WeaponId.Wasay,
            AttackerShield: ShieldId.None,
            HitLocation: BodyPart.Neck,
            Resolution: AttackResolution.Landed,
            ComboPosition: null,
            IsLethal: true);
        var discardedShieldBlocked = new AttackContactBundle(
            Sequence: 15,
            Tick: 5,
            AttackerEntityId: 3,
            DefenderEntityId: 34,
            Damage: 0,
            FactionId: 0,
            Weapon: WeaponId.Kalis,
            AttackerShield: ShieldId.TallHardwood,
            HitLocation: BodyPart.WeaponArm,
            Resolution: AttackResolution.ShieldBlocked,
            ComboPosition: null,
            IsLethal: false);

        var attacker = Agent(entityId: 2, xRaw: 0, yRaw: 0);
        var defender = Agent(entityId: 14, xRaw: 100, yRaw: 100, isAlive: false);

        // Channel 1: weapon cue. The discarded bundle carried a real,
        // non-null attack sound that will now never play.
        var soundRequest = SoundCueMapper.MapContact(discardedLandedLethal);
        Assert.Equal(GameSoundId.AttackWasay, soundRequest.Contact);

        // Channel 2: death cue. The bundle was marked lethal before it was
        // overwritten, so the owned Death cue it earned is lost with it.
        Assert.Equal(GameSoundId.Death, soundRequest.Lethal);

        // Channel 3: blood. A landed, lethal bundle starts a burst carrying
        // its own weapon, hit location, and lethal tier; none of that ever
        // reaches BloodEffectSystem for this contact.
        var blood = new BloodEffectSystem();
        blood.StartContact(discardedLandedLethal, attacker, defender);
        var burst = Assert.Single(blood.ActiveBursts.ToArray());
        Assert.Equal(WeaponId.Wasay, burst.Weapon);
        Assert.Equal(BodyPart.Neck, burst.HitLocation);
        Assert.True(burst.IsLethal);

        // Channel 4: defender reaction. The defender's flinch is keyed on
        // this exact contact; it never reaches DefenderReactionSystem.
        var reactions = new DefenderReactionSystem(capacity: 1);
        reactions.StartContact(discardedLandedLethal, attacker, defender);
        var reaction = Assert.Single(reactions.ActiveReactions.ToArray());
        Assert.Equal(14UL, reaction.DefenderEntityId);
        Assert.True(reaction.IsLethal);

        // Channel 5: clash. The shield-blocked bundle from the second
        // attacker pool would have put a clash spark on screen; it is
        // discarded before any renderer ever sees it.
        Assert.True(ClashEffect.FiresFor(discardedShieldBlocked.Resolution));

        // The dispatcher itself never yields either discarded sequence,
        // under any drain order.
        var survivors = Drain(dispatcher).Select(contact => contact.Sequence).ToArray();
        Assert.DoesNotContain(5L, survivors);
        Assert.DoesNotContain(15L, survivors);

        // Even the diagnostic line keeps no trace of the discarded bundle's
        // own identity: it logs the sequence and tick of the contact that
        // replaced it, not the one that was lost.
        var lines = writer.ToString().Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        using var firstCollapse = JsonDocument.Parse(lines[0]);
        var firstRoot = firstCollapse.RootElement;
        Assert.Equal(
            LogEvents.RenderAttackContactCollapsed,
            firstRoot.GetProperty("ev").GetString());
        Assert.Equal("dbg", firstRoot.GetProperty("lvl").GetString());
        Assert.Equal(2UL, firstRoot.GetProperty("attackerId").GetUInt64());
        Assert.Equal(1, firstRoot.GetProperty("collapsedCount").GetInt32());
        Assert.Equal(7, firstRoot.GetProperty("sequence").GetInt64());
        Assert.Equal(6, firstRoot.GetProperty("tick").GetInt64());

        using var secondCollapse = JsonDocument.Parse(lines[1]);
        var secondRoot = secondCollapse.RootElement;
        Assert.Equal(
            LogEvents.RenderAttackContactCollapsed,
            secondRoot.GetProperty("ev").GetString());
        Assert.Equal("dbg", secondRoot.GetProperty("lvl").GetString());
        Assert.Equal(3UL, secondRoot.GetProperty("attackerId").GetUInt64());
        Assert.Equal(2, secondRoot.GetProperty("collapsedCount").GetInt32());
        Assert.Equal(16, secondRoot.GetProperty("sequence").GetInt64());
        Assert.Equal(6, secondRoot.GetProperty("tick").GetInt64());

        // The event identifier's real wire value, pinned so a reader chasing
        // it (for example the attack-animation-v2 backlog, which currently
        // greps the wrong string "render.attack.contact.collapsed") lands on
        // the identifier that actually appears in a log line.
        Assert.Equal(
            "render.attackContactCollapsed",
            LogEvents.RenderAttackContactCollapsed);
    }

    [Fact]
    public void Ingest_DamageIsAggregateAndCreatesNoContact()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 2);

        dispatcher.Ingest(
        [
            AttackEvent(1, 1, 2, 7, damage: 12),
            AttackEvent(2, 1, 3, 7, damage: 7),
            DamageEvent(3, tick: 1, target: 7, damage: 19),
        ]);

        var contacts = Drain(dispatcher);
        Assert.Equal([1L, 2L], contacts.Select(contact => contact.Sequence));
        Assert.Equal([12, 7], contacts.Select(contact => contact.Damage));
    }

    [Fact]
    public void LatchesOneContactPerAttackerUntilMatchingAcknowledgement()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 2);
        dispatcher.Ingest(
        [
            AttackEvent(1, tick: 5, source: 2, target: 7),
            AttackEvent(2, tick: 5, source: 3, target: 8),
            AttackEvent(3, tick: 5, source: 2, target: 9),
        ]);

        Assert.True(dispatcher.TryLatchNext(out var attackerAFirst));
        Assert.Equal(1, attackerAFirst.Sequence);
        Assert.True(dispatcher.TryLatchNext(out var attackerBFirst));
        Assert.Equal(2, attackerBFirst.Sequence);
        Assert.False(dispatcher.TryLatchNext(out _));
        Assert.Equal(1, dispatcher.PendingCount);
        Assert.Equal(2, dispatcher.LatchedCount);

        Assert.False(
            dispatcher.AcknowledgeLatched(
                attackerAFirst.AttackerEntityId,
                sequence: 99));
        Assert.True(
            dispatcher.TryGetLatched(
                attackerAFirst.AttackerEntityId,
                out var stillLatched));
        Assert.Equal(attackerAFirst, stillLatched);
        Assert.False(dispatcher.TryLatchNext(out _));

        Assert.True(
            dispatcher.AcknowledgeLatched(
                attackerAFirst.AttackerEntityId,
                attackerAFirst.Sequence));
        Assert.True(dispatcher.TryLatchNext(out var attackerASecond));
        Assert.Equal(3, attackerASecond.Sequence);
        Assert.Equal(2UL, attackerASecond.AttackerEntityId);
        Assert.Equal(0, dispatcher.PendingCount);
        Assert.Equal(2, dispatcher.LatchedCount);
    }

    [Fact]
    public void Ingest_CountsLatchedContactTowardPerAttackerOverflowLimit()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 1);
        dispatcher.Ingest(
        [
            AttackEvent(1, 1, 2, 7),
            AttackEvent(2, 2, 2, 8),
            AttackEvent(3, 3, 2, 9),
            AttackEvent(4, 4, 2, 10),
            AttackEvent(5, 5, 2, 11),
        ]);
        Assert.True(dispatcher.TryLatchNext(out var latched));

        dispatcher.Ingest(
        [
            AttackEvent(
                6,
                6,
                2,
                12,
                damage: 17,
                weapon: WeaponId.Itak,
                hitLocation: BodyPart.Neck),
        ]);

        Assert.Equal(4, dispatcher.PendingCount);
        Assert.Equal(1, dispatcher.LatchedCount);
        Assert.Equal(1, dispatcher.CollapsedContactCount);
        Assert.True(
            dispatcher.TryGetLatched(
                latched.AttackerEntityId,
                out var retainedLatch));
        Assert.Equal(latched, retainedLatch);
        Assert.True(
            dispatcher.AcknowledgeLatched(
                latched.AttackerEntityId,
                latched.Sequence));

        var contacts = Drain(dispatcher);
        Assert.Equal([2L, 3L, 4L, 6L], contacts.Select(contact => contact.Sequence));
        var replacement = contacts[^1];
        Assert.Equal(12UL, replacement.DefenderEntityId);
        Assert.Equal(17, replacement.Damage);
        Assert.Equal(WeaponId.Itak, replacement.Weapon);
        Assert.Equal(BodyPart.Neck, replacement.HitLocation);
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
        Assert.True(dispatcher.TryLatchNext(out var latched));

        dispatcher.Clear();

        Assert.Equal(0, dispatcher.PendingCount);
        Assert.Equal(0, dispatcher.LatchedCount);
        Assert.Equal(0, dispatcher.CollapsedContactCount);
        Assert.False(
            dispatcher.TryGetLatched(
                latched.AttackerEntityId,
                out _));
        Assert.False(dispatcher.TryLatchNext(out _));
    }

    private static List<AttackContactBundle> Drain(AttackContactDispatcher dispatcher)
    {
        var contacts = new List<AttackContactBundle>();
        while (dispatcher.TryLatchNext(out var contact))
        {
            contacts.Add(contact);
            Assert.True(
                dispatcher.AcknowledgeLatched(
                    contact.AttackerEntityId,
                    contact.Sequence));
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

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            isAlive,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));
}
