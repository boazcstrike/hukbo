namespace Hukbo.Client.Presentation;

internal sealed record MatchSummary(
    string WinnerLabel,
    int BlueSurvivors,
    int RedSurvivors,
    long TerminalTick,
    double SimulatedDurationSeconds,
    ulong Seed);
