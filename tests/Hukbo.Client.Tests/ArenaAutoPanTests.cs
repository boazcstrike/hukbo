using Hukbo.Client.Settings;
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
    public void IsWorthTravelling_IsFalseForATargetNearTheCentre()
    {
        var target = new Vector2(HalfExtents.X * 0.4f, 0f);

        Assert.False(
            ArenaAutoPan.IsWorthTravelling(Vector2.Zero, target, HalfExtents));
    }

    [Fact]
    public void IsWorthTravelling_IsTrueWhenEitherAxisClearsTheThreshold()
    {
        var target = new Vector2(0f, HalfExtents.Y * 0.9f);

        Assert.True(
            ArenaAutoPan.IsWorthTravelling(Vector2.Zero, target, HalfExtents));
    }

    [Fact]
    public void GetTuning_GivesFollowASharperGraceAndDwellThanAssisted()
    {
        var assisted = ArenaAutoPan.GetTuning(AutoCameraMode.Assisted);
        var follow = ArenaAutoPan.GetTuning(AutoCameraMode.Follow);

        Assert.True(follow.IdleGraceSeconds < assisted.IdleGraceSeconds);
        Assert.True(follow.DwellSeconds < assisted.DwellSeconds);
        Assert.True(follow.OnScreenFraction < assisted.OnScreenFraction);
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

        var center = RunFrames(controller, agents, Vector2.Zero, frames: 600);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_HoldsStillThroughTheIdleGrace()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        var frames = (int)MathF.Floor(
            ArenaAutoPan.AssistedIdleGraceSeconds * 60f) - 1;

        var center = RunFrames(controller, agents, Vector2.Zero, frames);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_PansTowardTheFightOnceTheIdleGraceElapses()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);

        Assert.True(controller.IsPanning);
        Assert.True(center.X > 0f);
        Assert.True(center.X < 500f);
    }

    /// <summary>
    /// The regression that this whole feature exists for. An agent is only
    /// marked <see cref="AgentIntent.Attacking"/> on ticks where its target is
    /// inside contact distance, so a fight plainly on screen still reports no
    /// fighters on some frames. The assistant must not read those frames as an
    /// empty screen.
    /// </summary>
    [Fact]
    public void Controller_IgnoresFramesWhereAVisibleFightIsBetweenBlows()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] fighting =
        [
            CreateAgent(1, 5f, 0f, AgentIntent.Attacking),
            CreateAgent(2, 900f, 0f, AgentIntent.Attacking),
        ];
        AgentView[] betweenBlows =
        [
            CreateAgent(1, 5f, 0f, AgentIntent.Moving),
            CreateAgent(2, 900f, 0f, AgentIntent.Attacking),
        ];

        var center = Vector2.Zero;
        for (var frame = 0; frame < 600; frame++)
        {
            center = Update(
                controller,
                frame % 2 == 0 ? fighting : betweenBlows,
                center);
        }

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
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

        var center = RunFrames(controller, agents, Vector2.Zero, frames: 600);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_DoesNotTravelForAClusterCentredOnTheCamera()
    {
        // Zoomed in far enough that a single melee straddles the screen: no
        // fighter is inside the visible rectangle, but their centroid is the
        // camera centre already, so there is nowhere worth going.
        var tightExtents = new Vector2(4f, 4f);
        var controller = new ArenaAutoPanController();
        AgentView[] agents =
        [
            CreateAgent(1, 0f, 6f, AgentIntent.Attacking),
            CreateAgent(2, 0f, -6f, AgentIntent.Attacking),
        ];

        var center = RunFrames(
            controller,
            agents,
            Vector2.Zero,
            frames: 600,
            halfExtents: tightExtents);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_ArrivesAtTheFightAndDisengages()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        for (var frame = 0; frame < 600 && controller.IsPanning; frame++)
        {
            center = Update(controller, agents, center);
        }

        Assert.False(controller.IsPanning);
        Assert.True(IsSettled(center));
    }

    [Fact]
    public void Controller_DwellsAfterSettlingBeforeStartingAnotherPan()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);
        for (var frame = 0; frame < 600 && controller.IsPanning; frame++)
        {
            center = Update(controller, agents, center);
        }

        Assert.Equal(
            ArenaAutoPan.AssistedDwellSeconds,
            controller.DwellRemaining,
            precision: 3);

        // The fight vanishes the instant the camera settles. Even with the
        // screen empty and the grace long expired, the dwell holds the camera
        // still.
        AgentView[] gone = [CreateAgent(1, 5000f, 0f, AgentIntent.Attacking)];
        var settled = center;
        var dwellFrames = (int)MathF.Floor(
            ArenaAutoPan.AssistedDwellSeconds * 60f) - 1;
        center = RunFrames(controller, gone, center, dwellFrames);

        Assert.Equal(settled, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_GivesUpOnAPanThatOverrunsTheCeiling()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents =
        [
            CreateAgent(1, 20_000f, 0f, AgentIntent.Attacking),
        ];
        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        var ceilingFrames =
            (int)MathF.Ceiling(ArenaAutoPan.MaximumPanSeconds * 60f) + 1;
        center = RunFrames(controller, agents, center, ceilingFrames);

        Assert.False(controller.IsPanning);
        Assert.True(center.X < 20_000f);
        Assert.True(controller.DwellRemaining > 0f);
    }

    [Fact]
    public void Controller_FollowsAFightThatMovesWhileTheCameraTravels()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] departed = [CreateAgent(1, 900f, 0f, AgentIntent.Attacking)];
        var center = RunOutIdleGrace(controller, departed, Vector2.Zero);
        Assert.True(controller.IsPanning);

        // The melee drifts off the original bearing. A target resolved once at
        // departure would leave the camera heading at y = 0 forever.
        AgentView[] moved = [CreateAgent(1, 900f, 900f, AgentIntent.Attacking)];
        var retargetFrames =
            (int)MathF.Ceiling(ArenaAutoPan.RetargetIntervalSeconds * 60f) + 2;
        center = RunFrames(controller, moved, center, retargetFrames);

        Assert.True(center.Y > 0f);
    }

    [Fact]
    public void Controller_KeepsPanningWhileFighterSitsOutsideSettleMargin()
    {
        var controller = new ArenaAutoPanController();

        // Inside the full rectangle but outside the settle rectangle, so the
        // camera must keep closing rather than stopping at the screen edge.
        var edgeX = HalfExtents.X * 0.85f;
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        RunOutIdleGrace(controller, agents, Vector2.Zero);
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
        RunOutIdleGrace(controller, agents, Vector2.Zero);
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

        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);

        Assert.True(controller.IsPanning);
        Assert.True(center.X > 0f);
    }

    [Fact]
    public void Controller_StandsDownWhileSuppressed()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        RunOutIdleGrace(controller, agents, Vector2.Zero);
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
    public void Controller_NeverMovesTheCameraWhileOff()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];

        var center = RunFrames(
            controller,
            agents,
            Vector2.Zero,
            frames: 1200,
            mode: AutoCameraMode.Off);

        Assert.Equal(Vector2.Zero, center);
        Assert.False(controller.IsPanning);
    }

    [Fact]
    public void Controller_TurningOffMidPanAbandonsTheJourney()
    {
        var controller = new ArenaAutoPanController();
        AgentView[] agents = [CreateAgent(1, 500f, 0f, AgentIntent.Attacking)];
        var center = RunOutIdleGrace(controller, agents, Vector2.Zero);
        Assert.True(controller.IsPanning);

        var stopped = Update(
            controller,
            agents,
            center,
            mode: AutoCameraMode.Off);

        Assert.Equal(center, stopped);
        Assert.False(controller.IsPanning);
        Assert.Equal(0f, controller.DwellRemaining, precision: 3);
    }

    /// <summary>
    /// Follow judges the screen by the settle rectangle rather than the whole
    /// screen, so a fight drifting toward the edge pulls the camera back
    /// where assisted mode would leave it alone.
    /// </summary>
    [Fact]
    public void Controller_FollowRecentresAFightAssistedWouldLeaveAlone()
    {
        AgentView[] agents =
        [
            CreateAgent(1, HalfExtents.X * 0.9f, 0f, AgentIntent.Attacking),
        ];

        var assisted = new ArenaAutoPanController();
        var assistedCenter = RunFrames(
            assisted,
            agents,
            Vector2.Zero,
            frames: 600);

        var follow = new ArenaAutoPanController();
        var followCenter = RunFrames(
            follow,
            agents,
            Vector2.Zero,
            frames: 600,
            mode: AutoCameraMode.Follow);

        Assert.Equal(Vector2.Zero, assistedCenter);
        Assert.True(followCenter.X > 0f);
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
        Assert.Equal(0f, controller.DwellRemaining, precision: 3);
    }

    private static bool IsSettled(Vector2 center) =>
        MathF.Abs(500f - center.X) <= HalfExtents.X * ArenaAutoPan.SettleFraction;

    /// <summary>
    /// Runs frames until the idle grace has expired and a pan has begun, so a
    /// test that is about travelling does not restate the grace every time.
    /// </summary>
    private static Vector2 RunOutIdleGrace(
        ArenaAutoPanController controller,
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        AutoCameraMode mode = AutoCameraMode.Assisted)
    {
        var tuning = ArenaAutoPan.GetTuning(mode);
        var frames = (int)MathF.Ceiling(tuning.IdleGraceSeconds * 60f) + 1;
        for (var frame = 0; frame < frames && !controller.IsPanning; frame++)
        {
            center = Update(controller, agents, center, mode: mode);
        }

        return center;
    }

    private static Vector2 RunFrames(
        ArenaAutoPanController controller,
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        int frames,
        AutoCameraMode mode = AutoCameraMode.Assisted,
        Vector2? halfExtents = null)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            center = Update(
                controller,
                agents,
                center,
                mode: mode,
                halfExtents: halfExtents);
        }

        return center;
    }

    private static Vector2 Update(
        ArenaAutoPanController controller,
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        AutoCameraMode mode = AutoCameraMode.Assisted,
        bool manualPanApplied = false,
        bool isSuppressed = false,
        float elapsedSeconds = 1f / 60f,
        Vector2? halfExtents = null) =>
        controller.Update(
            agents,
            center,
            halfExtents ?? HalfExtents,
            zoom: 1f,
            mode,
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
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
