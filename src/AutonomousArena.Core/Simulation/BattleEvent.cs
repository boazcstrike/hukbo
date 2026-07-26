namespace AutonomousArena.Core.Simulation;

public enum BattleEventKind
{
    Move = 0,
    Attack = 1,
    Damage = 2,
    Death = 3,
    Outcome = 4,
}

public readonly record struct BattleEvent(
    long Sequence,
    long Tick,
    BattleEventKind Kind,
    ulong SourceEntityId,
    ulong? TargetEntityId,
    int Value,
    int? FactionId);
