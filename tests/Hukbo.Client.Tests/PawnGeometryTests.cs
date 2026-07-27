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
}
