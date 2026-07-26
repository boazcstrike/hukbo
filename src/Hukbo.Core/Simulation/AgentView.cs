using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

/// <param name="MovementResolution">
/// Why the agent finished the tick where it did. This is the spectator's
/// bounded explanation of collision, carried per agent instead of as per-contact
/// events, which a packed front would emit by the thousand. Defaulted so that
/// presentation tests can build a view without naming it.
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
    MovementResolution MovementResolution = MovementResolution.None);
