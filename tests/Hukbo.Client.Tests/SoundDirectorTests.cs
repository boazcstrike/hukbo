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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan)], []);

        Assert.Equal(
            GameSoundId.AttackKampilan,
            Assert.Single(player.Played).Sound);
        Assert.Equal(SoundDirector.CueVolume, player.Played[0].Volume);
        var cue = Assert.Single(director.Log.Entries);
        Assert.Equal(SoundCueStatus.Played, cue.Status);
        Assert.Equal(GameSoundId.AttackKampilan, cue.Sound);
    }

    [Fact]
    public void Ingest_MapsTheEventHitLocationToTheAcousticHitClass()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(
            [Attack(1, WeaponId.Kampilan, BodyPart.Head)], []);

        Assert.Equal(HitClass.Skull, Assert.Single(player.Played).HitClass);
    }

    [Fact]
    public void Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation()
    {
        // The guard for a regression that is silent by nature. A shield-blocked
        // attack still carries a real hit location, so a director that derives
        // the hit class from the event rather than from the mapped slot would
        // ask for (ClashShieldKampilan, HitClass.Skull) — a key a classless
        // slot is never registered under — and the cue would resolve Missing
        // forever with nothing else going red. If this test is deleted or
        // relaxed, that regression becomes invisible again.
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(
        [
            BattleEvent.Attack(
                sequence: 1,
                tick: 5,
                sourceEntityId: 1,
                targetEntityId: 2,
                damage: 0,
                factionId: 0,
                WeaponId.Kampilan,
                ShieldId.None,
                BodyPart.Head,
                AttackResolution.ShieldBlocked),
        ],
        []);

        var played = Assert.Single(player.Played);
        Assert.Null(played.HitClass);
        Assert.Equal(GameSoundId.ClashShieldKampilan, played.Sound);
    }

    [Fact]
    public void Ingest_UsesANullHitClassForAnEventWithNoHitLocation()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

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
                WeaponId.Kampilan,
                ShieldId.None,
                BodyPart.Chest);
        }

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(events, []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(
        [
            NonAttack(1, BattleEventKind.Move),
            NonAttack(2, BattleEventKind.Damage),
        ],
        []);

        Assert.Empty(player.Played);
        Assert.Empty(director.Log.Entries);
    }

    [Fact]
    public void Ingest_LogsAMissingBindingWithoutAskingThePlayerToPlay()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Missing);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

        Assert.Empty(player.Played);
        Assert.Equal(
            SoundCueStatus.Muted,
            Assert.Single(director.Log.Entries).Status);

        director.ToggleMute();
        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(2, BattleEventKind.Death)], []);

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
            events[index] = Attack(index + 1, WeaponId.Kampilan);
        }

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(events, []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Itak), Attack(2, WeaponId.Itak)], []);
        Assert.Single(player.Played);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(3, WeaponId.Itak)], []);

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

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Itak)], []);
        director.Ingest([Attack(2, WeaponId.Itak)], []);

        Assert.Single(player.Played);
    }

    [Fact]
    public void RequestCue_RoutesANonSimulationCueThroughTheSamePath()
    {
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.RequestCue(GameSoundId.UiClick, tick: 42);

        Assert.Equal(GameSoundId.UiClick, Assert.Single(player.Played).Sound);
        Assert.Equal(42, Assert.Single(director.Log.Entries).Tick);
    }

    [Fact]
    public void AttachPlayer_ReplacesThePlayerAndKeepsTheLog()
    {
        var director = new SoundDirector(logCapacity: 64);
        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);

        director.AttachPlayer(player);
        director.Ingest([NonAttack(2, BattleEventKind.Death)], []);

        Assert.Equal(2, director.Log.Entries.Count);
        Assert.Equal(SoundCueStatus.Missing, director.Log.Entries[0].Status);
        Assert.Equal(SoundCueStatus.Played, director.Log.Entries[1].Status);
    }

    [Fact]
    public void DefaultPlayer_IsSilentAndNeverAskedToPlay()
    {
        var director = new SoundDirector(logCapacity: 64);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan)], []);

        Assert.IsType<SilentSoundPlayer>(director.Player);
        Assert.Equal(
            SoundCueStatus.Missing,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Clear_EmptiesTheLog()
    {
        var director = new SoundDirector(logCapacity: 64);
        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([NonAttack(1, BattleEventKind.Death)], []);

        director.Clear();

        Assert.Empty(director.Log.Entries);
    }

    [Fact]
    public void Ingest_RejectsNullArguments()
    {
        var director = new SoundDirector(logCapacity: 64);

        Assert.Throws<ArgumentNullException>(() => director.Ingest(null!, []));
        Assert.Throws<ArgumentNullException>(
            () => director.Ingest([], null!));
        Assert.Throws<ArgumentNullException>(
            () => director.AttachPlayer(null!));
    }

    // --- RU-19: Release events resolve their weapon from the agent view. ---

    [Fact]
    public void Ingest_ResolvesAReleaseEventsWeaponFromTheSourceAgentsLoadout()
    {
        // RU-14 exposed SoundCueMapper.MapRelease(WeaponId?) as internal for
        // exactly this caller: a Release event's BattleEvent.Weapon is always
        // null by construction (BattleEvent.NonAttack forces every
        // combat-context field to null), so the weapon has to come from the
        // launching agent's own view instead of from the event.
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);
        var launcher = BusogAgent(entityId: 7);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Release(entityId: 7)], [launcher]);

        var played = Assert.Single(player.Played);
        Assert.Equal(GameSoundId.ReleaseBusog, played.Sound);
        // A release slot is not hit-location driven (SoundCatalogTests pins
        // this), so the director must still ask for a null hit class even
        // though it now derives the weapon from the view list.
        Assert.Null(played.HitClass);
    }

    [Fact]
    public void Ingest_IgnoresAReleaseEventWhoseSourceIsAbsentFromTheViewList()
    {
        // A launcher killed the same tick it fires is still findable, because
        // UpdateViews writes a view for every agent including the dead — see
        // ranged-units design section 5.3. This test covers the other case:
        // an entity ID the view list never carried at all. That must resolve
        // to silence, not a throw, exactly like an unmapped weapon already
        // does in SoundCueMapper.
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        var exception = Record.Exception(
            () => director.Ingest([Release(entityId: 7)], []));

        Assert.Null(exception);
        Assert.Empty(player.Played);
        Assert.Empty(director.Log.Entries);
    }

    [Fact]
    public void Ingest_StillIgnoresAMeleeWeaponsReleaseEvent()
    {
        // MapRelease only maps the three ranged weapons; a melee launcher
        // resolving through the view list must stay silent rather than throw
        // or fall back to a shared cue.
        var player = new RecordingSoundPlayer(SoundBindingStatus.Ready);
        var director = new SoundDirector(logCapacity: 64, player);
        var launcher = BusogAgent(entityId: 7) with
        {
            Loadout = new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
        };

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Release(entityId: 7)], [launcher]);

        Assert.Empty(player.Played);
    }

    private static BattleEvent Release(ulong entityId) =>
        BattleEvent.NonAttack(
            sequence: 1,
            tick: 5,
            BattleEventKind.Release,
            sourceEntityId: entityId,
            targetEntityId: null,
            value: 12,
            factionId: 0);

    private static AgentView BusogAgent(ulong entityId) =>
        new(
            EntityId: entityId,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 10,
            MaximumHitPoints: 10,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(WeaponId.Busog, ArmorId.LightOrganic, ShieldId.None));

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
            ShieldId.None,
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

    [Fact]
    public void Ingest_LowersTheGainAsVoicesAccumulateWithinAFrame()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest(
        [
            Attack(1, WeaponId.Kampilan),
            Attack(2, WeaponId.Kampilan),
            Attack(3, WeaponId.Kampilan),
            Attack(4, WeaponId.Kampilan),
        ],
        []);

        Assert.Equal(4, player.Played.Count);
        Assert.Equal(SoundDirector.CueVolume, player.Played[0].Volume);
        Assert.Equal(
            SoundDirector.CueVolume / MathF.Sqrt(2),
            player.Played[1].Volume,
            tolerance: 0.0001f);
        Assert.Equal(
            SoundDirector.CueVolume / MathF.Sqrt(4),
            player.Played[3].Volume,
            tolerance: 0.0001f);
    }

    [Fact]
    public void BeginFrame_RestoresFullGainOnceTheVoicesHaveFinished()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 0.016);
        director.Ingest([Attack(1, WeaponId.Kampilan), Attack(2, WeaponId.Kampilan)], []);
        Assert.True(player.Played[1].Volume < SoundDirector.CueVolume);

        // Long enough that both clips have finished.
        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(3, WeaponId.Kampilan)], []);

        Assert.Equal(SoundDirector.CueVolume, player.Played[2].Volume);
    }

    [Fact]
    public void BeginFrame_KeepsCountingAVoiceWhoseClipIsStillSounding()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan)], []);

        // One frame at sixty per second is far shorter than a 250 ms clip, so
        // the first voice is still sounding and must still lower the gain.
        director.BeginFrame(elapsedSeconds: 0.016);
        director.Ingest([Attack(2, WeaponId.Kampilan)], []);

        Assert.Equal(
            SoundDirector.CueVolume / MathF.Sqrt(2),
            player.Played[1].Volume,
            tolerance: 0.0001f);
    }

    [Fact]
    public void Ingest_LogsARefusedCueRatherThanReportingItAsPlayed()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25,
            refusesPlayback: true);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan)], []);

        Assert.Empty(player.Played);
        Assert.Equal(
            SoundCueStatus.Refused,
            Assert.Single(director.Log.Entries).Status);
    }

    [Fact]
    public void Ingest_DoesNotChargeARefusedCueAgainstTheVoiceCount()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25,
            refusesPlayback: true);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan), Attack(2, WeaponId.Kampilan)], []);

        Assert.Equal(0, director.SoundingVoices);
        Assert.Equal(SoundDirector.CueVolume, director.NextCueGain);
    }

    [Fact]
    public void SoundingVoices_ReportsTheLiveCountForThePanel()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 0.25);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan), Attack(2, WeaponId.Kampilan)], []);

        Assert.Equal(2, director.SoundingVoices);
        Assert.Equal(
            SoundDirector.CueVolume / MathF.Sqrt(3),
            director.NextCueGain,
            tolerance: 0.0001f);
    }

    [Fact]
    public void Clear_RetiresEveryVoiceSoTheNextRoundStartsAtFullGain()
    {
        var player = new RecordingSoundPlayer(
            SoundBindingStatus.Ready,
            variantCount: 1,
            durationSeconds: 5);
        var director = new SoundDirector(logCapacity: 64, player);

        director.BeginFrame(elapsedSeconds: 1.0);
        director.Ingest([Attack(1, WeaponId.Kampilan)], []);
        Assert.Equal(1, director.SoundingVoices);

        director.Clear();

        Assert.Equal(0, director.SoundingVoices);
        Assert.Equal(SoundDirector.CueVolume, director.NextCueGain);
    }

    private sealed class RecordingSoundPlayer : ISoundPlayer
    {
        private readonly SoundBindingStatus _status;
        private readonly int _variantCount;
        private readonly double _durationSeconds;
        private readonly bool _refusesPlayback;

        public RecordingSoundPlayer(
            SoundBindingStatus status,
            int variantCount = 1,
            double durationSeconds = 0.25,
            bool refusesPlayback = false)
        {
            _status = status;
            _variantCount = variantCount;
            _durationSeconds = durationSeconds;
            _refusesPlayback = refusesPlayback;
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

        public double GetDurationSeconds(
            GameSoundId sound,
            HitClass? hitClass,
            int variantIndex) =>
            _status == SoundBindingStatus.Ready ? _durationSeconds : 0;

        public bool Play(
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

            if (_refusesPlayback)
            {
                return false;
            }

            Played.Add((sound, hitClass, variantIndex, volume));
            return true;
        }
    }
}
