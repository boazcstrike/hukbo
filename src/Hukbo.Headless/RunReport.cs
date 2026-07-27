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

/// <param name="AllocatedBytes">
/// Managed bytes allocated on the calling thread across the whole measured
/// loop: both simulations, both state-hash computations, the determinism
/// comparison, and all per-tick metric accumulation. The harness-total
/// figure; its meaning is unchanged from earlier reports.
/// </param>
/// <param name="CoreAllocatedBytes">
/// Managed bytes allocated on the calling thread by the run-under-test
/// simulation's <c>AdvanceOneTick</c> calls only, summed across every tick.
/// Excludes the determinism twin's advance, both state-hash computations,
/// the event-sequence comparison, and all per-tick metric accumulation.
/// Always less than or equal to <see cref="AllocatedBytes"/>. Defaulted, for
/// the same reason as <see cref="CombatMetrics"/>, so a caller reading an
/// older report or a test building one by hand does not have to supply a
/// figure it does not care about; the runner always populates it.
/// </param>
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
    CombatMetrics CombatMetrics = default,
    long CoreAllocatedBytes = 0);
