using System.Collections.Immutable;
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

    /// <summary>
    /// The living-warrior count, per faction, at or below which the
    /// last-stand rally formation engages. Zero (the record default) disables
    /// the behaviour entirely; production scenarios enable it through
    /// <see cref="CreateDefault"/>. This is a game-design tuning value, not a
    /// historical measurement — see <see cref="FormationRules"/>.
    /// </summary>
    public int LastStandThresholdAgents { get; init; }

    public CombatPresetId CombatPreset { get; init; } =
        CombatPresetId.PrecolonialPhilippinesV1;

    /// <summary>
    /// Per-battle warrior counts, one entry per roster index in
    /// <see cref="Combat.CombatRuleset.Roster"/>, applied identically to
    /// both factions. Empty (the default) means the existing round-robin
    /// <see cref="Combat.CombatRuleset.ResolveLoadout"/> assignment is used
    /// instead. Check <see cref="ImmutableArray{T}.IsDefaultOrEmpty"/>, not
    /// <c>== default</c> or <c>.Length == 0</c> alone: the compiler default
    /// and an explicitly empty array are different values under
    /// <c>==</c>, and only the default check treats them the same.
    /// </summary>
    public ImmutableArray<int> RosterCounts { get; init; } =
        ImmutableArray<int>.Empty;

    public int TotalAgents => checked(AgentsPerFaction * 2);

    /// <summary>
    /// A positional record synthesises <c>Equals</c>/<c>GetHashCode</c>
    /// across all instance auto-properties, but
    /// <see cref="ImmutableArray{T}"/> equality compares the underlying
    /// array by reference. Two scenarios built independently with
    /// identical roster counts would otherwise compare unequal, so this is
    /// a manual, element-wise override rather than a compiler default.
    /// </summary>
    public bool Equals(Scenario? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Seed == other.Seed &&
            MapWidth == other.MapWidth &&
            MapHeight == other.MapHeight &&
            AgentsPerFaction == other.AgentsPerFaction &&
            TickRate == other.TickRate &&
            TickLimit == other.TickLimit &&
            MaximumHitPoints == other.MaximumHitPoints &&
            DamagePerAttack == other.DamagePerAttack &&
            AttackRangeRaw == other.AttackRangeRaw &&
            PerceptionRangeRaw == other.PerceptionRangeRaw &&
            MovementSpeedRaw == other.MovementSpeedRaw &&
            AttackCooldownTicks == other.AttackCooldownTicks &&
            BodyRadiusRaw == other.BodyRadiusRaw &&
            CollisionPolicy == other.CollisionPolicy &&
            LastStandThresholdAgents == other.LastStandThresholdAgents &&
            CombatPreset == other.CombatPreset &&
            RosterCountsSpan.SequenceEqual(other.RosterCountsSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Seed);
        hash.Add(MapWidth);
        hash.Add(MapHeight);
        hash.Add(AgentsPerFaction);
        hash.Add(TickRate);
        hash.Add(TickLimit);
        hash.Add(MaximumHitPoints);
        hash.Add(DamagePerAttack);
        hash.Add(AttackRangeRaw);
        hash.Add(PerceptionRangeRaw);
        hash.Add(MovementSpeedRaw);
        hash.Add(AttackCooldownTicks);
        hash.Add(BodyRadiusRaw);
        hash.Add(CollisionPolicy);
        hash.Add(LastStandThresholdAgents);
        hash.Add(CombatPreset);
        foreach (var count in RosterCountsSpan)
        {
            hash.Add(count);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<int> RosterCountsSpan =>
        RosterCounts.IsDefault ? ReadOnlySpan<int>.Empty : RosterCounts.AsSpan();

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
            TickLimit: 10_000)
        {
            LastStandThresholdAgents = FormationRules.DefaultLastStandThresholdAgents,
        };
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

        if (!RosterCounts.IsDefaultOrEmpty)
        {
            var rules = CombatPresetRegistry.Get(CombatPreset);
            if (RosterCounts.Length != rules.Roster.Count)
            {
                throw new ArgumentException(
                    "Roster counts length must match the combat preset " +
                    $"roster count ({rules.Roster.Count}).",
                    nameof(RosterCounts));
            }

            var sum = 0;
            for (var index = 0; index < RosterCounts.Length; index++)
            {
                ValidateInRange(
                    RosterCounts[index],
                    0,
                    AgentsPerFaction,
                    $"{nameof(RosterCounts)}[{index}]");
                sum = checked(sum + RosterCounts[index]);
            }

            if (sum != AgentsPerFaction)
            {
                throw new ArgumentException(
                    "Roster counts must sum to exactly AgentsPerFaction " +
                    $"({AgentsPerFaction}); actual sum was {sum}.",
                    nameof(RosterCounts));
            }
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

        ValidateInRange(
            LastStandThresholdAgents,
            0,
            FormationRules.MaximumLastStandThresholdAgents,
            nameof(LastStandThresholdAgents));

        if (LastStandThresholdAgents > 0 && 8L * BodyRadiusRaw + 1 > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                "Body radius is too large: with the last stand enabled, the " +
                "rally jitter span (8 * BodyRadiusRaw + 1) would overflow " +
                "Int32.");
        }

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
