using System.Collections.Immutable;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Movement;

/// <summary>
/// Exhaustive registry of authoritative movement rulesets keyed by
/// <see cref="MovementPresetId"/>. New presets require a new enum value and
/// a corresponding switch arm here; unregistered values fail loudly rather
/// than silently falling back to a default ruleset.
/// </summary>
public static class MovementPresetRegistry
{
    /// <summary>
    /// The frozen preset. What is frozen is this preset's simulated
    /// behaviour, proved byte-identically by
    /// tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json,
    /// not its field list. <see cref="MovementRuleset.ContentHash"/> does
    /// reach the state hash, but only for a preset whose
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
    /// <see langword="true"/>: <c>BattleSimulation.ComputeStateHash</c> hands
    /// the movement content hash to <c>StateHasher.Compute</c> for those and
    /// hands <see langword="null"/> for the rest
    /// (src/Hukbo.Core/Simulation/BattleSimulation.cs:642-656,
    /// src/Hukbo.Core/Determinism/StateHasher.cs:81-84). This preset
    /// registers that flag <see langword="false"/>, so a task that adds a
    /// field to <see cref="MovementRuleset"/> moves only the pinned
    /// <c>ContentHash</c> identity literal in
    /// <c>MovementPresetRegistryTests</c> — recomputed from the built code,
    /// never guessed — and leaves this preset's actual behaviour and state
    /// hash untouched. That reasoning does not carry over to
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>, which does
    /// fold the movement content hash. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3.
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c> are
    /// registered here at <c>(0, 1)</c>, which collapses both the entry and
    /// exit thresholds in <c>MovementRules.ResolveContingentState</c> to
    /// <c>Max(1, ...)</c> — "at least one member in contact" — exactly
    /// today's minimum-distance rule, so this preset's behaviour does not
    /// move.
    /// </summary>
    private static readonly MovementRuleset IndependentPursuitV1Ruleset = new(
        id: MovementPresetId.IndependentPursuitV1,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 0,
        closeFractionDenominator: 1,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false,
        usesEquipmentRelativeFootwork: false,
        immediateRadiusBodyDiametersBasisPoints: 0,
        supportRadiusBodyDiametersBasisPoints: 0,
        loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The persistent-contingent preset. Every tunable is the same value
    /// <see cref="IndependentPursuitV1Ruleset"/> already carries.
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c> are
    /// registered here at <c>(0, 1)</c> for the same reason as above: the
    /// floor of <c>Max(1, ...)</c> makes the fraction reproduce today's
    /// minimum-distance rule exactly, so introducing the fields moves no
    /// behaviour under this preset either. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3 for
    /// the derivation of each value.
    /// </summary>
    private static readonly MovementRuleset PersistentContingentsV2Ruleset = new(
        id: MovementPresetId.PersistentContingentsV2,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 0,
        closeFractionDenominator: 1,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false,
        usesEquipmentRelativeFootwork: false,
        immediateRadiusBodyDiametersBasisPoints: 0,
        supportRadiusBodyDiametersBasisPoints: 0,
        loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The contact-fraction preset. Every tunable is the same value
    /// <see cref="PersistentContingentsV2Ruleset"/> already carries except
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>,
    /// registered here at <c>(1, 2)</c>: transition rule 3 closes the
    /// contingent once at least half its living members have a selected
    /// target inside the close radius, instead of the single-member minimum
    /// the <c>(0, 1)</c> floor reproduces. Not the shipped default — reachable
    /// only through <c>--movement-preset</c> until
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md T6 flips it. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3 for
    /// the derivation.
    /// </summary>
    /// <remarks>
    /// The pair <c>(1, 2)</c> is a provisional game-design choice, not a
    /// historical measurement. No source describes a unit's contact
    /// threshold, and half-to-close with a quarter-to-re-open is a starting
    /// point chosen for the shape it produces, not a quantity derived from
    /// anything. It is re-measured by <c>Hukbo.Tools.ContingentShape</c> in
    /// plan task T7, and the exit band in particular — half the entry
    /// fraction — is the open question design section 7 records.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV3Ruleset = new(
        id: MovementPresetId.PersistentContingentsV3,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false,
        usesEquipmentRelativeFootwork: false,
        immediateRadiusBodyDiametersBasisPoints: 0,
        supportRadiusBodyDiametersBasisPoints: 0,
        loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The narrowed-cohesion-scan preset, and the shipped default. Every
    /// tunable is the same value <see cref="PersistentContingentsV3Ruleset"/>
    /// already carries; the single difference is
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>, registered here
    /// at <see langword="true"/>, which restricts movement gate 6 to
    /// contingents that could actually be granted cohesion this tick.
    /// </summary>
    /// <remarks>
    /// The narrowing is the remedy design section 3.5 pre-analysed, declined,
    /// and named as "the first remedy if the inertness bar in section 10.3
    /// fails"; section 13 question 8 reserved the ordering for the user. The
    /// bar did fail — <c>CohesionCoverageIsNotPracticallyInertAcrossSeedsOneThroughTwenty</c>
    /// reported seed 11, faction 1 with no cohering tick in the later half of
    /// a 138-tick pre-<c>Close</c> window — and the user answered question 8
    /// in favour of narrowing. Adopting it did not clear the bar; see
    /// docs/plans/2026-07-28-cohesion-scan-narrowing-design.md for what the
    /// measurement found instead. It lands as a
    /// new preset rather than as an edit to
    /// <see cref="PersistentContingentsV3Ruleset"/> because V3 has already
    /// shipped as a default: CLAUDE.md section 5 requires a new preset version
    /// plus new golden expectations for any change that moves simulated
    /// behaviour, and <c>PersistentContingentsV2</c> and V3 both keep the
    /// behaviour their own recorded expectations pin.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV4Ruleset = new(
        id: MovementPresetId.PersistentContingentsV4,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: false,
        usesEquipmentRelativeFootwork: false,
        immediateRadiusBodyDiametersBasisPoints: 0,
        supportRadiusBodyDiametersBasisPoints: 0,
        loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The rank-aware leader-scan preset. Every tunable is the same value
    /// <see cref="PersistentContingentsV4Ruleset"/> already carries,
    /// restated verbatim rather than referenced, following the
    /// "restate, do not reference" convention V4 already uses against V3;
    /// the single difference is <see cref="MovementRuleset.SelectsLeaderByRank"/>,
    /// registered here at <see langword="true"/>, which orders the leader
    /// scan's candidates by <c>(RankId ascending, EntityId ascending)</c>
    /// instead of <c>EntityId</c> alone.
    /// </summary>
    /// <remarks>
    /// It lands as a new preset rather than as an edit to
    /// <see cref="PersistentContingentsV4Ruleset"/> because V4 has already
    /// shipped as a default: CLAUDE.md section 5 requires a new preset
    /// version plus new golden expectations for any change that moves
    /// simulated behaviour, and V1 through V4 all keep the behaviour their
    /// own recorded expectations pin. See
    /// docs/plans/2026-07-29-leader-rank-design.md section 2 for why this
    /// comparator swap reaches further into the simulation than the leader
    /// scan itself.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV5Ruleset = new(
        id: MovementPresetId.PersistentContingentsV5,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: true,
        usesEquipmentRelativeFootwork: false,
        immediateRadiusBodyDiametersBasisPoints: 0,
        supportRadiusBodyDiametersBasisPoints: 0,
        loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The opt-in equipment-relative footwork preset. It carries
    /// <see cref="PersistentContingentsV5Ruleset"/>'s cohesion tunables
    /// unchanged and registers
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/>
    /// <see langword="true"/>, the two local-context radii — 2.5 body
    /// diameters immediate, 6 body diameters support — and the six
    /// per-loadout movement profile rows in canonical
    /// <c>KP, WA, KA, IT, KS, IS</c> order. Both radii and every profile
    /// value are provisional reconstructions: gameplay tuning; no historical
    /// measurement. This entry is no longer registration only: twelve
    /// <c>BattleSimulation</c> code paths consult the flag — lines 142, 146,
    /// 297, 420, 584, 593, 606, 654, 922, 1461, 3183, and 3337 of
    /// src/Hukbo.Core/Simulation/BattleSimulation.cs — so selecting this
    /// preset changes local-context derivation, footwork pacing, attack
    /// marking, and the state hash fold itself. The shipped default
    /// nonetheless stays
    /// <see cref="MovementPresetId.PersistentContingentsV4"/>, and the preset
    /// is reachable only through explicit selection. See
    /// docs/plans/2026-07-30-weapon-movement-foundation-design.md sections 3,
    /// 5, and 13.
    /// This entry registers
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/>
    /// <see langword="false"/> with three zero weights, exactly as the five
    /// presets above do. Because that flag is the version gate the four
    /// pressure-interrupt values fold behind, this preset writes none of them
    /// into <see cref="MovementRuleset.ContentHash"/>, and its pinned identity
    /// literal and frozen trajectory digest are unchanged by their addition —
    /// which matters here more than above, because this is the first preset
    /// whose movement content hash reaches the state hash.
    /// </summary>
    private static readonly MovementRuleset EquipmentRelativeFootworkV6Ruleset = new(
        id: MovementPresetId.EquipmentRelativeFootworkV6,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: true,
        usesEquipmentRelativeFootwork: true,
        immediateRadiusBodyDiametersBasisPoints: 25_000,
        supportRadiusBodyDiametersBasisPoints: 60_000,
        loadoutMovementProfiles:
        [
            KampilanMovementProfile.Row,
            WasayMovementProfile.Row,
            KalisMovementProfile.Row,
            ItakMovementProfile.Row,
            TallHardwoodMovementProfiles.KalisRow,
            TallHardwoodMovementProfiles.ItakRow,
        ],
        appliesPressureInterrupt: false,
        supportPressureWeightBasisPoints: 0,
        incomingDamageWeightBasisPoints: 0,
        allyCollapseWeightBasisPoints: 0);

    /// <summary>
    /// The pressure-interrupt preset. It carries
    /// <see cref="EquipmentRelativeFootworkV6Ruleset"/>'s cohesion tunables,
    /// both local-context radii, and all six per-loadout movement profile rows
    /// forward unchanged; the single difference is
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/>, registered here
    /// at <see langword="true"/> along with the three signal weights the
    /// interrupt's weighted sum uses and, on every row, the threshold that sum
    /// is compared against. All four are passed by name below rather than left
    /// to their trailing defaults: those defaults exist so the five presets
    /// above and V6 could keep compiling untouched when the members landed, and
    /// a V7 entry that omitted them would compile cleanly, register the flag
    /// <see langword="false"/>, and silently never fire the feature this preset
    /// exists for.
    /// The shipped default nonetheless stays
    /// <see cref="MovementPresetId.PersistentContingentsV4"/>, and this preset
    /// is reachable only through explicit selection. See
    /// docs/plans/2026-07-31-movement-v7-pressure-interrupt-design.md sections
    /// 4.6, 6.2, and 6.3.
    /// </summary>
    /// <remarks>
    /// The cohesion tunables and both radii are restated verbatim rather than
    /// referenced, following the "restate, do not reference" convention every
    /// preset above uses. The six profile rows are the exception: they are
    /// derived from V6's rows through
    /// <see cref="LoadoutMovementProfile.WithPressureInterruptThreshold"/>
    /// rather than duplicated, because duplicating sixteen scalars per row
    /// would let V7's and V6's shared tuning drift apart before a task
    /// deliberately moves it (design section 6.3).
    /// It lands as a new preset rather than as an edit to
    /// <see cref="EquipmentRelativeFootworkV6Ruleset"/> because V6 has already
    /// shipped: CLAUDE.md section 5 requires a new preset version plus new
    /// golden expectations for any change that moves simulated behaviour, and
    /// V1 through V6 all keep the behaviour their own recorded expectations
    /// pin.
    /// </remarks>
    private static readonly MovementRuleset EquipmentRelativeFootworkV7Ruleset = new(
        id: MovementPresetId.EquipmentRelativeFootworkV7,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: true,
        usesEquipmentRelativeFootwork: true,
        immediateRadiusBodyDiametersBasisPoints: 25_000,
        supportRadiusBodyDiametersBasisPoints: 60_000,
        loadoutMovementProfiles:
        [
            WithDisengageRatioAsStartingThreshold(KampilanMovementProfile.Row),
            WithDisengageRatioAsStartingThreshold(WasayMovementProfile.Row),
            WithDisengageRatioAsStartingThreshold(KalisMovementProfile.Row),
            WithDisengageRatioAsStartingThreshold(ItakMovementProfile.Row),
            WithDisengageRatioAsStartingThreshold(
                TallHardwoodMovementProfiles.KalisRow),
            WithDisengageRatioAsStartingThreshold(
                TallHardwoodMovementProfiles.ItakRow),
        ],
        appliesPressureInterrupt: true,

        // The three weights are provisional reconstructions of gameplay
        // tuning under CLAUDE.md section 7, not historical measurements. No
        // source describes how a warrior in the pre-colonial Philippines
        // decided to abandon a committed blow, and none is claimed here; they
        // are chosen to make a battle terminate. They total exactly
        // MovementRuleset.TotalPressureInterruptWeightBasisPoints, which the
        // constructor's coupled validation requires. The split leans on
        // support odds because that signal is the one a spectator can read off
        // the screen, and gives damage taken more weight than allies lost
        // because the second is the slower of the two. Plan task E1 re-tunes
        // all three against design section 2.1's termination bar and records
        // what it measured; these are a principled starting point, not a
        // final answer.
        supportPressureWeightBasisPoints: 5_000,
        incomingDamageWeightBasisPoints: 3_000,
        allyCollapseWeightBasisPoints: 2_000);

    /// <summary>
    /// Returns the supplied V6 profile row with its pressure-interrupt
    /// threshold registered at its own
    /// <see cref="LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints"/>.
    /// The weighted pressure sum and that ratio are measured in the same
    /// basis-point space and the first signal feeding the sum <em>is</em> the
    /// enemy-to-ally ratio, so a row's existing disengage ratio is the natural
    /// starting bar for interrupting a committed blow: a warrior interrupts at
    /// roughly the odds at which it would already have refused to close.
    /// Deriving it keeps each row's starting threshold in step with the tuning
    /// it is drawn from instead of restating a literal that could drift from
    /// it.
    /// </summary>
    /// <remarks>
    /// Every value this produces is a provisional reconstruction of gameplay
    /// tuning under CLAUDE.md section 7, not a historical measurement. Plan
    /// task E1 replaces these calls with per-row tuned literals once design
    /// section 2.1's termination bar has been measured. Each result lands
    /// inside the inclusive range <c>[1, SignalCeilingBasisPoints]</c> the
    /// constructor's coupled validation requires — 20,000 for Kampilan and
    /// Wasay, 15,000 for Kalis, 12,500 for Itak, 17,500 for the shielded
    /// Kalis row, and 15,000 for the shielded Itak row, against a ceiling of
    /// 30,000.
    /// </remarks>
    private static LoadoutMovementProfile WithDisengageRatioAsStartingThreshold(
        LoadoutMovementProfile row) =>
        row.WithPressureInterruptThreshold(row.DisengageEnemyToAllyBasisPoints);

    public static bool IsRegistered(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => true,
            MovementPresetId.PersistentContingentsV2 => true,
            MovementPresetId.PersistentContingentsV3 => true,
            MovementPresetId.PersistentContingentsV4 => true,
            MovementPresetId.PersistentContingentsV5 => true,
            MovementPresetId.EquipmentRelativeFootworkV6 => true,
            MovementPresetId.EquipmentRelativeFootworkV7 => true,
            _ => false,
        };

    public static MovementRuleset Get(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => IndependentPursuitV1Ruleset,
            MovementPresetId.PersistentContingentsV2 => PersistentContingentsV2Ruleset,
            MovementPresetId.PersistentContingentsV3 => PersistentContingentsV3Ruleset,
            MovementPresetId.PersistentContingentsV4 => PersistentContingentsV4Ruleset,
            MovementPresetId.PersistentContingentsV5 => PersistentContingentsV5Ruleset,
            MovementPresetId.EquipmentRelativeFootworkV6 => EquipmentRelativeFootworkV6Ruleset,
            MovementPresetId.EquipmentRelativeFootworkV7 => EquipmentRelativeFootworkV7Ruleset,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Movement preset {id} is not registered."),
        };
}
