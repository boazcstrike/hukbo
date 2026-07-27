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
    int Level = 1);
