using System.Globalization;
using Hukbo.Tools.MixAnalysis;

// Renders what a real battle actually sounds like, offline. Sums the shipped
// clips at the trigger times a real BattleSimulation produces, measures the
// result against digital full scale, and writes each render to a WAV so the
// outcome can be heard rather than argued about.
//
// No audio device is opened and no repository file is modified.

var audioDirectory = args.Length > 0
    ? args[0]
    : Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "Hukbo.Client", "Content", "Audio");

var outputDirectory = args.Length > 1 ? args[1] : "mix-output";
var agents = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 200;
var seed = args.Length > 3 ? ulong.Parse(args[3], CultureInfo.InvariantCulture) : 1UL;
var speed = args.Length > 4 ? double.Parse(args[4], CultureInfo.InvariantCulture) : 1.0;

audioDirectory = Path.GetFullPath(audioDirectory);
Directory.CreateDirectory(outputDirectory);

Console.WriteLine($"audio directory : {audioDirectory}");
Console.WriteLine($"output directory: {Path.GetFullPath(outputDirectory)}");
Console.WriteLine($"battle          : {agents} agents, seed {seed}, {speed:F0}x speed");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Load every clip and group the file names into variant lists by prefix, the
// way the client's library does.
// ---------------------------------------------------------------------------
var clips = new Dictionary<string, WavClip>(StringComparer.OrdinalIgnoreCase);
foreach (var path in Directory.EnumerateFiles(audioDirectory, "*.wav").Order(StringComparer.Ordinal))
{
    try
    {
        var clip = WavFile.Read(path);
        clips[Path.GetFileName(path)] = clip;
    }
    catch (InvalidDataException exception)
    {
        Console.Error.WriteLine($"skipped: {exception.Message}");
    }
}

if (clips.Count == 0)
{
    Console.Error.WriteLine("No usable clips.");
    return 1;
}

var sampleRate = clips.Values.First().SampleRate;
var channels = clips.Values.First().Channels;
if (clips.Values.Any(clip => clip.SampleRate != sampleRate || clip.Channels != channels))
{
    Console.Error.WriteLine("Clips disagree on sample rate or channel count; mixing would be wrong.");
    return 1;
}

Console.WriteLine($"loaded {clips.Count} clips at {sampleRate} Hz, {channels} channels, " +
    $"mean {clips.Values.Average(clip => clip.DurationSeconds) * 1000:F0} ms");

var variantsByPrefix = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
foreach (var fileName in clips.Keys.Order(StringComparer.Ordinal))
{
    var stem = Path.GetFileNameWithoutExtension(fileName);
    var lastDash = stem.LastIndexOf('-');
    if (lastDash <= 0 || !int.TryParse(
            stem.AsSpan(lastDash + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out _))
    {
        continue;
    }

    var prefix = stem[..(lastDash + 1)];
    if (!variantsByPrefix.TryGetValue(prefix, out var list))
    {
        list = new List<string>();
        variantsByPrefix[prefix] = list;
    }

    ((List<string>)list).Add(fileName);
}

// ---------------------------------------------------------------------------
// Build the cue schedule from a real battle.
// ---------------------------------------------------------------------------
const int TickRate = 20;
var (cues, ticksRun, outcome) = CueSchedule.Build(agents, seed, 10_000, variantsByPrefix);
Console.WriteLine($"battle ran {ticksRun} ticks, outcome {outcome}, {cues.Count} cues demanded");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Render each policy and measure it.
// ---------------------------------------------------------------------------
MixPolicy[] policies =
[
    new("today", MaximumPerSound: 3, MaximumTotal: 8, BaseGain: 0.8f,
        CompensateForVoiceCount: false, LimiterCeiling: null),
    new("uncapped-same-gain", MaximumPerSound: null, MaximumTotal: null, BaseGain: 0.8f,
        CompensateForVoiceCount: false, LimiterCeiling: null),
    new("uncapped-compensated", MaximumPerSound: null, MaximumTotal: null, BaseGain: 0.8f,
        CompensateForVoiceCount: true, LimiterCeiling: null),
    new("uncapped-compensated-limited", MaximumPerSound: null, MaximumTotal: null, BaseGain: 0.8f,
        CompensateForVoiceCount: true, LimiterCeiling: 0.89f),

    // The shipped policy after the gain-compensation change: the budget kept as
    // a backstop at values above measured demand, with the correction doing the
    // loudness work. Mirrors SoundCueBudget's defaults and SoundDirector.
    new("shipped-after-change", MaximumPerSound: 16, MaximumTotal: 64, BaseGain: 0.8f,
        CompensateForVoiceCount: true, LimiterCeiling: null),
    new("shipped-gain-0.72", MaximumPerSound: 16, MaximumTotal: 64, BaseGain: 0.72f,
        CompensateForVoiceCount: true, LimiterCeiling: null),
    new("shipped-gain-0.65", MaximumPerSound: 16, MaximumTotal: 64, BaseGain: 0.65f,
        CompensateForVoiceCount: true, LimiterCeiling: null),
];

Console.WriteLine("policy                        played  suppressed  peakVoices  peak dBFS  clippedSamples  clipped%");

foreach (var policy in policies)
{
    var result = Mixer.Render(
        cues,
        clips,
        TickRate,
        speed,
        sampleRate,
        channels,
        policy);

    Console.WriteLine(
        $"{result.Label,-28}  {result.CuesPlayed,6}  {result.CuesSuppressed,10}  " +
        $"{result.PeakVoices,10}  {result.PeakDbfs,9:F1}  {result.ClippedSamples,14}  " +
        $"{result.ClippedPercent,7:F3}%");

    var fileName = $"{agents}-agents-{speed:F0}x-{result.Label}.wav";
    WavFile.Write(
        Path.Combine(outputDirectory, fileName),
        sampleRate,
        channels,
        result.Samples);
}

Console.WriteLine();
Console.WriteLine("Peak dBFS above 0.0 means the mix asked for more than the format can carry;");
Console.WriteLine("the written WAV hard clips it, which is what the audio device also does.");
return 0;
