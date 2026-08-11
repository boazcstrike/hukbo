using Hukbo.Diagnostics;
using Microsoft.Xna.Framework.Audio;

namespace Sandata.Client.Audio;

/// <summary>
/// Why a requested cue did or did not reach the audio device. Mirrors the
/// shape of <c>Hukbo.Client.Audio.SoundCueStatus</c> for the melee side.
/// </summary>
internal enum SandataSoundCueStatus
{
    /// <summary>This (slot, variant) pair has never been requested.</summary>
    Unknown = 0,

    /// <summary>The file was found, decoded, and handed to the audio device.</summary>
    Played = 1,

    /// <summary>No file exists at the resolved path.</summary>
    Missing = 2,

    /// <summary>A file exists but could not be decoded as PCM WAV data.</summary>
    LoadFailed = 3,

    /// <summary>
    /// The audio backend declined the cue — MonoGame's managed instance pool
    /// or the OpenAL source list beneath it was exhausted. The caller already
    /// reserved a <see cref="SandataSoundBudget"/> slot before calling
    /// <see cref="ISandataSoundOutput.Play"/>, so this means the platform's
    /// own ceiling is lower than that budget's, not that the budget was
    /// wrong.
    /// </summary>
    Refused = 4,
}

/// <summary>
/// The MonoGame-backed <see cref="ISandataSoundOutput"/> Sandata has lacked
/// since task 39 wired <see cref="SandataSoundPlayer"/> against a placeholder
/// (<c>SandataGame.cs</c>'s <c>NullSandataSoundOutput</c>). Reads raw WAV
/// files at run time under <see cref="SandataSoundLibrary.GetDefaultDirectoryPath"/>,
/// never through the MonoGame content pipeline — the same answer
/// <c>Hukbo.Client.Audio.MonoGameSoundPlayer</c> gives for the melee side.
/// </summary>
/// <remarks>
/// <para>
/// Every decoded <see cref="SoundEffect"/> is cached after its first load, in
/// a <see cref="BoundedCache{TKey,TValue}"/> capped at
/// <see cref="MaxCachedEffects"/> — see that constant's own remarks for the
/// cap and why it is bounded rather than growing forever. A cache hit costs a
/// linear scan of the cache's current entries; a miss costs one disk read and
/// one WAV decode.
/// </para>
/// <para>
/// A missing file and an unparseable file are both negative-cached in
/// <see cref="_statuses"/>, so a slot with no take never re-touches the
/// filesystem on a later shot. <see cref="_statuses"/> is a flat list, not a
/// map keyed structure, matching this folder's own convention — see this
/// type's file for why — and carries no capacity of its own: its key space is
/// exactly the set of file paths <see cref="SandataSoundLibrary.GetFilePath"/>
/// can produce, which is bounded by <see cref="SandataSoundCatalog"/>'s own
/// fixed, static row and variant-count declarations, not by anything a
/// running session can grow without limit — the distinction CLAUDE.md's ban
/// on unbounded caches is drawn against.
/// </para>
/// </remarks>
internal sealed class MonoGameSandataSoundOutput : ISandataSoundOutput, IDisposable
{
    /// <summary>
    /// The largest number of distinct decoded <see cref="SoundEffect"/>
    /// buffers held at once. Each is uncompressed PCM audio, so this is the
    /// real memory concern this cache exists to bound: <see cref="SandataSoundCatalog"/>
    /// declares on the order of a hundred rows, each with up to
    /// <see cref="SandataSoundCatalog.MaxVariantCount"/> numbered takes, so
    /// the full space of distinct files a long session could touch is far
    /// larger than any one moment's engagement needs. 256 matches
    /// <see cref="SandataSoundBudget.DefaultMaximumInstances"/>, the measured
    /// ceiling on concurrently playing instances recorded by task 53: this
    /// cache can hold a distinct decoded buffer for every instance the pool
    /// could simultaneously be playing, with headroom for the slots reused
    /// across successive shots. Past that, the least-recently-used buffer is
    /// evicted and disposed; its next use is a slower cue from a fresh disk
    /// read, never a failure.
    /// </summary>
    private const int MaxCachedEffects = 256;

    private readonly string _directoryPath;
    private readonly DiagnosticLog _log;
    private readonly BoundedCache<string, SoundEffect> _effects;
    private readonly List<(string FilePath, SandataSoundCueStatus Status)> _statuses = [];
    private bool _isDisposed;

    public MonoGameSandataSoundOutput(string directoryPath, DiagnosticLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        _directoryPath = directoryPath;
        _log = log ?? DiagnosticLog.Disabled;
        _effects = new BoundedCache<string, SoundEffect>(
            MaxCachedEffects,
            onEvicted: effect => effect.Dispose());
    }

    /// <summary>
    /// The last recorded outcome for this exact (slot, variant) pair, or
    /// <see cref="SandataSoundCueStatus.Unknown"/> when it has never been
    /// requested. Lets a caller distinguish "played" from "file absent" from
    /// "file present but unparseable" without inspecting an exception.
    /// </summary>
    public SandataSoundCueStatus GetStatus(SoundSlot slot, int variantNumber)
    {
        var filePath = SandataSoundLibrary.GetFilePath(_directoryPath, slot, variantNumber);
        return TryGetStatus(filePath, out var status) ? status : SandataSoundCueStatus.Unknown;
    }

    /// <inheritdoc />
    public bool Play(SoundSlot slot, int variantNumber, ulong shooterEntityId)
    {
        if (_isDisposed)
        {
            return false;
        }

        var filePath = SandataSoundLibrary.GetFilePath(_directoryPath, slot, variantNumber);
        var effect = GetOrLoad(filePath, slot, variantNumber);
        if (effect is null)
        {
            // GetOrLoad already recorded and logged Missing or LoadFailed.
            return false;
        }

        try
        {
            // Hukbo.Client.Audio.MonoGameSoundPlayer.Play's own reasoning
            // applies unchanged: both refusal paths are reported rather than
            // swallowed. Play returns false when the managed instance pool
            // is exhausted; the OpenAL layer beneath it throws when its own
            // source list is.
            var played = effect.Play(volume: 1f, pitch: 0f, pan: 0f);
            var status = played ? SandataSoundCueStatus.Played : SandataSoundCueStatus.Refused;
            SetStatus(filePath, status);
            LogCue(slot, variantNumber, shooterEntityId, status);
            return played;
        }
        catch (Exception exception) when (
            exception is InstancePlayLimitException or
            NoAudioHardwareException or
            InvalidOperationException)
        {
            SetStatus(filePath, SandataSoundCueStatus.Refused);
            LogCue(slot, variantNumber, shooterEntityId, SandataSoundCueStatus.Refused);
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
        foreach (var effect in _effects.Values)
        {
            effect.Dispose();
        }

        _effects.Clear();
    }

    private SoundEffect? GetOrLoad(string filePath, SoundSlot slot, int variantNumber)
    {
        if (_effects.TryGet(filePath, out var cached))
        {
            return cached;
        }

        if (TryGetStatus(filePath, out var knownStatus) &&
            knownStatus is SandataSoundCueStatus.Missing or SandataSoundCueStatus.LoadFailed)
        {
            // Already known bad. Do not re-touch the filesystem every shot
            // for a slot that has no take.
            return null;
        }

        if (!SandataSoundLibrary.FileExists(filePath))
        {
            SetStatus(filePath, SandataSoundCueStatus.Missing);
            LogCueMiss(slot, variantNumber, filePath, SandataSoundCueStatus.Missing);
            return null;
        }

        if (!TryLoadEffect(filePath, out var effect))
        {
            SetStatus(filePath, SandataSoundCueStatus.LoadFailed);
            LogCueMiss(slot, variantNumber, filePath, SandataSoundCueStatus.LoadFailed);
            return null;
        }

        _effects.Set(filePath, effect);
        return effect;
    }

    private bool TryGetStatus(string filePath, out SandataSoundCueStatus status)
    {
        foreach (var entry in _statuses)
        {
            if (string.Equals(entry.FilePath, filePath, StringComparison.Ordinal))
            {
                status = entry.Status;
                return true;
            }
        }

        status = SandataSoundCueStatus.Unknown;
        return false;
    }

    private void SetStatus(string filePath, SandataSoundCueStatus status)
    {
        for (var index = 0; index < _statuses.Count; index++)
        {
            if (string.Equals(_statuses[index].FilePath, filePath, StringComparison.Ordinal))
            {
                _statuses[index] = (filePath, status);
                return;
            }
        }

        _statuses.Add((filePath, status));
    }

    private static bool TryLoadEffect(string filePath, out SoundEffect effect)
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

    private void LogCue(
        SoundSlot slot,
        int variantNumber,
        ulong shooterEntityId,
        SandataSoundCueStatus status)
    {
        // Per-shot: this fires far more than once a second under sustained
        // fire from many shooters, so it belongs at trc, and the level check
        // below must run before any payload value is touched.
        if (!_log.IsEnabledFor(LogLevel.Trace, LogChannel.Audio))
        {
            return;
        }

        _log.Write(
            LogLevel.Trace,
            LogChannel.Audio,
            LogEvents.AudioSandataCue,
            "family",
            slot.Family.ToString(),
            "familyKey",
            slot.FamilyKey,
            "mode",
            slot.Mode.ToString(),
            "environment",
            slot.Environment.ToString(),
            "variant",
            variantNumber,
            "shooter",
            shooterEntityId,
            "status",
            status.ToString());
    }

    private void LogCueMiss(
        SoundSlot slot,
        int variantNumber,
        string filePath,
        SandataSoundCueStatus status)
    {
        // Not per-shot: GetOrLoad's negative cache means this fires once per
        // distinct missing or unparseable file, never once per shot that
        // asks for it, so warn is the right level rather than trc.
        if (!_log.IsEnabledFor(LogLevel.Warning, LogChannel.Audio))
        {
            return;
        }

        _log.Write(
            LogLevel.Warning,
            LogChannel.Audio,
            status == SandataSoundCueStatus.Missing
                ? LogEvents.AudioSandataCueMissing
                : LogEvents.AudioSandataCueLoadFailed,
            "family",
            slot.Family.ToString(),
            "familyKey",
            slot.FamilyKey,
            "mode",
            slot.Mode.ToString(),
            "environment",
            slot.Environment.ToString(),
            "variant",
            variantNumber,
            "file",
            filePath);
    }
}

/// <summary>
/// A fixed-capacity least-recently-used cache. Kept generic and separate from
/// any MonoGame type so its eviction and hit/miss behavior is directly
/// testable with plain values — no <see cref="SoundEffect"/>, no
/// <c>GraphicsDevice</c>, no audio hardware required to exercise it.
/// </summary>
/// <remarks>
/// A flat <see cref="List{T}"/> of entries ordered most-recently-used first,
/// scanned linearly on every lookup — the same shape
/// <c>SandataSoundBudget</c>'s reservation pool already uses for this folder,
/// and this folder's own hygiene test forbids a keyed map structure here. The
/// capacity <see cref="MonoGameSandataSoundOutput"/> actually builds this
/// with (256) keeps a linear scan cheap; a much larger capacity would want a
/// different structure. <see cref="Set"/> evicts the least-recently-used
/// entry, invoking <paramref name="onEvicted"/> (never on <see cref="Clear"/>,
/// whose caller is expected to already own disposal) so a cache of disposable
/// values never leaks the ones it drops.
/// </remarks>
internal sealed class BoundedCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Action<TValue>? _onEvicted;
    private readonly List<CacheEntry> _entries;

    public BoundedCache(int capacity, Action<TValue>? onEvicted = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _onEvicted = onEvicted;
        _entries = new List<CacheEntry>(capacity);
    }

    /// <summary>The number of entries currently cached.</summary>
    public int Count => _entries.Count;

    /// <summary>Every currently cached value, most-recently-used first.</summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var entry in _entries)
            {
                yield return entry.Value;
            }
        }
    }

    /// <summary>
    /// Looks up <paramref name="key"/>, marking it most recently used on a
    /// hit.
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        var index = FindIndex(key);
        if (index < 0)
        {
            value = default!;
            return false;
        }

        var entry = _entries[index];
        _entries.RemoveAt(index);
        _entries.Insert(0, entry);
        value = entry.Value;
        return true;
    }

    /// <summary>
    /// Inserts or updates <paramref name="key"/>. When the cache is already
    /// at capacity and <paramref name="key"/> is new, evicts the
    /// least-recently-used entry first, invoking the constructor's
    /// <c>onEvicted</c> callback, if any, with the evicted value.
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        var index = FindIndex(key);
        if (index >= 0)
        {
            _entries.RemoveAt(index);
            _entries.Insert(0, new CacheEntry(key, value));
            return;
        }

        if (_entries.Count >= _capacity)
        {
            var oldestIndex = _entries.Count - 1;
            var oldest = _entries[oldestIndex];
            _entries.RemoveAt(oldestIndex);
            _onEvicted?.Invoke(oldest.Value);
        }

        _entries.Insert(0, new CacheEntry(key, value));
    }

    /// <summary>
    /// Drops every entry without invoking the constructor's <c>onEvicted</c>
    /// callback — for a caller, such as
    /// <see cref="MonoGameSandataSoundOutput.Dispose"/>, that has already
    /// disposed every value itself.
    /// </summary>
    public void Clear() => _entries.Clear();

    private int FindIndex(TKey key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key))
            {
                return index;
            }
        }

        return -1;
    }

    private readonly record struct CacheEntry(TKey Key, TValue Value);
}
