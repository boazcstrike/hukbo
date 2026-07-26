using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal static class MatchSummaryFactory
{
    public static MatchSummary Create(
        BattleOutcome outcome,
        IReadOnlyList<AgentView> agents,
        long tick,
        int tickRate,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickRate);

        var winnerLabel = outcome switch
        {
            BattleOutcome.Faction0Victory => "Blue",
            BattleOutcome.Faction1Victory => "Red",
            BattleOutcome.Draw => "Draw",
            BattleOutcome.Ongoing => throw new ArgumentException(
                "A match summary requires a terminal outcome.",
                nameof(outcome)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unknown battle outcome."),
        };

        var blueSurvivors = 0;
        var redSurvivors = 0;
        foreach (var agent in agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                blueSurvivors++;
            }
            else if (agent.FactionId == 1)
            {
                redSurvivors++;
            }
        }

        return new MatchSummary(
            winnerLabel,
            blueSurvivors,
            redSurvivors,
            tick,
            (double)tick / tickRate,
            seed);
    }
}
