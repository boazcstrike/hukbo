using Hukbo.Client.Rendering;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One warrior's stored gait state: its authoritative position as of the last
/// ingested tick, its stride phase, and the mode and direction that position
/// change most recently produced. Held for presentation only, never
/// authoritative and never hashed.
/// </summary>
/// <param name="EntityId">The warrior this entry belongs to.</param>
/// <param name="PreviousXRaw">
/// The warrior's <c>AgentView.XRaw</c> as of the last ingested tick, used to
/// compute the next tick's displacement.
/// </param>
/// <param name="PreviousYRaw">The <c>YRaw</c> counterpart of <see cref="PreviousXRaw"/>.</param>
/// <param name="PhaseTurns">
/// Progress through one stride cycle, wrapped into <c>[0, 1)</c>. Includes the
/// per-entity deterministic offset baked in at the entry's creation, so two
/// warriors moving identically still carry different values here.
/// </param>
/// <param name="Mode">The stride state the most recent tick resolved.</param>
/// <param name="DirectionSign">
/// The world-X sign of the most recent nonzero displacement, or zero if the
/// warrior has never moved since this entry was created.
/// </param>
internal readonly record struct GaitEntry(
    ulong EntityId,
    int PreviousXRaw,
    int PreviousYRaw,
    float PhaseTurns,
    GaitMode Mode,
    float DirectionSign);

/// <summary>
/// Holds one gait entry per living warrior currently in view, in a
/// fixed-capacity array sized at construction. Mirrors
/// <see cref="SwingAnimationSystem"/>'s fixed-capacity, zero-heap-allocation
/// shape, keyed on position change rather than on battle events.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Ingest"/> is the only place phase advances, and it advances by
/// distance travelled since the previous ingested tick, never by elapsed
/// time — see the design's "phase is advanced by distance, not by time"
/// section. A warrior standing still for a tick does not advance its phase at
/// all; instead its stored phase eases toward the nearest neutral point (a
/// multiple of half a turn, where every stride offset is already zero) by a
/// fixed fraction of the remaining distance, per ingested tick. This has no
/// visible effect while the warrior is actually standing still, because
/// <see cref="GaitGeometry.ResolvePose"/> zeroes every offset at
/// <see cref="GaitMode.Stance"/> regardless of phase; its purpose is to leave
/// the phase near a neutral point so that motion resuming after a long idle
/// period does not make the legs jump instantly to an arbitrary mid-stride
/// extension on the very first moving tick.
/// </para>
/// <para>
/// A warrior absent from, or not alive in, the views supplied to
/// <see cref="Ingest"/> is dropped from the store on that same call, so a
/// corpse's phase never lingers into a later round and a later reincarnation
/// of the same <c>EntityId</c> (which does not happen today, but is not
/// assumed against) starts from a clean entry.
/// </para>
/// </remarks>
internal sealed class GaitAnimationSystem
{
    /// <summary>
    /// PROVISIONAL. Raw fixed-point distance a warrior must cover to complete
    /// one full stride cycle. Chosen so a warrior moving at the run threshold
    /// completes a cycle in a small handful of ticks, rather than either
    /// freezing for many ticks per step or cycling faster than a spectator can
    /// register.
    /// </summary>
    internal const float StrideCycleDistanceRaw = 6000f;

    /// <summary>
    /// PROVISIONAL. Fraction of the remaining distance to the nearest neutral
    /// phase point (a multiple of half a turn) closed per ingested tick a
    /// warrior stands still. Ticks, not seconds, so a paused battle also
    /// freezes this easing exactly where it was.
    /// </summary>
    internal const float IdleEasePerTick = 0.2f;

    /// <summary>
    /// Local mixing salt for the per-entity phase offset
    /// (<see cref="ResolvePhaseOffsetTurns"/>), in the same spirit as
    /// <see cref="PresentationSalts"/> but not registered there: the offset
    /// reads only <c>EntityId</c>, the same shape
    /// <see cref="DustEffectSystem"/>'s attack-kick throttle already uses
    /// without a registry entry, for the same reason — a per-entity constant
    /// derived entirely from an identity the caller already carries needs no
    /// shared stream of its own to stay collision-free.
    /// </summary>
    private const ulong PhaseOffsetSalt = 0x9B1F5A3E7C4D2601UL;

    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly GaitEntry[] _entries;
    private int _count;

    public GaitAnimationSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _entries = new GaitEntry[capacity];
    }

    public ReadOnlySpan<GaitEntry> ActiveEntries => _entries.AsSpan(0, _count);

    /// <summary>
    /// Updates every living warrior's gait entry from its position change
    /// since the previous call, drops any entry whose warrior is absent or no
    /// longer alive, and creates a new entry for any living warrior not
    /// already held, up to capacity.
    /// </summary>
    public void Ingest(IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (agent.IsAlive)
            {
                _agentsById[agent.EntityId] = agent;
            }
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _count; readIndex++)
        {
            var entry = _entries[readIndex];
            if (!_agentsById.Remove(entry.EntityId, out var agent))
            {
                // Absent or dead: dropped, not carried into the compacted
                // array.
                continue;
            }

            _entries[writeIndex] = Advance(entry, agent);
            writeIndex++;
        }

        // Whatever remains in _agentsById after the loop above removed every
        // warrior with an existing entry is newly seen this tick.
        foreach (var agent in _agentsById.Values)
        {
            if (writeIndex >= _entries.Length)
            {
                // Capacity is expected to be sized to the roster; a full
                // store silently skips animating any warrior beyond it rather
                // than evicting one already mid-stride.
                break;
            }

            _entries[writeIndex] = new GaitEntry(
                agent.EntityId,
                agent.XRaw,
                agent.YRaw,
                ResolvePhaseOffsetTurns(agent.EntityId),
                GaitMode.Stance,
                DirectionSign: 0f);
            writeIndex++;
        }

        // writeIndex can exceed the previous _count when new entries were
        // added, so only the surplus of the old array actually needs
        // clearing.
        var staleLength = Math.Max(0, _count - writeIndex);
        Array.Clear(_entries, writeIndex, staleLength);
        _count = writeIndex;
    }

    /// <summary>
    /// Fetches the gait entry held for one warrior, if any.
    /// </summary>
    public bool TryGetEntry(ulong entityId, out GaitEntry entry)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_entries[index].EntityId != entityId)
            {
                continue;
            }

            entry = _entries[index];
            return true;
        }

        entry = default;
        return false;
    }

    public void Clear()
    {
        Array.Clear(_entries, 0, _count);
        _count = 0;
        _agentsById.Clear();
    }

    /// <summary>
    /// Advances one entry from the warrior's position this tick: a nonzero
    /// displacement classifies the mode and direction, and advances the phase
    /// by the distance covered; a zero displacement resolves
    /// <see cref="GaitMode.Stance"/>, keeps the previous direction, and eases
    /// the phase toward the nearest neutral point instead of advancing it.
    /// </summary>
    private static GaitEntry Advance(GaitEntry entry, AgentView agent)
    {
        var deltaX = (float)(agent.XRaw - entry.PreviousXRaw);
        var deltaY = (float)(agent.YRaw - entry.PreviousYRaw);
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var mode = GaitGeometry.ResolveMode(distance);

        if (mode == GaitMode.Stance)
        {
            return entry with
            {
                PreviousXRaw = agent.XRaw,
                PreviousYRaw = agent.YRaw,
                PhaseTurns = EaseTowardNeutral(entry.PhaseTurns),
                Mode = mode,
            };
        }

        var directionSign = deltaX switch
        {
            > 0f => 1f,
            < 0f => -1f,
            _ => entry.DirectionSign,
        };
        var phase = WrapTurns(
            entry.PhaseTurns + (distance / StrideCycleDistanceRaw));

        return entry with
        {
            PreviousXRaw = agent.XRaw,
            PreviousYRaw = agent.YRaw,
            PhaseTurns = phase,
            Mode = mode,
            DirectionSign = directionSign,
        };
    }

    /// <summary>
    /// Closes a fixed fraction of the distance from <paramref name="phase"/>
    /// to the nearer of the two neutral points a full turn contains — zero and
    /// half a turn, where every stride offset <see cref="GaitGeometry"/>
    /// produces is already zero regardless of mode.
    /// </summary>
    private static float EaseTowardNeutral(float phase)
    {
        var nearestNeutral = MathF.Round(phase * 2f) / 2f;
        var eased = phase + ((nearestNeutral - phase) * IdleEasePerTick);
        return WrapTurns(eased);
    }

    private static float WrapTurns(float turns)
    {
        var wrapped = turns % 1f;
        return wrapped < 0f ? wrapped + 1f : wrapped;
    }

    /// <summary>
    /// Deterministic per-entity phase offset, so warriors moving identically
    /// do not step in lockstep. A SplitMix64 finalizer over the entity's id
    /// mixed with <see cref="PhaseOffsetSalt"/>, the same finalizer shape
    /// <see cref="DustEffectSystem"/> already uses for its attack-kick
    /// throttle. No randomness of any kind: the same <c>EntityId</c> always
    /// resolves the same offset.
    /// </summary>
    private static float ResolvePhaseOffsetTurns(ulong entityId)
    {
        var mixed = Mix(entityId ^ PhaseOffsetSalt);

        // 24 bits of the mixed value give ample resolution for a visual phase
        // offset while keeping the division exact in float arithmetic.
        return (mixed >> 40) / (float)(1UL << 24);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
