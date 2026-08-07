using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="RangedPoseResolver"/>: the mapping from the agent views
/// to one pose per ranged agent, the lookup shape the draw loop uses, and the
/// swing-suppression rule design section 8.3 of
/// <c>docs/plans/2026-08-07-ranged-units-design.md</c> requires. Mirrors
/// <c>SwingPoseResolverTests</c> and <c>GaitPoseResolverTests</c>.
/// </summary>
public sealed class RangedPoseResolverTests
{
    [Fact]
    public void Resolve_ReturnsNoPoseForAnAgentWithNoRangedPhase()
    {
        AgentView[] agents =
        [
            Agent(2, WeaponId.Kampilan, RangedPhase.None, 0),
            Agent(7, WeaponId.Busog, RangedPhase.Draw, 5),
        ];
        var destination = new Dictionary<ulong, RangedPose>();

        var poses = RangedPoseResolver.Resolve(agents, destination);

        Assert.False(poses.ContainsKey(2));
        Assert.True(poses.ContainsKey(7));
    }

    [Fact]
    public void Resolve_ReturnsOnePosePerAgentWithARangedPhaseAndMatchesTheGeometry()
    {
        AgentView[] agents =
        [
            Agent(2, WeaponId.Bangkaw, RangedPhase.Draw, 5),
            Agent(9, WeaponId.Arquebus, RangedPhase.Load, 3),
        ];
        var destination = new Dictionary<ulong, RangedPose>();

        var poses = RangedPoseResolver.Resolve(agents, destination);

        Assert.Equal(2, poses.Count);
        Assert.Equal(
            RangedGeometry.ResolvePose(WeaponId.Bangkaw, RangedPhase.Draw, 5),
            poses[2]);
        Assert.Equal(
            RangedGeometry.ResolvePose(WeaponId.Arquebus, RangedPhase.Load, 3),
            poses[9]);
    }

    [Fact]
    public void Resolve_ASecondResolveIntoOneBufferReplacesRatherThanAccumulates()
    {
        var destination = new Dictionary<ulong, RangedPose>();
        AgentView[] firstTick = [Agent(2, WeaponId.Busog, RangedPhase.Draw, 5)];
        RangedPoseResolver.Resolve(firstTick, destination);
        Assert.True(destination.ContainsKey(2));

        AgentView[] secondTick = [Agent(9, WeaponId.Arquebus, RangedPhase.Load, 2)];
        var poses = RangedPoseResolver.Resolve(secondTick, destination);

        Assert.False(poses.ContainsKey(2));
        Assert.True(poses.ContainsKey(9));
        Assert.Single(poses);
    }

    [Fact]
    public void Resolve_ReturnsImmediatelyWhenNoAgentHasARangedPhase()
    {
        AgentView[] agents =
        [
            Agent(2, WeaponId.Kampilan, RangedPhase.None, 0),
            Agent(7, WeaponId.Wasay, RangedPhase.None, 0),
        ];
        var destination = new Dictionary<ulong, RangedPose>
        {
            [404] = RangedGeometry.ResolvePose(WeaponId.Busog, RangedPhase.Ready, 0),
        };

        var poses = RangedPoseResolver.Resolve(agents, destination);

        Assert.Empty(poses);
        Assert.Same(destination, poses);
    }

    [Fact]
    public void Resolve_ReturnsEmptyForAnEmptyAgentList()
    {
        var destination = new Dictionary<ulong, RangedPose>();

        var poses = RangedPoseResolver.Resolve([], destination);

        Assert.Empty(poses);
    }

    [Fact]
    public void TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity()
    {
        AgentView[] agents =
        [
            Agent(2, WeaponId.Busog, RangedPhase.Draw, 5),
            Agent(7, WeaponId.Kampilan, RangedPhase.None, 0),
        ];
        var poses = RangedPoseResolver.Resolve(agents, new Dictionary<ulong, RangedPose>());

        Assert.True(RangedPoseResolver.TryGetPose(poses, 2, out var found));
        Assert.Equal(poses[2], found);

        Assert.False(RangedPoseResolver.TryGetPose(poses, 7, out var missing));
        Assert.Equal(default, missing);

        Assert.False(RangedPoseResolver.TryGetPose(poses, 404, out var unknown));
        Assert.Equal(default, unknown);
    }

    [Fact]
    public void SuppressesSwing_TrueExactlyWhenARangedPoseExistsForThatPawn()
    {
        AgentView[] agents =
        [
            Agent(2, WeaponId.Busog, RangedPhase.Draw, 5),
            Agent(7, WeaponId.Kampilan, RangedPhase.None, 0),
        ];
        var poses = RangedPoseResolver.Resolve(agents, new Dictionary<ulong, RangedPose>());

        Assert.True(RangedPoseResolver.SuppressesSwing(poses, 2));
        Assert.False(RangedPoseResolver.SuppressesSwing(poses, 7));
        Assert.False(RangedPoseResolver.SuppressesSwing(poses, 404));
    }

    [Fact]
    public void Resolve_ThrowsForNullAgentsOrDestination()
    {
        Assert.Throws<ArgumentNullException>(
            () => RangedPoseResolver.Resolve(null!, new Dictionary<ulong, RangedPose>()));
        Assert.Throws<ArgumentNullException>(
            () => RangedPoseResolver.Resolve(Array.Empty<AgentView>(), null!));
    }

    private static AgentView Agent(
        ulong entityId,
        WeaponId weapon,
        RangedPhase rangedPhase,
        int rangedPhaseTicksRemaining) =>
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
                weapon,
                ArmorId.LightOrganic,
                weapon is WeaponId.Bangkaw or WeaponId.Busog or WeaponId.Arquebus
                    ? ShieldId.None
                    : ShieldId.TallHardwood),
            RangedPhase: rangedPhase,
            RangedPhaseTicksRemaining: rangedPhaseTicksRemaining);
}
