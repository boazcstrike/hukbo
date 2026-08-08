using Microsoft.Xna.Framework;
using Sandata.Core.Mathematics;

namespace Sandata.Client.Rendering;

/// <summary>
/// Detail tier gating which of the fifteen operator layers (design section 11,
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md:1531-1567</c>) currently
/// draw. Declared independently of <c>Hukbo.Client.Rendering.PawnDetailTier</c>
/// (<c>src/Hukbo.Client/Rendering/PawnGeometry.cs:7-12</c>) rather than shared
/// with it: Sandata.Client owns its own rendering types end to end and does not
/// reference <c>Hukbo.Client</c>.
/// </summary>
internal enum OperatorDetailTier
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The pure geometry an operator renderer draws from: the fifteen composed
/// layers design section 11 names — ground ring, boots, legs, torso, plate
/// carrier, arms, weapon body, weapon foregrip, head, helmet, night-vision
/// mount, muzzle flash, sling, suppression bracket, selection ring — each a
/// <see cref="Rectangle"/> that is <see cref="Rectangle.Empty"/> whenever the
/// layer contributes nothing at the current detail tier or firing/selection
/// state. This is the same "pure output, empty when absent" convention
/// <c>Hukbo.Client.Rendering.PawnLayout</c>
/// (<c>src/Hukbo.Client/Rendering/PawnGeometry.cs:81-142</c>) already uses, and
/// <see cref="OperatorGeometry.Create"/> is this record's only producer, the
/// same split <c>PawnGeometry.Create</c> and <c>PawnRenderer.DrawLayout</c>
/// (<c>src/Hukbo.Client/Rendering/PawnRenderer.cs:267-463</c>) keep between
/// pure geometry and impure drawing.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WeaponAimBam"/> is the authoritative aim angle sourced from the
/// simulation's hashed <see cref="Bam16"/> state, so it is part of this
/// record's equality like every other member. <see cref="DisplayRotationRawUnits"/>
/// is different in kind: it is a presentation-only smoothing term — the
/// client-side angle actually rendered this frame, blended a fraction of the
/// way from the previous frame's displayed angle toward
/// <see cref="WeaponAimBam"/> along <see cref="Bam16.ShortestArc"/> — and it
/// never feeds back into the simulation. Because an ordinary auto-property
/// still contributes a backing field the compiler folds into a record's
/// generated equality, this record hand-writes
/// <see cref="Equals(OperatorLayout)"/> and <see cref="GetHashCode"/> to
/// leave <see cref="DisplayRotationRawUnits"/> out, the same technique
/// <c>Hukbo.Client.UI.ArmyComposition</c>
/// (<c>src/Hukbo.Client/UI/ArmyCompositionPanel.cs:21-49</c>) uses to hand
/// exclude a member the default record equality would otherwise fold in.
/// </para>
/// </remarks>
internal readonly record struct OperatorLayout(
    OperatorDetailTier DetailTier,
    Bam16 WeaponAimBam,
    Vector2 WeaponGripAnchor,
    Vector2 WeaponMuzzleAnchor,
    Rectangle GroundRingBounds,
    Rectangle BootsBounds,
    Rectangle LegsBounds,
    Rectangle TorsoBounds,
    Rectangle PlateCarrierBounds,
    Rectangle ArmsBounds,
    Rectangle WeaponBodyBounds,
    Rectangle WeaponForegripBounds,
    Rectangle HeadBounds,
    Rectangle HelmetBounds,
    Rectangle NightVisionMountBounds,
    Rectangle MuzzleFlashBounds,
    Rectangle SlingBounds,
    Rectangle SuppressionBracketBounds,
    Rectangle SelectionRingBounds)
{
    /// <summary>
    /// The angle actually rendered this frame, in the same raw
    /// <see cref="Bam16"/> unit space (0 through 65,535 per full turn,
    /// wrapping freely) as <see cref="WeaponAimBam"/> so a caller can keep
    /// blending it forward next frame with <see cref="Bam16.ShortestArc"/>
    /// without ever crossing the wrap the wrong way. Deliberately excluded
    /// from <see cref="Equals(OperatorLayout)"/> and <see cref="GetHashCode"/>
    /// — see the remarks above.
    /// </summary>
    public float DisplayRotationRawUnits { get; init; }

    /// <inheritdoc/>
    public bool Equals(OperatorLayout other) =>
        DetailTier == other.DetailTier &&
        WeaponAimBam == other.WeaponAimBam &&
        WeaponGripAnchor == other.WeaponGripAnchor &&
        WeaponMuzzleAnchor == other.WeaponMuzzleAnchor &&
        GroundRingBounds == other.GroundRingBounds &&
        BootsBounds == other.BootsBounds &&
        LegsBounds == other.LegsBounds &&
        TorsoBounds == other.TorsoBounds &&
        PlateCarrierBounds == other.PlateCarrierBounds &&
        ArmsBounds == other.ArmsBounds &&
        WeaponBodyBounds == other.WeaponBodyBounds &&
        WeaponForegripBounds == other.WeaponForegripBounds &&
        HeadBounds == other.HeadBounds &&
        HelmetBounds == other.HelmetBounds &&
        NightVisionMountBounds == other.NightVisionMountBounds &&
        MuzzleFlashBounds == other.MuzzleFlashBounds &&
        SlingBounds == other.SlingBounds &&
        SuppressionBracketBounds == other.SuppressionBracketBounds &&
        SelectionRingBounds == other.SelectionRingBounds;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DetailTier);
        hash.Add(WeaponAimBam);
        hash.Add(WeaponGripAnchor);
        hash.Add(WeaponMuzzleAnchor);
        hash.Add(GroundRingBounds);
        hash.Add(BootsBounds);
        hash.Add(LegsBounds);
        hash.Add(TorsoBounds);
        hash.Add(PlateCarrierBounds);
        hash.Add(ArmsBounds);
        hash.Add(WeaponBodyBounds);
        hash.Add(WeaponForegripBounds);
        hash.Add(HeadBounds);
        hash.Add(HelmetBounds);
        hash.Add(NightVisionMountBounds);
        hash.Add(MuzzleFlashBounds);
        hash.Add(SlingBounds);
        hash.Add(SuppressionBracketBounds);
        hash.Add(SelectionRingBounds);
        return hash.ToHashCode();
    }
}
