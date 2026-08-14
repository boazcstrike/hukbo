namespace Sandata.Headless;

/// <summary>
/// The machine-readable environment a run executed under. Mirrors
/// <c>Hukbo.Headless.RunEnvironment</c> field for field, so a caller diffing
/// two JSON reports across the two games' runners does not have to learn a
/// second shape for the same information.
/// </summary>
public sealed record RunEnvironment(
    string OperatingSystem,
    string Framework,
    string ProcessArchitecture,
    int ProcessorCount);

/// <summary>
/// Wall-clock duration percentiles across every measured tick, in
/// milliseconds. Wall-clock time here is a reported measurement only — it
/// never feeds a simulation decision; the mission's own tick counter is
/// authoritative, per <c>CLAUDE.md</c> section 5.
/// </summary>
public sealed record TickPercentiles(
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds);

/// <summary>
/// The complete report <see cref="HeadlessRunner.Execute"/> hands back to
/// <c>Program</c> for one seeded determinism workload — task 51 of
/// Sandata's scaffold plan. Serialized to indented,
/// camelCase JSON on stdout, and optionally to <c>--output</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No expected-hash literal belongs here or anywhere near it.</b> The
/// wave-11 audit (Sandata's scaffold plan) forbids this
/// task from pinning a golden Sandata hash: task 77, landing in the same
/// batch, moves <c>SandataRuleset.ContentHash</c>, which moves every mission
/// hash with it. Every assertion this report feeds is a self-consistency
/// check — two runs of one seed agree with each other, a resumed run agrees
/// with an uninterrupted one — never a comparison against a value typed into
/// a test file.
/// </para>
/// <para>
/// <b><see cref="StateHash"/></b> is <c>SandataSimulation.LastStateHash</c>
/// at the end of the run, formatted as sixteen uppercase hex digits.
/// <b><see cref="EventHash"/></b> is the final tick's
/// <c>MissionState.EventFeed.Hash</c>, task 76's rolling FNV-1a fold over
/// every event ever appended to the feed — deliberately independent of the
/// state hash (design section 4), and, unlike the state hash, never gated by
/// a cadence: the feed folds every event into its hash the moment the event
/// is appended, so it reflects the full ordered event stream even though the
/// feed itself retains only the most recent 200 events.
/// </para>
/// </remarks>
public sealed record RunReport(
    RunEnvironment Environment,
    ulong Seed,
    int OperatorsPerFaction,
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
    long? FirstMismatchTick);
