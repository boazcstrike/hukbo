using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="DeathCollapseSystem"/>: when a collapse registers, which
/// way it goes, how it ages, and the ordinal validity rule its lookup rests on
/// (the 2026-08-14 death-collapse design, section 7).
/// </summary>
public sealed class DeathCollapseSystemTests
{
    [Fact]
    public void Observe_RegistersNothingForALivingAgent()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);

        collapses.Observe([Agent(2, isAlive: true)], reactions);

        Assert.False(collapses.TryGetCollapse(0, 2, out _));
        Assert.Equal(0f, collapses.ResolveRotationRadians(0, 2));
    }

    /// <summary>
    /// The lethal hold is the reason the collapse does not begin when
    /// <c>IsAlive</c> goes false. Starting the fall there would consume the
    /// window the lethal-blow legibility work bought.
    /// </summary>
    [Fact]
    public void Observe_RegistersNothingWhileTheLethalHoldIsStillRunning()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents = [Agent(2, isAlive: false)];
        StartLethalContact(reactions, attacker: Agent(1, true), defender: agents[0]);

        Assert.True(reactions.IsLethalHoldActive(2));
        collapses.Observe(agents, reactions);

        Assert.False(collapses.TryGetCollapse(0, 2, out _));
    }

    [Fact]
    public void Observe_RegistersOnceTheLethalHoldHasExpired()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents = [Agent(2, isAlive: false)];
        StartLethalContact(reactions, Agent(1, true), agents[0]);
        reactions.Advance(DefenderReaction.LethalHoldSeconds + 0.01f);

        Assert.False(reactions.IsLethalHoldActive(2));
        collapses.Observe(agents, reactions);

        Assert.True(collapses.TryGetCollapse(0, 2, out var collapse));
        Assert.Equal(2UL, collapse.EntityId);
        Assert.Equal(0f, collapse.AgeSeconds);
    }

    /// <summary>
    /// A death with no surviving reaction at all still falls over. This is the
    /// path a warrior takes when the reaction has already aged out by the time
    /// anything observes the roster.
    /// </summary>
    [Fact]
    public void Observe_RegistersADeathWithNoReactionAtAll()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);

        collapses.Observe([Agent(2, isAlive: false)], reactions);

        Assert.True(collapses.TryGetCollapse(0, 2, out _));
    }

    [Fact]
    public void Observe_NeverRestartsACollapseItHasAlreadyRegistered()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents = [Agent(2, isAlive: false)];
        collapses.Observe(agents, reactions);
        collapses.Advance(0.2f);

        collapses.Observe(agents, reactions);

        Assert.True(collapses.TryGetCollapse(0, 2, out var collapse));
        Assert.Equal(0.2f, collapse.AgeSeconds, 1e-5f);
    }

    /// <summary>
    /// A warrior struck from its left falls to its right.
    /// <c>DefenderReaction.DirectionX</c> points from attacker to defender, so
    /// an attacker to the left of its victim produces a positive direction and
    /// a positive resting angle.
    /// </summary>
    [Theory]
    [InlineData(-5000, true)]
    [InlineData(5000, false)]
    public void Observe_FallsAwayFromTheKillingBlow(
        int attackerXRaw,
        bool expectPositiveRotation)
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents = [Agent(2, isAlive: false)];
        StartLethalContact(
            reactions,
            Agent(1, true) with { XRaw = attackerXRaw },
            agents[0]);
        reactions.Advance(DefenderReaction.LethalHoldSeconds + 0.01f);

        collapses.Observe(agents, reactions);

        Assert.True(collapses.TryGetCollapse(0, 2, out var collapse));
        Assert.Equal(
            expectPositiveRotation,
            collapse.FinalRotationRadians > 0f);
    }

    /// <summary>
    /// A blow arriving straight down the screen says nothing about which way
    /// the body goes, so the fall falls back to the entity id's low bit rather
    /// than to a coin flip that would differ between runs.
    /// </summary>
    [Theory]
    [InlineData(2UL, true)]
    [InlineData(3UL, false)]
    public void Observe_FallsBackToTheEntityIdWhenTheBlowIsVertical(
        ulong entityId,
        bool expectPositiveRotation)
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents = [Agent(entityId, isAlive: false)];
        StartLethalContact(
            reactions,
            Agent(1, true) with { XRaw = 0, YRaw = -5000 },
            agents[0]);
        reactions.Advance(DefenderReaction.LethalHoldSeconds + 0.01f);

        collapses.Observe(agents, reactions);

        Assert.True(collapses.TryGetCollapse(0, entityId, out var collapse));
        Assert.Equal(expectPositiveRotation, collapse.FinalRotationRadians > 0f);
    }

    [Fact]
    public void Advance_AgesARegisteredCollapse()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        collapses.Observe([Agent(2, isAlive: false)], reactions);

        collapses.Advance(0.1f);
        collapses.Advance(0.1f);

        Assert.True(collapses.TryGetCollapse(0, 2, out var collapse));
        Assert.Equal(0.2f, collapse.AgeSeconds, 1e-5f);
    }

    /// <summary>
    /// A body that has finished falling stops accumulating age. The pose is
    /// already exactly the resting angle past that point, so the only thing a
    /// growing float would buy is lost precision over a long battle.
    /// </summary>
    [Fact]
    public void Advance_StopsAccumulatingOnceTheCollapseHasFinished()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        collapses.Observe([Agent(2, isAlive: false)], reactions);
        collapses.Advance(CollapsePose.CollapseSeconds + 1f);
        var settled = collapses.ResolveRotationRadians(0, 2);

        for (var frame = 0; frame < 500; frame++)
        {
            collapses.Advance(0.016f);
        }

        Assert.Equal(settled, collapses.ResolveRotationRadians(0, 2));
        Assert.True(collapses.TryGetCollapse(0, 2, out var collapse));
        Assert.Equal(collapse.FinalRotationRadians, settled);
    }

    [Fact]
    public void Advance_RejectsANegativeOrNonFiniteElapsedTime()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => collapses.Advance(-0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => collapses.Advance(float.NaN));
    }

    /// <summary>
    /// The store is addressed by roster ordinal, so a mismatched identity must
    /// be a miss rather than a wrong answer: a store carried across a roster it
    /// no longer describes can only fail to find a body, never invent one.
    /// </summary>
    [Fact]
    public void TryGetCollapse_MissesOnAMismatchedEntityId()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        collapses.Observe([Agent(2, isAlive: false)], reactions);

        Assert.False(collapses.TryGetCollapse(0, 404, out _));
        Assert.Equal(0f, collapses.ResolveRotationRadians(0, 404));
    }

    [Fact]
    public void TryGetCollapse_MissesOnAnOrdinalOutsideTheStore()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);

        Assert.False(collapses.TryGetCollapse(-1, 2, out _));
        Assert.False(collapses.TryGetCollapse(9999, 2, out _));
    }

    /// <summary>
    /// The roster size is a scenario input and the coordinator is built before
    /// a scenario exists, so a roster larger than the initial capacity grows
    /// the store rather than dropping the warriors past the end of it.
    /// </summary>
    [Fact]
    public void Observe_GrowsToHoldARosterLargerThanTheInitialCapacity()
    {
        var collapses = new DeathCollapseSystem(capacity: 1);
        var reactions = new DefenderReactionSystem(capacity: 4);
        AgentView[] agents =
        [
            Agent(2, isAlive: false),
            Agent(3, isAlive: false),
            Agent(4, isAlive: false),
        ];

        collapses.Observe(agents, reactions);

        Assert.True(collapses.TryGetCollapse(2, 4, out _));
    }

    [Fact]
    public void Clear_EmptiesTheStore()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);
        var reactions = new DefenderReactionSystem(capacity: 4);
        collapses.Observe([Agent(2, isAlive: false)], reactions);

        collapses.Clear();

        Assert.False(collapses.TryGetCollapse(0, 2, out _));
    }

    [Fact]
    public void Observe_RejectsANullRosterOrReactionSystem()
    {
        var collapses = new DeathCollapseSystem(capacity: 4);

        Assert.Throws<ArgumentNullException>(
            () => collapses.Observe(null!, new DefenderReactionSystem(capacity: 4)));
        Assert.Throws<ArgumentNullException>(
            () => collapses.Observe([Agent(2, isAlive: false)], null!));
    }

    private static void StartLethalContact(
        DefenderReactionSystem reactions,
        AgentView attacker,
        AgentView defender) =>
        reactions.StartContact(
            new AttackContactBundle(
                Sequence: 1,
                Tick: 1,
                AttackerEntityId: attacker.EntityId,
                DefenderEntityId: defender.EntityId,
                Damage: 40,
                FactionId: attacker.FactionId,
                Weapon: WeaponId.Kampilan,
                AttackerShield: ShieldId.None,
                HitLocation: BodyPart.Chest,
                Resolution: AttackResolution.Landed,
                ComboPosition: null,
                IsLethal: true),
            attacker,
            defender);

    private static AgentView Agent(ulong entityId, bool isAlive) =>
        new(
            entityId,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
