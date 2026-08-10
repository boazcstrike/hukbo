using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

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
    private const int DefaultMaximumProjectilesInFlight = 512;
    private const int MaximumProjectilesInFlightCeiling = 1_000_000;

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

    /// <summary>
    /// The hard ceiling on projectiles simultaneously in flight, sized once
    /// at <see cref="BattleSimulation"/> construction into a flat pooled
    /// array. A launch attempted while the pool already holds this many live
    /// projectiles is refused outright — the shot does not occur, the
    /// launching warrior's cooldown is not charged, and a derived counter
    /// records the refusal — rather than the pool growing, which would be
    /// both an unbounded per-tick allocation and an unbounded state-hash
    /// fold; see <see cref="Projectile"/> and
    /// <c>BattleSimulation.GatherAndCommitAttacks</c>.
    /// <para>
    /// A scenario is free to declare this below the worst case its own
    /// roster could produce — that is exactly how the refusal path is
    /// exercised deterministically in a test — so <see cref="Validate"/>
    /// only bounds this to a representable, non-negative capacity rather
    /// than rejecting an "insufficient" value. Sizing it comfortably above
    /// routine use, so a refusal stays a genuine anomaly rather than a
    /// routine occurrence, is a calibration concern for whichever preset
    /// actually fields ranged weapons, not a constraint this record enforces.
    /// A combat preset that fields no ranged weapon at all — every preset up
    /// to and including <see cref="CombatPresetId.PrecolonialPhilippinesV4"/> —
    /// never launches a projectile, so this ceiling is inert for it
    /// regardless of the value chosen.
    /// </para>
    /// </summary>
    public int MaximumProjectilesInFlight { get; init; } =
        DefaultMaximumProjectilesInFlight;

    /// <summary>
    /// The combat ruleset this battle is fought under. Earlier presets stay
    /// registered and unmodified so a replay recorded against one remains
    /// reproducible by naming it here.
    /// <para>
    /// The shipped default moved from
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> to
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV4"/>, the preset that
    /// assigns each roster entry a <see cref="RankId"/> and a per-rank
    /// fighter level, so a spectator's default battle carries the visible rank
    /// the composition panel and agent inspector present.
    /// </para>
    /// <para>
    /// On 2026-08-11 it moved again, to
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV6"/>. V6 restates
    /// every one of V4's tables unchanged and retunes only cadence and damage:
    /// blows land roughly half as often and hurt roughly twice as much, at a
    /// damage per tick held within two per cent of V4's. The reason is
    /// legibility rather than balance — an interactive smoke run failed the
    /// CL-1, CL-3, and CL-7 rows of docs/development/smoke-checklist.md
    /// because event-feed lines and clash crosses arrived faster than a
    /// spectator could attribute them to individual blows, and the artefact
    /// rate is the attack rate. Twenty seeds were measured against both
    /// presets before this flip: both decide all twenty, and V6's median
    /// decision tick is 1,651 against V4's 1,668. See
    /// docs/plans/2026-08-11-combat-cadence-v6-design.md.
    /// </para>
    /// <para>
    /// Note that the default is <em>not</em> simply the highest-numbered
    /// preset. <see cref="CombatPresetId.PrecolonialPhilippinesV5"/> fields
    /// the three ranged weapons and remains deliberately opt-in; V6 descends
    /// from V4 and fields no ranged row.
    /// </para>
    /// </summary>
    public CombatPresetId CombatPreset { get; init; } =
        CombatPresetId.PrecolonialPhilippinesV6;

    /// <summary>
    /// The movement preset this battle is fought under. Defaults to the
    /// newest preset; earlier ones stay registered and unmodified so a
    /// replay recorded against one remains reproducible by naming it here.
    /// Task T6 of the contingent close-latch plan
    /// flipped the shipped default from <see cref="MovementPresetId.PersistentContingentsV2"/>
    /// to <see cref="MovementPresetId.PersistentContingentsV3"/> -- the
    /// rule-3 close latch and the contact-count denominator that plan
    /// landed. The shipped default has since moved on again, to
    /// <see cref="MovementPresetId.PersistentContingentsV4"/>, which narrows
    /// movement gate 6's cross-contingent scan to contingents that could
    /// actually be granted cohesion -- the remedy design section 3.5
    /// pre-analysed and section 13 question 8 reserved for the user, adopted
    /// after section 10.3's inertness bar failed on seed 11 with chain denial
    /// established as the cause. <c>PersistentContingentsV2</c> and
    /// <c>PersistentContingentsV3</c> both stay registered and byte-identical
    /// for a replay that names one of them explicitly.
    /// </summary>
    public MovementPresetId MovementPreset { get; init; } =
        MovementPresetId.PersistentContingentsV4;

    /// <summary>
    /// Every warrior's level, until a leveling system exists. Set once, at
    /// spawn, onto <see cref="AgentState.Level"/> and never mutated
    /// afterward. Bounds an active attack combination's maximum length
    /// alongside <see cref="Combat.WeaponProfile.ComboMaxSteps"/> — see
    /// <c>BattleSimulation.GatherAndCommitAttacks</c>. Must be at least
    /// <c>1</c>.
    /// </summary>
    public int PlaceholderFighterLevel { get; init; } = 1;

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
            MaximumProjectilesInFlight == other.MaximumProjectilesInFlight &&
            CombatPreset == other.CombatPreset &&
            MovementPreset == other.MovementPreset &&
            PlaceholderFighterLevel == other.PlaceholderFighterLevel &&
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
        hash.Add(MaximumProjectilesInFlight);
        hash.Add(CombatPreset);
        hash.Add(MovementPreset);
        hash.Add(PlaceholderFighterLevel);
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

        ValidateInRange(
            PlaceholderFighterLevel,
            1,
            MaximumCombatValue,
            nameof(PlaceholderFighterLevel));

        if (!CombatPresetRegistry.IsRegistered(CombatPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(CombatPreset),
                CombatPreset,
                "Combat preset must be a registered value.");
        }

        if (!MovementPresetRegistry.IsRegistered(MovementPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MovementPreset),
                MovementPreset,
                "Movement preset must be a registered value.");
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

        // Checked against the largest damage any warrior could actually
        // deal, not against this scenario's global value: from combat preset
        // V2 onward a weapon profile supplies the per-warrior damage and can
        // exceed DamagePerAttack, so guarding on the scenario value alone
        // would under-count the worst case this accumulator has to hold.
        var worstCaseDamage = Math.Max(
            DamagePerAttack,
            CombatPresetRegistry.Get(CombatPreset)
                .MaximumProfileDamagePerAttack ?? 0);
        if ((long)AgentsPerFaction * worstCaseDamage > int.MaxValue)
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

        ValidateInRange(
            MaximumProjectilesInFlight,
            0,
            MaximumProjectilesInFlightCeiling,
            nameof(MaximumProjectilesInFlight));

        if (LastStandThresholdAgents > 0 &&
            !FormationRules.IsBodyRadiusWithinJitterSpanRange(BodyRadiusRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                "Body radius is too large: with the last stand enabled, the " +
                "rally jitter span (8 * BodyRadiusRaw + 1) would overflow " +
                "Int32.");
        }

        // A contingent can in principle hold every living member of a
        // faction, so AgentsPerFaction is the worst-case living headcount
        // the persistent-contingent cohesion path could ever compute a
        // jitter and trail for. Checked here, up front, rather than left to
        // throw from inside a tick the first time a contingent actually
        // reaches that size.
        if (!FormationRules.IsBodyRadiusWithinContingentJitterRange(
            BodyRadiusRaw,
            AgentsPerFaction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                "Body radius is too large: the worst-case contingent jitter " +
                "(BodyRadiusRaw * (IntegerSquareRoot(4 * AgentsPerFaction) + " +
                "1)) would overflow Int32.");
        }

        var worstCaseContingentJitterRaw = FormationRules.ComputeContingentJitterRaw(
            BodyRadiusRaw,
            AgentsPerFaction);

        if (!FormationRules.IsBodyRadiusWithinContingentTrailRange(
            BodyRadiusRaw,
            worstCaseContingentJitterRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                "Body radius is too large: the worst-case contingent trail " +
                "distance (((3 * jitterRaw + 1) / 2) + (3 * BodyRadiusRaw)) " +
                "would overflow Int32.");
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
