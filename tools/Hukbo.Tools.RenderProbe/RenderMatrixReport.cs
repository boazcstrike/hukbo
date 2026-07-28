using Hukbo.Client.Rendering;

namespace Hukbo.Tools.RenderProbe;

/// <summary>
/// One agent count's worth of camera-station results plus the scenario seed
/// that produced them, one entry per agent count driven by
/// <c>Hukbo.Tools.RenderProbe</c>'s <c>--matrix</c> mode (VIS-036).
/// </summary>
/// <param name="AgentCount">Total agents in this cell's scripted scenario (both factions).</param>
/// <param name="Seed">The scenario seed this cell ran, shared across every cell in one matrix run.</param>
/// <param name="Stations">
/// One aggregated result per camera station driven for this agent count —
/// minimum zoom, default fit, and maximum zoom.
/// </param>
public sealed record RenderMatrixCell(
    int AgentCount,
    ulong Seed,
    IReadOnlyList<RenderProbeStationResult> Stations);

/// <summary>
/// The combined output of <c>Hukbo.Tools.RenderProbe</c>'s <c>--matrix</c>
/// mode (VIS-036, integration design section 11's full measurement matrix
/// and implementation-plan-draft.md's VIS-036 task). Each cell is captured by
/// re-invoking the same single-configuration path VIS-035/VIS-035R already
/// rely on — once per agent count — rather than by adding a second,
/// unverified in-process <c>ArenaGame</c> lifecycle to this tool.
/// </summary>
/// <param name="Fingerprint">
/// The hardware, resolution, build, and backend this whole matrix run shares
/// — taken from the first cell captured, since every cell in one matrix run
/// is captured back-to-back on the same machine and build.
/// </param>
/// <param name="Cells">One entry per agent count driven this run.</param>
/// <param name="AxesNote">
/// Honest disclosure (R-W6.13: never present an estimate as a measurement,
/// and never present a partially-driven matrix as the full one) of which of
/// the integration design's four matrix axes — agent count, camera-zoom
/// station, grass on/off, motion on/off — this run actually varied
/// independently. As of VIS-036 the render-probe seam built by
/// VIS-034/VIS-035/VIS-035R drives agent count and camera-zoom station
/// independently; it does not yet expose an independent grass-visibility or
/// motion-intensity override for the probe itself, so a matrix run captures
/// the natural state of both at each station (grass gated by
/// <c>DetailTierGate</c>'s own zoom-derived tier; motion at whatever
/// <c>MotionIntensity</c> the spectator's persisted settings file holds,
/// default <c>Full</c>) rather than driving them as independent on/off
/// cells. Extending the seam with those two overrides is recorded as a
/// follow-up here, never silently assumed or fabricated.
/// </param>
public sealed record RenderMatrixReport(
    RenderProbeFingerprint Fingerprint,
    IReadOnlyList<RenderMatrixCell> Cells,
    string AxesNote);
