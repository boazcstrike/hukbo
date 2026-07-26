using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class BattleEventFeed
{
    private readonly int _capacity;
    private readonly List<BattleEvent> _entries = [];
    private readonly ReadOnlyCollection<BattleEvent> _readOnlyEntries;
    private long? _lastSequence;
    private int _scrollStart;

    public BattleEventFeed(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _readOnlyEntries = _entries.AsReadOnly();
    }

    public IReadOnlyList<BattleEvent> Entries => _readOnlyEntries;

    public bool IsPinnedToBottom { get; private set; } = true;

    public void Ingest(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var battleEvent in events)
        {
            if (_lastSequence.HasValue &&
                battleEvent.Sequence <= _lastSequence.Value)
            {
                continue;
            }

            _entries.Add(battleEvent);
            _lastSequence = battleEvent.Sequence;
        }

        var evictionCount = _entries.Count - _capacity;
        if (evictionCount <= 0)
        {
            return;
        }

        _entries.RemoveRange(0, evictionCount);
        if (!IsPinnedToBottom)
        {
            _scrollStart = Math.Max(0, _scrollStart - evictionCount);
        }
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

    public ReadOnlySpan<BattleEvent> GetVisibleEntries(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        var start = GetScrollStart(visibleRowCount);
        var count = Math.Min(visibleRowCount, _entries.Count - start);
        return CollectionsMarshal.AsSpan(_entries).Slice(start, count);
    }

    public void Clear()
    {
        _entries.Clear();
        _lastSequence = null;
        _scrollStart = 0;
        IsPinnedToBottom = true;
    }

    private int GetMaximumStart(int visibleRowCount) =>
        Math.Max(0, _entries.Count - visibleRowCount);
}
