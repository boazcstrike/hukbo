namespace Hukbo.Client.Audio;

/// <summary>
/// Per-frame playback ceiling. A backstop against a pathological scenario, not
/// a throttle on ordinary play.
/// </summary>
/// <remarks>
/// <para>
/// The original limits of three per slot and eight in total were chosen to
/// prevent an event volume that measurement showed does not occur: a 500-agent
/// battle peaks at 21 cues in a frame and a 200-agent battle at 15, against a
/// backend that plays 256 voices at once. Those limits discarded real cues for
/// no benefit, so they were raised above measured demand.
/// </para>
/// <para>
/// Loudness is handled where it belongs, by <see cref="SoundVoiceLedger"/>
/// scaling each cue's gain against the voices already sounding. This type no
/// longer has any part in that.
/// </para>
/// <para>
/// The limits remain provisional tuning values rather than measurements. See
/// <c>docs/research/SOUND-CAPACITY-MEASUREMENTS.md</c>.
/// </para>
/// </remarks>
internal sealed class SoundCueBudget
{
    public const int DefaultMaximumPerSound = 16;
    public const int DefaultMaximumTotal = 64;

    private readonly int[] _perSoundCounts;
    private int _total;

    public SoundCueBudget(
        int maximumPerSound = DefaultMaximumPerSound,
        int maximumTotal = DefaultMaximumTotal)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPerSound);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotal);

        MaximumPerSound = maximumPerSound;
        MaximumTotal = maximumTotal;
        _perSoundCounts = new int[SoundCatalog.AllSounds.Count];
    }

    public int MaximumPerSound { get; }

    public int MaximumTotal { get; }

    /// <summary>
    /// Clears the frame's counters. Call once per frame, before ingesting the
    /// ticks that frame advanced.
    /// </summary>
    public void BeginFrame()
    {
        Array.Clear(_perSoundCounts);
        _total = 0;
    }

    /// <summary>
    /// Reserves one playback slot for <paramref name="sound"/>, returning
    /// <c>false</c> when either cap is already reached.
    /// </summary>
    public bool TryConsume(GameSoundId sound)
    {
        var index = GetIndex(sound);
        if (_total >= MaximumTotal ||
            _perSoundCounts[index] >= MaximumPerSound)
        {
            return false;
        }

        _perSoundCounts[index]++;
        _total++;
        return true;
    }

    private static int GetIndex(GameSoundId sound)
    {
        for (var index = 0; index < SoundCatalog.AllSounds.Count; index++)
        {
            if (SoundCatalog.AllSounds[index] == sound)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(sound),
            sound,
            "The sound catalog must list every slot.");
    }
}
