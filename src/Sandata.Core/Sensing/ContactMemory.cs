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
/// <c>Sandata.Core.Simulation.ContactMemoryEntry</c>'s <c>LastKnownCellIndex</c>
/// and <c>LastSeenTick</c> completely unchanged; a caller who wants the
/// ghost's age subtracts the unchanged <c>LastSeenTick</c> from the current
/// tick, and that subtraction grows every tick the ghost is not re-observed —
/// which is the whole of what "remembered rather than live" means here. The
/// one field a ghost does not keep forever is its <c>ContactTier</c>: see
/// <see cref="IdentifiedMemoryTicks"/> and <see cref="Decay"/> for the single,
/// one-way, terminal downgrade an <see cref="ContactTier.Identified"/> ghost
/// undergoes once it has aged past that threshold, and <see cref="Update"/>'s
/// <c>deadEnemyEntityIds</c> remarks for the one case a ghost is dropped
/// outright rather than downgraded. An enemy who has never been observed at
/// all — no prior entry, and this tick's observation (if any) does not clear
/// <see cref="ContactTier.Unknown"/> — has no entry whatsoever;
/// <see cref="ContactTier.Unknown"/> is never written into a stored entry,
/// only ever returned transiently by <see cref="ClassifyTier"/> and read by
/// <see cref="Update"/> to decide whether an entry should exist at all. See
/// <see cref="ContactTier.Unknown"/>'s own remarks.
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
    /// How many ticks an <see cref="ContactTier.Identified"/> ghost may go
    /// unseen before it downgrades to <see cref="ContactTier.QuestionMark"/>.
    /// At <c>TickRate</c> 50 this is 2 seconds. <b>Provisional
    /// reconstruction, unmeasured</b> — like <see cref="IdentifyRangeWu"/> and
    /// <see cref="DetectRangeWu"/>, this figure is not published anywhere in
    /// the design document; it exists only so an identified contact does not
    /// stay <c>Engage</c>-ranked forever once its subject has broken line of
    /// sight (see <see cref="Update"/>'s <c>deadEnemyEntityIds</c> remarks for
    /// the sibling bug this shares a root cause with — an
    /// <c>IntentSelection</c> that never revisits a stale
    /// <see cref="ContactTier.Identified"/> entry). Whichever future task can
    /// observe actual firefights is expected to confirm or revise it. The
    /// downgrade is one-way and terminal: a ghost that has already reached
    /// <see cref="ContactTier.QuestionMark"/> this way never decays further,
    /// because <see cref="ContactTier.QuestionMark"/> already means "not
    /// shootable" and there is nothing below it but dropping the entry
    /// outright, which would erase the last-known-cell age the HUD is
    /// specified to display (design section 4's "last known cell" table).
    /// </summary>
    public const long IdentifiedMemoryTicks = 100;

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
    /// <param name="deadEnemyEntityIds">
    /// Every enemy that is no longer alive, in any order. A memory entry
    /// naming one of these is dropped rather than carried forward as a ghost.
    /// See this method's remarks on why death is the one thing a ghost does
    /// not survive.
    /// </param>
    public static ImmutableArray<Simulation.ContactMemoryEntry> Update(
        ImmutableArray<Simulation.ContactMemoryEntry> existingMemory,
        ReadOnlySpan<ContactObservation> observationsThisTick,
        long currentTick,
        ReadOnlySpan<ulong> deadEnemyEntityIds = default) =>
        Update(existingMemory, observationsThisTick, currentTick, scratch: default, deadEnemyEntityIds);

    /// <summary>
    /// <see cref="Update(ImmutableArray{Simulation.ContactMemoryEntry}, ReadOnlySpan{ContactObservation}, long)"/>
    /// against a merge buffer the caller owns, for a caller that runs this
    /// once per operator per tick.
    /// </summary>
    /// <param name="scratch">
    /// Working space for the merge, at least
    /// <c>existingMemory.Length + observationsThisTick.Length</c> long. A
    /// shorter span — including the default empty one the allocating overload
    /// passes — makes this method allocate its own instead, so a caller may
    /// pass a buffer it has not resized yet without getting a wrong answer.
    /// Nothing survives one call into the next result: every element this
    /// method reads back it wrote during the same call, and the returned
    /// <see cref="ImmutableArray{T}"/> is built by copying out of it, so the
    /// buffer's prior contents can never reach a caller.
    /// </param>
    /// <param name="deadEnemyEntityIds">
    /// Every enemy that is no longer alive, in any order.
    /// <para>
    /// A ghost survives losing sight of its subject, by design — that is what
    /// "world state is remembered rather than live" means. It does not survive
    /// its subject dying, and is dropped outright rather than merely
    /// downgraded, because a dead subject is never coming back into view to
    /// re-earn the entry — unlike an <see cref="IdentifiedMemoryTicks"/> aging
    /// out, which reflects a live enemy the operator has simply lost sight of.
    /// Before this parameter existed, and before <see cref="IdentifiedMemoryTicks"/>
    /// existed, a contact identified once stayed <see cref="ContactTier.Identified"/>
    /// for the rest of the mission, and <c>IntentSelection</c> ranks an
    /// identified contact as <c>Engage</c> above every other intent
    /// unconditionally. Measured 2026-08-15 before this parameter existed: an
    /// operator whose target died at tick 672 still held <c>Engage</c> at tick
    /// 35,999, standing over the body while a live hostile elsewhere on the
    /// map was never approached.
    /// </para>
    /// <para>
    /// Forgetting is deliberately not gated on the observer having seen the
    /// death. Gating it that way would leave exactly the same permanent ghost
    /// whenever the kill happened out of sight, and — before
    /// <see cref="IdentifiedMemoryTicks"/> existed — this type had no decay
    /// rule to fall back on. That decay rule now exists for the living-but-
    /// unseen case; it is deliberately not reused for the dead case, because
    /// dropping the entry immediately is strictly more correct once the
    /// subject is confirmed dead, and there is no reason to wait out an aging
    /// window first.
    /// </para>
    /// </param>
    public static ImmutableArray<Simulation.ContactMemoryEntry> Update(
        ImmutableArray<Simulation.ContactMemoryEntry> existingMemory,
        ReadOnlySpan<ContactObservation> observationsThisTick,
        long currentTick,
        Span<Simulation.ContactMemoryEntry> scratch,
        ReadOnlySpan<ulong> deadEnemyEntityIds = default)
    {
        var existingSpan = existingMemory.IsDefault
            ? ReadOnlySpan<Simulation.ContactMemoryEntry>.Empty
            : existingMemory.AsSpan();

        var maxCount = existingSpan.Length + observationsThisTick.Length;
        if (maxCount == 0)
        {
            return ImmutableArray<Simulation.ContactMemoryEntry>.Empty;
        }

        var buffer = scratch.Length >= maxCount
            ? scratch[..maxCount]
            : new Simulation.ContactMemoryEntry[maxCount];
        var count = 0;

        // Every enemy already in memory: either refreshed by this tick's
        // observation, or carried forward unchanged as a ghost.
        foreach (var existing in existingSpan)
        {
            if (Contains(deadEnemyEntityIds, existing.EnemyEntityId))
            {
                continue; // dead: forgotten outright, never carried forward.
            }

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
                // Ghost: unobserved this tick. Cell and tick never change, but
                // an Identified ghost still ages toward its QuestionMark
                // downgrade — see Decay.
                buffer[count++] = Decay(existing, currentTick);
            }
        }

        // Every observation about an enemy with no prior entry: becomes a
        // brand-new entry only if this tick actually clears Unknown.
        foreach (var observation in observationsThisTick)
        {
            // An observation naming a dead enemy cannot reach here from the
            // simulation's own sensing pass, which never observes one, but a
            // direct caller can construct one — so the rule is applied on the
            // creation path too rather than trusting every caller to hold it.
            if (Contains(deadEnemyEntityIds, observation.EnemyEntityId))
            {
                continue;
            }

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

        var merged = buffer[..count];

        // Span.Sort over the merge window rather than Array.Sort over an
        // array slice, since the window may now live inside a caller-owned
        // span. Both dispatch to the same introsort, and this comparer's own
        // remarks record that no two entries in one call can compare equal —
        // so an unstable sort has no tie to be unstable about, and the order
        // is a total one either way.
        merged.Sort(EntityIdComparer.Instance);

        return ImmutableArray.Create((ReadOnlySpan<Simulation.ContactMemoryEntry>)merged);
    }

    /// <summary>
    /// Whether <paramref name="entityIds"/> names <paramref name="entityId"/>.
    /// A linear scan rather than a <c>HashSet&lt;&gt;</c>, which
    /// <c>CLAUDE.md</c> section 5 bans from <c>Sandata.Core</c> outright, and
    /// which would be the wrong shape anyway: this span holds one entry per
    /// dead operator, so it is empty for most of a mission and single-digit
    /// for the rest.
    /// </summary>
    private static bool Contains(ReadOnlySpan<ulong> entityIds, ulong entityId)
    {
        foreach (var candidate in entityIds)
        {
            if (candidate == entityId)
            {
                return true;
            }
        }

        return false;
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
            // Lost this tick: the prior entry persists as a ghost. Cell and
            // tick never change, but an Identified ghost still ages toward
            // its QuestionMark downgrade — see Decay.
            ? Decay(ghost, currentTick)
            : new Simulation.ContactMemoryEntry(
                observation.EnemyEntityId, observation.CurrentCellIndex, (int)tier, currentTick);
    }

    /// <summary>
    /// Ages a ghost by one tick's worth of possible downgrade. An
    /// <see cref="ContactTier.Identified"/> entry unseen for at least
    /// <see cref="IdentifiedMemoryTicks"/> becomes <see cref="ContactTier.QuestionMark"/>;
    /// every other tier — including a <see cref="ContactTier.QuestionMark"/>
    /// ghost that has already decayed once — passes through unchanged. This
    /// only ever runs against a ghost being carried forward, never against a
    /// freshly observed entry, so <see cref="Simulation.ContactMemoryEntry.LastSeenTick"/>
    /// and <see cref="Simulation.ContactMemoryEntry.LastKnownCellIndex"/> are
    /// never touched here — decay changes the tier only, and the age anchor
    /// this method reads (<paramref name="currentTick"/> minus the ghost's own
    /// <c>LastSeenTick</c>) must itself remain untouched for the next call to
    /// keep measuring from the tick the subject was actually last seen.
    /// </summary>
    private static Simulation.ContactMemoryEntry Decay(Simulation.ContactMemoryEntry ghost, long currentTick) =>
        ghost.ContactTier == (int)ContactTier.Identified && currentTick - ghost.LastSeenTick >= IdentifiedMemoryTicks
            ? ghost with { ContactTier = (int)ContactTier.QuestionMark }
            : ghost;

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
