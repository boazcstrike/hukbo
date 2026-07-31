using Hukbo.Core.Simulation;

namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, testable resolution rules for the equipment-relative footwork
/// preset: the nine-branch tactical-posture table of the weapon-relative
/// movement design, section 8, the ten-step provisional footwork lifecycle
/// of section 9.1, and the lane-clearance finalisation of section 9.4.
/// Every method reads only its own scalar arguments — no agent array, no
/// simulation, no tick pipeline — so <c>Hukbo.Core.Tests</c> calls each one
/// directly with hand-built inputs instead of observing through a whole
/// battle, the same testability shape <see cref="MovementRules"/> and
/// <see cref="FacingRules"/> already use. Every ratio comparison is a
/// widened <see langword="checked"/> integer cross-product; nothing
/// divides, and nothing here touches floating point.
/// </summary>
internal static class WeaponMovementRules
{
    /// <summary>
    /// The scale of one whole in the basis-point ratio model: the
    /// enemy-to-ally support ratio thresholds on
    /// <see cref="LoadoutMovementProfile"/> are expressed in
    /// ten-thousandths.
    /// </summary>
    private const long RatioBasisPointScale = 10_000;

    /// <summary>
    /// The inclusive ceiling, in basis points, that each pressure-interrupt
    /// signal saturates at before it is weighted — three whole units. Its
    /// purpose is to stop one saturated signal from carrying the weighted sum
    /// on its own: without it, a warrior facing forty enemies alone
    /// contributes a support-pressure signal of 400,000 basis points and the
    /// other two weights become decorative. It is also the inclusive upper
    /// bound of every registered
    /// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>
    /// under a preset that applies the interrupt, which
    /// <see cref="MovementRuleset"/> enforces at construction. A provisional
    /// reconstruction of gameplay tuning under CLAUDE.md section 7, not a
    /// historical measurement: no source describes how a warrior in the
    /// pre-colonial Philippines decided to break off a committed blow, and
    /// this value claims nothing about one. See
    /// docs/plans/2026-07-31-movement-v7-pressure-interrupt-design.md
    /// section 5.1.
    /// </summary>
    internal const long SignalCeilingBasisPoints = 30_000;

    /// <summary>
    /// Resolves one contingent's <see cref="TacticalPosture"/> for this tick
    /// from the global living faction totals, the contingent's own
    /// tick-start <see cref="ContingentState"/>, and the two factions'
    /// role-coverage tallies (design section 8.1, first match wins). Every
    /// boundary operator is exact: a ratio landing exactly on a boundary
    /// takes the earliest branch that admits it, always the more
    /// conservative reading.
    /// </summary>
    /// <param name="globalAllies">
    /// The acting contingent's faction-wide living total.
    /// </param>
    /// <param name="globalEnemies">
    /// The opposing faction's living total.
    /// </param>
    /// <param name="contingentState">
    /// The contingent's own tick-start state, already resolved by the
    /// existing contingent-state stage. Under a preset that resolves
    /// postures, <see cref="ContingentState.None"/> means the contingent has
    /// no living member (design section 8.1, branch 1).
    /// </param>
    /// <param name="alliedRoleCoverage">
    /// The acting faction's
    /// <see cref="LoadoutCompositionCounts.RoleCoverage"/>, in <c>[0, 3]</c>.
    /// </param>
    /// <param name="enemyRoleCoverage">
    /// The opposing faction's role coverage, in <c>[0, 3]</c>.
    /// </param>
    internal static TacticalPosture ResolveTacticalPosture(
        int globalAllies,
        int globalEnemies,
        ContingentState contingentState,
        int alliedRoleCoverage,
        int enemyRoleCoverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(globalAllies);
        ArgumentOutOfRangeException.ThrowIfNegative(globalEnemies);

        long allies = globalAllies;
        long enemies = globalEnemies;

        // Branch 1: the contingent has no living member.
        if (contingentState == ContingentState.None)
        {
            return TacticalPosture.None;
        }

        // Branch 2: no living enemy exists.
        if (enemies == 0)
        {
            return TacticalPosture.Pursue;
        }

        // Branch 3: exact double outnumbering is already Withdraw.
        if (checked(allies * 2) <= enemies)
        {
            return TacticalPosture.Withdraw;
        }

        // Branch 4: exact four-to-three pressure is already Yield.
        if (checked(allies * 4) <= checked(enemies * 3))
        {
            return TacticalPosture.Yield;
        }

        // Branch 5: a holding contingent gathers before anything else.
        if (contingentState == ContingentState.Hold)
        {
            return TacticalPosture.Regroup;
        }

        // Branch 6: exact five-to-four advantage is already Advance.
        if (checked(allies * 4) >= checked(enemies * 5))
        {
            return TacticalPosture.Advance;
        }

        // Branches 7 and 8: role coverage breaks a contested world, and
        // only strictly — equal headcounts with equal coverage fall through
        // both to Hold.
        if (allies >= enemies && alliedRoleCoverage > enemyRoleCoverage)
        {
            return TacticalPosture.Advance;
        }

        if (allies <= enemies && alliedRoleCoverage < enemyRoleCoverage)
        {
            return TacticalPosture.Yield;
        }

        // Branch 9.
        return TacticalPosture.Hold;
    }

    /// <summary>
    /// Resolves one agent's provisional <see cref="FootworkPhase"/> and
    /// timer for this tick through the ten first-match transition steps of
    /// design section 9.1. The result is provisional scratch: section 9.4's
    /// route generation and lane clearance decide what is committed, through
    /// <see cref="FinalizeFootwork"/>, and authoritative state is written
    /// exactly once, after that finalisation.
    /// </summary>
    /// <remarks>
    /// The ratio steps are 4, the release, and 5, the entry — checked in
    /// that order, so an already-disengaging agent consults only the release
    /// threshold. Entry equality enters, release equality leaves, and a
    /// ratio strictly between the two thresholds preserves the previous
    /// state; construction validation on
    /// <see cref="LoadoutMovementProfile"/> keeps the release strictly below
    /// the entry, so no count can enter and leave disengagement on the same
    /// tick. Zero living enemies never enters and never remains in
    /// disengagement on the ratio arithmetic alone, because
    /// <paramref name="supportAllies"/> counts the actor itself and is
    /// therefore at least one for a living agent — the precondition this
    /// method enforces. Step 6 is unconditional: every member of a
    /// <see cref="TacticalPosture.Withdraw"/> or
    /// <see cref="TacticalPosture.Yield"/> contingent takes
    /// <see cref="FootworkPhase.Disengage"/> regardless of its own local
    /// advantage; only routes differ per agent (design section 9.3).
    /// </remarks>
    /// <param name="isAlive">Whether the agent is alive this tick.</param>
    /// <param name="priorPhase">
    /// The agent's authoritative phase from the previous tick.
    /// </param>
    /// <param name="priorTicksRemaining">
    /// The agent's authoritative <c>Commit</c>/<c>Recover</c> timer from the
    /// previous tick. An entry timer counts its entry tick (design section
    /// 9.5), and a continuing phase whose prior timer is <c>1</c> expires
    /// this tick.
    /// </param>
    /// <param name="posture">
    /// The contingent's posture, already resolved for this tick.
    /// </param>
    /// <param name="supportAllies">
    /// Living allies within the support radius, including the actor itself —
    /// <see cref="LocalMovementContext.SupportAllies"/>, never the immediate
    /// count.
    /// </param>
    /// <param name="supportEnemies">
    /// Living perceived enemies within the support radius —
    /// <see cref="LocalMovementContext.SupportEnemies"/>, never the
    /// immediate count.
    /// </param>
    /// <param name="disengageEnemyToAllyBasisPoints">
    /// The profile's disengagement entry threshold, in basis points of the
    /// enemy-to-ally support ratio.
    /// </param>
    /// <param name="reengageEnemyToAllyBasisPoints">
    /// The profile's disengagement release threshold, strictly below the
    /// entry threshold.
    /// </param>
    /// <param name="recoveryTicks">
    /// The profile's <see cref="LoadoutMovementProfile.RecoveryTicks"/>,
    /// loaded into the timer when a continuing <c>Commit</c> expires.
    /// </param>
    /// <param name="hasTarget">
    /// Whether this tick's target selection produced a target.
    /// </param>
    /// <param name="targetAtOrInsidePreferredDistance">
    /// Whether the selected target sits at or inside the offset-adjusted
    /// preferred distance, compared inclusively on squared values by the
    /// caller. Meaningless when <paramref name="hasTarget"/> is
    /// <see langword="false"/>.
    /// </param>
    internal static (FootworkPhase Phase, int TicksRemaining)
        ResolveProvisionalFootwork(
            bool isAlive,
            FootworkPhase priorPhase,
            int priorTicksRemaining,
            TacticalPosture posture,
            int supportAllies,
            int supportEnemies,
            int disengageEnemyToAllyBasisPoints,
            int reengageEnemyToAllyBasisPoints,
            int recoveryTicks,
            bool hasTarget,
            bool targetAtOrInsidePreferredDistance)
    {
        // Step 1: dead.
        if (!isAlive)
        {
            return (FootworkPhase.None, 0);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(supportAllies, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(supportEnemies);

        // Step 2: a continuing Commit decrements; an expiring Commit enters
        // Recover with the profile recovery duration.
        if (priorPhase == FootworkPhase.Commit)
        {
            return priorTicksRemaining > 1
                ? (FootworkPhase.Commit, priorTicksRemaining - 1)
                : (FootworkPhase.Recover, recoveryTicks);
        }

        // Step 3: a continuing Recover decrements; an expiring Recover
        // falls through to the rules below.
        if (priorPhase == FootworkPhase.Recover && priorTicksRemaining > 1)
        {
            return (FootworkPhase.Recover, priorTicksRemaining - 1);
        }

        long enemiesScaled =
            checked(supportEnemies * RatioBasisPointScale);

        // Step 4, the release: an agent already disengaging remains until
        // the ratio falls to the release threshold — release equality
        // leaves.
        if (priorPhase == FootworkPhase.Disengage &&
            enemiesScaled >
                checked((long)supportAllies * reengageEnemyToAllyBasisPoints))
        {
            return (FootworkPhase.Disengage, 0);
        }

        // Step 5, the entry: entry equality enters.
        if (enemiesScaled >=
            checked((long)supportAllies * disengageEnemyToAllyBasisPoints))
        {
            return (FootworkPhase.Disengage, 0);
        }

        // Step 6, unconditional on posture: no ratio here.
        if (posture is TacticalPosture.Withdraw or TacticalPosture.Yield)
        {
            return (FootworkPhase.Disengage, 0);
        }

        // Step 7.
        if (posture == TacticalPosture.Regroup)
        {
            return (FootworkPhase.Regroup, 0);
        }

        // Step 8: at or inside the offset-adjusted preferred distance,
        // inclusive.
        if (hasTarget && targetAtOrInsidePreferredDistance)
        {
            return (FootworkPhase.Engage, 0);
        }

        // Step 9.
        if (hasTarget)
        {
            return (FootworkPhase.Approach, 0);
        }

        // Step 10.
        return posture == TacticalPosture.Pursue
            ? (FootworkPhase.Pursue, 0)
            : (FootworkPhase.None, 0);
    }

    /// <summary>
    /// Commits a provisional phase against the lane-clearance outcome
    /// (design section 9.4). With a surviving candidate the provisional
    /// phase stands. With none, a provisional
    /// <see cref="FootworkPhase.Approach"/>,
    /// <see cref="FootworkPhase.Engage"/>, or
    /// <see cref="FootworkPhase.Pursue"/> finalises
    /// <see cref="FootworkPhase.Refuse"/> with a zero timer, while every
    /// other phase — the <c>Commit</c>/<c>Recover</c> attack lifecycle and
    /// the <c>Disengage</c>/<c>Regroup</c> safety phases — retains its phase
    /// and timer and simply emits no movement, because a blocked lane must
    /// not erase a safety or attack lifecycle.
    /// <see cref="FootworkPhase.None"/> stays
    /// <see cref="FootworkPhase.None"/>.
    /// </summary>
    /// <param name="provisionalPhase">
    /// The provisional phase from <see cref="ResolveProvisionalFootwork"/>.
    /// </param>
    /// <param name="provisionalTicksRemaining">
    /// The provisional timer kept in scratch alongside it.
    /// </param>
    /// <param name="hasSurvivingCandidate">
    /// Whether at least one route candidate survived lane clearance.
    /// </param>
    internal static (FootworkPhase Phase, int TicksRemaining)
        FinalizeFootwork(
            FootworkPhase provisionalPhase,
            int provisionalTicksRemaining,
            bool hasSurvivingCandidate)
    {
        if (!hasSurvivingCandidate &&
            provisionalPhase is FootworkPhase.Approach
                or FootworkPhase.Engage
                or FootworkPhase.Pursue)
        {
            return (FootworkPhase.Refuse, 0);
        }

        return (provisionalPhase, provisionalTicksRemaining);
    }
}
