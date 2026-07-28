using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="BackdropQuadCount"/> against the real draw sinks it
/// mirrors: <c>PlainsBackdropRenderer</c> (ground grid, decals),
/// <c>GrassRenderer</c> (grass clusters, trample marks), and
/// <c>DustRenderer</c> (dust puffs) — VIS-034, integration design section
/// 8's "backdrop caps", amendment A-1.
/// </summary>
public sealed class BackdropQuadCountTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(6, 4, 24)]
    public void GroundGrid_OneQuadPerCellUnlessEitherDimensionIsZero(
        int columns,
        int rows,
        int expected)
    {
        Assert.Equal(expected, BackdropQuadCount.GroundGrid(columns, rows));
    }

    [Fact]
    public void GroundGrid_PinsTheWorstCaseAtTheNamedGridCeiling()
    {
        Assert.Equal(
            PlainsBackdropGeometry.MaximumGridDimension *
                PlainsBackdropGeometry.MaximumGridDimension,
            BackdropQuadCount.GroundGrid(
                PlainsBackdropGeometry.MaximumGridDimension,
                PlainsBackdropGeometry.MaximumGridDimension));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(37, 37)]
    public void Decals_OneQuadPerDecal(int decalCount, int expected)
    {
        Assert.Equal(expected, BackdropQuadCount.Decals(decalCount));
    }

    [Fact]
    public void Decals_PinsTheWorstCaseAtTheNamedDecalCeiling()
    {
        Assert.Equal(
            PlainsBackdropGeometry.MaximumDecalCount,
            BackdropQuadCount.Decals(PlainsBackdropGeometry.MaximumDecalCount));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 9)]
    public void TrampleMarks_OneQuadPerMark(int markCount, int expected)
    {
        Assert.Equal(expected, BackdropQuadCount.TrampleMarks(markCount));
    }

    [Fact]
    public void TrampleMarks_PinsTheWorstCaseAtTheNamedCapacity()
    {
        Assert.Equal(
            TrampleMarkSystem.Capacity,
            BackdropQuadCount.TrampleMarks(TrampleMarkSystem.Capacity));
    }

    [Fact]
    public void GrassClusters_EmptyArrayContributesNoQuads()
    {
        Assert.Equal(
            0,
            BackdropQuadCount.GrassClusters(
                ImmutableArray<GrassCluster>.Empty,
                GrassZoomBand.Near));
    }

    [Fact]
    public void GrassClusters_SumsGetQuadCountAcrossEveryCluster()
    {
        var clusters = ImmutableArray.Create(
            new GrassCluster(Vector2.Zero, Phase: 0f, SizeClass: GrassSizeClass.Small, QuadCount: 1),
            new GrassCluster(Vector2.One, Phase: 0f, SizeClass: GrassSizeClass.Medium, QuadCount: 3),
            new GrassCluster(Vector2.UnitX, Phase: 0f, SizeClass: GrassSizeClass.Large, QuadCount: 4));

        var expected =
            GrassGeometry.GetQuadCount(clusters[0], GrassZoomBand.Mid) +
            GrassGeometry.GetQuadCount(clusters[1], GrassZoomBand.Mid) +
            GrassGeometry.GetQuadCount(clusters[2], GrassZoomBand.Mid);

        Assert.Equal(
            expected,
            BackdropQuadCount.GrassClusters(clusters, GrassZoomBand.Mid));
    }

    [Fact]
    public void GrassClusters_PinsTheWorstCaseAtTheNamedClusterAndPerClusterCeilings()
    {
        var worstCaseCluster = new GrassCluster(
            Vector2.Zero,
            Phase: 0f,
            SizeClass: GrassSizeClass.Large,
            QuadCount: GrassGeometry.MaximumQuadsPerCluster);
        var clusters = Enumerable
            .Repeat(worstCaseCluster, GrassGeometry.MaximumClusterCount)
            .ToImmutableArray();

        // Near band: GetQuadCount clamps to MaximumQuadsPerCluster even
        // after its own +1 near-band bump, so the per-cluster ceiling holds.
        Assert.Equal(
            GrassGeometry.MaximumClusterCount * GrassGeometry.MaximumQuadsPerCluster,
            BackdropQuadCount.GrassClusters(clusters, GrassZoomBand.Near));
    }

    [Fact]
    public void DustPuffs_NoPuffsContributesNoQuads()
    {
        Assert.Equal(0, BackdropQuadCount.DustPuffs([], cameraZoom: 1f));
    }

    [Fact]
    public void DustPuffs_DeathPuffsContributeTwoQuadsAndAttackKicksContributeOne()
    {
        DustPuff[] puffs =
        [
            new DustPuff(Sequence: 1, XRaw: 0, YRaw: 0, Kind: DustPuffKind.Death, AgeSeconds: 0f),
            new DustPuff(Sequence: 2, XRaw: 0, YRaw: 0, Kind: DustPuffKind.AttackKick, AgeSeconds: 0f),
        ];

        Assert.Equal(3, BackdropQuadCount.DustPuffs(puffs, cameraZoom: 1f));
    }

    [Fact]
    public void DustPuffs_PinsTheWorstCaseAtTheNamedCapacityWithEveryPuffAtDeathKind()
    {
        var puffs = new DustPuff[DustEffectSystem.Capacity];
        for (var i = 0; i < puffs.Length; i++)
        {
            puffs[i] = new DustPuff(
                Sequence: i,
                XRaw: 0,
                YRaw: 0,
                Kind: DustPuffKind.Death,
                AgeSeconds: 0f);
        }

        // Every puff is a two-rectangle Death puff, the densest kind.
        Assert.Equal(DustEffectSystem.Capacity * 2, BackdropQuadCount.DustPuffs(puffs, cameraZoom: 1f));
    }
}
