using Hukbo.Client.Rendering;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// The bounded per-battle appearance cache (GPU-017). Every warrior drawn in
/// a frame needs its <see cref="PawnAppearance"/>, and that appearance is a
/// pure function of inputs that cannot change while a battle runs, so
/// recomputing it for every agent on every frame is work the renderer pays
/// for nothing.
/// </summary>
/// <remarks>
/// <para>
/// This type carries the full cache declaration that
/// <c>SIMULATION-GAME-STANDARDS.md</c> requires before a cache may be
/// written, and <c>CLAUDE.md</c>'s prohibition on unbounded caches is met by
/// construction rather than by discipline: the storage is one flat array
/// allocated once at <see cref="Capacity"/> entries and there is no code path
/// that resizes it.
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Source.</b> <see cref="PawnAppearanceFactory.Create"/> remains the
/// single authority. This type never computes an appearance itself; it only
/// remembers one the factory produced.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Key.</b> The triple (entity ID, weapon, shield), not the entity ID
/// alone. Keying on identity alone would silently return a stale appearance
/// if a loadout ever became mutable, and the key must not depend on a
/// property the simulation is not contractually obliged to keep.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Value.</b> The <see cref="PawnAppearance"/> value.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Size bound.</b> <see cref="Capacity"/> entries, which is
/// <c>2 * ArmyCompositionStepper.MaximumUnitsPerTeam</c> — one slot for every
/// warrior the largest permitted battle can field. This is a hard capacity
/// allocated once, not a growth target. Because a battle's agent set is fixed
/// at scenario creation, the cache fills exactly once and never evicts.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Lifetime.</b> One battle. <see cref="Clear"/> is called on scenario
/// reset, on next round, and on full reset.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Invalidation.</b> None during a battle, because no key input can change
/// during a battle. That is a load-bearing assumption rather than a
/// convenience, and <c>PawnAppearanceCacheTests</c> asserts it against the
/// shape of <c>AgentView</c> and <c>CombatLoadout</c> themselves: if a
/// loadout ever becomes mutable mid-battle, that test fails and this cache is
/// invalid.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Counters.</b> Hits, misses, and fills, reported through the existing
/// <see cref="IRenderMetricsRecorder"/> seam so a probe run shows them.
/// </description>
/// </item>
/// </list>
/// <para>
/// Slots are addressed by the agent's ordinal position in the roster rather
/// than by a hash of the key, which is what keeps the cache a flat array with
/// no probing, no buckets, and no growth. The ordinal is only a guess at
/// where the answer lives: the key is stored beside the value and compared on
/// every read, so an ordinal that has come to mean a different agent produces
/// a miss and a recomputation, never a wrong appearance. An ordinal outside
/// the capacity bypasses the cache entirely and is answered straight from the
/// factory, so a battle larger than the declared bound stays correct and
/// merely stops being accelerated.
/// </para>
/// <para>
/// <see cref="Resolve"/> deliberately takes only the three key inputs and
/// calls the factory the way both render call sites already do, leaving
/// <c>factionId</c> and <c>scenarioSeed</c> at the factory's own defaults.
/// Accepting an input that is not part of the key would let two reads with
/// the same key legitimately expect different answers, which is exactly the
/// failure this cache must not be able to produce. When VIS-022 threads the
/// real faction and scenario-seed values through the render path, they become
/// part of the key and both this signature and the stored entry change with
/// them.
/// </para>
/// <para>
/// Not thread-safe, matching every other per-frame render-side type: read and
/// written from the single render thread only.
/// </para>
/// </remarks>
internal sealed class PawnAppearanceCache
{
    /// <summary>
    /// The hard capacity, in entries: one slot for each warrior in the
    /// largest battle the composition stepper permits, both teams counted.
    /// Declared against
    /// <see cref="ArmyCompositionStepper.MaximumUnitsPerTeam"/> rather than
    /// written out as a number, so raising the stepper's maximum raises this
    /// bound in the same edit instead of silently leaving the tail of a
    /// larger battle uncached.
    /// </summary>
    internal const int Capacity = 2 * ArmyCompositionStepper.MaximumUnitsPerTeam;

    /// <summary>
    /// The whole of the cache's storage: one array, allocated once at
    /// <see cref="Capacity"/> and never reallocated. <see cref="Entry"/> is a
    /// value type, so this is a single contiguous allocation with no
    /// per-entry object behind it.
    /// </summary>
    private readonly Entry[] _entries = new Entry[Capacity];

    private readonly IRenderMetricsRecorder _recorder;

    private int _fill;

    /// <param name="recorder">
    /// Where hit, miss, and fill counts are reported. Pass
    /// <see cref="NullRenderMetricsRecorder.Instance"/> when measurement is
    /// off; its calls are no-ops that allocate nothing, so the cache does not
    /// need to test whether recording is enabled before counting something it
    /// has already computed.
    /// </param>
    public PawnAppearanceCache(IRenderMetricsRecorder recorder) =>
        _recorder = recorder;

    /// <summary>
    /// How many slots currently hold an appearance. Rises to the battle's
    /// agent count over the first frame and stays there until
    /// <see cref="Clear"/>. Never exceeds <see cref="Capacity"/>, because
    /// there are only that many slots.
    /// </summary>
    internal int Fill => _fill;

    /// <summary>
    /// Returns this agent's appearance, from the cache when the slot at
    /// <paramref name="ordinal"/> already holds this exact key and from
    /// <see cref="PawnAppearanceFactory.Create"/> otherwise.
    /// </summary>
    /// <param name="ordinal">
    /// The agent's position in the roster this frame is drawing. Used as the
    /// slot address only; the stored key, not this value, decides whether the
    /// slot's contents are actually this agent's.
    /// </param>
    /// <param name="entityId">The agent's identity, part of the key.</param>
    /// <param name="weapon">The authoritative weapon, part of the key.</param>
    /// <param name="shield">The authoritative shield, part of the key.</param>
    public PawnAppearance Resolve(
        int ordinal,
        ulong entityId,
        WeaponId weapon,
        ShieldId shield)
    {
        if ((uint)ordinal >= (uint)Capacity)
        {
            // Beyond the declared bound. Answer correctly from the single
            // authority and record the read as a miss, because that is what
            // it costs; growing the array instead is the one thing this type
            // must never do.
            _recorder.AddAppearanceCacheMisses(1);
            return PawnAppearanceFactory.Create(entityId, weapon, shield);
        }

        ref var entry = ref _entries[ordinal];

        if (entry.IsOccupied &&
            entry.EntityId == entityId &&
            entry.Weapon == weapon &&
            entry.Shield == shield)
        {
            _recorder.AddAppearanceCacheHits(1);
            return entry.Appearance;
        }

        var appearance =
            PawnAppearanceFactory.Create(entityId, weapon, shield);

        _recorder.AddAppearanceCacheMisses(1);

        if (!entry.IsOccupied)
        {
            _fill++;
            _recorder.AddAppearanceCacheFills(1);
        }

        entry = new Entry(true, entityId, weapon, shield, appearance);
        return appearance;
    }

    /// <summary>
    /// Empties every slot. Called at the three points that already rebuild
    /// presentation state — scenario reset, next round, and full reset —
    /// which is the whole of the cache's one-battle lifetime rule.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_entries);
        _fill = 0;
    }

    /// <summary>
    /// One slot: the stored key beside the stored value. A value type, so the
    /// whole cache is one array and never a graph of objects.
    /// </summary>
    private readonly record struct Entry(
        // Whether this slot holds anything. Carried explicitly rather than
        // inferred from a sentinel entity ID, because zero is a legitimate
        // entity ID and an empty slot must never be mistaken for a stored
        // one. default(Entry) is therefore the empty slot, which is what lets
        // Clear be a single array clear.
        bool IsOccupied,
        ulong EntityId,
        WeaponId Weapon,
        ShieldId Shield,
        PawnAppearance Appearance);
}
