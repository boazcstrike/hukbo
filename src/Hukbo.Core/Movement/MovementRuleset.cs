using Hukbo.Core.Determinism;

namespace Hukbo.Core.Movement;

/// <summary>
/// Immutable, versioned movement configuration: the persistent-contingent
/// cohesion tunables, the cohesion duty cycle, the arrival taper, and the
/// per-agent random-offset unit, plus a <see cref="ContentHash"/> computed
/// the same way <see cref="Combat.CombatRuleset.ContentHash"/> is. Gameplay
/// tuning values here are game-design choices, not historical measurements —
/// no source describes a unit cohesion radius, a duty cycle, or an arrival
/// taper; see docs/plans/2026-07-28-formation-movement-realism-design.md
/// section 3 for the derivations.
/// </summary>
/// <remarks>
/// What stays frozen is each preset's simulated behaviour, proved
/// byte-identically by its own digest fixture in
/// <c>MovementPresetFreezeTests</c> — not this type's field list.
/// <see cref="ContentHash"/> is folded over every field below, but it never
/// reaches the state hash: <c>BattleSimulation.ComputeStateHash</c> folds
/// <c>_rules.ContentHash</c>, where <c>_rules</c> is the
/// <c>CombatRuleset</c> (src/Hukbo.Core/Simulation/BattleSimulation.cs:18-19,
/// 393), and <c>StateHasher.Compute</c> never receives a
/// <see cref="MovementRuleset"/> at all. Adding a field here therefore cannot
/// move any preset's state hash, event hash, outcome, or recorded digest;
/// what it does move is the pinned <c>ContentHash</c> identity literals in
/// <c>MovementPresetRegistryTests</c>, which must be recomputed from the
/// built code whenever a field is added, never calculated by hand. See
/// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3.
/// </remarks>
public sealed class MovementRuleset
{
    public MovementRuleset(
        MovementPresetId id,
        int version,
        int cohesionRadiusMultiplier,
        int closeRadiusMultiplier,
        int closeFractionNumerator,
        int closeFractionDenominator,
        int minimumCohesiveMembers,
        int cohesionCycleTicks,
        int cohesionDutyTicks,
        int arrivalTaperMultiplier,
        int offsetUnit,
        bool narrowsCohesionScanToCohesionCapableContingents)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        Id = id;
        Version = version;
        CohesionRadiusMultiplier = cohesionRadiusMultiplier;
        CloseRadiusMultiplier = closeRadiusMultiplier;
        CloseFractionNumerator = closeFractionNumerator;
        CloseFractionDenominator = closeFractionDenominator;
        MinimumCohesiveMembers = minimumCohesiveMembers;
        CohesionCycleTicks = cohesionCycleTicks;
        CohesionDutyTicks = cohesionDutyTicks;
        ArrivalTaperMultiplier = arrivalTaperMultiplier;
        OffsetUnit = offsetUnit;
        NarrowsCohesionScanToCohesionCapableContingents =
            narrowsCohesionScanToCohesionCapableContingents;
        ContentHash = ComputeContentHash();
    }

    public MovementPresetId Id { get; }

    public int Version { get; }

    /// <summary>
    /// Multiplied by the agent body radius to give <c>cohesionRadiusRaw</c>,
    /// the spread at which a contingent stops being judged gathered. A
    /// game-design choice, not a measurement.
    /// </summary>
    public int CohesionRadiusMultiplier { get; }

    /// <summary>
    /// Multiplied by the agent body radius to give <c>closeRadiusRaw</c>, the
    /// distance to the nearest enemy at which a contingent breaks off
    /// cohesion for contact. A game-design choice, not a measurement.
    /// </summary>
    public int CloseRadiusMultiplier { get; }

    /// <summary>
    /// The numerator of the fraction of a contingent's living members whose
    /// selected target lies within <c>closeRadiusRaw</c> needed for the
    /// contingent to enter <c>ContingentState.Close</c>. A game-design
    /// choice, not a measurement.
    /// </summary>
    public int CloseFractionNumerator { get; }

    /// <summary>
    /// The denominator of the fraction of a contingent's living members
    /// whose selected target lies within <c>closeRadiusRaw</c> needed for
    /// the contingent to enter <c>ContingentState.Close</c>. A game-design
    /// choice, not a measurement.
    /// </summary>
    public int CloseFractionDenominator { get; }

    /// <summary>
    /// The living-member floor below which a contingent breaks on attrition
    /// regardless of its casualty ratio, so a contingent reduced to a
    /// handful of survivors is not asked to keep gathering. A game-design
    /// choice, not a measurement.
    /// </summary>
    public int MinimumCohesiveMembers { get; }

    /// <summary>
    /// The length, in ticks, of the cohesion duty cycle every contingent's
    /// gathering window is staggered across. A game-design choice, not a
    /// measurement.
    /// </summary>
    public int CohesionCycleTicks { get; }

    /// <summary>
    /// The number of ticks, out of every <see cref="CohesionCycleTicks"/>,
    /// during which a contingent's duty-cycle window is open and cohesion may
    /// be granted at all. A game-design choice, not a measurement.
    /// </summary>
    public int CohesionDutyTicks { get; }

    /// <summary>
    /// Multiplied by the agent body radius to give <c>taperRaw</c>, the
    /// remaining distance inside which an arriving warrior's movement step is
    /// tapered rather than full speed. A game-design choice, not a
    /// measurement.
    /// </summary>
    public int ArrivalTaperMultiplier { get; }

    /// <summary>
    /// The half-width of the raw draw <c>ContingentOffset.Compute</c> takes
    /// before scaling it into world units, so a member's personal offset is
    /// resolved to the same precision regardless of the jitter radius it is
    /// scaled against. A game-design choice, not a measurement.
    /// </summary>
    public int OffsetUnit { get; }

    /// <summary>
    /// Whether movement gate 6, the cross-contingent bias-square overlap test
    /// of design section 3.5, walks only those same-faction contingents that
    /// could actually be granted cohesion this tick — skipping any whose
    /// tick-start <see cref="Simulation.ContingentState"/> is
    /// <see cref="Simulation.ContingentState.Close"/> or
    /// <see cref="Simulation.ContingentState.Break"/> — rather than every
    /// living contingent. A game-design choice, not a measurement. Registered
    /// <see langword="false"/> for every preset up to and including
    /// <see cref="MovementPresetId.PersistentContingentsV3"/>, so introducing
    /// this field moves no existing preset's behaviour; only
    /// <see cref="MovementPresetId.PersistentContingentsV4"/> registers it
    /// <see langword="true"/>.
    /// </summary>
    public bool NarrowsCohesionScanToCohesionCapableContingents { get; }

    /// <summary>
    /// Content hash over every field above, folded in declaration order with
    /// the same FNV-1a primitive <see cref="Combat.CombatRuleset.ContentHash"/>
    /// uses. Two rulesets with identical fields hash identically regardless of
    /// which values were supplied by name at construction.
    /// </summary>
    public ulong ContentHash { get; }

    private ulong ComputeContentHash()
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, (ulong)Id);
        Fnv1a.Add(ref hash, (ulong)Version);
        Fnv1a.Add(ref hash, (ulong)CohesionRadiusMultiplier);
        Fnv1a.Add(ref hash, (ulong)CloseRadiusMultiplier);
        Fnv1a.Add(ref hash, (ulong)CloseFractionNumerator);
        Fnv1a.Add(ref hash, (ulong)CloseFractionDenominator);
        Fnv1a.Add(ref hash, (ulong)MinimumCohesiveMembers);
        Fnv1a.Add(ref hash, (ulong)CohesionCycleTicks);
        Fnv1a.Add(ref hash, (ulong)CohesionDutyTicks);
        Fnv1a.Add(ref hash, (ulong)ArrivalTaperMultiplier);
        Fnv1a.Add(ref hash, (ulong)OffsetUnit);
        Fnv1a.Add(
            ref hash,
            NarrowsCohesionScanToCohesionCapableContingents ? 1UL : 0UL);
        return hash;
    }
}
