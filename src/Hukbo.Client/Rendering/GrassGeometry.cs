using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Coarse visual size bucket for one grass cluster, drawn from the same
/// generation stream as its position and phase (battlefield-environment-
/// design.md, "Grass clusters"). A future rendering task maps this to the
/// actual quad geometry; this type stores only the deterministic choice,
/// never the derived shapes, so nothing generated here has to change when
/// the render-time quad layout is tuned.
/// </summary>
public enum GrassSizeClass
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// One placed grass cluster: a scattered tuft position within a clump drawn
/// around one of the generation's cluster centers, plus everything a future
/// renderer needs to draw it without re-deriving from the generation stream.
/// Roughly 16 bytes per entry, held in a flat array — no nested collection,
/// matching <see cref="PlainsDecal"/>'s shape.
/// </summary>
/// <param name="WorldPosition">
/// Final scattered position in world units, already clipped to the map
/// rectangle and the grass-free border margin
/// (<see cref="GrassGeometry.GrassFreeBorderMargin"/>).
/// </param>
/// <param name="Phase">
/// The cluster's sway phase offset in radians, drawn from the same
/// generation stream so two replays of the same seed assign identical
/// phases (R-W5.3). Unread until a future sway renderer consumes it.
/// </param>
/// <param name="SizeClass">The cluster's size bucket.</param>
/// <param name="QuadCount">
/// How many of the up to <see cref="GrassGeometry.MaximumQuadsPerCluster"/>
/// quads this cluster renders with, drawn from the same stream.
/// </param>
internal readonly record struct GrassCluster(
    Vector2 WorldPosition,
    float Phase,
    GrassSizeClass SizeClass,
    int QuadCount);

/// <summary>
/// The three zoom bands grass clusters render at, selected purely by camera
/// zoom (battlefield-environment-design.md, "LOD by zoom", R-W5.6). Far
/// collapses a cluster to a single static rectangle with sway fully off; mid
/// draws the full cluster with sway; near draws the full cluster with sway
/// and may add one extra silhouette quad within
/// <see cref="GrassGeometry.MaximumQuadsPerCluster"/>.
/// </summary>
public enum GrassZoomBand
{
    Far,
    Mid,
    Near,
}

/// <summary>
/// Pure generation helper for battlefield grass clusters (battlefield-
/// environment-design.md, "Grass clusters"). Two-level deterministic
/// placement: cluster centers are scattered uniformly across the map, then
/// for each center a per-center tuft count is drawn and tufts are scattered
/// around it with square-root radial falloff (biasing density toward the
/// center, producing clumps with deliberate empty ground between them
/// rather than uniform noise, R-W4.11). Everything is drawn from one
/// <see cref="SplitMix64"/> stream seeded by <c>Scenario.Seed</c> XOR
/// <see cref="PresentationSalts.GrassGenerationSalt"/> — a salt distinct
/// from every stream <see cref="PlainsBackdropGeometry"/> uses, so today's
/// ground shading and decal placement are unaffected by anything in this
/// type. Called exactly twice per match (scenario construction and reset),
/// mirroring <see cref="PlainsBackdropGeometry.GenerateDecals"/> — never per
/// tick or per frame (R-X.14). Touches only value types, so it is fully unit
/// tested with no graphics device.
/// </summary>
internal static class GrassGeometry
{
    /// <summary>
    /// Lower bound on the area-scaled cluster-center count, holding even on
    /// a very small map.
    /// </summary>
    public const int MinimumClusterCenterCount = 24;

    /// <summary>
    /// Upper bound on the area-scaled cluster-center count, holding even on
    /// a very large map.
    /// </summary>
    public const int MaximumClusterCenterCount = 48;

    /// <summary>
    /// Hard ceiling on the total number of generated clusters, regardless of
    /// cluster-center count or per-center draws. Density must never grow
    /// without bound as maps get larger — this is the safety valve a
    /// worst-case combination of <see cref="MaximumClusterCenterCount"/> and
    /// <see cref="MaximumTuftsPerClusterCenter"/> would otherwise exceed.
    /// </summary>
    public const int MaximumClusterCount = 320;

    /// <summary>
    /// Hard ceiling on how many quads one cluster renders with. Enforced at
    /// generation time on <see cref="GrassCluster.QuadCount"/> so a future
    /// renderer can trust the stored value without re-clamping.
    /// </summary>
    public const int MaximumQuadsPerCluster = 4;

    /// <summary>
    /// PROVISIONAL: named cap on the per-center random tuft draw. Combined
    /// with the <see cref="MinimumClusterCenterCount"/>–
    /// <see cref="MaximumClusterCenterCount"/> range, a typical map's total
    /// stays well under <see cref="MaximumClusterCount"/>, while the
    /// worst-case combination (every center at
    /// <see cref="MaximumClusterCenterCount"/>, every draw at this cap)
    /// exceeds it — exactly the case <see cref="MaximumClusterCount"/>
    /// exists to catch.
    /// </summary>
    public const int MaximumTuftsPerClusterCenter = 12;

    /// <summary>
    /// Grass-free margin, in world units, kept just inside the map border so
    /// the border stays the strongest line on the field (battlefield-
    /// environment-design.md, "Boundary readability"). Exactly one ground
    /// cell, tied to
    /// <see cref="PlainsBackdropGeometry.TargetGroundCellSize"/> rather than
    /// a duplicated literal.
    /// </summary>
    public const int GrassFreeBorderMargin = PlainsBackdropGeometry.TargetGroundCellSize;

    /// <summary>
    /// Number of declared <see cref="GrassSizeClass"/> members. Named rather
    /// than reflected at generation time (<c>Enum.GetValues</c> would
    /// allocate a fresh array on every tuft) and pinned equal to
    /// <see cref="GrassSizeClass"/>'s member count by test, the same
    /// convention <see cref="PlainsBackdropGeometry.DecalKindInterpolation"/>
    /// uses for <see cref="PlainsDecalKind"/>.
    /// </summary>
    internal const int GrassSizeClassCount = 3;

    /// <summary>
    /// Below this camera zoom, clusters draw as a single static rectangle
    /// with sway fully off — sub-pixel motion at this range is pure flicker
    /// (battlefield-environment-design.md, "LOD by zoom", R-W5.6). Exact
    /// value, not merely "roughly", per the requirement's named-constant
    /// obligation.
    /// </summary>
    public const float FarZoomThreshold = 0.3f;

    /// <summary>
    /// At or above this camera zoom, clusters draw with sway on and may add
    /// one extra silhouette quad within <see cref="MaximumQuadsPerCluster"/>
    /// (battlefield-environment-design.md, "LOD by zoom", R-W5.6).
    /// </summary>
    public const float NearZoomThreshold = 2.0f;

    /// <summary>
    /// Floor on how many quads a cluster renders with outside the far band,
    /// matching the design's "two to four tinted rectangles" framing even on
    /// a cluster whose generated <see cref="GrassCluster.QuadCount"/> drew as
    /// low as one.
    /// </summary>
    internal const int MinimumRenderedQuadsPerCluster = 2;

    /// <summary>
    /// PROVISIONAL. Factor applied to the shade interpolation under the
    /// high-contrast theme: halves the spread rather than eliminating it,
    /// reusing that theme's existing eliminate-visual-noise purpose as the
    /// trigger instead of inventing a new flag (R-W5.7).
    /// </summary>
    internal const float HighContrastShadeSpreadFactor = 0.5f;

    /// <summary>
    /// The three grass shade interpolation values, indexed by
    /// <see cref="GrassCluster.SizeClass"/>, all at or below
    /// <see cref="PlainsBackdropGeometry.MaximumBackdropInterpolation"/>
    /// (R-W4.2). Static readonly, so this allocates once rather than per
    /// frame, matching <see cref="PlainsBackdropGeometry.GroundShadeInterpolation"/>.
    /// </summary>
    internal static readonly ImmutableArray<float> GrassShadeInterpolation =
        [0.14f, 0.18f, 0.22f];

    // PROVISIONAL: per-GrassSizeClass multiplier applied both to the
    // far-band decal-style scale factor and the mid/near quad base size, so
    // a Large cluster reads larger than a Small one at every band.
    private static readonly ImmutableArray<float> SizeClassScaleMultiplier =
        [0.75f, 1.0f, 1.35f];

    // PROVISIONAL: base half-extents, in screen pixels at apparent scale 1,
    // for one grass-blade quad in the mid/near bands. Smaller than
    // PlainsBackdropGeometry.DecalBaseSize because several of these quads
    // compose one cluster rather than standing alone.
    private const float QuadBaseWidth = 3f;
    private const float QuadBaseHeight = 9f;

    // PROVISIONAL: screen-pixel radius, at apparent scale 1, the fan of
    // quads in one cluster spreads around its screen anchor.
    private const float QuadFanRadius = 4f;

    // PROVISIONAL: world area allotted to one additional cluster center
    // above the floor. Tuned only for a plausible scaling curve across the
    // scenario map sizes this package expects — not a historical or
    // measured value.
    private const double ClusterCenterWorldAreaDivisor = 150_000d;

    // PROVISIONAL: scatter radius, in world units, tufts are drawn around
    // their cluster center.
    private const float ClusterScatterRadius = 48f;

    // 24-bit precision unit-interval conversion, matching the convention
    // PlainsBackdropGeometry and HitEffectGeometry.SeedAngle already use.
    private const double UnitScale = 1.0 / 16_777_216.0;

    /// <summary>
    /// The area-scaled cluster-center count for a map of the given
    /// dimensions, clamped to <see cref="MinimumClusterCenterCount"/>–
    /// <see cref="MaximumClusterCenterCount"/>. Exposed separately so tests
    /// can pin the scaling curve without re-deriving it from
    /// <see cref="GenerateClusters"/>'s total output.
    /// </summary>
    public static int GetClusterCenterCount(int mapWidth, int mapHeight)
    {
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return 0;
        }

        var worldArea = (double)mapWidth * mapHeight;
        return (int)Math.Clamp(
            worldArea / ClusterCenterWorldAreaDivisor,
            MinimumClusterCenterCount,
            MaximumClusterCenterCount);
    }

    /// <summary>
    /// Generates the flat cluster array for one scenario. Seeded from
    /// <paramref name="seed"/> XOR
    /// <see cref="PresentationSalts.GrassGenerationSalt"/> using
    /// <see cref="SplitMix64"/>, so two calls with the same seed and map
    /// dimensions always reproduce the same sequence — identical positions,
    /// phases, size classes, quad counts, and total count. Density scales
    /// only with map area, through <see cref="GetClusterCenterCount"/>;
    /// nothing here reads agent count, frame rate, or any other external
    /// value, so a caller cannot accidentally couple grass density to either.
    /// Called exactly twice per match (scenario construction and reset) —
    /// never per tick or per frame.
    /// </summary>
    public static ImmutableArray<GrassCluster> GenerateClusters(
        ulong seed,
        int mapWidth,
        int mapHeight)
    {
        var centerCount = GetClusterCenterCount(mapWidth, mapHeight);
        if (centerCount == 0)
        {
            return [];
        }

        // The grass-free border margin bounds both cluster-center placement
        // and every scattered tuft position (boundary readability): on a
        // map narrower than two margins, both edges collapse onto the same
        // clamped line rather than producing an inverted range.
        var minX = (float)GrassFreeBorderMargin;
        var maxX = MathF.Max(minX, mapWidth - GrassFreeBorderMargin);
        var minY = (float)GrassFreeBorderMargin;
        var maxY = MathF.Max(minY, mapHeight - GrassFreeBorderMargin);

        var rng = new SplitMix64(seed ^ PresentationSalts.GrassGenerationSalt);
        var clusters = ImmutableArray.CreateBuilder<GrassCluster>();

        for (var centerIndex = 0;
            centerIndex < centerCount && clusters.Count < MaximumClusterCount;
            centerIndex++)
        {
            var centerX = minX + (float)(ToUnitInterval(rng.NextUInt64()) * (maxX - minX));
            var centerY = minY + (float)(ToUnitInterval(rng.NextUInt64()) * (maxY - minY));
            var tuftCount = 1 + rng.NextInt(MaximumTuftsPerClusterCenter);

            for (var tuftIndex = 0;
                tuftIndex < tuftCount && clusters.Count < MaximumClusterCount;
                tuftIndex++)
            {
                var angle = (float)(ToUnitInterval(rng.NextUInt64()) * MathF.Tau);

                // Radius times the square root of a unit draw: biases
                // scattered tufts toward the center rather than spreading
                // them uniformly across the disc (battlefield-environment-
                // design.md, "Grass clusters").
                var radiusUnit = Math.Sqrt(ToUnitInterval(rng.NextUInt64()));
                var radius = (float)radiusUnit * ClusterScatterRadius;

                var worldX = Math.Clamp(
                    centerX + (MathF.Cos(angle) * radius),
                    minX,
                    maxX);
                var worldY = Math.Clamp(
                    centerY + (MathF.Sin(angle) * radius),
                    minY,
                    maxY);
                var phase = (float)(ToUnitInterval(rng.NextUInt64()) * MathF.Tau);
                var sizeClass = (GrassSizeClass)rng.NextInt(GrassSizeClassCount);
                var quadCount = 1 + rng.NextInt(MaximumQuadsPerCluster);

                clusters.Add(new GrassCluster(
                    new Vector2(worldX, worldY),
                    phase,
                    sizeClass,
                    quadCount));
            }
        }

        return clusters.ToImmutable();
    }

    /// <summary>
    /// The zoom band <paramref name="cameraZoom"/> falls in, per the three
    /// named bands at <see cref="FarZoomThreshold"/> and
    /// <see cref="NearZoomThreshold"/> (R-W5.6). Pure and allocation free.
    /// </summary>
    public static GrassZoomBand GetZoomBand(float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        if (cameraZoom < FarZoomThreshold)
        {
            return GrassZoomBand.Far;
        }

        return cameraZoom < NearZoomThreshold
            ? GrassZoomBand.Mid
            : GrassZoomBand.Near;
    }

    /// <summary>
    /// How many quads <paramref name="cluster"/> draws with in
    /// <paramref name="band"/>: the far band collapses every cluster to the
    /// single static rectangle its LOD row specifies; the mid and near bands
    /// clamp the cluster's generated <see cref="GrassCluster.QuadCount"/> up
    /// to <see cref="MinimumRenderedQuadsPerCluster"/>, and the near band may
    /// add one further silhouette quad within
    /// <see cref="MaximumQuadsPerCluster"/> (R-W5.6). Pure and allocation
    /// free.
    /// </summary>
    public static int GetQuadCount(GrassCluster cluster, GrassZoomBand band)
    {
        if (band == GrassZoomBand.Far)
        {
            return 1;
        }

        var baseCount = Math.Clamp(
            cluster.QuadCount,
            MinimumRenderedQuadsPerCluster,
            MaximumQuadsPerCluster);

        return band == GrassZoomBand.Near
            ? Math.Min(MaximumQuadsPerCluster, baseCount + 1)
            : baseCount;
    }

    /// <summary>
    /// The shade interpolation for one cluster: its size class's entry in
    /// <see cref="GrassShadeInterpolation"/>, halved under the high-contrast
    /// theme by <see cref="HighContrastShadeSpreadFactor"/> (R-W5.7). Always
    /// stays at or below
    /// <see cref="PlainsBackdropGeometry.MaximumBackdropInterpolation"/>
    /// (R-W4.2). Pure and allocation free — the caller resolves the actual
    /// <c>Color</c> via <c>Color.Lerp(ArenaSurface, ArenaBorder, ...)</c>,
    /// matching <see cref="PlainsBackdropGeometry"/>'s split between
    /// interpolation values and display-time color resolution.
    /// </summary>
    public static float GetShadeInterpolation(
        GrassSizeClass sizeClass,
        bool highContrastTheme)
    {
        var interpolation = GrassShadeInterpolation[(int)sizeClass];
        return highContrastTheme
            ? interpolation * HighContrastShadeSpreadFactor
            : interpolation;
    }

    // --- VIS-028: trample suppression ----------------------------------

    /// <summary>
    /// PROVISIONAL: world-unit radius around a trample mark within which a
    /// grass cluster is suppressed — reduced height, zero sway
    /// (battlefield-environment-design.md, "Trampled and sparse areas",
    /// R-W4.7, R-W5.9). Chosen relative to <see cref="ClusterScatterRadius"/>
    /// so one mark visibly thins the clump nearest a fallen agent without
    /// reaching neighboring clusters.
    /// </summary>
    public const float TrampleSuppressionRadius = 40f;

    /// <summary>
    /// The trample mark's own shade interpolation toward
    /// <c>ArenaBorder</c> — darker than every grass shade in
    /// <see cref="GrassShadeInterpolation"/>, still at or below
    /// <see cref="PlainsBackdropGeometry.MaximumBackdropInterpolation"/>
    /// (R-W4.2). Pinned by test.
    /// </summary>
    internal const float TrampleMarkShadeInterpolation = 0.22f;

    /// <summary>
    /// True when <paramref name="clusterWorldPosition"/> lies within
    /// <see cref="TrampleSuppressionRadius"/> of any mark in
    /// <paramref name="trampleMarks"/> (battlefield-environment-design.md,
    /// "Trampled and sparse areas"). <see cref="GrassSway.ResolveAmplitudeFactor"/>
    /// consumes the result to zero sway (R-W5.9); the renderer consumes it
    /// separately to draw a reduced height. Converts each mark's fixed-point
    /// position with the same <see cref="FixedPoint.Scale"/> divisor
    /// <c>BloodRenderer</c> uses, so this stays in the same world-unit space
    /// as <paramref name="clusterWorldPosition"/> without the caller having
    /// to pre-convert a separate array. Pure and allocation free.
    /// </summary>
    public static bool IsSuppressedByTrample(
        Vector2 clusterWorldPosition,
        ReadOnlySpan<TrampleMark> trampleMarks)
    {
        var radiusSquared = TrampleSuppressionRadius * TrampleSuppressionRadius;
        for (var index = 0; index < trampleMarks.Length; index++)
        {
            var mark = trampleMarks[index];
            var deltaX = clusterWorldPosition.X - (mark.XRaw / (float)FixedPoint.Scale);
            var deltaY = clusterWorldPosition.Y - (mark.YRaw / (float)FixedPoint.Scale);
            if ((deltaX * deltaX) + (deltaY * deltaY) <= radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The far-band on-screen rectangle for one cluster: a single static
    /// rectangle in today's decal form (battlefield-environment-design.md,
    /// "LOD by zoom"), reusing
    /// <see cref="PlainsBackdropGeometry.GetDecalScreenBounds"/> so the clamp
    /// and rounding rules cannot drift from the decal path. There is no sway
    /// parameter here, so it is structurally impossible for the far band to
    /// consume sway (R-W5.6).
    /// </summary>
    public static Rectangle GetFarBandBounds(
        GrassCluster cluster,
        Vector2 screenAnchor,
        float cameraZoom) =>
        PlainsBackdropGeometry.GetDecalScreenBounds(
            screenAnchor,
            cameraZoom,
            SizeClassScaleMultiplier[(int)cluster.SizeClass]);

    /// <summary>
    /// The on-screen rectangle for quad <paramref name="quadIndex"/> of
    /// <paramref name="quadCount"/> in the mid or near band: quads fan around
    /// <paramref name="screenAnchor"/> at an angle derived from the cluster's
    /// generation-time <see cref="GrassCluster.Phase"/> and the quad index,
    /// so two replays of the same seed fan identically (R-W5.3).
    /// <paramref name="swayOffset"/> is a pure additive input —
    /// <see cref="Vector2.Zero"/> until VIS-030/031 supply the real sway
    /// clock — never computed here.
    /// </summary>
    public static Rectangle GetClusterQuadBounds(
        GrassCluster cluster,
        Vector2 screenAnchor,
        float cameraZoom,
        int quadIndex,
        int quadCount,
        Vector2 swayOffset)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        if (quadCount <= 0 || quadCount > MaximumQuadsPerCluster)
        {
            throw new ArgumentOutOfRangeException(nameof(quadCount));
        }

        if ((uint)quadIndex >= (uint)quadCount)
        {
            throw new ArgumentOutOfRangeException(nameof(quadIndex));
        }

        var apparentScale = Math.Clamp(
            cameraZoom,
            PlainsBackdropGeometry.MinimumDecalApparentScale,
            PlainsBackdropGeometry.MaximumDecalApparentScale);
        var sizeMultiplier = SizeClassScaleMultiplier[(int)cluster.SizeClass];
        var width = Math.Max(1f, QuadBaseWidth * sizeMultiplier * apparentScale);
        var height = Math.Max(1f, QuadBaseHeight * sizeMultiplier * apparentScale);

        var angle = cluster.Phase + (MathF.Tau * quadIndex / quadCount);
        var radius = QuadFanRadius * apparentScale;
        var center = screenAnchor +
            new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius) +
            swayOffset;

        return new Rectangle(
            (int)MathF.Round(center.X - (width / 2f)),
            (int)MathF.Round(center.Y - (height / 2f)),
            Math.Max(1, (int)MathF.Round(width)),
            Math.Max(1, (int)MathF.Round(height)));
    }

    /// <summary>
    /// The on-screen rectangle for quad <paramref name="quadIndex"/> of
    /// <paramref name="quadCount"/> in <paramref name="band"/> — the single
    /// entry point <c>GrassRenderer</c> calls per quad. Routes to
    /// <see cref="GetFarBandBounds"/> for the far band (where
    /// <paramref name="swayOffset"/> is accepted only for a uniform call
    /// shape and never applied — varying it cannot move the result, which is
    /// the far band's "sway fully off" guarantee made testable) and to
    /// <see cref="GetClusterQuadBounds"/> otherwise.
    /// </summary>
    public static Rectangle GetQuadBounds(
        GrassCluster cluster,
        Vector2 screenAnchor,
        float cameraZoom,
        GrassZoomBand band,
        int quadIndex,
        int quadCount,
        Vector2 swayOffset) =>
        band == GrassZoomBand.Far
            ? GetFarBandBounds(cluster, screenAnchor, cameraZoom)
            : GetClusterQuadBounds(
                cluster,
                screenAnchor,
                cameraZoom,
                quadIndex,
                quadCount,
                swayOffset);

    /// <summary>
    /// A bounding rectangle around every quad the cluster could draw in
    /// <paramref name="band"/>, used only for the cheap per-cluster cull test
    /// before the per-quad loop runs — never for the actual quad bounds.
    /// Deliberately generous (fan radius plus quad extent) rather than tight,
    /// since a false positive costs one skipped intersection test and a
    /// false negative would drop a visible cluster.
    /// </summary>
    public static Rectangle GetClusterCullBounds(
        GrassCluster cluster,
        Vector2 screenAnchor,
        float cameraZoom,
        GrassZoomBand band)
    {
        if (band == GrassZoomBand.Far)
        {
            return GetFarBandBounds(cluster, screenAnchor, cameraZoom);
        }

        var apparentScale = Math.Clamp(
            cameraZoom,
            PlainsBackdropGeometry.MinimumDecalApparentScale,
            PlainsBackdropGeometry.MaximumDecalApparentScale);
        var sizeMultiplier = SizeClassScaleMultiplier[(int)cluster.SizeClass];
        var extent = (QuadFanRadius + QuadBaseHeight) * sizeMultiplier * apparentScale;
        var size = Math.Max(1, (int)MathF.Round(extent * 2f));

        return new Rectangle(
            (int)MathF.Round(screenAnchor.X - (size / 2f)),
            (int)MathF.Round(screenAnchor.Y - (size / 2f)),
            size,
            size);
    }

    /// <summary>
    /// True when <paramref name="clusterBounds"/> survives the same
    /// clip-then-intersect test <c>PlainsBackdropRenderer.DrawDecals</c>
    /// uses: intersected against <paramref name="mapBounds"/> first (so a
    /// cluster near a map edge never bleeds past the border), then tested
    /// against <paramref name="arenaBounds"/>. Pure and allocation free — no
    /// spatial index, matching the decal precedent at this cluster count
    /// (battlefield-environment-design.md, "Culling").
    /// </summary>
    public static bool IsClusterVisible(
        Rectangle clusterBounds,
        Rectangle mapBounds,
        Rectangle arenaBounds,
        out Rectangle clippedBounds)
    {
        clippedBounds = Rectangle.Intersect(clusterBounds, mapBounds);
        return clippedBounds.Width > 0 &&
            clippedBounds.Height > 0 &&
            arenaBounds.Intersects(clippedBounds);
    }

    private static double ToUnitInterval(ulong value) =>
        (value & 0xFFFFFFUL) * UnitScale;
}
