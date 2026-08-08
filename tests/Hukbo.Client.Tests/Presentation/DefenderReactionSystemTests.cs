using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class DefenderReactionSystemTests
{
    [Fact]
    public void StartContact_CapturesTargetLocalOutcomeAndLethalHold()
    {
        var system = new DefenderReactionSystem(capacity: 2);

        system.StartContact(
            Contact(
                sequence: 4,
                resolution: AttackResolution.Parried,
                isLethal: true),
            Agent(2, xRaw: 100, yRaw: 200),
            Agent(7, xRaw: 400, yRaw: 600, isAlive: false));

        var reaction = Assert.Single(system.ActiveReactions.ToArray());
        Assert.Equal(4, reaction.Sequence);
        Assert.Equal(7UL, reaction.DefenderEntityId);
        Assert.Equal((400, 600), (reaction.XRaw, reaction.YRaw));
        Assert.Equal(0.6f, reaction.DirectionX, precision: 5);
        Assert.Equal(0.8f, reaction.DirectionY, precision: 5);
        Assert.Equal(AttackResolution.Parried, reaction.Resolution);
        Assert.True(reaction.IsLethal);
        Assert.True(system.IsLethalHoldActive(7));
    }

    [Fact]
    public void StartContact_KeepsOnlyNewestReactionPerDefender()
    {
        var system = new DefenderReactionSystem(capacity: 2);
        var attacker = Agent(2, 0, 0);
        var defender = Agent(7, 100, 0);

        system.StartContact(Contact(sequence: 1), attacker, defender);
        system.StartContact(
            Contact(sequence: 2, resolution: AttackResolution.Evaded),
            attacker,
            defender);

        var reaction = Assert.Single(system.ActiveReactions.ToArray());
        Assert.Equal(2, reaction.Sequence);
        Assert.Equal(AttackResolution.Evaded, reaction.Resolution);
    }

    [Fact]
    public void Advance_ExpiresBoundedReactionsAndClearRemovesAllState()
    {
        var system = new DefenderReactionSystem(capacity: 1);
        system.StartContact(
            Contact(sequence: 1, isLethal: true),
            Agent(2, 0, 0),
            Agent(7, 100, 0, isAlive: false));

        system.Advance(DefenderReaction.LethalHoldSeconds);

        Assert.False(system.IsLethalHoldActive(7));
        Assert.Single(system.ActiveReactions.ToArray());

        system.Clear();

        Assert.Empty(system.ActiveReactions.ToArray());
        Assert.False(system.TryGetReaction(7, out _));
    }

    [Fact]
    public void ConstructorAndAdvance_RejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DefenderReactionSystem(capacity: 0));
        var system = new DefenderReactionSystem(capacity: 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(-0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(float.NaN));
    }

    private static AttackContactBundle Contact(
        long sequence,
        AttackResolution resolution = AttackResolution.Landed,
        bool isLethal = false) =>
        new(
            sequence,
            Tick: 3,
            AttackerEntityId: 2,
            DefenderEntityId: 7,
            Damage: resolution == AttackResolution.Landed ? 12 : 0,
            FactionId: 0,
            WeaponId.Kampilan,
            AttackerShield: ShieldId.None,
            HitLocation: BodyPart.Chest,
            resolution,
            ComboPosition: null,
            isLethal);

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            isAlive,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));
}
