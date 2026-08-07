namespace Sandata.Core.Simulation;

/// <summary>
/// Explains, for one operator, why <see cref="IntentSelection.Select"/>
/// returned the <see cref="OperatorIntent"/> it returned this tick — plan
/// task 44's own words: "Every selected intent carries an inspectable reason
/// code so the operator inspector can explain a held position." Design
/// section 11's HUD list already names the operator inspector showing
/// "intent, reason code" as a built element; this enum is the reason-code
/// half of that pair, the same role <see cref="Navigation.PathReasonCode"/>
/// plays for a group's path.
/// </summary>
/// <remarks>
/// <para>
/// One member per outcome <see cref="IntentSelection.Select"/> can actually
/// return — a one-to-one pairing with <see cref="OperatorIntent"/>'s six
/// members — plus <see cref="Unspecified"/>, a zero sentinel
/// <see cref="IntentSelection.Select"/> never returns. Every real outcome
/// therefore carries a non-zero, and so non-default, reason code, matching
/// <see cref="Sensing.ContactTier.Unknown"/>'s own "never actually
/// constructed into a stored, returned value" pattern for a zero sentinel.
/// </para>
/// <para>
/// <b>Append-only</b>, matching every other Sandata enum's rule. A member's
/// numeric value never changes once it has shipped, a member is never
/// reordered, and a retired value is never reused for a different reason.
/// </para>
/// </remarks>
public enum IntentReasonCode
{
    /// <summary>Sentinel. Never returned by <see cref="IntentSelection.Select"/>.</summary>
    Unspecified = 0,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Dead"/>: <see cref="IntentSelectionInput.Health"/>
    /// was at or below zero this tick.
    /// </summary>
    OperatorIsDead = 1,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Reposition"/>:
    /// <see cref="IntentSelectionInput.SuppressionCounter"/> reached
    /// <see cref="IntentSelection.SuppressionRepositionThreshold"/>.
    /// </summary>
    RepositioningUnderSuppression = 2,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Engage"/>:
    /// <see cref="IntentSelectionInput.BestContactTier"/> was
    /// <see cref="Sensing.ContactTier.Identified"/> this tick.
    /// </summary>
    IdentifiedHostileContact = 3,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Breach"/>:
    /// <see cref="IntentSelectionInput.IsAtBreachPoint"/> was
    /// <see langword="true"/> this tick.
    /// </summary>
    AtBreachPoint = 4,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Advance"/>:
    /// <see cref="IntentSelectionInput.PathReasonCode"/> was
    /// <see cref="Navigation.PathReasonCode.PathValid"/> this tick.
    /// </summary>
    FollowingPublishedPath = 5,

    /// <summary>
    /// Backs <see cref="OperatorIntent.Hold"/>: none of the five conditions
    /// above applied, so the operator holds its current position.
    /// </summary>
    HoldingPosition = 6,
}
