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

    /// <summary>
    /// The trail says what the blow was, not only that one happened. A lethal
    /// landed contact reads hardest, an evasion faintest, and every outcome
    /// differs from every other.
    /// </summary>
    [Fact]
    public void Trail_EmphasisSeparatesEveryOutcomeAndTheLethalCase()
    {
        var emphases = Enum.GetValues<AttackResolution>()
            .Select(resolution => (
                Resolution: resolution,
                Emphasis: PosedTrail(resolution).Emphasis))
            .ToArray();

        Assert.Equal(
            emphases.Length,
            emphases.Select(entry => MathF.Round(entry.Emphasis, 4)).Distinct().Count());

        var landed = PosedTrail(AttackResolution.Landed).Emphasis;
        var evaded = PosedTrail(AttackResolution.Evaded).Emphasis;
        var lethal = PosedTrail(AttackResolution.Landed, isLethal: true).Emphasis;

        Assert.True(evaded < landed, "an evasion read as hard as a landed blow.");
        Assert.True(landed < lethal, "a lethal blow read no harder than a survived one.");
    }

    /// <summary>
    /// A pawn that is not attacking has no trail at all, so it has no outcome
    /// to emphasise either — the whole value stays at its neutral default.
    /// </summary>
    [Fact]
    public void Trail_IsWhollyNeutralWithoutAnAttackPose()
    {
        var trail = Neutral(WeaponId.Kampilan, HighZoom).SwingTrail;

        Assert.True(trail.IsEmpty);
        Assert.Equal(default, trail);
    }

    private static SwingTrail PosedTrail(
        AttackResolution resolution,
        bool isLethal = false)
    {
        var animation = AttackGeometryTests.Animation(
            WeaponId.Kampilan,
            resolution) with
        {
            IsLethal = isLethal,
        };

        return PawnGeometry.PoseBlindPrefix
            .Create(Anchor, HighZoom, Appearance(WeaponId.Kampilan, ShieldId.None))
            .CompleteAttackPosedLayout(AttackPoseResolver.Resolve(animation))
            .SwingTrail;
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
    /// Section 5 of the strike-while-moving design pinned a one-pixel floor
    /// for the gait-only case (<c>PawnGeometryTests.cs:766</c>) but found no
    /// equivalent for the composed attack-plus-gait case: the only composed
    /// test runs at High tier and asserts only that the striking stride is
    /// smaller than the walking one, with no pixel floor. At Medium tier with
    /// a 10-pixel leg, <c>PlantStride</c>'s full damping (40 per cent of the
    /// stride survives a fully committed attack) leaves a planted stride of
    /// 0.32 * 0.4 * 10 = 1.28 px against an unplanted 3.20 px — close enough
    /// to zero that rounding through the drawn rectangle, not the raw
    /// arithmetic, is the only way to know it survives.
    /// </summary>
    [Fact]
    public void AttackStance_KeepsAtLeastOnePixelOfLegOffsetAtMediumDetail()
    {
        var gait = new GaitPose(
            GaitMode.Walk,
            PhaseTurns: 0.25f,
            LeftLegOffsetRatio: 0.32f,
            RightLegOffsetRatio: -0.32f,
            LeftFootLiftRatio: 0.15f,
            RightFootLiftRatio: 0f,
            TorsoLeanX: 0f,
            TorsoLeanY: 0f,
            DirectionSign: 1f);
        var striking = Posed(WeaponId.Itak, MediumZoom, gaitPose: gait);

        Assert.Equal(PawnDetailTier.Medium, striking.DetailTier);
        Assert.False(striking.LeftLegBounds.IsEmpty);
        Assert.False(striking.RightLegBounds.IsEmpty);

        var strikingStride = MathF.Abs(
            striking.LeftLegBounds.Center.X - striking.RightLegBounds.Center.X);

        Assert.True(
            strikingStride >= 1f,
            $"stride {strikingStride} rounded below the one-pixel floor at " +
            "Medium tier.");
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

    /// <summary>
    /// The heading of this weapon's line on a pawn that is not attacking: the
    /// baseline every attack rotation is measured against.
    /// </summary>
    private static float NeutralWeaponAngle(WeaponId weapon, float zoom)
    {
        var layout = Neutral(weapon, zoom);
        return MathF.Atan2(
            layout.WeaponEnd.Y - layout.WeaponStart.Y,
            layout.WeaponEnd.X - layout.WeaponStart.X);
    }

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

    /// <summary>
    /// The settled guard pose carries no arms. A timeline stays in the
    /// animation store once it reaches readiness — that phase is the design's
    /// weapon-specific guarded stance rather than an expiry — so without this
    /// every warrior that had ever landed a blow would keep four arm quads for
    /// the rest of the battle and the design's per-active-attacker ceiling
    /// would quietly become a per-pawn cost.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void Arms_AreEmptyOnceThePoseHasSettledIntoReadiness(WeaponId weapon)
    {
        var settled = Posed(
            weapon,
            HighZoom,
            shield: ShieldId.TallHardwood,
            ageSeconds: 5f,
            awaitingDrawAcknowledgement: false);

        Assert.True(settled.Arms.IsEmpty);
        Assert.True(settled.SwingTrail.IsEmpty);
    }

    /// <summary>
    /// A pose still in recovery keeps its arms: the ceiling applies to a
    /// settled warrior, not to one mid-blow.
    /// </summary>
    [Fact]
    public void Arms_SurviveThroughRecovery()
    {
        var profile = AttackMotionCatalog.Resolve(WeaponId.Wasay);
        var recovering = Posed(
            WeaponId.Wasay,
            HighZoom,
            ageSeconds: profile.RecoverySeconds * 0.5f,
            awaitingDrawAcknowledgement: false);

        Assert.False(recovering.Arms.IsEmpty);
    }

    /// <summary>
    /// The trail lags the blade at every heading. <c>CreateSwingTrail</c> reads
    /// the sign of the weapon rotation as the direction of travel, so a
    /// rotation left unwrapped past pi — which a difference between two
    /// <c>Atan2</c> results can be — would sweep the arc ahead of the weapon
    /// instead of behind it. That inverted for roughly an eighth of all
    /// headings before the rotation was wrapped.
    /// </summary>
    [Theory]
    [MemberData(nameof(Weapons))]
    public void Trail_LagsTheBladeAtEveryHeading(WeaponId weapon)
    {
        // A trail is the visible sweep of an edge through the air, and none of
        // the three ranged releases sweeps one — AttackMotionCatalog pins
        // TrailEligible false for all three, which RU-44 established as seven
        // explicit per-weapon pins rather than a blanket assertion.
        //
        // This test arrived from the attack-animation-v2 branch enumerating
        // every WeaponId and asserting a trail exists, which was true when the
        // roster was four swinging weapons. Rather than drop the ranged three
        // from the data set, assert what is actually true of them: an
        // ineligible weapon draws no trail at any heading. That is a stricter
        // pin than excluding them, because a trail appearing on an arquebus
        // would now fail here instead of going unnoticed.
        var trailEligible = AttackMotionCatalog.Resolve(weapon).TrailEligible;

        for (var degrees = 0; degrees < 360; degrees += 3)
        {
            var radians = degrees * MathF.PI / 180f;
            var layout = Posed(
                weapon,
                HighZoom,
                directionX: MathF.Cos(radians),
                directionY: MathF.Sin(radians));
            var trail = layout.SwingTrail;

            if (!trailEligible)
            {
                Assert.True(
                    trail.IsEmpty,
                    $"{weapon} at {degrees} deg: drew a trail it is not " +
                    "eligible for.");
                continue;
            }

            Assert.False(trail.IsEmpty);

            // The arc runs from its trailing end to the weapon tip, so the
            // start of the sweep must sit behind the drawn weapon line.
            var weaponAngle = MathF.Atan2(
                layout.WeaponEnd.Y - layout.WeaponStart.Y,
                layout.WeaponEnd.X - layout.WeaponStart.X);
            var endDelta = MathF.Abs(
                MathF.IEEERemainder(trail.EndAngleRadians - weaponAngle, MathF.Tau));

            Assert.True(
                endDelta < 0.001f,
                $"{weapon} at {degrees} deg: trail ends {endDelta} rad off the blade.");

            // The sweep runs from the trailing end to the tip, so its sign is
            // the direction the weapon actually turned. Recomputed here from
            // the drawn line and the weapon's own neutral baseline rather than
            // read back out of the pose, so an unwrapped rotation cannot agree
            // with itself.
            var baseline = NeutralWeaponAngle(weapon, HighZoom);
            var turned = MathF.IEEERemainder(weaponAngle - baseline, MathF.Tau);
            var sweep = trail.EndAngleRadians - trail.StartAngleRadians;

            Assert.NotEqual(0f, sweep);
            Assert.True(
                (sweep >= 0f) == (turned >= 0f),
                $"{weapon} at {degrees} deg: swept {sweep} for a turn of {turned}.");
        }
    }
}
