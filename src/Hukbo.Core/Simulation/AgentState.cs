namespace Hukbo.Core.Simulation;

internal sealed class AgentState
{
    internal AgentState(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        int maximumHitPoints,
        int movementSpeedRaw,
        int perceptionRangeRaw,
        int attackRangeRaw,
        int damagePerAttack,
        int attackCooldownTicks)
    {
        if (entityId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        if (factionId is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(factionId));
        }

        EntityId = entityId;
        FactionId = factionId;
        XRaw = xRaw;
        YRaw = yRaw;
        HitPoints = maximumHitPoints;
        MaximumHitPoints = maximumHitPoints;
        MovementSpeedRaw = movementSpeedRaw;
        PerceptionRangeRaw = perceptionRangeRaw;
        AttackRangeRaw = attackRangeRaw;
        DamagePerAttack = damagePerAttack;
        AttackCooldownTicks = attackCooldownTicks;
        Intent = AgentIntent.Idle;
    }

    internal ulong EntityId { get; }

    internal int FactionId { get; }

    internal int XRaw { get; set; }

    internal int YRaw { get; set; }

    internal int HitPoints { get; set; }

    internal int MaximumHitPoints { get; }

    internal int MovementSpeedRaw { get; }

    internal int PerceptionRangeRaw { get; }

    internal int AttackRangeRaw { get; }

    internal int DamagePerAttack { get; }

    internal int AttackCooldownTicks { get; }

    internal int AttackCooldownRemaining { get; set; }

    internal ulong? TargetEntityId { get; set; }

    internal AgentIntent Intent { get; set; }

    internal bool IsAlive => HitPoints > 0;

    internal AgentView ToView() =>
        new(
            EntityId,
            FactionId,
            XRaw,
            YRaw,
            HitPoints,
            MaximumHitPoints,
            TargetEntityId,
            Intent,
            IsAlive);
}
