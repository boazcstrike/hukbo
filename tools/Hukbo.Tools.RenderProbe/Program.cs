using System.Globalization;
using System.Text.Json;
using Hukbo.Client;
using Hukbo.Client.Rendering;
using Hukbo.Core.Simulation;

// Hand-run render-measurement harness (VIS-035): launches the real client
// against a scripted scenario seed, drives the three camera stations named
// in the integration design's measurement matrix (minimum zoom, default
// fit, maximum zoom), and records frame-time percentiles, peak arena
// submission count, GC deltas, and the configuration fingerprint as JSON
// under artifacts/. Requires a real window and GPU — there is no headless
// mode for this one, unlike the other tools here. Not in Hukbo.slnx, not in
// the gate (R-W6.12).
//
// Usage: dotnet run -- [agents] [seed] [framesPerStation] [outputPath]
//   agents           total units, both factions combined (default 200)
//   seed             scenario seed (default 1)
//   framesPerStation frames sampled per camera station after warm-up (default 300)
//   outputPath       JSON report path (default artifacts/render-probe-<date>.json)

var agents = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 200;
var seed = args.Length > 1 ? ulong.Parse(args[1], CultureInfo.InvariantCulture) : 1UL;
var framesPerStation =
    args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 300;
var outputPath = args.Length > 3
    ? args[3]
    : Path.Combine(
        "artifacts",
        $"render-probe-{DateTime.UtcNow:yyyy-MM-dd}.json");

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

var report = new RenderProbeReport(
    new RenderProbeFingerprint(
        Environment.MachineName,
        1920,
        1080,
        buildConfiguration,
        DateTime.UtcNow),
    agents,
    seed,
    stationResults);

var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

File.WriteAllText(
    outputPath,
    JsonSerializer.Serialize(report, RenderProbeReport.SerializerOptions));

Console.WriteLine($"agents={agents} seed={seed} framesPerStation={framesPerStation}");
Console.WriteLine($"wrote {outputPath}");
foreach (var station in stationResults)
{
    Console.WriteLine(
        $"{station.StationName,-14} frames={station.FrameCount,4} " +
        $"p50={station.FrameMillisecondsP50,6:F2}ms " +
        $"p95={station.FrameMillisecondsP95,6:F2}ms " +
        $"p99={station.FrameMillisecondsP99,6:F2}ms " +
        $"submissions(max)={station.ArenaSubmissionCountMaximum,6} " +
        $"gc0={station.Gen0CollectionsDelta} gc1={station.Gen1CollectionsDelta} " +
        $"gc2={station.Gen2CollectionsDelta} allocBytes={station.AllocatedBytesDelta}");
}
