using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class PresentationCoordinator
{
    public PresentationCoordinator(
        int eventCapacity,
        int hitEffectCapacity = 256,
        int bloodBurstCapacity = 256,
        int swingCapacity = 256,
        int clashEffectCapacity = 256,
        int trampleMarkCapacity = TrampleMarkSystem.Capacity,
        int dustPuffCapacity = DustEffectSystem.Capacity)
    {
        EventFeed = new BattleEventFeed(eventCapacity);
        HitEffects = new HitEffectSystem(hitEffectCapacity);
        Blood = new BloodEffectSystem(bloodBurstCapacity);
        Swings = new SwingAnimationSystem(swingCapacity);
        ClashEffects = new ClashEffectSystem(clashEffectCapacity);
        Trample = new TrampleMarkSystem(trampleMarkCapacity);
        Dust = new DustEffectSystem(dustPuffCapacity);
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

    /// <summary>
    /// The fixed-capacity, oldest-replaced list of trample marks fed by
    /// <c>Death</c> events (battlefield-environment-design.md, "Trampled and
    /// sparse areas"). Unlike every other system here, its marks never age
    /// out in <see cref="AdvanceEffects"/> — they persist as visible battle
    /// wear until <see cref="ResetFor"/> clears the scenario.
    /// </summary>
    public TrampleMarkSystem Trample { get; }

    /// <summary>
    /// The fixed-capacity, event-driven dust puffs (battlefield-environment-
    /// design.md, "Dust and disturbed vegetation", VIS-029). Unlike
    /// <see cref="Trample"/>, its puffs age out on the same unscaled
    /// presentation clock as <see cref="HitEffects"/> and <see cref="Blood"/>.
    /// </summary>
    public DustEffectSystem Dust { get; }

    public MatchSummary? Summary { get; private set; }

    /// <summary>
    /// The client-side clock
    /// <see cref="Hukbo.Client.Rendering.GrassSway.GrassSwayOffset"/> reads
    /// (battlefield-environment-design.md, "Wind and motion", R-W5.4).
    /// Advanced in <see cref="AdvanceEffects"/> on unscaled frame seconds —
    /// ambient grass motion is not gameplay communication, so it never scales
    /// with the playback speed, unlike <see cref="Swings"/>. It never touches
    /// the simulation, no simulation value depends on it, and nothing it
    /// computes is ever stored, hashed, or snapshotted.
    /// </summary>
    public float GrassSwayClockSeconds { get; private set; }

    public void IngestTick(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        EventFeed.Ingest(events);
        HitEffects.Ingest(events, agents);
        Blood.Ingest(events, agents);
        Swings.Ingest(events, agents);
        ClashEffects.Ingest(events, agents);
        Trample.Ingest(events, agents);
        Dust.Ingest(events, agents);
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
        Dust.Advance(elapsedSeconds);
        Swings.Advance(elapsedSeconds * speedMultiplier);
        GrassSwayClockSeconds += elapsedSeconds;
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
        Trample.Clear();
        Dust.Clear();
        GrassSwayClockSeconds = 0f;
        Summary = null;
    }
}
