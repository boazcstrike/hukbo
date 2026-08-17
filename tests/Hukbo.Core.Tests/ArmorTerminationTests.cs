using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Guards the armor identity plan's verification criterion 7: battles must
/// still terminate once armor reduces damage.
/// </summary>
/// <remarks>
/// Damage reduction lengthens fights, and this repository has already lived
/// through a period in which every battle ran to a 10,000-tick standoff draw.
/// Reducing damage pushes in exactly that direction, so the criterion is not a
/// formality. Until this test existed the criterion was an observation someone
/// had to remember to make; it is now a test that fails on its own.
/// <para>
/// The bar is deliberately the same one
/// <see cref="RangedTerminationTests"/> applies, so the two results read
/// against a single yardstick: at least nineteen of twenty seeds decide before
/// a 5,000-tick cap, the median decisive tick sits below that cap, and neither
/// faction wins fewer than four of the twenty seeds. Only the preset pairing
/// differs.
/// </para>
/// <para>
/// The pairing is <see cref="CombatPresetId.PrecolonialPhilippinesV7"/> with
/// <see cref="MovementPresetId.EvasiveFootworkV14"/>, which is what
/// <c>ArenaGame.BuildScenario</c> ships and what the seventh
/// <c>scripts/verify.ps1</c> benchmark workload measures. Testing any other
/// pairing would prove termination for a build nobody runs.
/// </para>
/// <para>
/// <strong>If this test fails, the section 6.1 mitigation values are too high
/// and must come down.</strong> Raising <see cref="Scenario.TickLimit"/> to
/// accommodate armor is not an acceptable response: it hides the regression
/// instead of reporting it. That instruction is the plan document's, not this
/// test's, and it is repeated here because this is where somebody will be
/// standing when they are tempted.
/// </para>
/// </remarks>
public sealed class ArmorTerminationTests
{
    private const int Seeds = 20;
    private const int MinimumDecisiveSeeds = 19;
    private const int MedianDecisiveTickLimit = 5_000;
    private const int MinimumVictoriesPerFaction = 4;
    private const int TotalAgents = 200;

    /// <summary>
    /// Armor does not turn the shipped pairing into a standoff.
    /// </summary>
    [Fact]
    public void SeedsOneThroughTwentyStillDecideUnderArmorMitigation()
    {
        var faction0Victories = 0;
        var faction1Victories = 0;
        var decisiveSeeds = 0;
        var decisiveTicks = new List<long>(Seeds);

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, TotalAgents) with
            {
                CombatPreset = CombatPresetId.PrecolonialPhilippinesV7,
                MovementPreset = MovementPresetId.EvasiveFootworkV14,
                TickLimit = MedianDecisiveTickLimit,
            };
            scenario.Validate();

            var simulation = BattleSimulation.Create(scenario);

            // Bounded, for the reason RangedTerminationTests gives: an
            // unbounded loop turns a stall into a suite that hangs with no
            // diagnosis, rather than a test that fails and names the seed.
            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            switch (simulation.Outcome)
            {
                case BattleOutcome.Faction0Victory:
                    faction0Victories++;
                    decisiveSeeds++;
                    decisiveTicks.Add(simulation.Tick);
                    break;
                case BattleOutcome.Faction1Victory:
                    faction1Victories++;
                    decisiveSeeds++;
                    decisiveTicks.Add(simulation.Tick);
                    break;
                case BattleOutcome.Draw:
                    decisiveSeeds++;
                    decisiveTicks.Add(simulation.Tick);
                    break;
                case BattleOutcome.Ongoing:
                default:
                    break;
            }
        }

        Assert.True(
            decisiveSeeds >= MinimumDecisiveSeeds,
            $"Only {decisiveSeeds} of {Seeds} seeds reached a decision inside " +
            $"{MedianDecisiveTickLimit} ticks; the bar is {MinimumDecisiveSeeds}. " +
            "Armor mitigation has lengthened fights past the termination bar. " +
            "Lower the section 6.1 mitigation values. Do not raise the tick " +
            "limit.");

        Assert.True(
            faction0Victories >= MinimumVictoriesPerFaction &&
            faction1Victories >= MinimumVictoriesPerFaction,
            $"Faction 0 won {faction0Victories} and faction 1 won " +
            $"{faction1Victories} of {Seeds} seeds; each must win at least " +
            $"{MinimumVictoriesPerFaction}. A lopsided sweep means the mirrored " +
            "armor composition is not actually mirrored.");

        var ordered = decisiveTicks.Order().ToArray();
        var median = ordered[ordered.Length / 2];

        Assert.True(
            median <= MedianDecisiveTickLimit,
            $"The median decisive tick was {median}, above the " +
            $"{MedianDecisiveTickLimit} bar. Lower the section 6.1 mitigation " +
            "values. Do not raise the tick limit.");
    }

    /// <summary>
    /// Armor lengthens fights rather than shortening them, and it does so by a
    /// margin that leaves the tick budget intact.
    /// </summary>
    /// <remarks>
    /// This is the direction check the plan's constraint implies but never
    /// states as a test. V7 restates V5 exactly and adds armor, so running the
    /// same seed under both, with the same movement preset, isolates armor's
    /// effect from everything else the preset does. A V7 run that finished
    /// *faster* than V5 would mean mitigation was being applied with the wrong
    /// sign somewhere, which no other assertion in this suite would catch.
    /// <para>
    /// The upper bound is provisional gameplay tuning, not a measurement. It
    /// exists so that a future mitigation increase that doubles battle length
    /// has to be an explicit decision rather than an accident.
    /// </para>
    /// </remarks>
    [Fact]
    public void ArmorLengthensTheSameSeedWithoutExhaustingTheTickBudget()
    {
        const ulong Seed = 1;
        const int TickCeiling = 10_000;

        var withoutArmor = RunToDecision(
            Seed,
            CombatPresetId.PrecolonialPhilippinesV5,
            TickCeiling);
        var withArmor = RunToDecision(
            Seed,
            CombatPresetId.PrecolonialPhilippinesV7,
            TickCeiling);

        Assert.NotEqual(BattleOutcome.Ongoing, withoutArmor.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, withArmor.Outcome);

        Assert.True(
            withArmor.Ticks > withoutArmor.Ticks,
            $"The armored run finished in {withArmor.Ticks} ticks and the " +
            $"unarmored one in {withoutArmor.Ticks}. Armor reduces damage, so " +
            "the armored run must take longer; a shorter one means mitigation " +
            "is being applied with the wrong sign.");

        Assert.True(
            withArmor.Ticks <= withoutArmor.Ticks * 3,
            $"The armored run took {withArmor.Ticks} ticks against the " +
            $"unarmored run's {withoutArmor.Ticks}, more than three times as " +
            "long. That is the shape of an approaching standoff. Lower the " +
            "section 6.1 mitigation values.");
    }

    private static (BattleOutcome Outcome, long Ticks) RunToDecision(
        ulong seed,
        CombatPresetId combatPreset,
        int tickLimit)
    {
        var scenario = Scenario.CreateDefault(seed, TotalAgents) with
        {
            CombatPreset = combatPreset,
            MovementPreset = MovementPresetId.EvasiveFootworkV14,
            TickLimit = tickLimit,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        return (simulation.Outcome, simulation.Tick);
    }
}
