using Hukbo.Core.Simulation;

namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, testable arithmetic for the persistent-contingent movement preset:
/// the cohesion duty cycle, the leader-and-living-count forward scan, the
/// unit-state transition rules, and the movement-gate conjunction. Every
/// static here reads only its own scalar arguments — no agent array beyond
/// the one forward scan that has to see one, no simulation, no tick
/// pipeline — so <c>Hukbo.Core.Tests</c> calls each one directly with
/// hand-built inputs rather than only observing it through a whole battle.
/// This is the same testability shape <see cref="FormationRules"/> already
/// uses for the rally geometry, and the reason
/// <c>Hukbo.Core</c> carries <c>[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]</c>
/// (<c>src/Hukbo.Core/Properties/AssemblyInfo.cs:3</c>). See
/// docs/plans/2026-07-28-formation-movement-realism-design.md sections 3.4
/// and 3.5 for the derivations.
/// </summary>
internal static class MovementRules
{
    /// <summary>
    /// The cohesion duty-cycle predicate: whether a contingent in slot
    /// <paramref name="slot"/> may be granted a cohesion destination on
    /// <paramref name="tick"/>. A pure function of two integers and nothing
    /// else — no spread, no blocking, no enemy proximity, no casualties, no
    /// stored per-contingent field, nothing in the state hash. Because both
    /// <c>ResolveContingentStates</c> and the movement-gate check in
    /// <c>GatherMovementProposals</c> call this same function, the two
    /// stages evaluate the window independently and cannot disagree about
    /// whether it is open.
    /// </summary>
    /// <param name="tick">The current simulation tick.</param>
    /// <param name="slot"><c>FactionId * MaximumContingents + ContingentId</c>.</param>
    /// <param name="cohesionCycleTicks">
    /// The length, in ticks, of the duty cycle. A game-design choice, not a
    /// measurement.
    /// </param>
    /// <param name="cohesionDutyTicks">
    /// The number of ticks, out of every <paramref name="cohesionCycleTicks"/>,
    /// during which the window is open. A game-design choice, not a
    /// measurement.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the window is open for this slot on this
    /// tick.
    /// </returns>
    internal static bool IsCohesionWindowOpen(
        int tick,
        int slot,
        int cohesionCycleTicks,
        int cohesionDutyTicks)
    {
        var cohesionPhase = checked(slot * cohesionCycleTicks / 16);
        var phased = checked(tick + cohesionPhase) % cohesionCycleTicks;
        return phased < cohesionDutyTicks;
    }

    /// <summary>
    /// One forward scan over <paramref name="agents"/>, writing each living
    /// contingent's leader (the lowest living <see cref="AgentState.EntityId"/>
    /// among its members) and living headcount into the caller's preallocated
    /// sixteen-slot arrays. A slot with no living member is written as
    /// <c>0</c> in both arrays — <c>0</c> is never a valid
    /// <see cref="AgentState.EntityId"/>, so it is a safe empty sentinel,
    /// matching <see cref="Simulation.BattleSimulation"/>'s existing
    /// per-faction rally scan.
    /// </summary>
    /// <remarks>
    /// The comparison is against <see cref="AgentState.EntityId"/> explicitly,
    /// never against array position, so the result does not depend on the
    /// incidental order of <paramref name="agents"/>. Both output arrays are
    /// the caller's own, sized once at construction, so this allocates
    /// nothing.
    /// </remarks>
    /// <param name="agents">Every agent in the battle, in any order.</param>
    /// <param name="leaderEntityIdsBySlot">
    /// The caller's preallocated sixteen-slot leader output array.
    /// </param>
    /// <param name="livingCountsBySlot">
    /// The caller's preallocated sixteen-slot living-headcount output array.
    /// </param>
    internal static void ScanContingentLeadersAndLivingCounts(
        AgentState[] agents,
        ulong[] leaderEntityIdsBySlot,
        int[] livingCountsBySlot)
    {
        Array.Clear(leaderEntityIdsBySlot);
        Array.Clear(livingCountsBySlot);

        foreach (var agent in agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            livingCountsBySlot[slot]++;

            if (leaderEntityIdsBySlot[slot] == 0 ||
                agent.EntityId < leaderEntityIdsBySlot[slot])
            {
                leaderEntityIdsBySlot[slot] = agent.EntityId;
            }
        }
    }

    /// <summary>
    /// The six priority-ordered unit-state transition rules of design section
    /// 3.4, and nothing else. Every argument is a scalar the calling stage
    /// already holds; this touches no agent array, no simulation and no tick
    /// pipeline, so a test can pin the priority order directly with
    /// hand-built inputs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="livingCount"/> equal to zero yields
    /// <see cref="ContingentState.None"/> regardless of every other
    /// argument, ahead of even rule 1 (<see cref="ContingentState.Break"/> is
    /// terminal): a contingent with no living member has nothing left to
    /// hold a state for. Below that, the six rules are, first match wins:
    /// (1) a previous state of <see cref="ContingentState.Break"/> stays
    /// <see cref="ContingentState.Break"/>; (2) attrition —
    /// <c>livingCount * 4 &lt;= initialCount</c> or
    /// <c>livingCount &lt; minimumCohesiveMembers</c> — breaks; (3) a
    /// hysteresis-banded count of members in contact closes — see the
    /// paragraph below; (4) a shut duty-cycle window, <b>or</b> a denial from
    /// either geometric gate, forces <see cref="ContingentState.Advance"/> —
    /// this is the rule that keeps the inspector honest, so a contingent
    /// whose members are in fact pursuing independently is never labelled
    /// <see cref="ContingentState.Hold"/>; (5) the hysteresis-banded
    /// gathering test enters <see cref="ContingentState.Hold"/> above
    /// <c>cohesionRadiusRaw</c> and remains there down to
    /// <c>3/4 * cohesionRadiusRaw</c>; (6) otherwise
    /// <see cref="ContingentState.Advance"/>.
    /// </para>
    /// <para>
    /// Rule 3 is itself a hysteresis band, the same shape as rule 5's spread
    /// test but counting living members in contact rather than measuring a
    /// squared distance. Let <c>entryThreshold = Max(1, CeilDiv(livingCount *
    /// closeFractionNumerator, closeFractionDenominator))</c> and
    /// <c>exitThreshold = Max(1, CeilDiv(livingCount * closeFractionNumerator,
    /// 2 * closeFractionDenominator))</c>, both computed with integer
    /// ceiling division so a fraction is never rounded down to a weaker
    /// threshold than intended. A contingent not already
    /// <see cref="ContingentState.Close"/> enters it once
    /// <paramref name="contactCount"/> reaches <c>entryThreshold</c>; one
    /// already <see cref="ContingentState.Close"/> stays there until
    /// <paramref name="contactCount"/> falls below the strictly lower
    /// <c>exitThreshold</c>. Halving the entry fraction to obtain the exit
    /// fraction is a chosen band width, not a derived one — design section 7
    /// leaves the width open, and T7's state-flip frequency measurement is
    /// what settles it. The <c>Max(1, ...)</c> floor makes a preset
    /// registered at <c>closeFractionNumerator = 0</c> collapse both
    /// thresholds to <c>1</c>, reproducing exactly the single-member
    /// minimum-distance test this rule used before contact counting replaced
    /// it.
    /// </para>
    /// </remarks>
    /// <param name="previousState">
    /// The contingent's <see cref="ContingentState"/> at the start of this
    /// tick, read from its current leader.
    /// </param>
    /// <param name="livingCount">The contingent's living headcount.</param>
    /// <param name="initialCount">
    /// The contingent's headcount at spawn, fixed for the whole battle.
    /// </param>
    /// <param name="spreadSquared">
    /// The maximum, over living non-leader members, of the squared distance
    /// from that member to the leader. Zero when there are none.
    /// </param>
    /// <param name="contactCount">
    /// The number of living members whose selected target lies within
    /// <c>closeRadiusRaw</c> (<c>CloseRadiusMultiplier * BodyRadiusRaw</c>).
    /// Compared against the entry and exit thresholds derived from
    /// <paramref name="closeFractionNumerator"/> and
    /// <paramref name="closeFractionDenominator"/> — see the second
    /// <see cref="ResolveContingentState"/> remarks paragraph above.
    /// </param>
    /// <param name="cohesionRadiusRaw">
    /// <c>CohesionRadiusMultiplier * BodyRadiusRaw</c>.
    /// </param>
    /// <param name="closeFractionNumerator">
    /// The numerator of the fraction of living members that must be in
    /// contact for rule 3 to close. A game-design choice, not a measurement.
    /// </param>
    /// <param name="closeFractionDenominator">
    /// The denominator of the fraction of living members that must be in
    /// contact for rule 3 to close. A game-design choice, not a measurement.
    /// </param>
    /// <param name="minimumCohesiveMembers">
    /// The living-member floor below which a contingent breaks regardless of
    /// its casualty ratio.
    /// </param>
    /// <param name="windowOpen">
    /// Whether <see cref="IsCohesionWindowOpen"/> reports the duty-cycle
    /// window open for this contingent on this tick.
    /// </param>
    /// <param name="geometricGatesPass">
    /// Whether both the map-edge and cross-contingent geometric gates of
    /// design section 3.5 permit this contingent a cohesion destination this
    /// tick. <see langword="false"/> forces
    /// <see cref="ContingentState.Advance"/> for the same reason a shut duty
    /// cycle does, so the inspector never reports
    /// <see cref="ContingentState.Hold"/> while every member is in fact
    /// pursuing independently.
    /// </param>
    /// <returns>The contingent's resolved state for this tick.</returns>
    internal static ContingentState ResolveContingentState(
        ContingentState previousState,
        int livingCount,
        int initialCount,
        long spreadSquared,
        int contactCount,
        long cohesionRadiusRaw,
        int closeFractionNumerator,
        int closeFractionDenominator,
        int minimumCohesiveMembers,
        bool windowOpen,
        bool geometricGatesPass)
    {
        if (livingCount == 0)
        {
            return ContingentState.None;
        }

        if (previousState == ContingentState.Break)
        {
            return ContingentState.Break;
        }

        if (checked(livingCount * 4) <= initialCount ||
            livingCount < minimumCohesiveMembers)
        {
            return ContingentState.Break;
        }

        var scaledLivingCount = checked((long)livingCount * closeFractionNumerator);
        var entryThreshold = Math.Max(1L, CeilDiv(scaledLivingCount, closeFractionDenominator));
        var exitThreshold = Math.Max(
            1L,
            CeilDiv(scaledLivingCount, checked(2L * closeFractionDenominator)));
        var closeThreshold = previousState == ContingentState.Close
            ? exitThreshold
            : entryThreshold;

        if (contactCount >= closeThreshold)
        {
            return ContingentState.Close;
        }

        if (!windowOpen || !geometricGatesPass)
        {
            return ContingentState.Advance;
        }

        var cohesionRadiusSquared = checked(cohesionRadiusRaw * cohesionRadiusRaw);

        // spreadSquared is an unscaled squared world distance bounded only by
        // Scenario.MaximumMapDimension, so spreadSquared * 16 can exceed
        // long.MaxValue on a wide map even though cohesionRadiusSquared
        // cannot. Widening to Int128 makes the comparison exact and total: it
        // reproduces the checked long comparison bit-for-bit whenever that
        // comparison would not have overflowed, and for the inputs that would
        // have overflowed it returns the answer implied by unbounded integer
        // arithmetic — spreadSquared * 16 is astronomically larger than any
        // representable 9 * cohesionRadiusSquared, so the contingent is
        // unambiguously straggling rather than gathered.
        var qualifies = previousState == ContingentState.Hold
            ? (Int128)spreadSquared * 16 > (Int128)9 * cohesionRadiusSquared
            : spreadSquared > cohesionRadiusSquared;

        return qualifies ? ContingentState.Hold : ContingentState.Advance;
    }

    /// <summary>
    /// Ceiling integer division, <c>(dividend + divisor - 1) / divisor</c>,
    /// exact for non-negative operands. The only caller is
    /// <see cref="ResolveContingentState"/>'s rule 3 threshold arithmetic,
    /// where both operands are always non-negative, so there is no signed
    /// division and no floating point anywhere in the computation.
    /// </summary>
    /// <param name="dividend">The non-negative numerator.</param>
    /// <param name="divisor">The positive denominator.</param>
    /// <returns>The smallest integer greater than or equal to the exact
    /// quotient <paramref name="dividend"/> / <paramref name="divisor"/>.</returns>
    private static long CeilDiv(long dividend, long divisor)
    {
        return checked(dividend + divisor - 1) / divisor;
    }

    /// <summary>
    /// The conjunction of the six movement gates of design section 3.5: an
    /// agent is eligible for a cohesion destination this tick exactly when
    /// every one of the six permits it. Unlike
    /// <see cref="ResolveContingentState"/>'s six transition rules, these six
    /// gates have no priority order — every one is an unconditional denial,
    /// so the branch taken is the logical conjunction of all six and no gate
    /// can "win" over another. Evaluation order here is therefore a reading
    /// and short-circuit order only, never a semantic one.
    /// </summary>
    /// <param name="state">
    /// The contingent's <see cref="ContingentState"/> for this tick. Only
    /// <see cref="ContingentState.Hold"/> and
    /// <see cref="ContingentState.Advance"/> can ever pass gate 1; every
    /// other value denies unconditionally.
    /// </param>
    /// <param name="isLeader">
    /// Whether this agent is its contingent's own leader. A leader is always
    /// exempt (gate 2).
    /// </param>
    /// <param name="windowOpen">
    /// Whether <see cref="IsCohesionWindowOpen"/> reports the duty-cycle
    /// window open for this contingent on this tick (gate 3).
    /// </param>
    /// <param name="straggling">
    /// Whether this member has fallen beyond the straggler threshold behind
    /// its leader. Only consulted when <paramref name="state"/> is
    /// <see cref="ContingentState.Advance"/> (gate 4); a
    /// <see cref="ContingentState.Hold"/> contingent pulls every non-leader
    /// moving member regardless of this value.
    /// </param>
    /// <param name="squareFitsMap">
    /// Whether this contingent's bias square fits inside the map this tick
    /// (gate 5).
    /// </param>
    /// <param name="squareOverlapsAnother">
    /// Whether this contingent's bias square overlaps another living
    /// same-faction contingent's this tick (gate 6).
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when all six gates permit a cohesion
    /// destination.
    /// </returns>
    internal static bool IsCohesionEligible(
        ContingentState state,
        bool isLeader,
        bool windowOpen,
        bool straggling,
        bool squareFitsMap,
        bool squareOverlapsAnother)
    {
        if (state != ContingentState.Hold && state != ContingentState.Advance)
        {
            return false;
        }

        if (isLeader)
        {
            return false;
        }

        if (!windowOpen)
        {
            return false;
        }

        if (state == ContingentState.Advance && !straggling)
        {
            return false;
        }

        if (!squareFitsMap)
        {
            return false;
        }

        if (squareOverlapsAnother)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The arrival-slowdown taper of design section 3.6: the raw step an
    /// agent takes this tick toward its destination, active only under
    /// <see cref="MovementPresetId.PersistentContingentsV2"/>. Everywhere
    /// outside the final <paramref name="taperRaw"/> of remaining distance
    /// the result is exactly the untapered step,
    /// <c>Min(movementSpeedRaw, remainingRaw)</c>; inside it, the step scales
    /// down linearly with the fraction of the taper band still to cross, so a
    /// warrior eases into contact instead of snapping to a halt against an
    /// enemy body.
    /// </summary>
    /// <remarks>
    /// The taper can only ever <em>reduce</em> a step relative to the
    /// untapered formula, so it cannot cause tunnelling through a target and
    /// cannot break <c>Scenario.Validate</c>'s
    /// <c>MovementSpeedRaw &lt;= BodyRadiusRaw</c> invariant
    /// (<c>src/Hukbo.Core/Simulation/Scenario.cs:326-343</c>). The
    /// <c>Math.Max(1, ...)</c> floor preserves the existing one-raw-unit
    /// movement floor, so the arrived-guard semantics that stop a settled
    /// cluster from emitting a <c>Move</c> event every tick are unchanged.
    /// All intermediates are <see langword="long"/>; there is no floating
    /// point anywhere in this method.
    /// </remarks>
    /// <param name="remainingRaw">
    /// The caller's already-computed <c>Max(1, distance - stopShortRaw)</c> —
    /// the raw distance still to close this tick, before any speed cap.
    /// </param>
    /// <param name="movementSpeedRaw">
    /// The agent's raw movement speed for this tick, i.e.
    /// <c>AgentState.MovementSpeedRaw</c>.
    /// </param>
    /// <param name="taperRaw">
    /// <c>ArrivalTaperMultiplier * BodyRadiusRaw</c> — the width of the final
    /// approach band, inside which the taper is active.
    /// </param>
    /// <returns>
    /// The raw step to take this tick: at least <c>1</c>, never more than
    /// <paramref name="movementSpeedRaw"/>, and never more than the
    /// untapered step <c>Min(movementSpeedRaw, remainingRaw)</c> would have
    /// produced.
    /// </returns>
    internal static long ComputeArrivalStepRaw(
        long remainingRaw,
        int movementSpeedRaw,
        long taperRaw)
    {
        var untaperedStepRaw = Math.Min((long)movementSpeedRaw, remainingRaw);

        if (remainingRaw >= taperRaw)
        {
            return untaperedStepRaw;
        }

        var taperedStepRaw = checked(untaperedStepRaw * remainingRaw / taperRaw);
        return Math.Max(1L, taperedStepRaw);
    }
}
