using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class AttackFrameCoordinatorTests
{
    [Fact]
    public void ReleaseForDraw_LatchesOneContactPerAttackerInStableOrder()
    {
        var coordinator = new AttackFrameCoordinator(attackerCapacity: 2);
        coordinator.Ingest(
        [
            AttackEvent(sequence: 1, attacker: 2, defender: 7),
            AttackEvent(sequence: 2, attacker: 3, defender: 8),
        ]);

        var released = coordinator.ReleaseForDraw(
            [Agent(2, 0), Agent(7, 300), Agent(3, 100), Agent(8, 400)],
            MotionIntensity.Full,
            allowRelease: true).ToArray();

        Assert.Equal([1, 2], released.Select(contact => contact.Sequence));
        Assert.Equal(2, coordinator.Dispatcher.LatchedCount);
        Assert.Equal(0, coordinator.Dispatcher.PendingCount);
        Assert.All(
            coordinator.Animations.ActiveAnimations.ToArray(),
            animation =>
            {
                Assert.Equal(AttackAnimationPhase.Contact, animation.Phase);
                Assert.True(animation.AwaitingDrawAcknowledgement);
            });
    }

    [Fact]
    public void AcknowledgeDraw_ReleasesOnlyDrawnLatchesAndThenAllowsComboContact()
    {
        var coordinator = new AttackFrameCoordinator(attackerCapacity: 1);
        coordinator.Ingest(
        [
            AttackEvent(sequence: 1, attacker: 2, defender: 7),
            AttackEvent(sequence: 2, attacker: 2, defender: 7),
        ]);
        AgentView[] agents = [Agent(2, 0), Agent(7, 300)];

        var first = Assert.Single(
            coordinator.ReleaseForDraw(
                agents,
                MotionIntensity.Full,
                allowRelease: true).ToArray());
        coordinator.Advance(10f, speedMultiplier: 4f, advanceContacts: true);

        Assert.True(coordinator.Animations.TryGetAnimation(2, out var latched));
        Assert.Equal(0f, latched.AgeSeconds);
        Assert.True(coordinator.HasUndrawnContacts);

        Assert.Equal(1, coordinator.AcknowledgeDraw());
        Assert.Equal(first.Sequence, latched.Sequence);
        var second = Assert.Single(
            coordinator.ReleaseForDraw(
                agents,
                MotionIntensity.Full,
                allowRelease: true).ToArray());

        Assert.Equal(2, second.Sequence);
        Assert.Equal(2, Assert.Single(
            coordinator.Animations.ActiveAnimations.ToArray()).Sequence);
    }

    [Fact]
    public void PausedFrame_ReleasesAndAdvancesNothingButClearResetsQueue()
    {
        var coordinator = new AttackFrameCoordinator(attackerCapacity: 1);
        coordinator.Ingest([AttackEvent(sequence: 1, attacker: 2, defender: 7)]);

        Assert.Empty(
            coordinator.ReleaseForDraw(
                [Agent(2, 0), Agent(7, 300)],
                MotionIntensity.Reduced,
                allowRelease: false).ToArray());
        coordinator.Advance(1f, speedMultiplier: 4f, advanceContacts: false);

        Assert.Equal(1, coordinator.Dispatcher.PendingCount);
        Assert.Empty(coordinator.Animations.ActiveAnimations.ToArray());

        coordinator.Clear();

        Assert.False(coordinator.HasUndrawnContacts);
        Assert.Equal(0, coordinator.Dispatcher.PendingCount);
    }

    [Fact]
    public void ReleaseForDraw_WithNoPendingContacts_ReturnsEmptyAndLeavesLaterLatchIntact()
    {
        var coordinator = new AttackFrameCoordinator(attackerCapacity: 1);
        AgentView[] agents = [Agent(2, 0), Agent(7, 300)];

        Assert.Empty(
            coordinator.ReleaseForDraw(
                agents,
                MotionIntensity.Full,
                allowRelease: true).ToArray());
        Assert.Equal(0, coordinator.Dispatcher.PendingCount);

        coordinator.Ingest([AttackEvent(sequence: 1, attacker: 2, defender: 7)]);
        var released = Assert.Single(
            coordinator.ReleaseForDraw(
                agents,
                MotionIntensity.Full,
                allowRelease: true).ToArray());

        Assert.Equal(1, released.Sequence);
        Assert.True(coordinator.TryGetAgent(2, out var attacker));
        Assert.True(coordinator.TryGetAgent(7, out var defender));
        Assert.Equal(2ul, attacker.EntityId);
        Assert.Equal(7ul, defender.EntityId);
    }

    private static AgentView Agent(ulong entityId, int xRaw) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            YRaw: 0,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

    private static BattleEvent AttackEvent(
        long sequence,
        ulong attacker,
        ulong defender) =>
        BattleEvent.Attack(
            sequence,
            tick: sequence,
            attacker,
            defender,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Chest);
}
