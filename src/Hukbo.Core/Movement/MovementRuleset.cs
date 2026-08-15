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
/// taper; see
/// the formation and movement realism design
/// section 3 for the derivations.
/// </summary>
/// <remarks>
/// What stays frozen is each preset's simulated behaviour, proved
/// byte-identically by its own digest fixture in
/// <c>MovementPresetFreezeTests</c> — not this type's field list.
/// <see cref="ContentHash"/> is folded over every field below, and since V6
/// it does reach the state hash, conditionally.
/// <c>BattleSimulation.ComputeStateHash</c> always folds
/// <c>_rules.ContentHash</c>, where <c>_rules</c> is the
/// <c>CombatRuleset</c> (src/Hukbo.Core/Simulation/BattleSimulation.cs:25,
/// 628), and it additionally hands this type's <see cref="ContentHash"/> to
/// <c>StateHasher.Compute</c> for every preset whose
/// <see cref="UsesEquipmentRelativeFootwork"/> is <see langword="true"/>, and
/// <see langword="null"/> for every preset where that flag is
/// <see langword="false"/>
/// (src/Hukbo.Core/Simulation/BattleSimulation.cs:642-656).
/// <c>StateHasher.Compute</c> folds that value immediately after the combat
/// content hash when it is non-null, and writes nothing at all when it is
/// null (src/Hukbo.Core/Determinism/StateHasher.cs:81-84). Adding a field
/// here therefore leaves the state hash, event hash, outcome, and recorded
/// digest of V1 through V5 byte-identical, but moves every one of them for
/// each preset that opts into equipment-relative footwork — V6 today, whose
/// freeze fixture seed-1-200-agents-movement-v6-digest.json records a state
/// hash per tick and would have to be re-recorded. It also moves the pinned
/// <c>ContentHash</c> identity literals in
/// <c>MovementPresetRegistryTests</c> for every preset, and those must be
/// recomputed from the built code whenever a field is added, never
/// calculated by hand. See
/// the contingent close-latch design section 3.
/// All of that describes a field folded <em>unconditionally</em>. A field
/// folded behind a version gate does not move any preset the gate is
/// <see langword="false"/> for, because nothing is written for that preset at
/// all: the three pressure-interrupt weights, and every profile row's
/// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>,
/// fold inside <c>if (AppliesPressureInterrupt)</c>, which is why adding them
/// left the pinned identity literals and the frozen trajectory digests of V1
/// through V6 unchanged. The gate itself is not folded: inside its own branch
/// it is always <see langword="true"/>, so it would contribute a constant and
/// discriminate nothing. See
/// the 2026-07-31 movement V7 pressure interrupt design section 6.
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

    /// <summary>
    /// The exact basis-point total the three pressure-interrupt signal
    /// weights must sum to when <see cref="AppliesPressureInterrupt"/> is
    /// <see langword="true"/> — one whole unit, so the weighted sum stays in
    /// the same basis-point space the per-row threshold is compared against.
    /// A game-design choice, not a measurement.
    /// </summary>
    public const int TotalPressureInterruptWeightBasisPoints = 10_000;

    /// <summary>
    /// The basis-point value of
    /// <see cref="CohesionSquareMarginBasisPoints"/> that leaves a
    /// contingent's claimed square exactly the size every preset without
    /// <see cref="GathersContingentsBeforeContact"/> claims today — one whole
    /// unit, and therefore also the ceiling, because the tunable exists to
    /// shrink the claimed square and never to widen it. A game-design choice,
    /// not a measurement.
    /// </summary>
    public const int UnscaledCohesionSquareMarginBasisPoints = 10_000;

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
        ImmutableArray<LoadoutMovementProfile> loadoutMovementProfiles,
        bool appliesPressureInterrupt = false,
        int supportPressureWeightBasisPoints = 0,
        int incomingDamageWeightBasisPoints = 0,
        int allyCollapseWeightBasisPoints = 0,
        bool gathersContingentsBeforeContact = false,
        int cohesionBandNumerator = 0,
        int cohesionBandDenominator = 0,
        int cohesionSquareMarginBasisPoints = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ValidateEquipmentRelativeFootworkCoupling(
            usesEquipmentRelativeFootwork,
            immediateRadiusBodyDiametersBasisPoints,
            supportRadiusBodyDiametersBasisPoints,
            loadoutMovementProfiles,
            appliesPressureInterrupt,
            supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints);
        ValidateContingentCohesionCoupling(
            gathersContingentsBeforeContact,
            cohesionBandNumerator,
            cohesionBandDenominator,
            cohesionSquareMarginBasisPoints);

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
        AppliesPressureInterrupt = appliesPressureInterrupt;
        SupportPressureWeightBasisPoints = supportPressureWeightBasisPoints;
        IncomingDamageWeightBasisPoints = incomingDamageWeightBasisPoints;
        AllyCollapseWeightBasisPoints = allyCollapseWeightBasisPoints;
        ImmediateRadiusBodyDiametersBasisPoints =
            immediateRadiusBodyDiametersBasisPoints;
        SupportRadiusBodyDiametersBasisPoints =
            supportRadiusBodyDiametersBasisPoints;
        LoadoutMovementProfiles = loadoutMovementProfiles;
        GathersContingentsBeforeContact = gathersContingentsBeforeContact;
        CohesionBandNumerator = cohesionBandNumerator;
        CohesionBandDenominator = cohesionBandDenominator;
        CohesionSquareMarginBasisPoints = cohesionSquareMarginBasisPoints;
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
    /// Whether this preset lets local pressure interrupt a committed blow,
    /// resolving footwork to <c>FootworkPhase.Disengage</c> on the tick the
    /// weighted pressure signal reaches a warrior's registered threshold.
    /// Registered <see langword="false"/> with three zero weights for every
    /// preset up to and including
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>, so
    /// introducing this field moves no existing preset's behaviour. It is
    /// deliberately a separate gate from
    /// <see cref="UsesEquipmentRelativeFootwork"/>, which V6 already
    /// registers <see langword="true"/>: gating on that flag instead would
    /// move V6. A game-design choice, not a measurement.
    /// </summary>
    /// <remarks>
    /// This member and the three weights below are trailing optional
    /// constructor parameters defaulting to <see langword="false"/> and zero,
    /// so every construction site that predates them keeps compiling and
    /// keeps the legacy behaviour. They are nonetheless declared here, in the
    /// position their <see cref="ContentHash"/> fold occupies, because that
    /// fold runs in declaration order.
    /// </remarks>
    public bool AppliesPressureInterrupt { get; }

    /// <summary>
    /// The weight, in basis points, the pressure signal for local enemy-to-ally
    /// support odds carries in the interrupt's weighted sum. Zero for every
    /// preset whose <see cref="AppliesPressureInterrupt"/> is
    /// <see langword="false"/>; otherwise non-negative and, with the two
    /// weights below, totalling exactly
    /// <see cref="TotalPressureInterruptWeightBasisPoints"/>. Shared by all
    /// six loadout rows — only the threshold the sum is compared against is
    /// per row. A provisional reconstruction of gameplay tuning, not a
    /// historical measurement.
    /// </summary>
    public int SupportPressureWeightBasisPoints { get; }

    /// <summary>
    /// The weight, in basis points, the pressure signal for damage taken on
    /// the previous tick carries in the interrupt's weighted sum. Zero for
    /// every preset whose <see cref="AppliesPressureInterrupt"/> is
    /// <see langword="false"/>. A provisional reconstruction of gameplay
    /// tuning, not a historical measurement.
    /// </summary>
    public int IncomingDamageWeightBasisPoints { get; }

    /// <summary>
    /// The weight, in basis points, the pressure signal for supporting allies
    /// lost since the previous tick carries in the interrupt's weighted sum.
    /// Zero for every preset whose <see cref="AppliesPressureInterrupt"/> is
    /// <see langword="false"/>. A provisional reconstruction of gameplay
    /// tuning, not a historical measurement.
    /// </summary>
    public int AllyCollapseWeightBasisPoints { get; }

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
    /// Whether this preset gathers its contingents before contact, per the
    /// "Contingent cohesion before contact — plan". Registered
    /// <see langword="false"/> with three zero tunables for every preset up to
    /// and including
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/>, so introducing
    /// this field moves no existing preset's behaviour. A game-design choice,
    /// not a measurement.
    /// </summary>
    /// <remarks>
    /// This member and the three tunables below are trailing optional
    /// constructor parameters defaulting to <see langword="false"/> and zero,
    /// so every construction site that predates them keeps compiling and keeps
    /// the legacy behaviour. They are declared here, in the position their
    /// <see cref="ContentHash"/> fold occupies, because that fold runs in
    /// declaration order. Only the three numerics are folded, and only inside
    /// <c>if (GathersContingentsBeforeContact)</c>; the gate itself is not
    /// folded, because inside its own branch it is always
    /// <see langword="true"/> and would discriminate nothing. That is what
    /// keeps the pinned <c>ContentHash</c> literals of every earlier preset
    /// exactly where they are.
    /// </remarks>
    public bool GathersContingentsBeforeContact { get; }

    /// <summary>
    /// The numerator of the fraction of the cohesion radius inside which a
    /// member is close enough to its leader to stop counting as a straggler,
    /// replacing the hardcoded three-quarters comparison when
    /// <see cref="GathersContingentsBeforeContact"/> is
    /// <see langword="true"/>. Zero for every preset whose gate is
    /// <see langword="false"/>; otherwise non-negative and no greater than
    /// <see cref="CohesionBandDenominator"/>. A provisional reconstruction of
    /// gameplay tuning, not a historical measurement. See the "Contingent
    /// cohesion before contact — plan".
    /// </summary>
    public int CohesionBandNumerator { get; }

    /// <summary>
    /// The denominator of the cohesion band fraction described on
    /// <see cref="CohesionBandNumerator"/>. Zero for every preset whose
    /// <see cref="GathersContingentsBeforeContact"/> is
    /// <see langword="false"/>; otherwise at least one, so the band is a
    /// well-formed fraction and the comparison never divides by zero. A
    /// provisional reconstruction of gameplay tuning, not a historical
    /// measurement. See the "Contingent cohesion before contact — plan".
    /// </summary>
    public int CohesionBandDenominator { get; }

    /// <summary>
    /// The scale, in basis points, applied to the half-side of the square a
    /// contingent claims in the cross-contingent overlap test, so a contingent
    /// claims less ground than the packing bound alone would give it and
    /// <c>ContingentState.Hold</c> stays reachable under a crowded deployment.
    /// Zero for every preset whose
    /// <see cref="GathersContingentsBeforeContact"/> is
    /// <see langword="false"/>; otherwise in the inclusive range
    /// <c>[1, 10000]</c>, where 10000 is bit-identical to the unscaled margin
    /// every earlier preset uses. It scales the claimed margin only and never
    /// the per-member jitter, because changing jitter would change member
    /// spacing. A provisional reconstruction of gameplay tuning, not a
    /// historical measurement. See the "Contingent cohesion before contact —
    /// plan".
    /// </summary>
    public int CohesionSquareMarginBasisPoints { get; }

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
    /// <remarks>
    /// Pressure-interrupt design section 6.3 adds the parallel clause for
    /// <see cref="AppliesPressureInterrupt"/>: a preset that does not apply
    /// the interrupt carries three zero weights, a preset that does carries
    /// three non-negative weights totalling exactly
    /// <see cref="TotalPressureInterruptWeightBasisPoints"/>, and the
    /// interrupt may be applied only by a preset that also uses
    /// equipment-relative footwork. That clause is checked before the
    /// footwork clause returns early, so it binds both kinds of preset.
    /// </remarks>
    private static void ValidateEquipmentRelativeFootworkCoupling(
        bool usesEquipmentRelativeFootwork,
        int immediateRadiusBodyDiametersBasisPoints,
        int supportRadiusBodyDiametersBasisPoints,
        ImmutableArray<LoadoutMovementProfile> loadoutMovementProfiles,
        bool appliesPressureInterrupt,
        int supportPressureWeightBasisPoints,
        int incomingDamageWeightBasisPoints,
        int allyCollapseWeightBasisPoints)
    {
        if (loadoutMovementProfiles.IsDefault)
        {
            throw new ArgumentException(
                "The profile collection must be supplied; pass " +
                "ImmutableArray<LoadoutMovementProfile>.Empty for a preset " +
                "without equipment-relative footwork.",
                nameof(loadoutMovementProfiles));
        }

        ValidatePressureInterruptCoupling(
            usesEquipmentRelativeFootwork,
            loadoutMovementProfiles,
            appliesPressureInterrupt,
            supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints);

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

    /// <summary>
    /// The pressure-interrupt half of the coupled validation, per design
    /// section 6.3. A preset that does not apply the interrupt must register
    /// three zero weights, so its <see cref="ContentHash"/> fold stays exactly
    /// what it is today. A preset that does apply it must also use
    /// equipment-relative footwork, because the interrupt is evaluated inside
    /// a stage only that flag runs, and must register three non-negative
    /// weights totalling exactly
    /// <see cref="TotalPressureInterruptWeightBasisPoints"/>.
    /// </summary>
    /// <remarks>
    /// The per-row half of the same clause is checked here too, because this
    /// is the only place that sees both the version gate and the profile rows
    /// at once: every row's
    /// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>
    /// is zero under a preset that does not apply the interrupt — which is
    /// what keeps that preset's folded bytes identical to what they were
    /// before the member existed — and lies in the inclusive range
    /// <c>[1, SignalCeilingBasisPoints]</c> under one that does, so no row of
    /// a preset that applies the interrupt is silently unreachable and none
    /// carries a threshold no saturated signal could ever reach. A row
    /// validates only its own non-negativity, because it cannot see the gate.
    /// </remarks>
    private static void ValidatePressureInterruptCoupling(
        bool usesEquipmentRelativeFootwork,
        ImmutableArray<LoadoutMovementProfile> loadoutMovementProfiles,
        bool appliesPressureInterrupt,
        int supportPressureWeightBasisPoints,
        int incomingDamageWeightBasisPoints,
        int allyCollapseWeightBasisPoints)
    {
        if (!appliesPressureInterrupt)
        {
            if (supportPressureWeightBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportPressureWeightBasisPoints),
                    supportPressureWeightBasisPoints,
                    "A preset that does not apply the pressure interrupt " +
                    "must register a zero support pressure weight.");
            }

            if (incomingDamageWeightBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(incomingDamageWeightBasisPoints),
                    incomingDamageWeightBasisPoints,
                    "A preset that does not apply the pressure interrupt " +
                    "must register a zero incoming damage weight.");
            }

            if (allyCollapseWeightBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allyCollapseWeightBasisPoints),
                    allyCollapseWeightBasisPoints,
                    "A preset that does not apply the pressure interrupt " +
                    "must register a zero ally collapse weight.");
            }

            foreach (var profile in loadoutMovementProfiles)
            {
                if (profile.PressureInterruptThresholdBasisPoints != 0)
                {
                    throw new ArgumentException(
                        "A preset that does not apply the pressure interrupt " +
                        "must register a zero threshold on every profile row; " +
                        "the row keyed " +
                        $"({profile.Loadout.Weapon}, {profile.Loadout.Armor}, " +
                        $"{profile.Loadout.Shield}) registers " +
                        $"{profile.PressureInterruptThresholdBasisPoints}.",
                        nameof(loadoutMovementProfiles));
                }
            }

            return;
        }

        if (!usesEquipmentRelativeFootwork)
        {
            throw new ArgumentException(
                "A preset may apply the pressure interrupt only when it also " +
                "uses equipment-relative footwork, because the interrupt is " +
                "evaluated inside a stage that only the latter flag runs.",
                nameof(appliesPressureInterrupt));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(
            supportPressureWeightBasisPoints,
            nameof(supportPressureWeightBasisPoints));
        ArgumentOutOfRangeException.ThrowIfNegative(
            incomingDamageWeightBasisPoints,
            nameof(incomingDamageWeightBasisPoints));
        ArgumentOutOfRangeException.ThrowIfNegative(
            allyCollapseWeightBasisPoints,
            nameof(allyCollapseWeightBasisPoints));

        var totalWeightBasisPoints =
            (long)supportPressureWeightBasisPoints +
            incomingDamageWeightBasisPoints +
            allyCollapseWeightBasisPoints;
        if (totalWeightBasisPoints != TotalPressureInterruptWeightBasisPoints)
        {
            throw new ArgumentException(
                "A preset that applies the pressure interrupt must register " +
                "three weights totalling exactly " +
                $"{TotalPressureInterruptWeightBasisPoints} basis points; " +
                $"these total {totalWeightBasisPoints}.",
                nameof(supportPressureWeightBasisPoints));
        }

        foreach (var profile in loadoutMovementProfiles)
        {
            if (profile.PressureInterruptThresholdBasisPoints < 1 ||
                profile.PressureInterruptThresholdBasisPoints >
                    WeaponMovementRules.SignalCeilingBasisPoints)
            {
                throw new ArgumentException(
                    "A preset that applies the pressure interrupt must " +
                    "register a threshold in the inclusive range [1, " +
                    $"{WeaponMovementRules.SignalCeilingBasisPoints}] on " +
                    "every profile row; the row keyed " +
                    $"({profile.Loadout.Weapon}, {profile.Loadout.Armor}, " +
                    $"{profile.Loadout.Shield}) registers " +
                    $"{profile.PressureInterruptThresholdBasisPoints}.",
                    nameof(loadoutMovementProfiles));
            }
        }
    }

    /// <summary>
    /// The coupled validation for the contingent cohesion tunables of the
    /// "Contingent cohesion before contact — plan", mirroring the shape
    /// <see cref="ValidateEquipmentRelativeFootworkCoupling"/> uses. A preset
    /// that does not gather its contingents before contact must register three
    /// zero tunables, so its <see cref="ContentHash"/> fold stays exactly what
    /// it is today and a half-configured ruleset cannot exist. A preset that
    /// does gather must register a well-formed band fraction and a margin
    /// scale inside the inclusive range
    /// <c>[1, UnscaledCohesionSquareMarginBasisPoints]</c>, so the band can
    /// never divide by zero, can never exceed the whole cohesion radius, and
    /// the margin scale can never widen the claimed square beyond the
    /// unscaled one every earlier preset uses.
    /// </summary>
    private static void ValidateContingentCohesionCoupling(
        bool gathersContingentsBeforeContact,
        int cohesionBandNumerator,
        int cohesionBandDenominator,
        int cohesionSquareMarginBasisPoints)
    {
        if (!gathersContingentsBeforeContact)
        {
            if (cohesionBandNumerator != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cohesionBandNumerator),
                    cohesionBandNumerator,
                    "A preset that does not gather its contingents before " +
                    "contact must register a zero cohesion band numerator.");
            }

            if (cohesionBandDenominator != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cohesionBandDenominator),
                    cohesionBandDenominator,
                    "A preset that does not gather its contingents before " +
                    "contact must register a zero cohesion band denominator.");
            }

            if (cohesionSquareMarginBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cohesionSquareMarginBasisPoints),
                    cohesionSquareMarginBasisPoints,
                    "A preset that does not gather its contingents before " +
                    "contact must register a zero cohesion square margin " +
                    "scale.");
            }

            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(
            cohesionBandDenominator,
            1,
            nameof(cohesionBandDenominator));
        ArgumentOutOfRangeException.ThrowIfNegative(
            cohesionBandNumerator,
            nameof(cohesionBandNumerator));

        if (cohesionBandNumerator > cohesionBandDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cohesionBandNumerator),
                cohesionBandNumerator,
                "A preset that gathers its contingents before contact must " +
                "register a cohesion band numerator no greater than its " +
                $"denominator; this numerator exceeds {cohesionBandDenominator}.");
        }

        if (cohesionSquareMarginBasisPoints < 1 ||
            cohesionSquareMarginBasisPoints >
                UnscaledCohesionSquareMarginBasisPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cohesionSquareMarginBasisPoints),
                cohesionSquareMarginBasisPoints,
                "A preset that gathers its contingents before contact must " +
                "register a cohesion square margin scale in the inclusive " +
                $"range [1, {UnscaledCohesionSquareMarginBasisPoints}].");
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
        if (AppliesPressureInterrupt)
        {
            // Design section 6.2: the version gate. Every preset that does
            // not apply the interrupt writes nothing here at all, so V1
            // through V6 keep the exact byte sequence they fold today, and
            // their pinned ContentHash literals and frozen trajectory
            // digests do not move. Folding these values unconditionally
            // would move V6, whose ContentHash does reach the state hash.
            //
            // The flag itself is not folded. Inside this branch it is
            // always true, so folding it would contribute a constant and
            // discriminate nothing; the branch is what records it. Three
            // weights fold here, as design section 6.2 item 1 specifies.
            Fnv1a.Add(ref hash, (ulong)SupportPressureWeightBasisPoints);
            Fnv1a.Add(ref hash, (ulong)IncomingDamageWeightBasisPoints);
            Fnv1a.Add(ref hash, (ulong)AllyCollapseWeightBasisPoints);
        }

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
            if (AppliesPressureInterrupt)
            {
                // Design section 6.2 item 2: the same version gate, one layer
                // down. Every preset that does not apply the interrupt writes
                // nothing here either, so V1 through V6 keep the exact per-row
                // byte sequence they fold today, and their pinned ContentHash
                // literals and frozen trajectory digests do not move. The gate
                // is available even though the value folded lives on the row,
                // which cannot see it, because the fold runs on the ruleset,
                // which can.
                Fnv1a.Add(
                    ref hash,
                    (ulong)profile.PressureInterruptThresholdBasisPoints);
            }
        }

        if (GathersContingentsBeforeContact)
        {
            // The same version gate the pressure interrupt above uses, for the
            // same reason. Every preset that does not gather its contingents
            // before contact writes nothing here at all, so V1 through V13
            // keep the exact byte sequence they fold today, and their pinned
            // ContentHash literals and frozen trajectory digests do not move.
            // Folding these values unconditionally would move all seven pinned
            // identity literals at once.
            //
            // The flag itself is not folded. Inside this branch it is always
            // true, so folding it would contribute a constant and discriminate
            // nothing; the branch is what records it. Only the three numeric
            // tunables fold here.
            Fnv1a.Add(ref hash, (ulong)CohesionBandNumerator);
            Fnv1a.Add(ref hash, (ulong)CohesionBandDenominator);
            Fnv1a.Add(ref hash, (ulong)CohesionSquareMarginBasisPoints);
        }

        return hash;
    }
}
