using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.UI;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

internal enum PawnVisualState
{
    Normal,
    Hovered,
    Selected,
    Dead,
}

/// <summary>
/// The three quads that make up the leader mark's upward chevron —
/// <see cref="PawnRenderer.GetLeaderMarkGlyph"/>'s pure return shape. A base
/// band plus two rising arms sharing one apex, all in value types so the
/// geometry can be asserted without a <c>GraphicsDevice</c>.
/// </summary>
/// <param name="Base">The horizontal band across the bottom of the slot.</param>
/// <param name="LeftArmStart">The base band's left end, where the left arm rises from.</param>
/// <param name="LeftArmEnd">The chevron's apex, shared with <paramref name="RightArmEnd"/>.</param>
/// <param name="RightArmStart">The base band's right end, where the right arm rises from.</param>
/// <param name="RightArmEnd">The chevron's apex, shared with <paramref name="LeftArmEnd"/>.</param>
public readonly record struct LeaderMarkGlyph(
    Rectangle Base,
    Vector2 LeftArmStart,
    Vector2 LeftArmEnd,
    Vector2 RightArmStart,
    Vector2 RightArmEnd);

internal static class PawnRenderer
{
    private static readonly Color ShadowColor = new(30, 40, 48);
    private static readonly Color OutlineColor = new(10, 15, 21);
    private static readonly Color CharredWood = new(48, 40, 33);
    private static readonly Color Iron = new(56, 66, 73);
    private static readonly Color IronHighlight = new(130, 142, 145);
    private static readonly Color HoverColor = new(231, 199, 84);
    private static readonly Color DeadColor = new(91, 98, 105);

    /// <summary>
    /// The leader mark's dedicated color. Deliberately not
    /// <see cref="DyePalette.GoldAccent"/> — that warm tone already draws the
    /// adornment accent on the same pawn (and sits close to
    /// <see cref="HoverColor"/>'s own warm yellow), so reusing it here would
    /// make the leader mark indistinguishable from existing detail at
    /// typical camera zoom. A cool, saturated tone absent from the rest of
    /// this file's warm dye-derived palette keeps it legible as a UI marker
    /// rather than a garment or ornament color.
    /// </summary>
    private static readonly Color LeaderColor = new(92, 200, 214);

    /// <summary>
    /// The break-off mark's dedicated color — channel 1 of the spectator
    /// explanation in pressure-interrupt design section 3, question 8. It
    /// follows <see cref="LeaderColor"/>'s precedent exactly: a private tone
    /// on this renderer rather than a semantic <c>UiTheme</c> role, because
    /// nothing in the pawn layer reads a theme. <c>PawnRenderer</c> takes no
    /// <c>UiTheme</c> parameter, <c>FactionColorPalette</c> resolves the only
    /// other pawn color statically, and threading a theme down to a warrior's
    /// head would be a wider change than this mark justifies.
    ///
    /// A hot orange, chosen against the rest of this file for the same reason
    /// <see cref="LeaderColor"/> is cool: it is far from
    /// <see cref="LeaderColor"/>'s cyan, which can sit one slot below it on
    /// the same pawn, and far from <see cref="HoverColor"/>'s and
    /// <see cref="HitPulseColor"/>'s pale yellows, which are the only other
    /// warm UI tones a pawn can carry.
    /// </summary>
    private static readonly Color BreakOffColor = new(232, 108, 40);

    private static readonly Color HitPulseColor = new(255, 244, 214);
    private static readonly Color SwingTrailColor = new(206, 214, 220);

    /// <summary>
    /// Segments used to walk one arc trail. The arc itself comes from the
    /// layout; this is only how finely it is stroked.
    /// </summary>
    private const int SwingTrailSegments = 6;

    /// <summary>
    /// Owns the seen-set that deduplicates the step-4 diagnostic (R-W6.5)
    /// once per session across every pawn this renderer draws. A static
    /// field is the persistent home for that state: <see cref="Draw"/> is
    /// called every frame for every pawn, and a fresh
    /// <see cref="VisualDiagnostics"/> per call would defeat the
    /// once-per-identifier contract.
    /// </summary>
    private static readonly VisualDiagnostics PlaceholderDiagnostics = new();

    /// <summary>
    /// The catalog and requested identifiers reported when the torso's
    /// resolution reaches <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>.
    /// No catalog drives the torso yet (visual-system-integration-design.md
    /// section 4), so these are fixed, stable stand-ins rather than a real
    /// lookup key.
    /// </summary>
    private const string TorsoPlaceholderCatalogId = "pawn.torso";

    private const string TorsoPlaceholderRequestedId = "torso";

    /// <summary>
    /// Neutral, pose-blind bounds, deliberately. This feeds the frustum cull,
    /// and a pose-aware cull would make the set of drawn pawns a function of
    /// presentation animation phase, so the same tick would render a different
    /// draw list depending on where each swing clock happened to sit. That is
    /// draw-list determinism and it is the whole reason.
    /// </summary>
    /// <remarks>
    /// The cost is real and is not waved away: the arena bounds are the
    /// scissored arena panel rather than the screen, so a pawn whose body sits
    /// outside the panel while its weapon would sweep into it is dropped
    /// entirely, and the tip clips at the panel edge while panning. The
    /// selection padding does not absorb this, by roughly four times. It is
    /// accepted and carries an interactive smoke row rather than an assertion.
    /// <para>
    /// GPU-013. A caller that also needs the posed
    /// <see cref="PawnLayout"/> for the same pawn — which the arena render
    /// loop always does, for every pawn that survives the cull — should reach
    /// for <see cref="PawnGeometry.CreateWithPoseBlindBounds"/> instead, which
    /// returns both from one call and builds the pose-blind rectangle from
    /// only the layers that can reach it. This method is a whole
    /// <see cref="PawnGeometry.Create"/> that keeps one field, and it stays
    /// exactly that on purpose: it is the reference the equivalence test in
    /// <c>PawnGeometryTests</c> compares the combined call against.
    /// </para>
    /// </remarks>
    public static Rectangle GetBounds(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f,
        float armorWidthFactor = 1f,
        bool hasSash = false,
        int adornmentAccentMarkCount = 0) =>
        PawnGeometry.Create(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier,
            swingPose: null,
            armorWidthFactor,
            hasSash,
            adornmentAccentMarkCount).VisualBounds;

    /// <param name="swingPose">
    /// The pose an in-flight swing puts this pawn in, or <c>null</c> for a
    /// pawn standing still. Optional so that the inspector portrait, which is
    /// a still, keeps compiling without passing one.
    /// </param>
    /// <param name="armorWidthFactor">
    /// VIS-023, layer 4. Forwarded to <see cref="PawnGeometry.Create"/>
    /// unchanged — see that method's own remarks. <c>1f</c> (the default) is
    /// the no-op state: no separate armor rectangle draws.
    /// </param>
    /// <param name="hasSash">
    /// VIS-023, layer 5. Forwarded to <see cref="PawnGeometry.Create"/>
    /// unchanged. <see langword="false"/> (the default) is the no-op state.
    /// </param>
    /// <param name="adornmentAccentMarkCount">
    /// VIS-023, layer 9. Forwarded to <see cref="PawnGeometry.Create"/>
    /// unchanged. <c>0</c> (the default) is the no-op state.
    /// </param>
    /// <param name="torsoResolutionStep">
    /// The fallback step the torso's (currently hard-coded, not yet
    /// catalog-driven) resolution reached. Defaults to
    /// <see cref="VisualFallbackStep.ModelCategoryDefault"/> — today's
    /// drawable, the step every existing caller already implicitly resolves
    /// at (visual-system-integration-design.md section 4) — so no existing
    /// call site changes behavior. Passing
    /// <see cref="VisualFallbackStep.DiagnosticPlaceholder"/> draws the
    /// conspicuous placeholder block in place of the torso instead.
    /// </param>
    /// <param name="log">
    /// The log the step-4 diagnostic (R-W6.5) is reported through when
    /// <paramref name="torsoResolutionStep"/> is
    /// <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>. <c>null</c>
    /// (the default) suppresses the report; every existing caller passes
    /// nothing, so no per-frame diagnostic emission is possible today.
    /// </param>
    /// <param name="contingentId">
    /// The pawn's <see cref="AgentView.ContingentId"/>, used only to derive
    /// the ground-base tint. Defaulted, matching <paramref name="swingPose"/>
    /// above, so the two existing call sites keep compiling without naming
    /// it.
    /// </param>
    /// <param name="contingentState">
    /// The pawn's <see cref="AgentView.ContingentState"/>, used only to gate
    /// the ground-base tint. Defaulted to <see cref="ContingentState.None"/>,
    /// under which the tint is <paramref name="factionColor"/> unmodified, so
    /// a run under the frozen preset — where every agent carries
    /// <see cref="ContingentState.None"/> — looks exactly as it looks today.
    /// </param>
    /// <param name="brokeOffUnderPressure">
    /// The pawn's <see cref="AgentView.BrokeOffUnderPressure"/>, used only to
    /// gate the break-off mark above the head. Defaulted to
    /// <see langword="false"/>, matching <paramref name="contingentState"/>
    /// above, so the existing call sites keep compiling without naming it and
    /// so a run under any preset that does not apply the pressure interrupt —
    /// where every view carries <see langword="false"/> — draws exactly what
    /// it draws today.
    /// </param>
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        Color factionColor,
        PawnVisualState state,
        float scaleMultiplier = 1f,
        float hitPulseStrength = 0f,
        SwingPose? swingPose = null,
        float armorWidthFactor = 1f,
        bool hasSash = false,
        int adornmentAccentMarkCount = 0,
        VisualFallbackStep torsoResolutionStep = VisualFallbackStep.ModelCategoryDefault,
        DiagnosticLog? log = null,
        int contingentId = 0,
        ContingentState contingentState = ContingentState.None,
        bool isLeader = false,
        bool brokeOffUnderPressure = false)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        if (!float.IsFinite(hitPulseStrength) ||
            hitPulseStrength is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(hitPulseStrength));
        }

        DrawLayout(
            spriteBatch,
            pixel,
            PawnGeometry.Create(
                footAnchor,
                cameraZoom,
                appearance,
                scaleMultiplier,
                swingPose,
                armorWidthFactor,
                hasSash,
                adornmentAccentMarkCount),
            appearance,
            factionColor,
            state,
            hitPulseStrength,
            torsoResolutionStep,
            log,
            contingentId,
            contingentState,
            isLeader,
            brokeOffUnderPressure);
    }

    /// <summary>
    /// Submits one composed pawn from a <see cref="PawnLayout"/> the caller
    /// already built, instead of building it here.
    /// </summary>
    /// <remarks>
    /// GPU-004. This method and <see cref="Draw"/> draw the identical
    /// sequence of quads: <see cref="Draw"/> is now nothing but the
    /// <see cref="PawnGeometry.Create"/> call followed by a delegation to
    /// this one, so no call site changes what it puts on screen. The split
    /// exists so the arena render path can fence layout construction — which
    /// is per-agent CPU work, and therefore Phase 2's concern — out of the
    /// span that measures submission, which is Phase 3's concern. Against the
    /// old combined method the two kinds of work were inseparable, because
    /// every pawn's <c>PawnGeometry.Create</c> ran inside the same call that
    /// issued its <c>SpriteBatch.Draw</c> calls.
    ///
    /// The colour resolution below (the hit-pulse and dead-state blends) is
    /// deliberately left here rather than hoisted with the layout. It is
    /// interleaved with the draw calls it feeds, and moving it would
    /// restructure the draw sequence, which this task may not do. It is
    /// therefore measured as part of submission, and that attribution is a
    /// known, stated approximation rather than an exact boundary.
    /// </remarks>
    public static void DrawLayout(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnAppearance appearance,
        Color factionColor,
        PawnVisualState state,
        float hitPulseStrength = 0f,
        VisualFallbackStep torsoResolutionStep = VisualFallbackStep.ModelCategoryDefault,
        DiagnosticLog? log = null,
        int contingentId = 0,
        ContingentState contingentState = ContingentState.None,
        bool isLeader = false,
        bool brokeOffUnderPressure = false)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        if (!float.IsFinite(hitPulseStrength) ||
            hitPulseStrength is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(hitPulseStrength));
        }

        var isDead = state == PawnVisualState.Dead;
        // VIS-018: the torso fill now draws the resolved warrior-appearance
        // preset's garment base tone (Presentation.PawnAppearance.
        // GarmentBaseTone) instead of the old three-trait placeholder
        // ClothingColor — "garment base tone folded into the existing torso
        // fill" (warrior-appearance-design.md zoom table, Low tier).
        // appearance.ClothingColor itself is untouched and still computed
        // exactly as before; only which field this draw reads changed.
        var torsoFillColor = ApplyHitPulse(
            ApplyState(appearance.GarmentBaseTone, isDead),
            hitPulseStrength);
        var accentColor = ApplyHitPulse(
            ApplyState(appearance.AccentColor, isDead),
            hitPulseStrength);
        var skinColor = ApplyHitPulse(
            ApplyState(appearance.SkinColor, isDead),
            hitPulseStrength);
        var headTreatmentColor = ApplyHitPulse(
            ApplyState(appearance.HeadTreatmentColor, isDead),
            hitPulseStrength);
        var groundBaseColor = FactionColorPalette.GetContingentGroundTint(
            factionColor,
            contingentId,
            contingentState);
        var displayedFactionColor = ApplyHitPulse(
            ApplyState(groundBaseColor, isDead),
            hitPulseStrength);
        // VIS-023: layers 4/5/9 draw from the closed dye palette (R-W3.8),
        // the same catalog DrawShield already reads a fixed accent color
        // from (WeaponVisualCatalog.RattanLashingTone). No per-preset armor
        // material tone or sash color exists yet — that is a later
        // preset-composition task's concern — so these are fixed,
        // PROVISIONAL stand-ins rather than appearance-driven values, routed
        // through the same single hit-pulse blend point as every other
        // color on this pawn (integration design section 6 rule 5).
        var armorMaterialToneColor = ApplyHitPulse(
            ApplyState(DyePalette.BarkBrown, isDead),
            hitPulseStrength);
        var sashColor = ApplyHitPulse(
            ApplyState(DyePalette.SappanRed, isDead),
            hitPulseStrength);
        var adornmentAccentColor = ApplyHitPulse(
            ApplyState(DyePalette.GoldAccent, isDead),
            hitPulseStrength);

        DrawGroundBase(
            spriteBatch,
            pixel,
            layout.GroundRingBounds,
            displayedFactionColor);
        // movement-gait-animation design section 7: legs and feet draw
        // between the ground ring and the torso, so an unobstructed torso,
        // shield, head, and weapon still draw after them, on top.
        DrawLegs(spriteBatch, pixel, layout, torsoFillColor);
        DrawFeet(spriteBatch, pixel, layout, skinColor);
        DrawSecondaryEquipment(
            spriteBatch,
            pixel,
            layout,
            appearance.WeaponRole,
            appearance.WeaponBladeColor,
            appearance.WeaponLashingBandColor,
            isDead);
        if (torsoResolutionStep == VisualFallbackStep.DiagnosticPlaceholder)
        {
            DrawPlaceholder(spriteBatch, pixel, layout.PlaceholderBounds);
            if (log is not null)
            {
                PlaceholderDiagnostics.ReportFallback(
                    log,
                    TorsoPlaceholderCatalogId,
                    TorsoPlaceholderRequestedId);
            }
        }
        else
        {
            DrawTorso(
                spriteBatch,
                pixel,
                layout,
                torsoFillColor,
                accentColor);
        }

        DrawArmor(spriteBatch, pixel, layout, armorMaterialToneColor);
        DrawSash(spriteBatch, pixel, layout, sashColor);

        // After the torso: the shield is held in front of the body, so it
        // reads correctly only when it overlaps the torso rather than being
        // occluded by it.
        DrawShield(
            spriteBatch,
            pixel,
            layout,
            appearance.ShieldSkinId,
            appearance.ShieldFaceColor,
            isDead);
        DrawHead(spriteBatch, pixel, layout.HeadBounds, skinColor);

        if (layout.DetailTier != PawnDetailTier.Low)
        {
            DrawHeadTreatment(
                spriteBatch,
                pixel,
                layout,
                appearance.HeadTreatment,
                headTreatmentColor);
        }

        DrawAdornments(spriteBatch, pixel, layout, adornmentAccentColor);

        // Before the trail and the weapon, after the torso and the shield: the
        // arms leave the body and the weapon is held in the hand, so the
        // weapon has to draw over the hand that holds it rather than under it.
        DrawArms(spriteBatch, pixel, layout.Arms, skinColor);
        DrawSwingTrail(spriteBatch, pixel, layout.SwingTrail);
        DrawWeapon(
            spriteBatch,
            pixel,
            layout,
            appearance.WeaponRole,
            appearance.WeaponBladeColor,
            appearance.WeaponGripColor,
            isDead);

        if (state is PawnVisualState.Hovered or PawnVisualState.Selected)
        {
            DrawSelectionMark(
                spriteBatch,
                pixel,
                layout.SelectionBounds,
                state == PawnVisualState.Selected
                    ? Color.White
                    : HoverColor,
                state == PawnVisualState.Selected ? 2 : 1);
        }
        else if (isDead)
        {
            DrawDeadMark(
                spriteBatch,
                pixel,
                Rectangle.Union(
                    layout.TorsoBounds,
                    layout.HeadBounds));
        }

        // Coexistence decision, made explicitly rather than left to
        // if/else-if fallthrough nobody chose on purpose: the leader mark
        // draws whenever isLeader is true, independent of the
        // selection/hover/dead branch above. A spectator can select the
        // leader (selection ring is a screen-space ring around the whole
        // pawn; the leader mark sits above the head, so the two never
        // overlap), and the tick a leader falls, its view may still carry
        // IsLeader true until the next scan reassigns leadership — the dead
        // mark and the leader mark drawing together on that one tick is the
        // correct read: this warrior was leading and is now dead, not an
        // either/or.
        if (isLeader)
        {
            DrawLeaderMark(spriteBatch, pixel, layout.HeadBounds);
        }

        // The same coexistence decision as the leader mark above, taken for
        // the same reason and stated rather than inherited: the break-off
        // mark draws whenever brokeOffUnderPressure is true, independent of
        // both the selection/hover/dead branch and the leader branch. All
        // three can be true of one warrior on one tick — a selected leader
        // that the pressure interrupt has just pulled off its blow — and all
        // three are then correct to show. GetBreakOffMarkBounds reserves the
        // leader's slot whether or not the leader mark draws, so the three
        // never overlap; PawnRendererTests pins that over a real layout grid.
        if (TryGetBreakOffMarkBounds(
                brokeOffUnderPressure,
                layout.HeadBounds,
                out var breakOffMarkBounds))
        {
            spriteBatch.Draw(pixel, breakOffMarkBounds, BreakOffColor);
        }
    }

    private static void DrawGroundBase(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color factionColor)
    {
        spriteBatch.Draw(pixel, bounds, factionColor);

        var inner = Inset(bounds, 1);
        if (!inner.IsEmpty)
        {
            spriteBatch.Draw(pixel, inner, ShadowColor);
        }
    }

    /// <summary>
    /// Movement-gait-animation design section 7: the two leg rectangles
    /// between the torso and the ground ring, in the warrior's existing
    /// garment tone — this is moving geometry, not a new garment, so it
    /// reuses <paramref name="legColor"/> rather than reading a new color.
    /// </summary>
    /// <remarks>
    /// <see cref="PawnGeometry"/> already returns <see cref="Rectangle.Empty"/>
    /// for both leg rectangles at <see cref="PawnDetailTier.Low"/>, so this
    /// skips each rectangle exactly the way <see cref="DrawArmor"/> and
    /// <see cref="DrawSash"/> already skip an absent layer, rather than
    /// re-deriving the tier gate here.
    /// </remarks>
    private static void DrawLegs(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color legColor)
    {
        if (!layout.LeftLegBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.LeftLegBounds, legColor);
        }

        if (!layout.RightLegBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.RightLegBounds, legColor);
        }
    }

    /// <summary>
    /// Movement-gait-animation design section 7: the two foot rectangles,
    /// drawn bare in <paramref name="skinColor"/>. No footwear is documented
    /// for these warriors (design section 7, the footwear entry's
    /// <c>Documented</c> evidence tier), so this never reads a garment or
    /// footwear tone, and no sandal or boot variant is added here.
    /// </summary>
    /// <remarks>
    /// Empty at <see cref="PawnDetailTier.Low"/>, skipped the same way
    /// <see cref="DrawLegs"/> skips its own empty rectangles.
    /// </remarks>
    private static void DrawFeet(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color skinColor)
    {
        if (!layout.LeftFootBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.LeftFootBounds, skinColor);
        }

        if (!layout.RightFootBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.RightFootBounds, skinColor);
        }
    }

    private static void DrawTorso(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color torsoFillColor,
        Color accentColor)
    {
        DrawSteppedCapsule(
            spriteBatch,
            pixel,
            layout.TorsoBounds,
            OutlineColor);
        DrawSteppedCapsule(
            spriteBatch,
            pixel,
            Inset(layout.TorsoBounds, 1),
            torsoFillColor);

        if (layout.DetailTier == PawnDetailTier.High)
        {
            var beltHeight = Math.Max(
                1,
                (int)MathF.Round(layout.ApparentScale));
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    layout.TorsoBounds.Left + 1,
                    layout.TorsoBounds.Bottom -
                        Math.Max(2, layout.TorsoBounds.Height / 3),
                    Math.Max(1, layout.TorsoBounds.Width - 2),
                    beltHeight),
                accentColor);
        }
    }

    /// <summary>
    /// The step-4 diagnostic placeholder (visual-system-integration-
    /// design.md section 4, R-W6.4): a solid fill in
    /// <see cref="VisualFallbackResolver.PlaceholderColor"/> — fixed,
    /// conspicuous, never theme-derived — at the layout's already-computed
    /// <see cref="PawnLayout.PlaceholderBounds"/>. Drawn from the same 1x1
    /// pixel texture as every other pawn part, inside the caller's existing
    /// batch; no new draw-call class, no new <c>Begin</c>/<c>End</c>. Which
    /// element failed to resolve, and where the placeholder therefore goes,
    /// is decided entirely by the layout upstream — this method only fills
    /// the rectangle it is handed.
    /// </summary>
    private static void DrawPlaceholder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        spriteBatch.Draw(pixel, bounds, VisualFallbackResolver.PlaceholderColor);
    }

    /// <summary>
    /// Layer 4 in the composed-pawn order (integration design section 5):
    /// torso-capsule thickening and material color for worn armor.
    /// </summary>
    /// <remarks>
    /// VIS-023: <see cref="PawnLayout.ArmorBounds"/> is <see cref="Rectangle.Empty"/>
    /// for an unarmored pawn and at <see cref="PawnDetailTier.Low"/>
    /// (<c>PawnGeometry.CreateArmor</c> decides both), so this is a no-op in
    /// exactly those cases — the documented rollback ("no-op the three layer
    /// slots"). Otherwise a single fill in <paramref name="armorMaterialTone"/>,
    /// already routed through the caller's single hit-pulse blend point
    /// (<see cref="Draw"/>) before it reaches here.
    /// </remarks>
    private static void DrawArmor(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color armorMaterialTone)
    {
        if (layout.ArmorBounds.IsEmpty)
        {
            return;
        }

        spriteBatch.Draw(pixel, layout.ArmorBounds, armorMaterialTone);
    }

    /// <summary>
    /// Layer 5 in the composed-pawn order (integration design section 5):
    /// the sash line.
    /// </summary>
    /// <remarks>
    /// VIS-023: <see cref="PawnLayout.SashBounds"/> is <see cref="Rectangle.Empty"/>
    /// whenever the pawn has no sash or is at <see cref="PawnDetailTier.Low"/>
    /// (<c>PawnGeometry.CreateSash</c> decides both), so this is a no-op in
    /// exactly those cases. Otherwise a single fill in
    /// <paramref name="sashColor"/>, already hit-pulse blended by the caller.
    /// </remarks>
    private static void DrawSash(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color sashColor)
    {
        if (layout.SashBounds.IsEmpty)
        {
            return;
        }

        spriteBatch.Draw(pixel, layout.SashBounds, sashColor);
    }

    private static void DrawHead(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color skinColor)
    {
        DrawDisk(spriteBatch, pixel, bounds, OutlineColor);
        DrawDisk(spriteBatch, pixel, Inset(bounds, 1), skinColor);
    }

    private static void DrawHeadTreatment(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnHeadTreatment treatment,
        Color color)
    {
        var bounds = layout.HeadTreatmentBounds;

        switch (treatment)
        {
            case PawnHeadTreatment.CroppedHair:
                spriteBatch.Draw(pixel, bounds, color);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        bounds.Left,
                        bounds.Bottom,
                        Math.Max(1, bounds.Width / 4),
                        Math.Max(1, bounds.Height / 2)),
                    color);
                break;
            case PawnHeadTreatment.Headcloth:
                spriteBatch.Draw(pixel, bounds, color);
                var tailWidth = Math.Max(
                    1,
                    (int)MathF.Round(layout.ApparentScale));
                var tailTop = Math.Clamp(
                    bounds.Bottom - 1,
                    layout.HeadBounds.Top,
                    layout.HeadBounds.Bottom - 1);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        layout.HeadBounds.Right - tailWidth,
                        tailTop,
                        tailWidth,
                        layout.HeadBounds.Bottom - tailTop),
                    color);
                break;
            case PawnHeadTreatment.WrappedCloth:
                spriteBatch.Draw(pixel, bounds, color);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + Math.Max(1, bounds.Height / 2),
                        bounds.Width,
                        1),
                    color);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(treatment),
                    treatment,
                    null);
        }
    }

    /// <summary>
    /// Layer 9 in the composed-pawn order (integration design section 5):
    /// adornment accents (gold pixel, accent marks), High tier only.
    /// </summary>
    /// <remarks>
    /// VIS-023: <see cref="PawnLayout.AdornmentAccentPrimaryBounds"/> and
    /// <see cref="PawnLayout.AdornmentAccentSecondaryBounds"/> are each
    /// <see cref="Rectangle.Empty"/> below <see cref="PawnDetailTier.High"/>
    /// or when the pawn's recipe names fewer accents than that slot
    /// (<c>PawnGeometry.CreateAdornmentAccents</c> decides both), so an
    /// unaccented pawn draws neither and this is a no-op. Both share
    /// <paramref name="accentColor"/> — the gold pixel every renderable
    /// accent option (C3, I4, I5, E2) reduces to, already hit-pulse blended
    /// by the caller.
    /// </remarks>
    private static void DrawAdornments(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color accentColor)
    {
        if (!layout.AdornmentAccentPrimaryBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.AdornmentAccentPrimaryBounds, accentColor);
        }

        if (!layout.AdornmentAccentSecondaryBounds.IsEmpty)
        {
            spriteBatch.Draw(pixel, layout.AdornmentAccentSecondaryBounds, accentColor);
        }
    }

    /// <param name="weaponBladeColor">
    /// The resolved weapon tint's blade/head color (VIS-010/VIS-011,
    /// <c>Presentation.Catalogs.WeaponVisualCatalog</c>). Only the Wasay's
    /// axe head reads it — the Itak's off-hand piece keeps drawing the
    /// fixed <see cref="CharredWood"/> tone, unchanged.
    /// </param>
    /// <param name="weaponLashingBandColor">
    /// The resolved weapon tint's rattan lashing band accent color
    /// (weapon-visuals-design.md, R-W1.5), or <see langword="null"/> for
    /// every tint that carries none. Only ever non-null for the Wasay.
    /// </param>
    private static void DrawSecondaryEquipment(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnWeaponRole role,
        Color weaponBladeColor,
        Color? weaponLashingBandColor,
        bool isDead)
    {
        if (layout.SecondaryEquipmentBounds.IsEmpty)
        {
            return;
        }

        var scale = layout.ApparentScale;

        // The axe head is what separates the Wasay's silhouette from a thin
        // blade, so unlike the Itak's off-hand piece it survives the low
        // detail tier. PawnGeometry decides that; this only draws it.
        //
        // VIS-011: the head fills with the resolved weapon tint's blade
        // color instead of the fixed Iron constant, so the Wasay's three
        // tints (ochreHaft/charredHaft/lashedWorn) read on the head exactly
        // as DrawWeapon's blade line reads for the other three weapons. The
        // lashing band (lashedWorn only) draws over it.
        if (role == PawnWeaponRole.Wasay)
        {
            spriteBatch.Draw(
                pixel,
                layout.SecondaryEquipmentBounds,
                ApplyState(weaponBladeColor, isDead));
            DrawLashingBand(
                spriteBatch,
                pixel,
                layout,
                weaponLashingBandColor,
                isDead);
            return;
        }

        if (layout.DetailTier == PawnDetailTier.Low ||
            role != PawnWeaponRole.Itak)
        {
            return;
        }

        DrawLine(
            spriteBatch,
            pixel,
            layout.FootAnchor + new Vector2(-2f * scale, -4f * scale),
            layout.FootAnchor + new Vector2(-6f * scale, -11f * scale),
            ApplyState(CharredWood, isDead),
            MathF.Max(2f, 2f * scale));
    }

    /// <summary>
    /// The Wasay <c>lashedWorn</c> tint's rattan lashing band accent
    /// (weapon-visuals-design.md, R-W1.5): one short band across the base
    /// of the head rectangle — the head-haft junction — in the tint's
    /// resolved lashing band color. Drawn strictly inside
    /// <see cref="PawnLayout.SecondaryEquipmentBounds"/>, which
    /// <see cref="PawnGeometry"/> already computes unconditionally for the
    /// Wasay, so it can never grow the pawn's bounds beyond what
    /// <see cref="GetBounds"/> already reports (R-X.5) — the same
    /// "footprint unchanged, only fill varies" discipline VIS-013's shield
    /// skin uses. Absent for every other tint
    /// (<paramref name="lashingBandColor"/> is <see langword="null"/>) and
    /// absent below Medium detail tier through <see cref="DetailTierGate"/>,
    /// so it reads as a band rather than as damage or noise at distance
    /// (R-X.2).
    /// </summary>
    private static void DrawLashingBand(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color? lashingBandColor,
        bool isDead)
    {
        if (lashingBandColor is null ||
            !DetailTierGate.ShouldDraw(layout.ApparentScale, VisualDetailTier.Medium))
        {
            return;
        }

        var bounds = layout.SecondaryEquipmentBounds;
        if (bounds.Height < 3)
        {
            return;
        }

        var bandHeight = Math.Max(1, bounds.Height / 3);
        var band = new Rectangle(
            bounds.Left,
            bounds.Bottom - bandHeight,
            bounds.Width,
            bandHeight);

        spriteBatch.Draw(pixel, band, ApplyState(lashingBandColor.Value, isDead));
    }

    /// <summary>
    /// PROVISIONAL. How far the High-tier edge-tone step (shield-visuals-
    /// design.md, "a slightly darker rim suggesting thickness") darkens the
    /// resolved face tone, lerped toward <see cref="OutlineColor"/>. Tone
    /// only — never a distinct hue, so it reads as thickness rather than a
    /// second material.
    /// </summary>
    private const float ShieldEdgeToneDarkenFactor = 0.35f;

    /// <summary>
    /// The shield block beside the torso. Drawn at every detail tier: a
    /// shield changes what the warrior is, and dropping it at distance would
    /// erase the solo-versus-shielded distinction exactly when a spectator is
    /// watching whole formations.
    /// </summary>
    /// <param name="shieldSkinId">
    /// The resolved skin's stable catalog identifier (VIS-014,
    /// <c>Presentation.Catalogs.ShieldVisualCatalog</c>, shield-visuals-
    /// design.md, OD-10): selects which per-skin detail element draws at
    /// Medium tier — S3 <c>boxerCagayan</c>'s outline curvature alongside
    /// its kept vertical seam (OD-W2-c), S5 <c>visayanKalasag</c>'s
    /// horizontal rattan-binding accent in place of the vertical seam, and
    /// every other skin's plain vertical seam. Never affects footprint,
    /// which <see cref="PawnGeometry"/> alone decides.
    /// </param>
    /// <param name="shieldFaceColor">
    /// The resolved skin's face tone (VIS-013,
    /// <c>Presentation.Catalogs.ShieldVisualCatalog</c>, shield-visuals-
    /// design.md). Footprint is unchanged by the skin — only this fill color
    /// and the detail elements above vary.
    /// </param>
    /// <remarks>
    /// VIS-015 (S12 active posture): every element below is drawn through
    /// <see cref="DrawRotatedBlock"/> about one shared pivot —
    /// <c>bounds.Center</c> — instead of the plain axis-aligned
    /// <c>SpriteBatch.Draw(Texture2D, Rectangle, Color)</c> the block used
    /// before. <see cref="PawnLayout.ShieldPostureRotationRadians"/> is the
    /// same fixed angle for every call, so the face, seam/accent, and
    /// edge-tone elements stay rigid together as one angled block rather
    /// than a rotated face with unrotated details floating over it.
    /// </remarks>
    private static void DrawShield(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        string shieldSkinId,
        Color shieldFaceColor,
        bool isDead)
    {
        if (layout.ShieldBounds.IsEmpty)
        {
            return;
        }

        var bounds = layout.ShieldBounds;
        var pivot = new Vector2(bounds.Center.X, bounds.Center.Y);
        var rotation = layout.ShieldPostureRotationRadians;
        var faceColor = ApplyState(shieldFaceColor, isDead);
        var isBoxerCagayan =
            shieldSkinId == ShieldVisualCatalog.BoxerCagayan.Catalog.Id;
        var isVisayanKalasag =
            shieldSkinId == ShieldVisualCatalog.VisayanKalasag.Catalog.Id;

        if (isBoxerCagayan)
        {
            // S3: the Boxer Codex's gentle curve, one to two layout pixels
            // of top/bottom edge inset. Falls back to the plain rectangle
            // whenever the block is too narrow to show it, which is exactly
            // how it degrades to the straight block at Low tier.
            DrawCurvedShieldFace(
                spriteBatch,
                pixel,
                bounds,
                pivot,
                rotation,
                ShieldCurvatureInsetPixels(layout.DetailTier),
                faceColor);
        }
        else
        {
            DrawRotatedBlock(spriteBatch, pixel, bounds, pivot, rotation, faceColor);
        }

        if (DetailTierGate.ShouldDraw(layout.ApparentScale, VisualDetailTier.Medium))
        {
            if (isVisayanKalasag)
            {
                // S5: one horizontal rattan-binding accent line across the
                // face, replacing the vertical seam on this skin only.
                if (bounds.Height >= 3)
                {
                    DrawRotatedBlock(
                        spriteBatch,
                        pixel,
                        new Rectangle(
                            bounds.Left + 1,
                            bounds.Center.Y,
                            Math.Max(1, bounds.Width - 2),
                            1),
                        pivot,
                        rotation,
                        ApplyState(WeaponVisualCatalog.RattanLashingTone, isDead));
                }
            }
            else if (bounds.Width >= 3)
            {
                // A lighter vertical seam so the block reads as a face
                // rather than a silhouette hole. Kept for S3 alongside its
                // curvature (OD-W2-c, default retained).
                DrawRotatedBlock(
                    spriteBatch,
                    pixel,
                    new Rectangle(
                        bounds.Center.X,
                        bounds.Top + 1,
                        1,
                        Math.Max(1, bounds.Height - 2)),
                    pivot,
                    rotation,
                    ApplyState(Iron, isDead));
            }
        }

        if (layout.DetailTier == PawnDetailTier.High && bounds.Width >= 2)
        {
            // Every skin: a one-pixel darker-tone step on the long
            // (vertical) edges, suggesting thickness (shield-visuals-
            // design.md geometry-per-tier table).
            var edgeToneColor = ApplyState(
                Color.Lerp(shieldFaceColor, OutlineColor, ShieldEdgeToneDarkenFactor),
                isDead);
            DrawRotatedBlock(
                spriteBatch,
                pixel,
                new Rectangle(bounds.Left, bounds.Top, 1, bounds.Height),
                pivot,
                rotation,
                edgeToneColor);
            DrawRotatedBlock(
                spriteBatch,
                pixel,
                new Rectangle(bounds.Right - 1, bounds.Top, 1, bounds.Height),
                pivot,
                rotation,
                edgeToneColor);
        }
    }

    /// <summary>
    /// S3 <c>boxerCagayan</c>'s curvature inset in whole layout pixels: none
    /// at Low tier (the straight block), one at Medium, two at High — the
    /// plan's own "one-to-two-pixel outline curvature" (shield-visuals-
    /// design.md).
    /// </summary>
    private static int ShieldCurvatureInsetPixels(PawnDetailTier detailTier) =>
        detailTier switch
        {
            PawnDetailTier.Low => 0,
            PawnDetailTier.Medium => 1,
            _ => 2,
        };

    /// <summary>
    /// Draws <paramref name="bounds"/> with its top and bottom edges inset by
    /// <paramref name="insetPixels"/> pixels on each side, so the long
    /// (vertical) edges bow gently inward at the ends — the Boxer Codex's
    /// gentle curve, expressed entirely in whole layout pixels. Falls back to
    /// the plain rectangle whenever there is no room for the inset, which is
    /// exactly how the curvature degrades to the straight block at Low tier
    /// and on the smallest blocks at any tier.
    /// </summary>
    /// <param name="pivot">
    /// The shared point (VIS-015, S12) every sub-rectangle of the curved
    /// face rotates about, so the cap-and-middle assembly stays rigid
    /// instead of rotating each part about its own center.
    /// </param>
    /// <param name="rotationRadians">
    /// The fixed active-posture rotation (VIS-015);
    /// <see cref="PawnLayout.ShieldPostureRotationRadians"/> at every call
    /// site today.
    /// </param>
    private static void DrawCurvedShieldFace(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Vector2 pivot,
        float rotationRadians,
        int insetPixels,
        Color color)
    {
        var insetWidth = bounds.Width - (insetPixels * 2);
        if (insetPixels <= 0 || insetWidth <= 0)
        {
            DrawRotatedBlock(spriteBatch, pixel, bounds, pivot, rotationRadians, color);
            return;
        }

        var capHeight = Math.Clamp(insetPixels + 1, 1, Math.Max(1, bounds.Height / 3));
        var middleHeight = Math.Max(1, bounds.Height - (capHeight * 2));
        var middle = new Rectangle(
            bounds.Left,
            bounds.Top + capHeight,
            bounds.Width,
            middleHeight);
        var top = new Rectangle(
            bounds.Left + insetPixels,
            bounds.Top,
            insetWidth,
            capHeight);
        var bottom = new Rectangle(
            bounds.Left + insetPixels,
            Math.Max(bounds.Top, bounds.Bottom - capHeight),
            insetWidth,
            capHeight);

        DrawRotatedBlock(spriteBatch, pixel, middle, pivot, rotationRadians, color);
        DrawRotatedBlock(spriteBatch, pixel, top, pivot, rotationRadians, color);
        DrawRotatedBlock(spriteBatch, pixel, bottom, pivot, rotationRadians, color);
    }

    /// <summary>
    /// Draws <paramref name="localBounds"/> — a rectangle expressed in the
    /// shield block's own unrotated local layout, computed the same way
    /// every other <see cref="DrawShield"/> sub-element already is — rotated
    /// by <paramref name="rotationRadians"/> about <paramref name="pivot"/>
    /// rather than about its own center, so a group of sub-rectangles
    /// sharing one pivot stays rigid together (VIS-015, S12 active posture).
    /// A rotation of zero reproduces the plain axis-aligned draw this
    /// replaces, up to the sub-pixel rounding the rotation overload's
    /// floating-point origin introduces on an odd width or height — never
    /// visible in practice, since the posture rotation is never zero at any
    /// call site today.
    /// </summary>
    private static void DrawRotatedBlock(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle localBounds,
        Vector2 pivot,
        float rotationRadians,
        Color color)
    {
        var localCenter =
            new Vector2(localBounds.Center.X, localBounds.Center.Y) - pivot;
        var cosine = MathF.Cos(rotationRadians);
        var sine = MathF.Sin(rotationRadians);
        var rotatedCenter = new Vector2(
            (localCenter.X * cosine) - (localCenter.Y * sine),
            (localCenter.X * sine) + (localCenter.Y * cosine));

        spriteBatch.Draw(
            pixel,
            pivot + rotatedCenter,
            sourceRectangle: null,
            color,
            rotationRadians,
            new Vector2(0.5f, 0.5f),
            new Vector2(localBounds.Width, localBounds.Height),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    /// <summary>
    /// The four arm strokes an actively attacking warrior draws, in the same
    /// bare skin tone the feet already use. At most four quads, and none at
    /// all when the layout carries no arms — which is every pawn that is not
    /// mid-contact, and every pawn at the low detail tier.
    /// </summary>
    private static void DrawArms(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        ArmLayout arms,
        Color skinColor)
    {
        if (arms.IsEmpty)
        {
            return;
        }

        DrawArmSegment(spriteBatch, pixel, arms.WeaponUpperArm, arms.Thickness, skinColor);
        DrawArmSegment(spriteBatch, pixel, arms.WeaponForearm, arms.Thickness, skinColor);
        DrawArmSegment(spriteBatch, pixel, arms.SupportUpperArm, arms.Thickness, skinColor);
        DrawArmSegment(spriteBatch, pixel, arms.SupportForearm, arms.Thickness, skinColor);
    }

    private static void DrawArmSegment(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        ArmSegment segment,
        float thickness,
        Color skinColor)
    {
        if (segment.IsEmpty)
        {
            return;
        }

        DrawLine(
            spriteBatch,
            pixel,
            segment.From,
            segment.To,
            skinColor,
            thickness);
    }

    /// <summary>
    /// Strokes the arc the layout already computed. There is no trail formula
    /// here: the pivot, radius, and both angles arrive from
    /// <see cref="PawnGeometry"/>, and this method only walks between them.
    /// </summary>
    private static void DrawSwingTrail(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SwingTrail trail)
    {
        if (trail.IsEmpty)
        {
            return;
        }

        var previous = PointOnArc(trail, 0f);
        for (var segment = 1; segment <= SwingTrailSegments; segment++)
        {
            var along = segment / (float)SwingTrailSegments;
            var current = PointOnArc(trail, along);
            DrawLine(
                spriteBatch,
                pixel,
                previous,
                current,
                SwingTrailColor * Math.Clamp(
                    trail.Strength * trail.Emphasis * along * 0.55f,
                    0f,
                    1f),
                trail.Thickness);
            previous = current;
        }
    }

    private static Vector2 PointOnArc(SwingTrail trail, float along)
    {
        var angle = trail.StartAngleRadians +
            ((trail.EndAngleRadians - trail.StartAngleRadians) * along);
        return trail.Pivot + new Vector2(
            MathF.Cos(angle) * trail.Radius,
            MathF.Sin(angle) * trail.Radius);
    }

    // VIS-010/VIS-011: every weapon now draws its resolved tint
    // (Presentation.Catalogs.WeaponVisualCatalog, weapon-visuals-design.md)
    // instead of a shared fixed grip/blade tone. Geometry (gripEnd,
    // widthMultiplier) is untouched per weapon — only color differs, which
    // is exactly the R-X.3 classification-invariance and R-X.12
    // false-cause guarantees the design requires: a spectator must never
    // read a mechanical difference from a tint.
    private static void DrawWeapon(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnWeaponRole role,
        Color weaponBladeColor,
        Color weaponGripColor,
        bool isDead)
    {
        var gripColor = ApplyState(weaponGripColor, isDead);
        var bladeColor = ApplyState(weaponBladeColor, isDead);
        var ironHighlight = ApplyState(IronHighlight, isDead);

        switch (role)
        {
            case PawnWeaponRole.Itak:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.30f,
                    widthMultiplier: 2.1f);
                break;
            case PawnWeaponRole.Kampilan:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.22f,
                    widthMultiplier: 2.45f);
                break;
            case PawnWeaponRole.Wasay:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.28f,
                    widthMultiplier: 2.9f);
                break;
            case PawnWeaponRole.Kalis:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.16f,
                    widthMultiplier: 1.5f);
                break;
            case PawnWeaponRole.Bangkaw:
                // PROVISIONAL RECONSTRUCTION: a thrown/thrusting spear reads
                // as a long, thin haft with a short head, the opposite ratio
                // of the melee blades above (RU-42). gripEnd/widthMultiplier
                // are gameplay-legibility tuning, not a historical
                // measurement.
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.72f,
                    widthMultiplier: 1.2f);
                break;
            case PawnWeaponRole.Busog:
                // PROVISIONAL RECONSTRUCTION: the stave itself is thin and
                // near-vertical (RangedGeometry's Busog pose); the bowstring
                // is the readable content, drawn separately so DrawTension
                // (RU-42) has something to bend. gripEnd/widthMultiplier
                // tuning, not a historical measurement.
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.5f,
                    widthMultiplier: 0.9f);
                DrawBowstring(spriteBatch, pixel, layout, gripColor);
                break;
            case PawnWeaponRole.Arquebus:
                // PROVISIONAL RECONSTRUCTION: a long, uniformly thick barrel
                // read as one continuous line rather than a grip/blade
                // taper (RU-42). gripEnd/widthMultiplier tuning, not a
                // historical measurement.
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    gripColor,
                    bladeColor,
                    ironHighlight,
                    gripEnd: 0.35f,
                    widthMultiplier: 1.3f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }
    }

    private static void DrawBlade(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color gripColor,
        Color bladeColor,
        Color highlightColor,
        float gripEnd,
        float widthMultiplier)
    {
        var bladeStart = Vector2.Lerp(
            layout.WeaponStart,
            layout.WeaponEnd,
            gripEnd);
        DrawLine(
            spriteBatch,
            pixel,
            layout.WeaponStart,
            bladeStart,
            gripColor,
            MathF.Max(2f, layout.WeaponThickness * 0.85f));
        DrawLine(
            spriteBatch,
            pixel,
            bladeStart,
            layout.WeaponEnd,
            bladeColor,
            MathF.Max(2f, layout.WeaponThickness * widthMultiplier));
        DrawLine(
            spriteBatch,
            pixel,
            bladeStart,
            layout.WeaponEnd,
            highlightColor,
            MathF.Max(1f, layout.WeaponThickness * 0.55f));
    }

    /// <summary>
    /// The Busog bowstring's two half-segments, from each stave tip to a
    /// midpoint pulled toward the archer by <see cref="PawnLayout.RangedDrawTension"/>
    /// (RU-42). At zero tension the midpoint sits on the stave itself, so the
    /// string reads as a straight line flush against it; at full tension the
    /// midpoint is pulled back roughly a third of the stave's own length,
    /// reading as a drawn bow. Pure geometry so <c>PawnRendererTests</c> can
    /// assert the string actually moves with tension without a graphics
    /// device.
    /// </summary>
    private const float BowstringPullFraction = 0.35f;

    public static (Vector2 StaveTip, Vector2 Midpoint, Vector2 StaveBase) GetBowstringLine(
        PawnLayout layout)
    {
        var stave = layout.WeaponEnd - layout.WeaponStart;
        var perpendicular = new Vector2(-stave.Y, stave.X);
        var pull = perpendicular * (BowstringPullFraction * layout.RangedDrawTension);
        var midpoint = Vector2.Lerp(layout.WeaponStart, layout.WeaponEnd, 0.5f) + pull;
        return (layout.WeaponEnd, midpoint, layout.WeaponStart);
    }

    private static void DrawBowstring(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color stringColor)
    {
        var (staveTip, midpoint, staveBase) = GetBowstringLine(layout);
        var thickness = MathF.Max(1f, layout.WeaponThickness * 0.4f);
        DrawLine(spriteBatch, pixel, staveTip, midpoint, stringColor, thickness);
        DrawLine(spriteBatch, pixel, midpoint, staveBase, stringColor, thickness);
    }

    private static void DrawSteppedCapsule(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var step = Math.Min(
            Math.Max(1, bounds.Width / 4),
            Math.Max(1, bounds.Height / 4));
        var middle = new Rectangle(
            bounds.Left,
            bounds.Top + step,
            bounds.Width,
            Math.Max(1, bounds.Height - (step * 2)));
        var capWidth = Math.Max(1, bounds.Width - (step * 2));
        var top = new Rectangle(
            bounds.Left + step,
            bounds.Top,
            capWidth,
            Math.Max(1, step));
        var bottom = new Rectangle(
            bounds.Left + step,
            Math.Max(bounds.Top, bounds.Bottom - step),
            capWidth,
            Math.Max(1, step));

        spriteBatch.Draw(pixel, middle, color);
        spriteBatch.Draw(pixel, top, color);
        spriteBatch.Draw(pixel, bottom, color);
    }

    private static void DrawDisk(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var bandHeight = Math.Max(1, bounds.Height / 4);
        var inset = Math.Max(1, bounds.Width / 5);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Top + bandHeight,
                bounds.Width,
                Math.Max(1, bounds.Height - (bandHeight * 2))),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left + inset,
                bounds.Top,
                Math.Max(1, bounds.Width - (inset * 2)),
                bandHeight),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left + inset,
                bounds.Bottom - bandHeight,
                Math.Max(1, bounds.Width - (inset * 2)),
                bandHeight),
            color);
    }

    /// <summary>
    /// The arm length of one selection-mark corner bracket, in pixels. Pure
    /// placement math, extracted so <c>PawnRendererTests</c> can reason about
    /// where the selection ring actually puts pixels without a graphics
    /// device. <see cref="DrawSelectionMark"/> is its only production caller,
    /// so the drawn ring and the tested rectangles cannot drift apart.
    /// </summary>
    public static int GetSelectionMarkCornerLength(Rectangle selectionBounds) =>
        Math.Clamp(
            Math.Min(selectionBounds.Width, selectionBounds.Height) / 4,
            4,
            10);

    /// <summary>
    /// The axis-aligned bounding box of one of the four L-shaped corner
    /// brackets <see cref="DrawSelectionMark"/> draws, identified by which
    /// corner of <paramref name="selectionBounds"/> it hangs off. The
    /// bracket's two quads both lie inside this square whatever thickness is
    /// passed, because thickness is 1 or 2 and the arm length is at least 4,
    /// which makes an intersection test against this square a conservative
    /// stand-in for a test against the drawn pixels: anything that misses the
    /// square misses the bracket.
    /// </summary>
    public static Rectangle GetSelectionMarkCornerBounds(
        Rectangle selectionBounds,
        bool right,
        bool bottom)
    {
        var cornerLength = GetSelectionMarkCornerLength(selectionBounds);
        return new Rectangle(
            right ? selectionBounds.Right - cornerLength : selectionBounds.Left,
            bottom ? selectionBounds.Bottom - cornerLength : selectionBounds.Top,
            cornerLength,
            cornerLength);
    }

    private static void DrawSelectionMark(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness)
    {
        DrawCorner(right: false, bottom: false);
        DrawCorner(right: true, bottom: false);
        DrawCorner(right: false, bottom: true);
        DrawCorner(right: true, bottom: true);

        void DrawCorner(bool right, bool bottom)
        {
            var corner = GetSelectionMarkCornerBounds(bounds, right, bottom);
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    corner.Left,
                    bottom ? corner.Bottom - thickness : corner.Top,
                    corner.Width,
                    thickness),
                color);
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    right ? corner.Right - thickness : corner.Left,
                    corner.Top,
                    thickness,
                    corner.Height),
                color);
        }
    }

    /// <summary>
    /// An upward chevron hovering above the pawn's head, in
    /// <see cref="LeaderColor"/> — the client-visible leader marker (leader
    /// rank plan L4). A shape, not merely a tinted band, so a leader's
    /// silhouette above the head differs from a non-leader's at a glance
    /// (leader-character design section 4.3): a base band plus two rising
    /// arms, three quads via <see cref="GetLeaderMarkGlyph"/>. Modeled
    /// directly on <see cref="DrawSelectionMark"/> and
    /// <see cref="DrawDeadMark"/>: hand-drawn primitives over
    /// <paramref name="headBounds"/>, an existing <c>PawnLayout</c> bounds
    /// value, using the shared <paramref name="pixel"/> texture. No new
    /// <c>PawnGeometry</c> entry, matching both of those.
    /// </summary>
    private static void DrawLeaderMark(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle headBounds)
    {
        var glyph = GetLeaderMarkGlyph(headBounds);
        spriteBatch.Draw(pixel, glyph.Base, LeaderColor);
        DrawLine(spriteBatch, pixel, glyph.LeftArmStart, glyph.LeftArmEnd, LeaderColor, 1f);
        DrawLine(spriteBatch, pixel, glyph.RightArmStart, glyph.RightArmEnd, LeaderColor, 1f);
    }

    /// <summary>
    /// Where the leader slot sits, given the head it hangs over. Pure
    /// placement math, and the rectangle the break-off mark's own slot is
    /// derived from, so that <c>PawnRendererTests</c> can prove the two never
    /// overlap.
    /// </summary>
    /// <remarks>
    /// Widened for leader rank plan L4: the slot spans the full head width
    /// (was <c>headBounds.Width / 2</c>) and stands <c>max(2, headHeight /
    /// 4)</c> tall (was <c>max(1, headHeight / 6)</c>), so the chevron
    /// <see cref="DrawLeaderMark"/> draws inside it has room to carry vertical
    /// extent instead of reading as a flat, colour-only band. The one-pixel
    /// head in <c>PawnRendererTests.HeadBoundsGrid</c> is why the height floor
    /// is 2 rather than 1: at <c>headHeight / 4 == 0</c> the slot must still
    /// be tall enough to hold a base band and a rising arm above it.
    /// </remarks>
    public static Rectangle GetLeaderMarkBounds(Rectangle headBounds)
    {
        var markWidth = headBounds.Width;
        var markHeight = Math.Max(2, headBounds.Height / 4);
        var gap = GetMarkGap(headBounds);
        return new Rectangle(
            headBounds.Center.X - (markWidth / 2),
            headBounds.Top - gap - markHeight,
            markWidth,
            markHeight);
    }

    /// <summary>
    /// The three quads <see cref="DrawLeaderMark"/> submits, expressed as
    /// pure geometry over value types only — no <c>GraphicsDevice</c>, no
    /// <c>SpriteBatch</c> — so the shape can be asserted without a window.
    /// A base band spanning the full width of <see cref="GetLeaderMarkBounds"/>'s
    /// slot, plus two arms rising from the band's ends to an apex at the
    /// slot's top centre: an upward chevron, abstract HUD glyph rather than a
    /// depicted object (leader-character design section 4.3, section 6.3).
    /// Every point stays within the slot rectangle, so widening the chevron
    /// never widens what a collision test has to reason about.
    /// </summary>
    public static LeaderMarkGlyph GetLeaderMarkGlyph(Rectangle headBounds)
    {
        var slot = GetLeaderMarkBounds(headBounds);
        var baseHeight = Math.Max(1, slot.Height / 3);
        var baseBand = new Rectangle(slot.Left, slot.Bottom - baseHeight, slot.Width, baseHeight);
        var apex = new Vector2(slot.Center.X, slot.Top);
        var leftArmStart = new Vector2(slot.Left, baseBand.Top);
        var rightArmStart = new Vector2(slot.Right - 1, baseBand.Top);
        return new LeaderMarkGlyph(baseBand, leftArmStart, apex, rightArmStart, apex);
    }

    /// <summary>
    /// Where the break-off band sits, given the head it hangs over — channel
    /// 1 of the spectator explanation in pressure-interrupt design section 3,
    /// question 8.
    /// </summary>
    /// <remarks>
    /// It occupies the slot immediately above the leader band's, separated by
    /// the same gap that separates the leader band from the head, and it does
    /// so <em>whether or not this warrior is a leader</em>. Reserving the
    /// leader's slot unconditionally is what makes non-collision structural
    /// rather than circumstantial: the break-off band's rectangle does not
    /// depend on <c>isLeader</c>, so a warrior who is leading, selected, and
    /// breaking off on the same tick shows three marks that cannot overlap by
    /// construction. The cost is a one-slot gap under the band on a
    /// non-leader, which at the head sizes this renderer produces is between
    /// two and a few pixels.
    ///
    /// It is deliberately wider than the leader band and a different color,
    /// because sitting one slot apart is not on its own enough to tell two
    /// thin horizontal bands apart at typical camera zoom.
    /// </remarks>
    public static Rectangle GetBreakOffMarkBounds(Rectangle headBounds)
    {
        var leaderBounds = GetLeaderMarkBounds(headBounds);
        var markWidth = Math.Max(3, (headBounds.Width * 2) / 3);
        var markHeight = Math.Max(1, headBounds.Height / 6);
        var gap = GetMarkGap(headBounds);
        return new Rectangle(
            headBounds.Center.X - (markWidth / 2),
            leaderBounds.Top - gap - markHeight,
            markWidth,
            markHeight);
    }

    /// <summary>
    /// The vertical separation between one above-head mark slot and the next,
    /// and between the lowest slot and the head itself. Shared by
    /// <see cref="GetLeaderMarkBounds"/> and
    /// <see cref="GetBreakOffMarkBounds"/> so the stack cannot close up on one
    /// side only; the expression is the one the leader mark has always used.
    /// </summary>
    private static int GetMarkGap(Rectangle headBounds) =>
        Math.Max(1, headBounds.Height / 8);

    /// <summary>
    /// Whether a warrior carrying <paramref name="brokeOffUnderPressure"/>
    /// draws a break-off mark at all, and where. The gate and the placement
    /// live in one pure function, and <see cref="DrawLayout"/> holds no copy
    /// of either, so "a view flag of <see langword="false"/> puts no mark on
    /// screen" is a property a test can assert without a graphics device
    /// rather than a property a reader has to take on trust.
    /// </summary>
    public static bool TryGetBreakOffMarkBounds(
        bool brokeOffUnderPressure,
        Rectangle headBounds,
        out Rectangle bounds)
    {
        if (!brokeOffUnderPressure)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = GetBreakOffMarkBounds(headBounds);
        return true;
    }

    private static void DrawDeadMark(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds)
    {
        DrawLine(
            spriteBatch,
            pixel,
            new Vector2(bounds.Left, bounds.Top),
            new Vector2(bounds.Right, bounds.Bottom),
            DeadColor,
            2f);
        DrawLine(
            spriteBatch,
            pixel,
            new Vector2(bounds.Right, bounds.Top),
            new Vector2(bounds.Left, bounds.Bottom),
            DeadColor,
            2f);
    }

    private static void DrawLine(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 start,
        Vector2 end,
        Color color,
        float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= float.Epsilon)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            start,
            sourceRectangle: null,
            color,
            MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f),
            new Vector2(length, MathF.Max(1f, thickness)),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    private static Rectangle Inset(Rectangle bounds, int amount)
    {
        var width = bounds.Width - (amount * 2);
        var height = bounds.Height - (amount * 2);
        return width <= 0 || height <= 0
            ? Rectangle.Empty
            : new Rectangle(
                bounds.X + amount,
                bounds.Y + amount,
                width,
                height);
    }

    private static Color ApplyState(Color color, bool isDead) =>
        isDead ? Color.Lerp(color, DeadColor, 0.68f) : color;

    private static Color ApplyHitPulse(Color color, float strength) =>
        Color.Lerp(color, HitPulseColor, strength * 0.55f);
}
