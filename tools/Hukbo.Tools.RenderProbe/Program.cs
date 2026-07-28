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
// Single-configuration usage: dotnet run -- [agents] [seed] [framesPerStation] [outputPath]
//   agents           total units, both factions combined (default 200)
//   seed             scenario seed (default 1)
//   framesPerStation frames sampled per camera station after warm-up (default 300)
//   outputPath       JSON report path (default artifacts/render-probe-<date>.json)
//
// Full-matrix usage (VIS-036): Hukbo.Tools.RenderProbe.exe --matrix [seed] [framesPerStation] [outputPath]
//   Re-invokes this same executable once per agent count in the integration
//   design's measurement matrix (200, 500 visible units), reusing the exact
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
        $"backend={report.Fingerprint.Backend}");
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

    // The integration design's measurement matrix (section 11): 200 and 500
    // visible units. Camera-zoom station is driven inside each re-invoked
    // single-configuration run below, so it does not repeat here.
    var unitCounts = new[] { 200, 500 };

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
        "This run drove agent count (200, 500) and camera-zoom station " +
        "(minimum zoom, default fit, maximum zoom) independently, per the " +
        "existing render-probe seam (VIS-034/VIS-035/VIS-035R). It did NOT " +
        "independently drive grass-visibility or motion-intensity: the " +
        "probe has no override for either as of VIS-036, so grass followed " +
        "DetailTierGate's own zoom-derived tier at each station and motion " +
        "ran at the spectator's persisted MotionIntensity (default Full). " +
        "The integration design's grass-on/off and motion-on/off matrix " +
        "axes are not represented in this report; extending the seam with " +
        "those two overrides is a follow-up, not fabricated here (R-W6.13).";

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
            // The shipped client sets SynchronizeWithVerticalRetrace = true and
            // this probe does not override it yet, so every frame-time
            // percentile below is a refresh-interval floor rather than a
            // measurement. GPU-006 adds the probe-only override that flips
            // this to false.
            VerticalRetraceSynchronized: true,
            // 0 means "this probe build did not measure duplication", which is
            // the only honest value until GPU-005 derives the factor from the
            // recorded PawnGeometryInvocations count.
            ProbeDuplicationFactor: 0,
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
            $"geomBuild(p50/p95)={station.GeometryBuildMicrosecondsP50,7:F1}/" +
            $"{station.GeometryBuildMicrosecondsP95,7:F1}us " +
            $"submit(p50/p95)={station.SubmitMicrosecondsP50,7:F1}/" +
            $"{station.SubmitMicrosecondsP95,7:F1}us " +
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
