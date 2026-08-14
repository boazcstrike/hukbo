namespace Sandata.Core.Sensing;

/// <summary>
/// Design section 4's per-faction alert state: "one of Calm, Raised, Breach."
/// Backs <c>Sandata.Core.Simulation.FactionAlertState.AlertLevel</c>'s raw
/// <see langword="int"/> field. That type's own remarks record the numbering
/// this design line implies by its own listed order — <c>0</c> is
/// <see cref="Calm"/>, <c>1</c> is <see cref="Raised"/>, <c>2</c> is
/// <see cref="Breach"/> — and say plainly that confirming that numbering is
/// task 35's job, not <c>FactionAlertState</c>'s to enforce. This enum is
/// that confirmation.
/// </summary>
/// <remarks>
/// <para>
/// Design section 11 records why this is three roles rather than a boolean:
/// "the simulation carries three alert levels," and every alert transition
/// must convey through shape as well as colour in the HUD, never colour
/// alone.
/// </para>
/// <para>
/// This type declares the three named states only. Which events raise or
/// lower a faction's alert level — a contact reaching
/// <see cref="ContactTier.QuestionMark"/> or <see cref="ContactTier.Identified"/>,
/// a heard sound, a death scream pulling in investigators — is deliberately
/// left to whichever future task wires alert into the tick pipeline. No task
/// in Sandata's scaffold plan's table names a numeric
/// trigger or a decay rule for any transition among these three states, and
/// this task's own remit is the sensing primitives — contact tiers, ghosts,
/// and hearing radii — not the alert state machine those primitives will
/// eventually feed.
/// </para>
/// </remarks>
public enum AlertLevel
{
    /// <summary>No known hostile contact or hostile sound. The default state.</summary>
    Calm = 0,

    /// <summary>
    /// Something has been sensed — a contact at
    /// <see cref="ContactTier.QuestionMark"/> or better, or a heard sound —
    /// but nothing has yet escalated to open engagement.
    /// </summary>
    Raised = 1,

    /// <summary>
    /// Open engagement: gunfire, a death, or an equivalent unambiguous sign
    /// of combat has been sensed.
    /// </summary>
    Breach = 2,
}
