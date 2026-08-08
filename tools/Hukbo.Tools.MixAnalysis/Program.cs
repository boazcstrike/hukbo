using System.Globalization;
using Hukbo.Core.Combat;
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

// Sixth positional argument: the combat preset, by CombatPresetId member name
// or its numeric value, mirroring HeadlessRunner's --preset parsing. Defaults
// to Scenario's own shipped default (PrecolonialPhilippinesV4, melee-only) so
// an invocation with five or fewer arguments reproduces every prior measurement
// byte-for-byte. Pass PrecolonialPhilippinesV5 to field a ranged roster.
var presetArgument = args.Length > 5 ? args[5] : nameof(CombatPresetId.PrecolonialPhilippinesV4);
if (!TryParsePreset(presetArgument, out var preset))
{
    Console.Error.WriteLine(
        $"Unrecognized or unregistered combat preset: '{presetArgument}'.");
    return 1;
}

audioDirectory = Path.GetFullPath(audioDirectory);
Directory.CreateDirectory(outputDirectory);

Console.WriteLine($"audio directory : {audioDirectory}");
Console.WriteLine($"output directory: {Path.GetFullPath(outputDirectory)}");
Console.WriteLine($"battle          : {agents} agents, seed {seed}, {speed:F0}x speed, preset {preset}");
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

// ---------------------------------------------------------------------------
// Build the cue schedule from a real battle. CueSchedule replicates
// SoundCueMapper/SoundCatalog/HitClassCatalog/SoundLibrary/SoundVariantSelector
// against the raw clip set directly, so it performs the same numbered-match,
// fallback-chain, and bare-file resolution the client's library performs,
// rather than a simplified prefix grouping.
// ---------------------------------------------------------------------------
const int TickRate = 20;
var (cues, ticksRun, outcome, mappedCountsBySlot, mappedEvents) =
    CueSchedule.Build(agents, seed, 10_000, clips, preset);
Console.WriteLine(
    $"battle ran {ticksRun} ticks, outcome {outcome}, " +
    $"{mappedCountsBySlot.Sum()} events mapped, {cues.Count} cues playable");
Console.WriteLine();

var cuesBySlot = new int[CueSchedule.SlotCount];
foreach (var cue in cues)
{
    cuesBySlot[cue.Slot]++;
}

// "mapped" counts every event whose slot resolved (SoundCueMapper.Map
// returned a slot id), whether or not a file exists to play it — the true
// demand figure. "playable" counts only cues that also resolved to a file on
// disk, which is what the mixer actually renders. The two agree everywhere a
// slot has shipped audio; they diverge for the thirteen ranged slots today
// because RU-31 (the sound generation task this measurement gates) has not
// run, so no ranged .wav file exists yet.
Console.WriteLine("events mapped / cues playable per slot (26-slot catalog):");
for (var slot = 0; slot < CueSchedule.SlotCount; slot++)
{
    Console.WriteLine(
        $"  {slot,2}  {CueSchedule.SlotName(slot),-22} " +
        $"mapped {mappedCountsBySlot[slot],6}  playable {cuesBySlot[slot],6}");
}

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

    var worstSlot = result.WorstSlot();
    if (worstSlot >= 0)
    {
        Console.WriteLine(
            $"    worst slot: {worstSlot,2} {CueSchedule.SlotName(worstSlot),-22} " +
            $"{result.GetSlotPeakDbfs(worstSlot),9:F1} dBFS");
    }

    // The three ranged release slots are reported explicitly on every
    // policy, whether or not they carried a cue this run, because a release
    // cue fires on 100% of shots and concentrates on one slot per weapon —
    // the slot likeliest to clip once ranged combat lands. "mapped" is the
    // true per-shot demand (every Release event that resolved a launching
    // weapon); "playable" is how many of those found a .wav file and
    // actually entered this render. They diverge today because no ranged
    // audio exists yet (RU-31 has not run) — peak dBFS is correctly -Infinity
    // whenever playable is 0, and that absence is not a suppression.
    int[] releaseSlots = [13, 14, 15];
    foreach (var releaseSlot in releaseSlots)
    {
        var slotPeak = result.GetSlotPeakDbfs(releaseSlot);
        var slotMapped = mappedCountsBySlot[releaseSlot];
        var slotPlayable = cuesBySlot[releaseSlot];
        Console.WriteLine(
            $"    release slot: {releaseSlot,2} {CueSchedule.SlotName(releaseSlot),-22} " +
            $"mapped {slotMapped,6}  playable {slotPlayable,6}  peak {slotPeak,9:F1} dBFS");
    }

    var fileName = $"{agents}-agents-{speed:F0}x-{result.Label}.wav";
    WavFile.Write(
        Path.Combine(outputDirectory, fileName),
        sampleRate,
        channels,
        result.Samples);
}

// ---------------------------------------------------------------------------
// Does the shipped 16-per-slot / 64-total cap bind on the raw demand, for a
// slot that has no shipped audio and so never reaches Render's cue list at
// all? Render can only ever report 0 suppressed for such a slot, because a
// cue with no file never enters its accounting. This applies the identical
// per-frame cap logic to the full (tick, slot) event stream instead, which is
// the only way to answer the question honestly today.
// ---------------------------------------------------------------------------
var shippedPolicy = new MixPolicy(
    "shipped-gain-0.65", MaximumPerSound: 16, MaximumTotal: 64, BaseGain: 0.65f,
    CompensateForVoiceCount: true, LimiterCeiling: null);
var demandBudget = Mixer.EvaluateDemand(mappedEvents, TickRate, speed, shippedPolicy);

Console.WriteLine(
    $"shipped-policy cap (16/slot, 64 total) applied to raw demand (not just playable cues): " +
    $"{demandBudget.Demanded} demanded, {demandBudget.Accepted} accepted, " +
    $"{demandBudget.Suppressed} suppressed");
int[] releaseSlotsForDemand = [13, 14, 15];
foreach (var releaseSlot in releaseSlotsForDemand)
{
    Console.WriteLine(
        $"    release slot: {releaseSlot,2} {CueSchedule.SlotName(releaseSlot),-22} " +
        $"demanded {mappedCountsBySlot[releaseSlot],6}  " +
        $"suppressed-if-capped {demandBudget.SuppressedBySlot[releaseSlot],6}");
}

Console.WriteLine();
Console.WriteLine("Peak dBFS above 0.0 means the mix asked for more than the format can carry;");
Console.WriteLine("the written WAV hard clips it, which is what the audio device also does.");
return 0;

/// <summary>
/// Parses a <see cref="CombatPresetId"/> from either its member name
/// (case-insensitive) or its numeric value, mirroring
/// <c>HeadlessRunner.TryParsePreset</c>, and rejects anything not registered
/// in <see cref="CombatPresetRegistry"/>.
/// </summary>
static bool TryParsePreset(string value, out CombatPresetId preset)
{
    if (Enum.TryParse(value, ignoreCase: true, out preset) &&
        CombatPresetRegistry.IsRegistered(preset))
    {
        return true;
    }

    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
    {
        preset = (CombatPresetId)numeric;
        return CombatPresetRegistry.IsRegistered(preset);
    }

    preset = default;
    return false;
}
