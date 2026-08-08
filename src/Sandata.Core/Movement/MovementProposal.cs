namespace Sandata.Core.Movement;

/// <summary>
/// One entity's proposed move for a tick, computed by stage 9 of the tick
/// pipeline (design section 5) from the shared group polyline and that
/// entity's own slot arclength offset (plan task 34's <c>SlotTargets</c>).
/// <see cref="GroupId"/> and <see cref="SlotIndex"/> are the first two keys of
/// the commit order design section 8's "Local avoidance: propose, prioritise,
/// commit" names — <c>(groupId, slotIndex, entityId)</c> — carried on the
/// proposal itself so that <see cref="LocalAvoidance"/> never has to look
/// them up anywhere else while prioritising.
/// </summary>
/// <remarks>
/// <para>
/// This is stage 9's write-only output. Design section 5 states the rule this
/// type exists to satisfy: "Nothing between stages 5 and 9 may write
/// authoritative state that another unit in the same stage range then reads.
/// Proposals accumulate into a write-only buffer that stage 10 consumes."
/// Every <see cref="MovementProposal"/> is computed only from the frozen
/// tick-start view and the proposing entity's own slot — never from another
/// entity's proposal in the same tick — so a caller that builds a list of
/// these for the whole roster and hands that list to
/// <see cref="LocalAvoidance.Commit"/> has already produced the "buffer": no
/// separate accumulator type is needed, because a plain
/// <see cref="IReadOnlyList{T}"/> of independently-computed values already
/// has the property the design names. This mirrors
/// <c>Sandata.Core.Collision.SandataCollisionMoveRequest</c>'s own documented
/// contract on its caller — the type itself enforces nothing further about
/// how it was produced; that is a contract on the caller, exactly as it is
/// for the Hukbo original the design was written against.
/// </para>
/// <para>
/// <see cref="StartXRaw"/> and <see cref="StartYRaw"/> are always the
/// position committed at the end of the previous tick, never a position
/// another proposal in the same buffer might commit to this tick — which is
/// exactly the frozen-view discipline stage 9 is bound by.
/// </para>
/// </remarks>
internal readonly record struct MovementProposal(
    ulong EntityId,
    int StartXRaw,
    int StartYRaw,
    int DesiredXRaw,
    int DesiredYRaw,
    ulong GroupId,
    int SlotIndex);
