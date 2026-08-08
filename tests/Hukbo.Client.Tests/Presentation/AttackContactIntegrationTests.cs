using Hukbo.Client.Audio;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class AttackContactIntegrationTests
{
    [Fact]
    public void Release_LethalLandedBundleStartsEveryAppropriateChannelAtomically()
    {
        var fixture = new ContactFixture();
        var contact = Contact(sequence: 1, isLethal: true);

        fixture.Release(contact);

        Assert.True(fixture.Animations.TryGetAnimation(2, out var animation));
        Assert.Equal(contact.Sequence, animation.Sequence);
        var hit = Assert.Single(fixture.Hits.ActiveEffects.ToArray());
        Assert.Equal(contact.Sequence, hit.Sequence);
        Assert.True(hit.IsLethal);
        var blood = Assert.Single(fixture.Blood.ActiveBursts.ToArray());
        Assert.Equal(contact.Sequence, blood.Sequence);
        Assert.True(blood.IsLethal);
        Assert.Empty(fixture.Clashes.ActiveEffects.ToArray());
        var reaction = Assert.Single(fixture.Reactions.ActiveReactions.ToArray());
        Assert.Equal(contact.Sequence, reaction.Sequence);
        Assert.True(reaction.IsLethal);
        Assert.Equal(
            [GameSoundId.AttackKampilan, GameSoundId.Death],
            fixture.Player.Played.Select(cue => cue.Sound));
    }

    [Fact]
    public void Release_ParriedBundleStartsClashAndReactionWithoutHitOrBlood()
    {
        var fixture = new ContactFixture();
        var contact = Contact(
            sequence: 2,
            resolution: AttackResolution.Parried);

        fixture.Release(contact);

        Assert.True(fixture.Animations.TryGetAnimation(2, out var animation));
        Assert.Equal(AttackResolution.Parried, animation.Resolution);
        Assert.Empty(fixture.Hits.ActiveEffects.ToArray());
        Assert.Empty(fixture.Blood.ActiveBursts.ToArray());
        var clash = Assert.Single(fixture.Clashes.ActiveEffects.ToArray());
        Assert.Equal(contact.Sequence, clash.Sequence);
        Assert.Equal(AttackResolution.Parried, clash.Resolution);
        var reaction = Assert.Single(fixture.Reactions.ActiveReactions.ToArray());
        Assert.Equal(AttackResolution.Parried, reaction.Resolution);
        Assert.Equal(GameSoundId.AttackKampilan, Assert.Single(fixture.Player.Played).Sound);
    }

    [Fact]
    public void Dispatch_TwoAttackValuesAndAggregateDamageReleaseNoDuplicateContact()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 2);
        dispatcher.Ingest(
        [
            AttackEvent(1, source: 2, damage: 10),
            AttackEvent(2, source: 3, damage: 11),
            BattleEvent.NonAttack(
                sequence: 3,
                tick: 5,
                BattleEventKind.Damage,
                sourceEntityId: 7,
                targetEntityId: 7,
                value: 21,
                factionId: null),
            BattleEvent.NonAttack(
                sequence: 4,
                tick: 5,
                BattleEventKind.Death,
                sourceEntityId: 7,
                targetEntityId: null,
                value: 0,
                factionId: null),
        ]);
        var fixture = new ContactFixture();

        while (dispatcher.TryLatchNext(out var contact))
        {
            fixture.Release(contact);
        }

        Assert.Equal(2, fixture.Hits.ActiveEffects.Length);
        Assert.Equal(
            [10, 11],
            fixture.Hits.ActiveEffects.ToArray().Select(effect => effect.Damage));
        Assert.Equal(2, fixture.Blood.ActiveBursts.Length);
        Assert.Single(fixture.Hits.ActiveEffects.ToArray(), effect => effect.IsLethal);
        Assert.Single(fixture.Blood.ActiveBursts.ToArray(), burst => burst.IsLethal);
        Assert.Equal(3, fixture.Player.Played.Count);
        Assert.Equal(1, fixture.Player.Played.Count(cue => cue.Sound == GameSoundId.Death));
    }

    [Fact]
    public void CallerPause_FreezesContactTransientsAndQueueWhileStartedSoundFinishes()
    {
        var dispatcher = new AttackContactDispatcher(attackerCapacity: 1);
        dispatcher.Ingest(
        [
            AttackEvent(1, source: 2, damage: 10),
            AttackEvent(2, source: 2, damage: 11),
        ]);
        var fixture = new ContactFixture(soundDurationSeconds: 0.05);
        Assert.True(dispatcher.TryLatchNext(out var contact));
        fixture.Release(contact);
        Assert.True(fixture.Animations.AcknowledgeDraw(2, 1));
        var hitBeforePause = Assert.Single(fixture.Hits.ActiveEffects.ToArray());
        var bloodBeforePause = Assert.Single(fixture.Blood.ActiveBursts.ToArray());
        var reactionBeforePause = Assert.Single(
            fixture.Reactions.ActiveReactions.ToArray());

        // A paused caller advances no contact clock and releases no queued
        // bundle. Audio's already-started voice remains allowed to finish.
        fixture.Sounds.BeginFrame(elapsedSeconds: 0.10);

        Assert.Equal(hitBeforePause, Assert.Single(fixture.Hits.ActiveEffects.ToArray()));
        Assert.Equal(bloodBeforePause, Assert.Single(fixture.Blood.ActiveBursts.ToArray()));
        Assert.Equal(
            reactionBeforePause,
            Assert.Single(fixture.Reactions.ActiveReactions.ToArray()));
        Assert.True(fixture.Animations.TryGetAnimation(2, out var animation));
        Assert.Equal(0f, animation.AgeSeconds);
        Assert.True(dispatcher.TryGetLatched(2, out var stillLatched));
        Assert.Equal(contact, stillLatched);
        Assert.False(dispatcher.TryLatchNext(out _));
        Assert.Equal(1, dispatcher.PendingCount);
        Assert.Single(fixture.Player.Played);
        Assert.Equal(0, fixture.Sounds.SoundingVoices);
    }

    private static AttackContactBundle Contact(
        long sequence,
        AttackResolution resolution = AttackResolution.Landed,
        bool isLethal = false) =>
        new(
            sequence,
            Tick: 5,
            AttackerEntityId: 2,
            DefenderEntityId: 7,
            Damage: resolution == AttackResolution.Landed ? 10 : 0,
            FactionId: 0,
            WeaponId.Kampilan,
            AttackerShield: ShieldId.None,
            HitLocation: BodyPart.Chest,
            resolution,
            ComboPosition: null,
            isLethal);

    private static BattleEvent AttackEvent(long sequence, ulong source, int damage) =>
        BattleEvent.Attack(
            sequence,
            tick: 5,
            source,
            targetEntityId: 7,
            damage,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Chest);

    private sealed class ContactFixture
    {
        public ContactFixture(double soundDurationSeconds = 0.25)
        {
            Player = new RecordingSoundPlayer(soundDurationSeconds);
            Sounds = new SoundDirector(logCapacity: 16, Player);
            Sounds.BeginFrame(elapsedSeconds: 0);
        }

        public AttackAnimationSystem Animations { get; } = new(capacity: 4);

        public HitEffectSystem Hits { get; } = new(capacity: 8);

        public BloodEffectSystem Blood { get; } = new();

        public ClashEffectSystem Clashes { get; } = new(capacity: 8);

        public DefenderReactionSystem Reactions { get; } = new(capacity: 4);

        public RecordingSoundPlayer Player { get; }

        public SoundDirector Sounds { get; }

        public void Release(AttackContactBundle contact)
        {
            var attacker = Agent(contact.AttackerEntityId, 0, 0);
            var defender = Agent(
                contact.DefenderEntityId,
                300,
                0,
                isAlive: !contact.IsLethal);
            Animations.Ingest(
                contact,
                directionX: 1f,
                directionY: 0f,
                MotionIntensity.Full);
            Hits.StartContact(contact, defender);
            Blood.StartContact(contact, attacker, defender);
            Clashes.StartContact(contact, attacker, defender);
            Reactions.StartContact(contact, attacker, defender);
            Sounds.StartContact(contact);
        }

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

    private sealed class RecordingSoundPlayer(double durationSeconds) : ISoundPlayer
    {
        public string DirectoryPath => "/audio";

        public IReadOnlyList<SoundBinding> Bindings => [];

        public List<(GameSoundId Sound, HitClass? HitClass)> Played { get; } = [];

        public SoundBindingStatus GetStatus(GameSoundId sound, HitClass? hitClass) =>
            SoundBindingStatus.Ready;

        public int GetVariantCount(GameSoundId sound, HitClass? hitClass) => 1;

        public double GetDurationSeconds(
            GameSoundId sound,
            HitClass? hitClass,
            int variantIndex) =>
            durationSeconds;

        public bool Play(
            GameSoundId sound,
            HitClass? hitClass,
            int variantIndex,
            float volume)
        {
            Played.Add((sound, hitClass));
            return true;
        }
    }
}
