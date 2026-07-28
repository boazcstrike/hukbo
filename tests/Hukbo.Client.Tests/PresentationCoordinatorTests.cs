using Hukbo.Client.Presentation;
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
        coordinator.IngestTick([DamageEvent(2, 1), AttackEvent(3, 1, 1)], agents);
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

        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents);
        coordinator.IngestTick([AttackEvent(2, 1, 2)], agents);

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
        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents);

        Assert.NotEmpty(
            coordinator.BattleReportAccumulator.Snapshot(1).Leaderboard);

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Empty(
            coordinator.BattleReportAccumulator.Snapshot(1).Leaderboard);
    }

    [Fact]
    public void IngestTick_ForwardsEveryBatchToFeedAndHitEffects()
    {
        var coordinator = new PresentationCoordinator(
            eventCapacity: 5,
            hitEffectCapacity: 5);
        AgentView[] agents = [CreateAgent(1)];

        coordinator.IngestTick([DamageEvent(1, 1)], agents);
        coordinator.IngestTick([DamageEvent(2, 1)], agents);

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

        coordinator.IngestTick([AttackEvent(1, 2, 1)], agents);
        coordinator.IngestTick([AttackEvent(2, 2, 1)], agents);

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
            agents);

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
            agents);

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
            agents);

        Assert.NotEmpty(coordinator.Swings.ActiveSwings.ToArray());
        Assert.NotEmpty(coordinator.ClashEffects.ActiveEffects.ToArray());

        coordinator.ResetFor(ClientCommand.NextRound);

        Assert.Empty(coordinator.Swings.ActiveSwings.ToArray());
        Assert.Empty(coordinator.ClashEffects.ActiveEffects.ToArray());
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
        coordinator.IngestTick([AttackEvent(1, 1, 2)], agents);

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
        new(
            entityId,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
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
}
