using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class MatchSeries
{
    private const ulong SeedIncrement = 0x9E3779B97F4A7C15UL;
    private readonly ulong _initialSeed;

    public MatchSeries(ulong initialSeed)
    {
        _initialSeed = initialSeed;
        CurrentSeed = initialSeed;
    }

    public int TeamAWins { get; private set; }

    public int TeamBWins { get; private set; }

    public ulong CurrentSeed { get; private set; }

    public void StartNextRound(BattleOutcome outgoingOutcome)
    {
        switch (outgoingOutcome)
        {
            case BattleOutcome.Faction0Victory:
                TeamAWins = checked(TeamAWins + 1);
                break;
            case BattleOutcome.Faction1Victory:
                TeamBWins = checked(TeamBWins + 1);
                break;
        }

        CurrentSeed = unchecked(CurrentSeed + SeedIncrement);
    }

    public void FullReset()
    {
        TeamAWins = 0;
        TeamBWins = 0;
        CurrentSeed = _initialSeed;
    }
}
