using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class PresentationCoordinatorTests
{
    [Fact]
    public void ProcessTerminal_RejectsOngoingOutcomeWithoutCreatingSummary()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);

        Assert.Throws<ArgumentException>(
            () => coordinator.ProcessTerminal(
                BattleOutcome.Ongoing,
                Array.Empty<AgentView>(),
                tick: 0,
                tickRate: 20,
                seed: 1));
        Assert.Null(coordinator.Summary);
    }

    [Theory]
    [InlineData((int)ClientCommand.NextRound)]
    [InlineData((int)ClientCommand.FullReset)]
    public void ResetFor_ClearsDisposableStateAndPausesPlayback(
        int commandValue)
    {
        var command = (ClientCommand)commandValue;
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1)];
        coordinator.Playback.Play();
        coordinator.Selection.SelectNearest(
            agents,
            pointerXRaw: 0,
            pointerYRaw: 0,
            maximumDistanceSquared: 0);
        coordinator.EventFeed.Ingest([CreateEvent(1)]);
        coordinator.IngestTick([DamageEvent(2, 1), AttackEvent(3, 1, 1)], agents, default);
        coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        coordinator.ResetFor(command);

        Assert.False(coordinator.Playback.IsPlaying);
        Assert.Null(coordinator.Selection.SelectedEntityId);
        Assert.Empty(coordinator.EventFeed.Entries);
        Assert.True(coordinator.EventFeed.IsPinnedToBottom);
        Assert.Empty(coordinator.HitEffects.ActiveEffects.ToArray());
        Assert.Empty(coordinator.Blood.ActiveBursts.ToArray());
        Assert.Empty(coordinator.Blood.ActiveGroundMarks.ToArray());
        Assert.Empty(coordinator.Blood.ActiveSpurts.ToArray());
        Assert.Null(coordinator.Summary);
        Assert.Null(coordinator.Report);
        Assert.Empty(coordinator.BattleReportAccumulator.Snapshot(1).Leaderboard);
    }

    /// <summary>
    /// Mirrors <see cref="IngestTick_ForwardsEveryBatchToFeedAndHitEffects"/>,
    /// but for the battle-report accumulator, and asserts it received the raw
    /// per-tick events directly — not a truncated view through
    /// <see cref="PresentationCoordinator.EventFeed"/>, which retains only its
    /// last 200 entries.
    /// </summary>
    [Fact]
    public void IngestTick_ForwardsEveryBatchToBattleReportAccumulator()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];

        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents, default);
        coordinator.IngestTick([AttackEvent(2, 1, 2)], agents, default);

        var snapshot = coordinator.BattleReportAccumulator.Snapshot(
            terminalTick: 2);
        var attacker = Assert.Single(
            snapshot.Leaderboard,
            row => row.EntityId == 1);
        Assert.Equal(2, attacker.AttacksMade);
        Assert.Equal(2, attacker.AttacksLanded);
    }

    /// <summary>
    /// Mirrors <see cref="ResetFor_ClearsSwingsAndClashEffects"/> for the
    /// battle-report accumulator.
    /// </summary>
    [Fact]
    public void ResetFor_ClearsTheBattleReportAccumulator()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];
        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents, default);

        Assert.NotEmpty(
            coordinator.BattleReportAccumulator.Snapshot(1).Leaderboard);

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Empty(
            coordinator.BattleReportAccumulator.Snapshot(1).Leaderboard);
    }

    /// <summary>
    /// RU-38. The live call site at <c>PresentationCoordinator.cs:140</c>
    /// must pass <c>IngestTick</c>'s own <c>agents</c> parameter through to
    /// <see cref="BattleReportAccumulator.Ingest"/>, or
    /// <see cref="FactionReportTotals.HoldingCount"/> reads zero forever no
    /// matter how many warriors are actually holding — RU-16 shipped that
    /// field structurally complete and fully unit-tested but functionally
    /// dead, because <c>agents</c> defaulted to <see langword="null"/> at
    /// the accumulator and the live call site never supplied it. Asserted
    /// through <see cref="PresentationCoordinator.Report"/>, never by
    /// calling <see cref="PresentationCoordinator.BattleReportAccumulator"/>
    /// directly — a direct call bypasses the exact wiring this task exists
    /// to close.
    /// </summary>
    [Fact]
    public void ProcessTerminal_ReportsNonZeroHoldingCountForAFactionWithAHoldingWarrior()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents =
        [
            CreateAgent(1) with { Intent = AgentIntent.Holding },
            CreateAgent(2) with { FactionId = 1 },
        ];

        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents, default);
        coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        var faction0 = Assert.Single(
            coordinator.Report!.Factions, f => f.FactionId == 0);
        Assert.Equal(1, faction0.HoldingCount);
    }

    /// <summary>
    /// Mirrors
    /// <see cref="ProcessTerminal_ReportsNonZeroHoldingCountForAFactionWithAHoldingWarrior"/>
    /// with an all-<see cref="AgentIntent.Idle"/> roster, so the wiring is
    /// proven to report a real zero rather than one that would read zero
    /// with or without the roster attached.
    /// </summary>
    [Fact]
    public void ProcessTerminal_ReportsZeroHoldingCountWhenNoWarriorIsHolding()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents =
        [
            CreateAgent(1),
            CreateAgent(2) with { FactionId = 1 },
        ];

        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents, default);
        coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        Assert.All(
            coordinator.Report!.Factions,
            faction => Assert.Equal(0, faction.HoldingCount));
    }

    [Fact]
    public void IngestTick_ForwardsEveryBatchToFeedAndHitEffects()
    {
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            hitEffectCapacity: 5);
        AgentView[] agents = [CreateAgent(1)];

        coordinator.IngestTick([DamageEvent(1, 1)], agents, default);
        coordinator.IngestTick([DamageEvent(2, 1)], agents, default);

        Assert.Equal(2, coordinator.EventFeed.Entries.Count);
        Assert.Equal(2, coordinator.HitEffects.ActiveEffects.Length);
    }

    [Fact]
    public void IngestTick_ForwardsEveryBatchToBlood()
    {
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            hitEffectCapacity: 5,
            bloodBurstCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];

        coordinator.IngestTick([AttackEvent(1, 2, 1)], agents, default);
        coordinator.IngestTick([AttackEvent(2, 2, 1)], agents, default);

        Assert.Equal(2, coordinator.Blood.ActiveBursts.Length);
        Assert.Equal(2, coordinator.Blood.ActiveGroundMarks.Length);
    }

    [Fact]
    public void AdvanceEffects_AdvancesBloodAlongsideHitEffects()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];
        coordinator.IngestTick(
            [DamageEvent(1, 1), AttackEvent(2, 2, 1)],
            agents, default);

        coordinator.AdvanceEffects(0.5f);

        Assert.Empty(coordinator.HitEffects.ActiveEffects.ToArray());
        Assert.Empty(coordinator.Blood.ActiveBursts.ToArray());
        Assert.Single(coordinator.Blood.ActiveGroundMarks.ToArray());
    }

    /// <summary>
    /// The swing is the only action in progress rather than a wound already
    /// dealt, so it is the only clock the playback speed touches.
    /// </summary>
    [Fact]
    public void AdvanceEffects_ScalesOnlyTheSwingClockByTheSpeedMultiplier()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];
        coordinator.IngestTick(
            [
                DamageEvent(1, 1),
                AttackEvent(2, 2, 1),
                AttackEvent(3, 2, 1, AttackResolution.Parried),
            ],
            agents, default);

        coordinator.AdvanceEffects(0.02f, speedMultiplier: 4f);

        var swing = Assert.Single(coordinator.Swings.ActiveSwings.ToArray());
        Assert.Equal(0.08f, swing.AgeSeconds, precision: 5);
        var clash = Assert.Single(coordinator.ClashEffects.ActiveEffects.ToArray());
        Assert.Equal(0.02f, clash.AgeSeconds, precision: 5);
        var hit = Assert.Single(coordinator.HitEffects.ActiveEffects.ToArray());
        Assert.Equal(0.02f, hit.AgeSeconds, precision: 5);
        var burst = Assert.Single(coordinator.Blood.ActiveBursts.ToArray());
        Assert.Equal(0.02f, burst.AgeSeconds, precision: 5);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => coordinator.AdvanceEffects(0.02f, speedMultiplier: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => coordinator.AdvanceEffects(0.02f, speedMultiplier: -1f));
    }

    /// <summary>
    /// Ambient grass motion is not gameplay communication, so the sway clock
    /// advances on raw frame seconds even when the swing clock is scaled by
    /// the playback speed (battlefield-environment-design.md, "Wind and
    /// motion").
    /// </summary>
    [Fact]
    public void AdvanceEffects_AdvancesTheGrassSwayClockUnscaledByTheSpeedMultiplier()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);

        coordinator.AdvanceEffects(0.02f, speedMultiplier: 4f);
        coordinator.AdvanceEffects(0.5f, speedMultiplier: 1f);

        Assert.Equal(0.52f, coordinator.GrassSwayClockSeconds, precision: 5);
    }

    [Fact]
    public void ResetFor_ClearsTheGrassSwayClock()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        coordinator.AdvanceEffects(1.5f);

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Equal(0f, coordinator.GrassSwayClockSeconds);
    }

    [Fact]
    public void ResetFor_ClearsSwingsAndClashEffects()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];
        coordinator.IngestTick(
            [
                AttackEvent(1, 2, 1),
                AttackEvent(2, 1, 2, AttackResolution.ShieldBlocked),
            ],
            agents, default);

        Assert.NotEmpty(coordinator.Swings.ActiveSwings.ToArray());
        Assert.NotEmpty(coordinator.ClashEffects.ActiveEffects.ToArray());

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Empty(coordinator.Swings.ActiveSwings.ToArray());
        Assert.Empty(coordinator.ClashEffects.ActiveEffects.ToArray());
    }

    /// <summary>
    /// GPU-018. The appearance cache's declared lifetime is one battle, and
    /// <see cref="PresentationCoordinator.ResetFor"/> is the single point on
    /// disk where a battle ends — <c>ArenaGame.ResetSimulation</c> rebuilds the
    /// scenario and the simulation and then calls straight through to it, for
    /// both reset commands. So both commands must empty it, exactly as they
    /// empty every other per-battle system beside it.
    /// </summary>
    [Theory]
    [InlineData((int)ClientCommand.NextRound)]
    [InlineData((int)ClientCommand.FullReset)]
    public void ResetFor_ClearsThePawnAppearanceCache(int commandValue)
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);

        for (var ordinal = 0; ordinal < 4; ordinal++)
        {
            coordinator.PawnAppearances.Resolve(
                ordinal,
                entityId: (ulong)ordinal + 1,
                WeaponId.Kampilan,
                ShieldId.TallHardwood,
                isLeader: false);
        }

        Assert.Equal(4, coordinator.PawnAppearances.Fill);

        coordinator.ResetFor((ClientCommand)commandValue);

        Assert.Equal(0, coordinator.PawnAppearances.Fill);
    }

    /// <summary>
    /// The third point the cache declaration names — the startup scenario —
    /// needs no clear call, and this pins why: a coordinator is born holding an
    /// empty cache, so the first battle of a session starts cold for the same
    /// reason every later one does.
    /// </summary>
    [Fact]
    public void ANewCoordinatorStartsWithAnEmptyPawnAppearanceCache()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);

        Assert.Equal(0, coordinator.PawnAppearances.Fill);
    }

    /// <summary>
    /// The cache's hit, miss, and fill counters only reach a probe run if the
    /// coordinator actually hands its recorder down to the cache it builds. A
    /// silent default here would leave a probe reporting three permanent zeros
    /// and no way to tell that from a cache that never ran.
    /// </summary>
    [Fact]
    public void TheCoordinatorReportsPawnAppearanceCacheCountsThroughItsRecorder()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            renderMetricsRecorder: recorder);

        coordinator.PawnAppearances.Resolve(
            ordinal: 0,
            entityId: 7,
            WeaponId.Kampilan,
            ShieldId.None,
            isLeader: false);
        coordinator.PawnAppearances.Resolve(
            ordinal: 0,
            entityId: 7,
            WeaponId.Kampilan,
            ShieldId.None,
            isLeader: false);

        var snapshot = recorder.Snapshot();

        Assert.Equal(1, snapshot.AppearanceCacheHits);
        Assert.Equal(1, snapshot.AppearanceCacheMisses);
        Assert.Equal(1, snapshot.AppearanceCacheFills);
    }

    /// <summary>
    /// GPU-018's load-bearing assumption, pinned against the simulation itself
    /// rather than against a comment. The pawn loop addresses a cache slot by
    /// the agent's index in <see cref="BattleSimulation.Agents"/>, which is
    /// only worth doing if that index names the same warrior for a whole
    /// battle. It does: the view array is sized once at scenario creation and
    /// refilled element for element every tick, so a death clears
    /// <c>IsAlive</c> in place and never removes, compacts, or reorders an
    /// entry. If that ever changes, every ordinal after the first casualty
    /// shifts, the stored-key check turns every read into a miss, and this
    /// cache stops buying anything — so this test failing is the signal to
    /// re-derive the ordinal, not to relax the assertion.
    /// </summary>
    [Fact]
    public void TheAgentRosterKeepsEveryOrdinalStableAcrossAWholeBattle()
    {
        var simulation = BattleSimulation.Create(
            Scenario.CreateDefault(seed: 1, totalAgents: 40));
        var count = simulation.Agents.Count;
        var identitiesByOrdinal = new ulong[count];

        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            identitiesByOrdinal[ordinal] = simulation.Agents[ordinal].EntityId;
        }

        var deathsSeen = false;

        for (var tick = 0; tick < 2_000; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.Equal(count, simulation.Agents.Count);

            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                var agent = simulation.Agents[ordinal];

                Assert.Equal(identitiesByOrdinal[ordinal], agent.EntityId);
                deathsSeen |= !agent.IsAlive;
            }

            if (simulation.Outcome != BattleOutcome.Ongoing)
            {
                break;
            }
        }

        // Without a casualty the assertions above would hold trivially, and the
        // shift this test exists to catch is the one a death would cause.
        Assert.True(deathsSeen);
    }

    /// <summary>
    /// <see cref="PresentationCoordinator.IngestTick"/> must forward the
    /// completed tick's agent views to <see cref="PresentationCoordinator.Gait"/>,
    /// alongside every other presentation system it already feeds
    /// (movement-gait-animation-design.md section 3). A store the coordinator
    /// never ingests into would leave <see cref="GaitAnimationSystem.TryGetEntry"/>
    /// returning nothing for every warrior, forever.
    /// </summary>
    [Fact]
    public void IngestTick_ForwardsAgentsToTheGaitStore()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgentAt(1, xRaw: 0, yRaw: 0)];

        coordinator.IngestTick([], agents, default);

        Assert.True(coordinator.Gait.TryGetEntry(1, out var entry));
        Assert.Equal(0, entry.PreviousXRaw);
        Assert.Equal(0, entry.PreviousYRaw);
    }

    /// <summary>
    /// One <see cref="PresentationCoordinator.IngestTick"/> call advances the
    /// gait phase by exactly the distance covered between the two ingested
    /// positions — never by more, which is what a stray second call to
    /// <c>Gait.Ingest</c> from a different code path (for example
    /// <see cref="PresentationCoordinator.AdvanceEffects"/>, covered
    /// separately below) would risk introducing.
    /// </summary>
    [Fact]
    public void IngestTick_AdvancesTheGaitPhaseByExactlyOneTicksWorthOfDistance()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 0, yRaw: 0)], default);
        Assert.True(coordinator.Gait.TryGetEntry(1, out var initialEntry));

        // Half of GaitAnimationSystem.StrideCycleDistanceRaw (6000), so one
        // ingested tick of this displacement advances the phase by exactly
        // half a turn.
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 3000, yRaw: 0)], default);

        Assert.True(coordinator.Gait.TryGetEntry(1, out var advancedEntry));
        var expectedPhase = (initialEntry.PhaseTurns + 0.5f) % 1f;
        Assert.Equal(expectedPhase, advancedEntry.PhaseTurns, precision: 4);
    }

    /// <summary>
    /// The gait phase moves only with distance travelled per ingested tick,
    /// never with elapsed presentation seconds (movement-gait-animation-
    /// design.md section 4), unlike every other system
    /// <see cref="PresentationCoordinator.AdvanceEffects"/> advances.
    /// </summary>
    [Fact]
    public void AdvanceEffects_DoesNotChangeTheGaitStore()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 0, yRaw: 0)], default);
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 2000, yRaw: 0)], default);
        Assert.True(coordinator.Gait.TryGetEntry(1, out var beforeAdvance));

        coordinator.AdvanceEffects(1.5f, speedMultiplier: 4f);

        Assert.True(coordinator.Gait.TryGetEntry(1, out var afterAdvance));
        Assert.Equal(beforeAdvance, afterAdvance);
    }

    /// <summary>
    /// Mirrors <see cref="ResetFor_ClearsSwingsAndClashEffects"/> for the gait
    /// store: its declared lifetime is one battle, so both round-reset
    /// commands must empty it.
    /// </summary>
    [Theory]
    [InlineData((int)ClientCommand.NextRound)]
    [InlineData((int)ClientCommand.FullReset)]
    public void ResetFor_ClearsTheGaitStore(int commandValue)
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 0, yRaw: 0)], default);

        Assert.NotEmpty(coordinator.Gait.ActiveEntries.ToArray());

        coordinator.ResetFor((ClientCommand)commandValue);

        Assert.Empty(coordinator.Gait.ActiveEntries.ToArray());
        Assert.False(coordinator.Gait.TryGetEntry(1, out _));
    }

    /// <summary>
    /// RU-25. <see cref="PresentationCoordinator.IngestTick"/> must forward
    /// its own <c>tick</c> argument to
    /// <see cref="PresentationCoordinator.Projectiles"/> alongside the same
    /// events and agents every other system here receives — the wiring gap
    /// that left every ranged-package presentation system built but
    /// unreachable from the frame loop until this task.
    /// </summary>
    [Fact]
    public void IngestTick_ForwardsTheTickAndReleaseEventsToProjectiles()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents =
        [
            CreateAgentAt(1, xRaw: 0, yRaw: 0),
            CreateAgentAt(2, xRaw: 5000, yRaw: 0),
        ];

        coordinator.IngestTick(
            [ReleaseEvent(1, sourceEntityId: 1, targetEntityId: 2, flightTicks: 4)],
            agents,
            default,
            tick: 10);

        var flight = Assert.Single(coordinator.Projectiles.LiveFlights.ToArray());
        Assert.Equal(10, flight.LaunchTick);
        Assert.Equal(4, flight.FlightTicks);
        Assert.Equal(2ul, flight.TargetEntityId);
    }

    /// <summary>
    /// Mirrors <see cref="ResetFor_ClearsTheGaitStore"/> for the projectile
    /// store: its declared lifetime is one battle, so both round-reset
    /// commands must empty it.
    /// </summary>
    [Theory]
    [InlineData((int)ClientCommand.NextRound)]
    [InlineData((int)ClientCommand.FullReset)]
    public void ResetFor_ClearsTheProjectileStore(int commandValue)
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents =
        [
            CreateAgentAt(1, xRaw: 0, yRaw: 0),
            CreateAgentAt(2, xRaw: 5000, yRaw: 0),
        ];
        coordinator.IngestTick(
            [ReleaseEvent(1, sourceEntityId: 1, targetEntityId: 2, flightTicks: 4)],
            agents,
            default,
            tick: 1);

        Assert.NotEmpty(coordinator.Projectiles.LiveFlights.ToArray());

        coordinator.ResetFor((ClientCommand)commandValue);

        Assert.Empty(coordinator.Projectiles.LiveFlights.ToArray());
    }

    /// <summary>
    /// The spectator's <see cref="MotionIntensity"/> setting must reach gait
    /// resolution: <see cref="GaitPoseResolver.Resolve"/>'s
    /// <c>MotionIntensity.Off</c> path always resolves the neutral standing
    /// pose regardless of the store's own tracked mode, exactly the same
    /// store <see cref="PresentationCoordinator.Gait"/> exposes to the draw
    /// loop (movement-gait-animation-design.md section 9).
    /// </summary>
    [Fact]
    public void GaitPoseResolution_HonoursTheMotionIntensityAgainstTheCoordinatorsGaitStore()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        coordinator.IngestTick([], [CreateAgentAt(1, xRaw: 0, yRaw: 0)], default);
        AgentView[] moved = [CreateAgentAt(1, xRaw: 2000, yRaw: 0)];
        coordinator.IngestTick([], moved, default);
        var destination = new Dictionary<ulong, GaitPose>();

        var offPoses = GaitPoseResolver.Resolve(
            coordinator.Gait, moved, MotionIntensity.Off, destination);
        Assert.Equal(default, offPoses[1]);

        var fullPoses = GaitPoseResolver.Resolve(
            coordinator.Gait, moved, MotionIntensity.Full, destination);
        Assert.Equal(GaitMode.Run, fullPoses[1].Mode);
    }

    [Fact]
    public void ResetFor_RejectsCommandsThatDoNotResetTheRound()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => coordinator.ResetFor(ClientCommand.Play));
    }

    [Fact]
    public void ProcessTerminal_PausesAndIsIdempotent()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1)];
        BattleEvent[] finalEvents = [CreateEvent(1)];
        coordinator.Playback.Play();
        coordinator.EventFeed.Ingest(finalEvents);

        var first = coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);
        coordinator.EventFeed.Ingest(finalEvents);
        var second = coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        Assert.False(coordinator.Playback.IsPlaying);
        Assert.Same(first, second);
        Assert.Same(first, coordinator.Summary);
        Assert.Single(coordinator.EventFeed.Entries);
    }

    /// <summary>
    /// <see cref="PresentationCoordinator.ProcessTerminal"/> exposes the
    /// battle report snapshot alongside the match summary, and is idempotent
    /// about it in the same way — a later call with the same terminal
    /// arguments does not replace the already-materialized snapshot.
    /// </summary>
    [Fact]
    public void ProcessTerminal_SetsTheReportAlongsideTheSummary()
    {
        var coordinator = new PresentationCoordinator(eventCapacity: 5);
        AgentView[] agents = [CreateAgent(1), CreateAgent(2)];
        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents, default);

        Assert.Null(coordinator.Report);

        var summary = coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        Assert.NotNull(coordinator.Report);
        Assert.Equal(1, coordinator.Report!.TerminalTick);
        Assert.NotEmpty(coordinator.Report.Leaderboard);

        var firstReport = coordinator.Report;
        coordinator.ProcessTerminal(
            BattleOutcome.Faction0Victory,
            agents,
            tick: 1,
            tickRate: 20,
            seed: 1);

        Assert.Same(firstReport, coordinator.Report);
        Assert.Same(summary, coordinator.Summary);
    }

    private static AgentView CreateAgent(ulong entityId) =>
        CreateAgentAt(entityId, xRaw: 0, yRaw: 0);

    private static AgentView CreateAgentAt(ulong entityId, int xRaw, int yRaw) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent CreateEvent(long sequence) =>
        BattleEvent.NonAttack(
            sequence,
            tick: 1,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: 0,
            factionId: 0);

    private static BattleEvent AttackEvent(
        long sequence,
        ulong sourceEntityId,
        ulong targetEntityId,
        AttackResolution resolution = AttackResolution.Landed) =>
        BattleEvent.Attack(
            sequence,
            tick: sequence,
            sourceEntityId,
            targetEntityId,
            damage: resolution == AttackResolution.Landed ? 10 : 0,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Chest,
            resolution);

    private static BattleEvent DamageEvent(
        long sequence,
        ulong targetEntityId) =>
        BattleEvent.NonAttack(
            sequence,
            tick: sequence,
            BattleEventKind.Damage,
            sourceEntityId: targetEntityId,
            targetEntityId: targetEntityId,
            value: 10,
            factionId: null);

    private static BattleEvent ReleaseEvent(
        long sequence,
        ulong sourceEntityId,
        ulong? targetEntityId,
        int flightTicks) =>
        BattleEvent.NonAttack(
            sequence,
            tick: sequence,
            BattleEventKind.Release,
            sourceEntityId,
            targetEntityId,
            value: flightTicks,
            factionId: null);
}
