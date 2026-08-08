using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Converts authoritative attack events into a fixed-capacity, atomically
/// released contact stream without changing simulation state or event order.
/// </summary>
internal sealed class AttackContactDispatcher
{
    // The Client can catch up at most 0.5 seconds at 20 simulation ticks per
    // second. The fastest V4 combination strikes every two ticks, so one
    // attacker can produce at most five contacts during that catch-up window.
    public const int MaximumPendingContactsPerAttacker = 5;

    private readonly AttackContactBundle[] _pending;
    private readonly DiagnosticLog _diagnostics;
    private readonly long[] _insertionOrders;
    private AttackContactBundle _latched;
    private int _pendingCount;
    private long _nextInsertionOrder;
    private bool _hasLatched;

    public AttackContactDispatcher(
        int attackerCapacity,
        DiagnosticLog? diagnostics = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attackerCapacity);

        _diagnostics = diagnostics ?? DiagnosticLog.Disabled;
        var capacity = checked(
            attackerCapacity * MaximumPendingContactsPerAttacker);
        _pending = new AttackContactBundle[capacity];
        _insertionOrders = new long[capacity];
    }

    public int PendingCount => _pendingCount;

    public int CollapsedContactCount { get; private set; }

    /// <summary>
    /// Builds contacts only from Attack events. Damage events are aggregate
    /// semantic records; Death marks exactly one already-built landed contact.
    /// </summary>
    public void Ingest(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            switch (battleEvent.Kind)
            {
                case BattleEventKind.Attack:
                    if (TryCreateBundle(battleEvent, out var contact))
                    {
                        Add(contact);
                    }

                    break;
                case BattleEventKind.Death:
                    MarkLethal(
                        battleEvent.Tick,
                        battleEvent.SourceEntityId);
                    break;
            }
        }
    }

    /// <summary>
    /// Latches the oldest tick, breaking ties by ingestion order. A contact
    /// remains latched until every presentation channel acknowledges it.
    /// </summary>
    public bool TryLatchNext(out AttackContactBundle contact)
    {
        if (_hasLatched || _pendingCount == 0)
        {
            contact = default;
            return false;
        }

        var nextIndex = 0;
        for (var index = 1; index < _pendingCount; index++)
        {
            if (_pending[index].Tick < _pending[nextIndex].Tick ||
                (_pending[index].Tick == _pending[nextIndex].Tick &&
                 _insertionOrders[index] < _insertionOrders[nextIndex]))
            {
                nextIndex = index;
            }
        }

        _latched = _pending[nextIndex];
        _hasLatched = true;
        RemovePendingAt(nextIndex);
        contact = _latched;
        return true;
    }

    public bool TryGetLatched(out AttackContactBundle contact)
    {
        contact = _latched;
        return _hasLatched;
    }

    public void AcknowledgeLatched()
    {
        _latched = default;
        _hasLatched = false;
    }

    public void Clear()
    {
        Array.Clear(_pending, 0, _pendingCount);
        Array.Clear(_insertionOrders, 0, _pendingCount);
        _pendingCount = 0;
        _nextInsertionOrder = 0;
        _latched = default;
        _hasLatched = false;
        CollapsedContactCount = 0;
    }

    private static bool TryCreateBundle(
        BattleEvent battleEvent,
        out AttackContactBundle contact)
    {
        if (battleEvent.TargetEntityId is not { } defenderEntityId ||
            battleEvent.FactionId is not { } factionId ||
            battleEvent.Weapon is not { } weapon ||
            battleEvent.Shield is not { } shield ||
            battleEvent.HitLocation is not { } hitLocation ||
            battleEvent.Resolution is not { } resolution)
        {
            contact = default;
            return false;
        }

        contact = new AttackContactBundle(
            battleEvent.Sequence,
            battleEvent.Tick,
            battleEvent.SourceEntityId,
            defenderEntityId,
            battleEvent.Value,
            factionId,
            weapon,
            shield,
            hitLocation,
            resolution,
            battleEvent.ComboPosition,
            IsLethal: false);
        return true;
    }

    private void Add(AttackContactBundle contact)
    {
        var attackerContactCount = 0;
        var newestAttackerIndex = -1;
        for (var index = 0; index < _pendingCount; index++)
        {
            if (_pending[index].AttackerEntityId != contact.AttackerEntityId)
            {
                continue;
            }

            attackerContactCount++;
            if (newestAttackerIndex < 0 ||
                _insertionOrders[index] >
                _insertionOrders[newestAttackerIndex])
            {
                newestAttackerIndex = index;
            }
        }

        if (attackerContactCount >= MaximumPendingContactsPerAttacker)
        {
            ReplacePending(newestAttackerIndex, contact);
            return;
        }

        if (_pendingCount < _pending.Length)
        {
            _pending[_pendingCount] = contact;
            _insertionOrders[_pendingCount] = _nextInsertionOrder++;
            _pendingCount++;
            return;
        }

        // The constructor is sized to the scenario's attacker capacity, so
        // this is only defense against an inconsistent caller. Keep the
        // representation bounded and replace one whole newest bundle.
        var newestIndex = 0;
        for (var index = 1; index < _pendingCount; index++)
        {
            if (_insertionOrders[index] > _insertionOrders[newestIndex])
            {
                newestIndex = index;
            }
        }

        ReplacePending(newestIndex, contact);
    }

    private void ReplacePending(
        int index,
        AttackContactBundle contact)
    {
        _pending[index] = contact;
        _insertionOrders[index] = _nextInsertionOrder++;
        CollapsedContactCount++;
        _diagnostics.Write(
            LogLevel.Warning,
            LogChannel.Render,
            LogEvents.RenderAttackContactCollapsed,
            "attackerId",
            contact.AttackerEntityId,
            "collapsedCount",
            CollapsedContactCount,
            "sequence",
            contact.Sequence,
            "tick",
            contact.Tick);
    }

    private void MarkLethal(long tick, ulong defenderEntityId)
    {
        var lethalIndex = -1;
        for (var index = 0; index < _pendingCount; index++)
        {
            var contact = _pending[index];
            if (contact.Tick == tick &&
                contact.DefenderEntityId == defenderEntityId &&
                contact.Resolution == AttackResolution.Landed &&
                (lethalIndex < 0 ||
                 contact.Sequence > _pending[lethalIndex].Sequence))
            {
                lethalIndex = index;
            }
        }

        if (lethalIndex >= 0)
        {
            _pending[lethalIndex] = _pending[lethalIndex] with
            {
                IsLethal = true,
            };
        }
    }

    private void RemovePendingAt(int index)
    {
        var moveCount = _pendingCount - index - 1;
        if (moveCount > 0)
        {
            Array.Copy(_pending, index + 1, _pending, index, moveCount);
            Array.Copy(
                _insertionOrders,
                index + 1,
                _insertionOrders,
                index,
                moveCount);
        }

        _pendingCount--;
        _pending[_pendingCount] = default;
        _insertionOrders[_pendingCount] = 0;
    }
}
