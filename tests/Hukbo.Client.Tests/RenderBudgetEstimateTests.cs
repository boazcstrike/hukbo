using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Whole-frame arena-batch quad arithmetic against
/// <see cref="RenderBudgetEstimate"/> (VIS-034, integration design section
/// 11, amendment A-1): a representative dense per-pawn quad count times the
/// unit count, plus the backdrop's own worst case (ground grid, decals,
/// grass clusters, trample marks, and live dust puffs, every one at its
/// named hard cap simultaneously).
/// </summary>
/// <remarks>
/// The per-pawn term is the High-tier baseline pin from
/// <c>PawnQuadCountTests</c> (a fully visible, unshielded pawn) rather than
/// that test file's own combinatorial worst case (every optional cosmetic
/// layer maxed on every single unit at once, including a rare shield
/// skin/weapon-tint combination) — the design's own "today's counted order"
/// framing (integration design section 8) is a representative dense pawn,
/// not the counting seam's own combinatorial ceiling, and five hundred units
/// simultaneously hitting that literal combinatorial maximum is not a battle
/// roster this budget needs to defend against. The seam's own ceiling
/// behavior is separately exercised and pinned by
/// <c>PawnQuadCountTests.Count_PinsTheHighTierFullyLoadedSelectedPawn</c>.
/// </remarks>
public sealed class RenderBudgetEstimateTests
{
    [Fact]
    public void WholeFrameWorstCaseArithmetic_FitsWithinTheEstimateAt200And500Units()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, 3f, appearance);
        Assert.Equal(PawnDetailTier.High, layout.DetailTier);

        var perPawnQuads = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);

        var backdropWorstCaseQuads =
            BackdropQuadCount.GroundGrid(
                PlainsBackdropGeometry.MaximumGridDimension,
                PlainsBackdropGeometry.MaximumGridDimension) +
            BackdropQuadCount.Decals(PlainsBackdropGeometry.MaximumDecalCount) +
            BackdropQuadCount.GrassClusters(WorstCaseGrassClusters(), GrassZoomBand.Near) +
            BackdropQuadCount.TrampleMarks(TrampleMarkSystem.Capacity) +
            BackdropQuadCount.DustPuffs(WorstCaseDustPuffs(), cameraZoom: 3f);

        var totalAt200Units = (perPawnQuads * 200) + backdropWorstCaseQuads;
        var totalAt500Units = (perPawnQuads * 500) + backdropWorstCaseQuads;

        Assert.True(
            totalAt200Units <= RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate,
            $"200-unit worst case ({totalAt200Units} quads: {perPawnQuads} per pawn x 200 + " +
                $"{backdropWorstCaseQuads} backdrop) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate}).");
        Assert.True(
            totalAt500Units <= RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate,
            $"500-unit worst case ({totalAt500Units} quads: {perPawnQuads} per pawn x 500 + " +
                $"{backdropWorstCaseQuads} backdrop) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate}).");
    }

    private static ImmutableArray<GrassCluster> WorstCaseGrassClusters()
    {
        var worstCaseCluster = new GrassCluster(
            Vector2.Zero,
            Phase: 0f,
            SizeClass: GrassSizeClass.Large,
            QuadCount: GrassGeometry.MaximumQuadsPerCluster);

        return Enumerable
            .Repeat(worstCaseCluster, GrassGeometry.MaximumClusterCount)
            .ToImmutableArray();
    }

    private static DustPuff[] WorstCaseDustPuffs()
    {
        var puffs = new DustPuff[DustEffectSystem.Capacity];
        for (var i = 0; i < puffs.Length; i++)
        {
            // Death is the two-rectangle kind, the densest live puff.
            puffs[i] = new DustPuff(
                Sequence: i,
                XRaw: 0,
                YRaw: 0,
                Kind: DustPuffKind.Death,
                AgeSeconds: 0f);
        }

        return puffs;
    }
}
