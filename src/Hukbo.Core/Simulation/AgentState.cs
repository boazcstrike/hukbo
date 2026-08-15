using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

internal sealed class AgentState
{
    internal AgentState(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        int maximumHitPoints,
        int movementSpeedRaw,
        int perceptionRangeRaw,
        int attackRangeRaw,
        int damagePerAttack,
        int attackCooldownTicks,
        CombatLoadout loadout,
        // Defaulted, not because it is optional in the sense the other
        // parameters are, but because AgentState has one call site in
        // production (BattleSimulation.CreateAgent, which always passes
        // Scenario.PlaceholderFighterLevel explicitly) and several
        // named-argument call sites in tests that predate levels entirely.
        // A required parameter would force every one of those unrelated call
        // sites to be edited just to keep compiling. 1 matches
        // Scenario.PlaceholderFighterLevel's own default.
        int level = 1,
        // Defaulted for the same reason level is above: BattleSimulation.Create
        // is the only production call site and always passes this explicitly,
        // from FormationPlanner's returned membership, while several
        // named-argument test call sites predate contingent membership
        // entirely. 0 is a valid contingent index, not a sentinel, so those
        // tests simply never observe a ContingentId beyond it.
        int contingentId = 0)
    {
        if (entityId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        if (factionId is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(factionId));
        }

        EntityId = entityId;
        FactionId = factionId;
        XRaw = xRaw;
        YRaw = yRaw;
        HitPoints = maximumHitPoints;
        MaximumHitPoints = maximumHitPoints;
        MovementSpeedRaw = movementSpeedRaw;
        PerceptionRangeRaw = perceptionRangeRaw;
        AttackRangeRaw = attackRangeRaw;
        DamagePerAttack = damagePerAttack;
        AttackCooldownTicks = attackCooldownTicks;
        Loadout = loadout;
        Intent = AgentIntent.Idle;
        Level = level;
        ContingentId = contingentId;
        ContingentState = ContingentState.None;
        Rank = loadout.Rank;
    }

    internal ulong EntityId { get; }

    internal int FactionId { get; }

    internal int XRaw { get; set; }

    internal int YRaw { get; set; }

    internal int HitPoints { get; set; }

    internal int MaximumHitPoints { get; }

    internal int MovementSpeedRaw { get; }

    internal int PerceptionRangeRaw { get; }

    internal int AttackRangeRaw { get; }

    internal int DamagePerAttack { get; }

    internal int AttackCooldownTicks { get; }

    internal CombatLoadout Loadout { get; }

    internal int AttackCooldownRemaining { get; set; }

    internal ulong? TargetEntityId { get; set; }

    internal AgentIntent Intent { get; set; }

    /// <summary>
    /// This warrior's level, set once at spawn from
    /// <see cref="Scenario.PlaceholderFighterLevel"/> and never mutated
    /// afterward — there is no leveling system yet. Bounds an active attack
    /// combination's maximum length alongside
    /// <see cref="Combat.WeaponProfile.ComboMaxSteps"/>.
    /// </summary>
    internal int Level { get; }

    /// <summary>
    /// The contingent this warrior was dealt into by
    /// <see cref="FormationPlanner.PlanFactionDeployment"/>, in
    /// <c>[0, FormationPlanner.MaximumContingents)</c>. Written once, at
    /// spawn, from <see cref="BattleSimulation.Create"/> and never mutated
    /// afterward — a dead agent keeps its <see cref="ContingentId"/>, which
    /// is why the leader scan a movement preset performs must skip agents
    /// that are not alive rather than relying on membership to change.
    /// </summary>
    internal int ContingentId { get; }

    /// <summary>
    /// This contingent's behavioural mode, written on every living member by
    /// the tick stage that resolves it. <see cref="ContingentState.None"/>
    /// under a preset that does not assign contingent states, and for any
    /// agent this task's preset never touches.
    /// </summary>
    internal ContingentState ContingentState { get; set; }

    /// <summary>
    /// This warrior's social and legal standing, resolved once at spawn from
    /// its roster entry's <see cref="Combat.CombatLoadout.Rank"/> and never
    /// mutated afterward. It is not a separate constructor parameter — the
    /// loadout already carries it.
    /// </summary>
    internal RankId Rank { get; }

    /// <summary>
    /// The number of <em>additional</em> blows a currently-active attack
    /// combination may still land after the blow that most recently set it.
    /// <c>0</c> whenever no chain is active. Mutated only inside
    /// <see cref="BattleSimulation.GatherAndCommitAttacks"/>.
    /// </summary>
    internal int ComboStepsRemaining { get; set; }

    /// <summary>
    /// The entity the active chain is bound to. <c>null</c> exactly when
    /// <see cref="ComboStepsRemaining"/> is <c>0</c>.
    /// </summary>
    internal ulong? ComboTargetEntityId { get; set; }

    /// <summary>
    /// Why this agent finished the tick where it did. Written by the collision
    /// stage, authoritative, and included in the state hash.
    /// </summary>
    internal MovementResolution MovementResolution { get; set; }

    /// <summary>
    /// This warrior's 16-sector facing under a movement preset whose
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
    /// <see langword="true"/>: initialised at spawn — <see cref="Facing16.East"/>
    /// for faction 0, <see cref="Facing16.West"/> for faction 1 — then
    /// turned per the weapon-relative movement design, section 6. Every
    /// other preset leaves it at <see cref="Facing16.None"/> forever, and
    /// the explicit initializer is load-bearing because
    /// <see cref="Facing16"/>'s default numeric value is
    /// <see cref="Facing16.East"/>, not <see cref="Facing16.None"/>. A
    /// corpse retains its final facing as readable spectator information.
    /// The five properties from here to
    /// <see cref="FootworkTicksRemaining"/> are declared in the V6
    /// state-hash fold order (design section 14.1), frozen once the V6
    /// digest ships.
    /// </summary>
    internal Facing16 Facing { get; set; } = Facing16.None;

    /// <summary>
    /// The retained scalar pace of the weapon-relative movement design,
    /// section 6.5, in raw distance per tick. <c>0</c> forever under every
    /// preset that does not use equipment-relative footwork, and cleared to
    /// <c>0</c> by death cleanup.
    /// </summary>
    internal int MovementPaceRaw { get; set; }

    /// <summary>
    /// The contingent-level stance written on every living member each tick
    /// by the posture stage of the weapon-relative movement design, section
    /// 8. <see cref="Movement.TacticalPosture.None"/> forever under every
    /// preset that does not use equipment-relative footwork, and cleared by
    /// death cleanup.
    /// </summary>
    internal TacticalPosture TacticalPosture { get; set; }

    /// <summary>
    /// The footwork lifecycle phase written exactly once per tick, after
    /// the two-step finalisation of the weapon-relative movement design,
    /// section 9.4. <see cref="Movement.FootworkPhase.None"/> forever under
    /// every preset that does not use equipment-relative footwork, and
    /// cleared by death cleanup.
    /// </summary>
    internal FootworkPhase FootworkPhase { get; set; }

    /// <summary>
    /// The <see cref="Movement.FootworkPhase.Commit"/> /
    /// <see cref="Movement.FootworkPhase.Recover"/> timer, counting the
    /// current tick per design section 9.5. <c>0</c> outside those phases,
    /// <c>0</c> forever under every preset that does not use
    /// equipment-relative footwork, and cleared by death cleanup.
    /// </summary>
    internal int FootworkTicksRemaining { get; set; }

    /// <summary>
    /// The damage this warrior absorbed on the previous tick, stamped once per
    /// tick from the per-agent damage accumulator the attack stage already
    /// maintains, and read on the following tick as the incoming-damage signal
    /// of the pressure interrupt (V7 design section 4.5, signal B). <c>0</c>
    /// forever under every preset whose
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="false"/>, and cleared by death cleanup. This property and
    /// the two below it are declared after
    /// <see cref="FootworkTicksRemaining"/> so that the five properties above
    /// keep the V6 fold order the shipped V6 digest froze; they fold only
    /// under the V7 gate, in this declaration order.
    /// </summary>
    internal int DamageTakenLastTick { get; set; }

    /// <summary>
    /// The number of allies this warrior's support ring held on the previous
    /// tick, including itself, stamped after the footwork stage has already
    /// read it so a single integer suffices. Read on the following tick as the
    /// ally-collapse signal of the pressure interrupt (V7 design section 4.5,
    /// signal C). <c>0</c> at spawn, which is what keeps the signal silent on
    /// the first tick; <c>0</c> forever under every preset whose
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="false"/>, and cleared by death cleanup.
    /// </summary>
    internal int PriorSupportAllies { get; set; }

    /// <summary>
    /// Whether the pressure interrupt broke this warrior off a committed blow
    /// on the tick just resolved — the spectator channel of V7 design section
    /// 8, and the reason an interrupted warrior's cooldown and chain state
    /// read the way they do. <see langword="false"/> forever under every preset
    /// whose <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="false"/>, and cleared by death cleanup.
    /// </summary>
    internal bool BrokeOffUnderPressure { get; set; }

    /// <summary>
    /// This warrior's resolved in-fight evasive movement for the tick just
    /// gathered, under <see cref="MovementPresetId.EvasiveFootworkV14"/>.
    /// <see cref="EvasiveAction.None"/> forever under every other preset, and
    /// cleared by death cleanup so a corpse cannot carry a stale action into
    /// the state hash.
    /// </summary>
    /// <remarks>
    /// Declared last, after <see cref="BrokeOffUnderPressure"/>, for the same
    /// reason that property was declared after
    /// <see cref="FootworkTicksRemaining"/>: the five footwork properties fold
    /// in declaration order under the V6 gate and the three pressure
    /// properties fold in declaration order under the V7 gate, and both orders
    /// are frozen by shipped digests. This property folds after both, under a
    /// third gate of its own, so it cannot disturb either layout.
    /// </remarks>
    internal EvasiveAction EvasiveAction { get; set; }

    internal bool IsAlive => HitPoints > 0;

    internal AgentView ToView(bool isLeader) =>
        new(
            EntityId,
            FactionId,
            XRaw,
            YRaw,
            HitPoints,
            MaximumHitPoints,
            TargetEntityId,
            Intent,
            IsAlive,
            Loadout,
            MovementResolution,
            Level,
            ContingentId,
            ContingentState,
            Rank,
            isLeader,
            Facing,
            MovementPaceRaw,
            TacticalPosture,
            FootworkPhase,
            FootworkTicksRemaining)
        {
            // Set by name rather than by position: the five record parameters
            // between FootworkTicksRemaining and this one are defaulted and are
            // filled in by UpdateViews, so passing this positionally here would
            // write it into BrokeOffUnderPressure instead.
            EvasiveAction = EvasiveAction,
        };
}
