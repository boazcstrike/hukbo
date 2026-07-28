using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Hukbo.Client;
using Hukbo.Client.Rendering;
using Hukbo.Core.Simulation;
using Hukbo.Tools.RenderProbe;

// Hand-run render-measurement harness (VIS-035, retrofitted onto the
// renderer-agnostic Tier 1/Tier 2 measurement seam by VIS-035R, amendment
// A-1, 2026-07-28, user-approved; extended with a --matrix mode by VIS-036,
// integration design section 11's full measurement matrix): launches the
// real client against a scripted scenario seed, drives the three camera
// stations named in the integration design's measurement matrix (minimum
// zoom, default fit, maximum zoom), and records frame-time percentiles,
// Tier 1 (renderer-invariant, budgeted) quad/triangle/geometry-build/submit
// figures, Tier 2 (backend-specific, diagnostic-only) submission/batch/
// texture-bind figures, GC deltas, and the configuration fingerprint
// (including the active backend name) as JSON under artifacts/. Requires a
// real window and GPU — there is no headless mode for either usage below.
// Not in Hukbo.slnx, not in the gate (R-W6.12).
//
// A measurement run disables vertical retrace (GPU-006, integration design
// section 4.3), because a blocking wait for the display is not CPU cost. That
// override is the probe's alone: the shipped client keeps retrace enabled, and
// the setting the run actually got is recorded on the report fingerprint as
// VerticalRetraceSynchronized so no two reports taken under different settings
// can be compared by accident.
//
// Single-configuration usage: dotnet run -- [agents] [seed] [framesPerStation] [outputPath]
//   agents           total units, both factions combined (default 200)
//   seed             scenario seed (default 1)
//   framesPerStation frames sampled per camera station after warm-up (default 300)
//   outputPath       JSON report path (default artifacts/render-probe-<date>.json)
//
// Full-matrix usage (VIS-036): Hukbo.Tools.RenderProbe.exe --matrix [seed] [framesPerStation] [outputPath]
//   Re-invokes this same executable once per agent count in the integration
//   design's measurement matrix (200, 500, and 1,000 visible units; the
//   1,000-unit cell added by GPU-007), reusing the exact
//   single-configuration path above for each cell rather than adding a
//   second, unverified in-process ArenaGame lifecycle. Must be launched from
//   the built apphost executable (not "dotnet run"), because it re-invokes
//   Environment.ProcessPath with the single-configuration argument shape.
//   Grass-visibility and motion-intensity are NOT independently driven by
//   this mode — see RenderMatrixReport.AxesNote for the honest disclosure of
//   that gap (R-W6.13).
//   seed             scenario seed (default 1)
//   framesPerStation frames sampled per camera station after warm-up (default 300)
//   outputPath       combined JSON report path (default artifacts/render-matrix-<date>.json)

if (args.Length > 0 && args[0] == "--matrix")
{
    RunMatrix(args);
}
else
{
    RunSingleConfiguration(args);
}

return;

void RunSingleConfiguration(string[] singleArgs)
{
    var agents = singleArgs.Length > 0
        ? int.Parse(singleArgs[0], CultureInfo.InvariantCulture)
        : 200;
    var seed = singleArgs.Length > 1
        ? ulong.Parse(singleArgs[1], CultureInfo.InvariantCulture)
        : 1UL;
    var framesPerStation = singleArgs.Length > 2
        ? int.Parse(singleArgs[2], CultureInfo.InvariantCulture)
        : 300;
    var outputPath = singleArgs.Length > 3
        ? singleArgs[3]
        : Path.Combine(
            "artifacts",
            $"render-probe-{DateTime.UtcNow:yyyy-MM-dd}.json");

    var report = CaptureReport(agents, seed, framesPerStation);

    var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(
        outputPath,
        JsonSerializer.Serialize(report, RenderProbeReport.SerializerOptions));

    Console.WriteLine(
        $"agents={agents} seed={seed} framesPerStation={framesPerStation} " +
        $"backend={report.Fingerprint.Backend} " +
        $"retraceSynchronized={report.Fingerprint.VerticalRetraceSynchronized} " +
        $"probeDuplicationFactor={report.Fingerprint.ProbeDuplicationFactor:F3}");
    Console.WriteLine($"wrote {outputPath}");
    PrintStations(report.Stations);
}

void RunMatrix(string[] matrixArgs)
{
    var seed = matrixArgs.Length > 1
        ? ulong.Parse(matrixArgs[1], CultureInfo.InvariantCulture)
        : 1UL;
    var framesPerStation = matrixArgs.Length > 2
        ? int.Parse(matrixArgs[2], CultureInfo.InvariantCulture)
        : 300;
    var outputPath = matrixArgs.Length > 3
        ? matrixArgs[3]
        : Path.Combine(
            "artifacts",
            $"render-matrix-{DateTime.UtcNow:yyyy-MM-dd}.json");

    // The integration design's measurement matrix (section 11), extended by
    // GPU-007 (integration design section 4.5) with the 1,000-unit cell the
    // Phase 3 go/no-go trigger is actually stated against: 200, 500, and
    // 1,000 visible units. Camera-zoom station is driven inside each
    // re-invoked single-configuration run below, so it does not repeat here.
    //
    // Nothing had to be raised to make the 1,000-unit cell legal. Each cell
    // reaches ArenaGame through the scenarioOverride constructor parameter,
    // which replaces the persisted army composition outright, so
    // ArmyCompositionStepper.MaximumUnitsPerTeam — the shipped client's
    // opt-in ceiling, still 250 per team, and GPU-022's business, not this
    // task's — is not on this path at all. The bound that does apply is
    // Scenario.MaximumAgentsPerFaction (10,000 per faction, so 20,000 total)
    // together with Scenario's body-density check, which on the default
    // 1,280x720 map at the default body radius admits 12,755 total bodies.
    // 1,000 clears both by a wide margin.
    var unitCounts = new[] { 200, 500, 1_000 };

    var executablePath = Environment.ProcessPath;
    if (string.IsNullOrEmpty(executablePath))
    {
        Console.Error.WriteLine(
            "--matrix could not resolve Environment.ProcessPath to " +
            "re-invoke itself per agent count. Run the built apphost " +
            "executable directly (not \"dotnet run\"), or run each agent " +
            "count individually with the single-configuration usage.");
        Environment.ExitCode = 1;
        return;
    }

    var cells = new List<RenderMatrixCell>(unitCounts.Length);
    RenderProbeFingerprint? sharedFingerprint = null;

    foreach (var agents in unitCounts)
    {
        var cellPath = Path.Combine(
            Path.GetTempPath(),
            $"render-matrix-cell-{agents}-{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(agents.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(seed.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(
            framesPerStation.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(cellPath);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine(
                $"--matrix failed to start the agents={agents} cell.");
            Environment.ExitCode = 1;
            return;
        }

        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(cellPath))
        {
            Console.Error.WriteLine(
                $"--matrix cell agents={agents} exited {process.ExitCode} " +
                "or wrote no report; aborting the matrix rather than " +
                "recording a partial or fabricated result.");
            Environment.ExitCode = 1;
            return;
        }

        var cellReport = JsonSerializer.Deserialize<RenderProbeReport>(
            File.ReadAllText(cellPath),
            RenderProbeReport.SerializerOptions);
        TryDeleteCellFile(cellPath);

        if (cellReport is null)
        {
            Console.Error.WriteLine(
                $"--matrix cell agents={agents} deserialized to null; " +
                "aborting the matrix rather than recording a partial or " +
                "fabricated result.");
            Environment.ExitCode = 1;
            return;
        }

        sharedFingerprint ??= cellReport.Fingerprint;
        cells.Add(
            new RenderMatrixCell(
                cellReport.AgentCount,
                cellReport.Seed,
                cellReport.Stations));
    }

    if (sharedFingerprint is null)
    {
        Console.Error.WriteLine("--matrix produced no cells; nothing to write.");
        Environment.ExitCode = 1;
        return;
    }

    const string axesNote =
        "This run drove agent count (200, 500, 1000) and camera-zoom station " +
        "(minimum zoom, default fit, maximum zoom) independently, per the " +
        "existing render-probe seam (VIS-034/VIS-035/VIS-035R). It did NOT " +
        "independently drive grass-visibility or motion-intensity: the " +
        "probe has no override for either as of VIS-036, so grass followed " +
        "DetailTierGate's own zoom-derived tier at each station and motion " +
        "ran at the spectator's persisted MotionIntensity (default Full). " +
        "The integration design's grass-on/off and motion-on/off matrix " +
        "axes are not represented in this report; extending the seam with " +
        "those two overrides is a follow-up, not fabricated here (R-W6.13). " +
        "Every cell was captured with vertical retrace disabled (GPU-006, " +
        "integration design section 4.3), because a blocking wait for the " +
        "display is not CPU cost; the setting each run actually got is on " +
        "the fingerprint as VerticalRetraceSynchronized, and a report whose " +
        "fingerprint says true is a refresh-interval floor rather than a " +
        "measurement and must not be compared against one that says false. " +
        "The shipped client is unchanged and keeps retrace enabled. The " +
        "1,000-unit cell is the size the Phase 3 go/no-go trigger is stated " +
        "against (GPU-007, integration design section 4.5); it is reached " +
        "through the probe's scenario override and raises no shipped cap.";

    var matrixReport = new RenderMatrixReport(sharedFingerprint, cells, axesNote);

    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrEmpty(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    File.WriteAllText(
        outputPath,
        JsonSerializer.Serialize(matrixReport, RenderProbeReport.SerializerOptions));

    Console.WriteLine($"wrote {outputPath}");

    // GPU-005. The matrix keeps one shared fingerprint, taken from the first
    // cell, so this is the first cell's measured factor rather than every
    // cell's. Stated on its own line so nobody reads it as a per-cell figure.
    Console.WriteLine(
        $"fingerprint (from the first cell): backend={sharedFingerprint.Backend} " +
        $"retraceSynchronized={sharedFingerprint.VerticalRetraceSynchronized} " +
        $"probeDuplicationFactor={sharedFingerprint.ProbeDuplicationFactor:F3}");

    foreach (var cell in cells)
    {
        Console.WriteLine($"agents={cell.AgentCount} seed={cell.Seed}");
        PrintStations(cell.Stations);
    }

    Console.WriteLine(axesNote);
}

RenderProbeReport CaptureReport(int agents, ulong seed, int framesPerStation)
{
    // Read by ArenaGame's constructor before any of its own env-var-derived
    // fields initialize, so this must be set before `new ArenaGame(...)` runs.
    Environment.SetEnvironmentVariable("HUKBO_RENDER_PROBE", "1");

    var scenario = Scenario.CreateDefault(seed, agents);
    scenario.Validate();

    // Null zoom means "leave the initial Fit() result alone" — the default-fit
    // station is exactly what a spectator sees on launch, un-driven.
    var stations = new (string Name, float? Zoom)[]
    {
        ("minimum-zoom", 0.05f),
        ("default-fit", null),
        ("maximum-zoom", 12f),
    };

    // Lets LoadContent, the first Fit(), and the freshly resized window settle
    // before any frame counts toward a station's sample set.
    const int WarmupFrameCount = 30;

    var warmupFramesRemaining = WarmupFrameCount;
    var stationIndex = 0;
    var samplesByStation = new List<RenderProbeSample>[stations.Length];
    for (var index = 0; index < stations.Length; index++)
    {
        samplesByStation[index] = new List<RenderProbeSample>(framesPerStation);
    }

    using var game = new ArenaGame(scenarioOverride: scenario);

    // GPU-006, integration design section 4.3. Driver back-pressure from a
    // retrace-synchronized device blocks inside the measured window, at the
    // GraphicsDevice.Clear that opens the next frame, so a synchronized run
    // reports a display-imposed floor instead of CPU cost — three stations
    // drawing 9,326 and 1,028 quads landing within 0.06 ms of one another is
    // that floor, not that work. Set before Run() because
    // GraphicsDeviceManager reads the flag when it creates the device.
    game.SetProbeVerticalRetrace(synchronize: false);

    game.RenderProbeSampled += sample =>
    {
        if (warmupFramesRemaining > 0)
        {
            warmupFramesRemaining--;
            if (warmupFramesRemaining == 0 && stations[stationIndex].Zoom is { } firstZoom)
            {
                game.SetProbeCameraZoom(firstZoom);
            }

            return;
        }

        samplesByStation[stationIndex].Add(sample);
        if (samplesByStation[stationIndex].Count < framesPerStation)
        {
            return;
        }

        stationIndex++;
        if (stationIndex >= stations.Length)
        {
            game.Exit();
            return;
        }

        if (stations[stationIndex].Zoom is { } nextZoom)
        {
            game.SetProbeCameraZoom(nextZoom);
        }
    };

    game.Run();

    // Read back from the game that produced the samples above, after its run
    // has finished, rather than restated as a literal here: the fingerprint has
    // to describe what the run actually did, so that a report captured with
    // retrace still enabled is never mistaken for one captured without it.
    var verticalRetraceSynchronized = game.IsVerticalRetraceSynchronized;

    // GPU-005. Read back the same way and for the same reason: the factor is
    // the ratio of the pawn-geometry invocations this run actually counted to
    // the ones its draw path alone made, accumulated frame by frame while the
    // run was happening. Nothing here asserts what that ratio ought to be, so
    // when GPU-013 and GPU-014 remove the draw path's duplicate construction
    // this number moves on its own.
    var probeDuplicationFactor = game.ProbePawnGeometryDuplicationFactor;

    var stationResults = new RenderProbeStationResult[stations.Length];
    for (var index = 0; index < stations.Length; index++)
    {
        stationResults[index] = RenderProbeStatistics.Summarize(
            stations[index].Name,
            samplesByStation[index]);
    }

#if DEBUG
    const string buildConfiguration = "Debug";
#else
    const string buildConfiguration = "Release";
#endif

    // The only backend Hukbo.Client ships today (VIS-034/RenderMetrics.cs's
    // SpriteBatchRenderMetricsRecorder doc): one immediate-mode SpriteBatch
    // renderer sharing one 1x1 pixel texture. Recorded explicitly (amendment
    // A-1) so a later GPU-instanced backend's report is legibly a different
    // backend rather than silently comparable numbers in an incompatible unit.
    const string backend = "spritebatch-1x1";

    return new RenderProbeReport(
        new RenderProbeFingerprint(
            Environment.MachineName,
            1920,
            1080,
            buildConfiguration,
            backend,
            // GPU-006. Derived from the run above, never asserted: the probe
            // asks for retrace off, and this states whether the device it got
            // was in fact presenting unsynchronized. A true here means the
            // frame-time percentiles below are a refresh-interval floor rather
            // than a measurement, and it must be able to say so.
            VerticalRetraceSynchronized: verticalRetraceSynchronized,
            // GPU-005. Derived from the invocations the run recorded, never
            // hardcoded. A 0 here now means the draw path counted nothing at
            // all, which for a probe run that produced stations would itself
            // be a finding.
            ProbeDuplicationFactor: probeDuplicationFactor,
            DateTime.UtcNow),
        agents,
        seed,
        stationResults);
}

void PrintStations(IReadOnlyList<RenderProbeStationResult> stations)
{
    foreach (var station in stations)
    {
        Console.WriteLine(
            $"{station.StationName,-14} frames={station.FrameCount,4} " +
            $"p50={station.FrameMillisecondsP50,6:F2}ms " +
            $"p95={station.FrameMillisecondsP95,6:F2}ms " +
            $"p99={station.FrameMillisecondsP99,6:F2}ms " +
            $"quads(max)={station.QuadsMaximum,6} " +
            $"tris(max)={station.TrianglesMaximum,6} " +
            // GPU-005. geometryBuildMicroseconds is deliberately absent from
            // this line. Nothing writes it any more: the pass it used to time
            // is the probe's own and is now reported as probeOvh, while the
            // renderer's real per-pawn geometry cost is arenaGeom (GPU-004).
            // Printing a field no producer writes would read as a measured
            // zero. It stays in the JSON schema, which is versioned, rather
            // than on a console line, which is read at a glance.
            $"arenaGeom(p50/p95)={station.ArenaGeometryMicrosecondsP50,7:F1}/" +
            $"{station.ArenaGeometryMicrosecondsP95,7:F1}us " +
            $"submit(p50/p95)={station.SubmitMicrosecondsP50,7:F1}/" +
            $"{station.SubmitMicrosecondsP95,7:F1}us " +
            $"probeOvh(p50/p95)={station.ProbeOverheadMicrosecondsP50,7:F1}/" +
            $"{station.ProbeOverheadMicrosecondsP95,7:F1}us " +
            $"pawnGeomCalls(max)={station.PawnGeometryInvocationsMaximum,6} " +
            $"managedBytes(max)={station.ManagedBytesAllocatedMaximum} " +
            $"[Tier2 diagnostic] submissions(max)={station.SubmissionsMaximum,6} " +
            $"batches(max)={station.BatchesMaximum} textureBinds(max)={station.TextureBindsMaximum} " +
            $"gc0={station.Gen0CollectionsDelta} gc1={station.Gen1CollectionsDelta} " +
            $"gc2={station.Gen2CollectionsDelta} allocBytes={station.AllocatedBytesDelta}");
    }
}

void TryDeleteCellFile(string path)
{
    try
    {
        File.Delete(path);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}
