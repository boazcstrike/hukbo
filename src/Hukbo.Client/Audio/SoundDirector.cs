using Hukbo.Core.Simulation;

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
    /// A provisional tuning value, not a measurement. Individual files should be
    /// normalised by the owner rather than balanced here.
    /// </summary>
    internal const float CueVolume = 0.8f;

    private readonly SoundCueBudget _budget;

    public SoundDirector(
        int logCapacity,
        ISoundPlayer? player = null,
        SoundCueBudget? budget = null)
    {
        Log = new SoundCueLog(logCapacity);
        Player = player ?? new SilentSoundPlayer();
        _budget = budget ?? new SoundCueBudget();
    }

    public ISoundPlayer Player { get; private set; }

    public SoundCueLog Log { get; }

    public bool IsMuted { get; private set; }

    /// <summary>
    /// Replaces the player once real content has been loaded. The log is kept:
    /// rows recorded before loading are still accurate evidence of what the
    /// game did.
    /// </summary>
    public void AttachPlayer(ISoundPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Player = player;
    }

    public void ToggleMute() => IsMuted = !IsMuted;

    /// <summary>
    /// Clears the frame's playback budget. Call once per frame, before the
    /// <see cref="Ingest"/> calls for the ticks that frame advances.
    /// </summary>
    public void BeginFrame() => _budget.BeginFrame();

    /// <summary>
    /// Processes one tick's events in emission order.
    /// </summary>
    public void Ingest(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (SoundCueMapper.Map(battleEvent) is { } sound)
            {
                Resolve(sound, battleEvent.Tick);
            }
        }
    }

    /// <summary>
    /// Requests a cue that no simulation event produced, such as a UI click.
    /// </summary>
    public void RequestCue(GameSoundId sound, long tick) =>
        Resolve(sound, tick);

    public void Clear() => Log.Clear();

    private void Resolve(GameSoundId sound, long tick)
    {
        var status = Player.GetStatus(sound);
        if (status != SoundBindingStatus.Ready)
        {
            // A broken binding outranks mute and the budget: it is the one
            // thing the owner needs to see in order to fix the folder.
            Log.Append(
                tick,
                sound,
                status == SoundBindingStatus.LoadFailed
                    ? SoundCueStatus.LoadFailed
                    : SoundCueStatus.Missing);
            return;
        }

        if (IsMuted)
        {
            Log.Append(tick, sound, SoundCueStatus.Muted);
            return;
        }

        if (!_budget.TryConsume(sound))
        {
            Log.Append(tick, sound, SoundCueStatus.Suppressed);
            return;
        }

        Player.Play(sound, CueVolume);
        Log.Append(tick, sound, SoundCueStatus.Played);
    }
}
