using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// RU-13: <see cref="RangedPhase"/> and <see cref="RangedPhaseProjection"/> are
/// a derived projection, never stored, hashed, or snapshotted — see
/// ranged-units design section 8.1. These tests cover the projection directly
/// (the pure derivation is the actual unit under test) and, separately, that a
/// melee-only battle under the shipped default preset never observes anything
/// but <see cref="RangedPhase.None"/>.
/// </summary>
public sealed class AgentViewTests
{
    private const int AttackCooldownTicks = 100;

    [Fact]
    public void RangedPhaseNumericValuesArePinned()
    {
        Assert.Equal(0, (int)RangedPhase.None);
        Assert.Equal(1, (int)RangedPhase.Ready);
        Assert.Equal(2, (int)RangedPhase.Load);
        Assert.Equal(3, (int)RangedPhase.Draw);
        Assert.Equal(4, (int)RangedPhase.Release);
        Assert.Equal(5, (int)RangedPhase.Recover);
        Assert.Equal(6, Enum.GetValues<RangedPhase>().Length);
    }

    [Fact]
    public void DefaultAgentViewCarriesNoRangedPhase()
    {
        var view = default(AgentView);

        Assert.Equal(RangedPhase.None, view.RangedPhase);
        Assert.Equal(0, view.RangedPhaseTicksRemaining);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void Derive_ReturnsNoneForEveryMeleeWeaponAtEveryCooldownValue(WeaponId weapon)
    {
        for (var remaining = AttackCooldownTicks; remaining >= 0; remaining--)
        {
            var (phase, ticksRemaining) = RangedPhaseProjection.Derive(
                weapon,
                remaining,
                AttackCooldownTicks);

            Assert.Equal(RangedPhase.None, phase);
            Assert.Equal(0, ticksRemaining);
        }
    }

    [Theory]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void Derive_ReportsReadyAndZeroTicksAtOrBelowAZeroCooldown(WeaponId weapon)
    {
        var (phase, ticksRemaining) = RangedPhaseProjection.Derive(weapon, 0, AttackCooldownTicks);
        Assert.Equal(RangedPhase.Ready, phase);
        Assert.Equal(0, ticksRemaining);
    }

    [Theory]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void Derive_WalksExactlyReleaseRecoverLoadDrawReadyAsTheCooldownCountsDown(
        WeaponId weapon)
    {
        var observedPhaseOrder = new List<RangedPhase>();
        var previousPhase = RangedPhase.None;
        var previousTicksRemaining = int.MaxValue;

        for (var remaining = AttackCooldownTicks; remaining >= 0; remaining--)
        {
            var (phase, ticksRemaining) = RangedPhaseProjection.Derive(
                weapon,
                remaining,
                AttackCooldownTicks);

            if (phase != previousPhase)
            {
                observedPhaseOrder.Add(phase);
                previousPhase = phase;
            }
            else
            {
                Assert.True(
                    ticksRemaining < previousTicksRemaining,
                    $"{weapon} phase {phase}: RangedPhaseTicksRemaining " +
                    $"{ticksRemaining} did not strictly decrease from " +
                    $"{previousTicksRemaining} at cooldown {remaining}.");
            }

            previousTicksRemaining = ticksRemaining;
        }

        Assert.Equal(
            new[]
            {
                RangedPhase.Release,
                RangedPhase.Recover,
                RangedPhase.Load,
                RangedPhase.Draw,
                RangedPhase.Ready,
            },
            observedPhaseOrder);
    }

    [Fact]
    public void MeleeAgentViewsReportNoneAcrossMultipleTicksUnderTheShippedDefaultPreset()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 4);
        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 50; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.All(
                simulation.Agents,
                view =>
                {
                    Assert.Equal(RangedPhase.None, view.RangedPhase);
                    Assert.Equal(0, view.RangedPhaseTicksRemaining);
                });
        }
    }
}
