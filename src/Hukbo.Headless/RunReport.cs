using Hukbo.Core.Simulation;

namespace Hukbo.Headless;

public sealed record RunEnvironment(
    string OperatingSystem,
    string Framework,
    string ProcessArchitecture,
    int ProcessorCount);

public sealed record TickPercentiles(
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds);

public sealed record RunReport(
    RunEnvironment Environment,
    ulong Seed,
    int AgentCount,
    int RequestedTicks,
    long MeasuredTicks,
    double DurationMilliseconds,
    TickPercentiles TickPercentiles,
    long AllocatedBytes,
    string Outcome,
    int Faction0Survivors,
    int Faction1Survivors,
    string EventHash,
    string StateHash,
    bool Deterministic,
    long? FirstMismatchTick,
    CollisionMetrics CollisionMetrics,
    // Defaulted so that a caller reading an older report, or a test building
    // one by hand, does not have to supply a block it does not care about. The
    // runner always populates it.
    CombatMetrics CombatMetrics = default);
