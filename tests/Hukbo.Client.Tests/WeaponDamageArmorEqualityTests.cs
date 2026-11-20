using System.Collections.Generic;
using System.Linq;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Xunit.Abstractions;

namespace Hukbo.Client.Tests;

/// <summary>
/// The weapon damage tally design's section 3.1 question: can the client
/// reproduce each tick's post-armour <see cref="BattleEventKind.Damage"/>
/// total from the landed <see cref="BattleEventKind.Attack"/> events alone,
/// by applying <see cref="CombatRuleset.ApplyArmorMitigation"/> to every
/// landed blow with the target's armour and the event's hit location?
/// If yes, the tally can show true damage after armour. This runs a real
/// 500-agent <see cref="BattleSimulation"/> under the shipped presets, the
/// same scenario <c>ArenaGame.BuildScenario</c> builds, without constructing
/// <c>ArenaGame</c>.
/// </summary>
public sealed class WeaponDamageArmorEqualityTests
{
    private const ulong Seed = 1;
    private const int MaximumTicks = 3000;
    private const int MinimumLandedBlows = 500;

    private readonly ITestOutputHelper _output;

    public WeaponDamageArmorEqualityTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public void PerTargetMitigatedSumOfLandedBlowsEqualsTheDamageEventEveryTick()
    {
        var result = Run();

        _output.WriteLine(
            $"ticks={result.Ticks} landedBlows={result.LandedBlows} " +
            $"armourReducedBlows={result.ArmourReducedBlows} " +
            $"damageEvents={result.DamageEvents} " +
            $"damageEventTotal={result.DamageEventTotal} " +
            $"tallyTotal={result.TallyTotal} " +
            $"mismatchedTicks={result.MismatchedTicks.Count}");

        // Guard against a vacuous pass: a battle that never lands a blow, or
        // never meets armour, would make equality trivially true.
        Assert.True(
            result.LandedBlows >= MinimumLandedBlows,
            $"Only {result.LandedBlows} landed blows in {result.Ticks} ticks.");
        Assert.True(
            result.ArmourReducedBlows > 0,
            "No landed blow met armour, so mitigation was never exercised.");

        Assert.True(
            result.MismatchedTicks.Count == 0,
            "Per-target mitigated sums diverged from the Damage events on " +
            $"{result.MismatchedTicks.Count} tick(s); first: " +
            result.MismatchedTicks.FirstOrDefault());

        // The same battle fed through the tally the damage card reads: its two
        // factions' totals must add up to every Damage event of the battle,
        // so what the card shows is the damage the simulation applied.
        Assert.True(result.DamageEventTotal > 0, "No Damage event carried any damage.");
        Assert.Equal(result.DamageEventTotal, result.TallyTotal);
    }

    private static Scenario BuildShippedScenario()
    {
        var composition = ArmyComposition.Default;
        var scenario = Scenario.CreateDefault(Seed, composition.UnitsPerTeam * 2) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
            MovementPreset = MovementPresetId.EvasiveFootworkV14,
        };

        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        return scenario with
        {
            RosterCounts = ArenaGame.ExpandCompositionToRosterCounts(
                rules.Roster,
                composition,
                ArmorComposition.Default),
        };
    }

    private static EqualityRun Run()
    {
        var scenario = BuildShippedScenario();
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var simulation = BattleSimulation.Create(scenario);
        var tally = new WeaponDamageTally(rules);
        tally.SeedRoster(simulation.Agents);
        var armourByEntity = simulation.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => agent.Loadout.Armor);

        var mismatches = new List<string>();
        var landedBlows = 0;
        var armourReducedBlows = 0;
        var damageEvents = 0;
        long damageEventTotal = 0;
        var mitigatedByTarget = new Dictionary<ulong, int>();
        var damageByTarget = new Dictionary<ulong, int>();

        while (simulation.Outcome == BattleOutcome.Ongoing &&
               simulation.Tick < MaximumTicks)
        {
            simulation.AdvanceOneTick();
            tally.Ingest(simulation.LastEvents, simulation.Agents);
            mitigatedByTarget.Clear();
            damageByTarget.Clear();

            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind == BattleEventKind.Damage)
                {
                    damageEvents++;
                    damageEventTotal += battleEvent.Value;
                    damageByTarget[battleEvent.SourceEntityId] = battleEvent.Value;
                    continue;
                }

                if (battleEvent.Kind != BattleEventKind.Attack ||
                    battleEvent.Resolution != AttackResolution.Landed)
                {
                    continue;
                }

                var targetId = battleEvent.TargetEntityId!.Value;
                var mitigated = rules.ApplyArmorMitigation(
                    battleEvent.Value,
                    armourByEntity[targetId],
                    battleEvent.HitLocation!.Value);
                landedBlows++;
                if (mitigated < battleEvent.Value)
                {
                    armourReducedBlows++;
                }

                mitigatedByTarget[targetId] =
                    mitigatedByTarget.GetValueOrDefault(targetId) + mitigated;
            }

            var tickMismatch = FindMismatch(
                simulation.Tick,
                mitigatedByTarget,
                damageByTarget);
            if (tickMismatch is not null)
            {
                mismatches.Add(tickMismatch);
            }
        }

        return new EqualityRun(
            simulation.Tick,
            landedBlows,
            armourReducedBlows,
            damageEvents,
            damageEventTotal,
            tally.GetFactionDamage(0) + tally.GetFactionDamage(1),
            mismatches);
    }

    private static string? FindMismatch(
        long tick,
        Dictionary<ulong, int> mitigatedByTarget,
        Dictionary<ulong, int> damageByTarget)
    {
        // A target whose mitigated sum is zero emits no Damage event, so a
        // missing entry on either side reads as zero.
        foreach (var targetId in mitigatedByTarget.Keys.Union(damageByTarget.Keys))
        {
            var mitigated = mitigatedByTarget.GetValueOrDefault(targetId);
            var damage = damageByTarget.GetValueOrDefault(targetId);
            if (mitigated != damage)
            {
                return $"tick {tick} target {targetId}: " +
                    $"mitigated sum {mitigated}, Damage event {damage}";
            }
        }

        return null;
    }

    private sealed record EqualityRun(
        long Ticks,
        int LandedBlows,
        int ArmourReducedBlows,
        int DamageEvents,
        long DamageEventTotal,
        long TallyTotal,
        List<string> MismatchedTicks);
}
