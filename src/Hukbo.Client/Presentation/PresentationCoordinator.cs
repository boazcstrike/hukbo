using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class PresentationCoordinator
{
    public PresentationCoordinator(
        int eventCapacity,
        int hitEffectCapacity = 256,
        int bloodBurstCapacity = 256,
        int swingCapacity = 256,
        int clashEffectCapacity = 256)
    {
        EventFeed = new BattleEventFeed(eventCapacity);
        HitEffects = new HitEffectSystem(hitEffectCapacity);
        Blood = new BloodEffectSystem(bloodBurstCapacity);
        Swings = new SwingAnimationSystem(swingCapacity);
        ClashEffects = new ClashEffectSystem(clashEffectCapacity);
    }

    public PlaybackController Playback { get; } = new();

    public AgentSelection Selection { get; } = new();

    public BattleEventFeed EventFeed { get; }

    public HitEffectSystem HitEffects { get; }

    /// <summary>
    /// Keyed on <c>Attack</c> rather than <c>Damage</c>, so it sits alongside
    /// <see cref="HitEffects"/> instead of replacing it.
    /// </summary>
    public BloodEffectSystem Blood { get; }

    /// <summary>
    /// The in-flight weapon swings. Its clock is the only one scaled by the
    /// playback speed.
    /// </summary>
    public SwingAnimationSystem Swings { get; }

    /// <summary>
    /// The crosses where two weapons, or a weapon and a shield, met.
    /// </summary>
    public ClashEffectSystem ClashEffects { get; }

    public MatchSummary? Summary { get; private set; }

    public void IngestTick(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        EventFeed.Ingest(events);
        HitEffects.Ingest(events, agents);
        Blood.Ingest(events, agents);
        Swings.Ingest(events, agents);
        ClashEffects.Ingest(events, agents);
    }

    /// <param name="speedMultiplier">
    /// The playback speed. It scales the swing clock and nothing else: the
    /// simulation issues attacks at the playback speed, so at 4x an unscaled
    /// swing would still be mid-recovery when the next blow landed and every
    /// warrior would read as permanently mid-swing. The hit and blood effects
    /// are wounds already dealt rather than actions in progress, and they keep
    /// advancing on unscaled presentation time.
    /// </param>
    public void AdvanceEffects(float elapsedSeconds, float speedMultiplier = 1f)
    {
        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        HitEffects.Advance(elapsedSeconds);
        Blood.Advance(elapsedSeconds);
        ClashEffects.Advance(elapsedSeconds);
        Swings.Advance(elapsedSeconds * speedMultiplier);
    }

    public MatchSummary ProcessTerminal(
        BattleOutcome outcome,
        IReadOnlyList<AgentView> agents,
        long tick,
        int tickRate,
        ulong seed)
    {
        if (outcome == BattleOutcome.Ongoing)
        {
            throw new ArgumentException(
                "Terminal processing requires a completed battle.",
                nameof(outcome));
        }

        Playback.Pause();
        Summary ??= MatchSummaryFactory.Create(
            outcome,
            agents,
            tick,
            tickRate,
            seed);
        return Summary;
    }

    public void ResetFor(ClientCommand resetCommand)
    {
        if (resetCommand is not ClientCommand.NextRound and
            not ClientCommand.FullReset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetCommand),
                resetCommand,
                "Only round reset commands can clear presentation state.");
        }

        Playback.Pause();
        Selection.Clear();
        EventFeed.Clear();
        HitEffects.Clear();
        Blood.Clear();
        Swings.Clear();
        ClashEffects.Clear();
        Summary = null;
    }
}
