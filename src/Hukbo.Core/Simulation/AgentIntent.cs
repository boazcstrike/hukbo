namespace Hukbo.Core.Simulation;

/// <summary>
/// Values are pinned and append-only: they enter the state hash via
/// <see cref="Determinism.StateHasher"/>, so changing a numeric value, removing
/// a member, or reordering members changes the hash for every existing replay.
/// New members are always appended after the highest existing value.
/// <see cref="Regrouping"/> sits after <see cref="Dead"/> only because it was
/// added later and reordering is forbidden — not because it is conceptually
/// terminal like <see cref="Dead"/>.
/// </summary>
public enum AgentIntent
{
    Idle = 0,
    Moving = 1,
    Attacking = 2,
    Dead = 3,

    /// <summary>
    /// The warrior is closing on its faction's rally agent during a last
    /// stand, rather than moving toward or attacking an enemy.
    /// </summary>
    Regrouping = 4,

    /// <summary>
    /// This warrior is at the distance it wants to fight from and is
    /// deliberately not advancing.
    /// </summary>
    /// <remarks>
    /// CONTRACT: this value has exactly one producer in the whole codebase —
    /// the ranged standoff hold arm. It may never be written by a rejection,
    /// a collision, a blocked proposal, or a failed route search. Those are
    /// the cases <see cref="Movement.FootworkPhase.Refuse"/> already
    /// conflates, and that conflation is the verified cause of the V6/V7
    /// standoff — a warrior that could not advance and a warrior that chose
    /// not to were indistinguishable. <see cref="Holding"/> exists to stay
    /// distinguishable from both. No emission site exists yet; this value is
    /// unused until the hold arm lands.
    /// </remarks>
    Holding = 5,
}
