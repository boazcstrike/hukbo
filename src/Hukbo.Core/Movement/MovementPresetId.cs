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
    /// pass on this preset. The evidence is recorded in the 2026-07-28
    /// cohesion scan narrowing design.
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
    /// the 2026-07-29 leader rank design for the reasoning and
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
    /// the 2026-07-30 weapon-relative movement shared foundation design sections 3,
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
    /// the 2026-07-31 movement V7 pressure interrupt design sections
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
    /// the battlefield realism design.
    /// </summary>
    BattlefieldRealismV10 = 10,

    /// <summary>
    /// The last-stand engagement preset. A verbatim restatement of
    /// <see cref="BattlefieldRealismV10"/>'s registered field values under its
    /// own <c>id</c>, carrying every one of that preset's behaviours forward
    /// unchanged, and adding two yields to the last-stand regroup override.
    /// A follower is no longer marked <c>AgentIntent.Regrouping</c> when its
    /// faction's rally agent is itself within its own weapon reach of an
    /// enemy, or when the follower's own selected enemy is within the
    /// follower's own weapon reach. Both yields hand the warrior back to
    /// ordinary pursuit, so it closes on the enemy target selection already
    /// resolved for it rather than on a trail point behind its leader, and the
    /// endgame reads as two small bands colliding rather than as a duel with
    /// witnesses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The last-stand code this changes is shared and unversioned: the
    /// engagement gate is a <see cref="Simulation.Scenario"/> field and the
    /// geometry is <c>const</c> on the static
    /// <see cref="Simulation.FormationRules"/>, so neither is reachable from a
    /// movement ruleset. Both yields are therefore gated on preset identity at
    /// their own call site, the same convention
    /// <see cref="RangedStandoffV8"/>, <see cref="MonotoneAllyClearanceV9"/>,
    /// and <see cref="BattlefieldRealismV10"/> already use, and this value
    /// carries no new <see cref="MovementRuleset"/> field of its own. Every
    /// preset from V1 to V10 keeps its behaviour, its content hash, and its
    /// frozen trajectory digest byte-identical.
    /// </para>
    /// <para>
    /// Unlike V6 through V10, this preset is the shipped client default rather
    /// than an opt-in: it exists because the shipped build is where the defect
    /// was observed. See
    /// the last-stand engagement design remedy C and
    /// the last-stand engagement plan.
    /// </para>
    /// </remarks>
    LastStandEngagementV11 = 11,

    /// <summary>
    /// The contingent-shape preset. A verbatim restatement of
    /// <see cref="LastStandEngagementV11"/>'s registered field values under
    /// its own <c>id</c>, following the same convention
    /// <see cref="RangedStandoffV8"/>, <see cref="MonotoneAllyClearanceV9"/>,
    /// <see cref="BattlefieldRealismV10"/>, and <see cref="LastStandEngagementV11"/>
    /// already use: the behaviour is gated on preset identity at its own
    /// call site, never on a <see cref="MovementRuleset"/> field, so this
    /// value carries no new field of its own. It is reachable only through
    /// explicit selection — the shipped default stays
    /// <see cref="LastStandEngagementV11"/> — and is opt-in for the
    /// <see cref="Simulation.FormationPlanner"/> to shape a contingent's
    /// deployment footprint rather than its runtime cohesion behaviour.
    /// <see cref="Simulation.FormationPlanner.ResolveContingentSizes"/> now
    /// gates its authored-sizes path on this identity (commit 0766ee7), so a
    /// full battle run under this preset derives contingent count from
    /// fielded chiefs rather than the square-root or chief-count formulas
    /// V10 and V11 use; it is not guaranteed to be byte-identical to
    /// <see cref="LastStandEngagementV11"/> for that reason. The
    /// weapon-grouped cohort deployment gate at
    /// <see cref="Simulation.BattleSimulation"/> still checks preset
    /// identity with a closed <c>is BattlefieldRealismV10 or
    /// LastStandEngagementV11</c> pattern rather than a monotone "V10 or
    /// later" test, and this value falls outside that gate too. See
    /// the contingent shape task plan and
    /// the contingent shape design.
    /// </summary>
    ContingentShapeV12 = 12,

    /// <summary>
    /// The cohort lateral-spread preset. A verbatim restatement of
    /// <see cref="LastStandEngagementV11"/>'s registered field values under
    /// its own <c>id</c>, following the same convention
    /// <see cref="ContingentShapeV12"/> uses: the behaviour is gated on
    /// preset identity at its own call site, so this value carries no new
    /// field of its own. It is admitted to both the
    /// weapon-grouped-cohort-deployment gate and the last-stand-engagement
    /// gate at <see cref="Simulation.BattleSimulation"/>, so it inherits
    /// <see cref="LastStandEngagementV11"/>'s behaviour whole, plus one
    /// change: <see cref="CohortDeploymentAssignment.AssignForFaction"/>
    /// cuts its cohort-ordered warrior runs across contingent ids visited in
    /// lateral-riffle order — even ids ascending, then odd ids ascending —
    /// instead of size-descending, id-ascending order, so weapon cohorts are
    /// spread across an army's frontage rather than laid down sorted from
    /// one edge of the map to the other. It is not admitted to the
    /// <see cref="ContingentShapeV12"/> branch of
    /// <see cref="Simulation.FormationPlanner.ResolveContingentSizes"/>, so
    /// it takes the square-root sizing path V11 does. See
    /// the cohort lateral spread design and
    /// the cohort lateral spread plan.
    /// </summary>
    CohortLateralSpreadV13 = 13,

    /// <summary>
    /// The in-fight evasive footwork preset. A verbatim restatement of
    /// <see cref="CohortLateralSpreadV13"/>'s registered field values under its
    /// own <c>id</c>, following the convention
    /// <see cref="ContingentShapeV12"/> and <see cref="CohortLateralSpreadV13"/>
    /// both use: the behaviour is gated on preset identity at its own call
    /// site, so this value carries no new ruleset field of its own. In
    /// particular <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/>
    /// stays <see langword="false"/> — this preset does not revive the
    /// equipment-relative route pipeline, and no
    /// <see cref="FootworkPhase"/> or <see cref="TacticalPosture"/> value is
    /// resolved under it.
    /// <para>
    /// It is admitted to all three of the identity gates
    /// <see cref="CohortLateralSpreadV13"/> reaches — the battlefield-realism
    /// gate, the last-stand-engagement gate, and the lateral-riffle cohort
    /// traversal — so it inherits V13's behaviour whole. It is not admitted to
    /// the <see cref="ContingentShapeV12"/> branch of
    /// <see cref="Simulation.FormationPlanner.ResolveContingentSizes"/>, so it
    /// takes the square-root sizing path V11 and V13 take.
    /// </para>
    /// <para>
    /// What it adds is a post-pass at the tail of the movement-proposal stage
    /// that lets a warrior already engaged with a living enemy weave while
    /// closing, circle after a blow is turned aside, step off the line of an
    /// inbound missile, and yield a foot when the press pins it — without ever
    /// writing <see cref="Simulation.AgentIntent"/>, dropping its target, or
    /// leaving the fight. The resolved value is published on
    /// <c>AgentState.EvasiveAction</c> and folded into the state hash behind a
    /// gate of its own.
    /// </para>
    /// <para>
    /// One consequence is deliberate and is not a defect. A projectile's clash
    /// outcome is resolved from its launch tick, so a warrior that visibly
    /// leaps off the line of an arrow can still be recorded as hit. That is
    /// what the sixteenth-century account this rung is drawn from describes:
    /// the men at Mactan leaped about and were struck through shield and arm
    /// regardless. See the 2026-08-15 in-fight evasion design.
    /// </para>
    /// </summary>
    EvasiveFootworkV14 = 14,
}
