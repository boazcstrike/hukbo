using Hukbo.Client.Audio;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class PresentationCoordinator
{
    /// <param name="renderMetricsRecorder">
    /// Where <see cref="PawnAppearances"/> reports its hit, miss, and fill
    /// counts. Defaulted to <see cref="NullRenderMetricsRecorder.Instance"/>
    /// so the many presentation tests that build a coordinator without caring
    /// about measurement keep their existing call, and so a normal run — where
    /// the render probe is off — pays only no-op calls.
    /// </param>
    public PresentationCoordinator(
        int eventCapacity,
        int hitEffectCapacity = PawnAppearanceCache.Capacity,
        int bloodBurstCapacity = PawnAppearanceCache.Capacity,
        int bloodGroundMarkCapacity = PawnAppearanceCache.Capacity,
        int bloodSpurtCapacity = PawnAppearanceCache.Capacity,
        int swingCapacity = 256,
        int clashEffectCapacity = PawnAppearanceCache.Capacity,
        int trampleMarkCapacity = TrampleMarkSystem.Capacity,
        int dustPuffCapacity = DustEffectSystem.Capacity,
        int gaitCapacity = PawnAppearanceCache.Capacity,
        int attackCapacity = PawnAppearanceCache.Capacity,
        IRenderMetricsRecorder? renderMetricsRecorder = null)
    {
        PawnAppearances = new PawnAppearanceCache(
            renderMetricsRecorder ?? NullRenderMetricsRecorder.Instance);
        EventFeed = new BattleEventFeed(eventCapacity);
        HitEffects = new HitEffectSystem(hitEffectCapacity);
        Blood = new BloodEffectSystem(
            bloodBurstCapacity,
            bloodGroundMarkCapacity,
            bloodSpurtCapacity);
        Swings = new SwingAnimationSystem(swingCapacity);
        ClashEffects = new ClashEffectSystem(clashEffectCapacity);
        Trample = new TrampleMarkSystem(trampleMarkCapacity);
        Dust = new DustEffectSystem(dustPuffCapacity);
        Gait = new GaitAnimationSystem(gaitCapacity);
        AttackFrames = new AttackFrameCoordinator(attackCapacity);
        DefenderReactions = new DefenderReactionSystem(attackCapacity);
        BattleReportAccumulator = new BattleReportAccumulator();
    }

    public PlaybackController Playback { get; } = new();

    public AgentSelection Selection { get; } = new();

    public BattleEventFeed EventFeed { get; }

    /// <summary>
    /// The bounded per-battle appearance cache (GPU-017) the pawn loop reads
    /// through (GPU-018). It lives here rather than on the game class because
    /// its declared lifetime is one battle, and this is the type that already
    /// owns every other piece of state with that lifetime: <see cref="Clear"/>
    /// is called from <see cref="ResetFor"/> alongside the effect systems, so
    /// there is no second place a future reset path could forget.
    /// </summary>
    public PawnAppearanceCache PawnAppearances { get; }

    public HitEffectSystem HitEffects { get; }

    /// <summary>
    /// Keyed on <c>Attack</c> rather than <c>Damage</c>, so it sits alongside
    /// <see cref="HitEffects"/> instead of replacing it.
    /// </summary>
    public BloodEffectSystem Blood { get; }

    /// <summary>
    /// The legacy in-flight weapon swings, retained for compatibility while
    /// the attack renderer migrates. The live event route no longer ingests
    /// or advances this store.
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

    /// <summary>
    /// The per-entity previous-position and stride-phase store behind drawn
    /// legs and feet (movement-gait-animation-design.md section 3). Ingested
    /// from the completed-tick agent views alone, unlike every other system
    /// here — it consumes no events, because its whole signal is the change in
    /// a warrior's authoritative position between one ingested tick and the
    /// next. It is not advanced in <see cref="AdvanceEffects"/>: its phase
    /// moves only with distance travelled per ingested tick, never with
    /// elapsed presentation seconds (design section 4), so it has nothing to
    /// do on that clock.
    /// </summary>
    public GaitAnimationSystem Gait { get; }

    public AttackFrameCoordinator AttackFrames { get; }

    public AttackContactDispatcher AttackContacts => AttackFrames.Dispatcher;

    public AttackAnimationSystem AttackAnimations => AttackFrames.Animations;

    public DefenderReactionSystem DefenderReactions { get; }

    public bool HasUndrawnAttackContacts => AttackFrames.HasUndrawnContacts;

    public bool HasTerminalAttackPresentation
    {
        get
        {
            if (HasUndrawnAttackContacts)
            {
                return true;
            }

            var reactions = DefenderReactions.ActiveReactions;
            for (var index = 0; index < reactions.Length; index++)
            {
                var reaction = reactions[index];
                if (reaction.IsLethal &&
                    reaction.AgeSeconds < DefenderReaction.LethalHoldSeconds)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Accumulates the per-unit, per-faction, and battle-wide statistics
    /// behind the post-battle battle report. Fed here from the raw per-tick
    /// event list, never through <see cref="EventFeed"/> — that feed
    /// truncates to its last 200 entries, which would silently corrupt every
    /// statistic derived from it.
    /// </summary>
    public BattleReportAccumulator BattleReportAccumulator { get; }

    public MatchSummary? Summary { get; private set; }

    /// <summary>
    /// The client-side clock
    /// <see cref="Hukbo.Client.Rendering.GrassSway.GrassSwayOffset"/> reads
    /// (battlefield-environment-design.md, "Wind and motion", R-W5.4).
    /// Advanced in <see cref="AdvanceEffects"/> on unscaled frame seconds —
    /// ambient grass motion is not gameplay communication, so it never scales
    /// with the playback speed, unlike <see cref="AttackAnimations"/>. It never
    /// touches
    /// the simulation, no simulation value depends on it, and nothing it
    /// computes is ever stored, hashed, or snapshotted.
    /// </summary>
    public float GrassSwayClockSeconds { get; private set; }

    /// <summary>
    /// The immutable battle report snapshot, set once a battle reaches a
    /// terminal outcome by <see cref="ProcessTerminal"/>, alongside
    /// <see cref="Summary"/>.
    /// </summary>
    public BattleReport? Report { get; private set; }

    public void IngestTick(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents,
        FactionCombatMetrics tickCombatByFaction)
    {
        EventFeed.Ingest(events);
        BattleReportAccumulator.Ingest(events, tickCombatByFaction);
        AttackFrames.Ingest(events);
        Trample.Ingest(events, agents);
        Dust.Ingest(events, agents);

        // Gait consumes no events, unlike every system above — its whole
        // signal is the agent views' own XRaw/YRaw, per design section 3.
        Gait.Ingest(agents);
    }

    /// <param name="speedMultiplier">
    /// The playback speed. It scales the attack clock and nothing else: the
    /// simulation issues attacks at the playback speed, so at 4x an unscaled
    /// attack would still be mid-recovery when the next blow landed and every
    /// warrior would read as permanently mid-attack. Contact effects advance
    /// on unscaled presentation time while playback is active.
    /// </param>
    public void AdvanceEffects(
        float elapsedSeconds,
        float speedMultiplier = 1f,
        bool advanceContacts = true)
    {
        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        if (advanceContacts)
        {
            HitEffects.Advance(elapsedSeconds);
            Blood.Advance(elapsedSeconds);
            ClashEffects.Advance(elapsedSeconds);
            DefenderReactions.Advance(elapsedSeconds);
        }

        AttackFrames.Advance(
            elapsedSeconds,
            speedMultiplier,
            advanceContacts);
        Dust.Advance(elapsedSeconds);
        GrassSwayClockSeconds += elapsedSeconds;
    }

    /// <summary>
    /// Starts all contact consumers from the same bundle in the same frame.
    /// No individual channel observes the raw attack/death event stream.
    /// </summary>
    public int ReleaseAttackContactsForDraw(
        IReadOnlyList<AgentView> agents,
        MotionIntensity motionIntensity,
        SoundDirector soundDirector,
        bool allowRelease)
    {
        ArgumentNullException.ThrowIfNull(soundDirector);

        var released = AttackFrames.ReleaseForDraw(
            agents,
            motionIntensity,
            allowRelease);
        for (var index = 0; index < released.Length; index++)
        {
            var contact = released[index];
            if (!AttackFrames.TryGetAgent(
                    contact.AttackerEntityId,
                    out var attacker) ||
                !AttackFrames.TryGetAgent(
                    contact.DefenderEntityId,
                    out var defender))
            {
                throw new InvalidOperationException(
                    $"Attack contact {contact.Sequence} could not resolve its frame agents.");
            }

            HitEffects.StartContact(contact, defender);
            Blood.StartContact(contact, attacker, defender);
            ClashEffects.StartContact(contact, attacker, defender);
            DefenderReactions.StartContact(contact, attacker, defender);
            soundDirector.StartContact(contact);
        }

        return released.Length;
    }

    public int AcknowledgeAttackDraw() => AttackFrames.AcknowledgeDraw();

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
        Report ??= BattleReportAccumulator.Snapshot(tick);
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

        // GPU-018. The appearance cache's declared lifetime is one battle, and
        // this is the only method on disk that ends one: ArenaGame's
        // ResetSimulation rebuilds the scenario and the simulation and then
        // calls straight through to here, for both NextRound and FullReset.
        // The startup scenario is the third point the declaration names, and it
        // needs no call — the cache is constructed empty with the coordinator.
        //
        // Clearing is a lifetime obligation, not a correctness one. A stale
        // slot could never be returned as a wrong appearance: the stored key is
        // compared on every read, so an ordinal that has come to mean a
        // different agent misses and recomputes. What the clear buys is that a
        // new battle's Fill and its hit rate describe that battle alone.
        PawnAppearances.Clear();
        EventFeed.Clear();
        BattleReportAccumulator.Clear();
        HitEffects.Clear();
        Blood.Clear();
        Swings.Clear();
        AttackFrames.Clear();
        DefenderReactions.Clear();
        ClashEffects.Clear();
        Trample.Clear();
        Dust.Clear();
        Gait.Clear();
        GrassSwayClockSeconds = 0f;
        Summary = null;
        Report = null;
    }
}
