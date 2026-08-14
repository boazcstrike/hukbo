using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// PV-6. No published source gives an on-screen pixel height below which leg
/// motion stops being worth drawing, and two research passes looking for one
/// failed. This measures the game's own numbers instead: for each detail-tier
/// boundary and each of the three camera stations
/// (<c>ConservativePawnCullTests</c>'s review protocol), for both
/// <see cref="GaitMode.Walk"/> and <see cref="GaitMode.Run"/>, the drawn leg
/// height and the peak foot travel and lift a gait cycle reaches at that
/// height.
/// </summary>
/// <remarks>
/// <para>
/// This is a measurement, not a tuning pass. Every ratio it reads
/// (<c>GaitGeometry</c>'s stride and lift ratios, <c>PawnGeometry</c>'s leg
/// length and detail-tier thresholds) is read from source by name, and none
/// of them is changed here.
/// </para>
/// <para>
/// The drawn leg height reuses <see cref="PawnGeometry.Create"/>'s own
/// <c>ApparentScale</c> output rather than recomputing the zoom-to-scale
/// clamp by hand, so this measurement cannot drift from what the renderer
/// actually resolves for the same zoom. The rounding rule that turns a layout
/// length into a whole-pixel size — <c>Math.Max(1, (int)MathF.Round(value))</c>
/// — is <c>PawnGeometry.ToSize</c> (PawnGeometry.cs:2576-2577), reimplemented
/// here as <see cref="ToWholePixelSize"/> because it is private. Foot travel
/// and lift use plain <c>MathF.Round</c> with no floor, matching
/// <c>PawnGeometry.BuildLeg</c> (PawnGeometry.cs:1745), which rounds a lifted
/// leg's position with no minimum — a gait phase that produces zero
/// displacement really does draw at zero pixels, unlike the leg body itself,
/// which is never allowed to disappear.
/// </para>
/// </remarks>
public sealed class GaitPixelHeightTests
{
    // ============= Constants read from source, not retyped blind =============

    /// <summary>PawnGeometry.cs:482.</summary>
    private const float LegLengthUnits = 7.5f;

    /// <summary>PawnGeometry.cs:235. The Low/Medium detail-tier boundary.</summary>
    private const float MediumDetailScale = 0.95f;

    /// <summary>PawnGeometry.cs:236. The Medium/High detail-tier boundary.</summary>
    private const float HighDetailScale = 1.80f;

    /// <summary>GaitGeometry.cs:63.</summary>
    private const float WalkStrideRatio = 0.32f;

    /// <summary>GaitGeometry.cs:70.</summary>
    private const float RunStrideRatio = 0.60f;

    /// <summary>GaitGeometry.cs:73.</summary>
    private const float WalkFootLiftRatio = 0.15f;

    /// <summary>GaitGeometry.cs:80.</summary>
    private const float RunFootLiftRatio = 0.38f;

    // ============= The three camera stations =============
    //
    // Named and sourced exactly as ConservativePawnCullTests names them
    // (that file's own remarks, lines 17-31): the camera's own clamp values,
    // and the default-fit zoom the panel actually resolves for the tracked
    // Phase 1 render baseline's 1920x1080 arena bounds. Not invented here —
    // the minimum and maximum are SpectatorCamera's own floor and ceiling,
    // and the default-fit zoom is obtained by calling SpectatorCamera.Fit
    // rather than a copied literal, so it cannot go stale if that method's
    // arithmetic ever changes.

    /// <summary>SpectatorCamera's own zoom floor.</summary>
    private const float MinimumZoomStation = 0.05f;

    /// <summary>SpectatorCamera's own zoom ceiling.</summary>
    private const float MaximumZoomStation = 12f;

    // Scenario's own default map, in world units. ConservativePawnCullTests
    // lines 33-35.
    private const int MapWidth = 1_280;
    private const int MapHeight = 720;

    // The arena panel at the resolution the tracked Phase 1 render baseline
    // was captured at. Derivation and citation identical to
    // ConservativePawnCullTests.BaselineArenaBounds (lines 36-47).
    private static readonly Rectangle BaselineArenaBounds =
        new(12, 68, 1_466, 1_000);

    // ============= The two detail-tier-boundary zooms =============
    //
    // ResolveApparentScale(zoom) = clamp(zoom * 1.35, 0.72, 2.40)
    // (PawnGeometry.cs:247-251, ZoomScale at line 234). These are the same
    // zoom values ConservativePawnCullTests.ZoomSamples already carries
    // (lines 60 and 63) for exactly this reason: solved for
    // apparentScale = MediumDetailScale and apparentScale = HighDetailScale.
    // Reused rather than re-derived, per this task's brief.

    /// <summary>Apparent scale 0.95, the Low/Medium boundary.</summary>
    private const float LowMediumBoundaryZoom = 0.7037037f;

    /// <summary>Apparent scale 1.80, the Medium/High boundary.</summary>
    private const float MediumHighBoundaryZoom = 1.3333334f;

    private static readonly PawnAppearance NeutralAppearance =
        PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.TallHardwood);

    /// <summary>
    /// One row of the measurement: a point on the zoom axis (either a named
    /// camera station or a named detail-tier boundary), the resulting drawn
    /// leg height, and both gaits' peak foot travel and lift at that leg
    /// height.
    /// </summary>
    private readonly record struct Row(
        string Point,
        float Zoom,
        int LegHeightPx,
        int WalkFootTravelPx,
        int WalkFootLiftPx,
        int RunFootTravelPx,
        int RunFootLiftPx);

    /// <summary>
    /// Computes and pins the whole table in one place, so a single failure
    /// shows every figure that drifted rather than one row at a time.
    /// </summary>
    [Fact]
    public void Table_MatchesThePinnedMeasurement()
    {
        var defaultFitZoom = ResolveDefaultFitZoom();

        var rows = new[]
        {
            BuildRow("Minimum-zoom station", MinimumZoomStation),
            BuildRow("Low/Medium tier boundary", LowMediumBoundaryZoom),
            BuildRow("Default-fit station", defaultFitZoom),
            BuildRow("Medium/High tier boundary", MediumHighBoundaryZoom),
            BuildRow("Maximum-zoom station", MaximumZoomStation),
        };

        var expected = new[]
        {
            new Row("Minimum-zoom station", MinimumZoomStation, 5, 2, 1, 3, 2),
            new Row("Low/Medium tier boundary", LowMediumBoundaryZoom, 7, 2, 1, 4, 3),
            new Row("Default-fit station", defaultFitZoom, 10, 3, 2, 6, 4),
            new Row("Medium/High tier boundary", MediumHighBoundaryZoom, 14, 4, 2, 8, 5),
            new Row("Maximum-zoom station", MaximumZoomStation, 18, 6, 3, 11, 7),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            Assert.True(
                expected[i].LegHeightPx == rows[i].LegHeightPx &&
                    expected[i].WalkFootTravelPx == rows[i].WalkFootTravelPx &&
                    expected[i].WalkFootLiftPx == rows[i].WalkFootLiftPx &&
                    expected[i].RunFootTravelPx == rows[i].RunFootTravelPx &&
                    expected[i].RunFootLiftPx == rows[i].RunFootLiftPx,
                $"Row '{rows[i].Point}' drifted from the pinned measurement. " +
                $"Expected {expected[i]}, got {rows[i]}.");
        }
    }

    /// <summary>
    /// Confirms the detail tier each station and boundary actually lands in,
    /// so the leg-height figures above are read against the tier that gates
    /// whether the legs they describe draw at all
    /// (<c>PawnGeometry.CreateLegsAndFeet</c> returns an empty layout at
    /// <see cref="PawnDetailTier.Low"/> regardless of leg length).
    /// </summary>
    [Fact]
    public void MinimumZoomStation_LandsInLowTier_WhereLegsNeverDraw()
    {
        var layout = PawnGeometry.Create(
            Vector2.Zero,
            MinimumZoomStation,
            NeutralAppearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
    }

    [Fact]
    public void DefaultFitStation_LandsInMediumTier()
    {
        var layout = PawnGeometry.Create(
            Vector2.Zero,
            ResolveDefaultFitZoom(),
            NeutralAppearance);

        Assert.Equal(PawnDetailTier.Medium, layout.DetailTier);
    }

    [Fact]
    public void MaximumZoomStation_LandsInHighTier()
    {
        var layout = PawnGeometry.Create(
            Vector2.Zero,
            MaximumZoomStation,
            NeutralAppearance);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
    }

    private static Row BuildRow(string point, float zoom)
    {
        var apparentScale = PawnGeometry.Create(
            Vector2.Zero,
            zoom,
            NeutralAppearance).ApparentScale;

        var legHeightPx = ToWholePixelSize(LegLengthUnits * apparentScale);

        return new Row(
            point,
            zoom,
            legHeightPx,
            RoundToWholePixels(WalkStrideRatio * legHeightPx),
            RoundToWholePixels(WalkFootLiftRatio * legHeightPx),
            RoundToWholePixels(RunStrideRatio * legHeightPx),
            RoundToWholePixels(RunFootLiftRatio * legHeightPx));
    }

    /// <summary>
    /// The default-fit station's zoom, obtained by calling
    /// <see cref="SpectatorCamera.Fit"/> against the same map and arena
    /// bounds <c>ConservativePawnCullTests.AdmittedFraction_...</c> uses for
    /// its own "default fit" row, rather than a copied literal.
    /// </summary>
    private static float ResolveDefaultFitZoom()
    {
        var camera = new SpectatorCamera(MapWidth, MapHeight);
        camera.Fit(BaselineArenaBounds);
        return camera.Zoom;
    }

    /// <summary>
    /// <c>PawnGeometry.ToSize</c> (PawnGeometry.cs:2576-2577), reimplemented
    /// here because it is private. Every drawn layout size in
    /// <c>PawnGeometry</c>, including the drawn leg height this file
    /// measures, is floored to one whole pixel rather than allowed to vanish.
    /// </summary>
    private static int ToWholePixelSize(float value) =>
        Math.Max(1, (int)MathF.Round(value));

    /// <summary>
    /// Matches <c>PawnGeometry.BuildLeg</c>'s own rounding
    /// (PawnGeometry.cs:1745), which has no floor: a foot travel or lift of
    /// less than half a pixel really does draw at zero displacement.
    /// </summary>
    private static int RoundToWholePixels(float value) =>
        (int)MathF.Round(value);
}
