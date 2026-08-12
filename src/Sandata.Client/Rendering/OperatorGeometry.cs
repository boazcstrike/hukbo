using Microsoft.Xna.Framework;
using Sandata.Core.Mathematics;
using Sandata.Core.Weapons;

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
    // Raised from 16 to 22 on 2026-08-12. At 16 the rifle barely reached past
    // the 12-unit ground ring, so at the zoom a spectator actually starts at
    // the weapon was a couple of pixels buried inside the body's own
    // footprint, and the report was "still the guns are unclear". At 22 the
    // muzzle clears the ring by a body's width, which is what makes a rifle
    // read as a rifle before anybody zooms in. The pistol below is unchanged,
    // so the same edit widens the rifle-versus-pistol gap smoke row SD-4 asks
    // a tester to see.
    internal const float WeaponLength = 22f;
    internal const float WeaponThickness = 2f;
    internal const float WeaponForegripWidth = 4f;
    internal const float WeaponForegripHeight = 2f;
    internal const float MuzzleFlashSize = 4f;

    // A WeaponClass.Pistol weapon body: roughly half of WeaponLength so it
    // reads as a handgun rather than a shortened rifle, and one unit
    // thicker than WeaponThickness so the stubbier silhouette is legible at
    // Medium tier rather than just a shorter sliver of the same thickness.
    internal const float PistolWeaponLength = 8f;
    internal const float PistolWeaponThickness = 3f;

    // The friendly-only head-top pip (the second of the two faction-shape
    // cues design section 11's silhouette work calls for): a small square
    // that has to read even when the operator itself is only a few pixels
    // tall, so it is gated on isFriendly alone and never on detailTier the
    // way the helmet layer it sits above is.
    internal const float HeadPipSize = 2f;

    // The ground ring's rotation when isFriendly is false: a 45-degree turn
    // of the same square footprint into a diamond. GroundRingSize is the
    // largest layer on purpose (see Create's isFriendly remarks), so this is
    // the cue meant to survive at far zoom.
    internal const float GroundRingHostileRotationRadians = MathF.PI / 4f;

    // A lowered weapon: how far its drawn direction turns away from the aim
    // direction, and how much of its length survives the turn. Design section
    // 9 makes the lowered flag hashed state and requires its transition to be
    // observable, but says nothing about what a lowered weapon looks like from
    // directly overhead, so both values below are PROVISIONAL presentation
    // decisions made on 2026-08-12 for smoke row SD-4.
    //
    // The pair is chosen to read as "port arms" from above: the muzzle swings
    // roughly fifty degrees off the aim line and the barrel foreshortens,
    // which is what a weapon pulled in across the chest actually projects to
    // on the ground plane. Rotation alone was rejected — at the zoom a
    // spectator plays at, a rifle turned fifty degrees still reads as a rifle
    // pointing somewhere, and the row asks whether the weapon came *down*.
    // Shortening alone was rejected for the opposite reason: a shorter bar in
    // the same direction reads as a different weapon rather than as the same
    // weapon lowered.
    internal const float LoweredCarryRotationRadians = 0.9f;
    internal const float LoweredWeaponLengthScale = 0.55f;

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
    // One unit clear of the helmet's own top edge (HelmetCenterYOffset minus
    // half of HelmetSize, at -22), so the pip never overlaps the helmet at
    // Medium+ tier or the bare head at Low tier (whose top edge, at -20, sits
    // even lower than the helmet's).
    internal const float HeadPipCenterYOffset = -24f;
    internal const float HelmetCenterYOffset = -19f;
    internal const float NightVisionMountCenterYOffset = -20f;
    internal const float SlingCenterYOffset = -12f;
    internal const float WeaponGripCenterYOffset = -11f;

    /// <summary>
    /// Builds the sixteen-layer <see cref="OperatorLayout"/> for one operator
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
    /// <param name="isFriendly">
    /// Gives friendly and hostile operators a shape a spectator can tell
    /// apart without reading color: the default, <see langword="true"/>,
    /// keeps every pre-existing pinned rectangle in this class unchanged — a
    /// square <see cref="OperatorLayout.GroundRingBounds"/> footprint (no
    /// rotation) plus the head-top <see cref="OperatorLayout.HeadPipBounds"/>
    /// pip. <see langword="false"/> rotates the same ground ring footprint
    /// <see cref="GroundRingHostileRotationRadians"/> into a diamond and
    /// omits the pip, so a hostile reads as diamond-without-pip against a
    /// friendly's square-with-pip at any zoom, including the few-pixel case
    /// where every other layer has collapsed to noise.
    /// </param>
    /// <param name="weaponClass">
    /// Gives a <see cref="WeaponClass.Pistol"/> operator a shorter, thicker
    /// weapon body than the default <see cref="WeaponClass.Rifle"/>, and
    /// drops the foregrip and sling layers a handgun does not have. The
    /// rifle path is byte-identical to this parameter's introduction: every
    /// rectangle a caller already depends on for <see cref="WeaponClass.Rifle"/>
    /// is unchanged.
    /// </param>
    /// <param name="isWeaponLowered">
    /// The simulation's own <c>OperatorState.WeaponLowered</c>. When
    /// <see langword="true"/> every weapon-mounted layer is built from a
    /// direction turned <see cref="LoweredCarryRotationRadians"/> away from
    /// the aim line, the weapon body is scaled by
    /// <see cref="LoweredWeaponLengthScale"/>, and the muzzle flash cannot
    /// draw — a lowered weapon does not fire, so a flash at its muzzle would
    /// render something that did not happen. The default,
    /// <see langword="false"/>, leaves every rectangle this class produces
    /// exactly as it was before this parameter existed.
    /// </param>
    internal static OperatorLayout Create(
        Vector2 rootPosition,
        float apparentScale,
        OperatorDetailTier detailTier,
        Bam16 weaponAimBam,
        float previousDisplayRotationRawUnits,
        float smoothingFactor,
        bool isFiring,
        bool isSelected,
        bool isFriendly = true,
        WeaponClass weaponClass = WeaponClass.Rifle,
        bool isWeaponLowered = false)
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

        var aimRotationRawUnits = BlendTowardTarget(
            previousDisplayRotationRawUnits,
            weaponAimBam,
            smoothingFactor);

        // A lowered weapon is drawn turned away from the aim line, so the
        // rotation every weapon-mounted layer below is built from is the aim
        // rotation plus the carry offset. DisplayRotationRawUnits carries the
        // same value, because it is what OperatorRenderer rotates the weapon
        // sprite by: if the two disagreed, the sprite and the block geometry
        // beneath it would point in different directions.
        var displayRotationRawUnits = isWeaponLowered
            ? aimRotationRawUnits + RadiansToRawUnits(LoweredCarryRotationRadians)
            : aimRotationRawUnits;

        var rotationRadians = RawUnitsToRadians(displayRotationRawUnits);
        var weaponDirection = new Vector2(MathF.Cos(rotationRadians), MathF.Sin(rotationRadians));

        var isPistol = weaponClass == WeaponClass.Pistol;
        var baseWeaponLength = isPistol ? PistolWeaponLength : WeaponLength;
        var effectiveWeaponLength = isWeaponLowered
            ? baseWeaponLength * LoweredWeaponLengthScale
            : baseWeaponLength;
        var effectiveWeaponThickness = isPistol ? PistolWeaponThickness : WeaponThickness;

        var weaponGripAnchor = rootPosition +
            (new Vector2(0f, WeaponGripCenterYOffset) * apparentScale);
        var weaponMuzzleAnchor = weaponGripAnchor +
            (weaponDirection * (effectiveWeaponLength * apparentScale));

        var showGearLayer = detailTier is OperatorDetailTier.Medium or OperatorDetailTier.High;
        var showOpticsLayer = detailTier == OperatorDetailTier.High;

        var groundRingBounds = CenteredRect(
            rootPosition + (new Vector2(0f, GroundRingCenterYOffset) * apparentScale),
            GroundRingSize * apparentScale,
            GroundRingSize * apparentScale);
        var groundRingRotationRadians = isFriendly ? 0f : GroundRingHostileRotationRadians;

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
            effectiveWeaponLength * apparentScale,
            effectiveWeaponThickness * apparentScale);

        // A handgun has neither a foregrip nor a sling, regardless of tier.
        var weaponForegripBounds = showGearLayer && !isPistol
            ? CenteredRect(
                weaponGripAnchor + (weaponDirection * (ForegripDistanceFromGrip * apparentScale)),
                WeaponForegripWidth * apparentScale,
                WeaponForegripHeight * apparentScale)
            : Rectangle.Empty;

        var headBounds = CenteredRect(
            rootPosition + (new Vector2(0f, HeadCenterYOffset) * apparentScale),
            HeadSize * apparentScale,
            HeadSize * apparentScale);

        var headPipBounds = isFriendly
            ? CenteredRect(
                rootPosition + (new Vector2(0f, HeadPipCenterYOffset) * apparentScale),
                HeadPipSize * apparentScale,
                HeadPipSize * apparentScale)
            : Rectangle.Empty;

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

        var muzzleFlashBounds = isFiring && !isWeaponLowered
            ? CenteredRect(weaponMuzzleAnchor, MuzzleFlashSize * apparentScale, MuzzleFlashSize * apparentScale)
            : Rectangle.Empty;

        // A handgun has no sling, regardless of tier.
        var slingBounds = showGearLayer && !isPistol
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
            groundRingRotationRadians,
            bootsBounds,
            legsBounds,
            torsoBounds,
            plateCarrierBounds,
            armsBounds,
            weaponBodyBounds,
            weaponForegripBounds,
            headBounds,
            headPipBounds,
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
    /// The inverse of <see cref="RawUnitsToRadians"/>, for the one angle in
    /// this file authored in radians rather than in
    /// <see cref="Bam16"/> raw units: <see cref="LoweredCarryRotationRadians"/>.
    /// </summary>
    private static float RadiansToRawUnits(float radians) =>
        radians / MathF.Tau * Bam16.UnitsPerTurn;

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
