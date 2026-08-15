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
    /// the contingent close-latch design section 3.
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
    /// the contingent close-latch design section 3 for
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
    /// the contingent close-latch plan T6 flips it. See
    /// the contingent close-latch design section 3 for
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
    /// in favour of narrowing. Adopting it appeared not to clear the bar, until
    /// the bar's own gate-6 reconstruction was found to be measuring V4 with
    /// V3's rule; see
    /// the 2026-07-28 cohesion scan narrowing design
    /// for that measurement. It lands as a
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
    /// the 2026-07-29 leader rank design section 2 for why this
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
    /// the 2026-07-30 weapon-relative movement shared foundation design sections 3,
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
    /// the 2026-07-31 movement V7 pressure interrupt design sections
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
            // Each row's pressure-interrupt threshold is a provisional
            // reconstruction of gameplay tuning under CLAUDE.md section 7,
            // not a historical measurement. No source describes how a warrior
            // in the pre-colonial Philippines decided to break off a
            // committed blow, and none of these six numbers claims one.
            //
            // Every threshold is the row's own
            // LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints scaled by
            // SupportPressureWeightBasisPoints / 10,000 — 2.0:1 for Kampilan
            // and Wasay, 1.75:1 for the shielded Kalis row, 1.5:1 for Kalis
            // and the shielded Itak row, and 1.25:1 for Itak, each halved at
            // the shipped support weight of 5,000. That scaling is what makes
            // the intended reading of the threshold true: a warrior
            // interrupts at roughly the odds at which it would already have
            // refused to close. The values registered before task E1 were the
            // bare disengage ratios, compared against a weighted average that
            // is a different quantity in the same units, and three of the six
            // rows were unreachable as a result. The weighted sum can never
            // exceed 2 * SupportPressureWeightBasisPoints + 10,000 for a
            // warrior that survives the tick — 20,000 here — because the
            // weights are a true average, signal A saturates at
            // WeaponMovementRules.SignalCeilingBasisPoints, and a signal B of
            // 10,000 requires damage at least equal to maximum hit points,
            // which kills the agent. Kampilan and Wasay at 20,000 could
            // therefore never fire, and the shielded Kalis row at 17,500
            // needed all three signals saturated at once. The tuned values
            // below preserve the relative ordering the six weapon sessions
            // recorded and put every row inside reach.
            //
            // Reaching every row is not the same as terminating a battle, and
            // these values do not meet the design section 2.1 termination
            // bar: all ten cells of the section 2.2 matrix still ended in
            // Draw at the 10,000-tick limit. Neither did any other
            // configuration task E1 measured, including a probe that
            // registered the minimum threshold of 1 on all six rows and so
            // fired on every agent-tick the predicate can ever fire on. See
            // the 2026-07-31 movement V7 pressure interrupt calibration record.
            KampilanMovementProfile.Row
                .WithPressureInterruptThreshold(10_000),
            WasayMovementProfile.Row
                .WithPressureInterruptThreshold(10_000),
            KalisMovementProfile.Row
                .WithPressureInterruptThreshold(7_500),
            ItakMovementProfile.Row
                .WithPressureInterruptThreshold(6_250),
            TallHardwoodMovementProfiles.KalisRow
                .WithPressureInterruptThreshold(8_750),
            TallHardwoodMovementProfiles.ItakRow
                .WithPressureInterruptThreshold(7_500),
        ],
        appliesPressureInterrupt: true,

        // The three weights are provisional reconstructions of gameplay
        // tuning under CLAUDE.md section 7, not historical measurements. No
        // source describes how a warrior in the pre-colonial Philippines
        // decided to abandon a committed blow, and none is claimed here; they
        // were chosen in the hope of making a battle terminate, which task E1
        // then measured and disproved. They total exactly
        // MovementRuleset.TotalPressureInterruptWeightBasisPoints, which the
        // constructor's coupled validation requires. The split leans on
        // support odds because that signal is the one a spectator can read off
        // the screen, and gives damage taken more weight than allies lost
        // because the second is the slower of the two.
        //
        // Plan task E1 measured all three against design section 2.1's
        // termination bar and left them where they stand. Two alternative
        // splits were measured and neither did better: 7,000 / 2,000 / 1,000,
        // which raises the reachability ceiling to 24,000, and
        // 3,000 / 5,000 / 2,000, which leans on incoming damage instead. Both
        // drew every cell they were measured over, as this split does. The
        // full record, with the numbers and with the reason the interrupt
        // cannot reach the standoff at any tuning, is
        // the 2026-07-31 movement V7 pressure interrupt calibration record.
        supportPressureWeightBasisPoints: 5_000,
        incomingDamageWeightBasisPoints: 3_000,
        allyCollapseWeightBasisPoints: 2_000);

    /// <summary>
    /// The ranged-standoff preset. Every tunable is the same value
    /// <see cref="PersistentContingentsV4Ruleset"/> already carries, restated
    /// verbatim rather than referenced, following the "restate, do not
    /// reference" convention V4 already uses against V3; V8 declares no new
    /// field of its own, because the standoff hold is driven by preset
    /// identity and each agent's own <c>WeaponProfile.StandoffDistanceRaw</c>
    /// inside <c>BattleSimulation.GatherMovementProposals</c>, not by
    /// anything <see cref="MovementRuleset"/> stores.
    /// </summary>
    /// <remarks>
    /// Under V8, a warrior whose target lies at or inside its weapon's
    /// standoff distance reports <see cref="AgentIntent.Holding"/> and
    /// proposes no movement, instead of the ordinary pursuit or sidestep
    /// proposal V4 always builds. A warrior whose target lies beyond that
    /// distance still pursues, but stops short by the standoff distance
    /// instead of by two body radii. A melee weapon's
    /// <c>StandoffDistanceRaw</c> is always zero, so a melee-only roster sees
    /// none of this and moves byte-identically to
    /// <see cref="PersistentContingentsV4Ruleset"/>; V4 itself is untouched
    /// and stays the shipped default. See CLAUDE.md section 5: this is a new
    /// preset version rather than an edit to V4 because it changes simulated
    /// behaviour for any roster that fields a ranged weapon.
    /// </remarks>
    private static readonly MovementRuleset RangedStandoffV8Ruleset = new(
        id: MovementPresetId.RangedStandoffV8,
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
    /// The monotone-ally-clearance preset (RU-30, F-B). A verbatim
    /// restatement of <see cref="EquipmentRelativeFootworkV6Ruleset"/> --
    /// every field, including <c>usesEquipmentRelativeFootwork: true</c> and
    /// all six per-loadout movement profile rows -- not of
    /// <see cref="PersistentContingentsV4Ruleset"/>, because
    /// <c>BattleSimulation.IsLaneClearOfAllies</c> is reachable only from
    /// <c>TryProposeEquipmentRoute</c>, which
    /// <c>BattleSimulation.GatherMovementProposals</c> only calls when
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
    /// <see langword="true"/>. Registering V9 on V4's legacy field values
    /// would compile but never reach the new monotone rule at all, silently
    /// making the fix inert. The single behavioural difference from V6 is
    /// gated entirely on preset identity inside
    /// <c>IsLaneClearOfAllies</c> itself, not on any field this ruleset
    /// carries, exactly the way <see cref="RangedStandoffV8Ruleset"/>'s own
    /// standoff behaviour is gated on preset identity rather than a field.
    /// </summary>
    private static readonly MovementRuleset MonotoneAllyClearanceV9Ruleset = new(
        id: MovementPresetId.MonotoneAllyClearanceV9,
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
    /// The battlefield-realism preset. A verbatim restatement of
    /// <see cref="RangedStandoffV8Ruleset"/>'s field values under its own
    /// <c>id</c> -- every field, including
    /// <c>usesEquipmentRelativeFootwork: false</c> and
    /// <c>appliesPressureInterrupt: false</c> -- because V10 carries no new
    /// field of its own: its three behaviours are gated entirely on preset
    /// identity at each of their own call sites, exactly the way V8's
    /// standoff hold and V9's monotone clearance rule are already gated on
    /// preset identity rather than on anything this ruleset stores. All
    /// three behaviours are a labelled gameplay model, not a historical
    /// measurement, under CLAUDE.md section 7. See
    /// the battlefield realism design section 3.
    /// </summary>
    private static readonly MovementRuleset BattlefieldRealismV10Ruleset = new(
        id: MovementPresetId.BattlefieldRealismV10,
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
    /// The last-stand engagement preset. A verbatim restatement of
    /// <see cref="BattlefieldRealismV10Ruleset"/>'s field values under its own
    /// <c>id</c>, for the same reason V10 restates V8's: its two regroup
    /// yields are gated on preset identity at their own call site, so it
    /// carries no new field of its own. See
    /// the last-stand engagement plan.
    /// </summary>
    private static readonly MovementRuleset LastStandEngagementV11Ruleset = new(
        id: MovementPresetId.LastStandEngagementV11,
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
    /// The contingent-shape preset. A verbatim restatement of
    /// <see cref="LastStandEngagementV11Ruleset"/>'s field values under its
    /// own <c>id</c>, for the same reason V11 restates V10's: it is gated on
    /// preset identity at its own call site, so it carries no new field of
    /// its own. See the contingent shape task plan.
    /// </summary>
    private static readonly MovementRuleset ContingentShapeV12Ruleset = new(
        id: MovementPresetId.ContingentShapeV12,
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
    /// The cohort lateral-spread preset. A verbatim restatement of
    /// <see cref="LastStandEngagementV11Ruleset"/>'s field values under its
    /// own <c>id</c>, for the same reason <see cref="ContingentShapeV12Ruleset"/>
    /// restates it: the lateral-riffle deployment change it gates is gated
    /// on preset identity at its own call site, so it carries no new field
    /// of its own. See the cohort lateral spread design.
    /// </summary>
    private static readonly MovementRuleset CohortLateralSpreadV13Ruleset = new(
        id: MovementPresetId.CohortLateralSpreadV13,
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
    /// The in-fight evasive footwork preset. A verbatim restatement of
    /// <see cref="CohortLateralSpreadV13Ruleset"/>'s field values under its own
    /// <c>id</c>, for the same reason that ruleset restates V11's: the
    /// behaviour it gates is gated on preset identity at its own call site, so
    /// it carries no new field of its own.
    /// <para>
    /// <c>usesEquipmentRelativeFootwork</c> stays <see langword="false"/> and
    /// the loadout profile array stays empty. This preset adds movement
    /// <i>during</i> an engagement; it does not revive the equipment-relative
    /// route pipeline, which is a separate and much larger change. See the
    /// 2026-08-15 in-fight evasion design.
    /// </para>
    /// </summary>
    private static readonly MovementRuleset EvasiveFootworkV14Ruleset = new(
        id: MovementPresetId.EvasiveFootworkV14,
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
    /// The contingent-cohesion-before-contact preset. Every field down to
    /// <c>allyCollapseWeightBasisPoints</c> is a verbatim restatement of
    /// <see cref="LastStandEngagementV11Ruleset"/>'s registered values under
    /// its own <c>id</c>, exactly as
    /// <see cref="ContingentShapeV12Ruleset"/> and
    /// <see cref="CohortLateralSpreadV13Ruleset"/> restate them. Unlike those
    /// two it does carry fields of its own: it is the first preset to register
    /// <see cref="MovementRuleset.GathersContingentsBeforeContact"/> as
    /// <see langword="true"/>, which is also the gate that admits the three
    /// tunables below into <see cref="MovementRuleset.ContentHash"/>. Every
    /// preset above leaves that gate <see langword="false"/> and registers the
    /// three tunables at zero, so none of their content hashes moves. See the
    /// "Contingent cohesion before contact — plan".
    /// </summary>
    /// <remarks>
    /// The three tunables are a <b>provisional reconstruction</b> for gameplay
    /// purposes and carry no evidentiary basis whatever: no source describes
    /// how close a warrior stood to the man leading his contingent, or how
    /// much ground such a group claimed, and none of these numbers is offered
    /// as a historical measurement.
    /// <para>
    /// They were chosen from measurement rather than from taste. The
    /// calibration harness swept seeds 1 through 20 under this preset and
    /// under <see cref="MovementPresetId.CohortLateralSpreadV13"/> at seven
    /// candidate settings, reporting the hold share, the granted-cohesion
    /// share, the tick of first contact, and the terminal tick for each. At
    /// the registered one third and 6000 basis points, the share of
    /// living-contingent-ticks resolved to Hold rises from 10.04 to 11.65 per
    /// cent and the share of Advance members granted a cohesion destination
    /// rises from 1.83 to 13.37 per cent, with all twenty seeds still decided
    /// before the cap and a median terminal tick of 2058 against V13's 2328 —
    /// so contingents gather more and battles finish sooner rather than later.
    /// The full table is in the plan's results section.
    /// </para>
    /// <para>
    /// Wider bands measured better still on both shares and were rejected
    /// deliberately. At one quarter, nearly every member of an advancing
    /// contingent is eligible on nearly every tick, which stops being "close
    /// up when the group is spread" and becomes "always walk to the aim
    /// point" — the degenerate twin of the behaviour this preset exists to
    /// produce, and one that would read on screen as a contingent that never
    /// advances freely at all. One third leaves the inner third of each
    /// contingent exempt.
    /// </para>
    /// </remarks>
    private static readonly MovementRuleset ContingentCohesionBeforeContactV15Ruleset = new(
        id: MovementPresetId.ContingentCohesionBeforeContactV15,
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
        allyCollapseWeightBasisPoints: 0,
        gathersContingentsBeforeContact: true,
        cohesionBandNumerator: 1,
        cohesionBandDenominator: 3,
        cohesionSquareMarginBasisPoints: 6000);

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
            MovementPresetId.RangedStandoffV8 => true,
            MovementPresetId.MonotoneAllyClearanceV9 => true,
            MovementPresetId.BattlefieldRealismV10 => true,
            MovementPresetId.LastStandEngagementV11 => true,
            MovementPresetId.ContingentShapeV12 => true,
            MovementPresetId.CohortLateralSpreadV13 => true,
            MovementPresetId.EvasiveFootworkV14 => true,
            MovementPresetId.ContingentCohesionBeforeContactV15 => true,
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
            MovementPresetId.RangedStandoffV8 => RangedStandoffV8Ruleset,
            MovementPresetId.MonotoneAllyClearanceV9 => MonotoneAllyClearanceV9Ruleset,
            MovementPresetId.BattlefieldRealismV10 => BattlefieldRealismV10Ruleset,
            MovementPresetId.LastStandEngagementV11 => LastStandEngagementV11Ruleset,
            MovementPresetId.ContingentShapeV12 => ContingentShapeV12Ruleset,
            MovementPresetId.CohortLateralSpreadV13 => CohortLateralSpreadV13Ruleset,
            MovementPresetId.EvasiveFootworkV14 => EvasiveFootworkV14Ruleset,
            MovementPresetId.ContingentCohesionBeforeContactV15 =>
                ContingentCohesionBeforeContactV15Ruleset,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Movement preset {id} is not registered."),
        };
}
