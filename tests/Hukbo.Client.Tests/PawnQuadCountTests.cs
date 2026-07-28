using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="PawnQuadCount.Count"/> against
/// <c>PawnRenderer.Draw</c>'s real draw order (VIS-034, amendment A-1): every
/// expected value below was derived by walking that renderer method by
/// method, not by running the renderer itself (it needs a graphics device).
/// Any task that adds a primitive to <c>PawnRenderer</c> must update these
/// pins deliberately, in the same diff, with the budget arithmetic in the
/// commit message (the anti-density-creep rule).
/// </summary>
public sealed class PawnQuadCountTests
{
    /// <summary>
    /// Camera zooms matching <c>PawnGeometryTests</c>'s own convention for
    /// exercising all three detail tiers.
    /// </summary>
    private const float LowTierZoom = 0.05f;

    private const float MediumTierZoom = 1f;

    private const float HighTierZoom = 3f;

    [Fact]
    public void Count_PinsTheLowTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        Assert.Equal(
            17,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    [Fact]
    public void Count_PinsTheMediumTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, MediumTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Medium, layout.DetailTier);
        Assert.Equal(
            19,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    [Fact]
    public void Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.Equal(
            20,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    /// <summary>
    /// The High-tier worst case: every optional layer filled with its
    /// densest option at once — a Wasay with its rattan lashing band, a
    /// <c>boxerCagayan</c>-skinned shield (the only skin whose curved face
    /// adds two extra quads over the plain block), full armor widening, a
    /// sash, both adornment accent marks, and a selected-state outline. Not
    /// every one of these can occur together on a single loadout in the
    /// shipped game (armor/sash/adornments are VIS-023 layers with their own
    /// resolution rules); this is the counting seam's own worst-case ceiling,
    /// exercising every conditional branch <c>PawnQuadCount.Count</c> has at
    /// once, matching the design's "pin the worst case" instruction rather
    /// than one specific in-game appearance.
    /// </summary>
    [Fact]
    public void Count_PinsTheHighTierFullyLoadedSelectedPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Wasay, ShieldId.TallHardwood)
            with
        {
            WeaponLashingBandColor = Color.White,
            ShieldSkinId = ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
        };
        var layout = PawnGeometry.Create(
            Vector2.Zero,
            HighTierZoom,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.False(layout.ArmorBounds.IsEmpty);
        Assert.False(layout.SashBounds.IsEmpty);
        Assert.False(layout.AdornmentAccentSecondaryBounds.IsEmpty);
        Assert.Equal(
            40,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Selected));
    }

    /// <summary>
    /// <c>PawnRenderer.DrawSelectionMark</c> draws four corners of two
    /// rectangles each — a fixed +8 over the same layout's normal-state
    /// count, independent of every other conditional in
    /// <see cref="PawnQuadCount.Count"/>. A differential assertion rather
    /// than a second absolute pin, so it stays correct even if some other
    /// element's count changes.
    /// </summary>
    [Theory]
    [InlineData(nameof(PawnVisualState.Hovered))]
    [InlineData(nameof(PawnVisualState.Selected))]
    public void Count_AddsExactlyEightForTheSelectionMark(string stateName)
    {
        var state = Enum.Parse<PawnVisualState>(stateName);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        var normal = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var marked = PawnQuadCount.Count(layout, appearance, state);

        Assert.Equal(normal + 8, marked);
    }

    /// <summary>
    /// <c>PawnRenderer.DrawDeadMark</c> draws two crossed lines — a fixed +2
    /// over the same layout's normal-state count.
    /// </summary>
    [Fact]
    public void Count_AddsExactlyTwoForTheDeadMark()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        var normal = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var dead = PawnQuadCount.Count(layout, appearance, PawnVisualState.Dead);

        Assert.Equal(normal + 2, dead);
    }

    /// <summary>
    /// Every weapon role draws through the same three-line
    /// <c>DrawBlade</c> call — geometry differs by role, quad count does
    /// not. Restricted to the two roles whose
    /// <c>SecondaryEquipmentBounds</c> is empty at every tier (Kampilan and
    /// Kalis; Wasay and Itak each add their own secondary-equipment quads,
    /// covered separately above), and to the same entity ID for both calls,
    /// so stature/build/head-treatment — themselves entity-derived, not
    /// weapon-derived — cannot be the reason two totals match.
    /// </summary>
    [Fact]
    public void Count_TheWeaponAlwaysContributesTheSameQuadsRegardlessOfRole()
    {
        var kampilan = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var kalis = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var kampilanLayout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, kampilan);
        var kalisLayout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, kalis);

        Assert.True(kampilanLayout.SecondaryEquipmentBounds.IsEmpty);
        Assert.True(kalisLayout.SecondaryEquipmentBounds.IsEmpty);
        Assert.Equal(
            PawnQuadCount.Count(kampilanLayout, kampilan, PawnVisualState.Normal),
            PawnQuadCount.Count(kalisLayout, kalis, PawnVisualState.Normal));
    }

    [Fact]
    public void Count_TorsoPlaceholderDrawsExactlyOneQuadInsteadOfTheTorsoCapsule()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        var normalTorso = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            VisualFallbackStep.ModelCategoryDefault);
        var placeholderTorso = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            VisualFallbackStep.DiagnosticPlaceholder);

        Assert.False(layout.PlaceholderBounds.IsEmpty);
        // The placeholder (1 quad) replaces the torso capsule (7 quads at
        // High tier: 3 outline + 3 fill + 1 belt), a difference of 6.
        Assert.Equal(normalTorso - 6, placeholderTorso);
    }
}
