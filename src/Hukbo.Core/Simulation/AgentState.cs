using Hukbo.Core.Combat;

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

    internal bool IsAlive => HitPoints > 0;

    internal AgentView ToView() =>
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
            Rank);
}
