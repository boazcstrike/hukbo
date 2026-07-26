using AutonomousArena.Core.Mathematics;

namespace AutonomousArena.Core.Simulation;

/// <summary>
/// Immutable configuration for one disposable two-faction battle.
/// </summary>
public sealed record Scenario(
    ulong Seed,
    int MapWidth,
    int MapHeight,
    int AgentsPerFaction,
    int TickRate,
    int TickLimit)
{
    public const int MaximumAgentsPerFaction = 10_000;
    public const int MaximumMapDimension = 1_000_000;
    public const int MaximumTickRate = 1_000;
    public const int MaximumTickLimit = 100_000_000;

    private const int MaximumCombatValue = 1_000_000;
    private const int DefaultMapWidth = 1_280;
    private const int DefaultMapHeight = 720;

    public int MaximumHitPoints { get; init; } = 100;

    public int DamagePerAttack { get; init; } = 10;

    public int AttackRangeRaw { get; init; } = 12 * FixedPoint.Scale;

    public int PerceptionRangeRaw { get; init; } = 2_048 * FixedPoint.Scale;

    public int MovementSpeedRaw { get; init; } = 3 * FixedPoint.Scale;

    public int AttackCooldownTicks { get; init; } = 5;

    public int TotalAgents => checked(AgentsPerFaction * 2);

    public static Scenario CreateDefault(ulong seed = 1, int totalAgents = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalAgents);

        if ((totalAgents & 1) != 0)
        {
            throw new ArgumentException(
                "The total agent count must be even for two equally sized factions.",
                nameof(totalAgents));
        }

        var scenario = new Scenario(
            seed,
            DefaultMapWidth,
            DefaultMapHeight,
            totalAgents / 2,
            TickRate: 20,
            TickLimit: 10_000);
        scenario.Validate();
        return scenario;
    }

    public void Validate()
    {
        ValidateInRange(
            MapWidth,
            1,
            MaximumMapDimension,
            nameof(MapWidth));
        ValidateInRange(
            MapHeight,
            1,
            MaximumMapDimension,
            nameof(MapHeight));
        ValidateInRange(
            AgentsPerFaction,
            1,
            MaximumAgentsPerFaction,
            nameof(AgentsPerFaction));
        ValidateInRange(TickRate, 1, MaximumTickRate, nameof(TickRate));
        ValidateInRange(TickLimit, 1, MaximumTickLimit, nameof(TickLimit));
        ValidateInRange(
            MaximumHitPoints,
            1,
            MaximumCombatValue,
            nameof(MaximumHitPoints));
        ValidateInRange(
            DamagePerAttack,
            1,
            MaximumCombatValue,
            nameof(DamagePerAttack));
        ValidateRawWorldValue(AttackRangeRaw, nameof(AttackRangeRaw));
        ValidateRawWorldValue(PerceptionRangeRaw, nameof(PerceptionRangeRaw));
        ValidateRawWorldValue(MovementSpeedRaw, nameof(MovementSpeedRaw));
        ValidateInRange(
            AttackCooldownTicks,
            1,
            MaximumTickLimit,
            nameof(AttackCooldownTicks));

        if (PerceptionRangeRaw < AttackRangeRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PerceptionRangeRaw),
                "Perception range must be at least the attack range.");
        }

        if ((long)AgentsPerFaction * DamagePerAttack > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DamagePerAttack),
                "Maximum accumulated same-tick damage must fit in a signed integer.");
        }

        _ = checked(AgentsPerFaction * 2);
        var maximumRawDimension = checked(MaximumMapDimension * FixedPoint.Scale);
        _ = checked(
            ((long)maximumRawDimension * maximumRawDimension) +
            ((long)maximumRawDimension * maximumRawDimension));
    }

    private static void ValidateRawWorldValue(int value, string parameterName)
    {
        var maximumRawValue = checked(MaximumMapDimension * FixedPoint.Scale);
        ValidateInRange(value, 1, maximumRawValue, parameterName);
    }

    private static void ValidateInRange(
        int value,
        int minimum,
        int maximum,
        string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be between {minimum} and {maximum}, inclusive.");
        }
    }
}
