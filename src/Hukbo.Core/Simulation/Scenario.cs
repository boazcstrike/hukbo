using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

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

    public int BodyRadiusRaw { get; init; } = CollisionRules.DefaultBodyRadiusRaw;

    public CollisionPolicy CollisionPolicy { get; init; } = CollisionPolicy.Solid;

    public CombatPresetId CombatPreset { get; init; } =
        CombatPresetId.PrecolonialPhilippinesV1;

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
        ValidateRawWorldValue(BodyRadiusRaw, nameof(BodyRadiusRaw));
        ValidateInRange(
            AttackCooldownTicks,
            1,
            MaximumTickLimit,
            nameof(AttackCooldownTicks));

        if (!CombatPresetRegistry.IsRegistered(CombatPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(CombatPreset),
                CombatPreset,
                "Combat preset must be a registered value.");
        }

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

        ValidateCollisionConfiguration();

        _ = checked(AgentsPerFaction * 2);
        var maximumRawDimension = checked(MaximumMapDimension * FixedPoint.Scale);
        _ = checked(
            ((long)maximumRawDimension * maximumRawDimension) +
            ((long)maximumRawDimension * maximumRawDimension));
    }

    private void ValidateCollisionConfiguration()
    {
        if (CollisionPolicy != CollisionPolicy.Solid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CollisionPolicy),
                CollisionPolicy,
                "Solid is the only approved collision policy.");
        }

        var bodyDiameterRaw = checked(2L * BodyRadiusRaw);

        if (bodyDiameterRaw > AttackRangeRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                "The body diameter must not exceed the attack range, because " +
                "two bodies pressed into contact could then never reach each " +
                "other.");
        }

        if (MovementSpeedRaw > BodyRadiusRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MovementSpeedRaw),
                MovementSpeedRaw,
                "The movement speed must not exceed the body radius, because a " +
                "longer step could tunnel one body straight through another.");
        }

        var mapWidthRaw = checked((long)MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked((long)MapHeight * FixedPoint.Scale);

        if (bodyDiameterRaw > mapWidthRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MapWidth),
                MapWidth,
                "The map must be at least one body wide.");
        }

        if (bodyDiameterRaw > mapHeightRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MapHeight),
                MapHeight,
                "The map must be at least one body tall.");
        }

        ValidateBodyDensity(bodyDiameterRaw, mapWidthRaw, mapHeightRaw);
    }

    /// <summary>
    /// Rejects a population that cannot be placed even under the conservative
    /// square-packing bound
    /// <c>TotalAgents * bodyArea &gt; mapWidthRaw * mapHeightRaw</c>. That
    /// product overflows a signed 64-bit integer at the largest supported map,
    /// so the comparison is rearranged into a division. For a positive
    /// <c>bodyArea</c> the two forms accept exactly the same configurations,
    /// including equality at the bound.
    /// </summary>
    private void ValidateBodyDensity(
        long bodyDiameterRaw,
        long mapWidthRaw,
        long mapHeightRaw)
    {
        var bodyAreaRaw = checked(bodyDiameterRaw * bodyDiameterRaw);
        var mapAreaRaw = checked(mapWidthRaw * mapHeightRaw);

        if (TotalAgents > mapAreaRaw / bodyAreaRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AgentsPerFaction),
                AgentsPerFaction,
                "The population cannot be placed without overlapping bodies. " +
                "Reduce the agent count or the body radius, or enlarge the map.");
        }
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
