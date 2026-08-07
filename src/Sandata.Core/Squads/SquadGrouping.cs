using Sandata.Core.Collision;

namespace Sandata.Core.Squads;

/// <summary>
/// Derives squad membership, leadership, and marching-order slot for one
/// tick's operator roster, without storing any of it anywhere. Design section
/// 8 of <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Grouping is
/// derived, not stored": groups form by deterministic union-find, with path
/// compression and union by size, over the candidate contact-pair list —
/// each pair already normalised to (lower entity ID, higher entity ID) and
/// the whole list sorted ascending, the shape
/// <c>SandataCollisionGrid.Pairs</c> already produces — restricted to pairs
/// whose two operators share a faction. A component's identity is the
/// minimum entity ID it has ever contained; its leader is the lowest living
/// entity ID it currently contains. Both are recomputed from nothing on
/// every call, which is what lets a death re-derive a leader on the same
/// tick with no leaderless interregnum, and what lets a saved mission resume
/// with zero extra squad state.
/// </summary>
internal static class SquadGrouping
{
    /// <summary>
    /// Computes one <see cref="SquadSlot"/> per entry of
    /// <paramref name="entityIds"/>, written into the matching index of
    /// <paramref name="results"/>. <b>Retained for a caller with no position
    /// data to give the cohesion radius something to measure against</b> —
    /// today, <c>MovementSourceTests</c>, which exercises grouping in
    /// isolation from order assignment and never claimed to prove the radius.
    /// This overload unions every same-faction candidate pair
    /// unconditionally, exactly as this type did before task 74 gave the
    /// radius somewhere to be read: it is the pre-task-74 behaviour, kept
    /// only because nothing yet supplies this overload with positions, not
    /// because it is the design's rule. Design section 8 of
    /// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Grouping is
    /// derived, not stored", states the radius-gated rule; the overload below
    /// that takes positions and <c>groupCohesionRadiusRaw</c> is the one that
    /// enforces it, and is the one every future caller should prefer once it
    /// has positions to give it (task 49, the tick pipeline wiring).
    /// </summary>
    /// <param name="entityIds">
    /// Every operator in the tick-start roster, strictly ascending with no
    /// duplicate — the order <c>MissionState.Operators</c> is documented to
    /// hold.
    /// </param>
    /// <param name="isAlive">Parallel to <paramref name="entityIds"/>: whether that operator is currently alive.</param>
    /// <param name="factions">Parallel to <paramref name="entityIds"/>: that operator's faction.</param>
    /// <param name="pairs">
    /// The candidate contact pairs for this tick. The order of this list
    /// never affects the result: union is applied pair by pair, and set
    /// union is commutative and associative regardless of application
    /// order, so permuting <paramref name="pairs"/> permutes nothing in
    /// <paramref name="results"/>.
    /// </param>
    /// <param name="results">
    /// Receives one result per entry of <paramref name="entityIds"/>, in the
    /// same order. Must be exactly <paramref name="entityIds"/>.Length long.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pairs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="isAlive"/>, <paramref name="factions"/>, or
    /// <paramref name="results"/> has a different length than
    /// <paramref name="entityIds"/>; <paramref name="entityIds"/> is not
    /// strictly ascending; or a pair in <paramref name="pairs"/> names an
    /// entity ID absent from <paramref name="entityIds"/>.
    /// </exception>
    internal static void Compute(
        ReadOnlySpan<ulong> entityIds,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> factions,
        IReadOnlyList<SandataCollisionPair> pairs,
        Span<SquadSlot> results)
    {
        ComputeCore(
            entityIds, isAlive, factions, pairs, results,
            xRaw: default, yRaw: default, groupCohesionRadiusRaw: 0, applyCohesionRadius: false);
    }

    /// <summary>
    /// Computes one <see cref="SquadSlot"/> per entry of
    /// <paramref name="entityIds"/>, gating every union on
    /// <paramref name="groupCohesionRadiusRaw"/> as design section 8 states:
    /// "Two operators of the same faction within <c>GroupCohesionRadius</c>
    /// are unioned." <paramref name="groupCohesionRadiusRaw"/> is a plain
    /// caller-supplied parameter, never read from <c>SandataRuleset</c> by
    /// this type — the same convention <c>PathService</c> and
    /// <c>WeaponLoweredRules</c> already follow for their own ruleset
    /// constants, so the value stays usable from a test or a tool with no
    /// ruleset to hand.
    /// </summary>
    /// <param name="entityIds">
    /// Every operator in the tick-start roster, strictly ascending with no
    /// duplicate — the order <c>MissionState.Operators</c> is documented to
    /// hold.
    /// </param>
    /// <param name="isAlive">Parallel to <paramref name="entityIds"/>: whether that operator is currently alive.</param>
    /// <param name="factions">Parallel to <paramref name="entityIds"/>: that operator's faction.</param>
    /// <param name="xRaw">Parallel to <paramref name="entityIds"/>: that operator's world X position, in raw fixed-point units.</param>
    /// <param name="yRaw">Parallel to <paramref name="entityIds"/>: that operator's world Y position, in raw fixed-point units.</param>
    /// <param name="groupCohesionRadiusRaw">
    /// The distance, in raw world units, within which two same-faction
    /// operators are unioned — <c>SandataRuleset.GroupCohesionRadius</c> in
    /// the caller's possession. The comparison is inclusive: two operators
    /// exactly this distance apart are unioned, matching the collision
    /// broad phase's own inclusive <c>IsContact</c> convention.
    /// </param>
    /// <param name="pairs">
    /// The candidate contact pairs for this tick — a broad-phase shortlist
    /// only. A pair absent from this list is never unioned even when the two
    /// operators it would have named are within
    /// <paramref name="groupCohesionRadiusRaw"/>, because there is no
    /// candidate to evaluate. The order of this list never affects the
    /// result: union is applied pair by pair, gated the same way regardless
    /// of order, and set union is commutative and associative, so permuting
    /// <paramref name="pairs"/> permutes nothing in
    /// <paramref name="results"/>.
    /// </param>
    /// <param name="results">
    /// Receives one result per entry of <paramref name="entityIds"/>, in the
    /// same order. Must be exactly <paramref name="entityIds"/>.Length long.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pairs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="isAlive"/>, <paramref name="factions"/>,
    /// <paramref name="xRaw"/>, <paramref name="yRaw"/>, or
    /// <paramref name="results"/> has a different length than
    /// <paramref name="entityIds"/>; <paramref name="entityIds"/> is not
    /// strictly ascending; or a pair in <paramref name="pairs"/> names an
    /// entity ID absent from <paramref name="entityIds"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupCohesionRadiusRaw"/> is negative.</exception>
    internal static void Compute(
        ReadOnlySpan<ulong> entityIds,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> factions,
        ReadOnlySpan<int> xRaw,
        ReadOnlySpan<int> yRaw,
        int groupCohesionRadiusRaw,
        IReadOnlyList<SandataCollisionPair> pairs,
        Span<SquadSlot> results)
    {
        var count = entityIds.Length;

        if (xRaw.Length != count)
        {
            throw new ArgumentException("xRaw must have the same length as entityIds.", nameof(xRaw));
        }

        if (yRaw.Length != count)
        {
            throw new ArgumentException("yRaw must have the same length as entityIds.", nameof(yRaw));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(groupCohesionRadiusRaw);

        ComputeCore(
            entityIds, isAlive, factions, pairs, results,
            xRaw, yRaw, groupCohesionRadiusRaw, applyCohesionRadius: true);
    }

    /// <summary>
    /// The shared derivation both <see cref="Compute(ReadOnlySpan{ulong}, ReadOnlySpan{bool}, ReadOnlySpan{int}, IReadOnlyList{SandataCollisionPair}, Span{SquadSlot})"/>
    /// and its position-aware overload run. <paramref name="applyCohesionRadius"/>
    /// selects which of those two callers this is: when
    /// <see langword="false"/>, <paramref name="xRaw"/> and
    /// <paramref name="yRaw"/> are ignored entirely and every same-faction
    /// candidate pair unions unconditionally, reproducing this type's
    /// pre-task-74 behaviour for the caller that has no positions to give.
    /// </summary>
    private static void ComputeCore(
        ReadOnlySpan<ulong> entityIds,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> factions,
        IReadOnlyList<SandataCollisionPair> pairs,
        Span<SquadSlot> results,
        ReadOnlySpan<int> xRaw,
        ReadOnlySpan<int> yRaw,
        int groupCohesionRadiusRaw,
        bool applyCohesionRadius)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        var count = entityIds.Length;

        if (isAlive.Length != count)
        {
            throw new ArgumentException("isAlive must have the same length as entityIds.", nameof(isAlive));
        }

        if (factions.Length != count)
        {
            throw new ArgumentException("factions must have the same length as entityIds.", nameof(factions));
        }

        if (results.Length != count)
        {
            throw new ArgumentException("results must have the same length as entityIds.", nameof(results));
        }

        ValidateStrictlyAscending(entityIds);

        var unionFind = new UnionFind(count);

        // Squared once, outside the pair loop: comparing squared distance to
        // squared radius avoids a square root entirely, keeping the test
        // integer-only with no floating point and no epsilon. Widened to
        // long so a large radius or a large map coordinate cannot overflow
        // the multiply, the same guard SandataCollisionGrid.SquaredDistance
        // and ContactSquaredDistance already apply to the same shape of
        // comparison.
        var squaredCohesionRadius = applyCohesionRadius
            ? checked((long)groupCohesionRadiusRaw * groupCohesionRadiusRaw)
            : 0L;

        for (var pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
        {
            var pair = pairs[pairIndex];
            var lowIndex = IndexOf(entityIds, pair.LowEntityId);
            var highIndex = IndexOf(entityIds, pair.HighEntityId);

            if (lowIndex < 0 || highIndex < 0)
            {
                throw new ArgumentException(
                    $"Pair ({pair.LowEntityId}, {pair.HighEntityId}) names an entity ID absent from entityIds.",
                    nameof(pairs));
            }

            // Two operators of the same faction within cohesion range are
            // unioned into one squad; a candidate pair between opposing
            // factions never is, regardless of how close they stand.
            if (factions[lowIndex] != factions[highIndex])
            {
                continue;
            }

            // Inclusive comparison: exact tangency at the radius is a union,
            // matching SandataCollisionGrid.IsContact's own inclusive rule
            // for the collision broad phase this candidate list is drawn
            // from. When applyCohesionRadius is false, this is always true —
            // the pre-task-74 unconditional union this type performed before
            // any caller had positions to give it.
            var withinCohesionRadius = !applyCohesionRadius ||
                SandataCollisionGrid.SquaredDistance(xRaw[lowIndex], yRaw[lowIndex], xRaw[highIndex], yRaw[highIndex])
                    <= squaredCohesionRadius;

            if (withinCohesionRadius)
            {
                unionFind.Union(lowIndex, highIndex);
            }
        }

        // First pass, in ascending entity-ID order: for each component, the
        // first entity ID reached is its lowest (hence its group identity),
        // and the first living entity ID reached is its lowest living member
        // (hence its leader). This is the only place either quantity is
        // decided, and it depends only on entityIds and isAlive — never on
        // the order pairs arrived in.
        var groupIdOfRoot = new ulong[count];
        var hasGroupIdForRoot = new bool[count];
        var leaderOfRoot = new ulong[count];
        var hasLeaderForRoot = new bool[count];

        for (var index = 0; index < count; index++)
        {
            var root = unionFind.Find(index);

            if (!hasGroupIdForRoot[root])
            {
                groupIdOfRoot[root] = entityIds[index];
                hasGroupIdForRoot[root] = true;
            }

            if (isAlive[index] && !hasLeaderForRoot[root])
            {
                leaderOfRoot[root] = entityIds[index];
                hasLeaderForRoot[root] = true;
            }
        }

        // Second pass, still in ascending entity-ID order: slot indices are
        // assigned by counting only the living members of each component as
        // it is revisited, so a dead member never opens a gap in the
        // sequence the living members occupy.
        var livingSlotCountOfRoot = new int[count];

        for (var index = 0; index < count; index++)
        {
            var root = unionFind.Find(index);

            int? slotIndex = null;

            if (isAlive[index])
            {
                slotIndex = livingSlotCountOfRoot[root];
                livingSlotCountOfRoot[root]++;
            }

            var leaderEntityId = hasLeaderForRoot[root]
                ? leaderOfRoot[root]
                : (ulong?)null;

            results[index] = new SquadSlot(entityIds[index], groupIdOfRoot[root], leaderEntityId, slotIndex);
        }
    }

    private static void ValidateStrictlyAscending(ReadOnlySpan<ulong> entityIds)
    {
        for (var index = 1; index < entityIds.Length; index++)
        {
            if (entityIds[index] <= entityIds[index - 1])
            {
                throw new ArgumentException(
                    "entityIds must be strictly ascending with no duplicate.",
                    nameof(entityIds));
            }
        }
    }

    /// <summary>
    /// Locates <paramref name="entityId"/> in <paramref name="sortedEntityIds"/>
    /// by binary search over the flat, ascending-sorted span, or returns a
    /// negative value when it is absent. No dictionary and no hash set: the
    /// roster is already sorted, so a binary search is the flat-array
    /// equivalent of a keyed lookup.
    /// </summary>
    private static int IndexOf(ReadOnlySpan<ulong> sortedEntityIds, ulong entityId) =>
        sortedEntityIds.BinarySearch(entityId);
}
