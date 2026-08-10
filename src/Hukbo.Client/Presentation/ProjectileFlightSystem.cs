using Hukbo.Client.Rendering;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Fixed-capacity, tick-advanced store of in-flight ranged shots, fed by
/// <see cref="BattleEventKind.Release"/> events. Presentation only: the
/// simulation holds its own authoritative flight state, and this copy exists
/// solely to be drawn. It decides no targeting, no damage, and no outcome.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Ingest"/> is the system's only clock, and the tick the caller
/// passes is the whole clock — never wall time, never a frame delta. That is
/// what lets the store survive pause and playback speed with no scaling,
/// exactly as <see cref="GaitAnimationSystem"/> already does for its own
/// phase. A call at the same tick as the previous call is a no-op: it neither
/// re-adds an event already ingested nor advances anything a second time,
/// which is what keeps a caller that ingests one tick twice from
/// double-counting. A call at a tick earlier than the previous one throws,
/// since that would mean time ran backward without a <see cref="Clear"/> in
/// between.
/// </para>
/// <para>
/// An entry is dropped the tick it arrives — when
/// <c>tick - entry.LaunchTick &gt;= entry.FlightTicks</c> — on
/// <see cref="Clear"/> (a round reset), and, once the store is at capacity,
/// to make room for a newly released shot by evicting whichever live entry
/// is soonest to expire, tie-broken on the lower <c>Sequence</c>, the same
/// shape <c>HitEffectSystem</c> and <c>ClashEffectSystem</c> use for their own
/// full-buffer replacement. The backing array is allocated once, at
/// construction, and never grows; a full store never grows past
/// its capacity.
/// </para>
/// </remarks>
internal sealed class ProjectileFlightSystem
{
    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly ProjectileFlight[] _entries;
    private int _count;
    private long? _lastIngestedTick;

    public ProjectileFlightSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _entries = new ProjectileFlight[capacity];
    }

    public ReadOnlySpan<ProjectileFlight> LiveFlights => _entries.AsSpan(0, _count);

    /// <summary>
    /// Prunes every entry that has arrived as of <paramref name="tick"/>,
    /// advances the interpolated position of every entry that survives, and
    /// adds one new entry for each live <see cref="BattleEventKind.Release"/>
    /// event whose source warrior is present in <paramref name="agents"/>.
    /// A <see cref="BattleEventKind.Release"/> event whose source is absent,
    /// or whose flight time is zero or negative, is skipped: there is no
    /// origin, or nothing, to animate. A missing or absent target resolves
    /// the destination to the origin itself, rather than being skipped,
    /// since the shot still left the bow and still has a lifetime to spend.
    /// </summary>
    public void Ingest(
        long tick,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        if (_lastIngestedTick == tick)
        {
            return;
        }

        if (_lastIngestedTick is { } previousTick && tick < previousTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                tick,
                "Ingest ticks must never move backward; call Clear() " +
                "before restarting the store at an earlier tick.");
        }

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        Prune(tick);

        for (var index = 0; index < events.Count; index++)
        {
            TryAdd(events[index], tick);
        }

        _lastIngestedTick = tick;
    }

    public void Clear()
    {
        Array.Clear(_entries, 0, _count);
        _count = 0;
        _agentsById.Clear();
        _lastIngestedTick = null;
    }

    /// <summary>
    /// Drops every entry that has arrived as of <paramref name="tick"/> and
    /// re-interpolates the current position of every entry that survives.
    /// </summary>
    private void Prune(long tick)
    {
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _count; readIndex++)
        {
            var entry = _entries[readIndex];
            var elapsed = tick - entry.LaunchTick;
            if (elapsed >= entry.FlightTicks)
            {
                continue;
            }

            _entries[writeIndex] = Advance(entry, elapsed);
            writeIndex++;
        }

        Array.Clear(_entries, writeIndex, _count - writeIndex);
        _count = writeIndex;
    }

    private static ProjectileFlight Advance(ProjectileFlight entry, long elapsed)
    {
        var progress = entry.FlightTicks <= 0
            ? 1f
            : Math.Clamp((float)elapsed / entry.FlightTicks, 0f, 1f);

        return entry with
        {
            CurrentXRaw = Lerp(entry.OriginXRaw, entry.DestinationXRaw, progress),
            CurrentYRaw = Lerp(entry.OriginYRaw, entry.DestinationYRaw, progress),
        };
    }

    private static int Lerp(int from, int to, float progress) =>
        from + (int)MathF.Round((to - from) * progress);

    private void TryAdd(BattleEvent battleEvent, long tick)
    {
        if (battleEvent.Kind != BattleEventKind.Release ||
            battleEvent.Value <= 0 ||
            !_agentsById.TryGetValue(battleEvent.SourceEntityId, out var source))
        {
            return;
        }

        var destinationXRaw = source.XRaw;
        var destinationYRaw = source.YRaw;
        if (battleEvent.TargetEntityId is { } targetEntityId &&
            _agentsById.TryGetValue(targetEntityId, out var target))
        {
            destinationXRaw = target.XRaw;
            destinationYRaw = target.YRaw;
        }

        Add(new ProjectileFlight(
            battleEvent.Sequence,
            battleEvent.TargetEntityId,
            tick,
            battleEvent.Value,
            source.XRaw,
            source.YRaw,
            destinationXRaw,
            destinationYRaw,
            CurrentXRaw: source.XRaw,
            CurrentYRaw: source.YRaw,
            // Read from the launcher's view, never from the event: a Release
            // event is classless and carries no weapon. Captured here, at
            // launch, so a launcher that dies mid-flight cannot leave its own
            // shot without a silhouette. See ProjectileFlight.Weapon.
            source.Loadout.Weapon));
    }

    /// <summary>
    /// Appends, or overwrites the entry soonest to expire once the pool is
    /// full, breaking a tie on the lowest sequence.
    /// </summary>
    private void Add(ProjectileFlight entry)
    {
        if (_count < _entries.Length)
        {
            _entries[_count] = entry;
            _count++;
            return;
        }

        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _entries[index];
            var current = _entries[replacementIndex];
            if (ExpiresAtTick(candidate) < ExpiresAtTick(current) ||
                (ExpiresAtTick(candidate) == ExpiresAtTick(current) &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _entries[replacementIndex] = entry;
    }

    private static long ExpiresAtTick(ProjectileFlight entry) =>
        entry.LaunchTick + entry.FlightTicks;
}
