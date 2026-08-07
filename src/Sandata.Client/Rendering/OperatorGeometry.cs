using Microsoft.Xna.Framework;
using Sandata.Core.Mathematics;

namespace Sandata.Client.Rendering;

/// <summary>
/// Pure geometry for the modern operator pawn (design section 11,
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md:1531-1567</c>). Mirrors
/// the shape of <c>Hukbo.Client.Rendering.PawnGeometry</c>: a single
/// <see cref="Create"/> function of plain values that returns an
/// <see cref="OperatorLayout"/>, with no <c>GraphicsDevice</c>, no
/// <c>SpriteBatch</c>, and no window anywhere in the call graph, so
/// <c>tests/Sandata.Client.Tests/OperatorGeometryTests.cs</c> can pin every
/// layer's <see cref="Rectangle"/> without constructing a graphics device —
/// the same pure-helper/impure-draw split the <c>hukbo-client-ui</c> skill
/// documents for <c>Hukbo.Client</c> and that
/// <c>src/Sandata.Client/Rendering/WorldRenderer.cs</c> already follows for
/// map geometry.
/// </summary>
/// <remarks>
/// <para>
/// The one requirement <c>PawnGeometry</c> never had is a persistent aim: a
/// warrior's weapon only ever occupies a transient swing pose that springs
/// back to neutral, but an operator's rifle must track a continuously
/// updating <see cref="Bam16"/> aim angle from the simulation without ever
/// visibly reversing direction at the point where the angle wraps from
/// 65,535 back to 0. <see cref="Create"/> therefore takes the previous
/// frame's displayed angle and blends it a <paramref name="smoothingFactor"/>
/// fraction of the way toward <paramref name="weaponAimBam"/> along
/// <see cref="Bam16.ShortestArc"/> rather than along a naive float lerp: the
/// shortest arc is wrap-aware by construction (see the worked example in
/// <see cref="Bam16.ShortestArc"/>'s own documentation), so the blend always
/// turns the short way, including through the wrap, and a smoothing factor of
/// 1 snaps immediately to the target with no lag at all.
/// </para>
/// </remarks>
internal static class OperatorGeometry
{
    // Layer sizes, in the same "apparent scale" world-like units
    // PawnGeometry's own constants use (for example
    // src/Hukbo.Client/Rendering/PawnGeometry.cs:319's TorsoHeightUnits).
    // Every one is even so that, at an integer apparentScale and an integer
    // rootPosition, every centered rectangle below lands on an exact integer
    // with no rounding ambiguity for a test to depend on.
    internal const float GroundRingSize = 12f;
    internal const float BootsWidth = 6f;
    internal const float BootsHeight = 4f;
    internal const float LegsWidth = 6f;
    internal const float LegsHeight = 6f;
    internal const float TorsoWidth = 8f;
    internal const float TorsoHeight = 10f;
    internal const float PlateCarrierWidth = 10f;
    internal const float PlateCarrierHeight = 6f;
    internal const float ArmsWidth = 14f;
    internal const float ArmsHeight = 4f;
    internal const float HeadSize = 4f;
    internal const float HelmetSize = 6f;
    internal const float NightVisionMountWidth = 2f;
    internal const float NightVisionMountHeight = 2f;
    internal const float SlingWidth = 10f;
    internal const float SlingHeight = 2f;
    internal const float SuppressionBracketSize = 2f;
    internal const float SelectionRingSize = 16f;
    internal const float WeaponLength = 16f;
    internal const float WeaponThickness = 2f;
    internal const float WeaponForegripWidth = 4f;
    internal const float WeaponForegripHeight = 2f;
    internal const float MuzzleFlashSize = 4f;

    // Distances of the two weapon-mounted layers from the grip anchor, along
    // the weapon's own rotated direction vector, in the same units as
    // WeaponLength (a fraction of it, chosen as exact quarters so both stay
    // whole numbers under an integer scale).
    internal const float ForegripDistanceFromGrip = 4f;
    internal const float SuppressionBracketDistanceFromGrip = 12f;

    // Vertical placement of every non-weapon-mounted layer, as an offset from
    // rootPosition along Y before the apparentScale multiply. These describe
    // a rough standing silhouette from the ground ring at the foot anchor up
    // through the head; they carry no historical or simulation meaning and
    // exist purely to give each layer a distinct, pinned position.
    internal const float GroundRingCenterYOffset = 0f;
    internal const float BootsCenterYOffset = -2f;
    internal const float LegsCenterYOffset = -6f;
    internal const float TorsoCenterYOffset = -12f;
    internal const float PlateCarrierCenterYOffset = -13f;
    internal const float ArmsCenterYOffset = -11f;
    internal const float HeadCenterYOffset = -18f;
    internal const float HelmetCenterYOffset = -19f;
    internal const float NightVisionMountCenterYOffset = -20f;
    internal const float SlingCenterYOffset = -12f;
    internal const float WeaponGripCenterYOffset = -11f;

    /// <summary>
    /// Builds the fifteen-layer <see cref="OperatorLayout"/> for one operator
    /// on one frame.
    /// </summary>
    /// <param name="rootPosition">
    /// The operator's ground/foot anchor, the same role
    /// <c>PawnLayout.FootAnchor</c> plays for a warrior.
    /// </param>
    /// <param name="apparentScale">
    /// A positive, finite zoom-independent size multiplier applied to every
    /// layer constant above.
    /// </param>
    /// <param name="detailTier">
    /// Gates the plate carrier, arms, weapon foregrip, helmet, and sling
    /// layers on at <see cref="OperatorDetailTier.Medium"/> and above, and
    /// the night-vision mount and suppression bracket layers on at
    /// <see cref="OperatorDetailTier.High"/> only. <see cref="OperatorDetailTier.Low"/>
    /// shows only the ground ring, boots, legs, torso, weapon body, and head.
    /// </param>
    /// <param name="weaponAimBam">
    /// The authoritative aim angle the simulation hashes, carried unchanged
    /// onto the returned layout.
    /// </param>
    /// <param name="previousDisplayRotationRawUnits">
    /// The previous frame's <see cref="OperatorLayout.DisplayRotationRawUnits"/>,
    /// or 0 on the first frame an operator is drawn.
    /// </param>
    /// <param name="smoothingFactor">
    /// How far along the shortest arc to <paramref name="weaponAimBam"/> to
    /// move this frame, in the closed range [0, 1]. 0 leaves the displayed
    /// angle unchanged; 1 snaps immediately to the target.
    /// </param>
    /// <param name="isFiring">
    /// Gates the one-frame muzzle flash layer, anchored at
    /// <see cref="OperatorLayout.WeaponMuzzleAnchor"/>.
    /// </param>
    /// <param name="isSelected">Gates the selection ring layer.</param>
    internal static OperatorLayout Create(
        Vector2 rootPosition,
        float apparentScale,
        OperatorDetailTier detailTier,
        Bam16 weaponAimBam,
        float previousDisplayRotationRawUnits,
        float smoothingFactor,
        bool isFiring,
        bool isSelected)
    {
        if (!float.IsFinite(apparentScale) || apparentScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(apparentScale),
                apparentScale,
                "Apparent scale must be a finite, positive value.");
        }

        if (!float.IsFinite(previousDisplayRotationRawUnits))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousDisplayRotationRawUnits),
                previousDisplayRotationRawUnits,
                "The previous displayed rotation must be finite.");
        }

        if (!float.IsFinite(smoothingFactor) || smoothingFactor < 0f || smoothingFactor > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(smoothingFactor),
                smoothingFactor,
                "Smoothing factor must lie in the closed range [0, 1].");
        }

        var displayRotationRawUnits = BlendTowardTarget(
            previousDisplayRotationRawUnits,
            weaponAimBam,
            smoothingFactor);
        var rotationRadians = RawUnitsToRadians(displayRotationRawUnits);
        var weaponDirection = new Vector2(MathF.Cos(rotationRadians), MathF.Sin(rotationRadians));

        var weaponGripAnchor = rootPosition +
            (new Vector2(0f, WeaponGripCenterYOffset) * apparentScale);
        var weaponMuzzleAnchor = weaponGripAnchor +
            (weaponDirection * (WeaponLength * apparentScale));

        var showGearLayer = detailTier is OperatorDetailTier.Medium or OperatorDetailTier.High;
        var showOpticsLayer = detailTier == OperatorDetailTier.High;

        var groundRingBounds = CenteredRect(
            rootPosition + (new Vector2(0f, GroundRingCenterYOffset) * apparentScale),
            GroundRingSize * apparentScale,
            GroundRingSize * apparentScale);

        var bootsBounds = CenteredRect(
            rootPosition + (new Vector2(0f, BootsCenterYOffset) * apparentScale),
            BootsWidth * apparentScale,
            BootsHeight * apparentScale);

        var legsBounds = CenteredRect(
            rootPosition + (new Vector2(0f, LegsCenterYOffset) * apparentScale),
            LegsWidth * apparentScale,
            LegsHeight * apparentScale);

        var torsoBounds = CenteredRect(
            rootPosition + (new Vector2(0f, TorsoCenterYOffset) * apparentScale),
            TorsoWidth * apparentScale,
            TorsoHeight * apparentScale);

        var plateCarrierBounds = showGearLayer
            ? CenteredRect(
                rootPosition + (new Vector2(0f, PlateCarrierCenterYOffset) * apparentScale),
                PlateCarrierWidth * apparentScale,
                PlateCarrierHeight * apparentScale)
            : Rectangle.Empty;

        var armsBounds = showGearLayer
            ? CenteredRect(
                rootPosition + (new Vector2(0f, ArmsCenterYOffset) * apparentScale),
                ArmsWidth * apparentScale,
                ArmsHeight * apparentScale)
            : Rectangle.Empty;

        var weaponBodyBounds = CenteredRect(
            weaponGripAnchor,
            WeaponLength * apparentScale,
            WeaponThickness * apparentScale);

        var weaponForegripBounds = showGearLayer
            ? CenteredRect(
                weaponGripAnchor + (weaponDirection * (ForegripDistanceFromGrip * apparentScale)),
                WeaponForegripWidth * apparentScale,
                WeaponForegripHeight * apparentScale)
            : Rectangle.Empty;

        var headBounds = CenteredRect(
            rootPosition + (new Vector2(0f, HeadCenterYOffset) * apparentScale),
            HeadSize * apparentScale,
            HeadSize * apparentScale);

        var helmetBounds = showGearLayer
            ? CenteredRect(
                rootPosition + (new Vector2(0f, HelmetCenterYOffset) * apparentScale),
                HelmetSize * apparentScale,
                HelmetSize * apparentScale)
            : Rectangle.Empty;

        var nightVisionMountBounds = showOpticsLayer
            ? CenteredRect(
                rootPosition + (new Vector2(0f, NightVisionMountCenterYOffset) * apparentScale),
                NightVisionMountWidth * apparentScale,
                NightVisionMountHeight * apparentScale)
            : Rectangle.Empty;

        var muzzleFlashBounds = isFiring
            ? CenteredRect(weaponMuzzleAnchor, MuzzleFlashSize * apparentScale, MuzzleFlashSize * apparentScale)
            : Rectangle.Empty;

        var slingBounds = showGearLayer
            ? CenteredRect(
                rootPosition + (new Vector2(0f, SlingCenterYOffset) * apparentScale),
                SlingWidth * apparentScale,
                SlingHeight * apparentScale)
            : Rectangle.Empty;

        var suppressionBracketBounds = showOpticsLayer
            ? CenteredRect(
                weaponGripAnchor + (weaponDirection * (SuppressionBracketDistanceFromGrip * apparentScale)),
                SuppressionBracketSize * apparentScale,
                SuppressionBracketSize * apparentScale)
            : Rectangle.Empty;

        var selectionRingBounds = isSelected
            ? CenteredRect(
                rootPosition + (new Vector2(0f, GroundRingCenterYOffset) * apparentScale),
                SelectionRingSize * apparentScale,
                SelectionRingSize * apparentScale)
            : Rectangle.Empty;

        return new OperatorLayout(
            detailTier,
            weaponAimBam,
            weaponGripAnchor,
            weaponMuzzleAnchor,
            groundRingBounds,
            bootsBounds,
            legsBounds,
            torsoBounds,
            plateCarrierBounds,
            armsBounds,
            weaponBodyBounds,
            weaponForegripBounds,
            headBounds,
            helmetBounds,
            nightVisionMountBounds,
            muzzleFlashBounds,
            slingBounds,
            suppressionBracketBounds,
            selectionRingBounds)
        {
            DisplayRotationRawUnits = displayRotationRawUnits,
        };
    }

    /// <summary>
    /// Converts a raw <see cref="Bam16"/>-space angle (0 through 65,535 per
    /// full turn) to radians for <see cref="MathF.Cos"/>/<see cref="MathF.Sin"/>.
    /// </summary>
    private static float RawUnitsToRadians(float rawUnits) =>
        WrapRawUnits(rawUnits) / Bam16.UnitsPerTurn * MathF.Tau;

    /// <summary>
    /// Moves <paramref name="previousRawUnits"/> a <paramref name="smoothingFactor"/>
    /// fraction of the way toward <paramref name="targetAimBam"/>, using
    /// <see cref="Bam16.ShortestArc"/> so the move always takes the short way
    /// around the ring — including through the wrap from 65,535 back to 0 —
    /// rather than the long way a naive float lerp between two raw angle
    /// values would take whenever the short path happens to cross that wrap.
    /// </summary>
    private static float BlendTowardTarget(
        float previousRawUnits,
        Bam16 targetAimBam,
        float smoothingFactor)
    {
        var previousBam = new Bam16(RawUnitsToUShort(previousRawUnits));
        var shortestArc = Bam16.ShortestArc(previousBam, targetAimBam);
        return WrapRawUnits(previousRawUnits + (shortestArc * smoothingFactor));
    }

    /// <summary>
    /// Wraps a raw angle value into the half-open range [0, 65,536), the
    /// range every <see cref="Bam16.Raw"/> value already occupies.
    /// </summary>
    private static float WrapRawUnits(float rawUnits)
    {
        var wrapped = rawUnits % Bam16.UnitsPerTurn;
        return wrapped < 0f ? wrapped + Bam16.UnitsPerTurn : wrapped;
    }

    /// <summary>
    /// Wraps and rounds a raw angle value to the nearest <see cref="ushort"/>,
    /// guarding the one case where rounding a value just below 65,536 rounds
    /// up to exactly 65,536 — a value one past the end of <see cref="ushort"/>'s
    /// range — by folding it back to 0 rather than letting an out-of-range
    /// float-to-integral cast produce an unspecified result.
    /// </summary>
    private static ushort RawUnitsToUShort(float rawUnits)
    {
        var rounded = MathF.Round(WrapRawUnits(rawUnits));
        if (rounded >= Bam16.UnitsPerTurn)
        {
            rounded -= Bam16.UnitsPerTurn;
        }

        return (ushort)rounded;
    }

    /// <summary>
    /// A rectangle of the given size centered on <paramref name="center"/>,
    /// rounding each edge independently the same way
    /// <c>PawnGeometry.Create</c> rounds every rectangle it builds (for
    /// example <c>src/Hukbo.Client/Rendering/PawnGeometry.cs:1627-1629</c>).
    /// </summary>
    private static Rectangle CenteredRect(Vector2 center, float width, float height) =>
        new(
            (int)MathF.Round(center.X - (width / 2f)),
            (int)MathF.Round(center.Y - (height / 2f)),
            (int)MathF.Round(width),
            (int)MathF.Round(height));
}
