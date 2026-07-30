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
    int FootworkTicksRemaining = 0);
