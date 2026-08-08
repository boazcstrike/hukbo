using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Audio;

/// <summary>
/// Decides what the battle sounds like. Reads the same per-tick
/// <see cref="BattleEvent"/> buffer the battle log and hit effects read, maps
/// each event to a slot, applies mute and the frame budget, hands survivors to
/// the player, and records every decision in <see cref="Log"/>.
/// </summary>
/// <remarks>
/// Presentation only. Nothing here writes to simulation state, and nothing here
/// can change a hash: <c>Hukbo.Core</c> does not know this class exists.
/// </remarks>
internal sealed class SoundDirector
{
    /// <summary>
    /// The gain a cue plays at when nothing else is sounding. Every other cue
    /// is this value divided by the square root of the voices already
    /// sounding.
    /// </summary>
    /// <remarks>
    /// A provisional tuning value, not a measurement of anything. It was
    /// lowered from 0.8 because 0.8 still let the busiest measured case — 500
    /// agents at normal speed — reach +1.6 dBFS and flatten two samples. At
    /// 0.65 the peak stays under full scale in every measured configuration,
    /// from −6.1 to −0.2 dBFS, with nothing flattened. See
    /// <c>docs/research/SOUND-CAPACITY-MEASUREMENTS.md</c>. Individual files
    /// should be normalised by the owner rather than balanced here.
    /// </remarks>
    internal const float CueVolume = 0.65f;

    private readonly SoundCueBudget _budget;
    private readonly SoundVoiceLedger _voices = new();
    private readonly DiagnosticLog _diagnostics;
    private readonly Dictionary<ulong, AgentView> _agentsById = [];

    public SoundDirector(
        int logCapacity,
        ISoundPlayer? player = null,
        SoundCueBudget? budget = null,
        DiagnosticLog? diagnostics = null)
    {
        Log = new SoundCueLog(logCapacity);
        Player = player ?? new SilentSoundPlayer();
        _budget = budget ?? new SoundCueBudget();
        _diagnostics = diagnostics ?? DiagnosticLog.Disabled;
    }

    public ISoundPlayer Player { get; private set; }

    public SoundCueLog Log { get; }

    public bool IsMuted { get; private set; }

    /// <summary>
    /// Voices still sounding. Presentation state, shown in the sound panel so a
    /// spectator can see why a busy fight is quieter per blow than a duel.
    /// </summary>
    public int SoundingVoices => _voices.SoundingVoices;

    /// <summary>
    /// The gain the next cue would play at. Presentation state, shown beside
    /// <see cref="SoundingVoices"/>.
    /// </summary>
    public float NextCueGain => _voices.GetGainForNextCue(CueVolume);

    /// <summary>
    /// Replaces the player once real content has been loaded. The log is kept:
    /// rows recorded before loading are still accurate evidence of what the
    /// game did.
    /// </summary>
    public void AttachPlayer(ISoundPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Player = player;
        LogBindings(player);
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _diagnostics.Write(
            LogLevel.Information,
            LogChannel.Audio,
            LogEvents.AudioMuteToggled,
            "muted",
            IsMuted);
    }

    /// <summary>
    /// Clears the frame's playback budget and retires the voices whose clips
    /// finished during the frame. Call once per frame, before the
    /// <see cref="Ingest"/> calls for the ticks that frame advances.
    /// </summary>
    /// <param name="elapsedSeconds">
    /// The frame's real elapsed time. Passed in rather than read from a clock
    /// so nothing in this class depends on the wall clock, which keeps it
    /// testable and keeps the dependence visible at the call site.
    /// </param>
    public void BeginFrame(double elapsedSeconds)
    {
        _budget.BeginFrame();
        _voices.Advance(elapsedSeconds);
        _diagnostics.Write(
            LogLevel.Trace,
            LogChannel.Audio,
            LogEvents.AudioFrame,
            "elapsedSeconds",
            elapsedSeconds,
            "voices",
            _voices.SoundingVoices,
            "nextGain",
            NextCueGain);
    }

    /// <summary>
    /// Processes one tick's events in emission order. <paramref name="agents"/>
    /// is the same view list <see cref="Presentation.PresentationCoordinator"/>
    /// already holds; it is required because a classless
    /// <see cref="BattleEventKind.Release"/> event cannot name its own weapon
    /// — <see cref="BattleEvent.NonAttack"/> forces every combat-context field
    /// to <see langword="null"/> by construction, per ranged-units design
    /// section 5.3 — so the launching weapon is read from the source agent's
    /// <see cref="Combat.CombatLoadout.Weapon"/> instead. <c>UpdateViews</c>
    /// writes a view for every agent including the dead, so the lookup still
    /// succeeds for a launcher killed the same tick; an entity absent from
    /// <paramref name="agents"/> simply produces no cue.
    /// </summary>
    public void Ingest(IReadOnlyList<BattleEvent> events, IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            var sound = battleEvent.Kind == BattleEventKind.Release
                ? ResolveReleaseSound(battleEvent)
                : SoundCueMapper.Map(battleEvent);
            if (sound is { } resolvedSound)
            {
                // Derive the hit class from the slot that was mapped, never
                // from the event. Do not revert this to reading
                // battleEvent.HitLocation: a shield-blocked attack still
                // carries a hit location, so an event-derived hit class would
                // look a classless clash slot up as (slot, HitClass.Skull),
                // which is registered nowhere. That resolves Missing forever —
                // no crash, no failing test, and no complaint in the panel,
                // because the slot's own binding still reads Ready. One
                // predicate now decides both how SoundLibrary registers a slot
                // and how the director asks for it.
                // Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation
                // is the guard.
                var hitClass = SoundCatalog.IsHitLocationDriven(resolvedSound)
                    ? HitClassCatalog.FromBodyPart(battleEvent.HitLocation!.Value)
                    : (HitClass?)null;
                Resolve(resolvedSound, hitClass, battleEvent.Tick, battleEvent.SourceEntityId);
            }
        }
    }

    /// <summary>
    /// Resolves the launching weapon for a classless
    /// <see cref="BattleEventKind.Release"/> event from the source agent's
    /// view rather than from the event, then hands it to
    /// <see cref="SoundCueMapper.MapRelease"/>, the hook RU-14 exposed for
    /// exactly this caller. Returns <see langword="null"/> — no cue, no
    /// throw — when the source entity is absent from the view list.
    /// </summary>
    private GameSoundId? ResolveReleaseSound(BattleEvent battleEvent) =>
        _agentsById.TryGetValue(battleEvent.SourceEntityId, out var launcher)
            ? SoundCueMapper.MapRelease(launcher.Loadout.Weapon)
            : null;

    /// <summary>
    /// Requests a cue that no simulation event produced, such as a UI click.
    /// It carries no hit location and no source entity, so variant selection
    /// falls back to the fixed entity ID zero — deterministic, and immaterial
    /// for the single-file interface cues that use this path today.
    /// </summary>
    public void RequestCue(GameSoundId sound, long tick) =>
        Resolve(sound, hitClass: null, tick, sourceEntityId: 0UL);

    public void Clear()
    {
        Log.Clear();
        _voices.Clear();
    }

    private void Resolve(
        GameSoundId sound,
        HitClass? hitClass,
        long tick,
        ulong sourceEntityId)
    {
        var status = Player.GetStatus(sound, hitClass);
        if (status != SoundBindingStatus.Ready)
        {
            // A broken binding outranks mute and the budget: it is the one
            // thing the owner needs to see in order to fix the folder.
            Record(
                tick,
                sound,
                hitClass,
                status == SoundBindingStatus.LoadFailed
                    ? SoundCueStatus.LoadFailed
                    : SoundCueStatus.Missing,
                variantIndex: -1,
                gain: 0f,
                _voices.SoundingVoices);
            return;
        }

        if (IsMuted)
        {
            Record(
                tick,
                sound,
                hitClass,
                SoundCueStatus.Muted,
                variantIndex: -1,
                gain: 0f,
                _voices.SoundingVoices);
            return;
        }

        if (!_budget.TryConsume(sound))
        {
            Record(
                tick,
                sound,
                hitClass,
                SoundCueStatus.Suppressed,
                variantIndex: -1,
                gain: 0f,
                _voices.SoundingVoices);
            return;
        }

        var variantCount = Player.GetVariantCount(sound, hitClass);
        var variantIndex = SoundVariantSelector.Select(tick, sourceEntityId, variantCount);

        // Scale against the voices already sounding. Without this the mix sums
        // past full scale and the output stage flattens the peaks, which is
        // heard as a continuous rasp rather than as separate blows.
        //
        // Captured before Add so the logged figure is the count the gain was
        // derived from, not that count plus this cue.
        var voicesBefore = _voices.SoundingVoices;
        var gain = _voices.GetGainForNextCue(CueVolume);
        if (!Player.Play(sound, hitClass, variantIndex, gain))
        {
            Record(
                tick,
                sound,
                hitClass,
                SoundCueStatus.Refused,
                variantIndex,
                gain,
                voicesBefore);
            return;
        }

        _voices.Add(Player.GetDurationSeconds(sound, hitClass, variantIndex));
        Record(
            tick,
            sound,
            hitClass,
            SoundCueStatus.Played,
            variantIndex,
            gain,
            voicesBefore);
    }

    /// <summary>
    /// The single place a cue decision is recorded. The on-screen log gets the
    /// row a spectator can read; the debug log gets the same decision plus the
    /// variant, the gain, and the voice count, which are the three figures that
    /// explain why a blow in a crowded melee sounds different from the same
    /// blow in a duel and which no panel shows.
    /// </summary>
    private void Record(
        long tick,
        GameSoundId sound,
        HitClass? hitClass,
        SoundCueStatus status,
        int variantIndex,
        float gain,
        int voices)
    {
        Log.Append(tick, sound, status);

        if (!_diagnostics.IsEnabledFor(LogLevel.Debug, LogChannel.Audio))
        {
            return;
        }

        // The tick is not repeated as a payload field: the line's own `t`
        // already carries it.
        _diagnostics.Write(
            LogLevel.Debug,
            LogChannel.Audio,
            LogEvents.AudioCue,
            "slot",
            sound.ToString(),
            "hitClass",
            hitClass?.ToString(),
            "status",
            status.ToString(),
            "variant",
            variantIndex,
            "gain",
            gain,
            "voices",
            voices);
    }

    /// <summary>
    /// Reports the whole binding table once, when real content replaces the
    /// silent player. A slot with no file is otherwise invisible until the
    /// moment it fails to sound.
    /// </summary>
    private void LogBindings(ISoundPlayer player)
    {
        if (!_diagnostics.IsEnabledFor(LogLevel.Information, LogChannel.Assets))
        {
            return;
        }

        var ready = 0;
        var missing = 0;
        var failed = 0;
        foreach (var binding in player.Bindings)
        {
            switch (binding.Status)
            {
                case SoundBindingStatus.Ready:
                    ready++;
                    break;

                case SoundBindingStatus.LoadFailed:
                    failed++;
                    _diagnostics.Write(
                        LogLevel.Warning,
                        LogChannel.Assets,
                        LogEvents.AssetsSoundLoadFailed,
                        "slot",
                        binding.Sound.ToString(),
                        "file",
                        binding.FileName,
                        "variants",
                        binding.VariantCount);
                    break;

                default:
                    missing++;
                    _diagnostics.Write(
                        LogLevel.Warning,
                        LogChannel.Assets,
                        LogEvents.AssetsSoundMissing,
                        "slot",
                        binding.Sound.ToString(),
                        "file",
                        binding.FileName);
                    break;
            }
        }

        _diagnostics.Write(
            LogLevel.Information,
            LogChannel.Assets,
            LogEvents.AssetsSoundScanned,
            "directory",
            player.DirectoryPath,
            "slots",
            player.Bindings.Count,
            "ready",
            ready,
            "missing",
            missing,
            "loadFailed",
            failed);

        _diagnostics.Write(
            LogLevel.Information,
            LogChannel.Audio,
            LogEvents.AudioPlayerAttached,
            "player",
            player.GetType().Name,
            "directory",
            player.DirectoryPath,
            "ready",
            ready);
    }
}
