using System.Collections.Immutable;
using Hukbo.Core.Combat;
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
    /// <summary>
    /// The number of canonical loadouts — <c>KP, WA, KA, IT, KS, IS</c> — a
    /// preset with equipment-relative footwork must carry one profile row
    /// for. Equal in value to
    /// <see cref="LoadoutMovementProfile.OpponentDistanceOffsetCount"/>
    /// because every row also carries one offset cell per canonical
    /// opponent, but the two constants name different shapes.
    /// </summary>
    public const int CanonicalLoadoutCount = 6;

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
        bool narrowsCohesionScanToCohesionCapableContingents,
        bool selectsLeaderByRank,
        bool usesEquipmentRelativeFootwork,
        int immediateRadiusBodyDiametersBasisPoints,
        int supportRadiusBodyDiametersBasisPoints,
        ImmutableArray<LoadoutMovementProfile> loadoutMovementProfiles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ValidateEquipmentRelativeFootworkCoupling(
            usesEquipmentRelativeFootwork,
            immediateRadiusBodyDiametersBasisPoints,
            supportRadiusBodyDiametersBasisPoints,
            loadoutMovementProfiles);

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
        SelectsLeaderByRank = selectsLeaderByRank;
        UsesEquipmentRelativeFootwork = usesEquipmentRelativeFootwork;
        ImmediateRadiusBodyDiametersBasisPoints =
            immediateRadiusBodyDiametersBasisPoints;
        SupportRadiusBodyDiametersBasisPoints =
            supportRadiusBodyDiametersBasisPoints;
        LoadoutMovementProfiles = loadoutMovementProfiles;
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
    /// Whether <c>MovementRules.ScanContingentLeadersAndLivingCounts</c>
    /// orders leader candidates by <c>(RankId ascending, EntityId
    /// ascending)</c> instead of <c>EntityId</c> alone. A game-design
    /// choice about which comparator decides a contingent's leader, not a
    /// measurement. Registered <see langword="false"/> for every preset up
    /// to and including <see cref="MovementPresetId.PersistentContingentsV4"/>,
    /// so introducing this field moves no existing preset's leader
    /// selection; only <see cref="MovementPresetId.PersistentContingentsV5"/>
    /// registers it <see langword="true"/>.
    /// </summary>
    public bool SelectsLeaderByRank { get; }

    /// <summary>
    /// Whether this preset resolves an equipment-relative movement profile
    /// per loadout and runs the weapon-relative footwork pipeline. Registered
    /// <see langword="false"/> with zero context radii and an empty profile
    /// collection for every preset up to and including
    /// <see cref="MovementPresetId.PersistentContingentsV5"/>, so introducing
    /// this field moves no existing preset's behaviour; only
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> registers it
    /// <see langword="true"/>. A game-design choice, not a measurement.
    /// </summary>
    public bool UsesEquipmentRelativeFootwork { get; }

    /// <summary>
    /// Basis points of body diameter giving the immediate local-context
    /// radius — the scan inside which allies and enemies count as immediate
    /// neighbours. Zero for every preset whose
    /// <see cref="UsesEquipmentRelativeFootwork"/> is <see langword="false"/>.
    /// A game-design choice, not a measurement.
    /// </summary>
    public int ImmediateRadiusBodyDiametersBasisPoints { get; }

    /// <summary>
    /// Basis points of body diameter giving the support local-context
    /// radius — the wider scan feeding the disengage ratio counts. Zero for
    /// every preset whose <see cref="UsesEquipmentRelativeFootwork"/> is
    /// <see langword="false"/>. A game-design choice, not a measurement.
    /// </summary>
    public int SupportRadiusBodyDiametersBasisPoints { get; }

    /// <summary>
    /// The per-loadout movement profile rows, exactly six and in canonical
    /// <c>KP, WA, KA, IT, KS, IS</c> order when
    /// <see cref="UsesEquipmentRelativeFootwork"/> is <see langword="true"/>,
    /// otherwise empty. The stored order doubles as the fixed-size lookup
    /// <see cref="ResolveLoadoutProfile"/> indexes into, so it is validated
    /// at construction and never sorted again.
    /// </summary>
    public ImmutableArray<LoadoutMovementProfile> LoadoutMovementProfiles { get; }

    /// <summary>
    /// Content hash over every field above, folded in declaration order with
    /// the same FNV-1a primitive <see cref="Combat.CombatRuleset.ContentHash"/>
    /// uses. Two rulesets with identical fields hash identically regardless of
    /// which values were supplied by name at construction.
    /// </summary>
    public ulong ContentHash { get; }

    /// <summary>
    /// Resolves the movement profile for one warrior's equipment. The key is
    /// <c>(WeaponId, ArmorId, ShieldId)</c> and is rank-independent: rank is
    /// social standing with no movement meaning, so
    /// <paramref name="loadout"/>'s <see cref="CombatLoadout.Rank"/> is never
    /// read and two loadouts differing only in rank resolve to the same
    /// profile row. Throws for an unmapped key — including every key under a
    /// preset whose <see cref="UsesEquipmentRelativeFootwork"/> is
    /// <see langword="false"/> — rather than returning a default, so a future
    /// armor or shield fails loudly instead of silently inheriting another
    /// row's footwork.
    /// </summary>
    public LoadoutMovementProfile ResolveLoadoutProfile(CombatLoadout loadout)
    {
        var index = CanonicalLoadoutIndex(
            loadout.Weapon, loadout.Armor, loadout.Shield);
        if (index < 0 || index >= LoadoutMovementProfiles.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(loadout),
                loadout,
                "No movement profile is registered for this loadout under " +
                $"movement preset {Id}.");
        }

        return LoadoutMovementProfiles[index];
    }

    /// <summary>
    /// Maps an equipment key to its canonical loadout index — <c>KP</c> 0,
    /// <c>WA</c> 1, <c>KA</c> 2, <c>IT</c> 3, <c>KS</c> 4, <c>IS</c> 5 — or
    /// -1 for a key no profile row may carry. The canonical order is binding
    /// on the stored profile collection and on the content-hash fold.
    /// </summary>
    private static int CanonicalLoadoutIndex(
        WeaponId weapon, ArmorId armor, ShieldId shield) =>
        (weapon, armor, shield) switch
        {
            (WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None) => 0,
            (WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None) => 1,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None) => 2,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None) => 3,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood) => 4,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood) => 5,
            _ => -1,
        };

    /// <summary>
    /// The coupled validation of design section 5: a preset without
    /// equipment-relative footwork carries zero radii and no profile rows,
    /// and a preset with it carries strictly positive radii and exactly the
    /// six canonical rows, each appearing once, in canonical order. A
    /// duplicate key, a missing canonical row, an unsupported shield, or an
    /// unsupported armor fails construction here.
    /// </summary>
    private static void ValidateEquipmentRelativeFootworkCoupling(
        bool usesEquipmentRelativeFootwork,
        int immediateRadiusBodyDiametersBasisPoints,
        int supportRadiusBodyDiametersBasisPoints,
        ImmutableArray<LoadoutMovementProfile> loadoutMovementProfiles)
    {
        if (loadoutMovementProfiles.IsDefault)
        {
            throw new ArgumentException(
                "The profile collection must be supplied; pass " +
                "ImmutableArray<LoadoutMovementProfile>.Empty for a preset " +
                "without equipment-relative footwork.",
                nameof(loadoutMovementProfiles));
        }

        if (!usesEquipmentRelativeFootwork)
        {
            if (immediateRadiusBodyDiametersBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(immediateRadiusBodyDiametersBasisPoints),
                    immediateRadiusBodyDiametersBasisPoints,
                    "A preset without equipment-relative footwork must " +
                    "register a zero immediate radius.");
            }

            if (supportRadiusBodyDiametersBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportRadiusBodyDiametersBasisPoints),
                    supportRadiusBodyDiametersBasisPoints,
                    "A preset without equipment-relative footwork must " +
                    "register a zero support radius.");
            }

            if (!loadoutMovementProfiles.IsEmpty)
            {
                throw new ArgumentException(
                    "A preset without equipment-relative footwork must " +
                    "register an empty profile collection.",
                    nameof(loadoutMovementProfiles));
            }

            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            immediateRadiusBodyDiametersBasisPoints,
            nameof(immediateRadiusBodyDiametersBasisPoints));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            supportRadiusBodyDiametersBasisPoints,
            nameof(supportRadiusBodyDiametersBasisPoints));

        if (loadoutMovementProfiles.Length != CanonicalLoadoutCount)
        {
            throw new ArgumentException(
                "A preset with equipment-relative footwork must register " +
                $"exactly {CanonicalLoadoutCount} profile rows, one per " +
                "canonical loadout.",
                nameof(loadoutMovementProfiles));
        }

        for (var position = 0; position < loadoutMovementProfiles.Length; position++)
        {
            var key = loadoutMovementProfiles[position].Loadout;
            if (CanonicalLoadoutIndex(key.Weapon, key.Armor, key.Shield) !=
                position)
            {
                throw new ArgumentException(
                    $"The profile row at position {position} does not carry " +
                    "that position's canonical loadout key; rows must appear " +
                    "once each in canonical KP, WA, KA, IT, KS, IS order and " +
                    $"({key.Weapon}, {key.Armor}, {key.Shield}) is not that " +
                    "position's key.",
                    nameof(loadoutMovementProfiles));
            }
        }
    }

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
        Fnv1a.Add(ref hash, SelectsLeaderByRank ? 1UL : 0UL);
        Fnv1a.Add(ref hash, UsesEquipmentRelativeFootwork ? 1UL : 0UL);
        Fnv1a.Add(ref hash, (ulong)ImmediateRadiusBodyDiametersBasisPoints);
        Fnv1a.Add(ref hash, (ulong)SupportRadiusBodyDiametersBasisPoints);
        Fnv1a.Add(ref hash, (ulong)LoadoutMovementProfiles.Length);
        foreach (var profile in LoadoutMovementProfiles)
        {
            // Design section 5.1: the equipment key only — the rank field is
            // not part of the key, so it is not folded.
            Fnv1a.Add(ref hash, (ulong)(int)profile.Loadout.Weapon);
            Fnv1a.Add(ref hash, (ulong)(int)profile.Loadout.Armor);
            Fnv1a.Add(ref hash, (ulong)(int)profile.Loadout.Shield);
            Fnv1a.Add(ref hash, (ulong)profile.ForwardPaceBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.LateralPaceBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.BackwardPaceBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.CommittedPaceBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.PreferredDistanceBasisPoints);
            Fnv1a.Add(
                ref hash,
                (ulong)profile.OpponentDistanceOffsetBasisPoints.Length);
            foreach (var cell in profile.OpponentDistanceOffsetBasisPoints)
            {
                // A signed offset folds as its two's-complement value.
                Fnv1a.Add(ref hash, unchecked((ulong)(long)cell));
            }

            Fnv1a.Add(ref hash, (ulong)profile.MaximumFacingStepsPerTick);
            Fnv1a.Add(ref hash, (ulong)profile.CommittedFacingStepsPerTick);
            Fnv1a.Add(ref hash, (ulong)profile.AccelerationBasisPointsPerTick);
            Fnv1a.Add(ref hash, (ulong)profile.DecelerationBasisPointsPerTick);
            Fnv1a.Add(ref hash, (ulong)profile.CommitmentTicks);
            Fnv1a.Add(ref hash, (ulong)profile.RecoveryTicks);
            Fnv1a.Add(
                ref hash,
                (ulong)profile.AllyClearanceBodyDiametersBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.DisengageEnemyToAllyBasisPoints);
            Fnv1a.Add(ref hash, (ulong)profile.ReengageEnemyToAllyBasisPoints);
            Fnv1a.Add(
                ref hash,
                (ulong)profile.PursuitSupportBodyDiametersBasisPoints);
        }

        return hash;
    }
}
