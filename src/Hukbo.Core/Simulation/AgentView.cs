using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

/// <param name="MovementResolution">
/// Why the agent finished the tick where it did. This is the spectator's
/// bounded explanation of collision, carried per agent instead of as per-contact
/// events, which a packed front would emit by the thousand. Defaulted so that
/// presentation tests can build a view without naming it.
/// </param>
/// <param name="Level">
/// This warrior's level, set once at spawn from
/// <see cref="Scenario.PlaceholderFighterLevel"/> and never mutated
/// afterward. Defaulted, matching <see cref="MovementResolution"/> above, so
/// presentation tests written before levels existed can build a view without
/// naming it; the default matches
/// <see cref="Scenario.PlaceholderFighterLevel"/>'s own default.
/// </param>
/// <param name="ContingentId">
/// The contingent this warrior was dealt into at spawn. Defaulted, matching
/// <see cref="MovementResolution"/> and <see cref="Level"/> above, so
/// presentation tests written before contingents existed can build a view
/// without naming it.
/// </param>
/// <param name="ContingentState">
/// This warrior's contingent's behavioural mode, as of the tick this view was
/// captured. Defaulted to <see cref="ContingentState.None"/> for the same
/// reason <see cref="ContingentId"/> above is defaulted.
/// </param>
/// <param name="Rank">
/// This warrior's social and legal standing, resolved once at spawn from its
/// roster entry's <see cref="Combat.CombatLoadout.Rank"/>. Defaulted, matching
/// <see cref="MovementResolution"/>, <see cref="Level"/>, and
/// <see cref="ContingentId"/> above, so presentation tests written before
/// rank existed can build a view without naming it; the default matches
/// <see cref="Combat.CombatLoadout.Rank"/>'s own default.
/// </param>
/// <param name="IsLeader">
/// Whether this warrior is its contingent's current leader, as of the tick
/// this view was captured — a derived, per-tick fact, not stored state,
/// recomputed from scratch every tick by
/// <see cref="Movement.MovementRules.ScanContingentLeadersAndLivingCounts"/>,
/// exactly as <see cref="ContingentState"/> above is. Defaulted to
/// <see langword="false"/> for the same reason <see cref="ContingentId"/>
/// above is defaulted, so presentation tests written before leadership
/// existed can build a view without naming it.
/// </param>
/// <param name="Facing">
/// The warrior's 16-sector facing under a movement preset whose
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
/// <see langword="true"/>; <see cref="Facing16.None"/> forever under every
/// other preset. A corpse retains its final facing, so a dead V6 warrior's
/// view still carries it. Defaulted, like every member from
/// <see cref="MovementResolution"/> down, so presentation tests written
/// before the field existed still compile (weapon-relative movement design,
/// section 15.1).
/// </param>
/// <param name="MovementPaceRaw">
/// The retained scalar pace of design section 6.5, in raw distance per
/// tick. <c>0</c> forever under every preset that does not use
/// equipment-relative footwork, and cleared to <c>0</c> by death cleanup.
/// Defaulted for the same reason <see cref="Facing"/> is.
/// </param>
/// <param name="TacticalPosture">
/// The contingent-level stance written on every living member each tick by
/// the posture stage of design section 8.
/// <see cref="Movement.TacticalPosture.None"/> forever under every other
/// preset, and cleared by death cleanup. Defaulted for the same reason
/// <see cref="Facing"/> is.
/// </param>
/// <param name="FootworkPhase">
/// The footwork lifecycle phase of design section 9.
/// <see cref="Movement.FootworkPhase.None"/> forever under every other
/// preset, and cleared by death cleanup. Defaulted for the same reason
/// <see cref="Facing"/> is.
/// </param>
/// <param name="FootworkTicksRemaining">
/// The <see cref="Movement.FootworkPhase.Commit"/> /
/// <see cref="Movement.FootworkPhase.Recover"/> timer of design section
/// 9.5. <c>0</c> outside those phases and forever under every other
/// preset. Defaulted for the same reason <see cref="Facing"/> is.
/// </param>
/// <param name="BrokeOffUnderPressure">
/// Whether the pressure interrupt broke this warrior off a committed blow on
/// the tick just resolved — channel 1 of the spectator explanation in
/// pressure-interrupt design section 3, question 8. It is not a single-tick
/// pulse: it stays set for as long as the warrior remains in the
/// <see cref="Movement.FootworkPhase.Disengage"/> the interrupt produced, which
/// is what makes the mark it drives readable at 1x speed.
/// <see langword="false"/> forever under every preset whose
/// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
/// <see langword="false"/>, and <see langword="false"/> for a corpse. Defaulted
/// for the same reason <see cref="Facing"/> is.
/// </param>
/// <param name="PressureBasisPoints">
/// This warrior's weighted pressure as of the tick this view was captured, in
/// the same basis-point unit as <see cref="PressureThresholdBasisPoints"/>
/// below, so the two read against each other with no further arithmetic —
/// channel 3 of pressure-interrupt design section 3, question 8. It is carried
/// on every tick for every living warrior, not only on a tick an interrupt
/// fires, because a running value is what lets a spectator predict a break-off
/// rather than only witness one. <c>0</c> forever under every preset whose
/// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
/// <see langword="false"/>, and <c>0</c> for a corpse. Defaulted for the same
/// reason <see cref="Facing"/> is.
/// </param>
/// <param name="PressureThresholdBasisPoints">
/// The value <see cref="PressureBasisPoints"/> must reach for this warrior to
/// abandon a committed blow, read from its resolved
/// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>.
/// It is a per-loadout constant rather than a per-tick quantity, and pairing it
/// with the running value above is what explains why one warrior broke off and
/// the neighbour beside it did not. <c>0</c> forever under every preset whose
/// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
/// <see langword="false"/>, and <c>0</c> for a corpse. Defaulted for the same
/// reason <see cref="Facing"/> is.
/// </param>
/// <param name="RangedPhase">
/// This warrior's readable draw-and-loose cycle, a **derived projection and
/// not stored state** — <see cref="RangedPhaseProjection.Derive"/> computes
/// it every tick from the attack cooldown the tick has already produced, per
/// ranged-units design section 8.1. <see cref="Simulation.RangedPhase.None"/>
/// forever for a melee weapon, at every cooldown value. Defaulted for the
/// same reason <see cref="Facing"/> is.
/// </param>
/// <param name="RangedPhaseTicksRemaining">
/// How many ticks remain in <see cref="RangedPhase"/>, derived alongside it
/// by <see cref="RangedPhaseProjection.Derive"/>. Strictly decreasing while a
/// ranged warrior stays in one phase; <c>0</c> whenever
/// <see cref="RangedPhase"/> is <see cref="Simulation.RangedPhase.None"/> or
/// <see cref="Simulation.RangedPhase.Ready"/>. Defaulted for the same reason
/// <see cref="Facing"/> is.
/// </param>
/// <param name="ShieldBlockRecoveryTicksRemaining">
/// Ticks remaining in this warrior's shield block-recovery window,
/// shield-projectile-block design section 6.2 — authoritative agent state,
/// carried straight through from <see cref="AgentState.ShieldBlockRecoveryTicksRemaining"/>
/// rather than derived here. Strictly positive only while the warrior's pace
/// cap is clamped following a shield block; <c>0</c> forever under every
/// preset whose <see cref="MovementRuleset.AppliesShieldBlockRecovery"/> is
/// <see langword="false"/>, and cleared by death cleanup. Defaulted for the
/// same reason <see cref="Facing"/> is.
/// </param>
public readonly record struct AgentView(
    ulong EntityId,
    int FactionId,
    int XRaw,
    int YRaw,
    int HitPoints,
    int MaximumHitPoints,
    ulong? TargetEntityId,
    AgentIntent Intent,
    bool IsAlive,
    CombatLoadout Loadout,
    MovementResolution MovementResolution = MovementResolution.None,
    int Level = 1,
    int ContingentId = 0,
    ContingentState ContingentState = ContingentState.None,
    RankId Rank = RankId.Timawa,
    bool IsLeader = false,
    Facing16 Facing = Facing16.None,
    int MovementPaceRaw = 0,
    TacticalPosture TacticalPosture = TacticalPosture.None,
    FootworkPhase FootworkPhase = FootworkPhase.None,
    int FootworkTicksRemaining = 0,
    bool BrokeOffUnderPressure = false,
    int PressureBasisPoints = 0,
    int PressureThresholdBasisPoints = 0,
    RangedPhase RangedPhase = RangedPhase.None,
    int RangedPhaseTicksRemaining = 0,
    int ShieldBlockRecoveryTicksRemaining = 0);
