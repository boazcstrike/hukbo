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
    /// The number of canonical loadouts a preset with equipment-relative
    /// footwork carries when it also registers the two narrow-breast-high
    /// shield rows — <c>KP, WA, KA, IT, KS, IS</c> plus narrow-shield
    /// <c>KS</c> and <c>IS</c> at indices 6 and 7 — first registered by
    /// <see cref="MovementPresetId.ShieldEncumbranceV14"/>. A preset with
    /// equipment-relative footwork must carry exactly
    /// <see cref="CanonicalLoadoutCount"/> or exactly this many rows; no other
    /// count is legal (2026-08-15 shield-projectile-block design, section
    /// 6.1).
    /// </summary>
    public const int ExtendedCanonicalLoadoutCount = 8;

    /// <summary>
    /// The exact basis-point total the three pressure-interrupt signal
    /// weights must sum to when <see cref="AppliesPressureInterrupt"/> is
    /// <see langword="true"/> — one whole unit, so the weighted sum stays in
    /// the same basis-point space the per-row threshold is compared against.
    /// A game-design choice, not a measurement.
    /// </summary>
    public const int TotalPressureInterruptWeightBasisPoints = 10_000;

    /// <summary>
    /// The pace a warrior carrying no shield, or playing under a preset
    /// whose <see cref="AppliesShieldEncumbrance"/> is
    /// <see langword="false"/>, moves at: full speed, unscaled. The
    /// basis-point unit <see cref="ResolveShieldPaceBasisPoints"/> returns,
    /// and the ceiling every registered per-shield pace must fall strictly
    /// below.
    /// </summary>
    public const int FullShieldPaceBasisPoints = 10_000;

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
        bool appliesShieldBlockRecovery = false,
        int tallShieldBlockRecoveryTicks = 0,
        int narrowShieldBlockRecoveryTicks = 0,
        int shieldBlockRecoveryPaceCeilingBasisPoints = 0,
        bool appliesShieldEncumbrance = false,
        int narrowBreastHighShieldPaceBasisPoints = 0,
        int tallHardwoodShieldPaceBasisPoints = 0)
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
        ValidateShieldBlockRecoveryCoupling(
            appliesShieldBlockRecovery,
            tallShieldBlockRecoveryTicks,
            narrowShieldBlockRecoveryTicks,
            shieldBlockRecoveryPaceCeilingBasisPoints);
        ValidateShieldEncumbranceCoupling(
            appliesShieldEncumbrance,
            narrowBreastHighShieldPaceBasisPoints,
            tallHardwoodShieldPaceBasisPoints);

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
        AppliesShieldBlockRecovery = appliesShieldBlockRecovery;
        TallShieldBlockRecoveryTicks = tallShieldBlockRecoveryTicks;
        NarrowShieldBlockRecoveryTicks = narrowShieldBlockRecoveryTicks;
        ShieldBlockRecoveryPaceCeilingBasisPoints =
            shieldBlockRecoveryPaceCeilingBasisPoints;
        AppliesShieldEncumbrance = appliesShieldEncumbrance;
        NarrowBreastHighShieldPaceBasisPoints =
            narrowBreastHighShieldPaceBasisPoints;
        TallHardwoodShieldPaceBasisPoints = tallHardwoodShieldPaceBasisPoints;
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
    /// Whether this preset opens a brief pace-cap window on a warrior whose
    /// shield has just intercepted an attack — the block-recovery window of
    /// the 2026-08-15 shield-projectile-block design, section 6.2. Registered
    /// <see langword="false"/> with three zero values for every preset up to
    /// and including <see cref="MovementPresetId.CohortLateralSpreadV13"/>, so
    /// introducing this field moves no existing preset's behaviour; only
    /// <see cref="MovementPresetId.ShieldEncumbranceV14"/> registers it
    /// <see langword="true"/>. The window itself is opened, decremented, and
    /// read by <c>BattleSimulation</c> and hashed by <c>StateHasher</c>, both
    /// gated on this same flag; this ruleset only carries the flag and the
    /// three values it gates. A game-design choice, not a measurement.
    /// </summary>
    public bool AppliesShieldBlockRecovery { get; }

    /// <summary>
    /// The block-recovery window's duration, in ticks, for a warrior carrying
    /// <see cref="Combat.ShieldId.TallHardwood"/>. Zero for every preset whose
    /// <see cref="AppliesShieldBlockRecovery"/> is <see langword="false"/>;
    /// otherwise strictly greater than
    /// <see cref="NarrowShieldBlockRecoveryTicks"/>, because a heavier board
    /// takes longer to bring back into guard. Provisional reconstruction:
    /// gameplay tuning under CLAUDE.md section 7; no historical measurement.
    /// </summary>
    public int TallShieldBlockRecoveryTicks { get; }

    /// <summary>
    /// The block-recovery window's duration, in ticks, for a warrior carrying
    /// <see cref="Combat.ShieldId.NarrowBreastHigh"/>. Zero for every preset
    /// whose <see cref="AppliesShieldBlockRecovery"/> is
    /// <see langword="false"/>; otherwise strictly positive and strictly less
    /// than <see cref="TallShieldBlockRecoveryTicks"/>. Provisional
    /// reconstruction: gameplay tuning under CLAUDE.md section 7; no
    /// historical measurement.
    /// </summary>
    public int NarrowShieldBlockRecoveryTicks { get; }

    /// <summary>
    /// The pace-cap ceiling, in basis points, applied to a warrior while its
    /// block-recovery window is open, regardless of its resolved loadout
    /// pace. Zero for every preset whose
    /// <see cref="AppliesShieldBlockRecovery"/> is <see langword="false"/>;
    /// otherwise in the inclusive range [1, 10_000]. Provisional
    /// reconstruction: gameplay tuning under CLAUDE.md section 7; no
    /// historical measurement.
    /// </summary>
    public int ShieldBlockRecoveryPaceCeilingBasisPoints { get; }

    /// <summary>
    /// Whether this preset scales a warrior's movement speed at agent
    /// creation by the pace <see cref="ResolveShieldPaceBasisPoints"/>
    /// resolves for its carried shield — the shield-encumbrance effect of
    /// the 2026-08-15 shield-projectile-block design, section 6.1. This is
    /// deliberately independent of
    /// <see cref="UsesEquipmentRelativeFootwork"/> and of
    /// <see cref="LoadoutMovementProfiles"/>: the shipped movement pipeline
    /// registers no loadout profile row for a ranged loadout, so gating
    /// shield encumbrance on the equipment-relative footwork flag or
    /// resolving it through <see cref="ResolveLoadoutProfile"/> would throw
    /// for every ranged agent under a preset that applies it. The scaling is
    /// applied once, at spawn, directly to the agent's raw movement speed by
    /// <c>BattleSimulation.CreateAgent</c>, not through a loadout row.
    /// Registered <see langword="false"/> with two zero paces for every
    /// preset up to and including
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/>, so introducing
    /// this field moves no existing preset's behaviour; only
    /// <see cref="MovementPresetId.ShieldEncumbranceV14"/> registers it
    /// <see langword="true"/>. A game-design choice, not a measurement.
    /// </summary>
    public bool AppliesShieldEncumbrance { get; }

    /// <summary>
    /// The pace, in basis points of full speed, a warrior carrying
    /// <see cref="Combat.ShieldId.NarrowBreastHigh"/> moves at. Zero for
    /// every preset whose <see cref="AppliesShieldEncumbrance"/> is
    /// <see langword="false"/>; otherwise strictly between zero and
    /// <see cref="FullShieldPaceBasisPoints"/>, exclusive, and strictly
    /// greater than <see cref="TallHardwoodShieldPaceBasisPoints"/>, because
    /// the narrower shield is lighter and encumbers less. Provisional
    /// reconstruction: gameplay tuning under CLAUDE.md section 7; no
    /// historical measurement.
    /// </summary>
    public int NarrowBreastHighShieldPaceBasisPoints { get; }

    /// <summary>
    /// The pace, in basis points of full speed, a warrior carrying
    /// <see cref="Combat.ShieldId.TallHardwood"/> moves at. Zero for every
    /// preset whose <see cref="AppliesShieldEncumbrance"/> is
    /// <see langword="false"/>; otherwise strictly between zero and
    /// <see cref="NarrowBreastHighShieldPaceBasisPoints"/>, exclusive.
    /// Provisional reconstruction: gameplay tuning under CLAUDE.md section 7;
    /// no historical measurement.
    /// </summary>
    public int TallHardwoodShieldPaceBasisPoints { get; }

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
    /// The per-loadout movement profile rows — six or eight, in canonical
    /// <c>KP, WA, KA, IT, KS, IS</c> order, or <c>KP, WA, KA, IT, KS, IS,
    /// KS(narrow), IS(narrow)</c> once a preset registers
    /// <see cref="ExtendedCanonicalLoadoutCount"/> rows — when
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
    /// The block-recovery window's duration, in ticks, for a warrior carrying
    /// <paramref name="shield"/> — <see cref="TallShieldBlockRecoveryTicks"/>,
    /// <see cref="NarrowShieldBlockRecoveryTicks"/>, or zero for
    /// <see cref="Combat.ShieldId.None"/> and for any preset whose
    /// <see cref="AppliesShieldBlockRecovery"/> is <see langword="false"/>,
    /// since both stored durations are themselves zero there. Unlike
    /// <see cref="ResolveLoadoutProfile"/> this never throws: a warrior
    /// carrying no shield, or playing under a preset that does not apply the
    /// effect, simply never opens a window.
    /// </summary>
    public int ResolveShieldBlockRecoveryTicks(ShieldId shield) =>
        shield switch
        {
            ShieldId.TallHardwood => TallShieldBlockRecoveryTicks,
            ShieldId.NarrowBreastHigh => NarrowShieldBlockRecoveryTicks,
            _ => 0,
        };

    /// <summary>
    /// The pace, in basis points of full speed, a warrior carrying
    /// <paramref name="shield"/> moves at —
    /// <see cref="NarrowBreastHighShieldPaceBasisPoints"/>,
    /// <see cref="TallHardwoodShieldPaceBasisPoints"/>, or
    /// <see cref="FullShieldPaceBasisPoints"/> for
    /// <see cref="Combat.ShieldId.None"/> and for any preset whose
    /// <see cref="AppliesShieldEncumbrance"/> is <see langword="false"/>.
    /// Unlike <see cref="ResolveLoadoutProfile"/> this never throws and is
    /// never gated on the weapon carried, so it resolves for a ranged
    /// loadout exactly as it does for a melee one:
    /// <c>BattleSimulation.CreateAgent</c> calls this for every agent
    /// regardless of loadout to scale the agent's raw movement speed at
    /// spawn.
    /// </summary>
    public int ResolveShieldPaceBasisPoints(ShieldId shield)
    {
        if (!AppliesShieldEncumbrance)
        {
            return FullShieldPaceBasisPoints;
        }

        return shield switch
        {
            ShieldId.NarrowBreastHigh => NarrowBreastHighShieldPaceBasisPoints,
            ShieldId.TallHardwood => TallHardwoodShieldPaceBasisPoints,
            _ => FullShieldPaceBasisPoints,
        };
    }

    /// <summary>
    /// Maps an equipment key to its canonical loadout index — <c>KP</c> 0,
    /// <c>WA</c> 1, <c>KA</c> 2, <c>IT</c> 3, <c>KS</c> 4, <c>IS</c> 5, and,
    /// only under a preset that registers
    /// <see cref="ExtendedCanonicalLoadoutCount"/> rows, narrow-shield
    /// <c>KS</c> 6 and narrow-shield <c>IS</c> 7 — or -1 for a key no profile
    /// row may carry. The canonical order is binding on the stored profile
    /// collection and on the content-hash fold. Indices 6 and 7 are mapped
    /// unconditionally here: a preset with only six rows never reaches them
    /// because <see cref="ResolveLoadoutProfile"/> already throws once the
    /// index is at or past
    /// <see cref="LoadoutMovementProfiles"/>'s length, which is exactly the
    /// "fails loudly instead of silently inheriting another row's footwork"
    /// behaviour that method's doc comment promises.
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
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh)
                => 6,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh)
                => 7,
            _ => -1,
        };

    /// <summary>
    /// The coupled validation of design section 5: a preset without
    /// equipment-relative footwork carries zero radii and no profile rows,
    /// and a preset with it carries strictly positive radii and either the
    /// six canonical rows or, since
    /// <see cref="MovementPresetId.ShieldEncumbranceV14"/>, all eight
    /// (<see cref="CanonicalLoadoutCount"/> or
    /// <see cref="ExtendedCanonicalLoadoutCount"/>), each appearing once, in
    /// canonical order. Any other row count — seven included — is rejected. A
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

        if (loadoutMovementProfiles.Length != CanonicalLoadoutCount &&
            loadoutMovementProfiles.Length != ExtendedCanonicalLoadoutCount)
        {
            throw new ArgumentException(
                "A preset with equipment-relative footwork must register " +
                $"either {CanonicalLoadoutCount} or " +
                $"{ExtendedCanonicalLoadoutCount} profile rows, one per " +
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
    /// The shield-block-recovery half of the coupled validation, per
    /// shield-projectile-block design section 6.2. A preset that does not
    /// apply the effect must register three zero values, so its
    /// <see cref="ContentHash"/> fold stays exactly what it is today. A
    /// preset that does apply it must register a strictly positive tall
    /// duration, a strictly positive narrow duration strictly below the tall
    /// one — the broad shield recovers a block more slowly than the narrow
    /// one, mirroring the pace ordering of section 6.1 — and a pace ceiling
    /// in the inclusive range <c>[1, 10_000]</c> basis points.
    /// </summary>
    private static void ValidateShieldBlockRecoveryCoupling(
        bool appliesShieldBlockRecovery,
        int tallShieldBlockRecoveryTicks,
        int narrowShieldBlockRecoveryTicks,
        int shieldBlockRecoveryPaceCeilingBasisPoints)
    {
        if (!appliesShieldBlockRecovery)
        {
            if (tallShieldBlockRecoveryTicks != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tallShieldBlockRecoveryTicks),
                    tallShieldBlockRecoveryTicks,
                    "A preset that does not apply shield block recovery " +
                    "must register a zero tall-shield recovery duration.");
            }

            if (narrowShieldBlockRecoveryTicks != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(narrowShieldBlockRecoveryTicks),
                    narrowShieldBlockRecoveryTicks,
                    "A preset that does not apply shield block recovery " +
                    "must register a zero narrow-shield recovery duration.");
            }

            if (shieldBlockRecoveryPaceCeilingBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shieldBlockRecoveryPaceCeilingBasisPoints),
                    shieldBlockRecoveryPaceCeilingBasisPoints,
                    "A preset that does not apply shield block recovery " +
                    "must register a zero pace ceiling.");
            }

            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            narrowShieldBlockRecoveryTicks,
            nameof(narrowShieldBlockRecoveryTicks));

        if (tallShieldBlockRecoveryTicks <= narrowShieldBlockRecoveryTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tallShieldBlockRecoveryTicks),
                tallShieldBlockRecoveryTicks,
                "A preset that applies shield block recovery must " +
                "register a tall-shield recovery duration strictly " +
                "greater than its narrow-shield recovery duration " +
                $"({narrowShieldBlockRecoveryTicks}).");
        }

        if (shieldBlockRecoveryPaceCeilingBasisPoints < 1 ||
            shieldBlockRecoveryPaceCeilingBasisPoints > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shieldBlockRecoveryPaceCeilingBasisPoints),
                shieldBlockRecoveryPaceCeilingBasisPoints,
                "A preset that applies shield block recovery must " +
                "register a pace ceiling in the inclusive range " +
                "[1, 10000] basis points.");
        }
    }

    /// <summary>
    /// The shield-encumbrance half of the coupled validation, per
    /// shield-projectile-block design section 6.1. A preset that does not
    /// apply the effect must register two zero paces, so its
    /// <see cref="ContentHash"/> fold stays exactly what it is today. A
    /// preset that does apply it must register a strictly positive
    /// narrow-breast-high-shield pace strictly below
    /// <see cref="FullShieldPaceBasisPoints"/>, and a strictly positive
    /// tall-hardwood-shield pace strictly below the narrow-shield one —
    /// the heavier board encumbers more than the narrower one, mirroring
    /// the recovery-duration ordering of
    /// <see cref="ValidateShieldBlockRecoveryCoupling"/>.
    /// </summary>
    private static void ValidateShieldEncumbranceCoupling(
        bool appliesShieldEncumbrance,
        int narrowBreastHighShieldPaceBasisPoints,
        int tallHardwoodShieldPaceBasisPoints)
    {
        if (!appliesShieldEncumbrance)
        {
            if (narrowBreastHighShieldPaceBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(narrowBreastHighShieldPaceBasisPoints),
                    narrowBreastHighShieldPaceBasisPoints,
                    "A preset that does not apply shield encumbrance must " +
                    "register a zero narrow-breast-high-shield pace.");
            }

            if (tallHardwoodShieldPaceBasisPoints != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tallHardwoodShieldPaceBasisPoints),
                    tallHardwoodShieldPaceBasisPoints,
                    "A preset that does not apply shield encumbrance must " +
                    "register a zero tall-hardwood-shield pace.");
            }

            return;
        }

        if (narrowBreastHighShieldPaceBasisPoints <= 0 ||
            narrowBreastHighShieldPaceBasisPoints >= FullShieldPaceBasisPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(narrowBreastHighShieldPaceBasisPoints),
                narrowBreastHighShieldPaceBasisPoints,
                "A preset that applies shield encumbrance must register a " +
                "narrow-breast-high-shield pace strictly between zero and " +
                $"{FullShieldPaceBasisPoints} basis points.");
        }

        if (tallHardwoodShieldPaceBasisPoints <= 0 ||
            tallHardwoodShieldPaceBasisPoints >=
                narrowBreastHighShieldPaceBasisPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tallHardwoodShieldPaceBasisPoints),
                tallHardwoodShieldPaceBasisPoints,
                "A preset that applies shield encumbrance must register a " +
                "tall-hardwood-shield pace strictly between zero and its " +
                "narrow-breast-high-shield pace " +
                $"({narrowBreastHighShieldPaceBasisPoints}).");
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

        if (AppliesShieldBlockRecovery)
        {
            // Shield-projectile-block design section 6.2: the same version
            // gate as the pressure-interrupt fold above. Every preset that
            // does not apply shield block recovery writes nothing here at
            // all, so V1 through V13 keep the exact byte sequence they fold
            // today, and their pinned ContentHash literals and frozen
            // trajectory digests do not move. The flag itself is never
            // folded: inside this branch it is always true, so folding it
            // would contribute a constant that discriminates nothing — the
            // branch's presence is what records the distinction. The three
            // values fold here in declaration order.
            Fnv1a.Add(ref hash, (ulong)TallShieldBlockRecoveryTicks);
            Fnv1a.Add(ref hash, (ulong)NarrowShieldBlockRecoveryTicks);
            Fnv1a.Add(
                ref hash,
                (ulong)ShieldBlockRecoveryPaceCeilingBasisPoints);
        }

        if (AppliesShieldEncumbrance)
        {
            // Shield-projectile-block design section 6.1: the same version
            // gate as the block-recovery fold above. Every preset that does
            // not apply shield encumbrance writes nothing here at all, so
            // V1 through V13 keep the exact byte sequence they fold today,
            // and their pinned ContentHash literals and frozen trajectory
            // digests do not move. The flag itself is never folded, for the
            // same reason as every other gate in this method.
            Fnv1a.Add(ref hash, (ulong)NarrowBreastHighShieldPaceBasisPoints);
            Fnv1a.Add(ref hash, (ulong)TallHardwoodShieldPaceBasisPoints);
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

        return hash;
    }
}
