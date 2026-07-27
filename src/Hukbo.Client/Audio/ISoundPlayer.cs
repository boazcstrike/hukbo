namespace Hukbo.Client.Audio;

/// <summary>
/// The seam between sound decisions and the audio device. Everything above this
/// interface is pure and testable; the only implementation that touches MonoGame
/// is <c>MonoGameSoundPlayer</c>.
/// </summary>
internal interface ISoundPlayer
{
    /// <summary>The folder the bindings were resolved from.</summary>
    string DirectoryPath { get; }

    /// <summary>
    /// One binding per slot, in <see cref="SoundCatalog.AllSounds"/> order.
    /// </summary>
    IReadOnlyList<SoundBinding> Bindings { get; }

    /// <summary>
    /// The status of one slot's resolved variant list, after the fallback
    /// chain has already been applied. <paramref name="hitClass"/> is
    /// <c>null</c> for a slot that is not hit-location driven.
    /// </summary>
    SoundBindingStatus GetStatus(GameSoundId sound, HitClass? hitClass);

    /// <summary>
    /// The number of variants in the resolved list, so the caller can compute
    /// a selection index. Zero when <see cref="GetStatus"/> is not
    /// <see cref="SoundBindingStatus.Ready"/>.
    /// </summary>
    int GetVariantCount(GameSoundId sound, HitClass? hitClass);

    /// <summary>
    /// How long one specific variant sounds for. Zero when the variant is not
    /// loaded. The director uses this to know how long the cue occupies a
    /// voice, so the figure must be the clip's real length rather than an
    /// assumed one.
    /// </summary>
    double GetDurationSeconds(GameSoundId sound, HitClass? hitClass, int variantIndex);

    /// <summary>
    /// Requests playback of one specific variant. Called only for a
    /// (<paramref name="sound"/>, <paramref name="hitClass"/>) pair whose
    /// status is <see cref="SoundBindingStatus.Ready"/>, with
    /// <paramref name="variantIndex"/> in
    /// <c>[0, GetVariantCount(sound, hitClass))</c>.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the backend declined the cue — on MonoGame, an
    /// exhausted instance pool or source list. The caller records that as
    /// <see cref="SoundCueStatus.Refused"/> rather than reporting a cue that
    /// never sounded as played.
    /// </returns>
    bool Play(GameSoundId sound, HitClass? hitClass, int variantIndex, float volume);
}

/// <summary>
/// The player used before content is loaded, in tests, and anywhere audio must
/// be inert. Every slot reports <see cref="SoundBindingStatus.Missing"/>, so the
/// director records missing cues and never asks for playback.
/// </summary>
internal sealed class SilentSoundPlayer : ISoundPlayer
{
    public SilentSoundPlayer(string directoryPath = "")
    {
        ArgumentNullException.ThrowIfNull(directoryPath);

        DirectoryPath = directoryPath;
        var bindings = new SoundBinding[SoundCatalog.AllSounds.Count];
        for (var index = 0; index < SoundCatalog.AllSounds.Count; index++)
        {
            var sound = SoundCatalog.AllSounds[index];
            bindings[index] = new SoundBinding(
                sound,
                SoundCatalog.GetFileName(sound),
                SoundCatalog.IsHitLocationDriven(sound)
                    ? BuildMissingClassCounts()
                    : [],
                VariantCount: 0,
                SoundBindingStatus.Missing);
        }

        Bindings = bindings;
    }

    public string DirectoryPath { get; }

    public IReadOnlyList<SoundBinding> Bindings { get; }

    public SoundBindingStatus GetStatus(GameSoundId sound, HitClass? hitClass) =>
        SoundBindingStatus.Missing;

    public int GetVariantCount(GameSoundId sound, HitClass? hitClass) => 0;

    public double GetDurationSeconds(
        GameSoundId sound,
        HitClass? hitClass,
        int variantIndex) => 0;

    public bool Play(
        GameSoundId sound,
        HitClass? hitClass,
        int variantIndex,
        float volume) =>
        throw new InvalidOperationException(
            "The silent player has no ready bindings and must never be asked " +
            "to play a sound.");

    private static SoundClassCount[] BuildMissingClassCounts()
    {
        var counts = new SoundClassCount[HitClassCatalog.All.Count];
        for (var index = 0; index < counts.Length; index++)
        {
            counts[index] = new SoundClassCount(
                HitClassCatalog.All[index],
                Count: 0,
                SoundBindingStatus.Missing);
        }

        return counts;
    }
}
