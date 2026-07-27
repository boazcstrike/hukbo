using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class ArenaAutoPanTests
{
    private static readonly Vector2 HalfExtents = new(20f, 20f);

    [Fact]
    public void IsFighting_IsTrueOnlyForLivingAttacker()
    {
        Assert.True(
            ArenaAutoPan.IsFighting(
                CreateAgent(1, 0f, 0f, AgentIntent.Attacking)));
        Assert.False(
            ArenaAutoPan.IsFighting(
                CreateAgent(1, 0f, 0f, AgentIntent.Moving)));
        Assert.False(
            ArenaAutoPan.IsFighting(
                CreateAgent(1, 0f, 0f, AgentIntent.Idle)));
        Assert.False(
            ArenaAutoPan.IsFighting(
                CreateAgent(1, 0f, 0f, AgentIntent.Dead)));
    }

    [Fact]
    public void IsFighting_IsFalseForDeadAgentStillMarkedAttacking()
    {
        var agent = CreateAgent(
            1,
            0f,
            0f,
            AgentIntent.Attacking,
            isAlive: false);

        Assert.False(ArenaAutoPan.IsFighting(agent));
    }

    [Fact]
    public void HasFighterInside_IsTrueForFighterWithinRectangle()
    {
        AgentView[] agents =
        [
            CreateAgent(1, 10f, -5f, AgentIntent.Attacking),
        ];

        Assert.True(
            ArenaAutoPan.HasFighterInside(agents, Vector2.Zero, HalfExtents));
    }

    [Fact]
    public void HasFighterInside_IsFalseWhenOnlyFighterIsOffScreen()
    {
        AgentView[] agents =
        [
            CreateAgent(1, 400f, 0f, AgentIntent.Attacking),
            CreateAgent(2, 0f, 0f, AgentIntent.Moving),
        ];

        Assert.False(
            ArenaAutoPan.HasFighterInside(agents, Vector2.Zero, HalfExtents));
    }

    [Fact]
    public void TryResolveTarget_ReturnsFalseWhenNobodyIsFighting()
    {
        AgentView[] agents = [CreateAgent(1, 5f, 5f, AgentIntent.Moving)];

        Assert.False(
            ArenaAutoPan.TryResolveTarget(agents, Vector2.Zero, out var target));
        Assert.Equal(Vector2.Zero, target);
    }

    [Fact]
    public void TryResolveTarget_PicksNearestMeleeNotGlobalCentroid()
    {
        // Two melees at opposite ends. Their global centroid is empty ground at
        // x = 0, which is exactly the wrong place to aim the camera.
        AgentView[] agents =
        [
            CreateAgent(1, -300f, 0f, AgentIntent.Attacking),
            CreateAgent(2, -304f, 0f, AgentIntent.Attacking),
            CreateAgent(3, 300f, 0f, AgentIntent.Attacking),
            CreateAgent(4, 304f, 0f, AgentIntent.Attacking),
        ];

        Assert.True(
            ArenaAutoPan.TryResolveTarget(
                agents,
                new Vector2(250f, 0f),
                out var target));

        Assert.Equal(302f, target.X, precision: 3);
        Assert.Equal(0f, target.Y, precision: 3);
    }

    [Fact]
    public void TryResolveTarget_ExcludesFightersBeyondClusterRadius()
    {
        var farX = ArenaAutoPan.ClusterRadius + 10f;
        AgentView[] agents =
        [
            CreateAgent(1, 0f, 0f, AgentIntent.Attacking),
            CreateAgent(2, 4f, 0f, AgentIntent.Attacking),
            CreateAgent(3, farX, 0f, AgentIntent.Attacking),
        ];

        Assert.True(
            ArenaAutoPan.TryResolveTarget(agents, Vector2.Zero, out var target));

        Assert.Equal(2f, target.X, precision: 3);
    }

    [Fact]
    public void TryResolveTarget_BreaksAnchorTiesOnLowerEntityId()
    {
        // Both anchors are equidistant from the camera. Their clusters are far
        // apart, so the tie-break decides the whole result.
        AgentView[] agents =
        [
            CreateAgent(9, 100f, 0f, AgentIntent.Attacking),
            CreateAgent(4, -100f, 0f, AgentIntent.Attacking),
        ];

        Assert.True(
            ArenaAutoPan.TryResolveTarget(agents, Vector2.Zero, out var target));

        Assert.Equal(-100f, target.X, precision: 3);
    }

    [Fact]
    public void AdvanceCenter_DoesNotOvershootTarget()
    {
        var target = new Vector2(1f, 0f);

        var moved = ArenaAutoPan.AdvanceCenter(
            Vector2.Zero,
            target,
            zoom: 1f,
            elapsedSeconds: 10f);

        Assert.Equal(target, moved);
    }

    [Fact]
    public void AdvanceCenter_MovesPartwayForALargeGap()
    {
        var target = new Vector2(10_000f, 0f);

        var moved = ArenaAutoPan.AdvanceCenter(
            Vector2.Zero,
            target,
            zoom: 1f,
            elapsedSeconds: 1f / 60f);

        Assert.True(moved.X > 0f);
        Assert.True(moved.X < target.X);
        Assert.Equal(0f, moved.Y, precision: 3);
    }

    [Fact]
    public void Controller_StaysPutWhenAFighterIsAlreadyOnScreen()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents =
        [
            CreateAgent(1, 5f, 0f, AgentIntent.Attacking),
            CreateAgent(2, 900f, 0f, AgentIntent.Attacking),
        ];

        var center = Update(controller, agents, Vector2.Zero);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_PansTowardTheFightWhenScreenIsEmpty()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        var center = Update(controller, agents, Vector2.Zero);

        Assert.True(controller.IsPanning);
        Assert.True(center.X > 0f);
        Assert.True(center.X < 500f);
    }

    [Fact]
    public void Controller_DoesNothingWhenNobodyIsFighting()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents =
        [
            CreateAgent(1, 500f, 0f, AgentIntent.Moving),
            CreateAgent(2, 600f, 0f, AgentIntent.Idle),
        ];

        var center = Update(controller, agents, Vector2.Zero);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_ArrivesAtTheFightAndDisengages()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        var center = Update(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        for (var frame = 0; frame < 600 && controller.IsPanning; frame++)
        {
            center = Update(controller, agents, center);
        }

        Assert.False(controller.IsPanning);
        Assert.True(IsSettled(center));
    }

    [Fact]
    public void Controller_KeepsPanningWhileFighterSitsOutsideSettleMargin()
    {
        var controller = new ArenaAutoPanController();

        // Inside the full rectangle but outside the settle rectangle, so the
        // camera must keep closing rather than stopping at the screen edge.
        var edgeX = HalfExtents.X * 0.85f;
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        Update(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        var center = Update(controller, agents, new Vector2(500f - edgeX, 0f));

        Assert.True(controller.IsPanning);
        Assert.True(center.X > 500f - edgeX);
    }

    [Fact]
    public void Controller_YieldsToManualPanAndHoldsOffForTheOverrideWindow()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        Update(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        var center = Update(
            controller,
            agents,
            Vector2.Zero,
            manualPanApplied: true);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
        Assert.Equal(
            ArenaAutoPan.ManualOverrideSeconds,
            controller.ManualOverrideRemaining,
            3);

        center = Update(controller, agents, Vector2.Zero);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_ResumesAfterTheOverrideWindowExpires()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        Update(controller, agents, Vector2.Zero, manualPanApplied: true);

        Update(
            controller,
            agents,
            Vector2.Zero,
            elapsedSeconds: ArenaAutoPan.ManualOverrideSeconds);
        Assert.Equal(0f, controller.ManualOverrideRemaining, precision: 3);

        var center = Update(controller, agents, Vector2.Zero);

        Assert.True(controller.IsPanning);
        Assert.True(center.X > 0f);
    }

    [Fact]
    public void Controller_StandsDownWhileSuppressed()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        Update(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        var center = Update(
            controller,
            agents,
            Vector2.Zero,
            isSuppressed: true);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_ResetClearsEngagementAndOverride()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        Update(controller, agents, Vector2.Zero, manualPanApplied: true);

        controller.Reset();

        Assert.False(controller.IsPanning);
        Assert.Equal(0f, controller.ManualOverrideRemaining, precision: 3);
    }

    private static bool IsSettled(Vector2 center) =>
        MathF.Abs(500f - center.X) <= HalfExtents.X * ArenaAutoPan.SettleFraction;

    private static Vector2 Update(
        ArenaAutoPanController controller,
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        bool manualPanApplied = false,
        bool isSuppressed = false,
        float elapsedSeconds = 1f / 60f) =>
        controller.Update(
            agents,
            center,
            HalfExtents,
            zoom: 1f,
            manualPanApplied,
            isSuppressed,
            elapsedSeconds);

    private static AgentView CreateAgent(
        ulong entityId,
        float x,
        float y,
        AgentIntent intent,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            XRaw: (int)MathF.Round(x * FixedPoint.Scale),
            YRaw: (int)MathF.Round(y * FixedPoint.Scale),
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            intent,
            isAlive,
            Loadout: new CombatLoadout(
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
