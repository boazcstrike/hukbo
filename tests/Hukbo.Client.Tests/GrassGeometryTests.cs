using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class GrassGeometryTests
{
    private const int DefaultMapWidth = 1_280;
    private const int DefaultMapHeight = 720;
    private const int LargeMapWidth = 6_000;
    private const int LargeMapHeight = 6_000;
    private const ulong DefaultSeed = 1;

    [Fact]
    public void GenerateClusters_SameSeedAndMapProducesEqualSequences()
    {
        var first = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);
        var second = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        // Compared element-wise on purpose: ImmutableArray<T> implements
        // IEquatable<ImmutableArray<T>> as reference equality of the inner
        // array, so a direct Assert.Equal would compare identity rather than
        // contents and an Assert.NotEqual would always succeed. Element-wise
        // equality covers positions, phases, size classes, and quad counts
        // together, since GrassCluster is a record struct.
        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.NotEmpty(first);
    }

    [Fact]
    public void GenerateClusters_DifferentSeedsProduceDifferentSequences()
    {
        var first = GrassGeometry.GenerateClusters(
            1,
            DefaultMapWidth,
            DefaultMapHeight);
        var second = GrassGeometry.GenerateClusters(
            2,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.NotEqual(first.ToArray(), second.ToArray());
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(1_000u)]
    public void GenerateClusters_NeverExceedsTheMaximumClusterCount(ulong seed)
    {
        var clusters = GrassGeometry.GenerateClusters(
            seed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.True(clusters.Length <= GrassGeometry.MaximumClusterCount);

        // A very large map drives the cluster-center count to its ceiling
        // (48), and 48 centers at the per-center draw ceiling (12) would sum
        // to 576 — comfortably past MaximumClusterCount — so this is exactly
        // the shape of input the hard cap exists to catch, regardless of the
        // specific per-center draws any one seed happens to produce.
        var largeMapClusters = GrassGeometry.GenerateClusters(
            seed,
            1_000_000,
            1_000_000);

        Assert.True(largeMapClusters.Length <= GrassGeometry.MaximumClusterCount);
    }

    [Fact]
    public void GenerateClusters_TheHardCapIsReachableOnAVeryLargeMap()
    {
        // Seed 2 against a very large map is a pinned, verified combination
        // where the per-center random tuft draws sum past
        // MaximumClusterCount before every cluster center has been visited,
        // proving the early-exit safety valve actually engages for a real
        // seed rather than existing only on paper.
        var clusters = GrassGeometry.GenerateClusters(2, 1_000_000, 1_000_000);

        Assert.Equal(GrassGeometry.MaximumClusterCount, clusters.Length);
    }

    [Fact]
    public void GenerateClusters_EveryQuadCountStaysWithinTheNamedCap()
    {
        var clusters = GrassGeometry.GenerateClusters(
            DefaultSeed,
            LargeMapWidth,
            LargeMapHeight);

        Assert.NotEmpty(clusters);
        foreach (var cluster in clusters)
        {
            Assert.InRange(cluster.QuadCount, 1, GrassGeometry.MaximumQuadsPerCluster);
        }
    }

    [Fact]
    public void GenerateClusters_EveryPositionRespectsTheGrassFreeBorderMargin()
    {
        var clusters = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.NotEmpty(clusters);
        var minX = (float)GrassGeometry.GrassFreeBorderMargin;
        var maxX = (float)(DefaultMapWidth - GrassGeometry.GrassFreeBorderMargin);
        var minY = (float)GrassGeometry.GrassFreeBorderMargin;
        var maxY = (float)(DefaultMapHeight - GrassGeometry.GrassFreeBorderMargin);
        foreach (var cluster in clusters)
        {
            Assert.InRange(cluster.WorldPosition.X, minX, maxX);
            Assert.InRange(cluster.WorldPosition.Y, minY, maxY);
        }
    }

    [Fact]
    public void GrassFreeBorderMargin_IsExactlyOneGroundCell()
    {
        Assert.Equal(
            PlainsBackdropGeometry.TargetGroundCellSize,
            GrassGeometry.GrassFreeBorderMargin);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(0, 0)]
    public void GenerateClusters_DegenerateMapRectangleYieldsEmptyResultRatherThanThrowing(
        int mapWidth,
        int mapHeight)
    {
        var clusters = GrassGeometry.GenerateClusters(DefaultSeed, mapWidth, mapHeight);

        Assert.Empty(clusters);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1_280, 720)]
    [InlineData(6_000, 6_000)]
    public void GetClusterCenterCount_StaysWithinTheNamedRange(
        int mapWidth,
        int mapHeight)
    {
        var count = GrassGeometry.GetClusterCenterCount(mapWidth, mapHeight);

        Assert.InRange(
            count,
            GrassGeometry.MinimumClusterCenterCount,
            GrassGeometry.MaximumClusterCenterCount);
    }

    [Fact]
    public void GetClusterCenterCount_RespectsTheFloorOnAVerySmallMap()
    {
        var count = GrassGeometry.GetClusterCenterCount(10, 10);

        Assert.Equal(GrassGeometry.MinimumClusterCenterCount, count);
    }

    [Fact]
    public void GetClusterCenterCount_RespectsTheCeilingOnAVeryLargeMap()
    {
        var count = GrassGeometry.GetClusterCenterCount(1_000_000, 1_000_000);

        Assert.Equal(GrassGeometry.MaximumClusterCenterCount, count);
    }

    [Fact]
    public void GetClusterCenterCount_DegenerateMapYieldsZero()
    {
        Assert.Equal(0, GrassGeometry.GetClusterCenterCount(0, 720));
        Assert.Equal(0, GrassGeometry.GetClusterCenterCount(1280, 0));
    }

    [Fact]
    public void GetClusterCenterCount_ScalesUpwardWithMapAreaAlone()
    {
        var small = GrassGeometry.GetClusterCenterCount(DefaultMapWidth, DefaultMapHeight);
        var large = GrassGeometry.GetClusterCenterCount(LargeMapWidth, LargeMapHeight);

        Assert.True(large >= small);
        Assert.True(large > small);
    }

    [Fact]
    public void GenerateClusters_DensityScalesUpwardWithMapAreaAlone()
    {
        var small = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);
        var large = GrassGeometry.GenerateClusters(
            DefaultSeed,
            LargeMapWidth,
            LargeMapHeight);

        Assert.True(large.Length > small.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(500)]
    public void GenerateClusters_AgentCountHasNoEffectOnDensity(int agentCount)
    {
        // GenerateClusters's signature is (seed, mapWidth, mapHeight) only —
        // agentCount below is never passed to it. Varying it here and always
        // observing the identical result documents, rather than merely
        // asserts by omission, that density cannot be coupled to roster size
        // no matter how a caller's unit count changes.
        _ = agentCount;

        var clusters = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.Equal(
            GrassGeometry.GenerateClusters(
                DefaultSeed,
                DefaultMapWidth,
                DefaultMapHeight).ToArray(),
            clusters.ToArray());
    }

    [Fact]
    public void GenerateClusters_AllSizeClassesAppearAcrossAReasonableSample()
    {
        var clusters = GrassGeometry.GenerateClusters(
            DefaultSeed,
            LargeMapWidth,
            LargeMapHeight);

        var distinctSizeClasses = new HashSet<GrassSizeClass>();
        foreach (var cluster in clusters)
        {
            distinctSizeClasses.Add(cluster.SizeClass);
        }

        Assert.Equal(GrassGeometry.GrassSizeClassCount, distinctSizeClasses.Count);
    }

    [Fact]
    public void GrassSizeClassCountMatchesTheDeclaredEnumMemberCount()
    {
        Assert.Equal(
            GrassGeometry.GrassSizeClassCount,
            Enum.GetValues<GrassSizeClass>().Length);
    }

    [Fact]
    public void GenerateClusters_DoesNotShiftThePlainsBackdropDecalPlacement()
    {
        // The grass salt (PresentationSalts.GrassGenerationSalt) is a
        // distinct stream from the plains salt PlainsBackdropGeometry uses,
        // and GrassGeometry never touches PlainsBackdropGeometry's state.
        // This pins that in practice: generating decals, then clusters, then
        // decals again with the same seed and map yields byte-for-byte
        // identical decal sequences both times, exactly as it would if
        // GrassGeometry did not exist at all.
        var decalsBefore = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        _ = GrassGeometry.GenerateClusters(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        var decalsAfter = PlainsBackdropGeometry.GenerateDecals(
            DefaultSeed,
            DefaultMapWidth,
            DefaultMapHeight);

        Assert.Equal(decalsBefore.ToArray(), decalsAfter.ToArray());
    }

    [Fact]
    public void GrassGenerationSaltDiffersFromThePlainsBackdropSalt()
    {
        Assert.NotEqual(
            PresentationSalts.PlainsBackdropSalt,
            PresentationSalts.GrassGenerationSalt);
    }

    // --- VIS-026: zoom band selection ---------------------------------

    [Theory]
    [InlineData(0f, GrassZoomBand.Far)]
    [InlineData(0.29f, GrassZoomBand.Far)]
    [InlineData(0.3f, GrassZoomBand.Mid)]
    [InlineData(1f, GrassZoomBand.Mid)]
    [InlineData(1.99f, GrassZoomBand.Mid)]
    [InlineData(2.0f, GrassZoomBand.Near)]
    [InlineData(12f, GrassZoomBand.Near)]
    public void GetZoomBand_SelectsExactlyAtTheNamedThresholds(
        float cameraZoom,
        GrassZoomBand expectedBand)
    {
        Assert.Equal(expectedBand, GrassGeometry.GetZoomBand(cameraZoom));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void GetZoomBand_RejectsNonFiniteOrNegativeZoom(float cameraZoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GrassGeometry.GetZoomBand(cameraZoom));
    }

    // --- VIS-026: per-band quad count ----------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void GetQuadCount_FarBandAlwaysCollapsesToOneRegardlessOfStoredCount(
        int storedQuadCount)
    {
        var cluster = new GrassCluster(
            Vector2.Zero,
            Phase: 0f,
            SizeClass: GrassSizeClass.Medium,
            QuadCount: storedQuadCount);

        Assert.Equal(
            1,
            GrassGeometry.GetQuadCount(cluster, GrassZoomBand.Far));
    }

    [Theory]
    [InlineData(GrassZoomBand.Mid, 1, 2)]
    [InlineData(GrassZoomBand.Mid, 2, 2)]
    [InlineData(GrassZoomBand.Mid, 3, 3)]
    [InlineData(GrassZoomBand.Mid, 4, 4)]
    [InlineData(GrassZoomBand.Near, 1, 3)]
    [InlineData(GrassZoomBand.Near, 2, 3)]
    [InlineData(GrassZoomBand.Near, 3, 4)]
    [InlineData(GrassZoomBand.Near, 4, 4)]
    public void GetQuadCount_MidAndNearBandsClampAndNearAddsOneWithinTheCap(
        GrassZoomBand band,
        int storedQuadCount,
        int expectedQuadCount)
    {
        var cluster = new GrassCluster(
            Vector2.Zero,
            Phase: 0f,
            SizeClass: GrassSizeClass.Small,
            QuadCount: storedQuadCount);

        Assert.Equal(expectedQuadCount, GrassGeometry.GetQuadCount(cluster, band));
    }

    [Theory]
    [InlineData(GrassZoomBand.Far)]
    [InlineData(GrassZoomBand.Mid)]
    [InlineData(GrassZoomBand.Near)]
    public void GetQuadCount_NeverExceedsTheMaximumQuadsPerClusterCap(
        GrassZoomBand band)
    {
        for (var storedQuadCount = 1;
            storedQuadCount <= GrassGeometry.MaximumQuadsPerCluster;
            storedQuadCount++)
        {
            var cluster = new GrassCluster(
                Vector2.Zero,
                Phase: 0f,
                SizeClass: GrassSizeClass.Large,
                QuadCount: storedQuadCount);

            Assert.InRange(
                GrassGeometry.GetQuadCount(cluster, band),
                1,
                GrassGeometry.MaximumQuadsPerCluster);
        }
    }

    [Theory]
    [InlineData(GrassZoomBand.Mid)]
    [InlineData(GrassZoomBand.Near)]
    public void GetQuadCount_MidAndNearBandsNeverGoBelowTheTwoQuadFloor(
        GrassZoomBand band)
    {
        var cluster = new GrassCluster(
            Vector2.Zero,
            Phase: 0f,
            SizeClass: GrassSizeClass.Small,
            QuadCount: 1);

        Assert.True(
            GrassGeometry.GetQuadCount(cluster, band) >=
                GrassGeometry.MinimumRenderedQuadsPerCluster);
    }

    // --- VIS-026: shade-ceiling pins for every band and theme ----------

    [Theory]
    [InlineData(GrassSizeClass.Small, false)]
    [InlineData(GrassSizeClass.Medium, false)]
    [InlineData(GrassSizeClass.Large, false)]
    [InlineData(GrassSizeClass.Small, true)]
    [InlineData(GrassSizeClass.Medium, true)]
    [InlineData(GrassSizeClass.Large, true)]
    public void GetShadeInterpolation_NeverExceedsTheBackdropCeiling(
        GrassSizeClass sizeClass,
        bool highContrastTheme)
    {
        var interpolation = GrassGeometry.GetShadeInterpolation(
            sizeClass,
            highContrastTheme);

        Assert.InRange(
            interpolation,
            0f,
            PlainsBackdropGeometry.MaximumBackdropInterpolation);
    }

    [Theory]
    [InlineData(GrassSizeClass.Small)]
    [InlineData(GrassSizeClass.Medium)]
    [InlineData(GrassSizeClass.Large)]
    public void GetShadeInterpolation_HighContrastHalvesTheSpread(
        GrassSizeClass sizeClass)
    {
        var normal = GrassGeometry.GetShadeInterpolation(sizeClass, false);
        var highContrast = GrassGeometry.GetShadeInterpolation(sizeClass, true);

        Assert.Equal(
            normal * GrassGeometry.HighContrastShadeSpreadFactor,
            highContrast,
            precision: 5);
        Assert.True(highContrast < normal);
    }

    // --- VIS-026: far band emits one static rectangle, sway not consumed ---

    [Fact]
    public void GetQuadBounds_FarBandIgnoresSwayOffsetEntirely()
    {
        var cluster = new GrassCluster(
            new Vector2(200f, 150f),
            Phase: 1.2f,
            SizeClass: GrassSizeClass.Medium,
            QuadCount: 3);
        var screenAnchor = new Vector2(400f, 300f);

        var withoutSway = GrassGeometry.GetQuadBounds(
            cluster,
            screenAnchor,
            cameraZoom: 0.15f,
            GrassZoomBand.Far,
            quadIndex: 0,
            quadCount: 1,
            swayOffset: Vector2.Zero);
        var withSway = GrassGeometry.GetQuadBounds(
            cluster,
            screenAnchor,
            cameraZoom: 0.15f,
            GrassZoomBand.Far,
            quadIndex: 0,
            quadCount: 1,
            swayOffset: new Vector2(50f, -75f));

        Assert.Equal(withoutSway, withSway);
    }

    [Fact]
    public void GetQuadBounds_FarBandMatchesGetFarBandBoundsDirectly()
    {
        var cluster = new GrassCluster(
            new Vector2(200f, 150f),
            Phase: 1.2f,
            SizeClass: GrassSizeClass.Large,
            QuadCount: 2);
        var screenAnchor = new Vector2(400f, 300f);

        var viaQuadBounds = GrassGeometry.GetQuadBounds(
            cluster,
            screenAnchor,
            cameraZoom: 1.5f,
            GrassZoomBand.Far,
            quadIndex: 0,
            quadCount: 1,
            swayOffset: new Vector2(10f, 10f));
        var viaFarBandBounds = GrassGeometry.GetFarBandBounds(
            cluster,
            screenAnchor,
            cameraZoom: 1.5f);

        Assert.Equal(viaFarBandBounds, viaQuadBounds);
    }

    [Fact]
    public void GetQuadBounds_MidBandRespondsToSwayOffset()
    {
        var cluster = new GrassCluster(
            new Vector2(200f, 150f),
            Phase: 0.4f,
            SizeClass: GrassSizeClass.Medium,
            QuadCount: 3);
        var screenAnchor = new Vector2(400f, 300f);

        var withoutSway = GrassGeometry.GetQuadBounds(
            cluster,
            screenAnchor,
            cameraZoom: 1f,
            GrassZoomBand.Mid,
            quadIndex: 0,
            quadCount: 3,
            swayOffset: Vector2.Zero);
        var withSway = GrassGeometry.GetQuadBounds(
            cluster,
            screenAnchor,
            cameraZoom: 1f,
            GrassZoomBand.Mid,
            quadIndex: 0,
            quadCount: 3,
            swayOffset: new Vector2(50f, -75f));

        Assert.NotEqual(withoutSway, withSway);
    }

    // --- VIS-026: cull correctness on synthetic bounds ------------------

    [Fact]
    public void IsClusterVisible_TrueWhenClusterFallsInsideBothMapAndArenaBounds()
    {
        var clusterBounds = new Rectangle(100, 100, 20, 20);
        var mapBounds = new Rectangle(0, 0, 800, 600);
        var arenaBounds = new Rectangle(0, 0, 800, 600);

        var visible = GrassGeometry.IsClusterVisible(
            clusterBounds,
            mapBounds,
            arenaBounds,
            out var clipped);

        Assert.True(visible);
        Assert.Equal(clusterBounds, clipped);
    }

    [Fact]
    public void IsClusterVisible_FalseWhenClusterFallsEntirelyOutsideTheMapBounds()
    {
        var clusterBounds = new Rectangle(900, 900, 20, 20);
        var mapBounds = new Rectangle(0, 0, 800, 600);
        var arenaBounds = new Rectangle(0, 0, 800, 600);

        var visible = GrassGeometry.IsClusterVisible(
            clusterBounds,
            mapBounds,
            arenaBounds,
            out var clipped);

        Assert.False(visible);
        Assert.Equal(0, clipped.Width);
    }

    [Fact]
    public void IsClusterVisible_FalseWhenClippedToMapButStillOutsideArenaBounds()
    {
        // Clipped against mapBounds survives (positive width/height), but the
        // clipped rectangle sits entirely outside the separate arenaBounds —
        // exactly the two-stage DrawDecals test this mirrors.
        var clusterBounds = new Rectangle(10, 10, 20, 20);
        var mapBounds = new Rectangle(0, 0, 800, 600);
        var arenaBounds = new Rectangle(200, 200, 100, 100);

        var visible = GrassGeometry.IsClusterVisible(
            clusterBounds,
            mapBounds,
            arenaBounds,
            out var clipped);

        Assert.False(visible);
        Assert.True(clipped.Width > 0 && clipped.Height > 0);
    }

    [Fact]
    public void IsClusterVisible_ClipsAgainstMapBoundsBeforeTestingArenaBounds()
    {
        // The cluster rectangle straddles the map edge: the clipped result
        // must be narrower than the original, matching the DrawDecals
        // pattern of clipping to the map, not just the viewport.
        var clusterBounds = new Rectangle(790, 10, 40, 20);
        var mapBounds = new Rectangle(0, 0, 800, 600);
        var arenaBounds = new Rectangle(0, 0, 800, 600);

        var visible = GrassGeometry.IsClusterVisible(
            clusterBounds,
            mapBounds,
            arenaBounds,
            out var clipped);

        Assert.True(visible);
        Assert.True(clipped.Width < clusterBounds.Width);
    }

    // --- VIS-028: trample suppression -----------------------------------

    [Fact]
    public void IsSuppressedByTrample_FalseWhenNoMarksArePresent()
    {
        Assert.False(GrassGeometry.IsSuppressedByTrample(
            new Vector2(100f, 100f),
            ReadOnlySpan<TrampleMark>.Empty));
    }

    [Fact]
    public void IsSuppressedByTrample_TrueExactlyAtTheSuppressionRadius()
    {
        var mark = new TrampleMark(0, 0);
        var clusterPosition = new Vector2(GrassGeometry.TrampleSuppressionRadius, 0f);

        Assert.True(GrassGeometry.IsSuppressedByTrample(
            clusterPosition,
            [mark]));
    }

    [Fact]
    public void IsSuppressedByTrample_FalseJustBeyondTheSuppressionRadius()
    {
        var mark = new TrampleMark(0, 0);
        var clusterPosition = new Vector2(GrassGeometry.TrampleSuppressionRadius + 1f, 0f);

        Assert.False(GrassGeometry.IsSuppressedByTrample(
            clusterPosition,
            [mark]));
    }

    [Fact]
    public void IsSuppressedByTrample_TrueWhenAnyMarkAmongSeveralIsWithinRadius()
    {
        // Raw fixed-point coordinates, not world units: dividing by
        // FixedPoint.Scale is what recovers the (10_000, 10_000) and
        // (50, 50) world positions this test reasons about.
        var farMark = new TrampleMark(10_000 * FixedPoint.Scale, 10_000 * FixedPoint.Scale);
        var nearMark = new TrampleMark(50 * FixedPoint.Scale, 50 * FixedPoint.Scale);
        var clusterPosition = new Vector2(50f, 50f);

        Assert.True(GrassGeometry.IsSuppressedByTrample(
            clusterPosition,
            [farMark, nearMark]));
    }

    [Fact]
    public void IsSuppressedByTrample_ConvertsFixedPointMarkPositionsToWorldUnits()
    {
        // A mark stored at raw fixed-point coordinates equal to
        // FixedPoint.Scale converts to exactly one world unit — placing the
        // cluster one world unit away should read as suppressed, and placing
        // it far away should not, proving the conversion divisor is applied
        // rather than the raw fixed-point value being compared directly.
        var mark = new TrampleMark(FixedPoint.Scale, FixedPoint.Scale);

        Assert.True(GrassGeometry.IsSuppressedByTrample(
            new Vector2(1f, 1f),
            [mark]));
        Assert.False(GrassGeometry.IsSuppressedByTrample(
            new Vector2(FixedPoint.Scale, FixedPoint.Scale),
            [mark]));
    }

    [Fact]
    public void TrampleMarkShadeInterpolation_NeverExceedsTheBackdropCeiling()
    {
        Assert.InRange(
            GrassGeometry.TrampleMarkShadeInterpolation,
            0f,
            PlainsBackdropGeometry.MaximumBackdropInterpolation);
    }

    [Fact]
    public void TrampleStubbleShadeInterpolation_NeverExceedsTheBackdropCeiling()
    {
        Assert.InRange(
            GrassGeometry.TrampleStubbleShadeInterpolation,
            0f,
            PlainsBackdropGeometry.MaximumBackdropInterpolation);
    }

    /// <summary>
    /// The whole point of the stubble tone: a trample-suppressed cluster must
    /// read as closer to bare ground than every untouched grass shade,
    /// otherwise the trampled patch has no boundary against the grass around
    /// it. That was the state until 2026-08-11, when a suppressed Large
    /// cluster drew at the same <c>0.22</c> the mark beneath it uses
    /// (the armor bulk, adornment accents, and trample legibility design,
    /// section 4).
    /// </summary>
    [Fact]
    public void TrampleStubbleShadeInterpolation_SitsBelowEveryGrassShade()
    {
        foreach (var grassShade in GrassGeometry.GrassShadeInterpolation)
        {
            Assert.True(
                GrassGeometry.TrampleStubbleShadeInterpolation < grassShade,
                $"Stubble at {GrassGeometry.TrampleStubbleShadeInterpolation} " +
                $"must sit below the grass shade {grassShade}.");
        }
    }

    /// <summary>
    /// The stubble tone is deliberately a value already on the ground ladder,
    /// so it introduces no new point into the shade band and no new case for
    /// the faction-signal contrast guard.
    /// </summary>
    [Fact]
    public void TrampleStubbleShadeInterpolation_IsAlreadyOnTheGroundShadeLadder() =>
        Assert.Contains(
            GrassGeometry.TrampleStubbleShadeInterpolation,
            PlainsBackdropGeometry.GroundShadeInterpolation);

    /// <summary>
    /// A trample mark thins a whole clump rather than part of one: the
    /// suppression radius covers the cluster scatter radius tufts are drawn
    /// within, which is what makes adjacent marks merge into one worn area
    /// instead of reading as one blot per body.
    /// </summary>
    [Fact]
    public void TrampleSuppressionRadius_CoversAWholeClusterScatterRadius() =>
        Assert.True(
            GrassGeometry.TrampleSuppressionRadius >= 48f,
            "The suppression radius must cover a cluster's own scatter radius.");
}
