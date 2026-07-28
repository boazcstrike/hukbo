namespace Hukbo.Core.Simulation;

/// <summary>
/// A contingent's behavioural mode under a movement preset that reads it.
/// The authoritative store is per agent: every living member of a
/// contingent carries the same value on its own
/// <see cref="AgentState.ContingentState"/>, written every tick by the tick
/// stage that resolves it. This is a behavioural mode, never a positional
/// assignment — no agent is ever assigned to a rank, a file, or a named
/// formation slot. Numeric values are pinned and this enum is append-only
/// from the day it ships, because it enters the state hash: reordering or
/// renumbering a value requires a new preset version and new golden
/// expectations, exactly as <see cref="MovementResolution"/> requires.
/// </summary>
public enum ContingentState
{
    /// <summary>
    /// Not under a movement preset that assigns contingent states, or the
    /// contingent currently has no living members. Every agent carries this
    /// value under today's frozen preset.
    /// </summary>
    None = 0,

    /// <summary>Moving toward the enemy, cohesion limited to stragglers.</summary>
    Advance = 1,

    /// <summary>Gathering: cohesion pulls every living, moving, non-leader member.</summary>
    Hold = 2,

    /// <summary>A member has reached engagement distance; cohesion is off.</summary>
    Close = 3,

    /// <summary>
    /// The contingent has lost too many members to act as one; cohesion is
    /// off permanently.
    /// </summary>
    Break = 4,
}
