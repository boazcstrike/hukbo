using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

// Task 48 — measures the MonoGame SoundEffectInstance pool ceiling this
// machine's DesktopGL (OpenAL) backend can sustain under automatic-fire
// load, and records the instance count at which InstancePlayLimitException
// first fires. Design section 10 ("The MonoGame instance pool is the real
// ceiling", docs/plans/2026-08-07-sandata-scaffold-design.md) names this
// measurement, and plan task 48 hands it to this project.
//
// This is deliberately the MonoGame pool itself, not Sandata's own budget
// wrapper: SandataSoundPlayer, SandataSoundBudget, and ISandataSoundOutput
// (src/Sandata.Client/Audio/) are all `internal` to Sandata.Client, which
// grants InternalsVisibleTo to Sandata.Client.Tests only. Widening that grant
// is an open decision the user has not taken, so this harness allocates and
// plays SoundEffectInstance objects directly, mirroring the shape design
// section 10 describes — one looping instance plus one non-looping tail
// instance per shooter, never one instance per round — without depending on
// any internal Sandata type. It follows tools/Hukbo.Tools.VoiceStress/,
// the closest existing precedent: headless, no window, no Game class, so it
// measures the audio device alone.
//
// Sandata has not shipped audio content yet (that is a later task), so every
// clip here is synthesized in memory as raw 16-bit PCM rather than loaded
// from a WAV file. The instance-pool ceiling is a property of the MonoGame
// device, not of any particular clip's content, so a synthetic tone measures
// exactly the same ceiling a shipped gunshot WAV would.
//
// Runs headless: no window, no Game class. Volume is deliberately
// near-silent by default, matching Hukbo.Tools.VoiceStress's precedent, so a
// multi-hundred-instance ramp does not hurt anyone's ears.

// PROVISIONAL: "the maximum operator count" is not pinned to a number
// anywhere in the design or plan documents, and no MaxOperatorCount-shaped
// constant exists in Sandata.Core today. This harness uses 8 total shooters
// (both factions combined) for Phase B's sustained hold, matching the one
// concrete figure design section 10 itself uses in the same paragraph that
// names this harness ("eight shooters at 800 rounds per minute") and the
// 4-operators-per-faction roster already exercised in
// tests/Sandata.Core.Tests/MissionStateTests.cs (4 + 4 = 8). Task 54 should
// confirm or replace this figure once a real roster ceiling is decided.
const int MaxOperatorCount = 8;

// PROVISIONAL: synthetic clip durations. The loop duration is arbitrary — it
// only needs to be short enough that IsLooped keeps it audibly continuous.
// The tail duration targets roughly 4x the 0.191-second melee mean design
// section 10 cites, landing inside its stated 3x-5x range for how long a
// gunshot tail holds an instance.
const int LoopClipMs = 150;
const int TailClipMs = 764;
const int SampleRateHz = 44100;
const float PlaybackVolume = 0.02f;
const int MaxShooterAttempts = 256;
const double SustainedFireSeconds = 10.0;

var hardwareDescription =
    $"{Environment.MachineName} | {RuntimeInformation.OSDescription} " +
    $"({RuntimeInformation.OSArchitecture}) | {Environment.ProcessorCount} logical processors | " +
    $"{RuntimeInformation.FrameworkDescription}";

Console.WriteLine("=== hardware ===");
Console.WriteLine($"hardware description: {hardwareDescription}");
Console.WriteLine();

var loopTone = BuildTone(LoopClipMs, frequencyHz: 220.0, SampleRateHz);
var tailTone = BuildTone(TailClipMs, frequencyHz: 180.0, SampleRateHz);

Console.WriteLine($"synthetic loop clip: requested {LoopClipMs} ms, reported {loopTone.Duration.TotalMilliseconds:F0} ms");
Console.WriteLine($"synthetic tail clip: requested {TailClipMs} ms, reported {tailTone.Duration.TotalMilliseconds:F0} ms");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Phase A — shooter-pair ceiling ramp. Adds one shooter at a time, where a
// shooter is one looping instance (the automatic-fire loop) plus one
// non-looping instance (the tail), matching
// SandataSoundPlayer.HandleAutomaticRound's "one loop instance plus one tail
// instance per shooter" pairing. Stops at the first instance MonoGame
// refuses.
// ---------------------------------------------------------------------------
Console.WriteLine("=== Phase A: shooter-pair ceiling ramp ===");

var heldInstances = new List<SoundEffectInstance>();
var instancesAttempted = 0;
var shooterCountAtFirstFailure = -1;
var instanceCountAtFirstFailure = -1;
string? firstFailureExceptionName = null;
var rampStart = Stopwatch.GetTimestamp();

for (var shooter = 1; shooter <= MaxShooterAttempts; shooter++)
{
    if (!TryPlayInstance(loopTone, isLooped: true, PlaybackVolume, heldInstances, ref instancesAttempted, out var exceptionName))
    {
        shooterCountAtFirstFailure = shooter;
        instanceCountAtFirstFailure = instancesAttempted;
        firstFailureExceptionName = exceptionName;
        break;
    }

    if (!TryPlayInstance(tailTone, isLooped: false, PlaybackVolume, heldInstances, ref instancesAttempted, out exceptionName))
    {
        shooterCountAtFirstFailure = shooter;
        instanceCountAtFirstFailure = instancesAttempted;
        firstFailureExceptionName = exceptionName;
        break;
    }

    FrameworkDispatcher.Update();
}

var rampSeconds = Stopwatch.GetElapsedTime(rampStart).TotalSeconds;

Console.WriteLine($"ramp duration            : {rampSeconds:F1} s");
Console.WriteLine($"shooters held             : {heldInstances.Count / 2}");
if (firstFailureExceptionName is null)
{
    Console.WriteLine(
        $"instance count at first InstancePlayLimitException: none through {instancesAttempted} instances " +
        $"({MaxShooterAttempts} shooters attempted)");
}
else
{
    Console.WriteLine(
        $"instance count at first InstancePlayLimitException: {instanceCountAtFirstFailure} " +
        $"(shooter #{shooterCountAtFirstFailure}, {firstFailureExceptionName})");
}

Console.WriteLine();

foreach (var instance in heldInstances)
{
    instance.Stop();
    instance.Dispose();
}

heldInstances.Clear();
FrameworkDispatcher.Update();
Thread.Sleep(400);

// ---------------------------------------------------------------------------
// Phase B — sustained automatic fire from the maximum operator count. Holds
// MaxOperatorCount looping instances continuously for SustainedFireSeconds,
// while periodically firing a tail cue through fire-and-forget
// SoundEffect.Play — the same call the game itself makes for a report that
// does not need per-instance control — to keep stressing the pool for the
// whole duration rather than only at the instant fire starts.
// ---------------------------------------------------------------------------
Console.WriteLine("=== Phase B: sustained automatic fire at the maximum operator count ===");
Console.WriteLine(
    $"holding {MaxOperatorCount} shooters (each: one looping instance) for {SustainedFireSeconds:F0} s, " +
    "with a tail cue fired every tail-duration interval");
Console.WriteLine();

var sustainedLoops = new List<SoundEffectInstance>();
string? sustainedFailureExceptionName = null;
double? sustainedFailureAtSeconds = null;

for (var shooter = 0; shooter < MaxOperatorCount; shooter++)
{
    var instance = loopTone.CreateInstance();
    instance.IsLooped = true;
    instance.Volume = PlaybackVolume;

    try
    {
        instance.Play();
        sustainedLoops.Add(instance);
    }
    catch (Exception exception) when (
        exception is InstancePlayLimitException or NoAudioHardwareException or InvalidOperationException)
    {
        sustainedFailureExceptionName = exception.GetType().Name;
        sustainedFailureAtSeconds = 0.0;
        break;
    }
}

var tailRetriggerCount = 0;
var tailRetriggerRefused = 0;
var sustainStart = Stopwatch.GetTimestamp();

if (sustainedFailureExceptionName is null)
{
    var tailIntervalSeconds = TailClipMs / 1000.0;
    var nextTailAtSeconds = 0.0;

    while (Stopwatch.GetElapsedTime(sustainStart).TotalSeconds < SustainedFireSeconds)
    {
        var elapsedSeconds = Stopwatch.GetElapsedTime(sustainStart).TotalSeconds;

        if (elapsedSeconds >= nextTailAtSeconds)
        {
            tailRetriggerCount++;

            try
            {
                if (!tailTone.Play(PlaybackVolume, pitch: 0f, pan: 0f))
                {
                    tailRetriggerRefused++;
                }
            }
            catch (Exception exception) when (
                exception is InstancePlayLimitException or NoAudioHardwareException or InvalidOperationException)
            {
                tailRetriggerRefused++;
                sustainedFailureExceptionName = exception.GetType().Name;
                sustainedFailureAtSeconds = elapsedSeconds;
            }

            nextTailAtSeconds += tailIntervalSeconds;
        }

        FrameworkDispatcher.Update();
        Thread.Sleep(16);
    }
}

var sustainedSeconds = Stopwatch.GetElapsedTime(sustainStart).TotalSeconds;
var loopsStillPlaying = sustainedLoops.Count(instance => instance.State == SoundState.Playing);

Console.WriteLine($"sustained-fire duration    : {sustainedSeconds:F1} s (target {SustainedFireSeconds:F0} s)");
Console.WriteLine($"loop instances still playing at end: {loopsStillPlaying} / {sustainedLoops.Count}");
Console.WriteLine($"tail cues fired            : {tailRetriggerCount}, refused {tailRetriggerRefused}");

if (sustainedFailureExceptionName is null)
{
    Console.WriteLine(
        $"no InstancePlayLimitException while sustaining {MaxOperatorCount} shooters for {sustainedSeconds:F1} s.");
}
else
{
    Console.WriteLine(
        $"sustained-fire failure at {sustainedFailureAtSeconds:F1} s: {sustainedFailureExceptionName}");
}

foreach (var instance in sustainedLoops)
{
    instance.Stop();
    instance.Dispose();
}

Console.WriteLine();
Console.WriteLine("=== teardown ===");
loopTone.Dispose();
tailTone.Dispose();
Console.WriteLine("done");
return 0;

static SoundEffect BuildTone(int durationMs, double frequencyHz, int sampleRateHz)
{
    var sampleCount = sampleRateHz * durationMs / 1000;
    var buffer = new byte[sampleCount * 2];
    const short Amplitude = 6000;

    for (var sample = 0; sample < sampleCount; sample++)
    {
        var t = (double)sample / sampleRateHz;
        var value = (short)(Amplitude * Math.Sin(2.0 * Math.PI * frequencyHz * t));
        var sampleBytes = BitConverter.GetBytes(value);
        buffer[sample * 2] = sampleBytes[0];
        buffer[(sample * 2) + 1] = sampleBytes[1];
    }

    return new SoundEffect(buffer, sampleRateHz, AudioChannels.Mono);
}

static bool TryPlayInstance(
    SoundEffect clip,
    bool isLooped,
    float volume,
    List<SoundEffectInstance> held,
    ref int attempted,
    out string? exceptionName)
{
    attempted++;
    exceptionName = null;

    try
    {
        var instance = clip.CreateInstance();
        instance.IsLooped = isLooped;
        instance.Volume = volume;
        instance.Play();
        held.Add(instance);
        return true;
    }
    catch (Exception exception) when (
        exception is InstancePlayLimitException or NoAudioHardwareException or InvalidOperationException)
    {
        exceptionName = exception.GetType().Name;
        return false;
    }
}
