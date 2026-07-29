namespace Hukbo.Core.Movement;

/// <summary>
/// One warrior's footwork lifecycle phase under a movement preset whose
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
/// <see langword="true"/>, resolved per living agent per tick by the ten
/// first-match transition steps of the weapon-relative movement design,
/// section 9, then finalised against lane clearance per section 9.4. Every
/// other preset leaves the field at <see cref="None"/> forever. Numeric
/// values are part of the deterministic replay and state-hash contract; the
/// enum is append-only — never renumber, reorder, or remove a member.
/// </summary>
public enum FootworkPhase : byte
{
    /// <summary>
    /// No resolved phase: the agent is dead, no target or posture demands
    /// motion, or the movement preset does not resolve phases at all.
    /// </summary>
    None = 0,

    /// <summary>A target exists beyond preferred distance; close in.</summary>
    Approach = 1,

    /// <summary>
    /// The selected target sits at or inside the offset-adjusted preferred
    /// distance; fight at this range.
    /// </summary>
    Engage = 2,

    /// <summary>
    /// An accepted attack binds the body: the committed pace and turn caps
    /// apply while <c>AgentState.FootworkTicksRemaining</c> runs down.
    /// </summary>
    Commit = 3,

    /// <summary>
    /// The timed reset after <see cref="Commit"/> expires, before ordinary
    /// footwork resumes.
    /// </summary>
    Recover = 4,

    /// <summary>
    /// Every route candidate of a provisional <see cref="Approach"/>,
    /// <see cref="Engage"/>, or <see cref="Pursue"/> failed lane clearance;
    /// hold position this tick.
    /// </summary>
    Refuse = 5,

    /// <summary>
    /// Local support ratios or a <see cref="TacticalPosture.Withdraw"/> /
    /// <see cref="TacticalPosture.Yield"/> posture demand breaking contact.
    /// </summary>
    Disengage = 6,

    /// <summary>
    /// Posture <see cref="TacticalPosture.Regroup"/>: move to gather on the
    /// leader.
    /// </summary>
    Regroup = 7,

    /// <summary>
    /// Posture <see cref="TacticalPosture.Pursue"/> with no target in hand;
    /// hunt the survivors.
    /// </summary>
    Pursue = 8,
}
