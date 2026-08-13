using Microsoft.Xna.Framework.Audio;

namespace Hukbo.Client.Audio;

/// <summary>
/// The only file in the client that touches MonoGame audio. It is constructed
/// from <c>LoadContent</c> and disposed from <c>UnloadContent</c>, so no test and
/// no headless run ever opens an audio device.
/// </summary>
internal sealed class MonoGameSoundPlayer : ISoundPlayer, IDisposable
{
    /// <summary>
    /// The common peak every sample-domain-normalised take is scaled toward.
    /// See
    /// <c>docs/plans/2026-08-13-shield-clash-legibility-design.md</c>
    /// section 4 for why <c>0.85</c> keeps the loudest possible cue under the
    /// full-scale level today's flat <c>CueVolume</c> could already reach.
    /// </summary>
    internal const float ReferencePeak = 0.85f;

    /// <summary>
    /// Bounds on the per-take normalisation scale factor. A near-silent take
    /// would otherwise be amplified into noise; a take that is already at or
    /// above the reference peak is only ever attenuated a little.
    /// </summary>
    internal const float MinimumMultiplier = 0.5f;

    internal const float MaximumMultiplier = 6.0f;

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

            var normalize = SoundCatalog.IsMeleeShieldClash(variantList.Sound);
            var loaded = LoadEffects(directoryPath, variantList.FileNames, normalize);
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

    /// <summary>
    /// Plays a loaded variant at the caller's volume and pitch.
    /// <see cref="SoundDirector"/> already folds the slot's
    /// <see cref="SoundVoicing"/> level into <paramref name="volume"/> and its
    /// pitch offset into <paramref name="pitch"/> before calling here — see the
    /// remark on <see cref="SoundDirector.Resolve"/>. Per-take loudness
    /// normalisation is no longer applied here: it is baked into the sample
    /// data of the loaded <see cref="SoundEffect"/> itself at load time by
    /// <see cref="TryBuildNormalizedEffect"/>, because a
    /// <see cref="SoundEffectInstance"/> volume multiplier clamped to
    /// <c>[0, 1]</c> can only ever attenuate and could never lift a quiet take
    /// — see
    /// <c>docs/plans/2026-08-13-shield-clash-legibility-design.md</c>
    /// section 3.
    /// </summary>
    public bool Play(GameSoundId sound, HitClass? hitClass, int variantIndex, float volume, float pitch)
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
                pitch: Math.Clamp(pitch, -1f, 1f),
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
        IReadOnlyList<string> fileNames,
        bool normalize)
    {
        var loaded = new List<SoundEffect>(fileNames.Count);
        foreach (var fileName in fileNames)
        {
            var filePath = Path.Combine(directoryPath, fileName);
            if (TryLoadEffect(filePath, normalize, out var effect))
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

    /// <summary>
    /// Reads a variant's bytes exactly once and builds the playable
    /// <see cref="SoundEffect"/> from that single buffer. When
    /// <paramref name="normalize"/> is set — the four melee shield-clash
    /// slots only, see <see cref="SoundCatalog.IsMeleeShieldClash"/> — the
    /// effect is built from a sample-domain-scaled copy of the PCM data via
    /// <see cref="TryBuildNormalizedEffect"/>. Every other slot, and any
    /// clash take <see cref="TryBuildNormalizedEffect"/> declines, loads
    /// unmodified through <see cref="SoundEffect.FromStream"/> instead.
    /// </summary>
    private static bool TryLoadEffect(
        string filePath,
        bool normalize,
        out SoundEffect effect)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (normalize && TryBuildNormalizedEffect(bytes, out var normalizedEffect))
            {
                effect = normalizedEffect;
                return true;
            }

            using var stream = new MemoryStream(bytes, writable: false);
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

    /// <summary>
    /// Builds a clip from a sample-domain-scaled copy of
    /// <paramref name="wavBytes"/>, so the loaded <see cref="SoundEffect"/>
    /// itself peaks at <see cref="ReferencePeak"/> rather than relying on a
    /// play-time volume multiplier — <see cref="SoundEffectInstance"/> volume
    /// clamps to <c>[0, 1]</c>, so a multiplier could only ever attenuate and
    /// could never lift the quietest measured take, which is the defect this
    /// method exists to fix. See
    /// <c>docs/plans/2026-08-13-shield-clash-legibility-design.md</c>
    /// section 3. Returns <see langword="false"/> — never throws — for a take
    /// <see cref="WavePeak.TryReadPcm"/> cannot parse or that measures as pure
    /// silence, so <see cref="TryLoadEffect"/> falls back to the unmodified
    /// <see cref="SoundEffect.FromStream"/> path for either case.
    /// </summary>
    private static bool TryBuildNormalizedEffect(byte[] wavBytes, out SoundEffect effect)
    {
        effect = null!;

        if (!WavePeak.TryReadPcm(
                wavBytes,
                out var sampleRate,
                out var channelCount,
                out var pcmData,
                out var peak) ||
            peak <= 0f)
        {
            return false;
        }

        var scale = WavePeak.ComputeScaleFactor(
            peak,
            ReferencePeak,
            MinimumMultiplier,
            MaximumMultiplier);
        var scaledPcm = WavePeak.ApplyGain(pcmData, scale);
        effect = new SoundEffect(scaledPcm, sampleRate, (AudioChannels)channelCount);
        return true;
    }
}
