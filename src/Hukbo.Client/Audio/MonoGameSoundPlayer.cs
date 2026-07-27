using Microsoft.Xna.Framework.Audio;

namespace Hukbo.Client.Audio;

/// <summary>
/// The only file in the client that touches MonoGame audio. It is constructed
/// from <c>LoadContent</c> and disposed from <c>UnloadContent</c>, so no test and
/// no headless run ever opens an audio device.
/// </summary>
internal sealed class MonoGameSoundPlayer : ISoundPlayer, IDisposable
{
    private readonly Dictionary<(GameSoundId Sound, HitClass? HitClass), SoundEffect[]> _effects;
    private readonly Dictionary<(GameSoundId Sound, HitClass? HitClass), SoundBindingStatus> _variantStatuses;
    private readonly SoundBinding[] _bindings;
    private bool _isDisposed;

    private MonoGameSoundPlayer(
        string directoryPath,
        SoundBinding[] bindings,
        Dictionary<(GameSoundId, HitClass?), SoundEffect[]> effects,
        Dictionary<(GameSoundId, HitClass?), SoundBindingStatus> variantStatuses)
    {
        DirectoryPath = directoryPath;
        _bindings = bindings;
        _effects = effects;
        _variantStatuses = variantStatuses;
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

        var fileNames = SoundLibrary.ListFileNames(directoryPath);
        var bindings = SoundLibrary.Resolve(directoryPath, fileNames);
        var variantLists = SoundLibrary.ResolveVariants(directoryPath, fileNames);

        var effects = new Dictionary<(GameSoundId, HitClass?), SoundEffect[]>();
        var variantStatuses = new Dictionary<(GameSoundId, HitClass?), SoundBindingStatus>();

        foreach (var variantList in variantLists)
        {
            var key = (variantList.Sound, variantList.HitClass);
            if (variantList.Status != SoundBindingStatus.Ready)
            {
                variantStatuses[key] = variantList.Status;
                continue;
            }

            var loaded = LoadEffects(directoryPath, variantList.FileNames);
            if (loaded.Count == 0)
            {
                variantStatuses[key] = SoundBindingStatus.LoadFailed;
                continue;
            }

            effects[key] = [.. loaded];
            variantStatuses[key] = SoundBindingStatus.Ready;
        }

        var adjustedBindings = DowngradeBindingsWithNoLoadedVariant(bindings, variantStatuses);
        return new MonoGameSoundPlayer(
            directoryPath,
            adjustedBindings,
            effects,
            variantStatuses);
    }

    public SoundBindingStatus GetStatus(GameSoundId sound, HitClass? hitClass) =>
        _variantStatuses.TryGetValue((sound, hitClass), out var status)
            ? status
            : SoundBindingStatus.Missing;

    public int GetVariantCount(GameSoundId sound, HitClass? hitClass) =>
        _effects.TryGetValue((sound, hitClass), out var loaded) ? loaded.Length : 0;

    public double GetDurationSeconds(
        GameSoundId sound,
        HitClass? hitClass,
        int variantIndex) =>
        !_isDisposed &&
        _effects.TryGetValue((sound, hitClass), out var loaded) &&
        variantIndex >= 0 &&
        variantIndex < loaded.Length
            ? loaded[variantIndex].Duration.TotalSeconds
            : 0;

    public bool Play(GameSoundId sound, HitClass? hitClass, int variantIndex, float volume)
    {
        if (_isDisposed ||
            !_effects.TryGetValue((sound, hitClass), out var loaded) ||
            variantIndex < 0 ||
            variantIndex >= loaded.Length)
        {
            return false;
        }

        try
        {
            // Both refusal paths are reported. Play returns false when
            // MonoGame's managed instance pool is exhausted; the OpenAL layer
            // beneath it throws when its source list is. Silently swallowing
            // either one is what made an earlier audio investigation slow.
            return loaded[variantIndex].Play(
                Math.Clamp(volume, 0f, 1f),
                pitch: 0f,
                pan: 0f);
        }
        catch (Exception exception) when (
            exception is InstancePlayLimitException or
            NoAudioHardwareException or
            InvalidOperationException)
        {
            // The platform refused one voice. Dropping a single cue is the
            // correct outcome; audio must never interrupt a battle.
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var loaded in _effects.Values)
        {
            foreach (var effect in loaded)
            {
                effect.Dispose();
            }
        }

        _effects.Clear();
    }

    private static List<SoundEffect> LoadEffects(
        string directoryPath,
        IReadOnlyList<string> fileNames)
    {
        var loaded = new List<SoundEffect>(fileNames.Count);
        foreach (var fileName in fileNames)
        {
            var filePath = Path.Combine(directoryPath, fileName);
            if (TryLoadEffect(filePath, out var effect))
            {
                loaded.Add(effect);
            }
        }

        return loaded;
    }

    /// <summary>
    /// A slot reported <see cref="SoundBindingStatus.Ready"/> by
    /// <see cref="SoundLibrary.Resolve"/> — because at least one raw file
    /// existed for it — can still end up with nothing actually loaded, if
    /// every one of those files failed to parse as WAV. This downgrades that
    /// slot's aggregate binding to <see cref="SoundBindingStatus.LoadFailed"/>
    /// so the panel reports the real cause instead of a silent success.
    /// </summary>
    private static SoundBinding[] DowngradeBindingsWithNoLoadedVariant(
        IReadOnlyList<SoundBinding> bindings,
        IReadOnlyDictionary<(GameSoundId, HitClass?), SoundBindingStatus> variantStatuses)
    {
        var adjusted = new SoundBinding[bindings.Count];
        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];
            adjusted[index] = binding.Status == SoundBindingStatus.Ready &&
                !HasAnyReadyVariant(binding.Sound, variantStatuses)
                ? binding with { Status = SoundBindingStatus.LoadFailed }
                : binding;
        }

        return adjusted;
    }

    private static bool HasAnyReadyVariant(
        GameSoundId sound,
        IReadOnlyDictionary<(GameSoundId, HitClass?), SoundBindingStatus> variantStatuses)
    {
        if (!SoundCatalog.IsHitLocationDriven(sound))
        {
            return variantStatuses.TryGetValue((sound, null), out var status) &&
                status == SoundBindingStatus.Ready;
        }

        foreach (var hitClass in HitClassCatalog.All)
        {
            if (variantStatuses.TryGetValue((sound, hitClass), out var status) &&
                status == SoundBindingStatus.Ready)
            {
                return true;
            }
        }

        return false;
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
