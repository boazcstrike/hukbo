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

    /// <summary>
    /// Design section 8, defender side. Every resolution has to read
    /// differently at the moment of contact, and the five offsets are pinned
    /// against each other rather than against absolute numbers, so tuning one
    /// family's magnitude cannot quietly collapse two outcomes into one.
    /// </summary>
    [Fact]
    public void ResolveOffset_SeparatesEveryOutcomeAtContact()
    {
        var offsets = Enum.GetValues<AttackResolution>()
            .Select(resolution => (
                Resolution: resolution,
                Offset: Reaction(resolution).ResolveOffset()))
            .ToArray();

        foreach (var (resolution, offset) in offsets)
        {
            Assert.True(
                float.IsFinite(offset.X) && float.IsFinite(offset.Y),
                $"{resolution} produced a non-finite offset.");
        }

        var distinct = offsets
            .Select(entry => (
                X: MathF.Round(entry.Offset.X, 4),
                Y: MathF.Round(entry.Offset.Y, 4)))
            .Distinct()
            .Count();

        Assert.Equal(offsets.Length, distinct);
    }

    /// <summary>
    /// A landed blow drives the defender away along the attack line. A shield
    /// braces into it instead, which is the one outcome that moves toward the
    /// attacker rather than away.
    /// </summary>
    [Fact]
    public void ResolveOffset_PushesBackOnALandedBlowAndBracesIntoAShield()
    {
        var landed = Reaction(AttackResolution.Landed).ResolveOffset();
        var blocked = Reaction(AttackResolution.ShieldBlocked).ResolveOffset();

        Assert.True(landed.X > 0f, "a landed blow did not push the defender back.");
        Assert.True(blocked.X < 0f, "a shield did not brace into the contact.");
    }

    /// <summary>
    /// A parry redirects across the attack line and an evasion steps off it,
    /// so both carry more lateral travel than along-line travel. A deflection
    /// is the shallow case between a parry and a landed blow.
    /// </summary>
    [Fact]
    public void ResolveOffset_RedirectsLaterallyForAParryAndAnEvasion()
    {
        var parried = Reaction(AttackResolution.Parried).ResolveOffset();
        var evaded = Reaction(AttackResolution.Evaded).ResolveOffset();
        var deflected = Reaction(AttackResolution.Deflected).ResolveOffset();

        Assert.True(MathF.Abs(parried.Y) > MathF.Abs(parried.X));
        Assert.True(MathF.Abs(evaded.Y) > MathF.Abs(evaded.X));
        Assert.True(
            MathF.Abs(deflected.Y) < MathF.Abs(parried.Y),
            "a deflection redirected as hard as a parry.");
    }

    /// <summary>
    /// A lethal contact reads harder than the same resolution surviving it.
    /// </summary>
    [Fact]
    public void ResolveOffset_IsLargerForALethalContact()
    {
        var survived = Reaction(AttackResolution.Landed).ResolveOffset();
        var lethal = Reaction(AttackResolution.Landed, isLethal: true)
            .ResolveOffset();

        Assert.True(Magnitude(lethal) > Magnitude(survived));
    }

    /// <summary>
    /// The reaction is a displacement that returns. It is largest at contact,
    /// decays monotonically, and is exactly zero once the reaction has run its
    /// lifetime — a defender that keeps a residual offset has been moved by
    /// presentation, which is the one thing a reaction may never do.
    /// </summary>
    [Theory]
    [InlineData(AttackResolution.Landed)]
    [InlineData(AttackResolution.ShieldBlocked)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    [InlineData(AttackResolution.Evaded)]
    public void ResolveOffset_DecaysToExactlyNeutral(AttackResolution resolution)
    {
        var contact = Reaction(resolution);
        var previous = Magnitude(contact.ResolveOffset());

        Assert.True(previous > 0f);

        for (var step = 1; step <= 10; step++)
        {
            var aged = contact with
            {
                AgeSeconds = contact.LifetimeSeconds * (step / 10f),
            };
            var magnitude = Magnitude(aged.ResolveOffset());

            Assert.True(
                magnitude <= previous,
                $"{resolution} grew from {previous} to {magnitude} at step {step}.");
            previous = magnitude;
        }

        Assert.Equal(0f, previous);
    }

    /// <summary>
    /// The offset is expressed in the attack's own basis, so the same contact
    /// arriving from the opposite side pushes the opposite way rather than
    /// always pushing along screen X.
    /// </summary>
    [Fact]
    public void ResolveOffset_IsTargetLocal()
    {
        var fromLeft = Reaction(AttackResolution.Landed);
        var fromRight = fromLeft with { DirectionX = -1f, DirectionY = 0f };

        var left = fromLeft.ResolveOffset();
        var right = fromRight.ResolveOffset();

        Assert.True(left.X > 0f);
        Assert.True(right.X < 0f);
        Assert.Equal(left.X, -right.X, precision: 5);
    }

    private static float Magnitude((float X, float Y) offset) =>
        MathF.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));

    private static DefenderReaction Reaction(
        AttackResolution resolution,
        bool isLethal = false) =>
        new(
            Sequence: 1,
            AttackerEntityId: 2,
            DefenderEntityId: 7,
            XRaw: 100,
            YRaw: 0,
            DirectionX: 1f,
            DirectionY: 0f,
            resolution,
            isLethal,
            AgeSeconds: 0f);

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
