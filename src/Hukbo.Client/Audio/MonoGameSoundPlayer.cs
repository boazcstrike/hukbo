using Microsoft.Xna.Framework.Audio;

namespace Hukbo.Client.Audio;

/// <summary>
/// The only file in the client that touches MonoGame audio. It is constructed
/// from <c>LoadContent</c> and disposed from <c>UnloadContent</c>, so no test and
/// no headless run ever opens an audio device.
/// </summary>
internal sealed class MonoGameSoundPlayer : ISoundPlayer, IDisposable
{
    private readonly Dictionary<GameSoundId, SoundEffect> _effects;
    private readonly SoundBinding[] _bindings;
    private bool _isDisposed;

    private MonoGameSoundPlayer(
        string directoryPath,
        SoundBinding[] bindings,
        Dictionary<GameSoundId, SoundEffect> effects)
    {
        DirectoryPath = directoryPath;
        _bindings = bindings;
        _effects = effects;
    }

    public string DirectoryPath { get; }

    public IReadOnlyList<SoundBinding> Bindings => _bindings;

    /// <summary>
    /// Discovers and loads every file the owner has supplied. A missing folder,
    /// a missing file, an unreadable file, an unsupported format, and a machine
    /// with no audio hardware all resolve to a silent slot rather than an
    /// exception — the game must start either way.
    /// </summary>
    public static MonoGameSoundPlayer Load(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var resolved = SoundLibrary.Resolve(
            directoryPath,
            SoundLibrary.ListFileNames(directoryPath));
        var bindings = new SoundBinding[resolved.Count];
        var effects = new Dictionary<GameSoundId, SoundEffect>(resolved.Count);

        for (var index = 0; index < resolved.Count; index++)
        {
            var binding = resolved[index];
            if (binding.Status != SoundBindingStatus.Ready ||
                binding.FilePath is not { } filePath)
            {
                bindings[index] = binding;
                continue;
            }

            if (TryLoadEffect(filePath, out var effect))
            {
                effects[binding.Sound] = effect;
                bindings[index] = binding;
                continue;
            }

            bindings[index] = binding with
            {
                Status = SoundBindingStatus.LoadFailed,
            };
        }

        return new MonoGameSoundPlayer(directoryPath, bindings, effects);
    }

    public SoundBindingStatus GetStatus(GameSoundId sound)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Sound == sound)
            {
                return binding.Status;
            }
        }

        return SoundBindingStatus.Missing;
    }

    public void Play(GameSoundId sound, float volume)
    {
        if (_isDisposed || !_effects.TryGetValue(sound, out var effect))
        {
            return;
        }

        try
        {
            effect.Play(Math.Clamp(volume, 0f, 1f), pitch: 0f, pan: 0f);
        }
        catch (Exception exception) when (
            exception is InstancePlayLimitException or
            NoAudioHardwareException or
            InvalidOperationException)
        {
            // The platform refused one voice. Dropping a single cue is the
            // correct outcome; audio must never interrupt a battle.
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var effect in _effects.Values)
        {
            effect.Dispose();
        }

        _effects.Clear();
    }

    private static bool TryLoadEffect(
        string filePath,
        out SoundEffect effect)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            effect = SoundEffect.FromStream(stream);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            InvalidOperationException or
            ArgumentException or
            NoAudioHardwareException)
        {
            effect = null!;
            return false;
        }
    }
}
