namespace Hukbo.Core.Movement;

/// <summary>
/// Stable movement preset identity. Numeric values are part of the
/// deterministic replay and content-hash contract; do not renumber or
/// reorder. A new ruleset requires a new value plus a new
/// <see cref="MovementPresetRegistry"/> entry.
/// </summary>
public enum MovementPresetId
{
    /// <summary>
    /// Today's behaviour, frozen. Every warrior pursues its nearest enemy
    /// independently; contingents exist only at deployment and are never
    /// consulted again; no cohesion, no unit state, no arrival taper. See
    /// the formation and movement realism design
    /// section 6.2 for what "frozen" binds here.
    /// </summary>
    IndependentPursuitV1 = 1,

    /// <summary>
    /// Contingent membership survives past deployment. Every living member
    /// carries a <see cref="Simulation.ContingentState"/>, resolved every
    /// tick by the six priority-ordered transition rules of design section
    /// 3.4; a member that has fallen behind its contingent, or the whole
    /// contingent while gathering, may be given a cohesion destination
    /// instead of independent pursuit, subject to the six movement gates and
    /// the cohesion duty cycle of design section 3.5. See
    /// the formation and movement realism design
    /// sections 3.4 through 3.6.
    /// </summary>
    PersistentContingentsV2 = 2,

    /// <summary>
    /// Contingent membership survives past deployment. Every living member
    /// carries <see cref="Simulation.ContingentState"/>, resolved
    /// tick by six priority-ordered transition rules design section
    /// 3.4; member fallen behind contingent, whole
    /// contingent while gathering, may be given cohesion destination
    /// instead independent pursuit, subject six movement gates
    /// cohesion duty cycle design section 3.5. Identical to
    /// <see cref="PersistentContingentsV2"/> in every respect except transition
    /// rule 3, which now closes the contingent once at least half its living
    /// members have a selected target inside the close radius, and re-opens it
    /// once that fraction drops below a quarter, instead of the single-member
    /// minimum <see cref="PersistentContingentsV2"/> uses. See
    /// the contingent close-latch design section 3 for
    /// the derivation.
    /// </summary>
    PersistentContingentsV3 = 3,

    /// <summary>
    /// The narrowed-cohesion-scan preset. Identical to
    /// <see cref="PersistentContingentsV3"/> in every tunable; the one
    /// difference is that movement gate 6, the cross-contingent bias-square
    /// overlap test of design section 3.5, walks only those same-faction
    /// contingents that could actually be granted cohesion this tick, and
    /// skips any contingent whose tick-start
    /// <see cref="Simulation.ContingentState"/> is
    /// <see cref="Simulation.ContingentState.Close"/> or
    /// <see cref="Simulation.ContingentState.Break"/>. Such a contingent parks
    /// no cohesion aim points anywhere, because gate 1 already sends every one
    /// of its members to independent pursuit, so excluding it preserves the
    /// combined-density statement of design section 3.5 exactly while removing
    /// the chain-denial path that made cohesion inert once a faction's leading
    /// contingents reached the enemy. This is the remedy design section 3.5
    /// pre-analysed and declined, and section 13 question 8 reserved for the
    /// user; the user answered it in favour of narrowing after section 10.3's
    /// inertness bar failed. The trade it accepts is recorded in section
    /// 13 question 6: <c>Close</c> and <c>Break</c> members standing inside a
    /// granted square move out of "excluded by gate 6" and into the unbounded
    /// body-occupancy residual.
    /// </summary>
    /// <remarks>
    /// The narrowing provably stops a <c>Close</c> contingent denying its
    /// neighbours, which is what
    /// <c>UnderTheNarrowedScanACloseContingentStopsDenyingItsNeighbours</c>
    /// pins. It appeared at first not to fix the inertness bar, and to fail it
    /// on more faction-seeds than before rather than fewer. That turned out to
    /// be a defect in the bar's own gate-6 reconstruction, which scanned every
    /// living contingent while measuring the one preset whose distinguishing
    /// rule is that it does not; with the observer corrected, all three clauses
    /// pass on this preset. The evidence is recorded in
    /// docs/archives/2026-08-07/2026-07-28-cohesion-scan-narrowing-design.md.
    /// Section 13 question 7 — whether the three thresholds are the right ones
    /// — is still open, so do not read a bar that no longer fails as a bar
    /// proved right.
    /// </remarks>
    PersistentContingentsV4 = 4,

    /// <summary>
    /// The rank-aware leader-scan preset. Identical to
    /// <see cref="PersistentContingentsV4"/> in every cohesion tunable; the
    /// single difference is that the leader scan orders candidates by
    /// <c>(RankId ascending, EntityId ascending)</c> instead of
    /// <c>EntityId</c> alone, so a contingent's highest-ranking living
    /// member leads it rather than its lowest-entity-id living member. See
    /// docs/archives/2026-08-07/2026-07-29-leader-rank-design.md for the reasoning and
    /// the leader rank plan task L1 for this
    /// preset's derivation.
    /// </summary>
    PersistentContingentsV5 = 5,

    /// <summary>
    /// The opt-in equipment-relative footwork preset. Identical to
    /// <see cref="PersistentContingentsV5"/> in every cohesion tunable; the
    /// difference is that it registers
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/>
    /// <see langword="true"/> together with the two local-context radii and
    /// the six per-loadout movement profiles of the weapon-relative movement
    /// design, resolved rank-independently through
    /// <see cref="MovementRuleset.ResolveLoadoutProfile"/>. It is reachable
    /// only through explicit selection — the shipped default stays
    /// <see cref="PersistentContingentsV4"/> — and every profile value it
    /// carries is a provisional reconstruction for gameplay tuning, not a
    /// historical measurement. See
    /// docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md sections 3,
    /// 5, and 13.
    /// </summary>
    EquipmentRelativeFootworkV6 = 6,

    /// <summary>
    /// The pressure-interrupt preset. It carries
    /// <see cref="EquipmentRelativeFootworkV6"/>'s cohesion tunables, context
    /// radii, and six per-loadout movement profile rows forward unchanged, and
    /// adds one thing: it registers
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/>
    /// <see langword="true"/>, so local pressure may interrupt a committed
    /// blow. A warrior whose weighted pressure signal — enemy-to-ally support
    /// odds, damage taken on the previous tick, and supporting allies lost
    /// since the previous tick — reaches its row's registered threshold
    /// abandons the blow and resolves footwork to
    /// <c>FootworkPhase.Disengage</c> instead of finishing the commitment
    /// timer. The version gate is deliberately separate from
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/>, which
    /// <see cref="EquipmentRelativeFootworkV6"/> already registers
    /// <see langword="true"/>: gating on that flag instead would move V6's
    /// state hash and frozen trajectory digest.
    /// It is reachable only through explicit selection — the shipped default
    /// stays <see cref="PersistentContingentsV4"/> — and every weight and
    /// threshold it carries is a provisional reconstruction for gameplay
    /// tuning under CLAUDE.md section 7, not a historical measurement. See
    /// docs/archives/2026-08-06/movement/2026-07-31-movement-v7-pressure-interrupt-design.md sections
    /// 4.6, 6.2, and 6.3.
    /// </summary>
    EquipmentRelativeFootworkV7 = 7,

    /// <summary>
    /// The ranged-standoff preset. A verbatim restatement of
    /// <see cref="PersistentContingentsV4"/>'s values plus one rule, with
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> and
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> both
    /// <see langword="false"/>: a warrior whose target is beyond its weapon's
    /// standoff distance pursues with that distance as its stop-short radius
    /// instead of body contact; a warrior whose target is at or inside that
    /// distance proposes no movement and is assigned
    /// <c>AgentIntent.Holding</c> instead. A melee weapon's
    /// standoff distance is zero, so a melee-only roster behaves
    /// byte-identically to <see cref="PersistentContingentsV4"/>, which stays
    /// registered, unmodified, and the shipped default. See design section 6
    /// and plan task RU-21.
    /// </summary>
    RangedStandoffV8 = 8,

    /// <summary>
    /// The monotone-ally-clearance preset. Carries every tunable of
    /// <see cref="PersistentContingentsV4"/> forward unchanged except that
    /// <c>IsLaneClearOfAllies</c> rejects a candidate endpoint only when it
    /// moves the actor closer to an ally it is already too close to, rather
    /// than on the ally's absolute distance alone — the F-B fix for the
    /// standoff root cause recorded in design section 10.3. V4, V6, and V7
    /// keep their own content hashes and frozen trajectory digests
    /// byte-identical. See plan task RU-30.
    /// </summary>
    MonotoneAllyClearanceV9 = 9,

    /// <summary>
    /// The battlefield-realism preset. A verbatim restatement of
    /// <see cref="RangedStandoffV8"/>'s registered field values under its own
    /// <c>id</c>, following the same convention <see cref="RangedStandoffV8"/>
    /// and <see cref="MonotoneAllyClearanceV9"/> already use: the behaviour is
    /// gated on preset identity, never on a <see cref="MovementRuleset"/>
    /// field, so this value carries no new field of its own. Three
    /// behaviours are labelled as a gameplay model rather than a historical
    /// measurement — deployment groups warriors by weapon so a body of
    /// troops reads as one weapon rather than an even slice of the whole
    /// army, shield bearers take the forward-most slots of their own
    /// contingent, and a ranged warrior with a melee enemy inside a threat
    /// radius backs away and resumes shooting once clear or cornered. See
    /// docs/plans/2026-08-11-battlefield-realism-design.md.
    /// </summary>
    BattlefieldRealismV10 = 10,
}
