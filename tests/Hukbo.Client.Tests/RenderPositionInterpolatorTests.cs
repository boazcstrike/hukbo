using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// A pawn is drawn between its last two tick positions. What matters is that
/// the endpoints are exact, that nothing slides in from nowhere, and that the
/// frame loop's quirks (a frame with several ticks, a paused frame, a corpse)
/// cannot make a pawn drift.
/// </summary>
public sealed class RenderPositionInterpolatorTests
{
    [Fact]
    public void AlphaZeroReturnsThePreviousPositionAndAlphaOneTheCurrent()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 100, 200)]);
        interpolator.Capture([CreateAgent(1, 130, 160)]);

        Assert.Equal((100, 200), interpolator.Resolve(0, 0d));
        Assert.Equal((130, 160), interpolator.Resolve(0, 1d));
    }

    [Fact]
    public void AlphaBetweenTheEndpointsReturnsAPointBetweenThem()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 100, 200)]);
        interpolator.Capture([CreateAgent(1, 130, 160)]);

        Assert.Equal((115, 180), interpolator.Resolve(0, 0.5d));
        Assert.Equal((127, 164), interpolator.Resolve(0, 0.9d));
    }

    [Fact]
    public void AnAlphaOutsideTheUnitIntervalIsClampedToTheEndpoints()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);
        interpolator.Capture([CreateAgent(1, 30, -30)]);

        Assert.Equal((0, 0), interpolator.Resolve(0, -0.5d));
        Assert.Equal((30, -30), interpolator.Resolve(0, 1.5d));
    }

    [Fact]
    public void ResetRemovesHistorySoNothingSlidesIn()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);
        interpolator.Capture([CreateAgent(1, 500, 500)]);

        interpolator.Reset([CreateAgent(1, 900, 700)]);

        Assert.Equal((900, 700), interpolator.Resolve(0, 0d));
        Assert.Equal((900, 700), interpolator.Resolve(0, 0.5d));
        Assert.Equal((900, 700), interpolator.Resolve(0, 1d));
    }

    [Fact]
    public void ACaptureWithoutPriorHistoryHoldsStill()
    {
        var interpolator = new RenderPositionInterpolator();

        interpolator.Capture([CreateAgent(1, 400, 300)]);

        Assert.Equal((400, 300), interpolator.Resolve(0, 0d));
        Assert.Equal((400, 300), interpolator.Resolve(0, 0.5d));
    }

    [Fact]
    public void ADeadAgentHoldsStill()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 100, 100)]);

        interpolator.Capture([CreateAgent(1, 120, 100, isAlive: false)]);

        Assert.Equal((120, 100), interpolator.Resolve(0, 0d));
        Assert.Equal((120, 100), interpolator.Resolve(0, 0.5d));
    }

    [Fact]
    public void SeveralTicksInOneFrameUseOnlyTheLastPair()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);

        interpolator.Capture([CreateAgent(1, 10, 0)]);
        interpolator.Capture([CreateAgent(1, 20, 0)]);
        interpolator.Capture([CreateAgent(1, 50, 0)]);

        Assert.Equal((20, 0), interpolator.Resolve(0, 0d));
        Assert.Equal((35, 0), interpolator.Resolve(0, 0.5d));
        Assert.Equal((50, 0), interpolator.Resolve(0, 1d));
    }

    [Fact]
    public void WithoutACaptureTheResolvedPositionDependsOnlyOnAlpha()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);
        interpolator.Capture([CreateAgent(1, 40, 0)]);

        var first = interpolator.Resolve(0, 0.25d);
        var second = interpolator.Resolve(0, 0.25d);

        Assert.Equal(first, second);
        Assert.Equal((10, 0), first);
    }

    [Fact]
    public void AgentsAreResolvedByTheirOwnIndex()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0), CreateAgent(2, 1000, 1000)]);
        interpolator.Capture([CreateAgent(1, 10, 0), CreateAgent(2, 1000, 980)]);

        Assert.Equal(2, interpolator.Count);
        Assert.Equal((10, 0), interpolator.Resolve(0, 1d));
        Assert.Equal((1000, 990), interpolator.Resolve(1, 0.5d));
    }

    [Fact]
    public void ACountChangeDropsHistoryRatherThanMisalignIndices()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);

        interpolator.Capture([CreateAgent(1, 10, 0), CreateAgent(2, 700, 700)]);

        Assert.Equal(2, interpolator.Count);
        Assert.Equal((10, 0), interpolator.Resolve(0, 0d));
        Assert.Equal((700, 700), interpolator.Resolve(1, 0d));
    }

    [Fact]
    public void ShrinkingThenGrowingKeepsEveryIndexValid()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0), CreateAgent(2, 5, 5), CreateAgent(3, 9, 9)]);
        interpolator.Reset([CreateAgent(1, 1, 1)]);
        interpolator.Reset([CreateAgent(1, 2, 2), CreateAgent(2, 3, 3), CreateAgent(3, 4, 4)]);

        Assert.Equal((4, 4), interpolator.Resolve(2, 0.5d));
    }

    [Fact]
    public void AnIndexOutsideTheCapturedAgentsIsRejected()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, 0, 0)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => interpolator.Resolve(1, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => interpolator.Resolve(-1, 0d));
    }

    [Fact]
    public void ExtremeRawPositionsDoNotOverflow()
    {
        var interpolator = new RenderPositionInterpolator();
        interpolator.Reset([CreateAgent(1, int.MinValue, int.MaxValue)]);
        interpolator.Capture([CreateAgent(1, int.MaxValue, int.MinValue)]);

        Assert.Equal((int.MinValue, int.MaxValue), interpolator.Resolve(0, 0d));
        Assert.Equal((int.MaxValue, int.MinValue), interpolator.Resolve(0, 1d));
    }

    private static AgentView CreateAgent(
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
            Intent: isAlive ? AgentIntent.Idle : AgentIntent.Dead,
            isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
