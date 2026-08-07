namespace Sandata.Core.Combat;

/// <summary>
/// The result design section 5 stage 13 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md produces once a tick's
/// damage has been fully accumulated and every death for that tick has been
/// resolved — <see cref="OutcomeRules.Resolve"/>'s return type.
/// </summary>
/// <remarks>
/// Numeric values deliberately mirror
/// <c>Hukbo.Core.Simulation.BattleOutcome</c>'s own elimination-outcome
/// enum: <see cref="Ongoing"/> at <c>0</c>, a one-sided victory next, and
/// <see cref="Draw"/> last, for the simultaneous-elimination case that
/// design section 5's "two mutual kills both land" makes reachable — a
/// tick's damage can eliminate both factions' last living operator at once.
/// This type is not itself part of the hashed mission state (see
/// <c>Simulation.MissionState.Winner</c>, an <see langword="int"/> per
/// design section 4, which a later wiring task maps this value onto); it
/// exists so <see cref="OutcomeRules.Resolve"/> has a self-describing return
/// type instead of a bare integer.
/// </remarks>
public enum MissionOutcome
{
    /// <summary>Both factions still have at least one living operator.</summary>
    Ongoing = 0,

    /// <summary>Faction 0 has a living operator and faction 1 does not.</summary>
    Faction0Victory = 1,

    /// <summary>Faction 1 has a living operator and faction 0 does not.</summary>
    Faction1Victory = 2,

    /// <summary>
    /// Neither faction has a living operator left — reachable in one tick
    /// when each faction's last living operator kills the other
    /// simultaneously, per design section 5 stage 13.
    /// </summary>
    Draw = 3,
}
