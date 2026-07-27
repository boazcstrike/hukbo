using System.Diagnostics;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

// Measures what this machine's MonoGame 3.8.5 DesktopGL (OpenAL) backend can
// actually do: the hard ceiling on simultaneous voices, and the CPU cost of
// sustaining a given cue rate. Runs headless — no window, no Game class — so it
// measures the audio device alone.
//
// Volume is deliberately near-silent by default so a 300-voice burst does not
// hurt anyone's ears. Pass a volume as the second argument to hear it.

var audioDirectory = Path.GetFullPath(args.Length > 0
    ? args[0]
    : Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "Hukbo.Client", "Content", "Audio"));
var volume = args.Length > 1
    ? float.Parse(args[1], CultureInfo.InvariantCulture)
    : 0.02f;

Console.WriteLine($"audio directory : {audioDirectory}");
Console.WriteLine($"playback volume : {volume:F3}");
Console.WriteLine();

var files = Directory
    .EnumerateFiles(audioDirectory, "*.wav")
    .Order(StringComparer.Ordinal)
    .ToArray();

if (files.Length == 0)
{
    Console.Error.WriteLine("No .wav files found.");
    return 1;
}

var effects = new List<SoundEffect>(files.Length);
foreach (var file in files)
{
    try
    {
        using var stream = File.OpenRead(file);
        effects.Add(SoundEffect.FromStream(stream));
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"load failed: {Path.GetFileName(file)} — {exception.GetType().Name}");
    }
}

if (effects.Count == 0)
{
    Console.Error.WriteLine("No usable sound effects. Is there an audio device?");
    return 1;
}

var totalSeconds = effects.Sum(effect => effect.Duration.TotalSeconds);
Console.WriteLine($"loaded {effects.Count} clips, mean duration {totalSeconds / effects.Count * 1000:F0} ms, " +
    $"min {effects.Min(effect => effect.Duration.TotalMilliseconds):F0} ms, " +
    $"max {effects.Max(effect => effect.Duration.TotalMilliseconds):F0} ms");
Console.WriteLine($"SoundEffect.MasterVolume = {SoundEffect.MasterVolume}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Phase A — hard ceiling on simultaneous voices.
// Explicit SoundEffectInstance objects, started as fast as possible, so the
// count that are genuinely in the Playing state is observable rather than
// inferred. Uses the longest clip so nothing finishes during the ramp.
// ---------------------------------------------------------------------------
Console.WriteLine("=== Phase A: simultaneous voice ceiling (explicit instances) ===");

var longest = effects.OrderByDescending(effect => effect.Duration).First();
Console.WriteLine($"clip: {longest.Duration.TotalMilliseconds:F0} ms");

var instances = new List<SoundEffectInstance>();
var createFailedAt = -1;
var playFailedAt = -1;
var playExceptionAt = -1;
string? playExceptionName = null;
const int CeilingAttempts = 512;

for (var index = 0; index < CeilingAttempts; index++)
{
    SoundEffectInstance instance;
    try
    {
        instance = longest.CreateInstance();
    }
    catch (Exception exception)
    {
        createFailedAt = index;
        playExceptionName ??= exception.GetType().Name;
        break;
    }

    instance.Volume = volume;
    instances.Add(instance);

    try
    {
        instance.Play();
    }
    catch (Exception exception)
    {
        playExceptionAt = index;
        playExceptionName = exception.GetType().Name;
        break;
    }

    if (instance.State != SoundState.Playing && playFailedAt < 0)
    {
        playFailedAt = index;
    }
}

FrameworkDispatcher.Update();
var actuallyPlaying = instances.Count(instance => instance.State == SoundState.Playing);

Console.WriteLine($"instances created        : {instances.Count}");
Console.WriteLine($"reporting SoundState.Playing: {actuallyPlaying}");
Console.WriteLine($"first instance that did not enter Playing: " +
    (playFailedAt < 0 ? "none" : playFailedAt.ToString(CultureInfo.InvariantCulture)));
Console.WriteLine($"CreateInstance threw at   : {(createFailedAt < 0 ? "never" : createFailedAt.ToString(CultureInfo.InvariantCulture))}");
Console.WriteLine($"Play threw at             : {(playExceptionAt < 0 ? "never" : playExceptionAt.ToString(CultureInfo.InvariantCulture))}" +
    (playExceptionName is null ? "" : $" ({playExceptionName})"));

foreach (var instance in instances)
{
    instance.Stop();
    instance.Dispose();
}

instances.Clear();
FrameworkDispatcher.Update();
Thread.Sleep(400);
Console.WriteLine();

// ---------------------------------------------------------------------------
// Phase B — fire-and-forget SoundEffect.Play(), the call the game actually
// makes. Sustains a fixed cue rate for a fixed duration at a simulated 60 fps
// and reports how many calls the backend refused, plus the CPU and allocation
// cost of the audio work itself.
// ---------------------------------------------------------------------------
Console.WriteLine("=== Phase B: sustained fire-and-forget rate (SoundEffect.Play) ===");
Console.WriteLine("rate/s  attempted  refused   refused%  audioCpuMs/frame  allocKB/s  peakWorkingSetMB");

int[] rates = [20, 50, 100, 200, 400, 800, 1600];
const double DurationSeconds = 6.0;
const double FrameSeconds = 1.0 / 60.0;

var random = new Random(12345);

foreach (var rate in rates)
{
    var frames = (int)(DurationSeconds / FrameSeconds);
    var cuesPerFrame = rate * FrameSeconds;
    var carry = 0.0;
    var attempted = 0;
    var refused = 0;
    double audioCpuMs = 0;
    var allocStart = GC.GetAllocatedBytesForCurrentThread();
    var wallStart = Stopwatch.GetTimestamp();

    for (var frame = 0; frame < frames; frame++)
    {
        var frameStart = Stopwatch.GetTimestamp();

        carry += cuesPerFrame;
        var thisFrame = (int)carry;
        carry -= thisFrame;

        for (var cue = 0; cue < thisFrame; cue++)
        {
            var effect = effects[random.Next(effects.Count)];
            attempted++;
            try
            {
                if (!effect.Play(volume, 0f, 0f))
                {
                    refused++;
                }
            }
            catch (Exception exception) when (
                exception is InstancePlayLimitException or
                NoAudioHardwareException or
                InvalidOperationException)
            {
                refused++;
            }
        }

        FrameworkDispatcher.Update();
        audioCpuMs += Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;

        // Pace to a real 60 fps so clips get wall-clock time to finish and the
        // measured concurrency matches what the game would produce.
        var spent = Stopwatch.GetElapsedTime(frameStart).TotalSeconds;
        var remaining = FrameSeconds - spent;
        if (remaining > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds(remaining));
        }
    }

    var wallSeconds = Stopwatch.GetElapsedTime(wallStart).TotalSeconds;
    var allocBytes = GC.GetAllocatedBytesForCurrentThread() - allocStart;
    var peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024.0);

    Console.WriteLine(
        $"{rate,6}  {attempted,9}  {refused,7}  {(attempted == 0 ? 0 : 100.0 * refused / attempted),7:F1}%  " +
        $"{audioCpuMs / frames,16:F3}  {allocBytes / 1024.0 / wallSeconds,9:F1}  {peakWorkingSet,17:F1}");

    // Let the device drain before the next rate so one step does not poison the
    // next one's ceiling.
    Thread.Sleep(1200);
    FrameworkDispatcher.Update();
}

Console.WriteLine();
Hukbo.Tools.VoiceStress.Recycle.Run(effects, volume);

Console.WriteLine("=== teardown ===");
foreach (var effect in effects)
{
    effect.Dispose();
}

Console.WriteLine("done");
return 0;
