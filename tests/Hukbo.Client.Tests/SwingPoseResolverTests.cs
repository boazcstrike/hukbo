using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers the mapping and the lookup shape together. The lookup is the part
/// that lands in <c>ArenaGame.Rendering</c>, which is banned from tests, so a
/// resolver whose cases only covered "no swing" and "one pose per swing" would
/// leave the shipped draw loop unspecified.
/// </summary>
public sealed class SwingPoseResolverTests
{
    [Fact]
    public void Resolve_ReturnsNoPoseForAnAgentWithNoActiveSwing()
    {
        var swings = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];
        swings.Ingest([AttackEvent(1, source: 2, target: 7)], agents);
        var destination = new Dictionary<ulong, SwingPose>();

        var poses = SwingPoseResolver.Resolve(swings, agents, destination);

        Assert.True(poses.ContainsKey(2));
        Assert.False(poses.ContainsKey(7));
        Assert.False(poses.ContainsKey(9));
    }

    [Fact]
    public void Resolve_ReturnsOnePosePerActiveSwing()
    {
        var swings = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];
        swings.Ingest(
            [
                AttackEvent(1, source: 2, target: 7),
                AttackEvent(2, source: 9, target: 7),
            ],
            agents);
        swings.Advance(SwingAnimation.TotalSeconds * 0.6f);
        var destination = new Dictionary<ulong, SwingPose>();

        var poses = SwingPoseResolver.Resolve(swings, agents, destination);

        Assert.Equal(2, poses.Count);
        Assert.Equal(swings.ActiveSwings.Length, poses.Count);
        Assert.Equal(SwingPhase.ImpactHold, poses[2].Phase);
        Assert.NotEqual(default, poses[2]);
        Assert.NotEqual(default, poses[9]);

        // A second resolve into the same buffer replaces rather than
        // accumulates, which is what makes the buffer safe to reuse per frame.
        swings.Clear();
        var empty = SwingPoseResolver.Resolve(swings, agents, destination);

        Assert.Empty(empty);
    }

    [Fact]
    public void TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity()
    {
        var swings = new SwingAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];
        swings.Ingest(
            [
                AttackEvent(1, source: 2, target: 7, AttackResolution.Parried),
                AttackEvent(2, source: 9, target: 7, AttackResolution.Evaded),
            ],
            agents);
        swings.Advance(SwingAnimation.TotalSeconds * 0.7f);
        var poses = SwingPoseResolver.Resolve(
            swings,
            agents,
            new Dictionary<ulong, SwingPose>());

        foreach (var agent in agents)
        {
            var found = SwingPoseResolver.TryGetPose(
                poses,
                agent.EntityId,
                out var pose);

            if (!swings.TryGetSwing(agent.EntityId, out var swing))
            {
                Assert.False(found);
                Assert.Equal(default, pose);
                continue;
            }

            Assert.True(found);
            Assert.Equal(SwingGeometry.ResolvePose(swing), pose);
        }

        Assert.False(SwingPoseResolver.TryGetPose(poses, 404, out var missing));
        Assert.Equal(default, missing);
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
        AttackResolution resolution = AttackResolution.Landed) =>
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
