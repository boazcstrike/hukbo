using System.Collections.Immutable;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Determinism;

/// <summary>
/// Folds a <see cref="Mission"/> and its live <see cref="MissionState"/>,
/// under a <see cref="SandataRuleset"/>, into Sandata's state hash — the
/// mission-simulation analogue of <c>Hukbo.Core.Determinism.StateHasher</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pinned field order.</b> <see cref="Compute"/> folds, through
/// <see cref="SandataHash"/>, in exactly this order — design section 4's own
/// bullet order for the mission-level fields, the per-operator fields, and
/// the four per-collection bullets that follow, with <c>MissionContentHash</c>
/// and <c>SandataRuleset.ContentHash</c> folded last exactly where design
/// section 4's own bullet list places them:
/// </para>
/// <list type="number">
/// <item><description><see cref="MissionState.Tick"/></description></item>
/// <item><description><see cref="MissionState.Phase"/></description></item>
/// <item><description><see cref="MissionState.Winner"/></description></item>
/// <item><description><see cref="MissionState.NextEntityId"/></description></item>
/// <item><description><see cref="MissionState.NextEventSequence"/></description></item>
/// <item><description>
/// <see cref="MissionState.Operators"/>' count, then per operator, in the
/// array's stored order: <c>EntityId</c> (an implementer addition — see
/// <see cref="OperatorState"/>'s remarks — folded first, parallel to how
/// <c>Hukbo.Core.Determinism.StateHasher</c> folds <c>agent.EntityId</c>
/// first), then <c>PositionX</c>, <c>PositionY</c>, <c>Facing</c>,
/// <c>AimAngle</c>, <c>Health</c>, <c>Faction</c>,
/// <c>Intent</c>, <c>IsCrouched</c>, <c>WeaponLowered</c>,
/// <c>WeaponChainPhase</c>, <c>WeaponChainRemainingTicks</c>,
/// <c>MagazineRounds</c>, <c>CyclicFireAccumulator</c>,
/// <c>SuppressionCounter</c>, then that operator's
/// <c>ContactMemory</c>'s count and, for each entry, in the array's stored
/// order, <c>EnemyEntityId</c>, <c>LastKnownCellIndex</c>, <c>ContactTier</c>,
/// <c>LastSeenTick</c>, and finally (task 79c of Sandata's scaffold plan,
/// the wave-12 audit's corrected
/// obligation) that operator's <c>Firearm</c>, folded last in this block —
/// after the contact-memory entries, not interleaved among the scalar
/// fields above. <c>SquadSlotIndex</c> is not folded: task 64's
/// 2026-08-07 correction removed it from <see cref="OperatorState"/> because
/// design section 8 derives it every tick and stores nothing per group.
/// <c>EntityId</c>, <c>EnemyEntityId</c>, and <c>Groups</c>' <c>GroupId</c>
/// below are <see langword="ulong"/> and fold through the same
/// <c>unchecked((long)value)</c> reinterpretation <see cref="Mission.MissionContentHash"/>
/// and <see cref="SandataRuleset.ContentHash"/> already use lower in this
/// list, so the fold order and the hash algorithm are unchanged — only the
/// C# type carrying the bits changed.
/// <para>
/// Design section 4 states an operator's own fields and its contact memory
/// as two separate bullets ("Per operator: position ... suppression
/// counter." and, on the next line, "Per operator: contact memory ..."). This
/// hasher resolves that into one combined per-operator block — each
/// operator's own fields immediately followed by that same operator's
/// contact memory — rather than a second, separate pass over every operator.
/// Both readings satisfy "exactly the authoritative fields, in a fixed
/// order"; this is the implementer's documented choice between them, flagged
/// for the design owner to confirm.
/// </para>
/// </description></item>
/// <item><description>
/// <see cref="MissionState.FactionAlerts"/>' count, then per entry, in the
/// array's stored order: <c>FactionId</c>, <c>AlertLevel</c>.
/// </description></item>
/// <item><description>
/// <see cref="MissionState.Doors"/>' count, then per entry, in the array's
/// stored order: <c>DoorId</c>, <c>IsOpen</c>, <c>LastChangedTick</c>.
/// </description></item>
/// <item><description>
/// <see cref="MissionState.Groups"/>' count, then per entry, in the array's
/// stored order: <c>GroupId</c>, <c>DestinationCellIndex</c>,
/// <c>HasOutstandingRequest</c>, <c>StartCellIndex</c>, <c>GoalCellIndex</c>,
/// <c>RequestTick</c>.
/// </description></item>
/// <item><description>
/// <see cref="MissionState.RngStreams"/>' count, then per entry, in the
/// array's stored order: <c>StreamId</c>, <c>AlgorithmId</c>,
/// <c>RootSeed</c>, <c>StreamState</c>.
/// </description></item>
/// <item><description><see cref="Mission.MissionContentHash"/></description></item>
/// <item><description><see cref="SandataRuleset.ContentHash"/></description></item>
/// <item><description>
/// <b>Task 61 addition (Sandata's scaffold plan), appended
/// here rather than interleaved among the items above.</b>
/// <see cref="MissionState.OrderQueue"/>, folded only when it is not equal to
/// <see cref="Orders.OrderQueue.Empty"/>: first <c>NextOrderId</c> and
/// <c>NextOrderSequence</c>, then the queue's applied order count and, for
/// each order in <see cref="Orders.OrderQueue.InApplicationOrder"/>'s
/// ascending <c>(TargetTick, OrderSequence)</c> order — design section 16:
/// "it folds into the state hash, in ascending
/// <c>(TargetTick, OrderSequence)</c>" — <c>OrderId</c>,
/// <c>OrderSequence</c>, <c>TargetTick</c>, <c>FactionId</c>, <c>Kind</c>,
/// <c>Addressees</c>' count then each entry, and <c>PathNodes</c>' count then
/// each entry's <c>X</c> and <c>Y</c>.
/// </description></item>
/// <item><description>
/// <see cref="MissionState.OrderAssignments"/>' count then, for each entry in
/// the array's stored (ascending <c>EntityId</c>) order, <c>EntityId</c>,
/// <c>OrderId</c>, <c>CurrentNodeIndex</c>, and <c>PathNodes</c>' count then
/// each entry's <c>X</c> and <c>Y</c> — folded only when the array is
/// non-empty.
/// </description></item>
/// <item><description>
/// <b>Stage 0 addition (design decision 6 of
/// <c>docs/plans/2026-08-14-sandata-clear-the-map-design.md</c>), appended
/// here rather than interleaved among the items above.</b>
/// <see cref="MissionState.RoomClearStates"/>' count then, for each entry in
/// the array's stored order, <c>RoomId</c> then <c>Cleared</c> — folded only
/// when the array is non-empty.
/// </description></item>
/// <item><description>
/// <b>Stage 0 addition, appended last, after
/// <see cref="MissionState.RoomClearStates"/>.</b> <see cref="MissionState.Groups"/>'
/// count then, for each entry in the array's stored order,
/// <see cref="GroupPathState.TargetRoomId"/> alone — folded only when at
/// least one group's <c>TargetRoomId</c> differs from its <c>-1</c> default,
/// which is what keeps <c>MissionStateTests.PreTask79cBaselineHash</c>'s
/// pinned literal unmoved.
/// </description></item>
/// </list>
/// <para>
/// <b>Why "folded only when non-empty/non-default" for both new items.</b>
/// <see cref="SandataHash.Fold(ref ulong, long)"/>'s underlying FNV-1a fold is never a no-op — folding a zero-valued
/// field still changes the running hash — so appending an unconditional
/// count fold of <c>0</c> for a mission that never used the order layer would
/// silently change every hash computed by this method relative to the
/// pre-task-61 build, for every existing caller, even one that submits no
/// order and assigns no operator. Gating both new folds on "is there
/// anything here to report" instead means a mission whose
/// <see cref="MissionState.OrderQueue"/> is still
/// <see cref="Orders.OrderQueue.Empty"/> and whose
/// <see cref="MissionState.OrderAssignments"/> is still empty produces the
/// exact same <see cref="Compute"/> output this method produced before this
/// task. <c>OrderStateHashTests.StateHash_WithEmptyOrderQueueAndNoAssignments_IsUnaffectedByHowTheEmptyQueueWasConstructed</c>
/// guards that gate, and
/// <c>OrderStateHashTests.StateHash_QueueWithAdvancedCountersButNoStoredOrders_DiffersFromTheEmptyBaseline</c>
/// guards its converse. Both compare two computed hashes rather than pinning
/// an absolute literal, so a legitimate future change to the operator fold
/// does not break either one — see task 85. Any real order-layer activity (even a queue
/// whose <c>Orders</c> array is empty because every submission was rejected,
/// which still advances <c>NextOrderId</c>/<c>NextOrderSequence</c> away from
/// zero) is not equal to <see cref="Orders.OrderQueue.Empty"/> and is folded
/// in full, so this gate never hides a real state difference — it only makes
/// the one, singular "nothing has happened yet" state byte-identical to the
/// state that predates the order layer entirely.
/// </para>
/// <para>
/// Every array is folded in the order it is already stored in, never
/// re-sorted here — <see cref="MissionState"/>'s remarks place that ordering
/// obligation on whichever caller builds the state, not on this hasher.
/// <see cref="MissionState.OrderQueue"/> is the one exception: its own
/// <see cref="Orders.OrderQueue.InApplicationOrder"/> — not
/// <see cref="Orders.OrderQueue.Orders"/>'s raw submission order — is what
/// design section 16 names as the fold order, so this hasher calls that
/// method rather than reading <see cref="Orders.OrderQueue.Orders"/>
/// directly. Changing this fold order, or any field it folds, is a new
/// preset version with new golden expectations, per design section 4 and
/// <c>CLAUDE.md</c> section 5.
/// </para>
/// </remarks>
internal static class SandataStateHasher
{
    internal static ulong Compute(Mission mission, MissionState state, SandataRuleset ruleset)
    {
        var hash = SandataHash.Begin();

        SandataHash.Fold(ref hash, state.Tick);
        SandataHash.Fold(ref hash, state.Phase);
        SandataHash.Fold(ref hash, state.Winner);
        SandataHash.Fold(ref hash, unchecked((long)state.NextEntityId));
        SandataHash.Fold(ref hash, state.NextEventSequence);

        var operators = state.Operators;
        SandataHash.Fold(ref hash, operators.IsDefault ? 0 : operators.Length);
        if (!operators.IsDefault)
        {
            foreach (var operatorState in operators)
            {
                FoldOperator(ref hash, operatorState);
            }
        }

        var factionAlerts = state.FactionAlerts;
        SandataHash.Fold(ref hash, factionAlerts.IsDefault ? 0 : factionAlerts.Length);
        if (!factionAlerts.IsDefault)
        {
            foreach (var factionAlert in factionAlerts)
            {
                SandataHash.Fold(ref hash, factionAlert.FactionId);
                SandataHash.Fold(ref hash, factionAlert.AlertLevel);
            }
        }

        var doors = state.Doors;
        SandataHash.Fold(ref hash, doors.IsDefault ? 0 : doors.Length);
        if (!doors.IsDefault)
        {
            foreach (var door in doors)
            {
                SandataHash.Fold(ref hash, door.DoorId);
                SandataHash.Fold(ref hash, door.IsOpen);
                SandataHash.Fold(ref hash, door.LastChangedTick);
            }
        }

        var groups = state.Groups;
        SandataHash.Fold(ref hash, groups.IsDefault ? 0 : groups.Length);
        if (!groups.IsDefault)
        {
            foreach (var group in groups)
            {
                SandataHash.Fold(ref hash, unchecked((long)group.GroupId));
                SandataHash.Fold(ref hash, group.DestinationCellIndex);
                SandataHash.Fold(ref hash, group.HasOutstandingRequest);
                SandataHash.Fold(ref hash, group.StartCellIndex);
                SandataHash.Fold(ref hash, group.GoalCellIndex);
                SandataHash.Fold(ref hash, group.RequestTick);
            }
        }

        var rngStreams = state.RngStreams;
        SandataHash.Fold(ref hash, rngStreams.IsDefault ? 0 : rngStreams.Length);
        if (!rngStreams.IsDefault)
        {
            foreach (var rngStream in rngStreams)
            {
                SandataHash.Fold(ref hash, rngStream.StreamId);
                SandataHash.Fold(ref hash, rngStream.AlgorithmId);
                SandataHash.Fold(ref hash, unchecked((long)rngStream.RootSeed));
                SandataHash.Fold(ref hash, unchecked((long)rngStream.StreamState));
            }
        }

        SandataHash.Fold(ref hash, unchecked((long)mission.MissionContentHash));
        SandataHash.Fold(ref hash, unchecked((long)ruleset.ContentHash));

        // Task 61: appended after every field above, never interleaved among
        // them — see this type's own remarks for why each fold below is
        // gated on "is there anything here to report" rather than
        // unconditional.
        FoldOrderQueue(ref hash, state.OrderQueue);
        FoldOrderAssignments(ref hash, state.OrderAssignments);

        // Stage 0 (design decision 6 of
        // docs/plans/2026-08-14-sandata-clear-the-map-design.md), appended
        // after every field above, in the same "gated on anything real to
        // report" style Task 61 established: an empty RoomClearStates array
        // and a Groups array whose TargetRoomId is every entry's default -1
        // are both this method's own pre-Stage-0 output byte for byte,
        // including MissionStateTests.PreTask79cBaselineHash's pinned
        // literal, which carries one GroupPathState built before
        // TargetRoomId existed.
        FoldRoomClearStates(ref hash, state.RoomClearStates);
        FoldGroupTargetRoomIds(ref hash, state.Groups);

        return hash;
    }

    /// <summary>
    /// Folds <paramref name="roomClearStates"/> into <paramref name="hash"/>,
    /// but only when it is non-empty — see <see cref="Compute"/>'s own
    /// remarks on this fold's placement for why.
    /// </summary>
    private static void FoldRoomClearStates(ref ulong hash, ImmutableArray<RoomClearState> roomClearStates)
    {
        if (roomClearStates.IsDefaultOrEmpty)
        {
            return;
        }

        SandataHash.Fold(ref hash, roomClearStates.Length);
        foreach (var roomClearState in roomClearStates)
        {
            SandataHash.Fold(ref hash, roomClearState.RoomId);
            SandataHash.Fold(ref hash, roomClearState.Cleared);
        }
    }

    /// <summary>
    /// Folds every group's <see cref="GroupPathState.TargetRoomId"/> as its
    /// own separate, gated tail block, rather than interleaving it into each
    /// group's existing six-field fold above. Every <see cref="GroupPathState"/>
    /// built before Stage 0 leaves <see cref="GroupPathState.TargetRoomId"/>
    /// at its default <c>-1</c>, so interleaving an unconditional seventh
    /// fold into that per-group block would move this method's output for
    /// every caller with a non-empty <see cref="MissionState.Groups"/> —
    /// including <c>MissionStateTests.PreTask79cBaselineHash</c>'s pinned
    /// literal — even one that never touches room sweeping. The gate is "any
    /// group's <see cref="GroupPathState.TargetRoomId"/> differs from
    /// <c>-1</c>", so a mission with a real room sweep in progress folds this
    /// block in full and every other caller's hash is unaffected.
    /// </summary>
    private static void FoldGroupTargetRoomIds(ref ulong hash, ImmutableArray<GroupPathState> groups)
    {
        if (groups.IsDefaultOrEmpty)
        {
            return;
        }

        var anyTargeted = false;
        foreach (var group in groups)
        {
            if (group.TargetRoomId != -1)
            {
                anyTargeted = true;
                break;
            }
        }

        if (!anyTargeted)
        {
            return;
        }

        SandataHash.Fold(ref hash, groups.Length);
        foreach (var group in groups)
        {
            SandataHash.Fold(ref hash, group.TargetRoomId);
        }
    }

    /// <summary>
    /// Folds <paramref name="queue"/> into <paramref name="hash"/>, but only
    /// when it is not equal to <see cref="Orders.OrderQueue.Empty"/> — see
    /// this type's own remarks for why an unconditional fold would change
    /// every hash this method has ever produced, including for a mission
    /// that never touches the order layer.
    /// </summary>
    private static void FoldOrderQueue(ref ulong hash, OrderQueue queue)
    {
        if (queue.Equals(OrderQueue.Empty))
        {
            return;
        }

        SandataHash.Fold(ref hash, queue.NextOrderId);
        SandataHash.Fold(ref hash, queue.NextOrderSequence);

        var applied = queue.InApplicationOrder();
        SandataHash.Fold(ref hash, applied.Length);
        foreach (var order in applied)
        {
            FoldOrder(ref hash, order);
        }
    }

    private static void FoldOrder(ref ulong hash, Order order)
    {
        SandataHash.Fold(ref hash, order.OrderId);
        SandataHash.Fold(ref hash, order.OrderSequence);
        SandataHash.Fold(ref hash, order.TargetTick);
        SandataHash.Fold(ref hash, order.FactionId);
        SandataHash.Fold(ref hash, (long)order.Kind);

        var addressees = order.Addressees;
        SandataHash.Fold(ref hash, addressees.IsDefault ? 0 : addressees.Length);
        if (!addressees.IsDefault)
        {
            foreach (var addressee in addressees)
            {
                SandataHash.Fold(ref hash, unchecked((long)addressee));
            }
        }

        FoldPathNodes(ref hash, order.PathNodes);
    }

    /// <summary>
    /// Folds <paramref name="assignments"/> into <paramref name="hash"/>, but
    /// only when the array is not empty — the assignment analogue of
    /// <see cref="FoldOrderQueue"/>'s empty-queue gate. There is no counter
    /// analogue here to worry about: an empty <c>OrderAssignments</c> array
    /// has no other field that could make it a real, non-default state, so a
    /// plain emptiness check is the whole condition.
    /// </summary>
    private static void FoldOrderAssignments(ref ulong hash, ImmutableArray<OrderAssignment> assignments)
    {
        if (assignments.IsDefaultOrEmpty)
        {
            return;
        }

        SandataHash.Fold(ref hash, assignments.Length);
        foreach (var assignment in assignments)
        {
            SandataHash.Fold(ref hash, unchecked((long)assignment.EntityId));
            SandataHash.Fold(ref hash, assignment.OrderId);
            SandataHash.Fold(ref hash, assignment.CurrentNodeIndex);
            FoldPathNodes(ref hash, assignment.PathNodes);
        }
    }

    private static void FoldPathNodes(ref ulong hash, ImmutableArray<OrderPathNode> pathNodes)
    {
        SandataHash.Fold(ref hash, pathNodes.IsDefault ? 0 : pathNodes.Length);
        if (pathNodes.IsDefault)
        {
            return;
        }

        foreach (var node in pathNodes)
        {
            SandataHash.Fold(ref hash, node.X);
            SandataHash.Fold(ref hash, node.Y);
        }
    }

    private static void FoldOperator(ref ulong hash, OperatorState operatorState)
    {
        SandataHash.Fold(ref hash, unchecked((long)operatorState.EntityId));
        SandataHash.Fold(ref hash, operatorState.PositionX.RawValue);
        SandataHash.Fold(ref hash, operatorState.PositionY.RawValue);
        SandataHash.Fold(ref hash, (long)operatorState.Facing);
        SandataHash.Fold(ref hash, operatorState.AimAngle.Raw);
        SandataHash.Fold(ref hash, operatorState.Health);
        SandataHash.Fold(ref hash, operatorState.Faction);
        SandataHash.Fold(ref hash, operatorState.Intent);
        SandataHash.Fold(ref hash, operatorState.IsCrouched);
        SandataHash.Fold(ref hash, operatorState.WeaponLowered);
        SandataHash.Fold(ref hash, operatorState.WeaponChainPhase);
        SandataHash.Fold(ref hash, operatorState.WeaponChainRemainingTicks);
        SandataHash.Fold(ref hash, operatorState.MagazineRounds);
        SandataHash.Fold(ref hash, operatorState.CyclicFireAccumulator);
        SandataHash.Fold(ref hash, operatorState.SuppressionCounter);

        var contactMemory = operatorState.ContactMemory;
        SandataHash.Fold(ref hash, contactMemory.IsDefault ? 0 : contactMemory.Length);
        if (!contactMemory.IsDefault)
        {
            foreach (var entry in contactMemory)
            {
                SandataHash.Fold(ref hash, unchecked((long)entry.EnemyEntityId));
                SandataHash.Fold(ref hash, entry.LastKnownCellIndex);
                SandataHash.Fold(ref hash, entry.ContactTier);
                SandataHash.Fold(ref hash, entry.LastSeenTick);
            }
        }

        // Task 79c (Sandata's scaffold plan, the wave-12
        // audit's corrected obligation): the operator's loadout, folded last
        // inside this block -- after every field this hasher already
        // covered, including the nested contact-memory entries above. This
        // fold must be unconditional and outside the contact-memory
        // early-exit path above (fixed here: the pre-task-79c method
        // `return`ed from inside the `if (contactMemory.IsDefault)` branch,
        // which would have skipped this fold for any operator whose
        // ContactMemory array is still its default value).
        SandataHash.Fold(ref hash, (long)operatorState.Firearm);
    }
}
