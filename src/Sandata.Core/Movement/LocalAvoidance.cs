using Sandata.Core.Collision;

namespace Sandata.Core.Movement;

/// <summary>
/// Stage 10 of the tick pipeline (design section 5): commits a tick's
/// <see cref="MovementProposal"/> buffer against the collision grid, in the
/// exact three steps design section 8's "Local avoidance: propose,
/// prioritise, commit" names.
/// </summary>
/// <remarks>
/// <para>
/// <b>Propose</b> already happened before <see cref="Commit"/> is ever called
/// — it is stage 9, and its output is the <see cref="MovementProposal"/> list
/// the caller passes in. <see cref="Commit"/> only performs the remaining two
/// steps, <b>prioritise</b> and <b>commit</b>, and it performs both of them by
/// delegating straight to <see cref="SandataCollisionResolver"/>, plan task
/// 16's resolver, rather than re-implementing a second prioritised commit
/// walk: that resolver already sorts every request into the exact
/// <c>(groupId, slotIndex, entityId)</c> total order design section 8 names,
/// and already commits sequentially against its own grid with no force, no
/// impulse, and no push-apart — see its own remarks. Reusing it here means
/// this file adds exactly one new fact to the pipeline — the one sidestep
/// retry design section 8 adds beyond plain collision resolution — instead of
/// duplicating the broad-phase grid and the insertion-sort prioritiser a
/// second time.
/// </para>
/// <para>
/// <b>The sidestep retry is two resolver passes, not a third algorithm.</b>
/// <see cref="Commit"/> calls <see cref="SandataCollisionResolver.Resolve"/>
/// once to find out, for the whole buffer at once and in the correct
/// priority order, which proposals the plain collision rule blocks. For
/// exactly those blocked proposals — and no others — it builds one
/// alternative request whose desired position is
/// <see cref="SidestepRules.Sidestep"/>'s rotated version of the entity's own
/// already-proposed displacement, added back onto its start position, and
/// calls <see cref="SandataCollisionResolver.Resolve"/> a second time on the
/// full buffer with that substitution applied. The second pass's result list
/// is what stage 10 actually commits. A proposal the first pass did not block
/// carries its original desired position into the second pass unchanged, so
/// the second pass reproduces the first pass's answer for it exactly, other
/// than whatever the newly-relocated sidestepping entities changed about the
/// grid around it — which is the same "committed state can depend on who
/// already committed" rule the resolver already documents for its own single
/// pass.
/// </para>
/// <para>
/// <b>Why this stays "try exactly one sidestep, then wait a tick" and never a
/// cascade.</b> A proposal that the plain (first-pass) rule did not block
/// never receives a sidestep candidate, even if the second pass's grid state
/// would have blocked its original desired position — that would be a second
/// avoidance attempt layered on a proposal design section 8 never called
/// blocked in the first place. And a proposal whose sidestep candidate the
/// second pass also cannot commit is not retried again within this tick: it
/// commits at whatever the second pass settles on for it — its own start
/// position, by <see cref="SandataCollisionResolver"/>'s own blocked-holds-
/// still rule — which is exactly "waits a tick", since the entity's own next
/// stage-9 proposal next tick will again compute a fresh desired position
/// from that same, unmoved start. There is no loop and no retry counter
/// anywhere in this file for that reason: one substitution, one more resolver
/// pass, done.
/// </para>
/// <para>
/// <b>Never a force, an impulse, or a push-apart.</b> Every position this
/// type ever proposes to the resolver — the original desired position, and
/// the sidestepped one — is a full, independently computed candidate point,
/// never a fraction of some other unit's position blended in. The resolver
/// then either accepts a candidate exactly or rejects it outright to a
/// pre-declared fallback (hold at start, or the resolver's own fixed
/// east/west/north/south separation repair for exact coincidence); nothing in
/// either resolver pass, or in the substitution this type performs between
/// them, ever sums two or more entities' direction vectors into an
/// accumulator that a committed position is then read back from — which is
/// the property design section 8 and <c>CLAUDE.md</c> section 9 actually ban,
/// not the word "force" or "velocity" as a token.
/// </para>
/// </remarks>
internal sealed class LocalAvoidance
{
    private readonly SandataCollisionResolver _resolver;
    private readonly List<SandataCollisionMoveRequest> _firstPassRequests = [];
    private readonly List<SandataCollisionMoveRequest> _secondPassRequests = [];

    /// <param name="cellSizeRaw">The edge length of one square broad-phase cell in raw fixed-point units, forwarded to the resolver's own grid.</param>
    /// <param name="bodyRadiusRaw">The common body radius in raw fixed-point units, forwarded to the resolver.</param>
    internal LocalAvoidance(int cellSizeRaw, int bodyRadiusRaw)
    {
        _resolver = new SandataCollisionResolver(cellSizeRaw, bodyRadiusRaw);
    }

    /// <summary>
    /// Prioritises and commits <paramref name="proposals"/> — stage 9's
    /// write-only output for the whole roster — giving every proposal the
    /// plain collision rule blocks exactly one <see cref="SidestepRules"/>
    /// retry before it is allowed to fall back to holding its start position.
    /// </summary>
    /// <returns>
    /// One committed result per proposal, in the resolver's own priority
    /// commit order (ascending <c>(GroupId, SlotIndex, EntityId)</c>) — not
    /// necessarily <paramref name="proposals"/>'s own order, matching
    /// <see cref="SandataCollisionResolver.Resolve"/>'s documented contract,
    /// which this method's second pass returns directly.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="proposals"/> is <see langword="null"/>.</exception>
    internal IReadOnlyList<SandataCollisionMoveResult> Commit(IReadOnlyList<MovementProposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);

        var count = proposals.Count;

        _firstPassRequests.Clear();
        for (var index = 0; index < count; index++)
        {
            var proposal = proposals[index];
            _firstPassRequests.Add(new SandataCollisionMoveRequest(
                proposal.EntityId,
                proposal.StartXRaw,
                proposal.StartYRaw,
                proposal.DesiredXRaw,
                proposal.DesiredYRaw,
                proposal.GroupId,
                proposal.SlotIndex));
        }

        var firstPassResults = _resolver.Resolve(_firstPassRequests);

        _secondPassRequests.Clear();
        for (var index = 0; index < count; index++)
        {
            var proposal = proposals[index];
            var wasBlocked = FindResolution(firstPassResults, proposal.EntityId) == SandataMovementResolution.Blocked;

            if (!wasBlocked)
            {
                _secondPassRequests.Add(_firstPassRequests[index]);
                continue;
            }

            var deltaXRaw = proposal.DesiredXRaw - proposal.StartXRaw;
            var deltaYRaw = proposal.DesiredYRaw - proposal.StartYRaw;
            var sidestep = SidestepRules.Sidestep(deltaXRaw, deltaYRaw, proposal.EntityId);

            _secondPassRequests.Add(new SandataCollisionMoveRequest(
                proposal.EntityId,
                proposal.StartXRaw,
                proposal.StartYRaw,
                proposal.StartXRaw + sidestep.DeltaXRaw,
                proposal.StartYRaw + sidestep.DeltaYRaw,
                proposal.GroupId,
                proposal.SlotIndex));
        }

        return _resolver.Resolve(_secondPassRequests);
    }

    /// <summary>
    /// Linear scan for the settled resolution of <paramref name="entityId"/>
    /// within one resolver pass's result list. Not a
    /// <c>Dictionary&lt;</c> lookup — that collection type is banned from
    /// <c>Sandata.Core</c> by the determinism contract (plan standing rule
    /// 3) — and a plain scan is the right trade here regardless: the buffer
    /// this type ever commits is one indoor operator squad, never a
    /// battlefield roster, so an O(n) search per proposal costs nothing an
    /// insertion sort's own O(n log n) does not already spend.
    /// </summary>
    private static SandataMovementResolution FindResolution(
        IReadOnlyList<SandataCollisionMoveResult> results, ulong entityId)
    {
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].EntityId == entityId)
            {
                return results[index].Resolution;
            }
        }

        throw new InvalidOperationException(
            $"Entity {entityId} has no first-pass resolution; every proposed entity must appear exactly once in the resolver's result list.");
    }
}
