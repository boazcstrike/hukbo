using System.Collections.Immutable;

namespace Sandata.Core.Sensing;

/// <summary>
/// One enemy sighting to fold into an operator's contact memory this tick.
/// Not itself authoritative or hashed state — it is the ephemeral per-tick
/// input <see cref="ContactMemory.Update"/> consumes, assembled by whichever
/// future task computes line of sight against the frozen tick-start view
/// (design section 5 stage 5: "sensing: line of sight, vision cone, contact
/// tier, hearing"). The task that commissioned this file was explicit that
/// the tick-start view type does not exist yet and must not be invented here,
/// so this type takes the two facts that view will eventually supply — "is
/// this enemy visible right now" and "how far away is it" — as plain
/// parameters instead.
/// </summary>
/// <param name="EnemyEntityId">
/// The enemy operator this observation is about. Matches
/// <c>Sandata.Core.Simulation.ContactMemoryEntry.EnemyEntityId</c> and
/// <c>Sandata.Core.Simulation.OperatorState.EntityId</c>'s <see langword="ulong"/> type.
/// </param>
/// <param name="HasLineOfSightThisTick">
/// Whether the observing operator's vision cone
/// (<see cref="Geometry.VisionCone"/>) and shadowcast field
/// (<see cref="Shadowcast"/>) both currently include this enemy's cell —
/// occlusion and facing only, with no distance limit baked in. Distance is
/// judged separately, by <paramref name="RangeSquaredWu"/>, so that the two
/// concerns — "can anything be seen in that direction at all" and "how much
/// detail does that sighting resolve to" — stay independently testable.
/// </param>
/// <param name="RangeSquaredWu">
/// The squared straight-line distance, in world units, from the observing
/// operator to this enemy. Ignored when <paramref name="HasLineOfSightThisTick"/>
/// is <see langword="false"/>. Never a plain (non-squared) distance —
/// <c>CLAUDE.md</c> section 4 and design section 4 both ban
/// <c>Math.Sqrt</c> from <c>Sandata.Core</c>.
/// </param>
/// <param name="CurrentCellIndex">
/// The enemy's current nav-grid cell index, recorded as the sighting's
/// location when this observation updates the memory. Ignored when
/// <paramref name="HasLineOfSightThisTick"/> is <see langword="false"/>.
/// </param>
public readonly record struct ContactObservation(
    ulong EnemyEntityId,
    bool HasLineOfSightThisTick,
    long RangeSquaredWu,
    int CurrentCellIndex);

/// <summary>
/// The contact-memory update rule design section 4's "per operator: contact
/// memory — for each remembered enemy, the last known cell, the contact
/// tier, and the tick it was last seen" and the research consolidation's
/// "world state is remembered rather than live... enemies leave ghosts at
/// their last known position" both describe.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two range boundaries.</b> <see cref="ClassifyTier"/> owns both
/// numeric thresholds design section 4's contact-tier language implies:
/// <see cref="IdentifyRangeWu"/>, inside which a sighted enemy resolves to
/// <see cref="ContactTier.Identified"/>, and <see cref="DetectRangeWu"/>,
/// beyond which even a sighted enemy resolves to no better than
/// <see cref="ContactTier.QuestionMark"/> — and beyond which, in turn, an
/// enemy who is technically inside the vision cone and unoccluded still
/// registers as <see cref="ContactTier.Unknown"/> this tick, because
/// "something is there" requires being close enough to notice, not merely
/// geometrically unoccluded. Neither figure is published anywhere in
/// docs/plans/2026-08-07-sandata-scaffold-design.md or the research
/// consolidation it is built from; both are <b>provisional
/// reconstructions</b>, in the same spirit as <c>SandataRuleset</c>'s own
/// unmeasured tuning constants, chosen as round metre figures at the design's
/// pinned 16 wu-per-metre scale with <see cref="IdentifyRangeWu"/> well
/// inside <see cref="DetectRangeWu"/> so the three-tier progression the
/// design describes is actually reachable. Whichever future task wires
/// sensing into the tick pipeline and can observe actual firefights is
/// expected to confirm or revise both.
/// </para>
/// <para>
/// <b>Ghosts.</b> An enemy not observed this tick — no
/// <see cref="ContactObservation"/> at all, or one with
/// <see cref="ContactObservation.HasLineOfSightThisTick"/> false, or one whose
/// range classifies as <see cref="ContactTier.Unknown"/> — keeps its prior
/// <c>Sandata.Core.Simulation.ContactMemoryEntry</c> completely unchanged:
/// same <c>LastKnownCellIndex</c>, same <c>ContactTier</c>, same
/// <c>LastSeenTick</c>. Nothing in this type computes an age; a caller who
/// wants one subtracts the unchanged <c>LastSeenTick</c> from the current
/// tick, and that subtraction grows every tick the ghost is not re-observed —
/// which is the whole of what "remembered rather than live" means here. An
/// enemy who has never been observed at all — no prior entry, and this tick's
/// observation (if any) does not clear <see cref="ContactTier.Unknown"/> —
/// has no entry whatsoever; <see cref="ContactTier.Unknown"/> is never written
/// into a stored entry, only ever returned transiently by
/// <see cref="ClassifyTier"/> and read by <see cref="Update"/> to decide
/// whether an entry should exist at all. See <see cref="ContactTier.Unknown"/>'s
/// own remarks.
/// </para>
/// <para>
/// <b>Determinism.</b> The returned array is always sorted ascending by
/// <c>EnemyEntityId</c>, regardless of the order <paramref name="existingMemory"/>
/// or <paramref name="observationsThisTick"/> arrived in, matching every other
/// per-entity collection's ordering convention in this codebase. No
/// <c>Dictionary&lt;&gt;</c> or <c>HashSet&lt;&gt;</c> is used anywhere in this
/// file, per <c>CLAUDE.md</c> section 4's ban on both inside
/// <c>Sandata.Core</c>; matching an existing entry to an observation is a
/// linear scan, which is a fine cost for the handful of remembered enemies one
/// operator ever carries.
/// </para>
/// <para>
/// <b>Doors are not enemies, but the same rule applies to them.</b> The task
/// that commissioned this file requires proving that "a door opened out of
/// sight is not observed until seen" — the same remembered-not-live rule the
/// contact tiers follow, applied to a single fact about the world rather than
/// to a roster of enemies. <see cref="ObserveOrRemember{T}"/> is that rule,
/// generalised to any single observed value, so a caller tracking a door's
/// open/closed state (or any other single world fact an operator can only
/// learn by directly observing it) can reuse the identical rule this file
/// already proves for contacts, rather than a second, independently-written
/// copy of the same "keep the old value unless currently observed" branch.
/// </para>
/// </remarks>
public static class ContactMemory
{
    /// <summary>
    /// 6 m, at the design's pinned 16 wu-per-metre scale. <b>Provisional
    /// reconstruction</b> — see this type's remarks.
    /// </summary>
    public const int IdentifyRangeWu = 96;

    /// <summary>
    /// 16 m, at the design's pinned 16 wu-per-metre scale. <b>Provisional
    /// reconstruction</b> — see this type's remarks.
    /// </summary>
    public const int DetectRangeWu = 256;

    private const long IdentifyRangeSquaredWu = (long)IdentifyRangeWu * IdentifyRangeWu;
    private const long DetectRangeSquaredWu = (long)DetectRangeWu * DetectRangeWu;

    /// <summary>
    /// Classifies a sighted enemy's contact tier from nothing but the squared
    /// distance to it, per this type's remarks on the two range boundaries.
    /// The boundary is inclusive on both thresholds: a range exactly equal to
    /// <see cref="IdentifyRangeWu"/> is <see cref="ContactTier.Identified"/>,
    /// and a range exactly equal to <see cref="DetectRangeWu"/> is
    /// <see cref="ContactTier.QuestionMark"/>.
    /// </summary>
    /// <param name="rangeSquaredWu">
    /// The squared straight-line distance, in world units, between observer
    /// and candidate. Never negative in a real caller, but this method does
    /// not itself validate that — a negative value simply classifies as
    /// <see cref="ContactTier.Identified"/>, the same as zero, since it is
    /// smaller than both thresholds.
    /// </param>
    public static ContactTier ClassifyTier(long rangeSquaredWu)
    {
        if (rangeSquaredWu <= IdentifyRangeSquaredWu)
        {
            return ContactTier.Identified;
        }

        if (rangeSquaredWu <= DetectRangeSquaredWu)
        {
            return ContactTier.QuestionMark;
        }

        return ContactTier.Unknown;
    }

    /// <summary>
    /// Folds this tick's <paramref name="observationsThisTick"/> into
    /// <paramref name="existingMemory"/>, returning a new array rather than
    /// mutating either argument. See this type's remarks for the ghost rule,
    /// the two range boundaries, and the ordering guarantee.
    /// </summary>
    /// <param name="existingMemory">
    /// The operator's contact memory as of the start of this tick. Not
    /// mutated; the returned array is a new one.
    /// </param>
    /// <param name="observationsThisTick">
    /// Every enemy this tick's sensing pass has an opinion about, in any
    /// order. An enemy with no observation at all is treated exactly like one
    /// whose observation has <see cref="ContactObservation.HasLineOfSightThisTick"/>
    /// <see langword="false"/> — both mean "not observed this tick" — so a
    /// caller may omit an enemy entirely rather than construct a
    /// not-observed placeholder for it.
    /// </param>
    /// <param name="currentTick">
    /// The tick this update is being computed for. Written into
    /// <c>LastSeenTick</c> for every entry that is newly observed (at
    /// <see cref="ContactTier.QuestionMark"/> or <see cref="ContactTier.Identified"/>)
    /// this call; left unchanged on every ghost.
    /// </param>
    public static ImmutableArray<Simulation.ContactMemoryEntry> Update(
        ImmutableArray<Simulation.ContactMemoryEntry> existingMemory,
        ReadOnlySpan<ContactObservation> observationsThisTick,
        long currentTick)
    {
        var existingSpan = existingMemory.IsDefault
            ? ReadOnlySpan<Simulation.ContactMemoryEntry>.Empty
            : existingMemory.AsSpan();

        var maxCount = existingSpan.Length + observationsThisTick.Length;
        if (maxCount == 0)
        {
            return ImmutableArray<Simulation.ContactMemoryEntry>.Empty;
        }

        var buffer = new Simulation.ContactMemoryEntry[maxCount];
        var count = 0;

        // Every enemy already in memory: either refreshed by this tick's
        // observation, or carried forward unchanged as a ghost.
        foreach (var existing in existingSpan)
        {
            var matched = false;
            foreach (var observation in observationsThisTick)
            {
                if (observation.EnemyEntityId != existing.EnemyEntityId)
                {
                    continue;
                }

                matched = true;
                buffer[count++] = Resolve(observation, currentTick, ghost: existing);
                break;
            }

            if (!matched)
            {
                buffer[count++] = existing; // ghost: unobserved this tick, nothing changes.
            }
        }

        // Every observation about an enemy with no prior entry: becomes a
        // brand-new entry only if this tick actually clears Unknown.
        foreach (var observation in observationsThisTick)
        {
            var alreadyHandled = false;
            foreach (var existing in existingSpan)
            {
                if (existing.EnemyEntityId == observation.EnemyEntityId)
                {
                    alreadyHandled = true;
                    break;
                }
            }

            if (alreadyHandled)
            {
                continue;
            }

            var tier = TierOf(observation);
            if (tier == ContactTier.Unknown)
            {
                continue; // never detected: no ghost to create.
            }

            buffer[count++] = new Simulation.ContactMemoryEntry(
                observation.EnemyEntityId, observation.CurrentCellIndex, (int)tier, currentTick);
        }

        Array.Sort(buffer, 0, count, EntityIdComparer.Instance);

        return ImmutableArray.Create(buffer, 0, count);
    }

    /// <summary>
    /// Design section 4's "world state is remembered rather than live" rule,
    /// generalised to a single observed value of any type: the remembered
    /// value carries forward unchanged unless the observer can currently see
    /// the thing it describes, in which case the freshly observed value wins.
    /// This is the exact rule <see cref="Update"/> applies to a whole roster
    /// of enemy contacts, expressed here for a single fact — a door's
    /// open/closed state, for example — so both can share one proven
    /// implementation of "keep the old value unless observed now" rather than
    /// two independently-written copies of the same branch.
    /// </summary>
    /// <param name="rememberedValue">
    /// What the operator last knew, from an earlier tick's observation (or
    /// this call's default caller-supplied starting belief).
    /// </param>
    /// <param name="observedValue">
    /// The true, current value of the thing being observed — read only when
    /// <paramref name="isObservedThisTick"/> is <see langword="true"/>.
    /// </param>
    /// <param name="isObservedThisTick">
    /// Whether the observer's tick-start view can currently see the thing
    /// <paramref name="observedValue"/> describes. When <see langword="false"/>,
    /// <paramref name="observedValue"/> is never read, exactly as a door
    /// opened out of sight is not observed until the observer can see it
    /// again.
    /// </param>
    public static T ObserveOrRemember<T>(T rememberedValue, T observedValue, bool isObservedThisTick) =>
        isObservedThisTick ? observedValue : rememberedValue;

    private static Simulation.ContactMemoryEntry Resolve(
        ContactObservation observation, long currentTick, Simulation.ContactMemoryEntry ghost)
    {
        var tier = TierOf(observation);
        return tier == ContactTier.Unknown
            ? ghost // lost this tick: the prior entry persists as a ghost, wholly unchanged.
            : new Simulation.ContactMemoryEntry(
                observation.EnemyEntityId, observation.CurrentCellIndex, (int)tier, currentTick);
    }

    private static ContactTier TierOf(ContactObservation observation) =>
        observation.HasLineOfSightThisTick ? ClassifyTier(observation.RangeSquaredWu) : ContactTier.Unknown;

    /// <summary>
    /// Orders <see cref="Simulation.ContactMemoryEntry"/> values ascending by
    /// <see cref="Simulation.ContactMemoryEntry.EnemyEntityId"/>. Every
    /// enemy id this comparer ever sorts is unique within one call to
    /// <see cref="Update"/> — an enemy appears at most once across
    /// <paramref name="existingMemory"/>-turned-<paramref name="buffer"/> and
    /// the unmatched observations merged in after it — so <c>Array.Sort</c>'s
    /// introsort needing no tie-break is a correctness fact here, not an
    /// assumption.
    /// </summary>
    private sealed class EntityIdComparer : IComparer<Simulation.ContactMemoryEntry>
    {
        public static readonly EntityIdComparer Instance = new();

        public int Compare(Simulation.ContactMemoryEntry x, Simulation.ContactMemoryEntry y) =>
            x.EnemyEntityId.CompareTo(y.EnemyEntityId);
    }
}
