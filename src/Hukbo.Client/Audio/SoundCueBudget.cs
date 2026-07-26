namespace Hukbo.Client.Audio;

/// <summary>
/// Per-frame playback budget. Two hundred agents can produce dozens of attacks
/// in a single tick, and one frame at 4x speed can advance several ticks, so
/// without a cap the audio device would be handed more voices than it has and
/// the result would be noise rather than feedback.
/// </summary>
/// <remarks>
/// The default limits are provisional tuning values chosen so a busy tick still
/// reads as a clatter of blows rather than a wall of sound. They are not
/// measurements of anything.
/// </remarks>
internal sealed class SoundCueBudget
{
    public const int DefaultMaximumPerSound = 3;
    public const int DefaultMaximumTotal = 8;

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
