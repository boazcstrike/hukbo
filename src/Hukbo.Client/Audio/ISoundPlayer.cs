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

    SoundBindingStatus GetStatus(GameSoundId sound);

    /// <summary>
    /// Requests playback. Called only for a slot whose status is
    /// <see cref="SoundBindingStatus.Ready"/>.
    /// </summary>
    void Play(GameSoundId sound, float volume);
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
                FilePath: null,
                SoundBindingStatus.Missing);
        }

        Bindings = bindings;
    }

    public string DirectoryPath { get; }

    public IReadOnlyList<SoundBinding> Bindings { get; }

    public SoundBindingStatus GetStatus(GameSoundId sound) =>
        SoundBindingStatus.Missing;

    public void Play(GameSoundId sound, float volume) =>
        throw new InvalidOperationException(
            "The silent player has no ready bindings and must never be asked " +
            "to play a sound.");
}
