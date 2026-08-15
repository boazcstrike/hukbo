using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Xunit;

namespace Hukbo.Core.Tests;

/// <summary>
/// T3 (shield-projectile-block-design.md, section 6.2): the block-recovery
/// window. A warrior whose shield takes a blow has their pace clamped for a
/// short authored number of ticks and then recovers.
/// <para>
/// The window is authoritative state: it is written in tick stage 10 when a
/// block resolves, decremented in stage 1, read in stage 5 when the pace cap
/// is chosen, folded into the state hash, and carried on
/// <see cref="AgentView"/> so the inspector can show it. A block therefore
/// takes effect on the tick after the block, which the design states
/// explicitly so the one-tick offset is never read as a defect.
/// </para>
/// <para>
/// Every expected number here is a literal. None is read back out of the
/// constant under test, because a threshold taken from the constant it is
/// checking moves with it and proves nothing.
/// </para>
/// </summary>
public sealed class ShieldBlockRecoveryTests
{
    /// <summary>
    /// The shipped combat preset since the shield-size package. Its roster
    /// fields both shields and all three ranged weapons.
    /// </summary>
    private const CombatPresetId ShippedCombat =
        CombatPresetId.PrecolonialPhilippinesV7;

    /// <summary>The shipped movement preset since the same package.</summary>
    private const MovementPresetId ShippedMovement =
        MovementPresetId.ShieldEncumbranceV14;

    [Fact]
    public void TheShippedPresetAppliesTheWindowAndAuthorsALongerOneForTheBiggerShield()
    {
        var movement = MovementPresetRegistry.Get(ShippedMovement);

        Assert.True(movement.AppliesShieldBlockRecovery);

        // Literals, not the constants under test. Twenty ticks a second, so
        // five ticks is a quarter of a second and three is a hundred and
        // fifty milliseconds — quick, which is what the requirement asked
        // for.
        Assert.Equal(
            5,
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.TallHardwood));
        Assert.Equal(
            3,
            movement.ResolveShieldBlockRecoveryTicks(
                ShieldId.NarrowBreastHigh));
        Assert.Equal(
            0,
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.None));
    }

    /// <summary>
    /// The bigger shield's window is strictly the longer of the two. This is
    /// the second reason to prefer the smaller shield, and it is what stops
    /// the big shield being a strict upgrade if the pace difference alone is
    /// ever judged too small to feel.
    /// </summary>
    [Fact]
    public void TheBiggerShieldTakesLongerToBringBackIntoGuard()
    {
        var movement = MovementPresetRegistry.Get(ShippedMovement);

        Assert.True(
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.TallHardwood) >
            movement.ResolveShieldBlockRecoveryTicks(
                ShieldId.NarrowBreastHigh));
        Assert.True(
            movement.ResolveShieldBlockRecoveryTicks(
                ShieldId.NarrowBreastHigh) >
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.None));
    }

    /// <summary>
    /// Every preset before the shield-size package leaves the effect off and
    /// resolves a zero duration for every shield, including the new one. That
    /// is what keeps the five recorded gate baselines from moving: a preset
    /// that never opens a window never folds a non-zero counter.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.RangedStandoffV8)]
    [InlineData(MovementPresetId.BattlefieldRealismV10)]
    [InlineData(MovementPresetId.LastStandEngagementV11)]
    [InlineData(MovementPresetId.CohortLateralSpreadV13)]
    public void EveryEarlierPresetLeavesTheWindowClosed(MovementPresetId id)
    {
        var movement = MovementPresetRegistry.Get(id);

        Assert.False(movement.AppliesShieldBlockRecovery);
        Assert.Equal(
            0,
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.TallHardwood));
        Assert.Equal(
            0,
            movement.ResolveShieldBlockRecoveryTicks(
                ShieldId.NarrowBreastHigh));
        Assert.Equal(
            0,
            movement.ResolveShieldBlockRecoveryTicks(ShieldId.None));
    }

    /// <summary>
    /// The end-to-end proof: run the shipped presets and watch the counter
    /// actually open on a real block, stay within its authored ceiling, and
    /// never go negative. A shield that never opened a window would leave
    /// this vacuous, so the test fails rather than passes when it never
    /// observes one.
    /// </summary>
    [Fact]
    public void ARealBattleOpensTheWindowAndNeverExceedsTheAuthoredCeiling()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 120) with
        {
            CombatPreset = ShippedCombat,
            MovementPreset = ShippedMovement,
        };
        var simulation = BattleSimulation.Create(scenario);

        var everObserved = false;
        var highestObserved = 0;

        for (var tick = 0; tick < 600; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.CreateSnapshot().Agents)
            {
                var remaining = agent.ShieldBlockRecoveryTicksRemaining;

                Assert.True(
                    remaining >= 0,
                    $"Agent {agent.EntityId} carried a negative window of " +
                    $"{remaining} at tick {simulation.Tick}.");

                // Five is the tall shield's authored duration and the longest
                // window any shield in the game can open, written as a
                // literal so raising the constant cannot quietly raise the
                // ceiling this test enforces.
                Assert.True(
                    remaining <= 5,
                    $"Agent {agent.EntityId} carried a window of " +
                    $"{remaining} at tick {simulation.Tick}, above the " +
                    "longest duration any shield authors.");

                if (remaining > 0)
                {
                    everObserved = true;
                    highestObserved = Math.Max(highestObserved, remaining);
                }
            }
        }

        Assert.True(
            everObserved,
            "No warrior ever opened a block-recovery window across six " +
            "hundred ticks of the shipped presets, so this test proved " +
            "nothing about the effect it exists to cover.");
        Assert.True(highestObserved > 0);
    }

    /// <summary>
    /// A battle under a preset that does not apply the effect must leave the
    /// counter at zero on every agent for its whole length. This is the
    /// negative control for the fold gate: if it ever moved, an unmoved
    /// baseline would be luck rather than design.
    /// </summary>
    [Fact]
    public void ABattleUnderAnEarlierPresetNeverOpensAWindow()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 120) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = MovementPresetId.CohortLateralSpreadV13,
        };
        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 600; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.CreateSnapshot().Agents)
            {
                Assert.Equal(0, agent.ShieldBlockRecoveryTicksRemaining);
            }
        }
    }
}
