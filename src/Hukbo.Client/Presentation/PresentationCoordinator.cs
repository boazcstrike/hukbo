using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class PresentationCoordinator
{
    public PresentationCoordinator(
        int eventCapacity,
        int hitEffectCapacity = 256,
        int bloodBurstCapacity = 256)
    {
        EventFeed = new BattleEventFeed(eventCapacity);
        HitEffects = new HitEffectSystem(hitEffectCapacity);
        Blood = new BloodEffectSystem(bloodBurstCapacity);
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

    public MatchSummary? Summary { get; private set; }

    public void IngestTick(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        EventFeed.Ingest(events);
        HitEffects.Ingest(events, agents);
        Blood.Ingest(events, agents);
    }

    public void AdvanceEffects(float elapsedSeconds)
    {
        HitEffects.Advance(elapsedSeconds);
        Blood.Advance(elapsedSeconds);
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
        Summary = null;
    }
}
