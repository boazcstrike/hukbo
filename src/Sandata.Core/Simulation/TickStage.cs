namespace Sandata.Core.Simulation;

/// <summary>
/// The fourteen fixed tick stages <see cref="SandataSimulation.RunTick"/>
/// executes, in this exact numeric order, every tick, unconditionally.
/// Design section 5 of <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>
/// names this table; task 49 is the first caller to actually walk it end to
/// end. The order is part of the deterministic replay contract the same way
/// every enum in this codebase is: <b>append-only</b>, never renumbered,
/// never reordered — a change here is a new preset version with new golden
/// expectations, per <c>CLAUDE.md</c> section 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two binding rules this ordering exists to enforce, verbatim from
/// the task brief:</b> stages 5 through 9 read only the frozen tick-start
/// view captured by <see cref="ViewCapture"/> and write only into write-only
/// proposal buffers — no unit in that range may read another unit's write
/// from the same range, so permuting the operator processing order within
/// stages 5 through 9 must never change the proposal buffers those stages
/// produce. Nothing at or after <see cref="LocalAvoidanceAndCollision"/>
/// (stage 10) may consult the frozen view; <see cref="TickStartView.Release"/>
/// is called once, between stage 9 and stage 10, and every accessor on the
/// view throws <see cref="System.InvalidOperationException"/> afterward.
/// </para>
/// <para>
/// <b>What this task actually implements, honestly.</b> Every stage below
/// exists as a named value regardless of whether <see cref="SandataSimulation"/>
/// carries a complete implementation for it — the enum names the pipeline's
/// full shape so a future task extending an unimplemented stage has a
/// pinned slot to extend, not a slot to invent. See
/// <see cref="SandataSimulation"/>'s own remarks, and this task's final
/// report, for exactly which stages are complete, partial, or not yet
/// implemented.
/// </para>
/// </remarks>
public enum TickStage
{
    /// <summary>
    /// Applies every order whose <c>TargetTick</c> equals the current tick,
    /// read from <c>Orders.OrderQueue.InApplicationOrder()</c> — the
    /// already-validated queue this pipeline consumes, never
    /// <c>OrderQueue.SubmitValidated</c>. See <see cref="SandataSimulation"/>'s
    /// remarks for why <c>SubmitValidated</c> is not a call this stage makes.
    /// </summary>
    OrderApplication = 1,

    /// <summary>
    /// Spawns and despawns operators. No spawn or despawn trigger source
    /// exists anywhere in this worktree, so this stage is an honest
    /// pass-through: the state it receives is the state it returns,
    /// unchanged.
    /// </summary>
    SpawnAndDespawn = 2,

    /// <summary>
    /// Captures the frozen <see cref="TickStartView"/> that stages 5 through
    /// 9 read, and rebuilds this tick's collision broad-phase grid from the
    /// state <see cref="OrderApplication"/> and <see cref="SpawnAndDespawn"/>
    /// left behind.
    /// </summary>
    ViewCapture = 3,

    /// <summary>
    /// Mutates door open/closed state. No door-trigger source (a breach
    /// action, a switch) exists anywhere in this worktree, so this stage is
    /// an honest pass-through: the state it receives is the state it
    /// returns, unchanged.
    /// </summary>
    DoorMutation = 4,

    /// <summary>
    /// Updates every living operator's <c>ContactMemory</c> against the
    /// frozen view (vision cone plus line of sight), then folds each living
    /// operator's resulting contact tier into that operator's faction alert
    /// level via <c>AlertRules.EvaluateFaction</c>.
    /// </summary>
    AlertAndSensing = 5,

    /// <summary>
    /// Derives this tick's squad membership, leadership, and marching-order
    /// slot for every operator via <c>Squads.SquadGrouping.Compute</c>,
    /// gated by <c>SandataRuleset.GroupCohesionRadius</c>. Purely derived,
    /// per design section 8 — nothing here is written back into
    /// <c>MissionState</c>.
    /// </summary>
    SquadGrouping = 6,

    /// <summary>
    /// Advances <c>Navigation.PathService</c> by one tick, constructed once
    /// with <c>SandataRuleset.PathLatencyTicks</c>. No autonomous
    /// destination-request source exists anywhere in this worktree, so this
    /// stage's search-and-publish machinery runs every tick but, in this
    /// wave, never has an outstanding request to act on — see
    /// <see cref="SandataSimulation"/>'s remarks.
    /// </summary>
    PathService = 7,

    /// <summary>
    /// Selects each living operator's <c>OperatorIntent</c> via
    /// <c>Simulation.IntentSelection.Select</c>, assembling one
    /// <c>IntentSelectionInput</c> per operator from the frozen view and this
    /// tick's path and breach-point facts.
    /// </summary>
    IntentSelection = 8,

    /// <summary>
    /// Selects each operator's <c>Orders.MovementSource</c> (an authored
    /// order or the autonomous slot), excludes assigned entities from the
    /// autonomous roster via <c>MovementSource.SlotTargetingRoster</c>, and
    /// produces one write-only <c>Movement.MovementProposal</c> per operator.
    /// </summary>
    MovementProposal = 9,

    /// <summary>
    /// Resolves every proposal from <see cref="MovementProposal"/> through
    /// <c>Movement.LocalAvoidance</c> (one sidestep, then a tick's wait) and
    /// commits the result. This is the first stage that may not read
    /// <see cref="TickStartView"/> — it has already been released.
    /// </summary>
    LocalAvoidanceAndCollision = 10,

    /// <summary>
    /// Evaluates <c>Combat.WeaponLoweredRules.IsForcedLowered</c> against the
    /// position <see cref="LocalAvoidanceAndCollision"/> just committed, then
    /// advances each operator's <c>Weapons.WeaponChain</c> by one tick.
    /// </summary>
    WeaponChain = 11,

    /// <summary>
    /// Proposes shots. <b>Not implemented by this task</b> — no call-site
    /// obligation for this stage was named in the task brief, and it depends
    /// on a target-selection concept (a per-operator best target and bearing)
    /// that does not exist in this data model. See
    /// <see cref="SandataSimulation"/>'s remarks.
    /// </summary>
    FireProposal = 12,

    /// <summary>
    /// Resolves damage and outcome (win condition, casualties). <b>Not
    /// implemented by this task</b> — no call-site obligation for this stage
    /// was named in the task brief, and it depends on <see cref="FireProposal"/>,
    /// which this task also does not implement.
    /// </summary>
    DamageResolution = 13,

    /// <summary>
    /// Computes the state hash via <c>Determinism.SandataStateHasher.Compute</c>
    /// at the mission's recorded cadence (<c>MissionTickPolicy.StateHashCadenceTicks</c>).
    /// </summary>
    StateHash = 14,
}
