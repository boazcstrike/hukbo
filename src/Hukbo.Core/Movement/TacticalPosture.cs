namespace Hukbo.Core.Movement;

/// <summary>
/// One contingent's tactical stance under a movement preset whose
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
/// <see langword="true"/>, resolved once per contingent per tick from the
/// global living faction totals, the role-coverage flags, and the
/// contingent's own tick-start <see cref="Simulation.ContingentState"/>
/// (weapon-relative movement design, section 8). Written to every living
/// member's <c>AgentState.TacticalPosture</c>; every other preset leaves the
/// field at <see cref="None"/> forever. Posture steers routes and footwork
/// phases downstream — it never changes physical speed, never adds virtual
/// warriors, and never writes <see cref="Simulation.ContingentState.Break"/>
/// or the last-stand intent. Numeric values are part of the deterministic
/// replay and state-hash contract; the enum is append-only — never renumber,
/// reorder, or remove a member.
/// </summary>
public enum TacticalPosture : byte
{
    /// <summary>
    /// No resolved posture: the contingent has no living member, or the
    /// movement preset does not resolve postures at all.
    /// </summary>
    None = 0,

    /// <summary>
    /// Numbers or role coverage favour this side; close on the enemy.
    /// </summary>
    Advance = 1,

    /// <summary>
    /// A contested world with no clear advantage either way; keep station.
    /// </summary>
    Hold = 2,

    /// <summary>
    /// Meaningful pressure — at least exact four-to-three outnumbering, or
    /// an even fight with weaker role coverage; give ground deliberately.
    /// </summary>
    Yield = 3,

    /// <summary>
    /// The contingent's own tick-start state is
    /// <see cref="Simulation.ContingentState.Hold"/>: gather on the leader
    /// before anything else.
    /// </summary>
    Regroup = 4,

    /// <summary>No living enemy exists; run the survivors down.</summary>
    Pursue = 5,

    /// <summary>
    /// Outnumbered at least exactly two to one; break contact.
    /// </summary>
    Withdraw = 6,
}
