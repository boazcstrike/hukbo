using System.Collections.Immutable;

namespace Sandata.Core.Orders;

/// <summary>
/// Pure evaluation of design section 16's <see cref="OrderKind.Sync"/>
/// primitive: "Sync pace-matches a set of operators. Each member that
/// reaches its polyline's final node holds there. When every living member
/// of the set is holding, all of them release on the same tick." Design
/// section 5's fourteen-stage table places this evaluation in stage 8,
/// against the frozen tick-start view: "The evaluation runs in stage 8
/// against the frozen tick-start view, so it is order-independent by
/// construction, and the set is keyed by its lowest entity id so that two
/// sets releasing on the same tick have a total order between them."
/// </summary>
/// <remarks>
/// <para>
/// <b>No frozen tick-start view type exists yet.</b> Task 49's tick
/// pipeline, which would freeze the view stage 3 produces and hand this
/// type its per-operator facts, has not been built in this worktree. Every
/// member of this type is therefore a pure function of caller-supplied
/// parameters rather than of a concrete view type, and — as the task brief
/// for this file states — is expected to have no production caller yet.
/// A future caller derives <see cref="MemberFacts"/> for one
/// <see cref="OrderKind.Sync"/> order's <see cref="Order.Addressees"/> by
/// reading that tick's frozen view once it exists; this file does not need
/// to know how.
/// </para>
/// <para>
/// <b>Why the set is not derived from an <see cref="Order"/> directly.</b>
/// <see cref="Order.Addressees"/> names which operators belong to a sync
/// set, but whether each one is alive and holding is a fact about the
/// current tick, not about the order that created the set. Coupling
/// <see cref="Evaluate"/> to <see cref="Order"/> would force it to also
/// accept the per-tick liveness and holding facts, keyed by entity id — and
/// keying by entity id inside this file would need a
/// <c>Dictionary&lt;</c>, which <c>SandataSourceHygieneTests</c> bans from
/// <c>Sandata.Core</c>. Passing one flat <see cref="MemberFacts"/> span
/// instead needs no map: <see cref="Evaluate"/> is a single linear pass.
/// </para>
/// </remarks>
public static class SyncRules
{
    /// <summary>
    /// One sync-set member's tick-start facts: which operator, whether it is
    /// alive, and whether it is currently holding at its polyline's final
    /// node. A future caller builds one of these per
    /// <see cref="Order.Addressees"/> entry of a <see cref="OrderKind.Sync"/>
    /// order from that tick's frozen view.
    /// </summary>
    /// <param name="EntityId">The operator this fact set describes.</param>
    /// <param name="IsAlive">
    /// Whether the operator is alive at this tick's start. A dead member
    /// never blocks the set — design section 16 does not use the word
    /// "living" loosely: "When every <b>living</b> member of the set is
    /// holding, all of them release." <see cref="Evaluate"/> excludes a
    /// dead member from the holding check entirely rather than treating a
    /// dead member as vacuously holding, so a member killed before it ever
    /// reaches its final node still lets the rest of the set release once
    /// they are all holding.
    /// </param>
    /// <param name="IsHolding">
    /// Whether the operator has reached its polyline's final node and is
    /// holding there. Meaningless when <paramref name="IsAlive"/> is
    /// <see langword="false"/> and never read in that case.
    /// </param>
    public readonly record struct MemberFacts(ulong EntityId, bool IsAlive, bool IsHolding);

    /// <summary>
    /// One sync set's evaluation result for one tick: the key design
    /// section 16 uses to order simultaneous releases, and whether the set
    /// releases this tick.
    /// </summary>
    /// <param name="SetKey">
    /// "The set is keyed by its lowest entity id" (design section 16) — the
    /// minimum <see cref="MemberFacts.EntityId"/> across every member
    /// <see cref="Evaluate"/> was given, alive or dead. Computed as a
    /// <see langword="min"/> over every member rather than read from
    /// position zero of the input span, so a caller that has not sorted its
    /// span still gets the correct key; this is also what makes
    /// <see cref="Evaluate"/> permutation-invariant on the key, not merely
    /// on <see cref="Releases"/>.
    /// </param>
    /// <param name="Releases">
    /// <see langword="true"/> when every living member is holding this tick
    /// (and at least one member is alive — see <see cref="Evaluate"/>'s own
    /// remarks for the all-dead case); <see langword="false"/> otherwise.
    /// </param>
    public readonly record struct SyncEvaluation(ulong SetKey, bool Releases);

    /// <summary>
    /// Evaluates one sync set for one tick: design section 16's whole rule,
    /// "When every living member of the set is holding, all of them release
    /// on the same tick."
    /// </summary>
    /// <param name="members">
    /// Every member of the set, in any order — see this method's remarks on
    /// permutation invariance. Must contain at least one entry; a
    /// <see cref="OrderKind.Sync"/> order always addresses at least one
    /// operator (design section 16 names no lower bound on a sync set's
    /// size, but a set naming nobody is not a set this rule can key or
    /// evaluate).
    /// </param>
    /// <returns>
    /// The set's <see cref="SyncEvaluation.SetKey"/> and whether it releases
    /// this tick.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="members"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// <b>Permutation invariance.</b> This method makes one linear pass over
    /// <paramref name="members"/>, folding a running minimum
    /// (<see cref="SyncEvaluation.SetKey"/>) and a running logical AND over
    /// living members' <see cref="MemberFacts.IsHolding"/>
    /// (<see cref="SyncEvaluation.Releases"/>). Both <c>min</c> and logical
    /// AND are commutative and associative, so the fold's result does not
    /// depend on the order <paramref name="members"/> arrives in — this is
    /// what design section 16 means by "order-independent by construction,"
    /// applied one level below the tick pipeline's own order-independence:
    /// not just "the tick's outcome does not depend on which unit evaluates
    /// first," but "this one set's evaluation does not depend on which
    /// member is checked first."
    /// </para>
    /// <para>
    /// <b>All-dead behaviour, decided here.</b> Design section 16 states the
    /// releasing rule only for a set with at least one living member; it
    /// says nothing about a set every member of which has died. This method
    /// decides: <b>an all-dead set never releases.</b> "Releases" is
    /// otherwise a statement of fact — the moment every living member is
    /// holding — and a set with no living member has no member for whom a
    /// release could have any observable effect (a dead operator does not
    /// resume walking). The alternative, vacuous truth over an empty
    /// condition (the classical "every element of the empty set satisfies
    /// the predicate," which would make an all-dead set release on the very
    /// first tick it is evaluated and on every tick afterward), was
    /// rejected specifically because it produces a spuriously repeating
    /// "true" fact for a set nothing can act on, whereas "never releases" is
    /// a stable, quiet, and — because it is tested here — an inspectable
    /// answer rather than an accidental one.
    /// </para>
    /// </remarks>
    public static SyncEvaluation Evaluate(ReadOnlySpan<MemberFacts> members)
    {
        if (members.IsEmpty)
        {
            throw new ArgumentException("A sync set must name at least one member.", nameof(members));
        }

        var setKey = ulong.MaxValue;
        var hasLivingMember = false;
        var everyLivingMemberIsHolding = true;

        foreach (var member in members)
        {
            if (member.EntityId < setKey)
            {
                setKey = member.EntityId;
            }

            if (member.IsAlive)
            {
                hasLivingMember = true;

                if (!member.IsHolding)
                {
                    everyLivingMemberIsHolding = false;
                }
            }
        }

        var releases = hasLivingMember && everyLivingMemberIsHolding;
        return new SyncEvaluation(setKey, releases);
    }

    /// <summary>
    /// Design section 16's total-order guarantee across sets: "two sets
    /// releasing on the same tick have a total order between them," keyed by
    /// <see cref="SyncEvaluation.SetKey"/>. Filters
    /// <paramref name="evaluations"/> down to the ones that release this
    /// tick and returns them ascending by
    /// <see cref="SyncEvaluation.SetKey"/> — a non-releasing set contributes
    /// nothing to an ordering of releases.
    /// </summary>
    /// <param name="evaluations">
    /// One <see cref="SyncEvaluation"/> per sync set evaluated this tick, in
    /// any order.
    /// </param>
    /// <returns>
    /// Every releasing evaluation from <paramref name="evaluations"/>,
    /// ascending by <see cref="SyncEvaluation.SetKey"/>.
    /// </returns>
    /// <remarks>
    /// Distinct sync sets evaluated on the same tick always carry distinct
    /// keys in practice: an operator can hold at most one active
    /// <see cref="OrderKind.Sync"/> assignment at a time (design section
    /// 16's single-assignment rule — "There is no third case and no blend of
    /// the two" — applies one level up, to which order an operator is
    /// following at all, and no task in this wave introduces a way for one
    /// operator to belong to two sync sets simultaneously), so the lowest
    /// entity id of one set cannot equal the lowest entity id of a different
    /// set unless the two sets share that member. This method still sorts
    /// with a plain ascending comparison rather than assuming that
    /// precondition, so a caller that ever violates it gets a deterministic
    /// — if unspecified in relative order between the tied entries — sort
    /// rather than a comparator that throws.
    /// </remarks>
    public static ImmutableArray<SyncEvaluation> OrderReleasingSets(ReadOnlySpan<SyncEvaluation> evaluations)
    {
        var builder = ImmutableArray.CreateBuilder<SyncEvaluation>();

        foreach (var evaluation in evaluations)
        {
            if (evaluation.Releases)
            {
                builder.Add(evaluation);
            }
        }

        builder.Sort(CompareBySetKey);

        return builder.ToImmutable();
    }

    private static int CompareBySetKey(SyncEvaluation left, SyncEvaluation right) =>
        left.SetKey.CompareTo(right.SetKey);
}
