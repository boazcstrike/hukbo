namespace Hukbo.Tools.MixAnalysis;

/// <summary>
/// The playback policy a render is measured under.
/// </summary>
/// <param name="Label">Short name used in output and file names.</param>
/// <param name="MaximumPerSound">Per-slot cap per frame, or <c>null</c> for none.</param>
/// <param name="MaximumTotal">Total cap per frame, or <c>null</c> for none.</param>
/// <param name="BaseGain">Per-cue gain before any compensation.</param>
/// <param name="CompensateForVoiceCount">
/// When true, each cue's gain is divided by the square root of the number of
/// voices already sounding, the standard correction for summing uncorrelated
/// material.
/// </param>
/// <param name="LimiterCeiling">
/// Bus limiter ceiling in linear amplitude, or <c>null</c> for no limiter.
/// </param>
internal readonly record struct MixPolicy(
    string Label,
    int? MaximumPerSound,
    int? MaximumTotal,
    float BaseGain,
    bool CompensateForVoiceCount,
    float? LimiterCeiling);

internal readonly record struct MixResult(
    string Label,
    float[] Samples,
    int CuesPlayed,
    int CuesSuppressed,
    int PeakVoices,
    float PeakAmplitude,
    long ClippedSamples,
    long TotalSamples,
    float[] PerSlotPeakAmplitude)
{
    public double PeakDbfs => PeakAmplitude <= 0
        ? double.NegativeInfinity
        : 20.0 * Math.Log10(PeakAmplitude);

    public double ClippedPercent => TotalSamples == 0
        ? 0
        : 100.0 * ClippedSamples / TotalSamples;

    /// <summary>
    /// The finished-mix peak while <paramref name="slot"/> had at least one
    /// voice sounding — how loud the bus gets while that slot is playing, not
    /// merely the clip's own amplitude.
    /// </summary>
    public double GetSlotPeakDbfs(int slot)
    {
        var amplitude = PerSlotPeakAmplitude[slot];
        return amplitude <= 0 ? double.NegativeInfinity : 20.0 * Math.Log10(amplitude);
    }

    /// <summary>The slot with the highest recorded peak, or -1 if none played.</summary>
    public int WorstSlot()
    {
        var worst = -1;
        var worstAmplitude = 0f;
        for (var slot = 0; slot < PerSlotPeakAmplitude.Length; slot++)
        {
            if (PerSlotPeakAmplitude[slot] > worstAmplitude)
            {
                worstAmplitude = PerSlotPeakAmplitude[slot];
                worst = slot;
            }
        }

        return worst;
    }
}

/// <summary>
/// The outcome of applying a per-slot/per-total cap to raw demand, with no
/// audio involved. Answers "would <see cref="MixPolicy.MaximumPerSound"/> and
/// <see cref="MixPolicy.MaximumTotal"/> suppress anything" for a slot that has
/// no shipped clip and so never reaches <see cref="Mixer.Render"/>.
/// </summary>
internal readonly record struct DemandBudgetResult(
    int Demanded,
    int Accepted,
    int Suppressed,
    int[] SuppressedBySlot);

internal static class Mixer
{
    private const int FramesPerSecond = 60;

    /// <summary>
    /// Renders a cue schedule to an interleaved stereo buffer, applying the
    /// policy's frame budget the way the client does — cleared once per
    /// rendered frame and shared by every tick that frame advanced.
    /// </summary>
    public static MixResult Render(
        IReadOnlyList<ScheduledCue> cues,
        IReadOnlyDictionary<string, WavClip> clips,
        int tickRate,
        double speedMultiplier,
        int sampleRate,
        int channels,
        MixPolicy policy)
    {
        var lastTick = cues.Count == 0 ? 0 : cues[^1].Tick;
        var tailFrames = sampleRate; // one second of tail for the last clip
        var totalFrames = (int)(lastTick / (double)tickRate / speedMultiplier * sampleRate) + tailFrames;
        var buffer = new float[totalFrames * channels];

        // Frame index a tick lands in, so the budget can be shared exactly as
        // the client shares it.
        var framesPerTick = FramesPerSecond / (tickRate * speedMultiplier);

        var perSound = new int[CueSchedule.SlotCount];
        var total = 0;
        var currentFrame = -1L;

        var played = 0;
        var suppressed = 0;

        // Voice bookkeeping: the sample index at which each sounding voice ends.
        var activeUntil = new List<int>();
        var peakVoices = 0;

        // Every played cue's (slot, startFrame, endFrame), so the per-slot
        // peak can be measured against the finished buffer after the limiter
        // (if any) has run, rather than against each clip in isolation.
        var playedRanges = new List<(int Slot, int StartFrame, int EndFrame)>();

        foreach (var cue in cues)
        {
            var frameIndex = (long)(cue.Tick * framesPerTick);
            if (frameIndex != currentFrame)
            {
                currentFrame = frameIndex;
                Array.Clear(perSound);
                total = 0;
            }

            if ((policy.MaximumTotal is { } maxTotal && total >= maxTotal) ||
                (policy.MaximumPerSound is { } maxPerSound && perSound[cue.Slot] >= maxPerSound))
            {
                suppressed++;
                continue;
            }

            perSound[cue.Slot]++;
            total++;

            if (!clips.TryGetValue(cue.FileName, out var clip))
            {
                continue;
            }

            var startFrame = (int)(cue.Tick / (double)tickRate / speedMultiplier * sampleRate);
            activeUntil.RemoveAll(end => end <= startFrame);
            var voicesSounding = activeUntil.Count;
            peakVoices = Math.Max(peakVoices, voicesSounding + 1);

            var gain = policy.BaseGain;
            if (policy.CompensateForVoiceCount)
            {
                gain /= MathF.Sqrt(voicesSounding + 1);
            }

            Overlay(buffer, clip, startFrame, channels, gain);
            activeUntil.Add(startFrame + clip.FrameCount);
            playedRanges.Add((cue.Slot, startFrame, startFrame + clip.FrameCount));
            played++;
        }

        if (policy.LimiterCeiling is { } ceiling)
        {
            ApplyLimiter(buffer, channels, sampleRate, ceiling);
        }

        var peak = 0f;
        long clipped = 0;
        for (var index = 0; index < buffer.Length; index++)
        {
            var magnitude = MathF.Abs(buffer[index]);
            if (magnitude > peak)
            {
                peak = magnitude;
            }

            if (magnitude > 1f)
            {
                clipped++;
            }
        }

        var perSlotPeak = MeasureSlotPeaks(buffer, channels, playedRanges);

        return new MixResult(
            policy.Label,
            buffer,
            played,
            suppressed,
            peakVoices,
            peak,
            clipped,
            buffer.Length,
            perSlotPeak);
    }

    /// <summary>
    /// Applies exactly the frame-clearing and cap logic <see cref="Render"/>
    /// applies to playable cues, but to the raw (tick, slot) demand stream
    /// instead — no clip lookup, no sample summing, no audio. This is the
    /// only way to ask whether <paramref name="policy"/>'s per-slot or total
    /// cap would suppress a slot that has no shipped <c>.wav</c> file and so
    /// never enters <see cref="Render"/>'s cue list at all, as is true today
    /// for every ranged slot: the schedule and its timing are real,
    /// measured output of the same <c>BattleSimulation</c>
    /// <see cref="CueSchedule.Build"/> ran; only the audio is absent.
    /// </summary>
    public static DemandBudgetResult EvaluateDemand(
        IReadOnlyList<(long Tick, int Slot)> events,
        int tickRate,
        double speedMultiplier,
        MixPolicy policy)
    {
        var framesPerTick = FramesPerSecond / (tickRate * speedMultiplier);

        var perSound = new int[CueSchedule.SlotCount];
        var suppressedBySlot = new int[CueSchedule.SlotCount];
        var total = 0;
        var currentFrame = -1L;

        var accepted = 0;
        var suppressed = 0;

        foreach (var (tick, slot) in events)
        {
            var frameIndex = (long)(tick * framesPerTick);
            if (frameIndex != currentFrame)
            {
                currentFrame = frameIndex;
                Array.Clear(perSound);
                total = 0;
            }

            if ((policy.MaximumTotal is { } maxTotal && total >= maxTotal) ||
                (policy.MaximumPerSound is { } maxPerSound && perSound[slot] >= maxPerSound))
            {
                suppressed++;
                suppressedBySlot[slot]++;
                continue;
            }

            perSound[slot]++;
            total++;
            accepted++;
        }

        return new DemandBudgetResult(events.Count, accepted, suppressed, suppressedBySlot);
    }

    /// <summary>
    /// For each slot, the highest magnitude the finished (post-limiter)
    /// buffer reaches during any frame the slot has a voice sounding.
    /// </summary>
    private static float[] MeasureSlotPeaks(
        float[] buffer,
        int channels,
        List<(int Slot, int StartFrame, int EndFrame)> playedRanges)
    {
        var frames = buffer.Length / channels;
        var perSlotPeak = new float[CueSchedule.SlotCount];

        foreach (var (slot, startFrame, endFrame) in playedRanges)
        {
            var clampedStart = Math.Max(0, startFrame);
            var clampedEnd = Math.Min(frames, endFrame);
            for (var frame = clampedStart; frame < clampedEnd; frame++)
            {
                var offset = frame * channels;
                for (var channel = 0; channel < channels; channel++)
                {
                    var magnitude = MathF.Abs(buffer[offset + channel]);
                    if (magnitude > perSlotPeak[slot])
                    {
                        perSlotPeak[slot] = magnitude;
                    }
                }
            }
        }

        return perSlotPeak;
    }

    private static void Overlay(
        float[] buffer,
        WavClip clip,
        int startFrame,
        int channels,
        float gain)
    {
        var start = startFrame * channels;
        var count = Math.Min(clip.Samples.Length, buffer.Length - start);
        for (var index = 0; index < count; index++)
        {
            buffer[start + index] += clip.Samples[index] * gain;
        }
    }

    /// <summary>
    /// A plain feed-forward peak limiter: fast attack, slow release, operating
    /// on the loudest channel of each frame so the stereo image does not shift.
    /// Not a mastering-grade limiter — enough to show whether holding the bus
    /// under full scale removes the overload without gutting the transients.
    /// </summary>
    private static void ApplyLimiter(
        float[] buffer,
        int channels,
        int sampleRate,
        float ceiling)
    {
        var attackCoefficient = MathF.Exp(-1f / (0.005f * sampleRate));
        var releaseCoefficient = MathF.Exp(-1f / (0.100f * sampleRate));
        var envelope = 0f;
        var frames = buffer.Length / channels;

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * channels;
            var magnitude = 0f;
            for (var channel = 0; channel < channels; channel++)
            {
                magnitude = MathF.Max(magnitude, MathF.Abs(buffer[offset + channel]));
            }

            var coefficient = magnitude > envelope ? attackCoefficient : releaseCoefficient;
            envelope = (coefficient * envelope) + ((1f - coefficient) * magnitude);

            var gain = envelope > ceiling ? ceiling / envelope : 1f;
            for (var channel = 0; channel < channels; channel++)
            {
                buffer[offset + channel] *= gain;
            }
        }
    }
}
