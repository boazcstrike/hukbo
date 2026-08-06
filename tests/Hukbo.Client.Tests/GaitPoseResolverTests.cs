using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="GaitPoseResolver"/>: the mapping from the gait store and
/// the agent views to one pose per tracked agent, and the lookup shape the
/// draw loop uses. Mirrors <c>SwingPoseResolverTests</c>.
/// </summary>
public sealed class GaitPoseResolverTests
{
    [Fact]
    public void Resolve_ReturnsNoPoseForAnAgentTheStoreHasNotIngested()
    {
        var gait = new GaitAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0)];
        var destination = new Dictionary<ulong, GaitPose>();

        var poses = GaitPoseResolver.Resolve(
            gait, agents, MotionIntensity.Full, destination);

        Assert.False(poses.ContainsKey(2));
    }

    [Fact]
    public void Resolve_ReturnsOnePosePerTrackedAgent()
    {
        var gait = new GaitAnimationSystem(capacity: 8);
        AgentView[] first = [Agent(2, 0, 0), Agent(7, 0, 0)];
        gait.Ingest(first);
        AgentView[] agents = [Agent(2, 400, 0), Agent(7, 400, 0)];
        gait.Ingest(agents);
        var destination = new Dictionary<ulong, GaitPose>();

        var poses = GaitPoseResolver.Resolve(
            gait, agents, MotionIntensity.Full, destination);

        Assert.Equal(2, poses.Count);
        Assert.Equal(GaitMode.Walk, poses[2].Mode);
        Assert.Equal(GaitMode.Walk, poses[7].Mode);
    }

    [Fact]
    public void Resolve_OffResolvesTheNeutralPoseForEveryTrackedAgent()
    {
        var gait = new GaitAnimationSystem(capacity: 8);
        gait.Ingest([Agent(2, 0, 0)]);
        AgentView[] agents = [Agent(2, 2000, 0)];
        gait.Ingest(agents);
        var destination = new Dictionary<ulong, GaitPose>();

        var poses = GaitPoseResolver.Resolve(
            gait, agents, MotionIntensity.Off, destination);

        Assert.Equal(default, poses[2]);
    }

    [Fact]
    public void TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity()
    {
        var gait = new GaitAnimationSystem(capacity: 8);
        gait.Ingest([Agent(2, 0, 0)]);
        AgentView[] agents = [Agent(2, 400, 0)];
        gait.Ingest(agents);
        var destination = new Dictionary<ulong, GaitPose>();
        var poses = GaitPoseResolver.Resolve(
            gait, agents, MotionIntensity.Full, destination);

        var found = GaitPoseResolver.TryGetPose(poses, 2, out var pose);

        Assert.True(found);
        Assert.Equal(poses[2], pose);
        Assert.False(GaitPoseResolver.TryGetPose(poses, 404, out var missing));
        Assert.Equal(default, missing);
    }

    [Fact]
    public void Resolve_RejectsAnUndefinedMotionIntensity()
    {
        var gait = new GaitAnimationSystem(capacity: 8);
        AgentView[] agents = [Agent(2, 0, 0)];
        var destination = new Dictionary<ulong, GaitPose>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => GaitPoseResolver.Resolve(
                gait, agents, (MotionIntensity)99, destination));
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
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
