using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Core.Combat;
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
/// <param name="Emphasis">
/// How hard this contact reads, as a multiplier on the drawn opacity. One for
/// an ordinary landed blow; larger for a lethal one; smaller for a blow that
/// never met anything. A separate channel from <paramref name="Strength"/>
/// deliberately: strength is where the trail is in its own fade, emphasis is
/// what the blow was, and collapsing the two would make a fresh whiff read as
/// hard as a fresh kill.
/// </param>
internal readonly record struct SwingTrail(
    Vector2 Pivot,
    float Radius,
    float StartAngleRadians,
    float EndAngleRadians,
    float Strength,
    float Thickness,
    float Emphasis = 1f)
{
    public bool IsEmpty => Strength <= 0f || Radius <= 0f;
}

/// <summary>
/// The four arm segments an actively attacking warrior draws: two for the
/// hand that drives the weapon, and two for the hand that either supports the
/// weapon or carries the shield. Four rectangles is the design's ceiling of
/// four extra quads per active Medium or High pawn (attack-animation-v2
/// design section 11).
/// </summary>
/// <remarks>
/// <see langword="default"/> — four <see cref="Rectangle.Empty"/> rectangles —
/// is the neutral result, produced whenever no attack pose is active or the
/// pawn is at <see cref="PawnDetailTier.Low"/>, the same "empty when absent"
/// convention every other optional layer in this file already uses. The
/// support pair is empty on its own for a one-handed family carrying no
/// shield, which has no second arm to articulate.
/// </remarks>
/// <param name="From">The joint this segment leaves.</param>
/// <param name="To">The joint this segment reaches.</param>
/// <param name="Bounds">
/// The padded rectangle the stroke sits inside, which is what reaches the
/// bounding union. <see cref="Rectangle.Empty"/> when the segment is not
/// drawn at all, which is the one thing <see cref="IsEmpty"/> reports.
/// </param>
internal readonly record struct ArmSegment(
    Vector2 From,
    Vector2 To,
    Rectangle Bounds)
{
    public bool IsEmpty => Bounds.IsEmpty;
}

/// <param name="WeaponUpperArm">Shoulder to elbow on the weapon hand.</param>
/// <param name="WeaponForearm">Elbow to the weapon grip.</param>
/// <param name="SupportUpperArm">Shoulder to elbow on the off hand.</param>
/// <param name="SupportForearm">
/// Elbow to either the haft, for a two-handed family, or the shield.
/// </param>
/// <param name="Thickness">
/// The stroke width every segment is drawn at, in pixels. Carried here for
/// the same reason <see cref="PawnLayout.WeaponThickness"/> is carried on the
/// layout: it is the one arm value that is not a rectangle and therefore
/// cannot reach a bounding union.
/// </param>
internal readonly record struct ArmLayout(
    ArmSegment WeaponUpperArm,
    ArmSegment WeaponForearm,
    ArmSegment SupportUpperArm,
    ArmSegment SupportForearm,
    float Thickness)
{
    public bool IsEmpty =>
        WeaponUpperArm.IsEmpty &&
        WeaponForearm.IsEmpty &&
        SupportUpperArm.IsEmpty &&
        SupportForearm.IsEmpty;
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
/// already uses for an unshielded warrior. <see cref="LeftLegBounds"/>,
/// <see cref="RightLegBounds"/>, <see cref="LeftFootBounds"/>, and
/// <see cref="RightFootBounds"/> (movement-gait-animation design section 7)
/// are the leg and foot rectangles between the torso and the ground ring,
/// built from the pose-invariant leg proportions plus the gait pose the same
/// way the weapon line is built from the swing pose;
/// <see cref="Rectangle.Empty"/> at <see cref="PawnDetailTier.Low"/>, where
/// the ground ring already carries the footprint.
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
    SwingTrail SwingTrail,
    Rectangle LeftLegBounds,
    Rectangle RightLegBounds,
    Rectangle LeftFootBounds,
    Rectangle RightFootBounds,
    ArmLayout Arms);

/// <summary>
/// GPU-013. Both answers the arena render loop needs about one pawn, from
/// one call: the posed layout it draws from, and the pose-blind rectangle it
/// culls against.
/// </summary>
/// <remarks>
/// Before this existed, a visible pawn paid for two complete
/// <see cref="PawnGeometry.Create"/> constructions every frame — one through
/// <c>PawnRenderer.GetBounds</c>, which threw away everything except
/// <see cref="PawnLayout.VisualBounds"/>, and one for the layout itself
/// (redundancy R1 in
/// <c>docs/archives/2026-08-07/gpu-render/2026-07-28-gpu-render.md</c>).
/// <see cref="PawnGeometry.CreateWithPoseBlindBounds"/> keeps both results
/// but builds the second one from only the subset of the layout that
/// actually reaches the union: the ground ring, torso, head, head treatment,
/// weapon line, shield block, and armor capsule. The sash, the adornment
/// accents, the diagnostic placeholder, the swing trail, and the weapon
/// thickness are all skipped, because none of them can move the rectangle,
/// and the scale, detail tier, ground ring, and every whole-pixel size are
/// computed once and shared by both results.
/// </remarks>
/// <param name="Layout">
/// The posed layout, identical in every field to what
/// <see cref="PawnGeometry.Create"/> returns for the same arguments.
/// </param>
/// <param name="PoseBlindVisualBounds">
/// The neutral-pose visual bounds, identical to what
/// <c>PawnRenderer.GetBounds</c> returns for the same arguments. Pose-blind
/// on purpose: a pose-aware cull would make the drawn set a function of
/// presentation animation phase. See <c>PawnRenderer.GetBounds</c>.
/// </param>
internal readonly record struct PosedPawnGeometry(
    PawnLayout Layout,
    Rectangle PoseBlindVisualBounds);

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
    /// <summary>
    /// How much of the gait stride an attack at full stance weight removes.
    /// PROVISIONAL: enough that a strike reads as planted, short of the one
    /// that would snap a running warrior's legs together.
    /// </summary>
    private const float MaximumStancePlant = 0.6f;

    /// <summary>
    /// How far a legal shield overlay brings the block toward the exchange, in
    /// pawn units before apparent scale. PROVISIONAL presentation choreography.
    /// </summary>
    private const float ShieldGuardOffsetUnits = 1.4f;

    /// <summary>
    /// Where an arm leaves the torso, as a share of torso height below its
    /// top edge.
    /// </summary>
    private const float ShoulderHeightShare = 0.18f;

    /// <summary>
    /// Half the drawn arm thickness, in pawn units before apparent scale, and
    /// the whole-pixel floor below which an arm would stop being drawable at
    /// all.
    /// </summary>
    private const float ArmHalfWidthUnits = 0.8f;

    /// <summary>See <see cref="ArmHalfWidthUnits"/>.</summary>
    private const float ArmMinimumHalfWidthPixels = 0.6f;

    /// <summary>
    /// How far the elbow leaves the straight shoulder-to-hand line, in pawn
    /// units before apparent scale. PROVISIONAL: large enough to read as a
    /// bend at Medium detail, small enough that the arm stays inside the
    /// weapon's own reach envelope.
    /// </summary>
    private const float ElbowDisplacementUnits = 1.6f;

    /// <summary>
    /// Where a two-handed family's support hand grips the haft, as a share of
    /// the distance from the grip to the weapon tip.
    /// </summary>
    private const float SupportGripShareAlongHaft = 0.28f;

    /// <summary>
    /// How much harder a lethal contact's trail reads than the same outcome
    /// survived. PROVISIONAL presentation choreography.
    /// </summary>
    private const float LethalTrailEmphasis = 1.35f;

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

    // ============= Gait legs and feet (movement-gait-animation design
    // section 7, T8 correction) =============
    //
    // The legs are a real part of the body now, not a decoration squeezed
    // into whatever gap was left over. CreateTorso used to reserve a fixed
    // max(1, apparentScale)-unit gap between the torso's own bottom and the
    // body anchor regardless of how tall the legs were, which left no room
    // for a visible leg: at LegLengthUnits' old value of 1f, ToSize's
    // whole-pixel floor rounded the drawn leg body to zero height at almost
    // every apparent scale, so a moving warrior's stride was invisible. That
    // constraint is gone. CreateTorso now reserves exactly
    // <see cref="PawnProportions.TorsoBottomGap"/> — the full scaled leg
    // length at Medium and High tier, where legs draw — so the leg-plus-foot
    // band sits between the torso's bottom and the ground ring by
    // construction rather than by accident, and the neutral (unlifted) foot
    // lands exactly on the ground ring's bottom edge rather than short of it.
    // At <see cref="PawnDetailTier.Low"/>, where legs never draw, the gap is
    // still the original <c>max(1, apparentScale)</c> so a distant pawn's
    // silhouette is pixel-for-pixel what it always was.
    //
    // LegLengthUnits is sized so the leg-plus-foot band reads as roughly
    // 30-33% of the full head-top-to-foot-anchor silhouette at every apparent
    // scale from the Medium-tier floor through the clamp maximum: worked out
    // from TorsoHeightUnits (8), HeadSize's own 7-unit multiplier, and
    // HeadGap's own 1-unit multiplier as
    // LegLengthUnits / (7 + 1 + TorsoHeightUnits + LegLengthUnits). This is a
    // drawing choice, not a measurement — every constant in this section is
    // PROVISIONAL.
    //
    // PawnRenderer.GetBounds' pinned regression rectangle
    // (PawnGeometryTests.GetBounds_MatchesThePinnedRegressionRectangle) moves
    // as a direct, deliberate consequence of this restructure: the torso is
    // shorter and the legs now occupy real, visible space below it.

    /// <summary>
    /// Torso height per layout unit at unit scale, before
    /// <see cref="PawnAppearance.StatureMultiplier"/>. Reduced from its
    /// original <c>12f</c> so the leg-plus-foot band
    /// (<see cref="LegLengthUnits"/>) has real room to occupy without
    /// growing the whole silhouette — see this section's own remarks above
    /// for the worked ratio. A drawing choice, not a measurement.
    /// </summary>
    private const float TorsoHeightUnits = 8f;

    /// <summary>PROVISIONAL. Leg width per leg, in layout units at unit scale.</summary>
    private const float LegWidthUnits = 1.6f;

    /// <summary>
    /// PROVISIONAL. Full leg-plus-foot vertical span, in layout units at unit
    /// scale — the distance from the torso's bottom edge down to the ground
    /// ring's own bottom edge at neutral stance. Raised from its old value of
    /// <c>1f</c>, which rounded to a zero-height drawn leg body at almost
    /// every apparent scale (see this section's own remarks above); this
    /// value is sized instead for a legible ~30-33% leg-band share of the
    /// silhouette and a stride displacement of at least one whole pixel at
    /// the Medium-tier floor.
    /// </summary>
    private const float LegLengthUnits = 7.5f;

    /// <summary>
    /// PROVISIONAL. Horizontal separation between each leg's neutral-stance
    /// center and the body anchor, in layout units at unit scale.
    /// </summary>
    private const float LegGapUnits = 1.5f;

    /// <summary>PROVISIONAL. Foot width, in layout units at unit scale.</summary>
    private const float FootWidthUnits = 2.2f;

    /// <summary>
    /// PROVISIONAL. Foot height, in layout units at unit scale. Raised from
    /// its old value of <c>0.4f</c>, which rounded to <c>1</c> pixel by
    /// itself but left the leg body above it — <c>LegLength - FootHeight</c>
    /// — at zero height, since the two nearly-equal whole-pixel floors
    /// cancelled out. Sized well below <see cref="LegLengthUnits"/> so the
    /// leg body keeps a positive whole-pixel height of its own at every
    /// apparent scale where legs draw at all (Medium tier and up).
    /// </summary>
    private const float FootHeightUnits = 2f;

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
    /// <param name="gaitPose">
    /// The stride pose the warrior's legs and feet are drawn in, or
    /// <c>null</c> for a pawn standing still (movement-gait-animation design
    /// section 7). A neutral pose produces the same legs as no pose at all,
    /// so a caller may pass either, exactly as <paramref name="swingPose"/>
    /// already allows.
    /// </param>
    public static PawnLayout Create(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f,
        SwingPose? swingPose = null,
        float armorWidthFactor = 1f,
        bool hasSash = false,
        int adornmentAccentMarkCount = 0,
        GaitPose? gaitPose = null)
    {
        var proportions = CreateProportions(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier,
            armorWidthFactor,
            adornmentAccentMarkCount);

        return CreateLayout(
            proportions,
            footAnchor,
            appearance,
            swingPose ?? default,
            armorWidthFactor,
            hasSash,
            adornmentAccentMarkCount,
            gaitPose ?? default);
    }

    /// <summary>
    /// GPU-013. The posed layout and the pose-blind cull rectangle from one
    /// call, so a visible pawn stops building two complete layouts per frame.
    /// Every parameter means exactly what the same parameter on
    /// <see cref="Create"/> means, and the returned
    /// <see cref="PosedPawnGeometry.Layout"/> is what <see cref="Create"/>
    /// returns for those arguments.
    /// </summary>
    /// <remarks>
    /// <see cref="PosedPawnGeometry.PoseBlindVisualBounds"/> is not a second
    /// <see cref="Create"/> call with the pose dropped — that would defeat
    /// the point. It is built from the subset of the layout that can actually
    /// reach the bounding union: the ground ring, the torso, the head, the
    /// head treatment, the weapon line, the shield block, and the armor
    /// capsule. The sash and the adornment accents are inscribed inside the
    /// torso and head footprints by construction, the diagnostic placeholder
    /// is inscribed inside the torso, and neither the swing trail nor the
    /// weapon thickness is a rectangle at all, so all five are skipped. The
    /// argument checks, the apparent scale, the detail tier, the ground ring,
    /// and every whole-pixel size in <see cref="PawnProportions"/> are
    /// computed once and read by both results.
    /// </remarks>
    public static PosedPawnGeometry CreateWithPoseBlindBounds(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f,
        SwingPose? swingPose = null,
        float armorWidthFactor = 1f,
        bool hasSash = false,
        int adornmentAccentMarkCount = 0,
        GaitPose? gaitPose = null)
    {
        var prefix = PoseBlindPrefix.Create(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier,
            armorWidthFactor,
            hasSash,
            adornmentAccentMarkCount);

        return new PosedPawnGeometry(
            prefix.CompletePosedLayout(swingPose, gaitPose),
            prefix.PoseBlindVisualBounds);
    }

    /// <summary>
    /// GPU-014. The two-stage form of <see cref="CreateWithPoseBlindBounds"/>:
    /// everything about a pawn's geometry that no swing pose can change,
    /// carrying the pose-blind cull rectangle a caller tests and enough state
    /// to finish the posed layout for a pawn that survives that test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The combined call above assumes both of its results are wanted. A
    /// renderer's two geometry calls do not serve the same population: the
    /// cull rectangle is needed for every living agent, while the posed layout
    /// is needed only for the pawns the cull keeps. Handing every agent the
    /// combined call would make a culled agent pay for a posed layout it never
    /// draws, which at a thousand units and maximum zoom — where most agents
    /// are culled — costs more than the duplication it removes.
    /// </para>
    /// <para>
    /// So the two stages are split rather than duplicated.
    /// <see cref="Create"/> checks its arguments, computes the pose-invariant
    /// proportions, builds the posed layout, and derives the pose-blind
    /// rectangle; stage one stops after the rectangle, and
    /// <see cref="CompletePosedLayout"/> resumes from the same proportions.
    /// One <see cref="Create"/> call, one <see cref="CompletePosedLayout"/>
    /// call on its result, and one <see cref="PawnGeometry.Create"/> call cost
    /// the same and return the same layout; stopping after stage one costs
    /// strictly less than any of them.
    /// </para>
    /// <para>
    /// The value is opaque on purpose. A caller reads
    /// <see cref="PoseBlindVisualBounds"/> and hands the value back, which is
    /// what makes it impossible for the two stages to disagree about the
    /// inputs they were given. A <see langword="default"/> value is not a
    /// prefix of anything and describes no pawn; only <see cref="Create"/>
    /// produces a valid one.
    /// </para>
    /// </remarks>
    internal readonly struct PoseBlindPrefix
    {
        private readonly PawnProportions _proportions;
        private readonly Vector2 _footAnchor;
        private readonly PawnAppearance _appearance;
        private readonly float _armorWidthFactor;
        private readonly bool _hasSash;
        private readonly int _adornmentAccentMarkCount;

        private PoseBlindPrefix(
            in PawnProportions proportions,
            Vector2 footAnchor,
            PawnAppearance appearance,
            float armorWidthFactor,
            bool hasSash,
            int adornmentAccentMarkCount,
            Rectangle poseBlindVisualBounds)
        {
            _proportions = proportions;
            _footAnchor = footAnchor;
            _appearance = appearance;
            _armorWidthFactor = armorWidthFactor;
            _hasSash = hasSash;
            _adornmentAccentMarkCount = adornmentAccentMarkCount;
            PoseBlindVisualBounds = poseBlindVisualBounds;
        }

        /// <summary>
        /// The neutral-pose visual bounds, identical to what
        /// <c>PawnRenderer.GetBounds</c> returns for the same arguments.
        /// Pose-blind on purpose: a pose-aware cull would make the drawn set a
        /// function of presentation animation phase. See
        /// <c>PawnRenderer.GetBounds</c>.
        /// </summary>
        public Rectangle PoseBlindVisualBounds { get; }

        /// <summary>
        /// Stage one. Checks the arguments exactly as <see cref="Create"/>
        /// does, computes the pose-invariant proportions once, and derives the
        /// pose-blind cull rectangle from the cheap subset of the layout that
        /// can actually reach it. The posed layout is not built.
        /// </summary>
        /// <remarks>
        /// Every parameter means exactly what the same parameter on
        /// <see cref="PawnGeometry.Create"/> means. There is no
        /// <c>swingPose</c>, because a pose is precisely what this stage does
        /// not need and does not read.
        /// </remarks>
        public static PoseBlindPrefix Create(
            Vector2 footAnchor,
            float cameraZoom,
            PawnAppearance appearance,
            float scaleMultiplier = 1f,
            float armorWidthFactor = 1f,
            bool hasSash = false,
            int adornmentAccentMarkCount = 0)
        {
            var proportions = CreateProportions(
                footAnchor,
                cameraZoom,
                appearance,
                scaleMultiplier,
                armorWidthFactor,
                adornmentAccentMarkCount);

            return new PoseBlindPrefix(
                proportions,
                footAnchor,
                appearance,
                armorWidthFactor,
                hasSash,
                adornmentAccentMarkCount,
                CreatePoseBlindVisualBounds(
                    proportions,
                    footAnchor,
                    appearance,
                    armorWidthFactor));
        }

        /// <summary>
        /// Stage two. Finishes the posed layout from the proportions stage one
        /// already paid for, returning exactly what
        /// <see cref="PawnGeometry.Create"/> returns for the arguments stage
        /// one was given together with <paramref name="swingPose"/>.
        /// </summary>
        /// <param name="swingPose">
        /// The pose one in-flight swing puts this pawn in, or <c>null</c> for
        /// a pawn standing still, exactly as on
        /// <see cref="PawnGeometry.Create"/>. A neutral pose produces the same
        /// layout as no pose at all, so a caller may pass either.
        /// </param>
        /// <param name="gaitPose">
        /// The stride pose the legs and feet are drawn in, or <c>null</c> for
        /// a pawn standing still, exactly as on
        /// <see cref="PawnGeometry.Create"/>.
        /// </param>
        /// <remarks>
        /// Nothing stops a caller finishing the same prefix more than once
        /// with different poses: the prefix is a value, the stage is pure, and
        /// the proportions a pose cannot change are shared by every result.
        /// </remarks>
        public PawnLayout CompletePosedLayout(
            SwingPose? swingPose = null,
            GaitPose? gaitPose = null) =>
            CreateLayout(
                _proportions,
                _footAnchor,
                _appearance,
                swingPose ?? default,
                _armorWidthFactor,
                _hasSash,
                _adornmentAccentMarkCount,
                gaitPose ?? default);

        /// <summary>
        /// Stage two for the event-synchronized attack pose. The pose drives
        /// the target-facing weapon line and trail through the same channel a
        /// swing already used, and additionally composes the articulated arms,
        /// the planted attack stance, and the shield guard on top of whatever
        /// stride <paramref name="gaitPose"/> is in — it never replaces the
        /// rest of the pawn.
        /// </summary>
        /// <param name="attackPose">
        /// The pose one contact-latched attack puts this pawn in, or
        /// <c>null</c> for a pawn that is not attacking. A <c>null</c> pose
        /// produces a layout bit-for-bit identical to
        /// <see cref="PawnGeometry.Create"/>'s neutral one.
        /// </param>
        /// <param name="gaitPose">
        /// The stride pose the legs and feet are drawn in, exactly as on
        /// <see cref="CompletePosedLayout"/>. An active attack plants the
        /// stance, damping this stride rather than discarding it.
        /// </param>
        /// <param name="reactionOffset">
        /// The presentation-only displacement a contact reaction puts this
        /// pawn's body in, in pawn units before apparent scale
        /// (<c>DefenderReaction.ResolveOffset</c>), or <c>default</c> for a
        /// pawn nobody has just struck. A third additive lean channel
        /// alongside the attack pose's and the gait pose's own, so the feet
        /// stay planted on the authoritative position while the struck body
        /// recoils and settles back onto it.
        /// </param>
        public PawnLayout CompleteAttackPosedLayout(
            AttackPose? attackPose = null,
            GaitPose? gaitPose = null,
            (float X, float Y) reactionOffset = default) =>
            CreateLayout(
                _proportions,
                _footAnchor,
                _appearance,
                attackPose is { } pose
                    ? ToSwingPose(pose, _appearance.WeaponRole)
                    : default,
                _armorWidthFactor,
                _hasSash,
                _adornmentAccentMarkCount,
                gaitPose ?? default,
                attackPose,
                reactionOffset);
    }

    private static SwingPose ToSwingPose(
        AttackPose pose,
        PawnWeaponRole weaponRole)
    {
        var weaponDirection = pose.WeaponTip - pose.WeaponHand;
        var desiredAngle = MathF.Atan2(
            weaponDirection.Y,
            weaponDirection.X);
        var baseline = weaponRole switch
        {
            PawnWeaponRole.Itak => new Vector2(8f, -8f),
            PawnWeaponRole.Kampilan => new Vector2(14f, -13f),
            PawnWeaponRole.Wasay => new Vector2(11f, -13f),
            PawnWeaponRole.Kalis => new Vector2(13f, -14f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(weaponRole),
                weaponRole,
                null),
        };
        var baselineAngle = MathF.Atan2(baseline.Y, baseline.X);
        var extensionRatio =
            (weaponDirection.Length() - 1f) / ExtensionReach;
        var phase = pose.Phase is AttackAnimationPhase.Contact or
            AttackAnimationPhase.LethalHold
            ? SwingPhase.ImpactHold
            : SwingPhase.Recovery;

        return new SwingPose(
            phase,
            PhaseProgress: phase == SwingPhase.ImpactHold ? 0f : 1f,
            WeaponAngleRadians: NormalizeAngle(desiredAngle - baselineAngle),
            TorsoLeanX: pose.TorsoOffset.X,
            TorsoLeanY: pose.TorsoOffset.Y,
            ExtensionRatio: extensionRatio,
            TrailStrength: pose.TrailStrength);
    }

    /// <summary>
    /// Wraps a rotation into <c>[-pi, pi]</c>. The rotation itself is
    /// unaffected — a cosine and a sine do not care which turn they are on —
    /// but its <em>sign</em> is read as a direction of travel by
    /// <see cref="CreateSwingTrail"/>, and an unwrapped difference between two
    /// <see cref="MathF.Atan2"/> results can exceed pi. Without this wrap a
    /// warrior striking roughly up-and-left through left reported a positive
    /// rotation where the real one is negative, and its trail was drawn ahead
    /// of the blade instead of behind it, for about an eighth of all headings.
    /// </summary>
    private static float NormalizeAngle(float radians)
    {
        var wrapped = MathF.IEEERemainder(radians, MathF.Tau);
        return float.IsFinite(wrapped) ? wrapped : 0f;
    }

    /// <summary>
    /// Checks the arguments and computes everything about a pawn's layout
    /// that no swing pose can move: the apparent scale, the detail tier, the
    /// ground ring the planted feet keep, and every whole-pixel size the
    /// composed layers are built from. A pose translates and rotates what is
    /// drawn; it never resizes it, which is why these values are shared
    /// between the posed layout and the pose-blind bounds rather than derived
    /// twice.
    /// </summary>
    private static PawnProportions CreateProportions(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier,
        float armorWidthFactor,
        int adornmentAccentMarkCount)
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

        var torsoHeight = ToSize(
            TorsoHeightUnits * appearance.StatureMultiplier * apparentScale);
        var torsoWidth = ToSize(
            7f * appearance.BuildMultiplier * apparentScale);
        var headSize = ToSize(7f * apparentScale);
        var headGap = ToSize(apparentScale);
        var headTreatmentHeight = Math.Max(1, ToSize(2.6f * apparentScale));

        var legWidth = ToSize(LegWidthUnits * apparentScale);
        var legLength = ToSize(LegLengthUnits * apparentScale);
        var legGap = LegGapUnits * apparentScale;
        var footWidth = ToSize(FootWidthUnits * apparentScale);
        var footHeight = ToSize(FootHeightUnits * apparentScale);

        // Low tier never draws legs (design section 9), so the torso keeps
        // sitting in the same small max(1, apparentScale) gap above the
        // ground ring it always has — a distant pawn's silhouette is
        // pixel-for-pixel unchanged. Medium and High tier reserve the full
        // scaled leg length instead, so the leg-plus-foot band has real room
        // between the torso's bottom and the ground ring (see this class's
        // gait remarks above).
        var torsoBottomGap = detailTier == PawnDetailTier.Low
            ? MathF.Max(1f, apparentScale)
            : legLength;

        return new PawnProportions(
            apparentScale,
            detailTier,
            groundRingBounds,
            torsoWidth,
            torsoHeight,
            torsoBottomGap,
            headSize,
            headGap,
            headTreatmentHeight,
            CreateShieldBlock(
                footAnchor,
                apparentScale,
                appearance.ShieldSkinId,
                detailTier),
            legWidth,
            legLength,
            legGap,
            footWidth,
            footHeight);
    }

    /// <summary>
    /// The whole posed layout, built from the pose-invariant
    /// <paramref name="proportions"/> and the one pose that positions it.
    /// </summary>
    private static PawnLayout CreateLayout(
        in PawnProportions proportions,
        Vector2 footAnchor,
        PawnAppearance appearance,
        SwingPose pose,
        float armorWidthFactor,
        bool hasSash,
        int adornmentAccentMarkCount,
        GaitPose gaitPose,
        AttackPose? attackPose = null,
        (float X, float Y) reactionOffset = default)
    {
        var apparentScale = proportions.ApparentScale;
        var detailTier = proportions.DetailTier;

        // The feet stay planted, so the ground ring keeps the foot anchor
        // while everything the warrior can lean moves with the torso.
        var bodyAnchor = CreateBodyAnchor(
            footAnchor,
            apparentScale,
            pose,
            gaitPose,
            reactionOffset);
        var torsoBounds = CreateTorso(bodyAnchor, proportions);
        var legs = CreateLegsAndFeet(
            bodyAnchor,
            torsoBounds,
            proportions,
            detailTier,
            PlantStride(gaitPose, attackPose));
        var legsBounds = Rectangle.Union(
            Rectangle.Union(legs.LeftLeg, legs.RightLeg),
            Rectangle.Union(legs.LeftFoot, legs.RightFoot));
        var headBounds = CreateHead(bodyAnchor, torsoBounds, proportions);
        var headTreatmentBounds = CreateHeadTreatment(headBounds, proportions);

        // The step-4 diagnostic placeholder (visual-system-integration-
        // design.md section 4, R-W6.4) occupies the torso's footprint — the
        // one element every pawn always has — so that whichever domain's
        // resolution eventually reaches VisualFallbackStep.DiagnosticPlaceholder
        // has an unconditional, pure, already-bounded rectangle to draw
        // instead of nothing. Inscribed within TorsoBounds (never larger),
        // so it never grows VisualBounds beyond what the torso already
        // contributes and never disturbs PawnRenderer.GetBounds.
        // T8: built from torsoBounds' own integer edges rather than through
        // CenteredRectangle's float-rounded center. TorsoHeightUnits shrank
        // for the leg restructure (this class's gait remarks above), which
        // now lands on an odd torsoBounds.Height at some scales; Rectangle's
        // integer-truncating Center combined with CenteredRectangle's own
        // round-half-to-even could then place the placeholder's top edge one
        // pixel above torsoBounds' own top, breaking the inscribed-square
        // guarantee this rectangle exists to keep. This formula can never do
        // that: placeholderSide never exceeds either torso dimension, so both
        // integer offsets below are non-negative and the far edge never
        // exceeds torsoBounds' own far edge.
        var placeholderSide = Math.Min(torsoBounds.Width, torsoBounds.Height);
        var placeholderBounds = new Rectangle(
            torsoBounds.X + ((torsoBounds.Width - placeholderSide) / 2),
            torsoBounds.Y + ((torsoBounds.Height - placeholderSide) / 2),
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
        var shield = ApplyShieldGuard(
            CreateShield(
                proportions,
                torsoBounds,
                appearance.ShieldRole),
            apparentScale,
            attackPose);

        var arms = CreateArms(
            torsoBounds,
            weapon,
            shield,
            proportions,
            detailTier,
            attackPose);

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

        var renderedBounds = CreateRenderedBounds(
            proportions.GroundRingBounds,
            torsoBounds,
            headBounds,
            headTreatmentBounds,
            weapon.Bounds,
            weapon.SecondaryBounds,
            shield.Bounds,
            armorBounds,
            legsBounds,
            ArmsBounds(arms));
        var selectionBounds = CreateSelectionBounds(renderedBounds, apparentScale);
        var visualBounds = Rectangle.Union(renderedBounds, selectionBounds);

        return new PawnLayout(
            footAnchor,
            apparentScale,
            detailTier,
            proportions.GroundRingBounds,
            torsoBounds,
            headBounds,
            headTreatmentBounds,
            weapon.Start,
            weapon.End,
            CreateWeaponThickness(appearance.WeaponRole, apparentScale),
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
            CreateSwingTrail(weapon, apparentScale, detailTier, pose, attackPose),
            legs.LeftLeg,
            legs.RightLeg,
            legs.LeftFoot,
            legs.RightFoot,
            arms);
    }

    /// <summary>
    /// An attacking warrior plants. The stride the gait pose is carrying is
    /// damped toward the neutral stance in proportion to the attack's own
    /// stance weight — full at contact, gone once the pose has recovered —
    /// rather than being discarded, which is what keeps a warrior who strikes
    /// mid-stride from snapping its legs together for one frame.
    /// </summary>
    /// <remarks>
    /// The design's gait-conflict risk (section 14) is exactly this: two
    /// independent offset channels writing the same legs. Damping one by the
    /// other is a composition, so the neutral result of either channel is
    /// still the neutral result of the pair.
    /// </remarks>
    private static GaitPose PlantStride(GaitPose gaitPose, AttackPose? attackPose)
    {
        if (attackPose is not { } pose)
        {
            return gaitPose;
        }

        var planted = 1f - (MaximumStancePlant * Math.Clamp(pose.StanceWeight, 0f, 1f));

        return gaitPose with
        {
            LeftLegOffsetRatio = gaitPose.LeftLegOffsetRatio * planted,
            RightLegOffsetRatio = gaitPose.RightLegOffsetRatio * planted,
            LeftFootLiftRatio = gaitPose.LeftFootLiftRatio * planted,
            RightFootLiftRatio = gaitPose.RightFootLiftRatio * planted,
        };
    }

    /// <summary>
    /// The off-hand block's attack guard. A legal shield overlay brings the
    /// block a fixed few units toward the weapon line so it reads as covering
    /// the exchange; an illegal one — a two-handed family, where
    /// <see cref="AttackPose.HasShield"/> is already false — leaves the block
    /// exactly where the neutral layout puts it.
    /// </summary>
    /// <remarks>
    /// PROVISIONAL presentation choreography, not a documented guard position.
    /// The offset is deliberately fixed rather than derived from the pose's
    /// own shield hand: the block is a static silhouette element (see
    /// <see cref="CreateShieldBlock"/>) and a per-frame tracked shield would
    /// make the shielded silhouette harder to read at the one moment it
    /// matters most.
    /// </remarks>
    private static ShieldLayout ApplyShieldGuard(
        ShieldLayout shield,
        float apparentScale,
        AttackPose? attackPose)
    {
        if (shield.Bounds.IsEmpty ||
            attackPose is not { HasShield: true } pose)
        {
            return shield;
        }

        var guard = (int)MathF.Round(
            ShieldGuardOffsetUnits * apparentScale *
            Math.Clamp(pose.StanceWeight, 0f, 1f));
        if (guard == 0)
        {
            return shield;
        }

        return shield with
        {
            Bounds = new Rectangle(
                shield.Bounds.X + guard,
                shield.Bounds.Y - guard,
                shield.Bounds.Width,
                shield.Bounds.Height),
            Anchor = shield.Anchor + new Vector2(guard, -guard),
        };
    }

    /// <summary>
    /// The two-segment arms an actively attacking warrior draws. The weapon
    /// arm always runs shoulder to elbow to the grip the weapon line already
    /// starts from, so the hand and the weapon can never disagree about where
    /// the weapon is held. The off-hand arm exists only when the pose has
    /// somewhere to put it: a two-handed family's support grip on the haft, or
    /// a legal shield overlay's block.
    /// </summary>
    /// <remarks>
    /// Omitted entirely at <see cref="PawnDetailTier.Low"/>, where a pawn is a
    /// handful of pixels tall and two more segments per arm would be noise —
    /// the same tier rule the swing trail and the leg rectangles already
    /// follow. The elbow is displaced perpendicular to the shoulder-to-hand
    /// line so the pair reads as an articulated arm rather than one bar; the
    /// displacement is a PROVISIONAL presentation constant.
    /// </remarks>
    private static ArmLayout CreateArms(
        Rectangle torsoBounds,
        WeaponLayout weapon,
        ShieldLayout shield,
        in PawnProportions proportions,
        PawnDetailTier detailTier,
        AttackPose? attackPose)
    {
        if (detailTier == PawnDetailTier.Low || attackPose is not { } pose)
        {
            return default;
        }

        // A settled guard pose draws no arms. AttackAnimationSystem keeps a
        // timeline in its store once it reaches readiness — that phase is the
        // design's weapon-specific guarded stance, not an expiry — so without
        // this every warrior that had ever landed a blow would carry four arm
        // quads for the rest of the battle, and the design's "at most four
        // extra quads per active Medium or High pawn" would silently become a
        // per-pawn cost.
        if (pose.Phase == AttackAnimationPhase.Readiness)
        {
            return default;
        }

        var scale = proportions.ApparentScale;
        var padding = MathF.Max(ArmMinimumHalfWidthPixels, ArmHalfWidthUnits * scale);
        var shoulderY = torsoBounds.Top + (torsoBounds.Height * ShoulderHeightShare);
        var weaponShoulder = new Vector2(torsoBounds.Right, shoulderY);
        var supportShoulder = new Vector2(torsoBounds.Left, shoulderY);

        var thickness = MathF.Max(1f, padding * 2f);
        var (weaponUpper, weaponFore) = BuildArm(
            weaponShoulder,
            weapon.Start,
            scale,
            padding);

        if (!TryResolveSupportHand(pose, weapon, shield, out var supportHand))
        {
            return new ArmLayout(
                weaponUpper,
                weaponFore,
                default,
                default,
                thickness);
        }

        var (supportUpper, supportFore) = BuildArm(
            supportShoulder,
            supportHand,
            scale,
            padding);

        return new ArmLayout(
            weaponUpper,
            weaponFore,
            supportUpper,
            supportFore,
            thickness);
    }

    /// <summary>
    /// Where the off hand goes, or nowhere. A two-handed family anchors it on
    /// the haft a fixed share along the weapon line; a legal shield overlay
    /// anchors it on the block. A one-handed family carrying no shield has no
    /// second arm to draw and reports so.
    /// </summary>
    private static bool TryResolveSupportHand(
        AttackPose pose,
        WeaponLayout weapon,
        ShieldLayout shield,
        out Vector2 supportHand)
    {
        if (pose.HasSupportHand)
        {
            supportHand = Vector2.Lerp(
                weapon.Start,
                weapon.End,
                SupportGripShareAlongHaft);
            return true;
        }

        if (pose.HasShield && !shield.Bounds.IsEmpty)
        {
            supportHand = new Vector2(
                shield.Bounds.Right,
                shield.Bounds.Center.Y);
            return true;
        }

        supportHand = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// One arm as two rectangles. The elbow sits on the perpendicular of the
    /// shoulder-to-hand line, displaced away from the body, so the two
    /// segments never collapse onto each other however the weapon is aimed.
    /// </summary>
    private static (ArmSegment Upper, ArmSegment Fore) BuildArm(
        Vector2 shoulder,
        Vector2 hand,
        float scale,
        float padding)
    {
        var span = hand - shoulder;
        var length = span.Length();

        // A coincident shoulder and hand carries no direction to take a
        // perpendicular of, so the elbow drops straight down instead of
        // producing a zero or NaN basis.
        var perpendicular = length > 0f
            ? new Vector2(-span.Y / length, span.X / length)
            : new Vector2(0f, 1f);
        var elbow = shoulder + (span * 0.5f) +
            (perpendicular * ElbowDisplacementUnits * scale);

        return (
            new ArmSegment(
                shoulder,
                elbow,
                BoundsFromLine(shoulder, elbow, padding)),
            new ArmSegment(
                elbow,
                hand,
                BoundsFromLine(elbow, hand, padding)));
    }

    private static Rectangle ArmsBounds(ArmLayout arms)
    {
        if (arms.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var bounds = Rectangle.Union(
            arms.WeaponUpperArm.Bounds,
            arms.WeaponForearm.Bounds);

        if (!arms.SupportUpperArm.IsEmpty)
        {
            bounds = Rectangle.Union(bounds, arms.SupportUpperArm.Bounds);
        }

        if (!arms.SupportForearm.IsEmpty)
        {
            bounds = Rectangle.Union(bounds, arms.SupportForearm.Bounds);
        }

        return bounds;
    }

    /// <summary>
    /// GPU-013. <see cref="PawnLayout.VisualBounds"/> as it comes out of a
    /// neutral pose, computed from only the layers that can reach the
    /// bounding union. This is the cheap subset of <see cref="CreateLayout"/>,
    /// not a second run of it: the sash, the adornment accents, the
    /// diagnostic placeholder, the swing trail, and the weapon thickness are
    /// never built, and <paramref name="proportions"/> carries the argument
    /// checks, the scale, the tier, the ground ring, and every size already
    /// paid for by the posed layout.
    /// </summary>
    /// <remarks>
    /// The pose is passed as <see langword="default"/> through the same
    /// <see cref="CreateBodyAnchor"/>, <see cref="CreateWeaponLayout"/>, and
    /// <see cref="CreateLegsAndFeet"/> the posed path uses, rather than by
    /// omitting the pose terms, so the result is identical to
    /// <c>PawnRenderer.GetBounds</c> by construction rather than by argument.
    /// A neutral pose leans by nothing and rotates by nothing, which is
    /// exactly what <c>swingPose: null</c> means to <see cref="Create"/>, and
    /// <c>default(GaitPose)</c> is documented as the same "standing still"
    /// neutral for the legs. This is deliberately the neutral-stance leg
    /// footprint rather than a stride envelope larger than it: the actual
    /// gait phase is never read here, which is what keeps two different
    /// phases at one position producing an identical result, and reading
    /// <see langword="default"/> unconditionally is what keeps this
    /// bit-identical to <c>PawnRenderer.GetBounds</c>, which never forwards a
    /// gait pose at all.
    /// </remarks>
    private static Rectangle CreatePoseBlindVisualBounds(
        in PawnProportions proportions,
        Vector2 footAnchor,
        PawnAppearance appearance,
        float armorWidthFactor)
    {
        var apparentScale = proportions.ApparentScale;
        var detailTier = proportions.DetailTier;

        var bodyAnchor = CreateBodyAnchor(footAnchor, apparentScale, default, default);
        var torsoBounds = CreateTorso(bodyAnchor, proportions);
        var headBounds = CreateHead(bodyAnchor, torsoBounds, proportions);
        var headTreatmentBounds = CreateHeadTreatment(headBounds, proportions);
        var weapon = CreateWeaponLayout(
            bodyAnchor,
            apparentScale,
            appearance.WeaponRole,
            detailTier,
            default);
        var shieldBounds = CreateShieldBounds(
            proportions,
            torsoBounds,
            appearance.ShieldRole);
        var armorBounds = CreateArmor(torsoBounds, detailTier, armorWidthFactor);
        var legs = CreateLegsAndFeet(
            bodyAnchor,
            torsoBounds,
            proportions,
            detailTier,
            default);
        var legsBounds = Rectangle.Union(
            Rectangle.Union(legs.LeftLeg, legs.RightLeg),
            Rectangle.Union(legs.LeftFoot, legs.RightFoot));

        var renderedBounds = CreateRenderedBounds(
            proportions.GroundRingBounds,
            torsoBounds,
            headBounds,
            headTreatmentBounds,
            weapon.Bounds,
            weapon.SecondaryBounds,
            shieldBounds,
            armorBounds,
            legsBounds,

            // Pose-blind on purpose: no arm rectangle exists without an
            // attack pose, and reading one here would make the cull
            // rectangle a function of presentation animation phase.
            Rectangle.Empty);

        return Rectangle.Union(
            renderedBounds,
            CreateSelectionBounds(renderedBounds, apparentScale));
    }

    /// <summary>
    /// Where the torso, and everything that leans with it, is centred. The
    /// lean is the only way a pose moves the body; a neutral pose leaves the
    /// body on the foot anchor exactly. Sums the swing pose's lean with the
    /// gait pose's own <see cref="GaitPose.TorsoLeanX"/>/
    /// <see cref="GaitPose.TorsoLeanY"/> (nonzero only at
    /// <see cref="GaitMode.Run"/>), the same way the two are independent,
    /// additive channels on the pose types themselves.
    /// </summary>
    private static Vector2 CreateBodyAnchor(
        Vector2 footAnchor,
        float apparentScale,
        SwingPose pose,
        GaitPose gaitPose,
        (float X, float Y) reactionOffset = default) =>
        footAnchor + new Vector2(
            (pose.TorsoLeanX + gaitPose.TorsoLeanX + reactionOffset.X) * apparentScale,
            (pose.TorsoLeanY + gaitPose.TorsoLeanY + reactionOffset.Y) * apparentScale);

    private static Rectangle CreateTorso(
        Vector2 bodyAnchor,
        in PawnProportions proportions)
    {
        var torsoBottom = (int)MathF.Round(
            bodyAnchor.Y - proportions.TorsoBottomGap);

        return new Rectangle(
            (int)MathF.Round(bodyAnchor.X - (proportions.TorsoWidth / 2f)),
            torsoBottom - proportions.TorsoHeight,
            proportions.TorsoWidth,
            proportions.TorsoHeight);
    }

    private static Rectangle CreateHead(
        Vector2 bodyAnchor,
        Rectangle torsoBounds,
        in PawnProportions proportions) =>
        new(
            (int)MathF.Round(bodyAnchor.X - (proportions.HeadSize / 2f)),
            torsoBounds.Top - proportions.HeadGap - proportions.HeadSize,
            proportions.HeadSize,
            proportions.HeadSize);

    private static Rectangle CreateHeadTreatment(
        Rectangle headBounds,
        in PawnProportions proportions) =>
        new(
            headBounds.Left,
            headBounds.Top,
            headBounds.Width,
            proportions.HeadTreatmentHeight);

    /// <summary>
    /// The four leg and foot rectangles a warrior draws between the torso's
    /// bottom and the ground ring (movement-gait-animation design section
    /// 7), built from the pose-invariant leg proportions plus
    /// <paramref name="gaitPose"/>. Mirrors <see cref="CreateWeaponLayout"/>'s
    /// own shape: a pure function of the anchor, the proportions, the tier,
    /// and one pose, called once with the actual gait pose by
    /// <see cref="CreateLayout"/> and once with
    /// <see langword="default"/>(<see cref="GaitPose"/>) by
    /// <see cref="CreatePoseBlindVisualBounds"/>, exactly as
    /// <see cref="CreateBodyAnchor"/> and <see cref="CreateWeaponLayout"/>
    /// already are.
    /// </summary>
    /// <remarks>
    /// <see langword="default"/>(<see cref="LegLayout"/>) — four
    /// <see cref="Rectangle.Empty"/> rectangles — at
    /// <see cref="PawnDetailTier.Low"/> (design section 9's detail-tier
    /// table), where a pawn is a handful of pixels tall and the ground ring
    /// already carries the footprint. <see cref="GaitPose.LeftLegOffsetRatio"/>
    /// and <see cref="GaitPose.RightLegOffsetRatio"/> are direction-agnostic
    /// on the pose itself, so this is where
    /// <see cref="GaitPose.DirectionSign"/> is applied, matching that field's
    /// own documented contract.
    /// </remarks>
    private static LegLayout CreateLegsAndFeet(
        Vector2 bodyAnchor,
        Rectangle torsoBounds,
        in PawnProportions proportions,
        PawnDetailTier detailTier,
        GaitPose gaitPose)
    {
        if (detailTier == PawnDetailTier.Low)
        {
            return default;
        }

        var legTop = torsoBounds.Bottom;
        var legBaseHeight = Math.Max(
            0,
            proportions.LegLength - proportions.FootHeight);

        var leftCenterX = bodyAnchor.X - proportions.LegGap +
            (gaitPose.LeftLegOffsetRatio * gaitPose.DirectionSign * proportions.LegLength);
        var rightCenterX = bodyAnchor.X + proportions.LegGap +
            (gaitPose.RightLegOffsetRatio * gaitPose.DirectionSign * proportions.LegLength);
        var leftLiftY = gaitPose.LeftFootLiftRatio * proportions.LegLength;
        var rightLiftY = gaitPose.RightFootLiftRatio * proportions.LegLength;

        var leftLeg = BuildLeg(
            leftCenterX,
            legTop,
            leftLiftY,
            legBaseHeight,
            proportions.LegWidth);
        var rightLeg = BuildLeg(
            rightCenterX,
            legTop,
            rightLiftY,
            legBaseHeight,
            proportions.LegWidth);
        var leftFoot = BuildFoot(leftLeg, proportions.FootWidth, proportions.FootHeight);
        var rightFoot = BuildFoot(rightLeg, proportions.FootWidth, proportions.FootHeight);

        return new LegLayout(leftLeg, rightLeg, leftFoot, rightFoot);
    }

    /// <summary>
    /// One leg rectangle: horizontally centred on <paramref name="centerX"/>,
    /// rising <paramref name="liftY"/> layout pixels above
    /// <paramref name="legTop"/> when the gait pose lifts that leg.
    /// </summary>
    private static Rectangle BuildLeg(
        float centerX,
        int legTop,
        float liftY,
        int legBaseHeight,
        int legWidth)
    {
        var top = legTop - (int)MathF.Round(liftY);
        return new Rectangle(
            (int)MathF.Round(centerX - (legWidth / 2f)),
            top,
            legWidth,
            legBaseHeight);
    }

    /// <summary>
    /// The foot directly beneath <paramref name="leg"/>, so a lifted leg's
    /// foot rises with it.
    /// </summary>
    private static Rectangle BuildFoot(
        Rectangle leg,
        int footWidth,
        int footHeight) =>
        new(
            (int)MathF.Round(leg.Center.X - (footWidth / 2f)),
            leg.Bottom,
            footWidth,
            footHeight);

    /// <summary>
    /// The union every drawn layer sits inside, in the one order both the
    /// posed layout and the pose-blind bounds walk it. Sole authority for
    /// which layers contribute, so the two callers can never drift apart on
    /// that question.
    /// </summary>
    /// <remarks>
    /// The sash, the adornment accents, and the diagnostic placeholder are
    /// absent deliberately: each is inscribed inside the torso or head
    /// footprint by construction — see each helper's own remarks — so none of
    /// them can grow this rectangle. <paramref name="armorBounds"/> is a
    /// composed layer that can extend past the torso it widens, and
    /// <paramref name="legsBounds"/> (movement-gait-animation design section
    /// 7) sits below the torso rather than inside it, so both have to be
    /// folded in explicitly.
    /// </remarks>
    private static Rectangle CreateRenderedBounds(
        Rectangle groundRingBounds,
        Rectangle torsoBounds,
        Rectangle headBounds,
        Rectangle headTreatmentBounds,
        Rectangle weaponBounds,
        Rectangle weaponSecondaryBounds,
        Rectangle shieldBounds,
        Rectangle armorBounds,
        Rectangle legsBounds,
        Rectangle armsBounds)
    {
        var renderedBounds = Rectangle.Union(groundRingBounds, torsoBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headTreatmentBounds);
        renderedBounds = Rectangle.Union(renderedBounds, weaponBounds);

        if (!weaponSecondaryBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(
                renderedBounds,
                weaponSecondaryBounds);
        }

        if (!shieldBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, shieldBounds);
        }

        if (!armorBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, armorBounds);
        }

        if (!legsBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, legsBounds);
        }

        if (!armsBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, armsBounds);
        }

        return renderedBounds;
    }

    private static Rectangle CreateSelectionBounds(
        Rectangle renderedBounds,
        float apparentScale) =>
        Inflate(
            renderedBounds,
            Math.Max(3, (int)MathF.Ceiling(3f * apparentScale)));

    /// <summary>
    /// The arc the weapon tip has just swept, derived from the pose alone. It
    /// is omitted entirely at the low detail tier, where a pawn is a handful
    /// of pixels tall and the arc would be noise.
    /// </summary>
    private static SwingTrail CreateSwingTrail(
        WeaponLayout weapon,
        float scale,
        PawnDetailTier detailTier,
        SwingPose pose,
        AttackPose? attackPose = null)
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
            MathF.Max(1f, TrailThickness * scale),
            ResolveTrailEmphasis(attackPose));
    }

    /// <summary>
    /// How hard the trail reads for the outcome that produced it. A lethal
    /// blow is the hardest, an evasion the faintest — a weapon that met
    /// nothing leaves the least behind it — and everything else sits between.
    /// PROVISIONAL presentation choreography; the magnitudes carry no combat
    /// or historical meaning.
    /// </summary>
    private static float ResolveTrailEmphasis(AttackPose? attackPose)
    {
        if (attackPose is not { } pose)
        {
            return 1f;
        }

        var outcome = pose.Resolution switch
        {
            AttackResolution.Landed => 1f,
            AttackResolution.ShieldBlocked => 0.82f,
            AttackResolution.Parried => 0.74f,
            AttackResolution.Deflected => 0.88f,
            AttackResolution.Evaded => 0.62f,
            _ => throw new ArgumentOutOfRangeException(
                nameof(attackPose),
                pose.Resolution,
                null),
        };

        return pose.IsLethal ? outcome * LethalTrailEmphasis : outcome;
    }

    /// <summary>
    /// Everything about the block a shield bearer draws beside the torso that
    /// the torso's own position does not decide: its width, its height, its
    /// left edge, and the gap between the torso's top and its own. All four
    /// are pose-invariant — the shield is deliberately not posed, and its
    /// left edge is measured from the planted foot anchor rather than from
    /// the leaning body — so they are computed once per pawn and reused by
    /// both the posed layout and the pose-blind bounds.
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
    private static ShieldBlock CreateShieldBlock(
        Vector2 footAnchor,
        float scale,
        string shieldSkinId,
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

        return new ShieldBlock(left, width, height, ToSize(scale));
    }

    /// <summary>
    /// Where <see cref="CreateShieldBlock"/>'s block lands once the torso is
    /// placed. The torso's top is the only thing the pose contributes here.
    /// </summary>
    private static Rectangle CreateShieldRectangle(
        in PawnProportions proportions,
        Rectangle torsoBounds) =>
        new(
            proportions.Shield.Left,
            torsoBounds.Top + proportions.Shield.TopOffset,
            proportions.Shield.Width,
            proportions.Shield.Height);

    /// <summary>
    /// The drawn shield block alone, <see cref="Rectangle.Empty"/> for a
    /// warrior carrying no shield. What the bounding union reads; the
    /// pose-blind path needs nothing else from the shield, so it never builds
    /// the anchor.
    /// </summary>
    private static Rectangle CreateShieldBounds(
        in PawnProportions proportions,
        Rectangle torsoBounds,
        PawnShieldRole role) =>
        role == PawnShieldRole.None
            ? Rectangle.Empty
            : CreateShieldRectangle(proportions, torsoBounds);

    /// <summary>
    /// The block a shield bearer draws beside the torso, on the side opposite
    /// the weapon, together with the point future shield-skin work anchors
    /// to. <see cref="ShieldLayout.Bounds"/> is <see cref="Rectangle.Empty"/>
    /// for a warrior carrying no shield; <see cref="ShieldLayout.Anchor"/> is
    /// computed unconditionally, because it is a pure layout output the
    /// composed-layer design (integration design section 5) reserves as an
    /// attachment point regardless of loadout.
    /// </summary>
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
        in PawnProportions proportions,
        Rectangle torsoBounds,
        PawnShieldRole role)
    {
        var rectangle = CreateShieldRectangle(proportions, torsoBounds);

        return new ShieldLayout(
            role == PawnShieldRole.None ? Rectangle.Empty : rectangle,
            new Vector2(rectangle.Center.X, rectangle.Center.Y));
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
            bounds,
            secondaryBounds);
    }

    /// <summary>
    /// The stroke width the weapon line is drawn at. Split out of
    /// <see cref="CreateWeaponLayout"/> because it is the one weapon value
    /// that is not a rectangle and therefore cannot reach a bounding union:
    /// <see cref="CreatePoseBlindVisualBounds"/> never asks for it.
    /// </summary>
    private static float CreateWeaponThickness(
        PawnWeaponRole role,
        float scale) =>
        MathF.Max(
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
        Rectangle Bounds,
        Rectangle SecondaryBounds);

    /// <summary>
    /// The four rectangles <see cref="CreateLegsAndFeet"/> produces.
    /// <see langword="default"/> — four <see cref="Rectangle.Empty"/>
    /// rectangles — is the <see cref="PawnDetailTier.Low"/> result, and their
    /// union collapses to <see cref="Rectangle.Empty"/> in that case, the
    /// same "empty when absent" convention every other optional layer in this
    /// class already uses.
    /// </summary>
    private readonly record struct LegLayout(
        Rectangle LeftLeg,
        Rectangle RightLeg,
        Rectangle LeftFoot,
        Rectangle RightFoot);

    /// <summary>
    /// GPU-013. The pose-invariant half of a pawn's layout: the values a
    /// swing can never change, computed once per call and read by both the
    /// posed layout and the pose-blind bounds. A pose leans the body and
    /// rotates the weapon line, so it moves rectangles; it never resizes
    /// them, and it never touches the ground ring, which stays on the planted
    /// feet.
    /// </summary>
    /// <param name="ShieldTopOffset">
    /// How far below the torso's own top the shield block starts. Part of the
    /// block rather than of <see cref="CreateShieldRectangle"/> because the
    /// distance is a function of apparent scale alone.
    /// </param>
    /// <param name="TorsoBottomGap">
    /// How far above the body anchor the torso's own bottom edge sits
    /// (movement-gait-animation design section 7, T8 correction): the full
    /// scaled <see cref="LegLength"/> at Medium and High tier, where the legs
    /// occupy that band for real, or the original small
    /// <c>max(1, apparentScale)</c> at <see cref="PawnDetailTier.Low"/>,
    /// where no legs draw and the torso keeps its pre-gait position exactly.
    /// </param>
    /// <param name="LegWidth">See <see cref="LegWidthUnits"/>.</param>
    /// <param name="LegLength">See <see cref="LegLengthUnits"/>.</param>
    /// <param name="LegGap">See <see cref="LegGapUnits"/>.</param>
    /// <param name="FootWidth">See <see cref="FootWidthUnits"/>.</param>
    /// <param name="FootHeight">See <see cref="FootHeightUnits"/>.</param>
    private readonly record struct PawnProportions(
        float ApparentScale,
        PawnDetailTier DetailTier,
        Rectangle GroundRingBounds,
        int TorsoWidth,
        int TorsoHeight,
        float TorsoBottomGap,
        int HeadSize,
        int HeadGap,
        int HeadTreatmentHeight,
        ShieldBlock Shield,
        int LegWidth,
        int LegLength,
        float LegGap,
        int FootWidth,
        int FootHeight);

    /// <param name="Left">
    /// The block's left edge, measured from the planted foot anchor rather
    /// than from the leaning body, so no pose moves it.
    /// </param>
    /// <param name="TopOffset">
    /// The gap between the torso's top and the block's own top.
    /// </param>
    private readonly record struct ShieldBlock(
        int Left,
        int Width,
        int Height,
        int TopOffset);

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
