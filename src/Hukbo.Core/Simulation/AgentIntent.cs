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
}
