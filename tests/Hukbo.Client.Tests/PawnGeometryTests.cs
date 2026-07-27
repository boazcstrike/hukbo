using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class PawnGeometryTests
{
    [Fact]
    public void Create_AppliesMonotonicClampedZoomScaling()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);
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
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);
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
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);
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
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);
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
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);
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

    [Fact]
    public void Create_FixedPortraitScaleFitsRenderedPartsInsideFrame()
    {
        var portraitBounds = new Rectangle(0, 0, 56, 56);
        var footAnchor = new Vector2(
            portraitBounds.Center.X,
            portraitBounds.Bottom - 7);
        var baseAppearance = PawnAppearanceFactory.Create(0, WeaponId.GreatBlade);

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
}
