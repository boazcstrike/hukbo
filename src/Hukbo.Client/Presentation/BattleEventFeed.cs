using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class BattleEventFeed
{
    private readonly int _capacity;
    private readonly List<BattleEvent> _entries = [];
    private readonly List<BattleEvent> _filteredEntries = [];
    private readonly ReadOnlyCollection<BattleEvent> _readOnlyEntries;
    private readonly ReadOnlyCollection<BattleEvent> _readOnlyFilteredEntries;
    private long? _lastSequence;
    private int _scrollStart;

    public BattleEventFeed(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _readOnlyEntries = _entries.AsReadOnly();
        _readOnlyFilteredEntries = _filteredEntries.AsReadOnly();
    }

    public IReadOnlyList<BattleEvent> Entries => _readOnlyEntries;

    public IReadOnlyList<BattleEvent> FilteredEntries =>
        _readOnlyFilteredEntries;

    public BattleEventKind? KindFilter { get; private set; }

    public int? FactionFilter { get; private set; }

    public ulong? ActorFilter { get; private set; }

    public string TextFilter { get; private set; } = string.Empty;

    public bool HasActiveFilters =>
        KindFilter.HasValue ||
        FactionFilter.HasValue ||
        ActorFilter.HasValue ||
        !string.IsNullOrWhiteSpace(TextFilter);

    public long? SelectedSequence { get; private set; }

    public BattleEvent? SelectedEvent
    {
        get
        {
            if (SelectedSequence is not { } sequence)
            {
                return null;
            }

            foreach (var battleEvent in _filteredEntries)
            {
                if (battleEvent.Sequence == sequence)
                {
                    return battleEvent;
                }
            }

            return null;
        }
    }

    public bool IsPinnedToBottom { get; private set; } = true;

    public int NewEventCount { get; private set; }

    public void Ingest(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var oldScrollStart = _scrollStart;
        var oldAnchorSequence =
            oldScrollStart < _filteredEntries.Count
                ? _filteredEntries[oldScrollStart].Sequence
                : (long?)null;
        var addedMatchingCount = 0;
        foreach (var battleEvent in events)
        {
            if (_lastSequence.HasValue &&
                battleEvent.Sequence <= _lastSequence.Value)
            {
                continue;
            }

            _entries.Add(battleEvent);
            _lastSequence = battleEvent.Sequence;
            if (MatchesFilters(battleEvent))
            {
                addedMatchingCount++;
            }
        }

        var evictionCount = _entries.Count - _capacity;
        var matchingEvictionCount = 0;
        if (evictionCount > 0)
        {
            for (var index = 0; index < evictionCount; index++)
            {
                if (MatchesFilters(_entries[index]))
                {
                    matchingEvictionCount++;
                }
            }

            _entries.RemoveRange(0, evictionCount);
        }

        RebuildFilteredEntries(
            preserveScrollPosition: !IsPinnedToBottom,
            oldScrollStart,
            oldAnchorSequence,
            matchingEvictionCount);
        if (!IsPinnedToBottom && addedMatchingCount > 0)
        {
            NewEventCount += addedMatchingCount;
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
        if (IsPinnedToBottom)
        {
            NewEventCount = 0;
        }
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
        var count = Math.Min(
            visibleRowCount,
            _filteredEntries.Count - start);
        return CollectionsMarshal.AsSpan(_filteredEntries).Slice(start, count);
    }

    public void SetFilters(
        BattleEventKind? kind,
        int? factionId,
        ulong? actorId,
        string? textQuery)
    {
        var normalizedText = string.IsNullOrWhiteSpace(textQuery)
            ? string.Empty
            : textQuery;
        if (KindFilter == kind &&
            FactionFilter == factionId &&
            ActorFilter == actorId &&
            string.Equals(
                TextFilter,
                normalizedText,
                StringComparison.Ordinal))
        {
            return;
        }

        KindFilter = kind;
        FactionFilter = factionId;
        ActorFilter = actorId;
        TextFilter = normalizedText;
        _scrollStart = 0;
        IsPinnedToBottom = true;
        NewEventCount = 0;
        RebuildFilteredEntries(
            preserveScrollPosition: false,
            oldScrollStart: 0,
            oldAnchorSequence: null,
            matchingEvictionCount: 0);
    }

    public void SetKindFilter(BattleEventKind? kind) =>
        SetFilters(kind, FactionFilter, ActorFilter, TextFilter);

    public void SetFactionFilter(int? factionId) =>
        SetFilters(KindFilter, factionId, ActorFilter, TextFilter);

    public void SetActorFilter(ulong? actorId) =>
        SetFilters(KindFilter, FactionFilter, actorId, TextFilter);

    public void SetTextFilter(string? textQuery) =>
        SetFilters(KindFilter, FactionFilter, ActorFilter, textQuery);

    public void ClearFilters() =>
        SetFilters(
            kind: null,
            factionId: null,
            actorId: null,
            textQuery: null);

    public bool Select(long sequence, int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        var index = FindFilteredIndex(sequence);
        if (index < 0)
        {
            return false;
        }

        SelectAtIndex(index, visibleRowCount);
        return true;
    }

    public void MoveSelection(int rowDelta, int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        if (_filteredEntries.Count == 0 || rowDelta == 0)
        {
            return;
        }

        var currentIndex = SelectedSequence is { } sequence
            ? FindFilteredIndex(sequence)
            : -1;
        var targetIndex = currentIndex < 0
            ? (rowDelta > 0 ? 0 : _filteredEntries.Count - 1)
            : (int)Math.Clamp(
                (long)currentIndex + rowDelta,
                0,
                _filteredEntries.Count - 1);
        SelectAtIndex(targetIndex, visibleRowCount);
    }

    public void SelectFirst(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        if (_filteredEntries.Count > 0)
        {
            SelectAtIndex(0, visibleRowCount);
        }
    }

    public void SelectLast(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        if (_filteredEntries.Count > 0)
        {
            SelectAtIndex(_filteredEntries.Count - 1, visibleRowCount);
        }
    }

    public void ReturnToLatest(int visibleRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleRowCount);

        if (_filteredEntries.Count == 0)
        {
            return;
        }

        SelectedSequence = _filteredEntries[^1].Sequence;
        _scrollStart = GetMaximumStart(visibleRowCount);
        IsPinnedToBottom = true;
        NewEventCount = 0;
    }

    public void Clear()
    {
        _entries.Clear();
        _filteredEntries.Clear();
        _lastSequence = null;
        _scrollStart = 0;
        KindFilter = null;
        FactionFilter = null;
        ActorFilter = null;
        TextFilter = string.Empty;
        SelectedSequence = null;
        IsPinnedToBottom = true;
        NewEventCount = 0;
    }

    private int GetMaximumStart(int visibleRowCount) =>
        Math.Max(0, _filteredEntries.Count - visibleRowCount);

    private void SelectAtIndex(int index, int visibleRowCount)
    {
        var effectiveVisibleRowCount = Math.Max(1, visibleRowCount);
        var currentStart = GetScrollStart(effectiveVisibleRowCount);
        var visibleEnd = currentStart + effectiveVisibleRowCount - 1;
        if (index < currentStart)
        {
            _scrollStart = index;
        }
        else if (index > visibleEnd)
        {
            _scrollStart = Math.Max(
                0,
                index - effectiveVisibleRowCount + 1);
        }
        else
        {
            _scrollStart = currentStart;
        }

        SelectedSequence = _filteredEntries[index].Sequence;
        IsPinnedToBottom = false;
        NewEventCount = 0;
    }

    private int FindFilteredIndex(long sequence)
    {
        for (var index = 0; index < _filteredEntries.Count; index++)
        {
            if (_filteredEntries[index].Sequence == sequence)
            {
                return index;
            }
        }

        return -1;
    }

    private void RebuildFilteredEntries(
        bool preserveScrollPosition,
        int oldScrollStart,
        long? oldAnchorSequence,
        int matchingEvictionCount)
    {
        _filteredEntries.Clear();
        foreach (var battleEvent in _entries)
        {
            if (MatchesFilters(battleEvent))
            {
                _filteredEntries.Add(battleEvent);
            }
        }

        if (SelectedSequence is { } selectedSequence &&
            FindFilteredIndex(selectedSequence) < 0)
        {
            SelectedSequence = null;
        }

        if (IsPinnedToBottom || !preserveScrollPosition)
        {
            return;
        }

        if (oldAnchorSequence is { } anchorSequence)
        {
            var anchorIndex = FindFilteredIndex(anchorSequence);
            if (anchorIndex >= 0)
            {
                _scrollStart = anchorIndex;
                return;
            }
        }

        _scrollStart = Math.Max(
            0,
            oldScrollStart - matchingEvictionCount);
    }

    private bool MatchesFilters(BattleEvent battleEvent)
    {
        if (KindFilter is { } kind && battleEvent.Kind != kind)
        {
            return false;
        }

        if (FactionFilter is { } factionId &&
            battleEvent.FactionId != factionId)
        {
            return false;
        }

        if (ActorFilter is { } actorId &&
            battleEvent.SourceEntityId != actorId)
        {
            return false;
        }

        var searchTerm = TextFilter.Trim();
        if (searchTerm.Length == 0)
        {
            return true;
        }

        // BattleEventFormatter.Format throws for an Attack-kind event missing
        // Weapon/HitLocation. Core's BattleEvent factories guarantee both are
        // always set for an Attack event, so TryFormat returning false here
        // is defense-in-depth against that invariant, not a reachable path
        // with today's Core.
        return TryFormat(battleEvent, out var formatted) &&
            formatted.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFormat(BattleEvent battleEvent, out string formatted)
    {
        if (battleEvent.Kind == BattleEventKind.Attack &&
            (battleEvent.Weapon is null || battleEvent.HitLocation is null))
        {
            formatted = string.Empty;
            return false;
        }

        formatted = BattleEventFormatter.Format(battleEvent);
        return true;
    }
}
