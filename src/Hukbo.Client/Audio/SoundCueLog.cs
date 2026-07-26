using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Hukbo.Client.Audio;

/// <summary>
/// The bounded, scrollable record of what the audio system did. Kept separate
/// from the battle event feed so the two views never compete: one is the battle,
/// this one is the sound plumbing.
/// </summary>
internal sealed class SoundCueLog
{
    private readonly int _capacity;
    private readonly List<SoundCue> _entries = [];
    private readonly ReadOnlyCollection<SoundCue> _readOnlyEntries;
    private int _scrollStart;

    public SoundCueLog(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _readOnlyEntries = _entries.AsReadOnly();
    }

    public IReadOnlyList<SoundCue> Entries => _readOnlyEntries;

    public bool IsPinnedToBottom { get; private set; } = true;

    /// <summary>
    /// Records one cue. A cue identical to the newest row — same tick, slot, and
    /// status — increments that row's count instead of appending, so a tick of
    /// forty suppressed attacks costs one row rather than flushing the log.
    /// </summary>
    public void Append(long tick, GameSoundId sound, SoundCueStatus status)
    {
        if (_entries.Count > 0)
        {
            var newest = _entries[^1];
            if (newest.Tick == tick &&
                newest.Sound == sound &&
                newest.Status == status)
            {
                if (newest.Count < int.MaxValue)
                {
                    _entries[^1] = newest with { Count = newest.Count + 1 };
                }

                return;
            }
        }

        _entries.Add(new SoundCue(tick, sound, status, Count: 1));

        var evictionCount = _entries.Count - _capacity;
        if (evictionCount <= 0)
        {
            return;
        }

        _entries.RemoveRange(0, evictionCount);
        _scrollStart = Math.Max(0, _scrollStart - evictionCount);
    }

    public void Scroll(int rowDelta, int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        var maximumStart = GetMaximumStart(visibleRowCount);
        var currentStart = IsPinnedToBottom
            ? maximumStart
            : Math.Min(_scrollStart, maximumStart);
        _scrollStart = (int)Math.Clamp(
            (long)currentStart + rowDelta,
            0,
            maximumStart);
        IsPinnedToBottom = _scrollStart == maximumStart;
    }

    public int GetScrollStart(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        var maximumStart = GetMaximumStart(visibleRowCount);
        return IsPinnedToBottom
            ? maximumStart
            : Math.Min(_scrollStart, maximumStart);
    }

    public ReadOnlySpan<SoundCue> GetVisibleEntries(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        var start = GetScrollStart(visibleRowCount);
        var count = Math.Min(visibleRowCount, _entries.Count - start);
        return CollectionsMarshal.AsSpan(_entries).Slice(start, count);
    }

    public void Clear()
    {
        _entries.Clear();
        _scrollStart = 0;
        IsPinnedToBottom = true;
    }

    private int GetMaximumStart(int visibleRowCount) =>
        Math.Max(0, _entries.Count - visibleRowCount);
}
