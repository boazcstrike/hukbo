using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

/// <summary>
/// End-to-end vertical-slice coverage for the pre-colonial Philippine combat
/// preset: fixed-seed and same-seed determinism across full battles, large
/// fixed-tuple-matrix statistical checks on hit-location targeting, and
/// regression checks proving aggregate damage, same-tick mutual death, and
/// outcome/cooldown behavior are unchanged by the weapon/armor/shield and
/// hit-location additions.
/// </summary>
/// <remarks>
/// Every "sample" test below draws from a large, explicitly fixed tuple
/// matrix of (seed, tick, sourceEntityId, targetEntityId) values. No
/// <see cref="Random"/> or <see cref="System.Random"/> is used anywhere in
/// this file; the matrix is enumerated deterministically so failures are
/// reproducible.
/// </remarks>
public sealed class PhilippineCombatIntegrationTests
{
    private static readonly CombatRuleset Rules = PhilippineCombatPreset.Rules;

    private static readonly ulong[] FixedSeeds = [1UL, 7UL, 42UL, 999UL, 0xDEADBEEFUL];

    private static readonly (ulong Source, ulong Target)[] FixedEntityPairs =
    [
        (1UL, 2UL),
        (3UL, 4UL),
        (5UL, 6UL),
        (7UL, 8UL),
        (9UL, 10UL),
        (11UL, 12UL),
        (13UL, 14UL),
        (15UL, 16UL),
    ];

    private const long TicksPerSeed = 500;

    // ---------------------------------------------------------------
    // 1. Fixed-seed battle: every accepted attack has a configured
    //    weapon and a nonzero-weighted hit location.
    // ---------------------------------------------------------------

    [Fact]
    public void FixedSeedFullBattle_EveryAcceptedAttackHasAConfiguredWeaponAndHitLocation()
    {
        var scenario = Scenario.CreateDefault(seed: 4242, totalAgents: 40);
        var simulation = BattleSimulation.Create(scenario);
        var attackEvents = new List<BattleEvent>();

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
            attackEvents.AddRange(
                simulation.LastEvents.Where(
                    battleEvent => battleEvent.Kind == BattleEventKind.Attack));
        }

        Assert.NotEmpty(attackEvents);

        // Read each defender's actual assigned loadout off the simulated
        // agent state instead of recomputing it with
        // Rules.ResolveLoadout(entityId) -- the same formula the
        // production assignment path already used. Recomputing it here
        // would make this assertion pass even if that production
        // assignment were wrong.
        var loadoutsByEntityId = simulation.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => agent.Loadout);

        Assert.All(attackEvents, attack =>
        {
            Assert.True(attack.Weapon.HasValue, "Attack event is missing a weapon.");
            Assert.True(
                attack.HitLocation.HasValue,
                "Attack event is missing a hit location.");
            Assert.True(
                attack.TargetEntityId.HasValue,
                "Attack event is missing a target entity ID.");

            var weapon = attack.Weapon!.Value;
            var hitLocation = attack.HitLocation!.Value;
            var defenderLoadout = loadoutsByEntityId[attack.TargetEntityId!.Value];

            var effectiveWeight =
                (long)Rules.ResolveWeaponWeight(weapon, hitLocation) *
                Rules.ResolveDefenseMultiplier(defenderLoadout.Shield, hitLocation);
            Assert.True(
                effectiveWeight > 0,
                $"Resolved hit location {hitLocation} for weapon {weapon} against " +
                $"shield {defenderLoadout.Shield} has zero configured weight.");
        });
    }

    // ---------------------------------------------------------------
    // 2. Same-seed repeated battle: identical loadouts, full event
    //    history, tick, outcome, and state hash.
    // ---------------------------------------------------------------

    [Fact]
    public void SameSeedRepeatedBattle_ProducesIdenticalLoadoutsFullEventHistoryOutcomeAndTick()
    {
        var scenario = Scenario.CreateDefault(seed: 777, totalAgents: 30);

        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);
        var leftEvents = new List<BattleEvent>();
        var rightEvents = new List<BattleEvent>();

        while (left.Outcome == BattleOutcome.Ongoing)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();
            leftEvents.AddRange(left.LastEvents);
            rightEvents.AddRange(right.LastEvents);
        }

        Assert.Equal(
            left.Agents.Select(agent => agent.Loadout),
            right.Agents.Select(agent => agent.Loadout));
        Assert.Equal(leftEvents, rightEvents);
        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());
        Assert.NotEmpty(leftEvents);
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    [Fact]
    public void SameSeedRepeatedHeadlessRun_ProducesIdenticalEventHashStateHashOutcomeAndTick()
    {
        string[] arguments = ["--agents", "30", "--ticks", "500", "--seed", "777"];
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();
        var errorOutput = new StringWriter();

        var firstExitCode = HeadlessRunner.Run(arguments, firstOutput, errorOutput);
        var secondExitCode = HeadlessRunner.Run(arguments, secondOutput, errorOutput);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        using var firstReport = JsonDocument.Parse(firstOutput.ToString());
        using var secondReport = JsonDocument.Parse(secondOutput.ToString());

        Assert.True(firstReport.RootElement.GetProperty("deterministic").GetBoolean());
        Assert.True(secondReport.RootElement.GetProperty("deterministic").GetBoolean());
        Assert.Equal(
            firstReport.RootElement.GetProperty("measuredTicks").GetInt64(),
            secondReport.RootElement.GetProperty("measuredTicks").GetInt64());
        Assert.Equal(
            firstReport.RootElement.GetProperty("outcome").GetString(),
            secondReport.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("eventHash").GetString(),
            secondReport.RootElement.GetProperty("eventHash").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("stateHash").GetString(),
            secondReport.RootElement.GetProperty("stateHash").GetString());
    }

    // ---------------------------------------------------------------
    // 3. Large fixed-tuple-matrix statistical checks.
    // ---------------------------------------------------------------

    [Fact]
    public void LargeFixedTupleMatrix_TallHardwoodShieldLowersChestAndAbdomenFrequency()
    {
        var (unshieldedHits, unshieldedTotal) = TallyChestOrAbdomenHits(ShieldId.None);
        var (shieldedHits, shieldedTotal) = TallyChestOrAbdomenHits(ShieldId.TallHardwood);

        Assert.Equal(unshieldedTotal, shieldedTotal);
        Assert.True(
            shieldedHits < unshieldedHits,
            "PROVISIONAL gameplay-tuning comparison, not a historical claim. " +
            $"Expected fewer chest/abdomen hits with TallHardwood ({shieldedHits} " +
            $"of {shieldedTotal}) than with no shield ({unshieldedHits} of " +
            $"{unshieldedTotal}) across the fixed tuple matrix.");

        // Comfortable margin check: the shielded rate should be reduced by a
        // clearly visible amount, not merely a handful of draws.
        var unshieldedRate = (double)unshieldedHits / unshieldedTotal;
        var shieldedRate = (double)shieldedHits / shieldedTotal;
        Assert.True(
            shieldedRate < unshieldedRate * 0.75,
            $"Expected a comfortable margin: shielded rate {shieldedRate:P2} " +
            $"should be well below unshielded rate {unshieldedRate:P2}.");
    }

    [Fact]
    public void LargeFixedTupleMatrix_FourWeaponProfilesProduceDistinctTargetDistributions()
    {
        var histograms = Enum.GetValues<WeaponId>()
            .ToDictionary(weapon => weapon, weapon => BuildHistogram(weapon, ShieldId.None));

        foreach (var (weaponA, weaponB) in AllUnorderedPairs(Enum.GetValues<WeaponId>()))
        {
            var histogramA = histograms[weaponA];
            var histogramB = histograms[weaponB];
            Assert.True(
                !HistogramsEqual(histogramA, histogramB),
                $"Expected distinct target distributions for {weaponA} and {weaponB}, " +
                $"but every body part received the same count. {weaponA}: " +
                $"{DescribeHistogram(histogramA)}. {weaponB}: " +
                $"{DescribeHistogram(histogramB)}.");
        }

        // Signature-part checks derived directly from the approved weapon
        // override table (design §3): each weapon's characteristic target
        // group should be struck more often, proportionally, than for any
        // other weapon over the same large fixed matrix.
        AssertSignatureGroupIsDominant(
            histograms,
            WeaponId.GreatBlade,
            [BodyPart.Head, BodyPart.Neck]);
        AssertSignatureGroupIsDominant(
            histograms,
            WeaponId.HeavyChopper,
            [BodyPart.Shoulder]);
        AssertSignatureGroupIsDominant(
            histograms,
            WeaponId.ThrustingBlade,
            [BodyPart.Abdomen, BodyPart.Chest]);
        AssertSignatureGroupIsDominant(
            histograms,
            WeaponId.Bolo,
            [BodyPart.WeaponArm, BodyPart.ShieldArm, BodyPart.Hands]);
    }

    private static (int Hits, int Total) TallyChestOrAbdomenHits(ShieldId defenderShield)
    {
        var hits = 0;
        var total = 0;

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var attacker = new CombatLoadout(weapon, ArmorId.LightOrganic, ShieldId.None);
            var defender = new CombatLoadout(
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                defenderShield);

            foreach (var (seed, tick, source, target) in BuildTupleMatrix())
            {
                var part = HitLocationResolver.Resolve(
                    Rules,
                    attacker,
                    defender,
                    seed,
                    tick,
                    source,
                    target);
                total++;
                if (part is BodyPart.Chest or BodyPart.Abdomen)
                {
                    hits++;
                }
            }
        }

        return (hits, total);
    }

    private static Dictionary<BodyPart, int> BuildHistogram(WeaponId weapon, ShieldId defenderShield)
    {
        var histogram = Enum.GetValues<BodyPart>().ToDictionary(part => part, _ => 0);
        var attacker = new CombatLoadout(weapon, ArmorId.LightOrganic, ShieldId.None);
        var defender = new CombatLoadout(
            WeaponId.GreatBlade,
            ArmorId.LightOrganic,
            defenderShield);

        foreach (var (seed, tick, source, target) in BuildTupleMatrix())
        {
            var part = HitLocationResolver.Resolve(
                Rules,
                attacker,
                defender,
                seed,
                tick,
                source,
                target);
            histogram[part]++;
        }

        return histogram;
    }

    private static void AssertSignatureGroupIsDominant(
        IReadOnlyDictionary<WeaponId, Dictionary<BodyPart, int>> histograms,
        WeaponId signatureWeapon,
        BodyPart[] signatureGroup)
    {
        var rates = histograms.ToDictionary(
            pair => pair.Key,
            pair => GroupRate(pair.Value, signatureGroup));
        var signatureRate = rates[signatureWeapon];

        foreach (var (weapon, rate) in rates)
        {
            if (weapon == signatureWeapon)
            {
                continue;
            }

            Assert.True(
                signatureRate > rate,
                $"Expected {signatureWeapon} to strike " +
                $"{string.Join('/', signatureGroup)} more often than {weapon} " +
                $"across the fixed tuple matrix, but rates were " +
                $"{signatureWeapon}={signatureRate:P2} and {weapon}={rate:P2}.");
        }
    }

    private static double GroupRate(IReadOnlyDictionary<BodyPart, int> histogram, BodyPart[] group)
    {
        var total = histogram.Values.Sum();
        var groupCount = group.Sum(part => histogram[part]);
        return (double)groupCount / total;
    }

    private static bool HistogramsEqual(
        IReadOnlyDictionary<BodyPart, int> left,
        IReadOnlyDictionary<BodyPart, int> right) =>
        Enum.GetValues<BodyPart>().All(part => left[part] == right[part]);

    private static string DescribeHistogram(IReadOnlyDictionary<BodyPart, int> histogram) =>
        string.Join(", ", histogram.Select(pair => $"{pair.Key}={pair.Value}"));

    private static IEnumerable<(WeaponId First, WeaponId Second)> AllUnorderedPairs(
        WeaponId[] weapons)
    {
        for (var i = 0; i < weapons.Length; i++)
        {
            for (var j = i + 1; j < weapons.Length; j++)
            {
                yield return (weapons[i], weapons[j]);
            }
        }
    }

    private static IEnumerable<(ulong Seed, long Tick, ulong Source, ulong Target)>
        BuildTupleMatrix()
    {
        foreach (var seed in FixedSeeds)
        {
            foreach (var (source, target) in FixedEntityPairs)
            {
                for (var tick = 1L; tick <= TicksPerSeed; tick++)
                {
                    yield return (seed, tick, source, target);
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // 4. Regression: aggregate damage, one Damage event per target per
    //    tick, same-tick mutual death ordering, and cooldown spacing.
    // ---------------------------------------------------------------

    [Fact]
    public void Regression_AggregateDamagePerTargetPerTickEqualsSumOfIndividualAttackValuesAcrossAFullBattle()
    {
        var scenario = Scenario.CreateDefault(seed: 2024, totalAgents: 20);
        var simulation = BattleSimulation.Create(scenario);
        var ticksWithDamage = 0;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            var attacksByTarget = simulation.LastEvents
                .Where(battleEvent => battleEvent.Kind == BattleEventKind.Attack)
                .GroupBy(battleEvent => battleEvent.TargetEntityId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(battleEvent => battleEvent.Value));
            var damageEvents = simulation.LastEvents
                .Where(battleEvent => battleEvent.Kind == BattleEventKind.Damage)
                .ToArray();

            // Every damaged target has exactly one aggregated Damage event
            // whose value equals the sum of that tick's individual Attack
            // values against it.
            var damageByTarget = damageEvents
                .GroupBy(battleEvent => battleEvent.SourceEntityId)
                .ToArray();
            Assert.All(
                damageByTarget,
                group => Assert.Single(group));

            foreach (var damageEvent in damageEvents)
            {
                Assert.True(
                    attacksByTarget.TryGetValue(damageEvent.SourceEntityId, out var summedAttackValue),
                    $"Damage event for entity {damageEvent.SourceEntityId} has no " +
                    "corresponding Attack events in the same tick.");
                Assert.Equal(summedAttackValue, damageEvent.Value);
            }

            // No damaged target is missing its aggregated Damage event.
            foreach (var (targetId, summedAttackValue) in attacksByTarget)
            {
                Assert.Contains(
                    damageEvents,
                    battleEvent => battleEvent.SourceEntityId == targetId &&
                        battleEvent.Value == summedAttackValue);
            }

            if (damageEvents.Length > 0)
            {
                ticksWithDamage++;
            }
        }

        Assert.True(
            ticksWithDamage > 0,
            "Expected at least one tick with damage across the full battle.");
    }

    [Fact]
    public void Regression_SameTickMutualDeathEventsPrecedeTheOutcomeEventInEmissionOrder()
    {
        var scenario = new Scenario(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 10,
            DamagePerAttack = 10,
            AttackRangeRaw = 20 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            MovementSpeedRaw = FixedPoint.Scale,
            AttackCooldownTicks = 1,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario),
            CreateAgent(2, factionId: 1, x: 20, y: 10, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(BattleOutcome.Draw, simulation.Outcome);
        var events = simulation.LastEvents;
        var deathIndexes = events
            .Select((battleEvent, index) => (battleEvent, index))
            .Where(pair => pair.battleEvent.Kind == BattleEventKind.Death)
            .Select(pair => pair.index)
            .ToArray();
        var outcomeIndex = events
            .Select((battleEvent, index) => (battleEvent, index))
            .Single(pair => pair.battleEvent.Kind == BattleEventKind.Outcome)
            .index;

        Assert.Equal(2, deathIndexes.Length);
        Assert.All(
            deathIndexes,
            deathIndex => Assert.True(
                deathIndex < outcomeIndex,
                $"Expected Death event at index {deathIndex} to precede the " +
                $"Outcome event at index {outcomeIndex}."));
    }

    [Fact]
    public void Regression_AttackCooldownGapsRemainAtLeastTheConfiguredCooldownTicksAcrossAFullBattle()
    {
        var scenario = Scenario.CreateDefault(seed: 55, totalAgents: 20);
        var simulation = BattleSimulation.Create(scenario);
        var attackTicksBySource = new Dictionary<ulong, List<long>>();

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var attack in simulation.LastEvents.Where(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack))
            {
                if (!attackTicksBySource.TryGetValue(attack.SourceEntityId, out var ticks))
                {
                    ticks = [];
                    attackTicksBySource[attack.SourceEntityId] = ticks;
                }

                ticks.Add(attack.Tick);
            }
        }

        Assert.NotEmpty(attackTicksBySource);
        foreach (var (sourceId, ticks) in attackTicksBySource)
        {
            for (var index = 1; index < ticks.Count; index++)
            {
                var gap = ticks[index] - ticks[index - 1];
                Assert.True(
                    gap >= scenario.AttackCooldownTicks,
                    $"Entity {sourceId} attacked at ticks " +
                    $"{ticks[index - 1]} and {ticks[index]} (gap {gap}), which is " +
                    $"below the configured cooldown of {scenario.AttackCooldownTicks} ticks.");
            }
        }
    }

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int x,
        int y,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            checked(x * FixedPoint.Scale),
            checked(y * FixedPoint.Scale),
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            Rules.ResolveLoadout(entityId));
}
