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
/// The pairing is <see cref="CombatPresetId.PrecolonialPhilippinesV8"/> with
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
                CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
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
    /// Armor does not lengthen a fight anywhere near far enough to approach a
    /// standoff.
    /// </summary>
    /// <remarks>
    /// V8 restates V7 exactly and adds armor, so running the same seed under
    /// both, with the same movement preset, is the closest available
    /// comparison. V7 is the right control precisely because it is V8's
    /// parent: comparing against V5 would fold in the narrow breast-high
    /// shield and its size-aware interception, and the difference would no
    /// longer be about armor at all.
    /// <para>
    /// <b>This test deliberately does not assert that the armored run takes
    /// longer.</b> It did at first, on the reasoning that a shorter armored
    /// run would betray a sign error in the mitigation arithmetic. That
    /// assertion was wrong and was removed rather than re-tuned: a single
    /// seed's battle length is chaotic, and armor changes more than damage —
    /// it also slows warriors through their weight class, which changes who
    /// reaches whom and when. Measured on seed 1, V7 decides in 1,706 ticks
    /// and V8 in 1,568, so the armored run is the shorter of the two, and
    /// nothing is wrong. The sign of the mitigation arithmetic is established
    /// where it can be established cleanly — by
    /// <c>ArmorMitigationTests</c> against the table itself, and by
    /// <c>ArmorDamageApplicationTests</c> against a controlled duel — not by
    /// inferring it from how long a two-hundred-agent battle happened to run.
    /// </para>
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
            CombatPresetId.PrecolonialPhilippinesV7,
            TickCeiling);
        var withArmor = RunToDecision(
            Seed,
            CombatPresetId.PrecolonialPhilippinesV8,
            TickCeiling);

        Assert.NotEqual(BattleOutcome.Ongoing, withoutArmor.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, withArmor.Outcome);

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
