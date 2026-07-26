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
        coordinator.IngestTick([DamageEvent(2, 1)], agents);
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
        Assert.Null(coordinator.Summary);
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
                WeaponId.GreatBlade,
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
