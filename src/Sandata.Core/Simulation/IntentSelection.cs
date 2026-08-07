using System.Collections.Immutable;
using Sandata.Core.Navigation;
using Sandata.Core.Sensing;

namespace Sandata.Core.Simulation;

/// <summary>
/// One operator's inputs to <see cref="IntentSelection.Select"/>, taken as
/// plain parameters standing in for the frozen tick-start view design
/// section 5 stage 8 reads. Plan task 44's own instruction: "No frozen
/// tick-start view type exists yet — task 49 builds it. Take the inputs as
/// parameters." Every field here names a fact stage 8 needs that an earlier
/// stage in the same tick (5 sensing, 6 squad grouping, or 7 the path
/// service) already computed against that same frozen view, so nothing here
/// is re-derived from raw geometry — this type only assembles what those
/// stages already produced into the shape stage 8 consumes.
/// </summary>
/// <param name="EntityId">
/// The operator this input describes, matching <c>OperatorState.EntityId</c>.
/// Carried through into <see cref="IntentSelectionResult.EntityId"/> so a
/// caller selecting many operators at once
/// (<see cref="IntentSelection.SelectAll"/>) can recover which result
/// belongs to which operator without relying on array position.
/// </param>
/// <param name="Health">
/// <c>OperatorState.Health</c> as of the start of this tick. A value at or
/// below zero selects <see cref="OperatorIntent.Dead"/> unconditionally —
/// see <see cref="IntentSelection.Select"/>'s remarks on evaluation order.
/// </param>
/// <param name="SuppressionCounter">
/// <c>OperatorState.SuppressionCounter</c> as of the start of this tick. A
/// value at or beyond
/// <see cref="IntentSelection.SuppressionRepositionThreshold"/> selects
/// <see cref="OperatorIntent.Reposition"/>.
/// </param>
/// <param name="BestContactTier">
/// The highest <see cref="ContactTier"/> among the operator's contact
/// memory this tick — stage 5's sensing output, per design section 5's
/// stage table row 5, "sensing: line of sight, vision cone, contact tier,
/// hearing." <see cref="ContactTier.Identified"/> selects
/// <see cref="OperatorIntent.Engage"/>; neither
/// <see cref="ContactTier.QuestionMark"/> nor <see cref="ContactTier.Unknown"/>
/// does, matching design section 4's "not shootable" language for a
/// question-mark contact.
/// </param>
/// <param name="IsAtBreachPoint">
/// Whether the operator currently occupies a breach point flagged for it
/// this tick. Selects <see cref="OperatorIntent.Breach"/>. What counts as a
/// breach point — a door tagged breachable in the map format (design
/// section 12), reached by an operator whose squad carries a
/// <see cref="Orders.OrderKind.Breach"/> order (design section 16) — is not
/// decided by this type; it is a caller-supplied fact, exactly like every
/// other field here, because neither the order layer nor the breach-point
/// derivation exists in this worktree yet.
/// </param>
/// <param name="PathReasonCode">
/// <see cref="Navigation.PathReasonCode"/> for the operator's group, from
/// stage 7's <c>PathService.GetReasonCode</c>.
/// <see cref="Navigation.PathReasonCode.PathValid"/> selects
/// <see cref="OperatorIntent.Advance"/>; every other reason code — no
/// destination requested, awaiting latency, or unreachable — falls through
/// toward <see cref="OperatorIntent.Hold"/>.
/// </param>
public readonly record struct IntentSelectionInput(
    ulong EntityId,
    int Health,
    int SuppressionCounter,
    ContactTier BestContactTier,
    bool IsAtBreachPoint,
    PathReasonCode PathReasonCode);

/// <summary>
/// One operator's selected intent and the reason code that explains it —
/// the pair the operator inspector shows, per design section 11's HUD
/// element list.
/// </summary>
public readonly record struct IntentSelectionResult(
    ulong EntityId,
    OperatorIntent Intent,
    IntentReasonCode ReasonCode);

/// <summary>
/// Design section 5 stage 8, "Select intent: hold, advance, breach, engage,
/// reposition, dead," read against the frozen tick-start view. Plan task
/// 44's row: "Every selected intent carries an inspectable reason code so
/// the operator inspector can explain a held position."
/// </summary>
/// <remarks>
/// <para>
/// <b>Evaluation order is a fixed, total cascade — first match wins.</b> The
/// six conditions below are not mutually exclusive by construction (an
/// operator can be both dead and, if that fact were ignored, also holding an
/// identified contact), so <see cref="Select"/> tests them in one pinned
/// order and returns on the first match:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>Health &lt;= 0</c> → <see cref="OperatorIntent.Dead"/>. Checked first
/// and unconditionally, so it overrides every other input exactly as plan
/// task 44 requires: "Dead is selected for a dead operator regardless of
/// every other input."
/// </description></item>
/// <item><description>
/// <c>SuppressionCounter &gt;= SuppressionRepositionThreshold</c> →
/// <see cref="OperatorIntent.Reposition"/>.
/// </description></item>
/// <item><description>
/// <c>BestContactTier == ContactTier.Identified</c> →
/// <see cref="OperatorIntent.Engage"/>.
/// </description></item>
/// <item><description>
/// <c>IsAtBreachPoint</c> → <see cref="OperatorIntent.Breach"/>.
/// </description></item>
/// <item><description>
/// <c>PathReasonCode == PathReasonCode.PathValid</c> →
/// <see cref="OperatorIntent.Advance"/>.
/// </description></item>
/// <item><description>
/// Otherwise → <see cref="OperatorIntent.Hold"/>.
/// </description></item>
/// </list>
/// <para>
/// <b>Why this order, and not another.</b> A dead operator's every other
/// field is meaningless, so death is checked before anything else is even
/// read. Suppression outranks engagement and breaching because an operator
/// pinned by fire is in no position to press a breach or hold a firing
/// position, so getting out of the suppressed arc is the more urgent fact.
/// Engagement outranks breaching and advancing because an identified,
/// shootable contact is the most urgent unresolved fact stage 8 can be
/// handed once suppression is ruled out. Breaching outranks advancing
/// because reaching a breach point is itself the terminal step of an
/// advance — once there, the operator's job changes rather than continuing
/// toward a point it has already reached.
/// </para>
/// <para>
/// <b>Order independence.</b> <see cref="Select"/> reads nothing but its own
/// <c>input</c> parameter — no static field, no shared collection, no other
/// operator's state — so one operator's result can never depend on another
/// operator's inputs, and never depends on the order operators are
/// processed in. <see cref="SelectAll"/> is nothing more than one
/// independent <see cref="Select"/> call per element, in the caller's own
/// order; it holds no state across elements and writes nothing any other
/// element's call could read. This is what makes stage 8 order-independent
/// by construction, per design section 5's rule that "nothing between
/// stages 5 and 9 may write authoritative state that another unit in the
/// same stage range then reads."
/// </para>
/// <para>
/// <b>No production caller yet.</b> Design section 5's frozen tick-start
/// view is built by task 49, which has not landed in this worktree. This
/// type is deliberately reachable and testable without it — task 49's tick
/// pipeline is expected to assemble one <see cref="IntentSelectionInput"/>
/// per living operator from the frozen view and call <see cref="SelectAll"/>
/// once per tick during stage 8.
/// </para>
/// </remarks>
public static class IntentSelection
{
    /// <summary>
    /// The <see cref="IntentSelectionInput.SuppressionCounter"/> value at
    /// which an operator's intent becomes <see cref="OperatorIntent.Reposition"/>
    /// rather than whatever it would otherwise have been. <b>PROVISIONAL
    /// reconstruction</b> — neither design section 4 (which authors
    /// <c>SuppressionCounter</c> as authoritative and hashed) nor design
    /// section 5's stage 8 row states a numeric threshold, and no other
    /// Sandata file computes or increments this counter yet. Chosen only to
    /// give this pure function a testable, self-consistent rule; whichever
    /// future task actually wires suppression into the tick pipeline — the
    /// system that increments <c>SuppressionCounter</c> in the first place —
    /// is expected to confirm or revise this value, in the same spirit as
    /// <see cref="Sensing.ContactMemory.IdentifyRangeWu"/> and
    /// <see cref="Sensing.ContactMemory.DetectRangeWu"/>.
    /// </summary>
    public const int SuppressionRepositionThreshold = 3;

    /// <summary>
    /// Selects one operator's intent and reason code from
    /// <paramref name="input"/>, per this type's remarks on the fixed
    /// evaluation cascade. Pure: allocates nothing beyond the returned
    /// value, reads no field but <paramref name="input"/>'s own, and always
    /// returns the same result for the same input.
    /// </summary>
    public static IntentSelectionResult Select(IntentSelectionInput input)
    {
        if (input.Health <= 0)
        {
            return new IntentSelectionResult(
                input.EntityId, OperatorIntent.Dead, IntentReasonCode.OperatorIsDead);
        }

        if (input.SuppressionCounter >= SuppressionRepositionThreshold)
        {
            return new IntentSelectionResult(
                input.EntityId, OperatorIntent.Reposition, IntentReasonCode.RepositioningUnderSuppression);
        }

        if (input.BestContactTier == ContactTier.Identified)
        {
            return new IntentSelectionResult(
                input.EntityId, OperatorIntent.Engage, IntentReasonCode.IdentifiedHostileContact);
        }

        if (input.IsAtBreachPoint)
        {
            return new IntentSelectionResult(
                input.EntityId, OperatorIntent.Breach, IntentReasonCode.AtBreachPoint);
        }

        if (input.PathReasonCode == PathReasonCode.PathValid)
        {
            return new IntentSelectionResult(
                input.EntityId, OperatorIntent.Advance, IntentReasonCode.FollowingPublishedPath);
        }

        return new IntentSelectionResult(
            input.EntityId, OperatorIntent.Hold, IntentReasonCode.HoldingPosition);
    }

    /// <summary>
    /// Selects every operator in <paramref name="inputs"/> independently, in
    /// the caller's own order — see this type's remarks on order
    /// independence. Not a batch optimisation over <see cref="Select"/>: this
    /// is exactly one <see cref="Select"/> call per element, made
    /// allocation-explicit into a single returned array for a caller that
    /// wants one result set per tick rather than driving the loop itself.
    /// </summary>
    public static ImmutableArray<IntentSelectionResult> SelectAll(ReadOnlySpan<IntentSelectionInput> inputs)
    {
        if (inputs.IsEmpty)
        {
            return ImmutableArray<IntentSelectionResult>.Empty;
        }

        var results = new IntentSelectionResult[inputs.Length];
        for (var i = 0; i < inputs.Length; i++)
        {
            results[i] = Select(inputs[i]);
        }

        return ImmutableArray.Create(results);
    }
}
