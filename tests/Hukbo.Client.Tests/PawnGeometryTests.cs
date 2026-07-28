using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class PawnGeometryTests
{
    [Fact]
    public void Create_AppliesMonotonicClampedZoomScaling()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var minimum = PawnGeometry.Create(Vector2.Zero, 0.05f, appearance);
        var low = PawnGeometry.Create(Vector2.Zero, 0.5f, appearance);
        var medium = PawnGeometry.Create(Vector2.Zero, 1f, appearance);
        var high = PawnGeometry.Create(Vector2.Zero, 2f, appearance);
        var maximum = PawnGeometry.Create(Vector2.Zero, 12f, appearance);
        var aboveMaximum = PawnGeometry.Create(Vector2.Zero, 24f, appearance);

        Assert.True(minimum.ApparentScale <= low.ApparentScale);
        Assert.True(low.ApparentScale <= medium.ApparentScale);
        Assert.True(medium.ApparentScale <= high.ApparentScale);
        Assert.True(high.ApparentScale <= maximum.ApparentScale);
        Assert.Equal(maximum.ApparentScale, aboveMaximum.ApparentScale);
    }

    [Fact]
    public void Create_UsesAllDetailTiersInOrder()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var low = PawnGeometry.Create(Vector2.Zero, 0.05f, appearance);
        var medium = PawnGeometry.Create(Vector2.Zero, 1f, appearance);
        var high = PawnGeometry.Create(Vector2.Zero, 3f, appearance);

        Assert.Equal(PawnDetailTier.Low, low.DetailTier);
        Assert.Equal(PawnDetailTier.Medium, medium.DetailTier);
        Assert.Equal(PawnDetailTier.High, high.DetailTier);
    }

    [Fact]
    public void Create_PreservesFootAnchorAcrossBodyVariation()
    {
        var footAnchor = new Vector2(137.25f, 241.75f);
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var slightShort = baseAppearance with
        {
            StatureMultiplier = 0.90f,
            BuildMultiplier = 0.86f,
        };
        var broadTall = baseAppearance with
        {
            StatureMultiplier = 1.10f,
            BuildMultiplier = 1.18f,
        };

        var first = PawnGeometry.Create(footAnchor, 1f, slightShort);
        var second = PawnGeometry.Create(footAnchor, 1f, broadTall);

        Assert.Equal(footAnchor, first.FootAnchor);
        Assert.Equal(footAnchor, second.FootAnchor);
    }

    [Fact]
    public void Create_KeepsHeadSizeStableWhileTorsoVaries()
    {
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var slightShort = baseAppearance with
        {
            StatureMultiplier = 0.90f,
            BuildMultiplier = 0.86f,
        };
        var broadTall = baseAppearance with
        {
            StatureMultiplier = 1.10f,
            BuildMultiplier = 1.18f,
        };

        var first = PawnGeometry.Create(Vector2.Zero, 1f, slightShort);
        var second = PawnGeometry.Create(Vector2.Zero, 1f, broadTall);

        Assert.Equal(first.HeadBounds.Size, second.HeadBounds.Size);
        Assert.True(first.TorsoBounds.Width < second.TorsoBounds.Width);
        Assert.True(first.TorsoBounds.Height < second.TorsoBounds.Height);
    }

    [Fact]
    public void Create_EveryWeaponExtendsBeyondTorso()
    {
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                1f,
                baseAppearance with { WeaponRole = role });

            Assert.False(
                layout.TorsoBounds.Contains(layout.WeaponBounds),
                $"{role} weapon should extend beyond the torso.");
        }
    }

    [Fact]
    public void Create_VisualBoundsContainEveryRenderedPartAndSelectionPadding()
    {
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                3f,
                baseAppearance with { WeaponRole = role });

            Assert.True(layout.VisualBounds.Contains(layout.GroundRingBounds));
            Assert.True(layout.VisualBounds.Contains(layout.TorsoBounds));
            Assert.True(layout.VisualBounds.Contains(layout.HeadBounds));
            Assert.True(layout.VisualBounds.Contains(layout.HeadTreatmentBounds));
            Assert.True(layout.VisualBounds.Contains(layout.WeaponBounds));
            if (!layout.SecondaryEquipmentBounds.IsEmpty)
            {
                Assert.True(
                    layout.VisualBounds.Contains(
                        layout.SecondaryEquipmentBounds));
            }
            Assert.True(layout.VisualBounds.Contains(layout.SelectionBounds));
        }
    }

    /// <summary>
    /// No shield in the loadout draws no block on the battlefield: the
    /// silhouette carries only what the authoritative Core loadout grants.
    /// </summary>
    [Fact]
    public void Create_OmitsTheShieldBoundsForAnUnshieldedWarrior()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.None) with
        { ShieldRole = PawnShieldRole.None };

        var layout = PawnGeometry.Create(new Vector2(100, 100), 1f, appearance);

        Assert.True(layout.ShieldBounds.IsEmpty);
    }

    /// <summary>
    /// A shielded warrior draws a solid block beside the torso at every
    /// detail tier, including <see cref="PawnDetailTier.Low"/> — a shield
    /// changes what the warrior is, not how ornamented they are, so it must
    /// stay legible at distance and be contained in the visual bounds like
    /// every other rendered part.
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Create_DrawsTheShieldBoundsAtEveryDetailTierWhenShielded(
        float cameraZoom)
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom,
            appearance);

        Assert.False(layout.ShieldBounds.IsEmpty);
        Assert.True(layout.VisualBounds.Contains(layout.ShieldBounds));
    }

    /// <summary>
    /// The swing pose is optional so that every existing call site and every
    /// existing case here compiles and passes unchanged. A neutral pose is
    /// asserted alongside no pose at all, because <c>default(SwingPose)</c> is
    /// documented as a pawn standing as it does today and a caller may hand
    /// one over rather than a null.
    /// </summary>
    [Fact]
    public void Create_WithoutASwingPose_MatchesTheStaticLayout()
    {
        var footAnchor = new Vector2(137.25f, 241.75f);
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var appearance = baseAppearance with { WeaponRole = role };

            var withoutPose = PawnGeometry.Create(footAnchor, 2f, appearance);
            var withNullPose = PawnGeometry.Create(
                footAnchor,
                2f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null);
            var withNeutralPose = PawnGeometry.Create(
                footAnchor,
                2f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: default(SwingPose));

            Assert.Equal(withoutPose, withNullPose);
            Assert.Equal(withoutPose, withNeutralPose);
        }
    }

    /// <summary>
    /// The two operations the pose drives: the weapon line rotates about the
    /// grip, which leaves the grip where it was, and the torso leans along the
    /// swing while the feet stay planted.
    /// </summary>
    /// <remarks>
    /// Added beyond the single case the plan names for this task. The named
    /// case asserts that no pose changes nothing, which a parameter accepted
    /// and then ignored would also satisfy; this one fails in that situation.
    /// </remarks>
    [Fact]
    public void Create_WithASwingPose_RotatesTheWeaponAndLeansTheTorso()
    {
        var footAnchor = new Vector2(140f, 240f);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var pose = new SwingPose(
            SwingPhase.ImpactHold,
            PhaseProgress: 0.5f,
            WeaponAngleRadians: 0.8f,
            TorsoLeanX: 1.6f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);

        var still = PawnGeometry.Create(footAnchor, 2f, appearance);
        var swinging = PawnGeometry.Create(
            footAnchor,
            2f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose);

        Assert.Equal(footAnchor, swinging.FootAnchor);
        Assert.Equal(still.GroundRingBounds, swinging.GroundRingBounds);
        Assert.True(swinging.TorsoBounds.Left > still.TorsoBounds.Left);
        Assert.Equal(still.TorsoBounds.Size, swinging.TorsoBounds.Size);
        Assert.True(swinging.HeadBounds.Left > still.HeadBounds.Left);
        Assert.NotEqual(still.WeaponEnd, swinging.WeaponEnd);
        Assert.True(
            (swinging.WeaponEnd - swinging.WeaponStart).Length() >
            (still.WeaponEnd - still.WeaponStart).Length(),
            "An extended weapon line should be longer than the neutral one.");
        Assert.True(swinging.VisualBounds.Contains(swinging.WeaponBounds));
    }

    /// <summary>
    /// The arc lives on the layout so the renderer consumes it rather than
    /// deriving it a second time. That shape is required by the
    /// plains-backdrop review finding, where a duplicated formula left the
    /// shipped render loop uncovered.
    /// </summary>
    [Fact]
    public void Create_ExposesTheSwingTrailOnTheLayoutRatherThanRequiringTheRendererToRecomputeIt()
    {
        var footAnchor = new Vector2(140f, 240f);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var pose = new SwingPose(
            SwingPhase.Strike,
            PhaseProgress: 1f,
            WeaponAngleRadians: 0.8f,
            TorsoLeanX: 1.6f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);

        foreach (var cameraZoom in new[] { 1f, 3f })
        {
            var layout = PawnGeometry.Create(
                footAnchor,
                cameraZoom,
                appearance,
                scaleMultiplier: 1f,
                swingPose: pose);
            var trail = layout.SwingTrail;

            Assert.NotEqual(PawnDetailTier.Low, layout.DetailTier);
            Assert.False(trail.IsEmpty);
            Assert.Equal(layout.WeaponStart, trail.Pivot);
            Assert.Equal(
                (layout.WeaponEnd - layout.WeaponStart).Length(),
                trail.Radius,
                precision: 3);
            Assert.NotEqual(trail.StartAngleRadians, trail.EndAngleRadians);
            Assert.Equal(pose.TrailStrength, trail.Strength);
            Assert.True(trail.Thickness >= 1f);
        }
    }

    [Fact]
    public void Create_OmitsTheSwingTrailAtTheLowDetailTier()
    {
        var footAnchor = new Vector2(140f, 240f);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var pose = new SwingPose(
            SwingPhase.Strike,
            PhaseProgress: 1f,
            WeaponAngleRadians: 0.8f,
            TorsoLeanX: 1.6f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);

        var low = PawnGeometry.Create(
            footAnchor,
            cameraZoom: 0.05f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose);
        var untrailed = PawnGeometry.Create(
            footAnchor,
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose with { TrailStrength = 0f });

        Assert.Equal(PawnDetailTier.Low, low.DetailTier);
        Assert.True(low.SwingTrail.IsEmpty);
        Assert.Equal(default, low.SwingTrail);
        Assert.True(untrailed.SwingTrail.IsEmpty);
    }

    /// <summary>
    /// The named attachment points VIS-009 adds are scaffolding: they must
    /// equal the points the weapon and shield already draw from today, so
    /// that pinning them is a zero-visual-change change (integration design
    /// section 5, "Attachment points").
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Create_WeaponGripAnchorMatchesTheWeaponsDrawnStart(
        float cameraZoom)
    {
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                cameraZoom,
                baseAppearance with { WeaponRole = role });

            Assert.Equal(layout.WeaponStart, layout.WeaponGripAnchor);
        }
    }

    /// <summary>
    /// The grip anchor tracks the same pivot the weapon line rotates about
    /// under a swing pose, exactly as <see cref="PawnLayout.WeaponStart"/>
    /// does today.
    /// </summary>
    [Fact]
    public void Create_WeaponGripAnchorStaysAtTheSwingPivotUnderAPose()
    {
        var footAnchor = new Vector2(140f, 240f);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var pose = new SwingPose(
            SwingPhase.Strike,
            PhaseProgress: 1f,
            WeaponAngleRadians: 0.8f,
            TorsoLeanX: 1.6f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);

        var layout = PawnGeometry.Create(
            footAnchor,
            2f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose);

        Assert.Equal(layout.WeaponStart, layout.WeaponGripAnchor);
    }

    /// <summary>
    /// The shield anchor is the center of the block the renderer already
    /// fills for a shielded warrior.
    /// </summary>
    [Fact]
    public void Create_ShieldAnchorMatchesTheCenterOfTheDrawnShieldBlock()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };

        var layout = PawnGeometry.Create(new Vector2(100, 100), 1f, appearance);

        Assert.False(layout.ShieldBounds.IsEmpty);
        Assert.Equal(
            new Vector2(layout.ShieldBounds.Center.X, layout.ShieldBounds.Center.Y),
            layout.ShieldAnchor);
    }

    /// <summary>
    /// The shield anchor is a pure layout output, not a loadout-conditioned
    /// one: an unshielded warrior gets the identical attachment point a
    /// shielded warrior of the same build would, because the composed-layer
    /// design reserves it regardless of what is equipped (integration
    /// design section 5).
    /// </summary>
    [Fact]
    public void Create_ShieldAnchorIsIndependentOfWhetherAShieldIsEquipped()
    {
        var footAnchor = new Vector2(100, 100);
        var withShield = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };
        var withoutShield = withShield with { ShieldRole = PawnShieldRole.None };

        var shieldedLayout = PawnGeometry.Create(footAnchor, 1f, withShield);
        var unshieldedLayout = PawnGeometry.Create(footAnchor, 1f, withoutShield);

        Assert.True(unshieldedLayout.ShieldBounds.IsEmpty);
        Assert.Equal(shieldedLayout.ShieldAnchor, unshieldedLayout.ShieldAnchor);
    }

    // --- VIS-014 / OD-10: the shared aspect-ratio band and the per-skin
    // proportion deltas ---

    private static readonly string[] AllTallHardwoodSkinIds =
    [
        ShieldVisualCatalog.MactanThin.Catalog.Id,
        ShieldVisualCatalog.MorgaFullBody.Catalog.Id,
        ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
        ShieldVisualCatalog.VisayanKalasag.Catalog.Id,
    ];

    private static PawnAppearance ShieldedAppearance(string shieldSkinId) =>
        PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.TallHardwood) with
        {
            ShieldRole = PawnShieldRole.TallHardwood,
            ShieldSkinId = shieldSkinId,
        };

    /// <summary>
    /// Every skin's drawn rectangle stays inside the shared tall-shield
    /// aspect-ratio band at every detail tier (shield-visuals-design.md,
    /// OD-10 acceptance criteria): tight enough that no skin ever reads as a
    /// different equipment class.
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    [InlineData(1f)]
    [InlineData(1.35f)]
    [InlineData(1.8f)]
    [InlineData(3f)]
    [InlineData(24f)]
    public void Create_EveryTallHardwoodSkinStaysInsideTheSharedAspectRatioBand(
        float cameraZoom)
    {
        foreach (var skinId in AllTallHardwoodSkinIds)
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                cameraZoom,
                ShieldedAppearance(skinId));

            var ratio = layout.ShieldBounds.Width / (float)layout.ShieldBounds.Height;

            Assert.True(
                ratio is >= PawnGeometry.ShieldAspectRatioMinimum and
                    <= PawnGeometry.ShieldAspectRatioMaximum,
                $"{skinId} at cameraZoom {cameraZoom}: ratio {ratio} outside " +
                $"[{PawnGeometry.ShieldAspectRatioMinimum}, " +
                $"{PawnGeometry.ShieldAspectRatioMaximum}].");
        }
    }

    /// <summary>
    /// No skin's Low-tier block ever draws narrower or shorter than the
    /// original block's own Low-tier floor (R-W2.2): the acceptance
    /// criterion's literal "footprint floor" check.
    /// </summary>
    [Fact]
    public void Create_EveryTallHardwoodSkinMeetsTheLowTierFootprintFloor()
    {
        foreach (var skinId in AllTallHardwoodSkinIds)
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                0.05f,
                ShieldedAppearance(skinId));

            Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
            Assert.True(layout.ShieldBounds.Width >= 2);
            Assert.True(layout.ShieldBounds.Height >= 3);
        }
    }

    /// <summary>
    /// S2 <c>morgaFullBody</c>'s "tall end of the shared envelope": taller
    /// than S1 <c>mactanThin</c> at the same scale, width unchanged.
    /// </summary>
    [Fact]
    public void Create_MorgaFullBodyIsTallerThanMactanThinAtTheSameScale()
    {
        var mactanThin = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.MactanThin.Catalog.Id));
        var morgaFullBody = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.MorgaFullBody.Catalog.Id));

        Assert.Equal(mactanThin.ShieldBounds.Width, morgaFullBody.ShieldBounds.Width);
        Assert.True(morgaFullBody.ShieldBounds.Height > mactanThin.ShieldBounds.Height);
    }

    /// <summary>
    /// S5 <c>visayanKalasag</c>'s "narrowest proportion within the shared
    /// envelope": a strictly smaller width/height ratio than S1
    /// <c>mactanThin</c> at the same scale — never a smaller footprint
    /// (R-X.12, the false-cause guard OD-10 keeps).
    /// </summary>
    [Fact]
    public void Create_VisayanKalasagIsNarrowerThanMactanThinAtTheSameScale()
    {
        var mactanThin = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.MactanThin.Catalog.Id));
        var visayanKalasag = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.VisayanKalasag.Catalog.Id));

        var mactanThinRatio =
            mactanThin.ShieldBounds.Width / (float)mactanThin.ShieldBounds.Height;
        var visayanKalasagRatio =
            visayanKalasag.ShieldBounds.Width / (float)visayanKalasag.ShieldBounds.Height;

        Assert.True(visayanKalasagRatio < mactanThinRatio);
    }

    /// <summary>
    /// S3 <c>boxerCagayan</c> carries no proportion delta of its own — its
    /// only visual difference from S1 is the draw-time outline curvature
    /// (<c>PawnRenderer.DrawShield</c>), which never changes
    /// <see cref="PawnLayout.ShieldBounds"/>.
    /// </summary>
    [Fact]
    public void Create_BoxerCagayanSharesMactanThinsBaselineProportions()
    {
        var mactanThin = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.MactanThin.Catalog.Id));
        var boxerCagayan = PawnGeometry.Create(
            new Vector2(100, 100),
            1f,
            ShieldedAppearance(ShieldVisualCatalog.BoxerCagayan.Catalog.Id));

        Assert.Equal(mactanThin.ShieldBounds.Size, boxerCagayan.ShieldBounds.Size);
    }

    /// <summary>
    /// Stature and build multipliers grow the figure upward and outward from
    /// the ground-ring center rather than sinking the feet, matching the
    /// warrior-appearance research's category A rule ("every figure anchors
    /// at the feet") recorded in integration design section 5.
    /// </summary>
    [Fact]
    public void Create_GrowsTheFigureUpwardAndOutwardFromTheGroundRingCenterAsMultipliersGrow()
    {
        var footAnchor = new Vector2(120f, 220f);
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var shortNarrow = baseAppearance with
        {
            StatureMultiplier = 0.85f,
            BuildMultiplier = 0.85f,
        };
        var tallWide = baseAppearance with
        {
            StatureMultiplier = 1.15f,
            BuildMultiplier = 1.15f,
        };

        var shortLayout = PawnGeometry.Create(footAnchor, 1f, shortNarrow);
        var tallLayout = PawnGeometry.Create(footAnchor, 1f, tallWide);

        // The ground ring — the feet — never moves: only what stands on it
        // does.
        Assert.Equal(shortLayout.GroundRingBounds, tallLayout.GroundRingBounds);

        // Upward: a taller stature raises the torso and head tops without
        // moving their bottoms away from the planted feet.
        Assert.True(tallLayout.TorsoBounds.Top < shortLayout.TorsoBounds.Top);
        Assert.True(tallLayout.HeadBounds.Top < shortLayout.HeadBounds.Top);
        Assert.Equal(
            shortLayout.TorsoBounds.Bottom,
            tallLayout.TorsoBounds.Bottom);

        // Outward: a wider build expands the torso symmetrically to both
        // sides of the ground-ring center.
        Assert.True(tallLayout.TorsoBounds.Left < shortLayout.TorsoBounds.Left);
        Assert.True(tallLayout.TorsoBounds.Right > shortLayout.TorsoBounds.Right);
    }

    /// <summary>
    /// A literal regression pin for <c>PawnRenderer.GetBounds</c>, hand-
    /// derived from the geometry formulas in <see cref="PawnGeometry"/> at a
    /// zoom that clamps <see cref="PawnLayout.ApparentScale"/> to the exact
    /// constant 2.40, so the fixture carries no rounding ambiguity of its
    /// own. VIS-009 must not move this rectangle by a single pixel — the
    /// task's whole acceptance criterion is zero visual change.
    /// </summary>
    [Fact]
    public void GetBounds_MatchesThePinnedRegressionRectangle()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.None) with
        {
            WeaponRole = PawnWeaponRole.Kampilan,
            ShieldRole = PawnShieldRole.None,
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

        var bounds = PawnRenderer.GetBounds(
            new Vector2(100f, 100f),
            cameraZoom: 3f,
            appearance);

        Assert.Equal(new Rectangle(76, 36, 79, 72), bounds);
    }

    [Fact]
    public void Create_FixedPortraitScaleFitsRenderedPartsInsideFrame()
    {
        var portraitBounds = new Rectangle(0, 0, 56, 56);
        var footAnchor = new Vector2(
            portraitBounds.Center.X,
            portraitBounds.Bottom - 7);
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var layout = PawnGeometry.Create(
                footAnchor,
                cameraZoom: 1f,
                baseAppearance with { WeaponRole = role },
                scaleMultiplier: 1f);

            Assert.True(portraitBounds.Contains(layout.GroundRingBounds));
            Assert.True(portraitBounds.Contains(layout.TorsoBounds));
            Assert.True(portraitBounds.Contains(layout.HeadBounds));
            Assert.True(portraitBounds.Contains(layout.HeadTreatmentBounds));
            Assert.True(
                portraitBounds.Contains(layout.WeaponBounds),
                $"{role} weapon bounds {layout.WeaponBounds} should fit " +
                $"the inspector portrait frame {portraitBounds}.");

            if (!layout.SecondaryEquipmentBounds.IsEmpty)
            {
                Assert.True(
                    portraitBounds.Contains(
                        layout.SecondaryEquipmentBounds));
            }
        }
    }

    /// <summary>
    /// A stand-in for a real catalog lookup, used only to exercise the
    /// fallback chain (visual-system-integration-design.md section 4,
    /// R-W6.4) — never a production entry.
    /// </summary>
    private sealed record PlaceholderResolverDouble(string Id);

    /// <summary>
    /// VIS-008's automated verification names this explicitly: a resolver
    /// double, forced to fail every step so the chain falls all the way to
    /// <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>, is paired with
    /// the layout's already-computed placeholder rectangle — the thing a
    /// caller would draw once its own resolution reaches that step.
    /// <see cref="PawnLayout.PlaceholderBounds"/> is a pure, unconditional
    /// layout output (mirroring <see cref="PawnLayout.ShieldAnchor"/>'s
    /// unconditional-computation rule), so it is present whether or not any
    /// resolution actually failed; the resolver double only proves the step
    /// this rectangle is meant to answer for is reachable at all.
    /// </summary>
    [Fact]
    public void AResolverDoubleForcedToStep4YieldsThePlaceholderLayout()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (PlaceholderResolverDouble?)null,
            () => (PlaceholderResolverDouble?)null,
            () => (PlaceholderResolverDouble?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom: 3f,
            PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None));

        Assert.False(layout.PlaceholderBounds.IsEmpty);
    }

    /// <summary>
    /// A literal regression pin for
    /// <see cref="PawnLayout.PlaceholderBounds"/>, hand-derived from the
    /// geometry formulas the same way as
    /// <see cref="GetBounds_MatchesThePinnedRegressionRectangle"/>: zoom 3
    /// clamps <see cref="PawnLayout.ApparentScale"/> to the exact constant
    /// 2.40, so the fixture carries no rounding ambiguity of its own.
    /// </summary>
    [Fact]
    public void Create_PlaceholderBoundsMatchesThePinnedRegressionRectangle()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.None) with
        {
            WeaponRole = PawnWeaponRole.Kampilan,
            ShieldRole = PawnShieldRole.None,
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

        var layout = PawnGeometry.Create(
            new Vector2(100f, 100f),
            cameraZoom: 3f,
            appearance);

        Assert.Equal(new Rectangle(92, 74, 17, 17), layout.PlaceholderBounds);
    }

    /// <summary>
    /// The placeholder is a square inscribed in the torso footprint — the
    /// one element every pawn always has — so it is guaranteed conspicuous
    /// and never grows <see cref="PawnLayout.VisualBounds"/> beyond what the
    /// torso already contributes.
    /// </summary>
    [Fact]
    public void Create_PlaceholderBoundsIsASquareInscribedInTheTorso()
    {
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var role in Enum.GetValues<PawnWeaponRole>())
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                2f,
                baseAppearance with { WeaponRole = role });

            var expectedSide = Math.Min(
                layout.TorsoBounds.Width,
                layout.TorsoBounds.Height);

            Assert.Equal(expectedSide, layout.PlaceholderBounds.Width);
            Assert.Equal(expectedSide, layout.PlaceholderBounds.Height);
            Assert.True(layout.TorsoBounds.Contains(layout.PlaceholderBounds));
            Assert.True(layout.VisualBounds.Contains(layout.PlaceholderBounds));
        }
    }

    /// <summary>
    /// Same inputs, same rectangle, every call — the stability contract
    /// every layout output carries (integration design section 3).
    /// </summary>
    [Fact]
    public void Create_PlaceholderBoundsIsStableAcrossRepeatedCalls()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };

        var first = PawnGeometry.Create(new Vector2(50, 50), 1.5f, appearance);
        var second = PawnGeometry.Create(new Vector2(50, 50), 1.5f, appearance);

        Assert.Equal(first.PlaceholderBounds, second.PlaceholderBounds);
    }

    /// <summary>
    /// A pure layout output, not a loadout-conditioned one: the placeholder
    /// rectangle only depends on the torso, so a shielded and an unshielded
    /// warrior of the same build get the identical rectangle, exactly as
    /// <see cref="Create_ShieldAnchorIsIndependentOfWhetherAShieldIsEquipped"/>
    /// establishes for <see cref="PawnLayout.ShieldAnchor"/>.
    /// </summary>
    [Fact]
    public void Create_PlaceholderBoundsIsIndependentOfWhetherAShieldIsEquipped()
    {
        var footAnchor = new Vector2(100, 100);
        var withShield = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };
        var withoutShield = withShield with { ShieldRole = PawnShieldRole.None };

        var shieldedLayout = PawnGeometry.Create(footAnchor, 1f, withShield);
        var unshieldedLayout = PawnGeometry.Create(footAnchor, 1f, withoutShield);

        Assert.Equal(
            shieldedLayout.PlaceholderBounds,
            unshieldedLayout.PlaceholderBounds);
    }

    // --- VIS-015 / S12: active shield posture ---

    /// <summary>
    /// A literal regression pin for the S12 active posture (VIS-015,
    /// shield-visuals-design.md), hand-derived from the geometry formulas in
    /// <see cref="PawnGeometry"/> at a zoom that clamps
    /// <see cref="PawnLayout.ApparentScale"/> to the exact constant 2.40 —
    /// the same fixture convention
    /// <see cref="GetBounds_MatchesThePinnedRegressionRectangle"/> uses.
    /// Proves the named <c>PROVISIONAL</c>
    /// <see cref="PawnGeometry.ShieldPostureOffsetUnits"/> and
    /// <see cref="PawnGeometry.ShieldPostureRotationRadians"/> constants are
    /// the ones actually wired into the shield's drawn position and into
    /// the rotation carried on the layout for the renderer.
    /// </summary>
    [Fact]
    public void Create_ShieldPostureOffsetAndRotationMatchThePinnedRegressionRectangle()
    {
        Assert.Equal(1f, PawnGeometry.ShieldPostureOffsetUnits);
        Assert.Equal(0.15f, PawnGeometry.ShieldPostureRotationRadians);

        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        {
            WeaponRole = PawnWeaponRole.Kalis,
            ShieldRole = PawnShieldRole.TallHardwood,
            ShieldSkinId = ShieldVisualCatalog.MactanThin.Catalog.Id,
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

        var layout = PawnGeometry.Create(
            new Vector2(100f, 100f),
            cameraZoom: 3f,
            appearance);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.Equal(new Rectangle(75, 71, 10, 26), layout.ShieldBounds);
        Assert.Equal(new Vector2(80f, 84f), layout.ShieldAnchor);
        Assert.Equal(
            PawnGeometry.ShieldPostureRotationRadians,
            layout.ShieldPostureRotationRadians);
    }

    /// <summary>
    /// S12's posture offset and rotation add no pose channel of their own
    /// (shield-visuals-design.md: "identical for all four skins and constant
    /// over time... reads no combat state, is not animated"): with the
    /// torso's own vertical lean held at zero — the one pose component
    /// <c>CreateShield</c> already inherited before VIS-015, via
    /// <c>torsoBounds.Top</c> — an in-flight swing pose that moves the
    /// torso horizontally, rotates the weapon, and extends its reach leaves
    /// the shield rectangle, its anchor, and its posture rotation exactly as
    /// they were with no pose at all.
    /// </summary>
    [Fact]
    public void Create_ShieldBoundsAndPostureRotationAreIndependentOfSwingPoseAtZeroTorsoLeanY()
    {
        var appearance = ShieldedAppearance(ShieldVisualCatalog.MorgaFullBody.Catalog.Id);
        var footAnchor = new Vector2(120f, 220f);

        var stillLayout = PawnGeometry.Create(footAnchor, 1.5f, appearance);

        var pose = new SwingPose(
            SwingPhase.Strike,
            PhaseProgress: 1f,
            WeaponAngleRadians: 1.1f,
            TorsoLeanX: 2.4f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);
        var posedLayout = PawnGeometry.Create(
            footAnchor,
            1.5f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose);

        Assert.Equal(stillLayout.ShieldBounds, posedLayout.ShieldBounds);
        Assert.Equal(stillLayout.ShieldAnchor, posedLayout.ShieldAnchor);
        Assert.Equal(
            stillLayout.ShieldPostureRotationRadians,
            posedLayout.ShieldPostureRotationRadians);

        // Sanity: the pose really did move something else in the layout —
        // the torso's own horizontal lean — so the equalities above are not
        // a vacuous comparison of two identical calls.
        Assert.NotEqual(stillLayout.TorsoBounds, posedLayout.TorsoBounds);
    }

    /// <summary>
    /// The posture rotation is the same fixed constant for every skin
    /// (shield-visuals-design.md: "identical for all four skins") — only the
    /// per-skin proportion delta (VIS-014, OD-10) varies the block's own
    /// width and height, never the posture applied to it.
    /// </summary>
    [Fact]
    public void Create_ShieldPostureRotationIsIdenticalAcrossEveryTallHardwoodSkin()
    {
        foreach (var skinId in AllTallHardwoodSkinIds)
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                1f,
                ShieldedAppearance(skinId));

            Assert.Equal(
                PawnGeometry.ShieldPostureRotationRadians,
                layout.ShieldPostureRotationRadians);
        }
    }

    /// <summary>
    /// The posture offset is zeroed at <see cref="PawnDetailTier.Low"/>
    /// (<see cref="PawnGeometry"/>'s own remarks on
    /// <see cref="PawnGeometry.ShieldPostureOffsetUnits"/>), so the shield's
    /// left edge at that tier is bit-for-bit the pre-VIS-015 formula: the
    /// concrete form of bounds-neutrality the Low-tier non-occlusion
    /// guarantee below depends on.
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.6f)]
    public void Create_ShieldPositionIsUnchangedAtLowTier(float cameraZoom)
    {
        var appearance = ShieldedAppearance(ShieldVisualCatalog.MactanThin.Catalog.Id);
        var footAnchor = new Vector2(100, 100);

        var layout = PawnGeometry.Create(footAnchor, cameraZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);

        var expectedLeft =
            (int)MathF.Round(footAnchor.X - (7f * layout.ApparentScale))
            - layout.ShieldBounds.Width;

        Assert.Equal(expectedLeft, layout.ShieldBounds.Left);
    }

    /// <summary>
    /// The angled posture never occludes the faction ground ring, the
    /// weapon line, or the head at Low tier (shield-visuals-design.md,
    /// "Active posture (S12)"), for every shipped skin.
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.6f)]
    public void Create_ShieldNeverOccludesGroundRingWeaponOrHeadAtLowTier(
        float cameraZoom)
    {
        foreach (var skinId in AllTallHardwoodSkinIds)
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                cameraZoom,
                ShieldedAppearance(skinId));

            Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
            Assert.False(
                layout.ShieldBounds.Intersects(layout.GroundRingBounds),
                $"{skinId} at cameraZoom {cameraZoom}: shield {layout.ShieldBounds} " +
                $"occludes the ground ring {layout.GroundRingBounds}.");
            Assert.False(
                layout.ShieldBounds.Intersects(layout.HeadBounds),
                $"{skinId} at cameraZoom {cameraZoom}: shield {layout.ShieldBounds} " +
                $"occludes the head {layout.HeadBounds}.");
            Assert.False(
                layout.ShieldBounds.Intersects(layout.WeaponBounds),
                $"{skinId} at cameraZoom {cameraZoom}: shield {layout.ShieldBounds} " +
                $"occludes the weapon line {layout.WeaponBounds}.");
        }
    }

    // --- VIS-023: armor, sash, and adornment layers ---

    /// <summary>
    /// A literal regression pin for the three new layers, hand-derived from
    /// the geometry formulas in <see cref="PawnGeometry"/> at a zoom that
    /// clamps <see cref="PawnLayout.ApparentScale"/> to the exact constant
    /// 2.40 — the same fixture convention
    /// <see cref="GetBounds_MatchesThePinnedRegressionRectangle"/> and
    /// <see cref="Create_PlaceholderBoundsMatchesThePinnedRegressionRectangle"/>
    /// use, so <c>torsoBounds</c> is the already-verified
    /// <c>(92, 69, 17, 29)</c> and <c>headBounds</c> is
    /// <c>(92, 50, 17, 17)</c> those two tests independently confirm.
    /// </summary>
    [Fact]
    public void Create_ArmorSashAndAdornmentBoundsMatchThePinnedRegressionRectangles()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.None) with
        {
            WeaponRole = PawnWeaponRole.Kampilan,
            ShieldRole = PawnShieldRole.None,
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

        var layout = PawnGeometry.Create(
            new Vector2(100f, 100f),
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.Equal(new Rectangle(92, 69, 17, 29), layout.TorsoBounds);
        Assert.Equal(new Rectangle(92, 50, 17, 17), layout.HeadBounds);
        Assert.Equal(new Rectangle(90, 68, 20, 29), layout.ArmorBounds);
        Assert.Equal(new Rectangle(93, 83, 15, 2), layout.SashBounds);
        Assert.Equal(new Rectangle(107, 57, 2, 2), layout.AdornmentAccentPrimaryBounds);
        Assert.Equal(new Rectangle(99, 69, 2, 2), layout.AdornmentAccentSecondaryBounds);
    }

    [Theory]
    [InlineData(0.999f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Create_ThrowsWhenArmorWidthFactorIsBelowOneOrNonFinite(float armorWidthFactor)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PawnGeometry.Create(
                Vector2.Zero,
                1f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null,
                armorWidthFactor: armorWidthFactor));
    }

    /// <summary>
    /// R-W3.6: "Armor capsule widening is bounded inside the existing
    /// build-multiplier envelope (width factor at most 1.18)". A caller may
    /// not ask this layer to widen a pawn past
    /// <see cref="AppearanceComponentCatalog.MaxArmorWidthFactor"/>.
    /// </summary>
    [Fact]
    public void Create_ThrowsWhenArmorWidthFactorExceedsTheNamedCeiling()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PawnGeometry.Create(
                Vector2.Zero,
                1f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null,
                armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor + 0.01f));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_ThrowsWhenAdornmentAccentMarkCountIsNegative(int accentMarkCount)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PawnGeometry.Create(
                Vector2.Zero,
                1f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null,
                adornmentAccentMarkCount: accentMarkCount));
    }

    /// <summary>
    /// R-W3.6, "Area cap": at most
    /// <see cref="AppearanceComponentCatalog.MaxAccentMarksPerPawn"/> accent
    /// marks render per pawn.
    /// </summary>
    [Fact]
    public void Create_ThrowsWhenAdornmentAccentMarkCountExceedsTheNamedCap()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PawnGeometry.Create(
                Vector2.Zero,
                1f,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null,
                adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn + 1));
    }

    /// <summary>
    /// Every armor entry's width factor (VIS-019, category F, F2 through
    /// F4 — F1's own <c>1f</c> is the unarmored no-op, F5 does not widen at
    /// all) widens the capsule, and none can ever exceed the same named
    /// ceiling regardless of which factor is asked for.
    /// </summary>
    [Theory]
    [InlineData(1.06f)]
    [InlineData(1.12f)]
    [InlineData(1.18f)]
    public void Create_ArmorWideningNeverExceedsTheFactorTimesTheBaseTorsoWidth(
        float armorWidthFactor)
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.None) with
        {
            StatureMultiplier = 1f,
            BuildMultiplier = 1f,
        };

        var unarmored = PawnGeometry.Create(
            new Vector2(100, 100),
            3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null);
        var armored = PawnGeometry.Create(
            new Vector2(100, 100),
            3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: armorWidthFactor);

        Assert.False(armored.ArmorBounds.IsEmpty);
        Assert.True(armored.ArmorBounds.Width > unarmored.TorsoBounds.Width);
        Assert.True(
            armored.ArmorBounds.Width <=
            (int)MathF.Ceiling(
                unarmored.TorsoBounds.Width * AppearanceComponentCatalog.MaxArmorWidthFactor));
    }

    /// <summary>
    /// F1's own value ("no widening at all") is the layer's no-op state —
    /// the documented rollback ("no-op the three layer slots") for armor
    /// specifically.
    /// </summary>
    [Fact]
    public void Create_ArmorBoundsIsEmptyWhenTheWidthFactorIsTheUnarmoredDefault()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: 1f);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.True(layout.ArmorBounds.IsEmpty);
    }

    /// <summary>
    /// Every one of the three new layers gates on tier exactly as
    /// warrior-appearance-design.md's zoom table specifies: armor and sash
    /// from Medium tier up, adornment accents at High tier only.
    /// </summary>
    [Fact]
    public void Create_ArmorAndSashDrawFromMediumTierWhileAdornmentAccentsDrawOnlyAtHighTier()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var footAnchor = new Vector2(100, 100);

        PawnLayout Layout(float cameraZoom) => PawnGeometry.Create(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        var low = Layout(0.05f);
        var medium = Layout(1f);
        var high = Layout(3f);

        Assert.Equal(PawnDetailTier.Low, low.DetailTier);
        Assert.Equal(PawnDetailTier.Medium, medium.DetailTier);
        Assert.Equal(PawnDetailTier.High, high.DetailTier);

        Assert.True(low.ArmorBounds.IsEmpty);
        Assert.True(low.SashBounds.IsEmpty);
        Assert.True(low.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.True(low.AdornmentAccentSecondaryBounds.IsEmpty);

        Assert.False(medium.ArmorBounds.IsEmpty);
        Assert.False(medium.SashBounds.IsEmpty);
        Assert.True(medium.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.True(medium.AdornmentAccentSecondaryBounds.IsEmpty);

        Assert.False(high.ArmorBounds.IsEmpty);
        Assert.False(high.SashBounds.IsEmpty);
        Assert.False(high.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.False(high.AdornmentAccentSecondaryBounds.IsEmpty);
    }

    /// <summary>
    /// R-X.1, applied to the three new layers: nothing new may occlude the
    /// protected Low-tier set (ground ring, shield, weapon), proven here by
    /// the strongest form of that guarantee — the three layers draw nothing
    /// at all at Low tier, even when a pawn is fully equipped (maximally
    /// widened armor, a sash, and every accent).
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.6f)]
    public void Create_ArmorSashAndAdornmentBoundsAreEmptyAtLowTierEvenWhenFullyEquipped(
        float cameraZoom)
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood) with
        { ShieldRole = PawnShieldRole.TallHardwood };

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        Assert.True(layout.ArmorBounds.IsEmpty);
        Assert.True(layout.SashBounds.IsEmpty);
        Assert.True(layout.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.True(layout.AdornmentAccentSecondaryBounds.IsEmpty);
    }

    /// <summary>
    /// R-W3.6 rule 1: "clothing and adornment render only within the torso
    /// capsule ... footprint".
    /// </summary>
    [Fact]
    public void Create_SashBoundsIsInscribedInsideTheTorsoCapsule()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        foreach (var cameraZoom in new[] { 1f, 3f })
        {
            var layout = PawnGeometry.Create(
                new Vector2(100, 100),
                cameraZoom,
                appearance,
                scaleMultiplier: 1f,
                swingPose: null,
                hasSash: true);

            Assert.False(layout.SashBounds.IsEmpty);
            Assert.True(layout.TorsoBounds.Contains(layout.SashBounds));
        }
    }

    /// <summary>
    /// R-W3.6 rule 1: "within the torso capsule and head disk footprint,
    /// plus at most one pixel of accent overhang" — the primary mark (I4)
    /// stays inside the head disk and the secondary (I5/C3) stays inside
    /// the torso capsule, both with zero overhang.
    /// </summary>
    [Fact]
    public void Create_AdornmentAccentsAreInscribedInsideTheHeadAndTorsoFootprintWithNoOverhang()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.True(layout.HeadBounds.Contains(layout.AdornmentAccentPrimaryBounds));
        Assert.True(layout.TorsoBounds.Contains(layout.AdornmentAccentSecondaryBounds));
    }

    /// <summary>
    /// R-W3.6, "Area cap": each accent mark is at most
    /// <see cref="AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1"/>
    /// pixels on a side — a hard ceiling this layer never draws past,
    /// regardless of how far apparent scale climbs above 1.
    /// </summary>
    [Fact]
    public void Create_AdornmentAccentRectanglesNeverExceedTheNamedPixelSizeCap()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.True(
            layout.AdornmentAccentPrimaryBounds.Width <=
            AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1);
        Assert.True(
            layout.AdornmentAccentPrimaryBounds.Height <=
            AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1);
        Assert.True(
            layout.AdornmentAccentSecondaryBounds.Width <=
            AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1);
        Assert.True(
            layout.AdornmentAccentSecondaryBounds.Height <=
            AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1);
    }

    /// <summary>
    /// R-W3.6, "Area cap": <paramref name="accentMarkCount"/> controls how
    /// many of the two accent rectangles actually draw — a recipe naming
    /// zero, one, or two accents produces exactly that many non-empty
    /// rectangles, never more.
    /// </summary>
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    public void Create_AdornmentAccentMarkCountControlsHowManyAccentRectanglesAppear(
        int accentMarkCount,
        bool expectPrimary,
        bool expectSecondary)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            adornmentAccentMarkCount: accentMarkCount);

        Assert.Equal(expectPrimary, !layout.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.Equal(expectSecondary, !layout.AdornmentAccentSecondaryBounds.IsEmpty);
    }

    /// <summary>
    /// Integration design section 6 rule 1 ("composed appearance layers are
    /// time-invariant") and rule 2 ("pose-blind culling is preserved"): none
    /// of the three new layers reads <c>swingPose</c>. Held at zero torso
    /// lean the same way
    /// <see cref="Create_ShieldBoundsAndPostureRotationAreIndependentOfSwingPoseAtZeroTorsoLeanY"/>
    /// is, since <see cref="PawnLayout.ArmorBounds"/> and
    /// <see cref="PawnLayout.SashBounds"/> both derive their position from
    /// <c>torsoBounds</c>, which does lean under a nonzero pose — that lean
    /// is pre-existing behavior this task does not touch, not something
    /// these three layers introduce.
    /// </summary>
    [Fact]
    public void Create_ArmorSashAndAdornmentBoundsAreIndependentOfSwingPoseAtZeroTorsoLean()
    {
        var footAnchor = new Vector2(120f, 220f);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        PawnLayout Layout(SwingPose? pose) => PawnGeometry.Create(
            footAnchor,
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: pose,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        var stillLayout = Layout(null);
        var pose = new SwingPose(
            SwingPhase.Strike,
            PhaseProgress: 1f,
            WeaponAngleRadians: 1.1f,
            TorsoLeanX: 0f,
            TorsoLeanY: 0f,
            ExtensionRatio: 1f,
            TrailStrength: 1f);
        var posedLayout = Layout(pose);

        Assert.Equal(stillLayout.ArmorBounds, posedLayout.ArmorBounds);
        Assert.Equal(stillLayout.SashBounds, posedLayout.SashBounds);
        Assert.Equal(
            stillLayout.AdornmentAccentPrimaryBounds,
            posedLayout.AdornmentAccentPrimaryBounds);
        Assert.Equal(
            stillLayout.AdornmentAccentSecondaryBounds,
            posedLayout.AdornmentAccentSecondaryBounds);

        // Sanity: the pose really did move something else in the layout —
        // the weapon — so the equalities above are not a vacuous comparison
        // of two identical calls.
        Assert.NotEqual(stillLayout.WeaponEnd, posedLayout.WeaponEnd);
    }

    /// <summary>
    /// The composed-pawn design's own no-op guarantee: every default
    /// (<c>armorWidthFactor: 1f</c>, <c>hasSash: false</c>,
    /// <c>adornmentAccentMarkCount: 0</c>) draws nothing at every tier, so
    /// every existing call site that never names these three parameters is
    /// unaffected by this task (rollback: "no-op the three layer slots").
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Create_ArmorSashAndAdornmentBoundsAreEmptyByDefaultAtEveryTier(float cameraZoom)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom,
            appearance);

        Assert.True(layout.ArmorBounds.IsEmpty);
        Assert.True(layout.SashBounds.IsEmpty);
        Assert.True(layout.AdornmentAccentPrimaryBounds.IsEmpty);
        Assert.True(layout.AdornmentAccentSecondaryBounds.IsEmpty);
    }

    /// <summary>
    /// <see cref="PawnLayout.ArmorBounds"/> is the only one of the three new
    /// layers that can extend past the torso it widens (sash and accents
    /// are inscribed inside the torso/head footprint by construction — see
    /// <see cref="Create_SashBoundsIsInscribedInsideTheTorsoCapsule"/> and
    /// <see cref="Create_AdornmentAccentsAreInscribedInsideTheHeadAndTorsoFootprintWithNoOverhang"/>),
    /// so it is the only one that needs folding into the pose-blind
    /// <see cref="PawnLayout.VisualBounds"/>.
    /// </summary>
    [Fact]
    public void Create_VisualBoundsContainsTheWidenedArmorCapsule()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);

        var layout = PawnGeometry.Create(
            new Vector2(100, 100),
            cameraZoom: 3f,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor);

        Assert.False(layout.ArmorBounds.IsEmpty);
        Assert.True(layout.ArmorBounds.Width > layout.TorsoBounds.Width);
        Assert.True(layout.VisualBounds.Contains(layout.ArmorBounds));
    }
}
