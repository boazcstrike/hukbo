using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

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
    CombatLoadout Loadout);
