namespace AutonomousArena.Core.Simulation;

public sealed record BattleSnapshot(
    long Tick,
    BattleOutcome Outcome,
    IReadOnlyList<AgentView> Agents,
    IReadOnlyList<BattleEvent> Events,
    ulong StateHash);
