using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

/// <param name="MovementResolution">
/// Why the agent finished the tick where it did. This is the spectator's
/// bounded explanation of collision, carried per agent instead of as per-contact
/// events, which a packed front would emit by the thousand. Defaulted so that
/// presentation tests can build a view without naming it.
/// </param>
/// <param name="Level">
/// This warrior's level, set once at spawn from
/// <see cref="Scenario.PlaceholderFighterLevel"/> and never mutated
/// afterward. Defaulted, matching <see cref="MovementResolution"/> above, so
/// presentation tests written before levels existed can build a view without
/// naming it; the default matches
/// <see cref="Scenario.PlaceholderFighterLevel"/>'s own default.
/// </param>
/// <param name="ContingentId">
/// The contingent this warrior was dealt into at spawn. Defaulted, matching
/// <see cref="MovementResolution"/> and <see cref="Level"/> above, so
/// presentation tests written before contingents existed can build a view
/// without naming it.
/// </param>
/// <param name="ContingentState">
/// This warrior's contingent's behavioural mode, as of the tick this view was
/// captured. Defaulted to <see cref="ContingentState.None"/> for the same
/// reason <see cref="ContingentId"/> above is defaulted.
/// </param>
/// <param name="Rank">
/// This warrior's social and legal standing, resolved once at spawn from its
/// roster entry's <see cref="Combat.CombatLoadout.Rank"/>. Defaulted, matching
/// <see cref="MovementResolution"/>, <see cref="Level"/>, and
/// <see cref="ContingentId"/> above, so presentation tests written before
/// rank existed can build a view without naming it; the default matches
/// <see cref="Combat.CombatLoadout.Rank"/>'s own default.
/// </param>
public readonly record struct AgentView(
    ulong EntityId,
    int FactionId,
    int XRaw,
    int YRaw,
    int HitPoints,
    int MaximumHitPoints,
    ulong? TargetEntityId,
    AgentIntent Intent,
    bool IsAlive,
    CombatLoadout Loadout,
    MovementResolution MovementResolution = MovementResolution.None,
    int Level = 1,
    int ContingentId = 0,
    ContingentState ContingentState = ContingentState.None,
    RankId Rank = RankId.Timawa);
