using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

internal enum PawnDetailTier
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The arc a swinging weapon has just travelled, expressed as a pivot, a
/// radius, and the two angles it spans, so that a renderer walks the arc
/// without knowing how it was derived.
/// </summary>
/// <remarks>
/// It is computed once from the pose, with no position history, and it lives
/// on <see cref="PawnLayout"/> rather than in the renderer. The
/// plains-backdrop finding recorded in <c>docs/development/testing.md</c> is
/// what fixes this shape: a duplicated ground-cell formula left the shipped
/// render loop uncovered while the tests constrained a method with no
/// production caller.
/// </remarks>
/// <param name="Pivot">The grip the arc turns about.</param>
/// <param name="Radius">Distance from the grip to the weapon tip.</param>
/// <param name="StartAngleRadians">The trailing end of the arc.</param>
/// <param name="EndAngleRadians">The current weapon tip.</param>
/// <param name="Strength">
/// Trail opacity, zero when no trail is drawn at all.
/// </param>
/// <param name="Thickness">Stroke thickness in pixels.</param>
internal readonly record struct SwingTrail(
    Vector2 Pivot,
    float Radius,
    float StartAngleRadians,
    float EndAngleRadians,
    float Strength,
    float Thickness)
{
    public bool IsEmpty => Strength <= 0f || Radius <= 0f;
}

/// <summary>
/// The pure geometry a renderer draws from. <see cref="WeaponGripAnchor"/>
/// and <see cref="ShieldAnchor"/> are the named attachment points the
/// composed-layer design (integration design section 5) reserves for the
/// armor, sash, and adornment layers to attach to, instead of re-deriving
/// offsets from <see cref="WeaponStart"/> or <see cref="ShieldBounds"/>
/// directly. Both are pure layout outputs and carry no drawable content of
/// their own. <see cref="PlaceholderBounds"/> is the step-4 diagnostic
/// placeholder's layout rectangle (visual-system-integration-design.md
/// section 4, R-W6.4): a pure output computed unconditionally, the same way
/// <see cref="ShieldAnchor"/> is, so the renderer never derives its own
/// formula for where the placeholder goes. <see cref="ShieldPostureRotationRadians"/>
/// (VIS-015, S12) is the fixed active-posture rotation the renderer applies
/// about <see cref="ShieldBounds"/>'s own center — always the same
/// <see cref="ShieldPostureRotationRadians"/> constant, carried on the
/// layout rather than read from the geometry class directly, so
/// <c>PawnRenderer</c> never derives the value itself, exactly as it never
/// derives <see cref="ShieldAnchor"/>. <see cref="ArmorBounds"/>,
/// <see cref="SashBounds"/>, <see cref="AdornmentAccentPrimaryBounds"/>, and
/// <see cref="AdornmentAccentSecondaryBounds"/> (VIS-023) are layers 4, 5,
/// and 9 of the composed-pawn order (integration design section 5) —
/// <see cref="Rectangle.Empty"/> whenever the corresponding
/// <c>PawnGeometry.Create</c> input says the layer contributes nothing
/// (unarmored, no sash, no accents) or the current
/// <see cref="PawnDetailTier"/> gates the layer off, exactly the same
/// "pure output, empty when absent" convention <see cref="ShieldBounds"/>
/// already uses for an unshielded warrior.
/// </summary>
internal readonly record struct PawnLayout(
    Vector2 FootAnchor,
    float ApparentScale,
    PawnDetailTier DetailTier,
    Rectangle GroundRingBounds,
    Rectangle TorsoBounds,
    Rectangle HeadBounds,
    Rectangle HeadTreatmentBounds,
    Vector2 WeaponStart,
    Vector2 WeaponEnd,
    float WeaponThickness,
    Rectangle WeaponBounds,
    Vector2 WeaponGripAnchor,
    Rectangle SecondaryEquipmentBounds,
    Rectangle ShieldBounds,
    Vector2 ShieldAnchor,
    float ShieldPostureRotationRadians,
    Rectangle ArmorBounds,
    Rectangle SashBounds,
    Rectangle AdornmentAccentPrimaryBounds,
    Rectangle AdornmentAccentSecondaryBounds,
    Rectangle PlaceholderBounds,
    Rectangle SelectionBounds,
    Rectangle VisualBounds,
    SwingTrail SwingTrail);

internal static class PawnGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;
    private const float MediumDetailScale = 0.95f;
    private const float HighDetailScale = 1.80f;

    /// <summary>
    /// PROVISIONAL. Share of the neutral reach added to the weapon line at an
    /// extension ratio of one, which is where a blow makes contact.
    /// </summary>
    private const float ExtensionReach = 0.35f;

    /// <summary>
    /// PROVISIONAL. Angular span of the arc trail at full trail strength.
    /// </summary>
    private const float TrailSweepRadians = 0.85f;

    /// <summary>PROVISIONAL. Trail stroke thickness in pawn units.</summary>
    private const float TrailThickness = 1.2f;

    // ============= Shield proportion envelope (VIS-014, OD-10) =============
    //
    // shield-visuals-design.md, OD-10 (resolved 2026-07-28, option (a)):
    // R-W2.1 is amended with a fourth authorized skin-difference channel —
    // bounded per-skin proportion deltas of a few layout pixels inside one
    // shared tall-shield aspect-ratio band, with the rendered footprint
    // never falling below the current Low-tier block. These constants are
    // that band and those deltas, named so ShieldVisualCatalogTests/
    // PawnGeometryTests can pin them rather than re-deriving magic numbers.

    /// <summary>
    /// PROVISIONAL. The shared tall-body-shield aspect-ratio band (width
    /// divided by height) the four proportion deltas below are sized to keep
    /// every <c>PawnShieldRole.TallHardwood</c> skin's drawn rectangle
    /// inside, at every apparent scale and every detail tier
    /// (shield-visuals-design.md, OD-10). Bounds the S2 "tall end" and S5
    /// "narrowest" deltas so no skin ever reads as a different equipment
    /// class (a round or breast-high shield sits far outside this band) —
    /// internal rather than private so <c>PawnGeometryTests</c>'s
    /// classification test pins the same band the deltas below are tuned
    /// against, instead of a duplicated magic number.
    /// </summary>
    internal const float ShieldAspectRatioMinimum = 0.12f;

    /// <summary>See <see cref="ShieldAspectRatioMinimum"/>.</summary>
    internal const float ShieldAspectRatioMaximum = 0.50f;

    /// <summary>
    /// The floor every skin's drawn width passes through, unchanged from the
    /// block's own original floor (R-W2.2): no skin may ever draw a
    /// narrower-than-legible shield, regardless of its proportion delta.
    /// </summary>
    private const int ShieldMinimumWidth = 2;

    /// <summary>Height floor at <see cref="PawnDetailTier.Low"/>, unchanged.</summary>
    private const int ShieldLowTierMinimumHeight = 3;

    /// <summary>Height floor at Medium/High tier, unchanged.</summary>
    private const int ShieldMediumOrHighMinimumHeight = 5;

    /// <summary>
    /// PROVISIONAL. S2 <c>morgaFullBody</c>'s "tall end of the shared
    /// envelope" delta (shield-visuals-design.md skin table): a few layout
    /// pixels of extra height on top of the base S1 proportions, width
    /// unchanged, so the block reads slightly taller for its width than the
    /// baseline block. Applied before the height floor, so it can never push
    /// a skin below <see cref="ShieldLowTierMinimumHeight"/> or
    /// <see cref="ShieldMediumOrHighMinimumHeight"/> either.
    /// </summary>
    private const int ShieldTallEndHeightDeltaPixels = 2;

    /// <summary>
    /// PROVISIONAL. S5 <c>visayanKalasag</c>'s "narrowest proportion within
    /// the shared envelope" width delta (shield-visuals-design.md skin
    /// table): a few layout pixels narrower than the base S1 width. Applied
    /// before <see cref="ShieldMinimumWidth"/>'s floor, so this skin can
    /// never draw narrower than every other skin's own hard floor.
    /// </summary>
    private const int ShieldNarrowestWidthDeltaPixels = 1;

    /// <summary>
    /// PROVISIONAL. S5 <c>visayanKalasag</c>'s companion height delta: taller
    /// than S2's own tall-end delta, so the narrower width reads as a
    /// slenderer proportion rather than a smaller shield (R-X.12 — a
    /// "narrowest" skin must never read as less mechanical coverage than any
    /// other skin on the same loadout).
    /// </summary>
    private const int ShieldNarrowestHeightDeltaPixels = 2;

    // ============= Active shield posture (VIS-015, S12) =============
    //
    // shield-visuals-design.md, "Active posture (S12)": the shield is drawn
    // slightly angled forward of the pawn instead of as a passive side slab
    // (R-W2.5) — S12 is Provisional reconstruction (Hinilawod epic; Cole
    // 1922's tilting-grip description used as stance inspiration only,
    // R-X.9), never presented as a historical measurement. A fixed layout
    // offset and a small fixed rotation, identical for every skin and
    // constant over time: the shield is deliberately not posed (see
    // CreateShield's own remarks below), so this reads no combat state, is
    // not animated, and adds no pose channel.

    /// <summary>
    /// PROVISIONAL. The angled posture's fixed forward offset, in layout
    /// units at unit scale — multiplied by <c>apparentScale</c> the same way
    /// every other layout offset in this class is — applied to the shield
    /// block's drawn position, toward the torso rather than away from it. A
    /// few layout pixels, not a stance channel: the offset lands directly in
    /// <see cref="ShieldLayout.Bounds"/>'s position, so it is already
    /// accounted for wherever that rectangle is used, including
    /// <see cref="PawnLayout.VisualBounds"/>, with no per-frame
    /// recomputation. <see cref="CreateShield"/> zeroes it at
    /// <see cref="PawnDetailTier.Low"/>, where it stays the pre-VIS-015
    /// rectangle exactly, so the design's Low-tier non-occlusion guarantee
    /// against the ground ring, the weapon line, and the head holds without
    /// depending on rounding at the smallest apparent scales.
    /// </summary>
    internal const float ShieldPostureOffsetUnits = 1f;

    /// <summary>
    /// PROVISIONAL. The angled posture's small fixed rotation, in radians,
    /// applied by <c>PawnRenderer.DrawShield</c> about
    /// <see cref="ShieldLayout.Bounds"/>'s own center — carried on
    /// <see cref="PawnLayout.ShieldPostureRotationRadians"/> rather than read
    /// from this constant directly by the renderer. A drawing choice
    /// justified by the S12 posture evidence, never presented as a
    /// historical measurement.
    /// </summary>
    internal const float ShieldPostureRotationRadians = 0.15f;

    /// <param name="swingPose">
    /// The pose one in-flight swing puts this pawn in, or <c>null</c> for a
    /// pawn standing still. A neutral pose produces the same layout as no pose
    /// at all, so a caller may pass either.
    /// </param>
    /// <param name="armorWidthFactor">
    /// The torso-capsule width multiplier a worn armor option (research
    /// category F) contributes, bounded to
    /// <c>[1f, <see cref="AppearanceComponentCatalog.MaxArmorWidthFactor"/>]</c>
    /// (R-W3.6, "Armor capsule widening is bounded inside the existing
    /// build-multiplier envelope"). <c>1f</c> — matching
    /// <c>AppearanceComponentCatalog.ArmorF1Unarmored</c>'s own value — is
    /// the default and layer 4's no-op state: no separate armor rectangle
    /// draws at all (see <see cref="CreateArmor"/>). Time-invariant: an
    /// appearance input, never a pose one, exactly as
    /// <paramref name="appearance"/> itself is.
    /// </param>
    /// <param name="hasSash">
    /// Whether the resolved preset selects a sash/belt-line component
    /// (research category G). <see langword="false"/> (the default) is
    /// layer 5's no-op state: <see cref="PawnLayout.SashBounds"/> is
    /// <see cref="Rectangle.Empty"/>.
    /// </param>
    /// <param name="adornmentAccentMarkCount">
    /// How many adornment accent marks (research category I, plus C3 and
    /// E2) the resolved preset selects, bounded to
    /// <c>[0, <see cref="AppearanceComponentCatalog.MaxAccentMarksPerPawn"/>]</c>
    /// (R-W3.6, "Area cap"). <c>0</c> (the default) is layer 9's no-op
    /// state.
    /// </param>
    public static PawnLayout Create(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f,
        SwingPose? swingPose = null,
        float armorWidthFactor = 1f,
        bool hasSash = false,
        int adornmentAccentMarkCount = 0)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        if (!float.IsFinite(scaleMultiplier) || scaleMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));
        }

        if (!float.IsFinite(armorWidthFactor) ||
            armorWidthFactor < 1f ||
            armorWidthFactor > AppearanceComponentCatalog.MaxArmorWidthFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(armorWidthFactor));
        }

        if (adornmentAccentMarkCount < 0 ||
            adornmentAccentMarkCount > AppearanceComponentCatalog.MaxAccentMarksPerPawn)
        {
            throw new ArgumentOutOfRangeException(nameof(adornmentAccentMarkCount));
        }

        var apparentScale = Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale) * scaleMultiplier;
        var detailTier = apparentScale switch
        {
            < MediumDetailScale => PawnDetailTier.Low,
            < HighDetailScale => PawnDetailTier.Medium,
            _ => PawnDetailTier.High,
        };

        var ringWidth = ToSize(13f * apparentScale);
        var ringHeight = ToSize(4f * apparentScale);
        var groundRingBounds = CenteredRectangle(
            footAnchor.X,
            footAnchor.Y - (ringHeight / 2f),
            ringWidth,
            ringHeight);

        // The feet stay planted, so the ground ring keeps the foot anchor
        // while everything the warrior can lean moves with the torso.
        var pose = swingPose ?? default;
        var bodyAnchor = footAnchor + new Vector2(
            pose.TorsoLeanX * apparentScale,
            pose.TorsoLeanY * apparentScale);

        var torsoHeight = ToSize(
            12f * appearance.StatureMultiplier * apparentScale);
        var torsoWidth = ToSize(
            7f * appearance.BuildMultiplier * apparentScale);
        var torsoBottom = (int)MathF.Round(
            bodyAnchor.Y - MathF.Max(1f, apparentScale));
        var torsoBounds = new Rectangle(
            (int)MathF.Round(bodyAnchor.X - (torsoWidth / 2f)),
            torsoBottom - torsoHeight,
            torsoWidth,
            torsoHeight);

        var headSize = ToSize(7f * apparentScale);
        var headGap = ToSize(apparentScale);
        var headBounds = new Rectangle(
            (int)MathF.Round(bodyAnchor.X - (headSize / 2f)),
            torsoBounds.Top - headGap - headSize,
            headSize,
            headSize);
        var headTreatmentHeight = Math.Max(1, ToSize(2.6f * apparentScale));
        var headTreatmentBounds = new Rectangle(
            headBounds.Left,
            headBounds.Top,
            headBounds.Width,
            headTreatmentHeight);

        // The step-4 diagnostic placeholder (visual-system-integration-
        // design.md section 4, R-W6.4) occupies the torso's footprint — the
        // one element every pawn always has — so that whichever domain's
        // resolution eventually reaches VisualFallbackStep.DiagnosticPlaceholder
        // has an unconditional, pure, already-bounded rectangle to draw
        // instead of nothing. Inscribed within TorsoBounds (never larger),
        // so it never grows VisualBounds beyond what the torso already
        // contributes and never disturbs PawnRenderer.GetBounds.
        var placeholderSide = Math.Min(torsoBounds.Width, torsoBounds.Height);
        var placeholderBounds = CenteredRectangle(
            torsoBounds.Center.X,
            torsoBounds.Center.Y,
            placeholderSide,
            placeholderSide);

        var weapon = CreateWeaponLayout(
            bodyAnchor,
            apparentScale,
            appearance.WeaponRole,
            detailTier,
            pose);

        // The shield is deliberately not posed. A swing moves the weapon arm;
        // the off-hand block stays where the torso puts it, so a spectator can
        // still tell a shielded warrior from a solo one at the moment of
        // impact, which is exactly when the weapon line is least readable.
        var shield = CreateShield(
            footAnchor,
            apparentScale,
            appearance.ShieldRole,
            appearance.ShieldSkinId,
            torsoBounds,
            detailTier);

        // VIS-023, layers 4/5/9 (integration design section 5). None of
        // these read swingPose — they are pure functions of the torso/head
        // anchors, apparentScale, and detailTier, exactly like the
        // composed-layer design's "time-invariant" rule requires.
        var armorBounds = CreateArmor(torsoBounds, detailTier, armorWidthFactor);
        var sashBounds = CreateSash(torsoBounds, apparentScale, detailTier, hasSash);
        var (adornmentPrimaryBounds, adornmentSecondaryBounds) = CreateAdornmentAccents(
            headBounds,
            torsoBounds,
            apparentScale,
            detailTier,
            adornmentAccentMarkCount);

        var renderedBounds = Rectangle.Union(groundRingBounds, torsoBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headTreatmentBounds);
        renderedBounds = Rectangle.Union(renderedBounds, weapon.Bounds);

        if (!weapon.SecondaryBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(
                renderedBounds,
                weapon.SecondaryBounds);
        }

        if (!shield.Bounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, shield.Bounds);
        }

        // ArmorBounds is the only new layer that can extend past the torso
        // it widens (sash and accents are inscribed inside the torso/head
        // footprint by construction — see each helper's own remarks), so it
        // is the only one that needs folding into the pose-blind bound.
        if (!armorBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, armorBounds);
        }

        var selectionPadding = Math.Max(
            3,
            (int)MathF.Ceiling(3f * apparentScale));
        var selectionBounds = Inflate(renderedBounds, selectionPadding);
        var visualBounds = Rectangle.Union(renderedBounds, selectionBounds);

        return new PawnLayout(
            footAnchor,
            apparentScale,
            detailTier,
            groundRingBounds,
            torsoBounds,
            headBounds,
            headTreatmentBounds,
            weapon.Start,
            weapon.End,
            weapon.Thickness,
            weapon.Bounds,
            weapon.Start,
            weapon.SecondaryBounds,
            shield.Bounds,
            shield.Anchor,
            ShieldPostureRotationRadians,
            armorBounds,
            sashBounds,
            adornmentPrimaryBounds,
            adornmentSecondaryBounds,
            placeholderBounds,
            selectionBounds,
            visualBounds,
            CreateSwingTrail(weapon, apparentScale, detailTier, pose));
    }

    /// <summary>
    /// The arc the weapon tip has just swept, derived from the pose alone. It
    /// is omitted entirely at the low detail tier, where a pawn is a handful
    /// of pixels tall and the arc would be noise.
    /// </summary>
    private static SwingTrail CreateSwingTrail(
        WeaponLayout weapon,
        float scale,
        PawnDetailTier detailTier,
        SwingPose pose)
    {
        if (detailTier == PawnDetailTier.Low || pose.TrailStrength <= 0f)
        {
            return default;
        }

        var reach = weapon.End - weapon.Start;
        var radius = reach.Length();
        if (radius <= 0f)
        {
            return default;
        }

        // The arc trails behind the direction of travel, and the sign of the
        // weapon rotation is what says which way that is. There is no position
        // history to consult and none is kept.
        var facing = pose.WeaponAngleRadians >= 0f ? 1f : -1f;
        var endAngle = MathF.Atan2(reach.Y, reach.X);
        var sweep = TrailSweepRadians * pose.TrailStrength * facing;

        return new SwingTrail(
            weapon.Start,
            radius,
            endAngle - sweep,
            endAngle,
            pose.TrailStrength,
            MathF.Max(1f, TrailThickness * scale));
    }

    /// <summary>
    /// The block a shield bearer draws beside the torso, on the side opposite
    /// the weapon, together with the point future shield-skin work anchors
    /// to. <see cref="ShieldLayout.Bounds"/> is <see cref="Rectangle.Empty"/>
    /// for a warrior carrying no shield; <see cref="ShieldLayout.Anchor"/> is
    /// computed unconditionally, because it is a pure layout output the
    /// composed-layer design (integration design section 5) reserves as an
    /// attachment point regardless of loadout.
    /// </summary>
    /// <param name="shieldSkinId">
    /// The resolved skin's stable catalog identifier
    /// (<c>Presentation.Catalogs.ShieldVisualCatalog</c>, VIS-014, OD-10):
    /// selects the skin's proportion delta within the shared aspect-ratio
    /// band. Every identifier other than <see cref="ShieldVisualCatalog.MorgaFullBody"/>'s
    /// and <see cref="ShieldVisualCatalog.VisayanKalasag"/>'s own draws the
    /// base S1 proportions unchanged, so an unrecognized or fallback
    /// identifier degrades to the plain block rather than throwing.
    /// </param>
    /// <remarks>
    /// This is the surface that makes grip discoverable on the battlefield
    /// rather than only in the inspector: preset V2 is the first to field a
    /// solo and a shielded warrior of the same weapon at once, and they deal
    /// different damage, so without it the two are indistinguishable.
    /// <para>
    /// Drawn at every detail tier including <see cref="PawnDetailTier.Low"/>,
    /// unlike secondary equipment. A shield changes what the warrior is, not
    /// how ornamented they are, and dropping it at distance would remove the
    /// distinction exactly when a spectator is watching whole formations
    /// rather than individuals. It is a solid block, which survives being a
    /// few pixels tall in a way a drawn line does not.
    /// </para>
    /// </remarks>
    private static ShieldLayout CreateShield(
        Vector2 footAnchor,
        float scale,
        PawnShieldRole role,
        string shieldSkinId,
        Rectangle torsoBounds,
        PawnDetailTier detailTier)
    {
        var (widthDelta, heightDelta) = ShieldProportionDelta(shieldSkinId);

        // Tall enough to read as covering chest and abdomen, which is what
        // the targeting multiplier actually does. The per-skin delta is
        // added before the floor, so no skin — including the narrowest one —
        // can ever draw below the same floor every skin already shared
        // (R-W2.2, OD-10).
        var width = Math.Max(
            ShieldMinimumWidth,
            ToSize(4f * scale) + widthDelta);
        var height = Math.Max(
            detailTier == PawnDetailTier.Low
                ? ShieldLowTierMinimumHeight
                : ShieldMediumOrHighMinimumHeight,
            ToSize(11f * scale) + heightDelta);

        // S12 active posture (VIS-015): a fixed, PROVISIONAL forward offset
        // that brings the block a few layout pixels toward the torso rather
        // than leaving it a passive side slab; the matching small rotation
        // is applied by PawnRenderer.DrawShield about this rectangle's own
        // center, so it never touches this positioning arithmetic. Both are
        // static — this method never reads a pose — so they land the same
        // way on every call, for every skin. Zeroed at Low tier, the same
        // way the seam/accent detail (PawnRenderer.DrawShield) and the
        // curvature inset (ShieldCurvatureInsetPixels) degrade away at that
        // tier: it keeps this rectangle bit-for-bit the pre-VIS-015 block at
        // Low tier, which is exactly where the design's non-occlusion
        // guarantee against the ground ring, the weapon line, and the head
        // is required (shield-visuals-design.md, "Active posture (S12)").
        // Rounded without ToSize's usual whole-pixel floor, deliberately: the
        // documented rollback ("set the offset and rotation constants to
        // zero") must actually zero this term at every tier, and ToSize's
        // Math.Max(1, ...) floor would otherwise keep drawing a 1-pixel
        // offset even with ShieldPostureOffsetUnits set to zero.
        var postureOffset = detailTier == PawnDetailTier.Low
            ? 0
            : (int)MathF.Round(ShieldPostureOffsetUnits * scale);
        var left = (int)MathF.Round(footAnchor.X - (7f * scale)) - width + postureOffset;
        var top = torsoBounds.Top + ToSize(scale);
        var rectangle = new Rectangle(left, top, width, height);
        var anchor = new Vector2(rectangle.Center.X, rectangle.Center.Y);

        return new ShieldLayout(
            role == PawnShieldRole.None ? Rectangle.Empty : rectangle,
            anchor);
    }

    /// <summary>
    /// The shared aspect-ratio band's per-skin width/height delta, in whole
    /// layout pixels (shield-visuals-design.md, OD-10). Every skin other
    /// than the two named here draws the base S1 proportions — S1
    /// <c>mactanThin</c> and its curvature-only sibling S3
    /// <c>boxerCagayan</c> included, since curvature is a draw-time outline
    /// treatment (<c>PawnRenderer.DrawShield</c>) and never a proportion
    /// change.
    /// </summary>
    private static (int WidthDelta, int HeightDelta) ShieldProportionDelta(
        string shieldSkinId)
    {
        if (shieldSkinId == ShieldVisualCatalog.MorgaFullBody.Catalog.Id)
        {
            return (0, ShieldTallEndHeightDeltaPixels);
        }

        if (shieldSkinId == ShieldVisualCatalog.VisayanKalasag.Catalog.Id)
        {
            return (-ShieldNarrowestWidthDeltaPixels, ShieldNarrowestHeightDeltaPixels);
        }

        return (0, 0);
    }

    // ============= VIS-023: armor, sash, and adornment layers =============
    //
    // warrior-appearance-design.md, "Readability priority preservation" rule
    // 4 and its zoom table: layer 4 (armor) contributes tone from Low tier
    // up but a separate widened silhouette only from Medium tier up; layer 5
    // (sash) is Medium tier and up; layer 9 (adornment accents) is High tier
    // only. All three are pure functions of the torso/head anchors,
    // apparentScale, and detailTier already computed above — none reads
    // swingPose, matching integration design section 6 rule 1
    // ("composed appearance layers are time-invariant").

    /// <summary>
    /// Layer 4 (integration design section 5): the widened torso capsule a
    /// worn armor option (research category F) draws, bounded to
    /// <see cref="AppearanceComponentCatalog.MaxArmorWidthFactor"/> (R-W3.6).
    /// <see cref="Rectangle.Empty"/> for an unarmored pawn
    /// (<paramref name="armorWidthFactor"/> at its own floor of <c>1f</c>,
    /// matching <c>AppearanceComponentCatalog.ArmorF1Unarmored</c>'s own
    /// value — "renders no additional silhouette") and at
    /// <see cref="PawnDetailTier.Low"/>, where the design confines armor to
    /// a tone contribution folded into the torso fill
    /// (<c>PawnRenderer.Draw</c>) rather than a separate rectangle
    /// ("armor tone Low+, silhouette Medium+"). Centered on
    /// <paramref name="torsoBounds"/>'s own center and sharing its vertical
    /// extent exactly, so widening only ever grows the capsule outward,
    /// never up or down — the shield's <c>top</c> calculation
    /// (<c>CreateShield</c>) reads <c>torsoBounds.Top</c>, which this never
    /// changes.
    /// </summary>
    private static Rectangle CreateArmor(
        Rectangle torsoBounds,
        PawnDetailTier detailTier,
        float armorWidthFactor)
    {
        if (armorWidthFactor <= 1f || detailTier == PawnDetailTier.Low)
        {
            return Rectangle.Empty;
        }

        var widenedWidth = Math.Max(
            torsoBounds.Width,
            (int)MathF.Round(torsoBounds.Width * armorWidthFactor));

        return CenteredRectangle(
            torsoBounds.Center.X,
            torsoBounds.Center.Y,
            widenedWidth,
            torsoBounds.Height);
    }

    /// <summary>
    /// Layer 5 (integration design section 5): the sash/belt line (research
    /// category G), Medium tier and up (warrior-appearance-design.md zoom
    /// table). <see cref="Rectangle.Empty"/> when <paramref name="hasSash"/>
    /// is <see langword="false"/> or at <see cref="PawnDetailTier.Low"/>.
    /// Strictly inside <paramref name="torsoBounds"/> — inset one pixel on
    /// every side — so it can never occlude anything the torso capsule
    /// itself does not already reach (R-W3.6 rule 1, "render only within
    /// the torso capsule ... footprint").
    /// </summary>
    private static Rectangle CreateSash(
        Rectangle torsoBounds,
        float apparentScale,
        PawnDetailTier detailTier,
        bool hasSash)
    {
        if (!hasSash || detailTier == PawnDetailTier.Low)
        {
            return Rectangle.Empty;
        }

        var sashHeight = Math.Max(
            1,
            Math.Min(torsoBounds.Height, (int)MathF.Round(apparentScale)));
        var sashWidth = Math.Max(1, torsoBounds.Width - 2);
        var sashTop = Math.Min(
            torsoBounds.Top + Math.Max(1, torsoBounds.Height / 2),
            Math.Max(torsoBounds.Top, torsoBounds.Bottom - sashHeight));

        return new Rectangle(torsoBounds.Left + 1, sashTop, sashWidth, sashHeight);
    }

    /// <summary>
    /// Layer 9 (integration design section 5): up to
    /// <see cref="AppearanceComponentCatalog.MaxAccentMarksPerPawn"/>
    /// adornment accent marks (research category I, plus C3 and E2), High
    /// tier only (warrior-appearance-design.md zoom table). Both rectangles
    /// are <see cref="Rectangle.Empty"/> below <see cref="PawnDetailTier.High"/>
    /// or when <paramref name="accentMarkCount"/> is zero; the second is
    /// additionally empty whenever <paramref name="accentMarkCount"/> is
    /// exactly one. Each side is at most
    /// <see cref="AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1"/>
    /// pixels regardless of apparent scale — the design's "at most 2 pixels
    /// each at apparent scale 1" area cap (R-W3.6) read as a hard ceiling
    /// rather than a value that grows past it at higher zoom. The primary
    /// mark (I4, gold earrings) is inscribed inside
    /// <paramref name="headBounds"/>; the secondary (I5 gold necklace / C3
    /// gold-edged putong) sits at the top of <paramref name="torsoBounds"/>
    /// — both zero-overhang placements, comfortably inside the design's "at
    /// most one pixel of accent overhang" allowance.
    /// </summary>
    private static (Rectangle Primary, Rectangle Secondary) CreateAdornmentAccents(
        Rectangle headBounds,
        Rectangle torsoBounds,
        float apparentScale,
        PawnDetailTier detailTier,
        int accentMarkCount)
    {
        if (accentMarkCount <= 0 || detailTier != PawnDetailTier.High)
        {
            return (Rectangle.Empty, Rectangle.Empty);
        }

        var size = Math.Max(
            1,
            Math.Min(
                AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1,
                (int)MathF.Round(
                    AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1 *
                    apparentScale)));

        var primary = new Rectangle(
            headBounds.Right - size,
            headBounds.Top + Math.Max(0, (headBounds.Height - size) / 2),
            size,
            size);

        if (accentMarkCount < 2)
        {
            return (primary, Rectangle.Empty);
        }

        var secondary = new Rectangle(
            torsoBounds.Center.X - (size / 2),
            torsoBounds.Top,
            size,
            size);

        return (primary, secondary);
    }

    private static WeaponLayout CreateWeaponLayout(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role,
        PawnDetailTier detailTier,
        SwingPose pose)
    {
        // The Wasay is a haft, not a blade: a thinner shaft than the old
        // broad chopper carrying a distinct head at the far end, which
        // CreateSecondaryBounds supplies. The Kalis is narrow with a long
        // reach; the Itak reuses the short broad-dagger silhouette; the
        // Kampilan is unchanged.
        var start = role switch
        {
            PawnWeaponRole.Itak => Offset(footAnchor, 1f, -7f, scale),
            PawnWeaponRole.Kampilan => Offset(footAnchor, 1f, -6f, scale),
            PawnWeaponRole.Wasay => Offset(footAnchor, 1f, -5f, scale),
            PawnWeaponRole.Kalis =>
                Offset(footAnchor, 1f, -7f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var end = role switch
        {
            PawnWeaponRole.Itak => Offset(footAnchor, 9f, -15f, scale),
            PawnWeaponRole.Kampilan => Offset(footAnchor, 15f, -19f, scale),
            PawnWeaponRole.Wasay =>
                Offset(footAnchor, 12f, -18f, scale),
            PawnWeaponRole.Kalis =>
                Offset(footAnchor, 14f, -21f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var thickness = MathF.Max(
            1f,
            role switch
            {
                PawnWeaponRole.Itak => 2.2f * scale,
                PawnWeaponRole.Kampilan => 2.8f * scale,
                PawnWeaponRole.Wasay => 1.9f * scale,
                PawnWeaponRole.Kalis => 1.6f * scale,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    null),
            });
        var weaponPadding = role switch
        {
            PawnWeaponRole.Itak => 2.8f * scale,
            PawnWeaponRole.Kampilan => 4.2f * scale,
            PawnWeaponRole.Wasay => 4.4f * scale,
            PawnWeaponRole.Kalis => 3.2f * scale,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        end = ApplySwing(start, end, pose);
        var bounds = BoundsFromLine(start, end, weaponPadding);

        // The Wasay's head is what distinguishes an axe from a blade, so it
        // survives the low detail tier that drops the Itak's off-hand
        // secondary. Without it the axe silhouette is just a thin Kampilan.
        var secondaryBounds =
            detailTier == PawnDetailTier.Low && role != PawnWeaponRole.Wasay
                ? Rectangle.Empty
                : CreateSecondaryBounds(footAnchor, scale, role, end);

        return new WeaponLayout(
            start,
            end,
            thickness,
            bounds,
            secondaryBounds);
    }

    /// <summary>
    /// Rotates the weapon line about the grip and lengthens it along the
    /// reach. A neutral pose rotates by nothing and lengthens by nothing, so
    /// the line is bit-for-bit the static one.
    /// </summary>
    /// <remarks>
    /// The rotation is applied to the drawn line only; the pawn silhouette is
    /// not mirrored for a warrior striking to its left, so a leftward swing
    /// reads as an overhead sweep rather than as a blade ending on the target.
    /// Mirroring the silhouette needs a facing this pose does not carry, and
    /// is outside what this task was asked to change.
    /// </remarks>
    private static Vector2 ApplySwing(Vector2 start, Vector2 end, SwingPose pose)
    {
        var reach = end - start;
        var cosine = MathF.Cos(pose.WeaponAngleRadians);
        var sine = MathF.Sin(pose.WeaponAngleRadians);
        var rotated = new Vector2(
            (reach.X * cosine) - (reach.Y * sine),
            (reach.X * sine) + (reach.Y * cosine));
        var extension = MathF.Max(
            0f,
            1f + (pose.ExtensionRatio * ExtensionReach));

        return start + (rotated * extension);
    }

    private static Rectangle CreateSecondaryBounds(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role,
        Vector2 weaponEnd) =>
        role switch
        {
            PawnWeaponRole.Itak => BoundsFromLine(
                Offset(footAnchor, -2f, -4f, scale),
                Offset(footAnchor, -6f, -11f, scale),
                2f * scale),

            // The axe head, centred on the far end of the haft and set
            // across it so the mass reads as sitting behind the edge.
            PawnWeaponRole.Wasay => new Rectangle(
                (int)MathF.Round(weaponEnd.X - (2.6f * scale)),
                (int)MathF.Round(weaponEnd.Y - (2.6f * scale)),
                Math.Max(2, ToSize(5f * scale)),
                Math.Max(2, ToSize(5.2f * scale))),

            _ => Rectangle.Empty,
        };

    private static Vector2 Offset(
        Vector2 origin,
        float x,
        float y,
        float scale) =>
        origin + new Vector2(x * scale, y * scale);

    private static Rectangle CenteredRectangle(
        float centerX,
        float centerY,
        int width,
        int height) =>
        new(
            (int)MathF.Round(centerX - (width / 2f)),
            (int)MathF.Round(centerY - (height / 2f)),
            width,
            height);

    private static Rectangle BoundsFromLine(
        Vector2 start,
        Vector2 end,
        float padding)
    {
        var left = (int)MathF.Floor(MathF.Min(start.X, end.X) - padding);
        var top = (int)MathF.Floor(MathF.Min(start.Y, end.Y) - padding);
        var right = (int)MathF.Ceiling(MathF.Max(start.X, end.X) + padding);
        var bottom = (int)MathF.Ceiling(MathF.Max(start.Y, end.Y) + padding);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private static Rectangle Inflate(Rectangle bounds, int amount) =>
        new(
            bounds.X - amount,
            bounds.Y - amount,
            bounds.Width + (amount * 2),
            bounds.Height + (amount * 2));

    private static int ToSize(float value) =>
        Math.Max(1, (int)MathF.Round(value));

    private readonly record struct WeaponLayout(
        Vector2 Start,
        Vector2 End,
        float Thickness,
        Rectangle Bounds,
        Rectangle SecondaryBounds);

    /// <param name="Bounds">
    /// The drawn shield block, or <see cref="Rectangle.Empty"/> for an
    /// unshielded warrior.
    /// </param>
    /// <param name="Anchor">
    /// The shield's attachment point, computed the same way regardless of
    /// whether a shield is equipped.
    /// </param>
    private readonly record struct ShieldLayout(Rectangle Bounds, Vector2 Anchor);
}
