using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Behavioural coverage for the four evasive rungs of the 2026-08-15 in-fight
/// evasion design, section 5, exercised over real battles rather than over
/// hand-pinned endpoints.
/// </summary>
/// <remarks>
/// <para>
/// The invariants below are the ones that decide whether this feature is
/// movement <i>during</i> a fight or another way of leaving one, and every one
/// of them is a property of the ladder rather than of a particular tuning
/// constant. That is deliberate: plan task 13 tunes those constants against the
/// anti-goal bars, and a test pinning an exact endpoint would have to be
/// rewritten every time a period or an offset moved, which makes it a cost
/// rather than a check.
/// </para>
/// <para>
/// The one thing these cannot do is prove the rungs look right on screen. That
/// is what the smoke checklist is for, and no test here may close one of its
/// rows.
/// </para>
/// </remarks>
public sealed class EvasiveRungTests
{
    private static BattleSimulation CreateRun(
        MovementPresetId movementPreset,
        ulong seed = 1,
        int totalAgents = 200)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents) with
        {
            MovementPreset = movementPreset,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
        };
        scenario.Validate();
        return BattleSimulation.Create(scenario);
    }

    /// <summary>
    /// Every legacy preset leaves the field at
    /// <see cref="EvasiveAction.None"/> forever. This is the negative that
    /// keeps thirteen recorded baselines where they are.
    /// </summary>
    [Fact]
    public void CohortLateralSpreadV13NeverResolvesAnEvasiveAction()
    {
        var simulation = CreateRun(MovementPresetId.CohortLateralSpreadV13);

        for (var tick = 0; tick < 600; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                Assert.Equal(EvasiveAction.None, agent.EvasiveAction);
            }
        }
    }

    /// <summary>
    /// A dead warrior never carries an evasive action. Corpses are still folded
    /// into the state hash, and the pass that clears the footwork fields of the
    /// dead is gated on a flag this preset does not set, so the evasive stage
    /// has to do its own clearing.
    /// </summary>
    [Fact]
    public void ADeadWarriorNeverCarriesAnEvasiveAction()
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14);
        var sawADeadWarrior = false;

        for (var tick = 0; tick < 1200; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                if (agent.IsAlive)
                {
                    continue;
                }

                sawADeadWarrior = true;
                Assert.Equal(EvasiveAction.None, agent.EvasiveAction);
            }
        }

        Assert.True(
            sawADeadWarrior,
            "Nobody died in 1,200 ticks, so this proved nothing about corpses.");
    }

    /// <summary>
    /// No rung ever fires for a warrior that is leaving the fight. The ranged
    /// retreat rung writes <see cref="AgentIntent.BackingAway"/> and the
    /// last-stand regroup writes <see cref="AgentIntent.Regrouping"/>; a
    /// warrior in either state must be untouched, which is what keeps this
    /// feature from becoming a second retreat.
    /// </summary>
    [Fact]
    public void NoRungFiresForAWarriorThatIsLeavingTheFight()
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14);

        for (var tick = 0; tick < 1200; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                if (agent.Intent is AgentIntent.Moving or AgentIntent.Attacking)
                {
                    continue;
                }

                Assert.Equal(EvasiveAction.None, agent.EvasiveAction);
            }
        }
    }

    /// <summary>
    /// A warrior that resolved an evasive action still holds the enemy it
    /// selected. Keeping the target is the difference between working around an
    /// opponent and abandoning it, and no rung is permitted to write the intent
    /// or clear the target.
    /// </summary>
    [Fact]
    public void AWarriorThatEvadedStillHoldsItsTarget()
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14);
        var sawAnEvasion = false;

        for (var tick = 0; tick < 1200; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                if (agent.EvasiveAction == EvasiveAction.None)
                {
                    continue;
                }

                sawAnEvasion = true;
                Assert.True(agent.IsAlive);
                Assert.NotNull(agent.TargetEntityId);
                Assert.True(
                    agent.Intent is AgentIntent.Moving or AgentIntent.Attacking,
                    $"A warrior resolving {agent.EvasiveAction} held intent " +
                    $"{agent.Intent}, which no rung is allowed to produce.");
            }
        }

        Assert.True(sawAnEvasion, "No warrior evaded, so this proved nothing.");
    }

    /// <summary>
    /// All four rungs reach a real battle. A rung that never fires is dead code
    /// wearing a test, which is exactly what the give-ground rung was until its
    /// trigger stopped using the body-contact distance the collision resolver
    /// makes unreachable.
    /// </summary>
    [Fact]
    public void EveryRungFiresAtLeastOnceInARealBattle()
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14);
        var slip = 0;
        var dodge = 0;
        var giveGround = 0;
        var breakOff = 0;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                switch (agent.EvasiveAction)
                {
                    case EvasiveAction.SlipLateral:
                        slip++;
                        break;
                    case EvasiveAction.DodgeIncoming:
                        dodge++;
                        break;
                    case EvasiveAction.GiveGround:
                        giveGround++;
                        break;
                    case EvasiveAction.BreakOff:
                        breakOff++;
                        break;
                    default:
                        break;
                }
            }
        }

        Assert.True(slip > 0, "The slip rung never fired.");
        Assert.True(dodge > 0, "The dodge rung never fired.");
        Assert.True(giveGround > 0, "The give-ground rung never fired.");
        Assert.True(breakOff > 0, "The break-off rung never fired.");
    }

    /// <summary>
    /// Evasion stays a minority behaviour. The floor proves the feature is
    /// alive; the ceiling proves it has not taken the battle over, which is
    /// design section 8's bar 7.
    /// </summary>
    [Fact]
    public void EvasionStaysAMinorityOfLivingAgentTicks()
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14);
        var livingAgentTicks = 0L;
        var evasiveAgentTicks = 0L;
        var giveGroundAgentTicks = 0L;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                if (!agent.IsAlive)
                {
                    continue;
                }

                livingAgentTicks++;
                if (agent.EvasiveAction != EvasiveAction.None)
                {
                    evasiveAgentTicks++;
                }

                if (agent.EvasiveAction == EvasiveAction.GiveGround)
                {
                    giveGroundAgentTicks++;
                }
            }
        }

        Assert.True(livingAgentTicks > 0);
        Assert.True(evasiveAgentTicks > 0);

        // Asserted against literals rather than against the tuning constants,
        // so flipping a period cannot move the bar along with the behaviour it
        // is supposed to be checking.
        Assert.True(
            evasiveAgentTicks * 100 <= livingAgentTicks * 40,
            $"Evasion covered {evasiveAgentTicks} of {livingAgentTicks} living " +
            "agent-ticks, above the forty per cent ceiling.");
        Assert.True(
            giveGroundAgentTicks * 100 <= livingAgentTicks * 10,
            $"Give-ground covered {giveGroundAgentTicks} of {livingAgentTicks} " +
            "living agent-ticks, above the ten per cent ceiling.");
    }

    /// <summary>
    /// The battle still ends, and it ends decisively. This is the failure that
    /// closed out the V6 and V7 line — two sides that could not commit — and it
    /// is the single most important thing more not-attacking movement could
    /// break.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void TheBattleStillReachesADecisiveOutcome(ulong seed)
    {
        var simulation = CreateRun(MovementPresetId.EvasiveFootworkV14, seed);

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Outcome is BattleOutcome.Faction0Victory
                or BattleOutcome.Faction1Victory,
            $"Seed {seed} ended {simulation.Outcome} at tick {simulation.Tick}.");
        Assert.True(
            simulation.Tick < 5000,
            $"Seed {seed} took {simulation.Tick} ticks, past the 5,000-tick bar.");
    }
}
