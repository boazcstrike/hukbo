using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
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

        var embeddedProjectileQuads =
            EmbeddedProjectileSystem.Capacity *
            RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile;

        var totalAt200Units =
            (perPawnQuads * 200) + backdropWorstCaseQuads + embeddedProjectileQuads;
        var totalAt500Units =
            (perPawnQuads * 500) + backdropWorstCaseQuads + embeddedProjectileQuads;

        Assert.True(
            totalAt200Units <= RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate,
            $"200-unit worst case ({totalAt200Units} quads: {perPawnQuads} per pawn x 200 + " +
                $"{backdropWorstCaseQuads} backdrop + {embeddedProjectileQuads} embedded " +
                $"projectiles) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate}).");
        Assert.True(
            totalAt500Units <= RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate,
            $"500-unit worst case ({totalAt500Units} quads: {perPawnQuads} per pawn x 500 + " +
                $"{backdropWorstCaseQuads} backdrop + {embeddedProjectileQuads} embedded " +
                $"projectiles) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate}).");
    }

    /// <summary>
    /// RU-23: the all-ranged worst case — every one of the 200/500 units is a
    /// Busog pawn, the one ranged role that draws an extra rectangle
    /// (<see cref="PawnQuadCountTests.Count_PinsTheHighTierUnshieldedUnarmoredRangedPawn"/>)
    /// — plus the bounded in-flight projectile population, counted separately
    /// from the per-pawn term rather than folded into it, per the RU-23 plan
    /// row. <see cref="Scenario.MaximumProjectilesInFlight"/>'s own default is
    /// read here rather than repeated as a literal, so this test cannot drift
    /// from the value <c>Hukbo.Core</c> actually enforces.
    /// </summary>
    [Fact]
    public void WholeFrameWorstCaseArithmetic_AllRangedUnitsWithProjectilesFitsWithinTheEstimateAt200And500Units()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Busog, ShieldId.None);
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

        var maximumProjectilesInFlight = new Scenario(
            Seed: 1,
            MapWidth: 1,
            MapHeight: 1,
            AgentsPerFaction: 1,
            TickRate: 1,
            TickLimit: 1).MaximumProjectilesInFlight;
        var projectileQuads =
            maximumProjectilesInFlight * RenderBudgetEstimate.ProjectileQuadsPerProjectile;

        var embeddedProjectileQuads =
            EmbeddedProjectileSystem.Capacity *
            RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile;

        var totalAt200Units =
            (perPawnQuads * 200) + backdropWorstCaseQuads + projectileQuads +
                embeddedProjectileQuads;
        var totalAt500Units =
            (perPawnQuads * 500) + backdropWorstCaseQuads + projectileQuads +
                embeddedProjectileQuads;

        Assert.True(
            totalAt200Units <= RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate,
            $"200-unit all-ranged worst case ({totalAt200Units} quads: " +
                $"{perPawnQuads} per pawn x 200 + {backdropWorstCaseQuads} backdrop + " +
                $"{projectileQuads} in-flight projectiles + {embeddedProjectileQuads} " +
                $"embedded projectiles) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt200UnitsEstimate}).");
        Assert.True(
            totalAt500Units <= RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate,
            $"500-unit all-ranged worst case ({totalAt500Units} quads: " +
                $"{perPawnQuads} per pawn x 500 + {backdropWorstCaseQuads} backdrop + " +
                $"{projectileQuads} in-flight projectiles + {embeddedProjectileQuads} " +
                $"embedded projectiles) exceeds the ESTIMATE budget " +
                $"({RenderBudgetEstimate.ArenaBatchQuadsAt500UnitsEstimate}).");
    }

    /// <summary>
    /// PV-8: the whole-screen effects-quad assertion. Every fixed-capacity
    /// effect pool the presentation layer owns — blood bursts, blood ground
    /// marks, lethal spurts, hit effects, and clash effects, none of which
    /// the two tests above account for, plus dust and trample marks, which
    /// the backdrop worst case above already folds in — stacked at its own
    /// capacity in the same frame as the per-pawn and backdrop worst cases.
    /// The five new pools' capacities are read from
    /// <see cref="PawnAppearanceCache.Capacity"/>, the default every one of
    /// them actually constructs at in production
    /// (<c>PresentationCoordinator</c>'s constructor, unless a caller
    /// overrides it, which the shipped <c>ArenaGame</c> does not for any of
    /// these five), rather than repeated as a second literal.
    /// </summary>
    [Fact]
    public void WholeScreenEffectPoolWorstCaseArithmetic_FitsWithinTheWholeScreenEstimateAt200And500Units()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Busog, ShieldId.None);
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

        var maximumProjectilesInFlight = new Scenario(
            Seed: 1,
            MapWidth: 1,
            MapHeight: 1,
            AgentsPerFaction: 1,
            TickRate: 1,
            TickLimit: 1).MaximumProjectilesInFlight;
        var projectileQuads =
            maximumProjectilesInFlight * RenderBudgetEstimate.ProjectileQuadsPerProjectile;

        var embeddedProjectileQuads =
            EmbeddedProjectileSystem.Capacity *
            RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile;

        var hitEffectQuads =
            PawnAppearanceCache.Capacity * RenderBudgetEstimate.HitEffectQuadsPerHitEffect;
        var bloodBurstQuads =
            PawnAppearanceCache.Capacity * RenderBudgetEstimate.BloodBurstQuadsPerBurst;
        var bloodGroundMarkQuads =
            PawnAppearanceCache.Capacity * RenderBudgetEstimate.BloodGroundMarkQuadsPerMark;
        var bloodSpurtQuads =
            PawnAppearanceCache.Capacity * RenderBudgetEstimate.BloodSpurtQuadsPerSpurt;
        var clashEffectQuads =
            PawnAppearanceCache.Capacity * RenderBudgetEstimate.ClashEffectQuadsPerEffect;

        var totalAt200Units =
            (perPawnQuads * 200) + backdropWorstCaseQuads + projectileQuads +
                embeddedProjectileQuads + hitEffectQuads + bloodBurstQuads +
                bloodGroundMarkQuads + bloodSpurtQuads + clashEffectQuads;
        var totalAt500Units =
            (perPawnQuads * 500) + backdropWorstCaseQuads + projectileQuads +
                embeddedProjectileQuads + hitEffectQuads + bloodBurstQuads +
                bloodGroundMarkQuads + bloodSpurtQuads + clashEffectQuads;

        Assert.True(
            totalAt200Units <=
                RenderBudgetEstimate.WholeScreenEffectPoolQuadsAt200UnitsEstimate,
            $"200-unit whole-screen worst case ({totalAt200Units} quads: " +
                $"{perPawnQuads} per pawn x 200 + {backdropWorstCaseQuads} backdrop " +
                $"(ground grid + decals + grass clusters + trample marks + dust puffs) + " +
                $"{projectileQuads} in-flight projectiles + {embeddedProjectileQuads} " +
                $"embedded projectiles + {hitEffectQuads} hit effects + " +
                $"{bloodBurstQuads} blood bursts + {bloodGroundMarkQuads} blood ground " +
                $"marks + {bloodSpurtQuads} lethal spurts + {clashEffectQuads} clash " +
                $"effects) exceeds the whole-screen ESTIMATE budget " +
                $"({RenderBudgetEstimate.WholeScreenEffectPoolQuadsAt200UnitsEstimate}).");
        Assert.True(
            totalAt500Units <=
                RenderBudgetEstimate.WholeScreenEffectPoolQuadsAt500UnitsEstimate,
            $"500-unit whole-screen worst case ({totalAt500Units} quads: " +
                $"{perPawnQuads} per pawn x 500 + {backdropWorstCaseQuads} backdrop " +
                $"(ground grid + decals + grass clusters + trample marks + dust puffs) + " +
                $"{projectileQuads} in-flight projectiles + {embeddedProjectileQuads} " +
                $"embedded projectiles + {hitEffectQuads} hit effects + " +
                $"{bloodBurstQuads} blood bursts + {bloodGroundMarkQuads} blood ground " +
                $"marks + {bloodSpurtQuads} lethal spurts + {clashEffectQuads} clash " +
                $"effects) exceeds the whole-screen ESTIMATE budget " +
                $"({RenderBudgetEstimate.WholeScreenEffectPoolQuadsAt500UnitsEstimate}).");
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
