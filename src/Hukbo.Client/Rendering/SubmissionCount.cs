using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Renderer-agnostic Tier 1 quad-counting seam (VIS-034, integration design
/// sections 8 and 11, amendment A-1). Originally specified as a
/// submission-counting seam; amendment A-1 (2026-07-28, user-approved)
/// supersedes that unit with Tier 1 "quads" — logical filled rectangles or
/// stroked line segments the presentation layer asks to draw, identical
/// under either an immediate-mode or an instanced backend — so this file's
/// pins are QUAD counts, not <c>SpriteBatch.Draw</c> submission counts. Every
/// counting method here mirrors the corresponding renderer's exact draw-order
/// conditionals: given the same layout, appearance, and state a renderer
/// draws from, the count returned here equals the number of quads that
/// renderer actually emits. Pure functions over already-built layout values;
/// never called from the live render loop directly — <c>ArenaGame</c>'s
/// render-probe opt-in is the sole caller, evaluating these once per frame
/// to populate <see cref="RenderProbeSample.Metrics"/> (VIS-035R, retrofit
/// of the hookup point this file originally left open). Counting code
/// belongs only here — never in a renderer's per-frame path (the task's "no
/// counting code in the per-frame render path" prohibition).
/// </summary>
internal static class PawnQuadCount
{
    /// <summary>
    /// The number of stroked segments <c>PawnRenderer.DrawSwingTrail</c>
    /// walks one arc into. Kept in step with that renderer's own private
    /// <c>SwingTrailSegments</c> constant by <c>PawnQuadCountTests</c>
    /// asserting against a real trail, rather than by a shared constant —
    /// that constant is deliberately private to the renderer.
    /// </summary>
    internal const int SwingTrailSegments = 6;

    /// <summary>
    /// <c>PawnRenderer.DrawWeapon</c>: a grip line, a blade line, and a
    /// highlight line via <c>DrawBlade</c> for every role (geometry —
    /// gripEnd, widthMultiplier — differs by role, quad count does not), plus
    /// two bowstring segments via <c>DrawBowstring</c> for
    /// <see cref="PawnWeaponRole.Busog"/> only (RU-42): the Busog is the only
    /// role whose weapon-line quad count differs from the rest.
    /// </summary>
    private static int WeaponQuadCount(PawnWeaponRole role) =>
        role == PawnWeaponRole.Busog ? 5 : 3;

    /// <summary>
    /// <c>PawnRenderer.DrawHeadTreatment</c>: every one of the three
    /// <see cref="PawnHeadTreatment"/> cases issues exactly two draw calls.
    /// </summary>
    private const int HeadTreatmentQuadCount = 2;

    /// <summary>
    /// <c>PawnRenderer.DrawSelectionMark</c>: four corners, two rectangles
    /// each.
    /// </summary>
    private const int SelectionMarkQuadCount = 8;

    /// <summary>
    /// <c>PawnRenderer.DrawDeadMark</c>: two crossed lines.
    /// </summary>
    private const int DeadMarkQuadCount = 2;

    /// <summary>
    /// <c>PawnRenderer.DrawLeaderMark</c> (leader rank plan L4): one filled
    /// base band (<c>spriteBatch.Draw</c>) plus two stroked rising arms
    /// (<c>DrawLine</c>), per <c>PawnRenderer.GetLeaderMarkGlyph</c>'s own
    /// doc comment — "The three quads <c>DrawLeaderMark</c> submits".
    /// </summary>
    private const int LeaderMarkQuadCount = 3;

    /// <summary>
    /// The quad count <c>PawnRenderer.Draw</c> emits for one composed pawn
    /// under these exact inputs, walked in the renderer's own documented
    /// layer order (integration design section 5). A quad here is one
    /// filled rectangle or one stroked line segment (itself one rotated
    /// quad) — matching every draw call in <c>PawnRenderer</c> today, none of
    /// which emits more than one quad per call.
    /// </summary>
    /// <param name="torsoResolutionStep">
    /// Mirrors <c>PawnRenderer.Draw</c>'s own parameter: which fallback step
    /// the torso's resolution reached. Defaults to the same
    /// <see cref="VisualFallbackStep.ModelCategoryDefault"/> every existing
    /// caller resolves at.
    /// </param>
    /// <param name="isLeader">
    /// Mirrors <c>PawnRenderer.Draw</c>'s own <c>isLeader</c> branch (leader
    /// rank plan L4): whether <c>DrawLeaderMark</c> fires this frame, adding
    /// <see cref="LeaderMarkQuadCount"/> quads. Trailing optional, defaulting
    /// to <c>false</c>, so every call site written before the leader mark
    /// existed keeps its current meaning without edits.
    /// </param>
    public static int Count(
        PawnLayout layout,
        PawnAppearance appearance,
        PawnVisualState state,
        VisualFallbackStep torsoResolutionStep = VisualFallbackStep.ModelCategoryDefault,
        bool isLeader = false)
    {
        var total = 0;

        total += CountGroundBase(layout.GroundRingBounds);
        total += CountLegs(layout);
        total += CountFeet(layout);
        total += CountSecondaryEquipment(layout, appearance);
        total += torsoResolutionStep == VisualFallbackStep.DiagnosticPlaceholder
            ? CountPlaceholder(layout.PlaceholderBounds)
            : CountTorso(layout);
        total += CountArmor(layout.ArmorBounds);
        total += CountSash(layout.SashBounds);
        total += CountShield(layout, appearance);
        total += CountHead(layout.HeadBounds);

        if (layout.DetailTier != PawnDetailTier.Low)
        {
            total += HeadTreatmentQuadCount;
        }

        total += CountAdornments(layout);
        total += CountArms(layout.Arms);
        total += CountSwingTrail(layout.SwingTrail);
        total += WeaponQuadCount(appearance.WeaponRole);
        total += CountStateMark(state);

        if (isLeader)
        {
            total += LeaderMarkQuadCount;
        }

        return total;
    }

    /// <summary>
    /// <c>PawnRenderer.DrawGroundBase</c>: the outer fill draws
    /// unconditionally; the inner shadow draws whenever a one-pixel inset of
    /// <paramref name="bounds"/> is not empty.
    /// </summary>
    private static int CountGroundBase(Rectangle bounds) =>
        1 + (IsEmpty(Inset(bounds, 1)) ? 0 : 1);

    /// <summary>
    /// <c>PawnRenderer.DrawLegs</c> (movement-gait-animation design section
    /// 7): one quad per non-empty leg rectangle, zero at
    /// <see cref="PawnDetailTier.Low"/> where <c>PawnGeometry</c> returns
    /// both leg rectangles <see cref="Rectangle.Empty"/>.
    /// </summary>
    private static int CountLegs(PawnLayout layout) =>
        (IsEmpty(layout.LeftLegBounds) ? 0 : 1) +
        (IsEmpty(layout.RightLegBounds) ? 0 : 1);

    /// <summary>
    /// <c>PawnRenderer.DrawFeet</c> (movement-gait-animation design section
    /// 7): one quad per non-empty foot rectangle, zero at
    /// <see cref="PawnDetailTier.Low"/> for the same reason as
    /// <see cref="CountLegs"/>.
    /// </summary>
    private static int CountFeet(PawnLayout layout) =>
        (IsEmpty(layout.LeftFootBounds) ? 0 : 1) +
        (IsEmpty(layout.RightFootBounds) ? 0 : 1);

    /// <summary>
    /// <c>PawnRenderer.DrawSecondaryEquipment</c>: nothing when the layout
    /// says there is no secondary-equipment footprint at all (every role
    /// other than Wasay, and Itak below Medium tier, per
    /// <c>PawnGeometry.CreateWeaponLayout</c>). A Wasay draws its axe head
    /// plus an optional lashing band (<c>DrawLashingBand</c>'s own gate:
    /// a non-null lashing color, Medium tier or above, and at least three
    /// pixels of bounds height). Any other role reaching this point is an
    /// Itak at Medium/High tier, which draws its single off-hand line.
    /// </summary>
    private static int CountSecondaryEquipment(PawnLayout layout, PawnAppearance appearance)
    {
        var bounds = layout.SecondaryEquipmentBounds;
        if (IsEmpty(bounds))
        {
            return 0;
        }

        if (appearance.WeaponRole != PawnWeaponRole.Wasay)
        {
            return 1;
        }

        var drawsLashingBand =
            appearance.WeaponLashingBandColor is not null &&
            layout.DetailTier != PawnDetailTier.Low &&
            bounds.Height >= 3;

        return drawsLashingBand ? 2 : 1;
    }

    /// <summary>
    /// <c>PawnRenderer.DrawPlaceholder</c>: one quad, gated only on the
    /// layout's already-computed placeholder rectangle not being empty.
    /// </summary>
    private static int CountPlaceholder(Rectangle bounds) => IsEmpty(bounds) ? 0 : 1;

    /// <summary>
    /// <c>PawnRenderer.DrawTorso</c>: an outline stepped capsule plus an
    /// inset fill stepped capsule (three quads each, via
    /// <c>DrawSteppedCapsule</c>, when their bounds are not empty), plus one
    /// belt quad at High tier only.
    /// </summary>
    private static int CountTorso(PawnLayout layout)
    {
        var total = CountSteppedCapsule(layout.TorsoBounds) +
            CountSteppedCapsule(Inset(layout.TorsoBounds, 1));

        if (layout.DetailTier == PawnDetailTier.High)
        {
            total += 1;
        }

        return total;
    }

    private static int CountSteppedCapsule(Rectangle bounds) => IsEmpty(bounds) ? 0 : 3;

    private static int CountArmor(Rectangle bounds) => IsEmpty(bounds) ? 0 : 1;

    private static int CountSash(Rectangle bounds) => IsEmpty(bounds) ? 0 : 1;

    /// <summary>
    /// <c>PawnRenderer.DrawShield</c>: nothing for an unshielded warrior.
    /// Otherwise a face (one quad for every skin except <c>boxerCagayan</c>,
    /// whose curved face is three quads once its curvature inset actually
    /// carves the block — see <see cref="CountShieldFace"/>), an optional
    /// seam/accent quad at Medium tier and above, and two edge-tone quads at
    /// High tier when the block is wide enough.
    /// </summary>
    private static int CountShield(PawnLayout layout, PawnAppearance appearance)
    {
        var bounds = layout.ShieldBounds;
        if (IsEmpty(bounds))
        {
            return 0;
        }

        var isBoxerCagayan =
            appearance.ShieldSkinId == ShieldVisualCatalog.BoxerCagayan.Catalog.Id;
        var isVisayanKalasag =
            appearance.ShieldSkinId == ShieldVisualCatalog.VisayanKalasag.Catalog.Id;

        var total = CountShieldFace(bounds, layout.DetailTier, isBoxerCagayan);

        if (layout.DetailTier != PawnDetailTier.Low)
        {
            if (isVisayanKalasag)
            {
                if (bounds.Height >= 3)
                {
                    total += 1;
                }
            }
            else if (bounds.Width >= 3)
            {
                total += 1;
            }
        }

        if (layout.DetailTier == PawnDetailTier.High && bounds.Width >= 2)
        {
            total += 2;
        }

        return total;
    }

    /// <summary>
    /// Mirrors <c>PawnRenderer.DrawCurvedShieldFace</c>/
    /// <c>ShieldCurvatureInsetPixels</c> exactly: a non-<c>boxerCagayan</c>
    /// skin always draws the plain rotated block (one quad); <c>boxerCagayan</c>
    /// falls back to the same one quad whenever its tier-derived curvature
    /// inset leaves no room, and otherwise draws the three-piece curved face.
    /// </summary>
    private static int CountShieldFace(Rectangle bounds, PawnDetailTier tier, bool isBoxerCagayan)
    {
        if (!isBoxerCagayan)
        {
            return 1;
        }

        var insetPixels = tier switch
        {
            PawnDetailTier.Low => 0,
            PawnDetailTier.Medium => 1,
            _ => 2,
        };
        var insetWidth = bounds.Width - (insetPixels * 2);

        return insetPixels <= 0 || insetWidth <= 0 ? 1 : 3;
    }

    /// <summary>
    /// <c>PawnRenderer.DrawHead</c>: two <c>DrawDisk</c> calls (outline then
    /// skin fill), each three quads when its bounds are not empty.
    /// </summary>
    private static int CountHead(Rectangle headBounds) =>
        CountDisk(headBounds) + CountDisk(Inset(headBounds, 1));

    private static int CountDisk(Rectangle bounds) => IsEmpty(bounds) ? 0 : 3;

    /// <summary>
    /// <c>PawnRenderer.DrawAdornments</c>: one quad per non-empty adornment
    /// accent rectangle.
    /// </summary>
    private static int CountAdornments(PawnLayout layout) =>
        (IsEmpty(layout.AdornmentAccentPrimaryBounds) ? 0 : 1) +
        (IsEmpty(layout.AdornmentAccentSecondaryBounds) ? 0 : 1);

    /// <summary>
    /// <c>PawnRenderer.DrawSwingTrail</c>: nothing for an empty trail,
    /// otherwise exactly <see cref="SwingTrailSegments"/> stroked segments.
    /// </summary>
    private static int CountSwingTrail(SwingTrail trail) =>
        trail.IsEmpty ? 0 : SwingTrailSegments;

    /// <summary>
    /// <c>PawnRenderer.DrawArms</c>: one stroked quad per drawn arm segment,
    /// and nothing at all for a pawn that is not mid-contact or is at the low
    /// detail tier. Mirrors that method's four guarded calls exactly, which is
    /// what keeps the design's ceiling of four extra quads per active Medium
    /// or High pawn a counted fact rather than an assertion.
    /// </summary>
    private static int CountArms(ArmLayout arms)
    {
        if (arms.IsEmpty)
        {
            return 0;
        }

        var total = 0;

        if (!arms.WeaponUpperArm.IsEmpty)
        {
            total++;
        }

        if (!arms.WeaponForearm.IsEmpty)
        {
            total++;
        }

        if (!arms.SupportUpperArm.IsEmpty)
        {
            total++;
        }

        if (!arms.SupportForearm.IsEmpty)
        {
            total++;
        }

        return total;
    }

    /// <summary>
    /// <c>PawnRenderer.Draw</c>'s trailing state mark: the selection corners
    /// for a hovered or selected pawn, the crossed dead mark for a dead one,
    /// nothing for a pawn in its normal state.
    /// </summary>
    private static int CountStateMark(PawnVisualState state) =>
        state switch
        {
            PawnVisualState.Hovered or PawnVisualState.Selected => SelectionMarkQuadCount,
            PawnVisualState.Dead => DeadMarkQuadCount,
            _ => 0,
        };

    /// <summary>
    /// Matches <c>PawnRenderer.Inset</c> exactly, including its
    /// zero-or-negative-dimension collapse to <see cref="Rectangle.Empty"/>.
    /// </summary>
    private static Rectangle Inset(Rectangle bounds, int amount)
    {
        var width = bounds.Width - (amount * 2);
        var height = bounds.Height - (amount * 2);
        return width <= 0 || height <= 0
            ? Rectangle.Empty
            : new Rectangle(bounds.X + amount, bounds.Y + amount, width, height);
    }

    private static bool IsEmpty(Rectangle bounds) => bounds.IsEmpty;
}

/// <summary>
/// Renderer-agnostic Tier 1 quad counts for the battlefield backdrop and its
/// ambient effects (VIS-034, integration design section 8's "backdrop caps",
/// amendment A-1): the ground grid and scatter decals
/// (<see cref="PlainsBackdropRenderer"/>), grass clusters
/// (<see cref="GrassRenderer"/>), trample marks, and live dust puffs. Every
/// method is a pure function over already-built layout/generation values,
/// never called from the live render loop.
/// </summary>
internal static class BackdropQuadCount
{
    /// <summary>
    /// <c>PlainsBackdropRenderer.DrawGroundGrid</c>: one quad per cell, in
    /// the worst case (no cell culled by the arena bounds).
    /// </summary>
    public static int GroundGrid(int columns, int rows) =>
        columns > 0 && rows > 0 ? columns * rows : 0;

    /// <summary>
    /// <c>PlainsBackdropRenderer.DrawDecals</c>: one quad per decal, in the
    /// worst case (no decal clipped away entirely).
    /// </summary>
    public static int Decals(int decalCount) => Math.Max(0, decalCount);

    /// <summary>
    /// <c>GrassRenderer.DrawTrampleMarks</c>: one quad per live trample mark.
    /// </summary>
    public static int TrampleMarks(int markCount) => Math.Max(0, markCount);

    /// <summary>
    /// <c>GrassRenderer.Draw</c>'s grass loop: the sum of
    /// <see cref="GrassGeometry.GetQuadCount"/> across every cluster at the
    /// given zoom band, in the worst case (no cluster culled by the map or
    /// arena bounds).
    /// </summary>
    public static int GrassClusters(ImmutableArray<GrassCluster> clusters, GrassZoomBand band)
    {
        if (clusters.IsDefaultOrEmpty)
        {
            return 0;
        }

        var total = 0;
        foreach (var cluster in clusters)
        {
            total += GrassGeometry.GetQuadCount(cluster, band);
        }

        return total;
    }

    /// <summary>
    /// <c>DustRenderer</c>'s puff loop: the sum of
    /// <see cref="DustGeometry.Create"/>'s <c>RectangleCount</c> across every
    /// live puff at the given camera zoom.
    /// </summary>
    public static int DustPuffs(ReadOnlySpan<DustPuff> puffs, float cameraZoom)
    {
        var total = 0;
        foreach (var puff in puffs)
        {
            total += DustGeometry.Create(puff, cameraZoom).RectangleCount;
        }

        return total;
    }
}

/// <summary>
/// Whole-frame arena-batch quad budgets (integration design section 11):
/// ESTIMATE until VIS-036 measures a real frame with
/// <c>tools/Hukbo.Tools.RenderProbe</c> and either promotes these to enforced
/// numbers or records a reviewed revision — never silently rewritten to match
/// a measurement (the anti-density-creep rule, R-W6.14). Amendment A-1
/// redenominates the section 8 budget in quads rather than
/// <c>SpriteBatch.Draw</c> submissions; the numeric values themselves are
/// unchanged from the design.
/// </summary>
internal static class RenderBudgetEstimate
{
    // movement-gait-animation T4: the legs and feet
    // (PawnRenderer.DrawLegs/DrawFeet, PawnQuadCount.CountLegs/CountFeet)
    // raised the representative dense per-pawn quad count
    // RenderBudgetEstimateTests measures against these two ceilings from 20
    // to 24 quads (High tier, the unshielded/unarmored Kampilan baseline
    // pinned by PawnQuadCountTests.Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn):
    // a left and a right leg quad, plus a left and a right foot quad, is a
    // delta of +4. Backdrop's own worst case (ground grid + decals + grass
    // clusters + trample marks + dust puffs, every one at its own hard cap
    // simultaneously) is unaffected by this change and stays 4,032 quads.
    //
    //   old: (20 quads/pawn x 200 units) + 4,032 backdrop =  8,032 quads
    //   new: (24 quads/pawn x 200 units) + 4,032 backdrop =  8,832 quads
    //   old: (20 quads/pawn x 500 units) + 4,032 backdrop = 14,032 quads
    //   new: (24 quads/pawn x 500 units) + 4,032 backdrop = 16,032 quads
    //
    // Both new totals still fit under the existing ceilings below with
    // margin, so those ceilings are left exactly as VIS-034/035 set them —
    // moving either one for a per-pawn cost that already fits inside it
    // would be the "silently rewritten to match a measurement" this class's
    // own doc comment forbids (R-W6.14). RenderBudgetEstimateTests recomputes
    // the actual per-pawn term from PawnQuadCount.Count every run, so this
    // comment records the arithmetic rather than a value the test re-derives
    // on its own.
    //
    // RU-23: PawnGeometry.CreateSecondaryBounds gained a Busog arm (RU-22)
    // that draws the nocked arrow through the existing SecondaryEquipmentBounds
    // slot the Wasay's axe head already occupies — one new rectangle, and the
    // only one any of the three new PawnWeaponRole members adds (Bangkaw and
    // Arquebus draw no secondary rectangle at all). That raises the 24-quad
    // High-tier baseline to 25 for the worst case where every visible unit is
    // a Busog.
    //
    // RU-42: PawnRenderer.DrawWeapon's Busog arm gained a DrawBowstring call
    // (two stroked line segments — WeaponQuadCount is now role-dependent,
    // 5 for Busog vs 3 for every other role) so the bowstring can bend with
    // RangedPose.DrawTension. That raises the Busog worst case by 2, from 25
    // to 27:
    //
    //   (27 quads/pawn x 200 units) + 4,032 backdrop =  9,432 quads
    //   (27 quads/pawn x 500 units) + 4,032 backdrop = 17,532 quads
    //
    // Both stay under the ceilings below with margin (2,568 quads of headroom
    // at 200 units, 2,468 at 500), so — same rule as above — the ceilings are
    // left unmoved. In-flight projectiles are not folded into this per-pawn
    // term: they are a separate, bounded population
    // (Scenario.MaximumProjectilesInFlight, default 512) counted against the
    // same whole-frame estimate at ProjectileQuadsPerProjectile quad each (one
    // stroked line, the same "one quad per live instance" convention
    // BackdropQuadCount.TrampleMarks and .Decals already use):
    //
    //   9,432 + (512 x 1 projectile quad)  =  9,944 quads (200 units)
    //   17,532 + (512 x 1 projectile quad) = 18,044 quads (500 units)
    //
    // Both still fit under the unmoved ceilings (2,056 quads of headroom at
    // 200 units, 1,956 at 500) — but note the 500-unit margin has fallen from
    // 3,468 to 1,956 across RU-23 and RU-42, so the next feature that wants a
    // per-pawn quad owes a fresh measurement rather than an assumption.
    // The projectile draw itself landed in RU-25 as ArenaGame.DrawProjectiles
    // (ArenaGame.Rendering.cs), one stroked shaft per live flight, rather than
    // as a ProjectileRenderer.cs of its own; RenderBudgetEstimateTests
    // reads Scenario.MaximumProjectilesInFlight's own default rather than
    // repeating 512 as a second literal, so the two cannot drift apart.
    //
    // projectile-props (docs/plans/2026-08-11-projectile-props.md) is the
    // feature that note was written for, and it paid the debt: it wants both a
    // second projectile quad and a new per-pawn population, which is the more
    // expensive of the two shapes the note anticipated. The measurement is on
    // record — tools/Hukbo.Tools.RenderProbe, 500 units, seed 1, Release,
    // retrace disabled, 150 frames per station, 2026-08-11, report at
    // artifacts/render-probe-projectile-props-500.json:
    //
    //   minimum-zoom  9,245 quads   p50 0.72 ms
    //   default-fit   9,246 quads   p50 0.75 ms
    //   maximum-zoom  1,547 quads   p50 0.32 ms
    //
    // The worst real frame at 500 units is 9,246 quads against a ceiling of
    // 20,000. The 18,044 figure below is a stacked worst case — every visible
    // unit a High-tier Busog, every backdrop population at its own cap in the
    // same frame, and all 512 flight slots live at once — that no real frame
    // approaches. THE CEILINGS STILL DO NOT MOVE. A measurement showing slack
    // is not a licence to rewrite a budget to match it (R-W6.14, this class's
    // own doc comment); a ceiling bounds the worst case rather than describing
    // the median one. What the measurement buys is the answer to the question
    // the RU-42 note actually asked, which is whether the thin estimated
    // margin is a real risk. It is not.
    //
    // Two new terms, and note the first is a DELTA rather than a fresh cost —
    // the projectile-props design's own budget table got this wrong, adding
    // the full 1,024 on top of the 512 already charged above and arriving at
    // 420 quads of headroom that were never actually spent:
    //
    //   in-flight prop     2 quads x 512 flights = 1,024 (was 512, so +512)
    //   embedded pool      2 quads x 256 slots   =   512
    //
    //   17,532 + 1,024 + 512 = 19,068 quads (500 units), headroom 932
    //    9,432 + 1,024 + 512 = 10,968 quads (200 units), headroom 1,032
    //
    // The embedded pool is detail-tier gated (design section 8 decision 4), so
    // a real 500-unit frame — pulled far enough out for 500 units to be on
    // screen at once — drops those 512 entirely and sits at 18,556, headroom
    // 1,444. The gated figure is the likely one and the ungated figure is the
    // one budgeted for, which is the correct way round.

    /// <summary>Arena-batch quad ceiling at 200 visible units.</summary>
    internal const int ArenaBatchQuadsAt200UnitsEstimate = 12_000;

    /// <summary>Arena-batch quad ceiling at 500 visible units.</summary>
    internal const int ArenaBatchQuadsAt500UnitsEstimate = 20_000;

    /// <summary>
    /// Quad cost of one live in-flight projectile: the two stroked segments
    /// <see cref="ProjectileGeometry"/> builds — a shaft plus a head or a
    /// fletch. An arquebus ball draws one rather than two, so this is the
    /// bound rather than the exact cost of every flight, which is what a
    /// budget term is for.
    /// </summary>
    internal const int ProjectileQuadsPerProjectile = 2;

    /// <summary>
    /// Quad cost of one embedded projectile: a shaft plus a fletch, the same
    /// two-element shape an arrow draws in flight.
    /// </summary>
    internal const int EmbeddedProjectileQuadsPerProjectile = 2;
}
