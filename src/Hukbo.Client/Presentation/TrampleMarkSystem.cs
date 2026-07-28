using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One trample mark left at a fallen agent's world position (battlefield-
/// environment-design.md, "Trampled and sparse areas"). Fixed-point
/// <see cref="XRaw"/>/<see cref="YRaw"/>, matching <see cref="AgentView"/>
/// and every other presentation mark type in <c>BloodEffect.cs</c>
/// (<see cref="BloodBurst"/>, <see cref="GroundMark"/>) — converted to a
/// world-unit <c>Vector2</c> only at draw or suppression-test time.
/// </summary>
internal readonly record struct TrampleMark(int XRaw, int YRaw);

/// <summary>
/// Fixed-capacity, oldest-replaced list of trample marks fed by
/// authoritative <c>Death</c> events, matching <see cref="HitEffectSystem"/>'s
/// fixed-pool lifecycle shape (battlefield-environment-design.md, "Trampled
/// and sparse areas", R-W4.7). Unlike <see cref="HitEffectSystem"/>'s timed
/// effects, a trample mark never ages out on its own — it persists for the
/// rest of the battle as visible wear, so once the pool is full the next
/// mark always overwrites the single oldest entry by insertion order rather
/// than by any age comparison. Client-only: never persisted, never
/// snapshotted, never read by the simulation, and cleared only when the
/// scenario resets.
/// </summary>
internal sealed class TrampleMarkSystem
{
    /// <summary>
    /// Hard cap on live trample marks (battlefield-environment-design.md,
    /// "Trampled and sparse areas").
    /// </summary>
    public const int Capacity = 128;

    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly TrampleMark[] _marks;
    private int _count;
    private int _oldestIndex;

    public TrampleMarkSystem(int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _marks = new TrampleMark[capacity];
    }

    public ReadOnlySpan<TrampleMark> ActiveMarks => _marks.AsSpan(0, _count);

    /// <summary>
    /// Appends one mark for every <c>Death</c> event in
    /// <paramref name="events"/>, at the dying agent's end-of-tick position.
    /// <c>Move</c> never spawns anything — the design's default-declined
    /// <c>Attack</c>-feed option (OD-W4-a) means every event kind other than
    /// <c>Death</c> is ignored too.
    /// </summary>
    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind != BattleEventKind.Death ||
                !_agentsById.TryGetValue(battleEvent.SourceEntityId, out var agent))
            {
                continue;
            }

            Add(new TrampleMark(agent.XRaw, agent.YRaw));
        }
    }

    public void Clear()
    {
        Array.Clear(_marks, 0, _count);
        _count = 0;
        _oldestIndex = 0;
        _agentsById.Clear();
    }

    /// <summary>
    /// Appends while the pool has room; once full, always overwrites the
    /// single oldest slot and advances the ring cursor. A plain ring rather
    /// than <see cref="HitEffectSystem"/>'s age-comparison scan, because a
    /// trample mark carries no age to compare — insertion order alone
    /// decides which one goes.
    /// </summary>
    private void Add(TrampleMark mark)
    {
        if (_count < _marks.Length)
        {
            _marks[_count] = mark;
            _count++;
            return;
        }

        _marks[_oldestIndex] = mark;
        _oldestIndex = (_oldestIndex + 1) % _marks.Length;
    }
}
