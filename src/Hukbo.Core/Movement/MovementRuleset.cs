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
/// The constant set is closed at this type's introduction and stays closed.
/// <see cref="ContentHash"/> is folded over every field below, and
/// <c>IndependentPursuitV1</c>'s pinned <see cref="ContentHash"/> literal is
/// frozen — see design section 6.2. Adding a field here later, when the
/// persistent-contingent behaviour lands, would move that literal and break
/// the freeze on the very change that is supposed to leave
/// <c>IndependentPursuitV1</c> untouched. Every constant the behaviour will
/// eventually need is therefore declared now, at its frozen-preset value,
/// even though nothing under <c>IndependentPursuitV1</c> reads any of them.
/// </remarks>
public sealed class MovementRuleset
{
    public MovementRuleset(
        MovementPresetId id,
        int version,
        int cohesionRadiusMultiplier,
        int closeRadiusMultiplier,
        int minimumCohesiveMembers,
        int cohesionCycleTicks,
        int cohesionDutyTicks,
        int arrivalTaperMultiplier,
        int offsetUnit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        Id = id;
        Version = version;
        CohesionRadiusMultiplier = cohesionRadiusMultiplier;
        CloseRadiusMultiplier = closeRadiusMultiplier;
        MinimumCohesiveMembers = minimumCohesiveMembers;
        CohesionCycleTicks = cohesionCycleTicks;
        CohesionDutyTicks = cohesionDutyTicks;
        ArrivalTaperMultiplier = arrivalTaperMultiplier;
        OffsetUnit = offsetUnit;
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
        Fnv1a.Add(ref hash, (ulong)MinimumCohesiveMembers);
        Fnv1a.Add(ref hash, (ulong)CohesionCycleTicks);
        Fnv1a.Add(ref hash, (ulong)CohesionDutyTicks);
        Fnv1a.Add(ref hash, (ulong)ArrivalTaperMultiplier);
        Fnv1a.Add(ref hash, (ulong)OffsetUnit);
        return hash;
    }
}
