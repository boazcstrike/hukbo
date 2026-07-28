using System.Collections.Immutable;
using Hukbo.Client.Presentation;
using Hukbo.Core.Determinism;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// One ground-grid cell. <see cref="Bounds"/> is in screen space (the same
/// space the projected map rectangle passed to
/// <see cref="PlainsBackdropGeometry.GetGroundCell"/> is expressed in).
/// </summary>
internal readonly record struct PlainsGroundCell(Rectangle Bounds, int ShadeIndex);

internal enum PlainsDecalKind
{
    GrassTuft,
    DirtPatch,
    Rock,
}

/// <summary>
/// One scatter decal. <see cref="WorldPosition"/> is in world units, the same
/// space agent positions occupy, so a decal projects through
/// <c>SpectatorCamera.WorldToScreen</c> exactly like a pawn does.
/// </summary>
internal readonly record struct PlainsDecal(
    Vector2 WorldPosition,
    float ScaleFactor,
    PlainsDecalKind Kind);

/// <summary>
/// Pure helpers for the plains battlefield backdrop: ground-grid cell
/// boundaries and shading, and scatter-decal placement and sizing. Presentation
/// only — see docs/plans/2026-07-27-plains-backdrop-design.md. Touches only
/// value types, so it is fully unit tested with no graphics device.
/// </summary>
internal static class PlainsBackdropGeometry
{
    /// <summary>Target ground cell size, in world units.</summary>
    public const int TargetGroundCellSize = 64;

    /// <summary>
    /// Hard ceiling on grid columns and rows so an unusually large map cannot
    /// drive the per-frame fill count without bound.
    /// </summary>
    public const int MaximumGridDimension = 48;

    /// <summary>Number of distinct ground shades.</summary>
    public const int GroundShadeCount = 3;

    /// <summary>World area, in square world units, allotted to one decal.</summary>
    public const int WorldAreaPerDecal = 6_000;

    /// <summary>
    /// Named hard cap on decal count. Density must never grow without bound as
    /// maps get larger.
    /// </summary>
    public const int MaximumDecalCount = 256;

    /// <summary>
    /// Below this apparent scale a decal is sub-pixel and only produces
    /// flicker.
    /// </summary>
    public const float MinimumDecalApparentScale = 0.35f;

    /// <summary>
    /// Above this apparent scale a decal reads as a large blob rather than
    /// ground detail.
    /// </summary>
    public const float MaximumDecalApparentScale = 3.0f;

    /// <summary>
    /// Ceiling on how far any backdrop shade may be interpolated from
    /// <c>ArenaSurface</c> toward <c>ArenaBorder</c>. The backdrop must never
    /// compete with pawn silhouettes for attention, and in the high-contrast
    /// theme (pure black surface, pure white border) an unbounded value would
    /// scatter mid-grey speckle across the very theme whose purpose is
    /// eliminating visual noise.
    /// </summary>
    internal const float MaximumBackdropInterpolation = 0.22f;

    /// <summary>
    /// The three ground shade interpolation values, indexed by shade index.
    /// Static readonly, so this allocates once rather than per frame.
    /// </summary>
    internal static readonly ImmutableArray<float> GroundShadeInterpolation =
        [0.00f, 0.06f, 0.12f];

    /// <summary>
    /// Decal interpolation values, indexed by <see cref="PlainsDecalKind"/>.
    /// Spaced above the ground shades so decals read as distinct marks on the
    /// grid, and bounded by <see cref="MaximumBackdropInterpolation"/>.
    /// </summary>
    internal static readonly ImmutableArray<float> DecalKindInterpolation =
        [0.10f, 0.16f, 0.22f];

    /// <summary>
    /// Base decal size in screen pixels at an apparent scale of 1. Internal
    /// (not part of the design's fixed constants table) so tests can derive
    /// the exact expected rectangle size at the clamp extremes.
    /// </summary>
    internal const float DecalBaseSize = 5f;

    // Keeps the decal stream visibly distinct from any simulation stream. The
    // client never feeds values back into Core, so a collision would be
    // harmless, but a named salt makes the separation obvious to a reader.
    private const ulong PresentationSalt = 0x504C41494E530001UL;

    // Per-decal size variety, distinct from the display-time apparent-scale
    // clamp above: this is the raw randomized multiplier baked into each
    // decal at generation time, not the final on-screen bound.
    private const float DecalScaleFactorMinimum = 0.75f;
    private const float DecalScaleFactorMaximum = 1.35f;

    // 24-bit precision unit-interval conversion, matching the convention
    // already used by HitEffectGeometry.SeedAngle.
    private const double UnitScale = 1.0 / 16_777_216.0;

    /// <summary>
    /// Column and row counts for the ground grid, derived from the world map
    /// dimensions at <see cref="TargetGroundCellSize"/> world units per cell
    /// and clamped to <see cref="MaximumGridDimension"/>.
    /// </summary>
    public static (int Columns, int Rows) GetGridDimensions(
        int mapWidth,
        int mapHeight) =>
        (ComputeGridDimension(mapWidth), ComputeGridDimension(mapHeight));

    /// <summary>
    /// The single authoritative cell formula. Both the allocating
    /// <see cref="GetGroundCells"/> used by tests and the zero-allocation
    /// render loop call this, so the tested path and the shipped path cannot
    /// drift apart. Pure and allocation free: a struct return computed from
    /// <see cref="GetColumnBoundary"/>, <see cref="GetRowBoundary"/>, and
    /// <see cref="GetCellShadeIndex"/>.
    /// </summary>
    internal static PlainsGroundCell GetGroundCell(
        Rectangle mapBounds,
        int columns,
        int rows,
        int column,
        int row,
        ulong seed)
    {
        var left = GetColumnBoundary(mapBounds, columns, column);
        var right = GetColumnBoundary(mapBounds, columns, column + 1);
        var top = GetRowBoundary(mapBounds, rows, row);
        var bottom = GetRowBoundary(mapBounds, rows, row + 1);

        return new PlainsGroundCell(
            new Rectangle(left, top, right - left, bottom - top),
            GetCellShadeIndex(column, row, seed));
    }

    /// <summary>
    /// Builds every ground cell for the supplied projected map rectangle, in
    /// row-major order, by calling <see cref="GetGroundCell"/> per cell. The
    /// render path does not call this (it would allocate every frame) and
    /// instead calls <see cref="GetGroundCell"/> directly in a loop.
    /// </summary>
    public static PlainsGroundCell[] GetGroundCells(
        Rectangle mapBounds,
        int mapWidth,
        int mapHeight,
        ulong seed)
    {
        var (columns, rows) = GetGridDimensions(mapWidth, mapHeight);
        if (columns <= 0 || rows <= 0 ||
            mapBounds.Width <= 0 || mapBounds.Height <= 0)
        {
            return [];
        }

        var cells = new PlainsGroundCell[columns * rows];
        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                cells[index++] = GetGroundCell(
                    mapBounds,
                    columns,
                    rows,
                    column,
                    row,
                    seed);
            }
        }

        return cells;
    }

    /// <summary>
    /// The screen-space X coordinate of the boundary between column
    /// <paramref name="columnIndex"/> - 1 and <paramref name="columnIndex"/>.
    /// A pure function of its inputs, so calling it with the same
    /// <paramref name="columnIndex"/> for the right edge of one cell and the
    /// left edge of the next always yields the identical value — contiguity
    /// by construction, never independent per-cell rounding.
    /// </summary>
    internal static int GetColumnBoundary(
        Rectangle mapBounds,
        int columns,
        int columnIndex) =>
        mapBounds.Left +
        (int)((long)columnIndex * mapBounds.Width / columns);

    /// <summary>Row-axis counterpart to <see cref="GetColumnBoundary"/>.</summary>
    internal static int GetRowBoundary(
        Rectangle mapBounds,
        int rows,
        int rowIndex) =>
        mapBounds.Top + (int)((long)rowIndex * mapBounds.Height / rows);

    /// <summary>
    /// Deterministic hash for one corner-lattice point — an integer
    /// column/row intersection shared by up to four adjacent ground cells —
    /// mixed from the scenario seed and
    /// <see cref="PresentationSalts.GroundCornerLatticeSalt"/>. Returns a
    /// value in [0, 1) using the same 24-bit unit-interval conversion as
    /// <see cref="GenerateDecals"/>. Deliberately a distinct salt from
    /// <see cref="PresentationSalt"/> (battlefield-environment-design.md,
    /// "Ground shading"; R-W6.2): the two streams must never correlate, and
    /// the existing decal placement under <see cref="PresentationSalt"/>
    /// must not shift when this stream is introduced.
    /// </summary>
    private static double GetCornerLatticeValue(
        int latticeColumn,
        int latticeRow,
        ulong seed)
    {
        var combined = seed ^ PresentationSalts.GroundCornerLatticeSalt ^
            ((ulong)(uint)latticeColumn * 0x9E3779B97F4A7C15UL) ^
            ((ulong)(uint)latticeRow * 0xC2B2AE3D27D4EB4FUL);
        var rng = new SplitMix64(combined);
        return ToUnitInterval(rng.NextUInt64());
    }

    /// <summary>
    /// Deterministic ground shade for one cell, derived from the average of
    /// the cell's four corner-lattice hashes (battlefield-environment-
    /// design.md, "Ground shading") rather than an independent per-cell
    /// hash. Adjacent cells share two of their four lattice corners (the
    /// shared edge's endpoints), so the resulting shade drifts smoothly
    /// across neighboring cells — large tonal patches instead of per-cell
    /// confetti — while staying a pure, allocation-free, camera-independent
    /// function of (column, row, scenario seed) recomputable every frame.
    /// The result still indexes <see cref="GroundShadeInterpolation"/>, so
    /// the shade ladder and the <see cref="MaximumBackdropInterpolation"/>
    /// ceiling are unchanged.
    /// </summary>
    internal static int GetCellShadeIndex(int column, int row, ulong seed)
    {
        var topLeft = GetCornerLatticeValue(column, row, seed);
        var topRight = GetCornerLatticeValue(column + 1, row, seed);
        var bottomLeft = GetCornerLatticeValue(column, row + 1, seed);
        var bottomRight = GetCornerLatticeValue(column + 1, row + 1, seed);
        var average = (topLeft + topRight + bottomLeft + bottomRight) / 4.0;

        return Math.Clamp(
            (int)(average * GroundShadeCount),
            0,
            GroundShadeCount - 1);
    }

    private static int ComputeGridDimension(int mapExtent)
    {
        if (mapExtent <= 0)
        {
            return 0;
        }

        var raw = (int)Math.Ceiling(mapExtent / (double)TargetGroundCellSize);
        return Math.Clamp(raw, 1, MaximumGridDimension);
    }

    /// <summary>
    /// Generates the fixed-capacity decal set for one scenario. Seeded from
    /// <paramref name="seed"/> mixed with <see cref="PresentationSalt"/> using
    /// <see cref="SplitMix64"/>, so two calls with the same seed and map
    /// dimensions always reproduce the same sequence. Called exactly twice
    /// per match (scenario construction and reset) — never per tick or per
    /// frame.
    /// </summary>
    public static ImmutableArray<PlainsDecal> GenerateDecals(
        ulong seed,
        int mapWidth,
        int mapHeight)
    {
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return [];
        }

        // Clamped in double space: a very large map can make the quotient
        // exceed int.MaxValue, and casting that to int first would wrap to a
        // negative value and yield zero decals instead of the cap.
        var worldArea = (double)mapWidth * mapHeight;
        var count = (int)Math.Clamp(
            worldArea / WorldAreaPerDecal,
            0,
            MaximumDecalCount);
        if (count == 0)
        {
            return [];
        }

        var rng = new SplitMix64(seed ^ PresentationSalt);
        var decals = ImmutableArray.CreateBuilder<PlainsDecal>(count);
        for (var i = 0; i < count; i++)
        {
            var worldX = (float)(ToUnitInterval(rng.NextUInt64()) * mapWidth);
            var worldY = (float)(ToUnitInterval(rng.NextUInt64()) * mapHeight);
            var scaleFactor = DecalScaleFactorMinimum +
                (float)(ToUnitInterval(rng.NextUInt64()) *
                    (DecalScaleFactorMaximum - DecalScaleFactorMinimum));
            var kind = (PlainsDecalKind)rng.NextInt(DecalKindInterpolation.Length);

            decals.Add(new PlainsDecal(
                new Vector2(worldX, worldY),
                scaleFactor,
                kind));
        }

        return decals.MoveToImmutable();
    }

    private static double ToUnitInterval(ulong value) =>
        (value & 0xFFFFFFUL) * UnitScale;

    /// <summary>
    /// The on-screen rectangle for one decal, centered on
    /// <paramref name="screenAnchor"/>. Clamps the apparent scale to
    /// <see cref="MinimumDecalApparentScale"/> and
    /// <see cref="MaximumDecalApparentScale"/> regardless of
    /// <paramref name="decalScaleFactor"/>, the same clamp-first shape
    /// <c>PawnGeometry.Create</c> uses for pawn zoom scaling.
    /// </summary>
    public static Rectangle GetDecalScreenBounds(
        Vector2 screenAnchor,
        float cameraZoom,
        float decalScaleFactor)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        if (!float.IsFinite(decalScaleFactor) || decalScaleFactor <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(decalScaleFactor));
        }

        var apparentScale = Math.Clamp(
            cameraZoom * decalScaleFactor,
            MinimumDecalApparentScale,
            MaximumDecalApparentScale);
        var size = Math.Max(
            1,
            (int)MathF.Round(DecalBaseSize * apparentScale));

        return new Rectangle(
            (int)MathF.Round(screenAnchor.X - (size / 2f)),
            (int)MathF.Round(screenAnchor.Y - (size / 2f)),
            size,
            size);
    }
}
