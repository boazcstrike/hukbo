namespace Hukbo.Client.Audio;

/// <summary>
/// Tracks how many cues are still sounding, so the director can scale a new
/// cue's gain against the load it is joining.
/// </summary>
/// <remarks>
/// <para>
/// The ledger holds no clock of its own. <see cref="Advance"/> is fed the
/// frame's elapsed seconds by the caller, which keeps the time dependence
/// explicit and lets every test drive it without a wall clock, a
/// <c>GameTime</c>, or a real audio device.
/// </para>
/// <para>
/// Playback speed needs no special handling. A clip occupies a real voice for
/// a real duration however fast the simulation is advancing, and this clock is
/// real time — so a faster battle simply lands more cues inside the same
/// window and the ledger reports the higher count that actually exists.
/// </para>
/// </remarks>
internal sealed class SoundVoiceLedger
{
    /// <summary>
    /// Hard bound on retained entries. The frame budget already caps how many
    /// cues can start, so this only guards against a caller that ignores it.
    /// </summary>
    public const int MaximumTrackedVoices = 512;

    private readonly List<double> _endTimes = [];
    private double _now;

    /// <summary>
    /// Voices still sounding as of the last <see cref="Advance"/>. Expired
    /// entries are dropped by <see cref="Advance"/>, so this is a plain count.
    /// </summary>
    public int SoundingVoices => _endTimes.Count;

    /// <summary>
    /// Moves the presentation clock forward and retires every voice whose clip
    /// has finished. Call once per frame, before the frame's cues.
    /// </summary>
    public void Advance(double elapsedSeconds)
    {
        if (elapsedSeconds > 0)
        {
            _now += elapsedSeconds;
        }

        // A cue that ends exactly now is finished, so the comparison is
        // inclusive: at the boundary the voice is free.
        _endTimes.RemoveAll(endTime => endTime <= _now);
    }

    /// <summary>
    /// Records that a cue lasting <paramref name="durationSeconds"/> has just
    /// started. A non-positive duration is not tracked: it would expire on the
    /// next <see cref="Advance"/> anyway, and counting it would overstate the
    /// load for the cues that follow it in the same frame.
    /// </summary>
    public void Add(double durationSeconds)
    {
        if (durationSeconds <= 0 || _endTimes.Count >= MaximumTrackedVoices)
        {
            return;
        }

        _endTimes.Add(_now + durationSeconds);
    }

    /// <summary>
    /// The gain a cue should play at given the voices already sounding.
    /// Dividing by the square root of the voice count is the standard
    /// correction for summing uncorrelated material, which impacts from
    /// different agents largely are.
    /// </summary>
    /// <remarks>
    /// Deliberately unfloored. A floor would reintroduce the overload this
    /// exists to remove: at the measured worst case of 113 voices the gain
    /// lands near 0.075, which is correct, because 113 things are sounding.
    /// The exponent and <paramref name="baseGain"/> are provisional tuning
    /// values, not measurements of anything.
    /// </remarks>
    public float GetGainForNextCue(float baseGain)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseGain);

        return baseGain / MathF.Sqrt(_endTimes.Count + 1);
    }

    public void Clear()
    {
        _endTimes.Clear();
        _now = 0;
    }
}
