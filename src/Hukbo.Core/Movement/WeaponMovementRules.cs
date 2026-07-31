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
/// <see cref="FacingRules"/> already use. Every ratio comparison in the
/// posture table and in the footwork ladder is a widened
/// <see langword="checked"/> integer cross-product, so no comparison on
/// those paths divides at all. <see cref="ShouldPressureInterrupt"/> is the
/// single exception, and it is a deliberate one: a weighted sum of three
/// ratios over three different denominators has no cross-multiplied form,
/// because putting it over a common denominator produces a five-factor
/// product of roughly 1e25 that overflows <see langword="long"/> and would
/// force <c>Int128</c> arithmetic per agent per tick, in a stage already
/// under performance scrutiny. That predicate therefore performs three
/// <see langword="long"/> divisions. They truncate toward zero, which is
/// exact and deterministic on every platform, and it is the same behaviour
/// <c>FixedPoint.MultiplyRatio</c> and <c>MovementRules.CeilDiv</c> already
/// rely on inside hashed code paths. Nothing here touches floating point.
/// </summary>
internal static class WeaponMovementRules
{
    /// <summary>
    /// The scale of one whole in the basis-point ratio model: the
    /// enemy-to-ally support ratio thresholds on
    /// <see cref="LoadoutMovementProfile"/> are expressed in
    /// ten-thousandths. It is <see langword="internal"/> rather than private,
    /// as <see cref="SignalCeilingBasisPoints"/> already is, because the
    /// simulation divides <see cref="ComputeWeightedPressure"/>'s scaled sum
    /// back down by it to obtain the basis-point value the agent inspector
    /// shows against a row's own threshold.
    /// </summary>
    internal const long RatioBasisPointScale = 10_000;

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
    /// Decides whether the pressure interrupt fires for one agent this tick:
    /// the weighted sum of three saturating basis-point signals — support
    /// pressure, incoming damage, and ally collapse — measured against this
    /// row's own registered threshold (design section 5.1). The answer is what
    /// step 1a of <see cref="ResolveProvisionalFootwork"/> consumes, and the
    /// caller charges the cost of a firing interrupt — a full attack cooldown
    /// and a cleared combo chain — from that same single answer, so the
    /// predicate is evaluated exactly once per agent per tick. The weighted
    /// sum itself lives in <see cref="ComputeWeightedPressure"/>, the single
    /// authority for the formula, which the simulation also calls directly to
    /// fill the agent inspector's pressure row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The predicate returns <see langword="false"/> unless the prior phase is
    /// <see cref="FootworkPhase.Commit"/> or
    /// <see cref="FootworkPhase.Recover"/>. That transition-only clause is
    /// load-bearing rather than tidy. Without it the interrupt fires on every
    /// tick the pressure holds, including every tick the warrior is already
    /// disengaging, re-charging the cooldown each time, so a warrior under
    /// sustained pressure would never attack again — a worse standoff than the
    /// one this preset exists to end. With it, the cost is charged once per
    /// break-off and the subsequent stay in
    /// <see cref="FootworkPhase.Disengage"/> is governed by the existing
    /// hysteresis at steps 4 and 5, whose release threshold is validated
    /// strictly below its entry threshold. Outside the attack lifecycle there
    /// is in any case nothing to preempt.
    /// </para>
    /// <para>
    /// Every operation is on <see langword="long"/>, every multiplication is
    /// <see langword="checked"/>, and no floating-point value appears
    /// anywhere. The comparison is <c>&gt;=</c> rather than <c>&gt;</c> for
    /// the same reason step 5's entry comparison is: entry equality enters,
    /// which is the exactness convention this class documents throughout.
    /// </para>
    /// <para>
    /// The three divisions truncate toward zero and cannot divide by zero on
    /// any reachable input. <paramref name="supportAllies"/> counts the actor
    /// itself and is therefore at least one for a living agent, the
    /// precondition <see cref="ResolveProvisionalFootwork"/> enforces;
    /// <paramref name="maximumHitPoints"/> is validated to at least one when
    /// the scenario is validated; and the ally-collapse signal short-circuits
    /// to zero when <paramref name="priorSupportAllies"/> is zero, which is
    /// what a freshly spawned agent carries on the first tick. Design section
    /// 5.2 records the full overflow analysis: the tightest intermediate is
    /// the incoming-damage numerator, which still has roughly four hundred
    /// thirty thousand times headroom against <see cref="long.MaxValue"/>, and
    /// <see langword="checked"/> is present so that an unreachable overflow
    /// throws rather than wrapping silently.
    /// </para>
    /// </remarks>
    /// <param name="priorPhase">
    /// The agent's authoritative phase from the previous tick. Only
    /// <see cref="FootworkPhase.Commit"/> and
    /// <see cref="FootworkPhase.Recover"/> can interrupt.
    /// </param>
    /// <param name="supportAllies">
    /// Living allies within the support radius this tick, including the actor
    /// itself — <see cref="LocalMovementContext.SupportAllies"/>.
    /// </param>
    /// <param name="supportEnemies">
    /// Living perceived enemies within the support radius this tick —
    /// <see cref="LocalMovementContext.SupportEnemies"/>.
    /// </param>
    /// <param name="priorSupportAllies">
    /// The same supporting-ally count as it stood on the previous tick. Zero
    /// means no previous tick was recorded, and the collapse signal is then
    /// zero rather than undefined.
    /// </param>
    /// <param name="damageTakenLastTick">
    /// Damage this agent absorbed on the previous tick.
    /// </param>
    /// <param name="maximumHitPoints">
    /// The agent's maximum hit points, the denominator of the incoming-damage
    /// signal. At least one.
    /// </param>
    /// <param name="supportPressureWeightBasisPoints">
    /// The ruleset's shared weight for the support-pressure signal.
    /// </param>
    /// <param name="incomingDamageWeightBasisPoints">
    /// The ruleset's shared weight for the incoming-damage signal.
    /// </param>
    /// <param name="allyCollapseWeightBasisPoints">
    /// The ruleset's shared weight for the ally-collapse signal. The three
    /// weights total exactly 10,000 whenever the preset applies the interrupt,
    /// which <see cref="MovementRuleset"/> enforces at construction.
    /// </param>
    /// <param name="thresholdBasisPoints">
    /// This row's
    /// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>.
    /// Zero — what every row under a preset that does not apply the interrupt
    /// carries — never fires.
    /// </param>
    internal static bool ShouldPressureInterrupt(
        FootworkPhase priorPhase,
        int supportAllies,
        int supportEnemies,
        int priorSupportAllies,
        int damageTakenLastTick,
        int maximumHitPoints,
        int supportPressureWeightBasisPoints,
        int incomingDamageWeightBasisPoints,
        int allyCollapseWeightBasisPoints,
        int thresholdBasisPoints)
    {
        // The transition-only rule: the interrupt exists to preempt the attack
        // lifecycle, so it may only fire from inside that lifecycle.
        if (priorPhase != FootworkPhase.Commit &&
            priorPhase != FootworkPhase.Recover)
        {
            return false;
        }

        // A row that registered no threshold never interrupts, which is what
        // keeps every preset from V1 through V6 on the legacy ladder.
        if (thresholdBasisPoints <= 0)
        {
            return false;
        }

        long weighted = ComputeWeightedPressure(
            supportAllies,
            supportEnemies,
            priorSupportAllies,
            damageTakenLastTick,
            maximumHitPoints,
            supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints);

        return weighted >= checked(thresholdBasisPoints * RatioBasisPointScale);
    }

    /// <summary>
    /// The weighted sum of the three saturating pressure signals — support
    /// pressure, incoming damage, and ally collapse — scaled by
    /// <see cref="RatioBasisPointScale"/> (design section 5.1). This is the
    /// left-hand side of <see cref="ShouldPressureInterrupt"/>'s comparison,
    /// factored out so that the one number the agent inspector shows and the
    /// one number the interrupt weighs are produced by the same arithmetic and
    /// can never drift apart. Divide the result by
    /// <see cref="RatioBasisPointScale"/> to obtain the basis-point value that
    /// is directly comparable to a row's own
    /// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>;
    /// the comparison in <see cref="ShouldPressureInterrupt"/> instead scales
    /// the threshold up, because dividing the sum down first would truncate.
    /// </summary>
    /// <remarks>
    /// The three guards that decide whether an interrupt may fire at all — the
    /// transition-only rule and the zero-threshold rule — deliberately do not
    /// live here. They belong to the predicate, which short-circuits before
    /// reaching this method, while the spectator's pressure row is shown on
    /// every tick regardless of the warrior's phase (design section 3,
    /// question 8, channel 3). This method therefore has exactly the same
    /// preconditions the predicate documents: it divides by
    /// <paramref name="supportAllies"/>, by
    /// <paramref name="maximumHitPoints"/>, and — when that count is non-zero —
    /// by <paramref name="priorSupportAllies"/>, and it guards none of them, so
    /// it may only be called on the living-agent path.
    /// </remarks>
    /// <param name="supportAllies">
    /// Living allies within the support radius this tick, including the actor
    /// itself — <see cref="LocalMovementContext.SupportAllies"/>. At least one
    /// for a living agent, and the divisor of the support-pressure signal.
    /// </param>
    /// <param name="supportEnemies">
    /// Living perceived enemies within the support radius this tick —
    /// <see cref="LocalMovementContext.SupportEnemies"/>.
    /// </param>
    /// <param name="priorSupportAllies">
    /// The same supporting-ally count as it stood on the previous tick. Zero
    /// means no previous tick was recorded, and the collapse signal is then
    /// zero rather than undefined.
    /// </param>
    /// <param name="damageTakenLastTick">
    /// The damage the agent absorbed on the previous tick.
    /// </param>
    /// <param name="maximumHitPoints">
    /// The agent's maximum hit points, the denominator of the incoming-damage
    /// signal. At least one.
    /// </param>
    /// <param name="supportPressureWeightBasisPoints">
    /// The ruleset's shared weight for the support-pressure signal.
    /// </param>
    /// <param name="incomingDamageWeightBasisPoints">
    /// The ruleset's shared weight for the incoming-damage signal.
    /// </param>
    /// <param name="allyCollapseWeightBasisPoints">
    /// The ruleset's shared weight for the ally-collapse signal. The three
    /// weights total exactly 10,000 whenever the preset applies the interrupt,
    /// which <see cref="MovementRuleset"/> enforces at construction.
    /// </param>
    internal static long ComputeWeightedPressure(
        int supportAllies,
        int supportEnemies,
        int priorSupportAllies,
        int damageTakenLastTick,
        int maximumHitPoints,
        int supportPressureWeightBasisPoints,
        int incomingDamageWeightBasisPoints,
        int allyCollapseWeightBasisPoints)
    {
        // Signal A, support pressure: the enemy-to-ally ratio in the support
        // ring, saturated at the shared ceiling.
        long supportPressure = Math.Min(
            SignalCeilingBasisPoints,
            checked(supportEnemies * RatioBasisPointScale) / supportAllies);

        // Signal B, incoming damage: the previous tick's damage as a fraction
        // of maximum hit points, saturated at the same ceiling.
        long incomingDamage = Math.Min(
            SignalCeilingBasisPoints,
            checked(damageTakenLastTick * RatioBasisPointScale) / maximumHitPoints);

        // Signal C, ally collapse: the fraction of the support ring lost since
        // the previous tick. Ally growth yields zero rather than a negative,
        // and this signal needs no ceiling because the loss can never exceed
        // the prior count, so it is naturally at most one whole unit.
        long alliesLost = Math.Max(0L, (long)priorSupportAllies - supportAllies);
        long allyCollapse = priorSupportAllies == 0
            ? 0L
            : checked(alliesLost * RatioBasisPointScale) / priorSupportAllies;

        // The weights sum to exactly one whole unit, so the weighted sum is a
        // true weighted average scaled by RatioBasisPointScale and the
        // threshold stays directly comparable to a single signal's value.
        return checked(
            (supportPressure * supportPressureWeightBasisPoints)
            + (incomingDamage * incomingDamageWeightBasisPoints)
            + (allyCollapse * allyCollapseWeightBasisPoints));
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
    /// <para>
    /// Step 1a is the pressure interrupt. It is numbered 1a rather than by
    /// renumbering the ten steps because those numbers are cited by comments,
    /// by test names, and by both V7 design documents. Its position is forced
    /// rather than chosen: it sits below the dead check, because a dead agent
    /// resolves to <c>(None, 0)</c> and reads no counts; below the argument
    /// validation, because <see cref="ShouldPressureInterrupt"/> divides by
    /// <paramref name="supportAllies"/> and that validation is what guarantees
    /// the count is at least one; and above step 2, because step 2 returns
    /// unconditionally for a prior <c>Commit</c> and everything below it is
    /// unreachable for a committed warrior.
    /// </para>
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
    /// <param name="pressureInterruptFired">
    /// Whether <see cref="ShouldPressureInterrupt"/> fired for this agent this
    /// tick, as the caller computed it once and kept it. The default
    /// <see langword="false"/> is the legacy ladder exactly and by
    /// construction, which is what every preset from V1 through V6 and every
    /// call site written before the interrupt existed continues to get. It is
    /// a trailing optional parameter for that reason: widening the return
    /// tuple would break every helper's declared return type, and an
    /// <see langword="out"/> parameter cannot be defaulted, so either would
    /// have forced edits on call sites the interrupt does not concern.
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
            bool targetAtOrInsidePreferredDistance,
            bool pressureInterruptFired = false)
    {
        // Step 1: dead.
        if (!isAlive)
        {
            return (FootworkPhase.None, 0);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(supportAllies, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(supportEnemies);

        // Step 1a, the pressure interrupt: a warrior broken off mid-lifecycle
        // takes Disengage with a zero timer, matching every other Disengage
        // return in this ladder. It is numbered 1a rather than by renumbering
        // the ten steps, whose numbers are cited elsewhere; the remarks above
        // record why this position is forced rather than chosen. The finalised
        // phase still goes through FinalizeFootwork exactly as any other
        // provisional phase does, so lane clearance can still fall it back and
        // nothing downstream learns this Disengage arrived by a different
        // route.
        if (pressureInterruptFired)
        {
            return (FootworkPhase.Disengage, 0);
        }

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
