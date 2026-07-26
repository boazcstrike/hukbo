using Hukbo.Client.Audio;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class SoundDirectorTests
{
    [Fact]
    public void Ingest_PlaysAndLogsAMappedEvent()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest([Attack(1, WeaponId.GreatBlade)]);

        Assert.Equal(
            GameSoundId.AttackGreatBlade,
            Assert.Single(player.Played).Sound);
        Assert.Equal(SoundDirector.CueVolume, player.Played[0].Volume);
        var cue = Assert.Single(director.Log.Entries);
        Assert.Equal(SoundCueStatus.Played, cue.Status);
        Assert.Equal(GameSoundId.AttackGreatBlade, cue.Sound);
    }

    [Fact]
    public void Ingest_MapsTheEventHitLocationToTheAcousticHitClass()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest(
            [Attack(1, WeaponId.GreatBlade, BodyPart.Head)]);

        Assert.Equal(HitClass.Skull, Assert.Single(player.Played).HitClass);
    }

    [Fact]
    public void Ingest_UsesANullHitClassForAnEventWithNoHitLocation()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        Assert.Null(Assert.Single(player.Played).HitClass);
    }

    [Fact]
    public void Ingest_SpreadsVariantSelectionAcrossDifferentSourceEntities()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready, variantCount: 10);
        var director = new SoundDirector(
            logCapacity: 64,
            player,
            new SoundCueBudget(maximumPerSound: 20, maximumTotal: 20));
        var events = new BattleEvent[10];
        for (var index = 0; index < events.Length; index++)
        {
            events[index] = BattleEvent.Attack(
                index + 1,
                tick: 5,
                sourceEntityId: (ulong)(index + 1),
                targetEntityId: 99,
                damage: 9,
                factionId: 0,
                WeaponId.GreatBlade,
                BodyPart.Chest);
        }

        director.BeginFrame();
        director.Ingest(events);

        var distinctIndexes = player.Played
            .Select(played => played.VariantIndex)
            .Distinct()
            .Count();
        Assert.True(
            distinctIndexes > 1,
            "Ten different source entities should not all select the same variant.");
    }

    [Fact]
    public void RequestCue_SelectsTheSameVariantDeterministicallyWithNoSourceEntity()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready, variantCount: 10);
        var director = new SoundDirector(
            logCapacity: 64,
            player,
            new SoundCueBudget(maximumPerSound: 5, maximumTotal: 5));

        director.BeginFrame();
        director.RequestCue(GameSoundId.UiClick, tick: 1);
        director.RequestCue(GameSoundId.UiClick, tick: 1);

        Assert.Equal(2, player.Played.Count);
        Assert.Equal(player.Played[0].VariantIndex, player.Played[1].VariantIndex);
    }

    [Fact]
    public void Ingest_IgnoresSilentEventKinds()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest(
        [
            NonAttack(1, BattleEventKind.Move),
            NonAttack(2, BattleEventKind.Damage),
        ]);

        Assert.Empty(player.Played);
        Assert.Empty(director.Log.Entries);
    }

    [Fact]
    public void Ingest_LogsAMissingBindingWithoutAskingThePlayerToPlay()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Missing);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        Assert.Empty(player.Played);
        Assert.Equal(
            SoundCueStatus.Missing,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Ingest_LogsAFailedLoadDistinctlyFromAMissingFile()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.LoadFailed);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        Assert.Empty(player.Played);
        Assert.Equal(
            SoundCueStatus.LoadFailed,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Ingest_ReportsAMissingFileEvenWhileMuted()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Missing);
        var director = new SoundDirector(logCapacity: 64, player);
        director.ToggleMute();

        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        Assert.True(director.IsMuted);
        Assert.Equal(
            SoundCueStatus.Missing,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Ingest_SkipsPlaybackWhileMutedAndResumesAfterUnmuting()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);
        director.ToggleMute();

        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        Assert.Empty(player.Played);
        Assert.Equal(
            SoundCueStatus.Muted,
            Assert.Single(director.Log.Entries).Status);

        director.ToggleMute();
        director.BeginFrame();
        director.Ingest([NonAttack(2, BattleEventKind.Death)]);

        Assert.Single(player.Played);
        Assert.False(director.IsMuted);
    }

    [Fact]
    public void Ingest_SuppressesCuesPastTheFrameBudgetAndCollapsesTheLog()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(
            logCapacity: 64,
            player,
            new SoundCueBudget(maximumPerSound: 2, maximumTotal: 8));
        var events = new BattleEvent[10];
        for (var index = 0; index < events.Length; index++)
        {
            events[index] = Attack(index + 1, WeaponId.GreatBlade);
        }

        director.BeginFrame();
        director.Ingest(events);

        Assert.Equal(2, player.Played.Count);
        Assert.Equal(2, director.Log.Entries.Count);
        Assert.Equal(SoundCueStatus.Played, director.Log.Entries[0].Status);
        Assert.Equal(2, director.Log.Entries[0].Count);
        Assert.Equal(SoundCueStatus.Suppressed, director.Log.Entries[1].Status);
        Assert.Equal(8, director.Log.Entries[1].Count);
    }

    [Fact]
    public void BeginFrame_RestoresTheBudgetForTheNextFrame()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(
            logCapacity: 64,
            player,
            new SoundCueBudget(maximumPerSound: 1, maximumTotal: 1));

        director.BeginFrame();
        director.Ingest([Attack(1, WeaponId.Bolo), Attack(2, WeaponId.Bolo)]);
        Assert.Single(player.Played);

        director.BeginFrame();
        director.Ingest([Attack(3, WeaponId.Bolo)]);

        Assert.Equal(2, player.Played.Count);
    }

    [Fact]
    public void Ingest_KeepsTheBudgetAcrossTheTicksOfOneFrame()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(
            logCapacity: 64,
            player,
            new SoundCueBudget(maximumPerSound: 1, maximumTotal: 1));

        director.BeginFrame();
        director.Ingest([Attack(1, WeaponId.Bolo)]);
        director.Ingest([Attack(2, WeaponId.Bolo)]);

        Assert.Single(player.Played);
    }

    [Fact]
    public void RequestCue_RoutesANonSimulationCueThroughTheSamePath()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame();
        director.RequestCue(GameSoundId.UiClick, tick: 42);

        Assert.Equal(GameSoundId.UiClick, Assert.Single(player.Played).Sound);
        Assert.Equal(42, Assert.Single(director.Log.Entries).Tick);
    }

    [Fact]
    public void AttachPlayer_ReplacesThePlayerAndKeepsTheLog()
    {
        var director = new SoundDirector(logCapacity: 64);
        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);

        director.AttachPlayer(player);
        director.Ingest([NonAttack(2, BattleEventKind.Death)]);

        Assert.Equal(2, director.Log.Entries.Count);
        Assert.Equal(SoundCueStatus.Missing, director.Log.Entries[0].Status);
        Assert.Equal(SoundCueStatus.Played, director.Log.Entries[1].Status);
    }

    [Fact]
    public void DefaultPlayer_IsSilentAndNeverAskedToPlay()
    {
        var director = new SoundDirector(logCapacity: 64);

        director.BeginFrame();
        director.Ingest([Attack(1, WeaponId.GreatBlade)]);

        Assert.IsType<SilentSoundPlayer>(director.Player);
        Assert.Equal(
            SoundCueStatus.Missing,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Clear_EmptiesTheLog()
    {
        var director = new SoundDirector(logCapacity: 64);
        director.BeginFrame();
        director.Ingest([NonAttack(1, BattleEventKind.Death)]);

        director.Clear();

        Assert.Empty(director.Log.Entries);
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var director = new SoundDirector(logCapacity: 64);

        Assert.Throws<ArgumentNullException>(() => director.Ingest(null!));
        Assert.Throws<ArgumentNullException>(
            () => director.AttachPlayer(null!));
    }

    private static BattleEvent Attack(long sequence, WeaponId weapon) =>
        Attack(sequence, weapon, BodyPart.Chest);

    private static BattleEvent Attack(
        long sequence,
        WeaponId weapon,
        BodyPart hitLocation) =>
        BattleEvent.Attack(
            sequence,
            tick: 5,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 9,
            factionId: 0,
            weapon,
            hitLocation);

    private static BattleEvent NonAttack(
        long sequence,
        BattleEventKind kind) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 5,
            kind,
            sourceEntityId: 1,
            targetEntityId: 2,
            value: 9,
            factionId: 0);

    private sealed class RecordingSoundPlayer : ISoundPlayer
    {
        private readonly SoundBindingStatus _status;
        private readonly int _variantCount;

        public RecordingSoundPlayer(SoundBindingStatus status, int variantCount = 1)
        {
            _status = status;
            _variantCount = variantCount;
            var bindings = new SoundBinding[SoundCatalog.AllSounds.Count];
            for (var index = 0; index < SoundCatalog.AllSounds.Count; index++)
            {
                var sound = SoundCatalog.AllSounds[index];
                bindings[index] = new SoundBinding(
                    sound,
                    SoundCatalog.GetFileName(sound),
                    ClassCounts: [],
                    status == SoundBindingStatus.Ready ? variantCount : 0,
                    status);
            }

            Bindings = bindings;
        }

        public string DirectoryPath => "/audio";

        public IReadOnlyList<SoundBinding> Bindings { get; }

        public List<(GameSoundId Sound, HitClass? HitClass, int VariantIndex, float Volume)>
            Played
        { get; } = [];

        public SoundBindingStatus GetStatus(GameSoundId sound, HitClass? hitClass) => _status;

        public int GetVariantCount(GameSoundId sound, HitClass? hitClass) =>
            _status == SoundBindingStatus.Ready ? _variantCount : 0;

        public void Play(
            GameSoundId sound,
            HitClass? hitClass,
            int variantIndex,
            float volume)
        {
            if (_status != SoundBindingStatus.Ready)
            {
                throw new InvalidOperationException(
                    "The director must never play an unready binding.");
            }

            Played.Add((sound, hitClass, variantIndex, volume));
        }
    }
}
