using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Task 7. The drawn consequences of an active attack pose: articulated arms,
/// the posed weapon line and axe head, the contact trail, the shield guard
/// offset, and the bounding union that has to contain all of them.
/// </summary>
/// <remarks>
/// Every quantity asserted here is provisional presentation choreography. None
/// of it is a historical measurement and none of it reaches the simulation.
/// </remarks>
public sealed class AttackPoseRenderingTests
{
    private const float MediumZoom = 1.0f;
    private const float HighZoom = 1.6f;
    private const float LowZoom = 0.2f;

    private static readonly Vector2 Anchor = new(400.5f, 300.5f);

    public static TheoryData<WeaponId> Weapons =>
        new(Enum.GetValues<WeaponId>());

    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_AreNonDegenerate_ForEveryWeaponAtHighDetail(WeaponId weapon)
    {
        var layout = Posed(weapon, HighZoom);

        Assert.False(layout.Arms.IsEmpty);
        AssertDrawable(layout.Arms.WeaponUpperArm.Bounds);
        AssertDrawable(layout.Arms.WeaponForearm.Bounds);
    }

    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_AreNonDegenerate_ForEveryWeaponAtMediumDetail(WeaponId weapon)
    {
        var layout = Posed(weapon, MediumZoom);

        Assert.False(layout.Arms.IsEmpty);
        AssertDrawable(layout.Arms.WeaponUpperArm.Bounds);
        AssertDrawable(layout.Arms.WeaponForearm.Bounds);
    }

    /// <summary>
    /// A two-handed family commits both hands to the weapon, so it draws a
    /// support arm. A one-handed family solo does not.
    /// </summary>
    [Fact]
    public void SupportArm_FollowsTheProfileHandCount()
    {
        var twoHanded = Posed(WeaponId.Kampilan, HighZoom);
        var oneHanded = Posed(WeaponId.Itak, HighZoom);

        AssertDrawable(twoHanded.Arms.SupportUpperArm.Bounds);
        AssertDrawable(twoHanded.Arms.SupportForearm.Bounds);
        Assert.True(oneHanded.Arms.SupportUpperArm.IsEmpty);
        Assert.True(oneHanded.Arms.SupportForearm.IsEmpty);
    }

    /// <summary>
    /// A shielded one-handed warrior draws the off-hand arm anyway: the arm
    /// is what carries the shield guard, and without it the block floats.
    /// </summary>
    [Fact]
    public void ShieldedOneHandedWarrior_DrawsAnOffHandArm()
    {
        var layout = Posed(
            WeaponId.Kalis,
            HighZoom,
            shield: ShieldId.TallHardwood);

        AssertDrawable(layout.Arms.SupportUpperArm.Bounds);
        AssertDrawable(layout.Arms.SupportForearm.Bounds);
    }

    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_AreEmpty_AtLowDetail(WeaponId weapon)
    {
        var layout = Posed(weapon, LowZoom);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        Assert.True(layout.Arms.IsEmpty);
    }

    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_AreEmpty_WithoutAnAttackPose(WeaponId weapon)
    {
        var layout = Neutral(weapon, HighZoom);

        Assert.True(layout.Arms.IsEmpty);
    }

    /// <summary>
    /// The hand end of the weapon arm is the grip the weapon line starts
    /// from. Anything else draws a warrior holding air beside their weapon.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void WeaponForearm_ReachesTheGripTheWeaponLineStartsFrom(WeaponId weapon)
    {
        var layout = Posed(weapon, HighZoom);
        var grip = layout.WeaponGripAnchor;
        var forearm = Inflated(layout.Arms.WeaponForearm.Bounds, 1);

        Assert.True(
            forearm.Contains(new Point(
                (int)MathF.Round(grip.X),
                (int)MathF.Round(grip.Y))),
            $"{weapon}: forearm {layout.Arms.WeaponForearm.Bounds} misses grip {grip}.");
    }

    /// <summary>
    /// Two segments, not one bar. The elbow has to leave the straight line
    /// between shoulder and grip or the arm reads as a stick.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_ArticulateAtAnElbowOffTheShoulderToGripLine(WeaponId weapon)
    {
        var layout = Posed(weapon, HighZoom);
        var upper = Center(layout.Arms.WeaponUpperArm.Bounds);
        var fore = Center(layout.Arms.WeaponForearm.Bounds);

        Assert.NotEqual(upper, fore);
        Assert.True(
            Vector2.Distance(upper, fore) >= 1f,
            $"{weapon}: arm segments overlap at {upper}.");
    }

    /// <summary>
    /// The axe head is what makes a Wasay read as an axe, and it has to
    /// travel with the posed haft rather than staying at the neutral end.
    /// </summary>
    [Fact]
    public void AxeHead_IsDrawnAndTravelsWithThePosedHaft()
    {
        var neutral = Neutral(WeaponId.Wasay, HighZoom);
        var posed = Posed(
            WeaponId.Wasay,
            HighZoom,
            directionX: 0f,
            directionY: 1f);

        AssertDrawable(posed.SecondaryEquipmentBounds);
        Assert.NotEqual(
            neutral.SecondaryEquipmentBounds,
            posed.SecondaryEquipmentBounds);
    }

    /// <summary>
    /// The trail is the weapon edge's recent travel. It exists at contact and
    /// is gone by the time recovery completes, so a settled warrior carries no
    /// afterimage.
    /// </summary>
    [Fact]
    public void Trail_IsDrawnAtContactAndGoneByRecovery()
    {
        var contact = Posed(WeaponId.Kampilan, HighZoom);
        var settled = Posed(
            WeaponId.Kampilan,
            HighZoom,
            ageSeconds: 5f,
            awaitingDrawAcknowledgement: false);

        Assert.False(contact.SwingTrail.IsEmpty);
        Assert.True(contact.SwingTrail.Radius > 0f);
        Assert.True(settled.SwingTrail.IsEmpty);
    }

    [Fact]
    public void Trail_IsOmittedAtLowDetail()
    {
        var layout = Posed(WeaponId.Kampilan, LowZoom);

        Assert.True(layout.SwingTrail.IsEmpty);
    }

    /// <summary>
    /// A legal shield overlay moves the block into a guard; an illegal one
    /// (two-handed family) leaves the block exactly where the neutral layout
    /// puts it.
    /// </summary>
    [Fact]
    public void Shield_TakesAGuardOffsetOnlyForALegalOverlay()
    {
        var neutralShielded = Neutral(
            WeaponId.Kalis,
            HighZoom,
            shield: ShieldId.TallHardwood);
        var posedShielded = Posed(
            WeaponId.Kalis,
            HighZoom,
            shield: ShieldId.TallHardwood);
        var neutralTwoHanded = Neutral(
            WeaponId.Wasay,
            HighZoom,
            shield: ShieldId.TallHardwood);
        var posedTwoHanded = Posed(
            WeaponId.Wasay,
            HighZoom,
            shield: ShieldId.TallHardwood);

        Assert.NotEqual(neutralShielded.ShieldBounds, posedShielded.ShieldBounds);
        Assert.Equal(neutralTwoHanded.ShieldBounds, posedTwoHanded.ShieldBounds);
    }

    /// <summary>
    /// Composition, not replacement. An attack pose plants the stance, so the
    /// legs move less than the same gait pose moves them on its own, but the
    /// legs are still drawn.
    /// </summary>
    [Fact]
    public void AttackStance_DampensTheGaitStrideWithoutRemovingTheLegs()
    {
        var gait = new GaitPose(
            GaitMode.Walk,
            PhaseTurns: 0.25f,
            LeftLegOffsetRatio: 0.30f,
            RightLegOffsetRatio: -0.30f,
            LeftFootLiftRatio: 0.20f,
            RightFootLiftRatio: 0f,
            TorsoLeanX: 0f,
            TorsoLeanY: 0f,
            DirectionSign: 1f);
        var walking = Neutral(WeaponId.Itak, HighZoom, gaitPose: gait);
        var striking = Posed(WeaponId.Itak, HighZoom, gaitPose: gait);

        var walkingStride = MathF.Abs(
            walking.LeftLegBounds.Center.X - walking.RightLegBounds.Center.X);
        var strikingStride = MathF.Abs(
            striking.LeftLegBounds.Center.X - striking.RightLegBounds.Center.X);

        Assert.False(striking.LeftLegBounds.IsEmpty);
        Assert.False(striking.RightLegBounds.IsEmpty);
        Assert.True(
            strikingStride < walkingStride,
            $"stride {strikingStride} was not planted below {walkingStride}.");
    }

    /// <summary>
    /// Neutral geometry stays bit-for-bit what it was. An attack pose that is
    /// not active may not move a single rectangle.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void NeutralLayout_IsUnchangedByTheAttackPoseChannel(WeaponId weapon)
    {
        var appearance = Appearance(weapon, ShieldId.TallHardwood);
        var throughAttackPath = PawnGeometry.PoseBlindPrefix
            .Create(Anchor, HighZoom, appearance)
            .CompleteAttackPosedLayout(attackPose: null);
        var throughLegacyPath = PawnGeometry.Create(
            Anchor,
            HighZoom,
            appearance);

        Assert.Equal(throughLegacyPath, throughAttackPath);
    }

    /// <summary>
    /// Every element an active attack draws has to be inside the bounding
    /// union the renderer and the cull both read.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void VisualBounds_ContainEveryPosedElement(WeaponId weapon)
    {
        foreach (var (directionX, directionY) in Headings)
        {
            var layout = Posed(
                weapon,
                HighZoom,
                shield: ShieldId.TallHardwood,
                directionX: directionX,
                directionY: directionY);

            AssertContained(layout.VisualBounds, layout.WeaponBounds, "weapon");
            AssertContained(
                layout.VisualBounds,
                layout.SecondaryEquipmentBounds,
                "secondary");
            AssertContained(
                layout.VisualBounds,
                layout.Arms.WeaponUpperArm.Bounds,
                "upper");
            AssertContained(
                layout.VisualBounds,
                layout.Arms.WeaponForearm.Bounds,
                "forearm");
            AssertContained(
                layout.VisualBounds,
                layout.Arms.SupportUpperArm.Bounds,
                "support upper");
            AssertContained(
                layout.VisualBounds,
                layout.Arms.SupportForearm.Bounds,
                "support forearm");
            AssertContained(layout.VisualBounds, layout.ShieldBounds, "shield");
        }
    }

    /// <summary>
    /// The design's ceiling: an active attack adds at most four arm quads to a
    /// Medium or High pawn, and none at Low.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_NeverExceedFourQuads(WeaponId weapon)
    {
        foreach (var zoom in new[] { LowZoom, MediumZoom, HighZoom })
        {
            var layout = Posed(weapon, zoom, shield: ShieldId.TallHardwood);
            var quads = 0;

            if (!layout.Arms.WeaponUpperArm.IsEmpty)
            {
                quads++;
            }

            if (!layout.Arms.WeaponForearm.IsEmpty)
            {
                quads++;
            }

            if (!layout.Arms.SupportUpperArm.IsEmpty)
            {
                quads++;
            }

            if (!layout.Arms.SupportForearm.IsEmpty)
            {
                quads++;
            }

            Assert.InRange(quads, 0, 4);
        }
    }

    [Fact]
    public void PosedLayout_IsAllocationFreeAfterWarmup()
    {
        var appearance = Appearance(WeaponId.Kalis, ShieldId.TallHardwood);
        var prefix = PawnGeometry.PoseBlindPrefix.Create(
            Anchor,
            HighZoom,
            appearance);
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(WeaponId.Kalis, shield: ShieldId.TallHardwood));

        prefix.CompleteAttackPosedLayout(pose);
        GC.GetAllocatedBytesForCurrentThread();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            prefix.CompleteAttackPosedLayout(pose);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static readonly (float X, float Y)[] Headings =
    [
        (1f, 0f),
        (1f, 1f),
        (0f, 1f),
        (-1f, 1f),
        (-1f, 0f),
        (-1f, -1f),
        (0f, -1f),
        (1f, -1f),
    ];

    private static PawnLayout Posed(
        WeaponId weapon,
        float zoom,
        ShieldId shield = ShieldId.None,
        float directionX = 1f,
        float directionY = 0f,
        float ageSeconds = 0f,
        bool awaitingDrawAcknowledgement = true,
        GaitPose? gaitPose = null)
    {
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                weapon,
                shield: shield,
                directionX: directionX,
                directionY: directionY,
                ageSeconds: ageSeconds,
                awaitingDrawAcknowledgement: awaitingDrawAcknowledgement));

        return PawnGeometry.PoseBlindPrefix
            .Create(Anchor, zoom, Appearance(weapon, shield))
            .CompleteAttackPosedLayout(pose, gaitPose);
    }

    private static PawnLayout Neutral(
        WeaponId weapon,
        float zoom,
        ShieldId shield = ShieldId.None,
        GaitPose? gaitPose = null) =>
        PawnGeometry.PoseBlindPrefix
            .Create(Anchor, zoom, Appearance(weapon, shield))
            .CompleteAttackPosedLayout(attackPose: null, gaitPose);

    private static PawnAppearance Appearance(WeaponId weapon, ShieldId shield) =>
        PawnAppearanceFactory.Create(entityId: 11, weapon, shield);

    private static Vector2 Center(Rectangle bounds) =>
        new(bounds.Center.X, bounds.Center.Y);

    private static Rectangle Inflated(Rectangle bounds, int amount) =>
        new(
            bounds.X - amount,
            bounds.Y - amount,
            bounds.Width + (amount * 2),
            bounds.Height + (amount * 2));

    private static void AssertDrawable(Rectangle bounds)
    {
        Assert.True(bounds.Width >= 1, $"{bounds} has no width.");
        Assert.True(bounds.Height >= 1, $"{bounds} has no height.");
    }

    private static void AssertContained(
        Rectangle union,
        Rectangle part,
        string name)
    {
        if (part.IsEmpty)
        {
            return;
        }

        Assert.True(
            union.Contains(part),
            $"{name} {part} escapes the visual bounds {union}.");
    }
}
