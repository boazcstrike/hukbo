using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Five cases. Two are RED against the Phase 0 stub, which places nothing, and
/// three are GUARD because "places nothing for a landed blow" is satisfied by
/// a system that places nothing for anything.
/// </summary>
public sealed class ClashEffectSystemTests
{
    /// <summary>
    /// RED. The stub places nothing, so there is no effect to inspect.
    /// </summary>
    [Fact]
    public void Ingest_PlacesTheEffectAtTheContactMidpoint()
    {
        var system = new ClashEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 100, 200), Agent(7, 300, 400)];

        system.Ingest(
            [AttackEvent(1, source: 2, target: 7, AttackResolution.Parried)],
            agents);

        var effect = Assert.Single(system.ActiveEffects.ToArray());
        Assert.Equal(1, effect.Sequence);
        Assert.Equal(2UL, effect.AttackerEntityId);
        Assert.Equal(7UL, effect.TargetEntityId);
        Assert.Equal(AttackResolution.Parried, effect.Resolution);
        Assert.Equal((200, 300), (effect.XRaw, effect.YRaw));
        Assert.Equal(0f, effect.AgeSeconds);
    }

    /// <summary>
    /// RED. Eviction is by age, breaking a tie on the lowest sequence, which
    /// is the rule <see cref="HitEffectSystem"/> already uses. Both halves are
    /// asserted here because a full pool reached inside one tick has nothing
    /// but the tie-break to separate its entries.
    /// </summary>
    [Fact]
    public void Ingest_EvictsOldestWhenFull()
    {
        var system = new ClashEffectSystem(capacity: 2);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 200, 0), Agent(9, 0, 200)];

        system.Ingest(
            [AttackEvent(1, source: 2, target: 7, AttackResolution.Parried)],
            agents);
        system.Advance(0.05f);
        system.Ingest(
            [
                AttackEvent(2, source: 9, target: 7, AttackResolution.Deflected),
                AttackEvent(
                    3,
                    source: 7,
                    target: 2,
                    AttackResolution.ShieldBlocked),
            ],
            agents);

        var afterAge = system.ActiveEffects.ToArray();
        Assert.Equal(2, afterAge.Length);
        Assert.DoesNotContain(afterAge, effect => effect.Sequence == 1);

        var tieBreak = new ClashEffectSystem(capacity: 2);
        tieBreak.Ingest(
            [
                AttackEvent(1, source: 2, target: 7, AttackResolution.Parried),
                AttackEvent(2, source: 9, target: 7, AttackResolution.Parried),
                AttackEvent(3, source: 7, target: 2, AttackResolution.Parried),
            ],
            agents);

        var afterTie = tieBreak.ActiveEffects.ToArray();
        Assert.Equal(2, afterTie.Length);
        Assert.DoesNotContain(afterTie, effect => effect.Sequence == 1);
    }

    /// <summary>
    /// GUARD, satisfied by a stub that places nothing. A landed blow is
    /// already announced by the impact ring and the blood, and a cross on top
    /// of it would say two weapons met when one met a body.
    /// </summary>
    [Fact]
    public void Ingest_SkipsLandedAttacks()
    {
        var system = new ClashEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 100, 200), Agent(7, 300, 400)];

        system.Ingest(
            [AttackEvent(1, source: 2, target: 7, AttackResolution.Landed)],
            agents);

        Assert.Empty(system.ActiveEffects.ToArray());
    }

    /// <summary>
    /// GUARD, satisfied by a stub that places nothing. For a void the absence
    /// is the signal: nothing met anything, so nothing is drawn.
    /// </summary>
    [Fact]
    public void Ingest_SkipsAVoid()
    {
        var system = new ClashEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 100, 200), Agent(7, 300, 400)];

        system.Ingest(
            [AttackEvent(1, source: 2, target: 7, AttackResolution.Evaded)],
            agents);

        Assert.Empty(system.ActiveEffects.ToArray());
    }

    /// <summary>
    /// GUARD, satisfied by a stub that places nothing. A cross sits at the
    /// midpoint of two agents, so it needs both; neither a throw nor a cross
    /// at the origin is acceptable.
    /// </summary>
    [Fact]
    public void Ingest_IgnoresAnAttackWhoseAgentsAreMissingFromTheViews()
    {
        var system = new ClashEffectSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 100, 200)];

        system.Ingest(
            [
                AttackEvent(1, source: 2, target: 7, AttackResolution.Parried),
                AttackEvent(
                    2,
                    source: 5,
                    target: 2,
                    AttackResolution.ShieldBlocked),
            ],
            agents);

        Assert.Empty(system.ActiveEffects.ToArray());
    }

    private static AgentView Agent(ulong entityId, int xRaw, int yRaw) =>
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
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));

    private static BattleEvent AttackEvent(
        long sequence,
        ulong source,
        ulong target,
        AttackResolution resolution) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            target,
            damage: resolution == AttackResolution.Landed ? 10 : 0,
            factionId: 0,
            WeaponId.GreatBlade,
            BodyPart.Chest,
            resolution);
}
