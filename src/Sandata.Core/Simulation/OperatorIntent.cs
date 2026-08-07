namespace Sandata.Core.Simulation;

/// <summary>
/// The six operator intents design section 5 stage 8 names, in the exact
/// order that row lists them: "Select intent: hold, advance, breach, engage,
/// reposition, dead." <see cref="IntentSelection.Select"/> is the pure
/// function that chooses one of these six from the frozen tick-start view
/// design section 5 says stage 8 reads, and this enum backs
/// <c>Sandata.Core.Simulation.OperatorState.Intent</c>'s raw
/// <see langword="int"/> field — that field's own remarks say this enum's
/// numbering is task 44's to declare, not that file's to enforce, and this is
/// that declaration.
/// </summary>
/// <remarks>
/// <b>Append-only</b>, matching every other Sandata enum's rule
/// (<see cref="Orders.OrderKind"/>, <see cref="Navigation.PathReasonCode"/>,
/// <see cref="Sensing.ContactTier"/>): a member's numeric value never changes
/// once it has shipped, a member is never reordered, and a retired value is
/// never reused for a different intent. <c>IntentSelectionTests</c> pins
/// every member's numeric value as a literal so an accidental renumbering
/// fails loudly rather than silently re-keying every recorded replay or
/// snapshot that carries an <c>OperatorState.Intent</c> value.
/// </remarks>
public enum OperatorIntent
{
    /// <summary>
    /// Hold the current position. The outcome when none of the other five
    /// conditions <see cref="IntentSelection.Select"/> checks applies — see
    /// that type's remarks for the full evaluation cascade.
    /// </summary>
    Hold = 0,

    /// <summary>
    /// Move along the operator's group's currently published path. Design
    /// section 5 stage 9 ("compute movement proposals from the shared
    /// polyline and the slot arclength offset") is the stage that turns this
    /// intent into an actual movement proposal.
    /// </summary>
    Advance = 1,

    /// <summary>
    /// At a breach point, preparing to force entry — design section 16's
    /// <c>Breach</c> order kind and design section 12's breachable wall faces
    /// are the two data sources a caller draws this fact from.
    /// </summary>
    Breach = 2,

    /// <summary>
    /// Engaging an identified, shootable hostile contact — design section
    /// 4's "not shootable" language for a lesser contact tier is exactly why
    /// this intent requires <see cref="Sensing.ContactTier.Identified"/> and
    /// not merely <see cref="Sensing.ContactTier.QuestionMark"/>.
    /// </summary>
    Engage = 3,

    /// <summary>
    /// Moving to break an ongoing suppression rather than holding in the
    /// open under fire.
    /// </summary>
    Reposition = 4,

    /// <summary>
    /// The operator's health has reached zero this tick. Overrides every
    /// other input <see cref="IntentSelection.Select"/> accepts — plan task
    /// 44's own acceptance criterion: "Dead is selected for a dead operator
    /// regardless of every other input."
    /// </summary>
    Dead = 5,
}
