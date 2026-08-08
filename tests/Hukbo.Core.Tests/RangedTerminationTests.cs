using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Closes the canonical gate's ranged blind spot. The gate's one benchmark
/// invocation exercises <see cref="CombatPresetId.PrecolonialPhilippinesV4"/>
/// under <see cref="MovementPresetId.PersistentContingentsV4"/> — never the
/// ranged combat preset introduced for the ranged-units package — so a
/// completely broken ranged path (every projectile refused, every archer
/// stalling out to the tick cap, a standoff distance that never lets a shot
/// land) would leave <c>./scripts/verify.ps1</c> green. This mirrors
/// <see cref="BattleSimulationTests.SeedsOneThroughTwentyProduceVictoriesForBothFactions"/>
/// but against <see cref="CombatPresetId.PrecolonialPhilippinesV5"/> paired
/// with <see cref="MovementPresetId.RangedStandoffV8"/> — the only legal
/// pairing for a ranged roster, per <see cref="RangedCalibrationHarness"/>'s
/// own remarks.
/// </summary>
/// <remarks>
/// <para>
/// The roster share weighting below is copied from
/// <c>RangedCalibrationHarness.cs</c>'s own
/// <c>RangedCalibrationRun.DefaultRosterWeights</c> — the RU-24/RU-45
/// weighting the calibration harness measured a roster split against, not a
/// fresh weighting invented for this test. Measuring against a different
/// split would exercise a battle the calibration numbers never validated.
/// </para>
/// <para>
/// The array's length is asserted against
/// <see cref="PhilippineCombatPresetV5"/>'s own roster count rather than
/// assumed: <see cref="PhilippineCombatPresetV5.Rules"/>'s roster has
/// already grown once inside this package (RU-45 added two
/// <see cref="ShieldId.TallHardwood"/> rows), so a silent length mismatch
/// here would either throw out of <see cref="Scenario.Validate"/> with an
/// unhelpful message or, worse, compile against a stale roster shape.
/// </para>
/// </remarks>
public sealed class RangedTerminationTests
{
    /// <summary>
    /// RU-24/RU-45's roster share weights, in
    /// <see cref="PhilippineCombatPresetV5.Rules"/> roster order (Kampilan,
    /// Wasay, Kalis, Itak, Bangkaw, Busog, Arquebus, Kalis + TallHardwood,
    /// Itak + TallHardwood). Provisional gameplay tuning, not a historical
    /// measurement — see <c>RangedCalibrationHarness.cs</c>'s
    /// <c>DefaultRosterWeights</c> remarks for the derivation: a
    /// three-quarters melee majority, a one-quarter ranged share weighted
    /// toward the Bangkaw, and the two TallHardwood rows splitting their
    /// shieldless counterpart's pre-RU-45 weight roughly in half.
    /// </summary>
    private static readonly int[] RangedRosterShareWeights =
        [19, 19, 10, 9, 11, 8, 6, 9, 9];

    [Fact]
    public void RosterShareWeightsMatchThePresetRosterLength()
    {
        Assert.Equal(
            PhilippineCombatPresetV5.Rules.Roster.Count,
            RangedRosterShareWeights.Length);
    }

    /// <summary>
    /// Same two properties as
    /// <see cref="BattleSimulationTests.SeedsOneThroughTwentyProduceVictoriesForBothFactions"/>,
    /// against the ranged preset pairing instead of the shipped default: at
    /// least nineteen of twenty seeds decide before a 5,000-tick cap (the
    /// ranged-units plan's own termination bar, half the melee test's cap),
    /// the median decisive tick sits at or below that same cap, and neither
    /// faction wins fewer than four of the twenty seeds.
    /// </summary>
    [Fact]
    public void SeedsOneThroughTwentyProduceVictoriesForBothFactionsUnderRangedStandoff()
    {
        const int Seeds = 20;
        const int MinimumDecisiveSeeds = 19;
        const int MedianDecisiveTickLimit = 5_000;
        const int MinimumVictoriesPerFaction = 4;
        const int TotalAgents = 200;

        Assert.Equal(
            PhilippineCombatPresetV5.Rules.Roster.Count,
            RangedRosterShareWeights.Length);

        var agentsPerFaction = TotalAgents / 2;
        var rosterCounts = RangedCalibrationHarness.BuildRosterCounts(
            agentsPerFaction,
            RangedRosterShareWeights);

        var faction0Victories = 0;
        var faction1Victories = 0;
        var decisiveTicks = new List<long>(Seeds);

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, TotalAgents) with
            {
                CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
                MovementPreset = MovementPresetId.RangedStandoffV8,
                RosterCounts = rosterCounts,
                TickLimit = MedianDecisiveTickLimit,
            };
            scenario.Validate();

            var simulation = BattleSimulation.Create(scenario);

            // Bounded. An unbounded loop turns a stall into a suite that
            // hangs with no diagnosis rather than a test that fails and
            // names the seed, which would defeat the whole point of the
            // criterion.
            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            switch (simulation.Outcome)
            {
                case BattleOutcome.Faction0Victory:
                    faction0Victories++;
                    break;

                case BattleOutcome.Faction1Victory:
                    faction1Victories++;
                    break;

                case BattleOutcome.Ongoing:
                case BattleOutcome.Draw:
                default:
                    break;
            }

            if (simulation.Outcome is BattleOutcome.Faction0Victory or
                BattleOutcome.Faction1Victory or
                BattleOutcome.Draw)
            {
                decisiveTicks.Add(simulation.Tick);
            }
        }

        Assert.True(
            faction0Victories >= MinimumVictoriesPerFaction &&
            faction1Victories >= MinimumVictoriesPerFaction,
            $"Faction 0 won {faction0Victories} of 20 seeds and faction 1 won " +
            $"{faction1Victories}. Each faction must win at least " +
            $"{MinimumVictoriesPerFaction}.");

        Assert.True(
            decisiveTicks.Count >= MinimumDecisiveSeeds,
            $"Only {decisiveTicks.Count} of {Seeds} seeds decided before " +
            $"the tick cap; at least {MinimumDecisiveSeeds} are required.");

        var sorted = decisiveTicks.Order().ToArray();
        var median = sorted[sorted.Length / 2];
        Assert.True(
            median <= MedianDecisiveTickLimit,
            $"The median decisive tick was {median}, above the " +
            $"{MedianDecisiveTickLimit} tick clause.");
    }
}
