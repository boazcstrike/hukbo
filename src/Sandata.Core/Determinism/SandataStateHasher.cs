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
/// <c>AimAngle</c>, <c>Health</c>, <c>Faction</c>, <c>SquadSlotIndex</c>,
/// <c>Intent</c>, <c>IsCrouched</c>, <c>WeaponLowered</c>,
/// <c>WeaponChainPhase</c>, <c>WeaponChainRemainingTicks</c>,
/// <c>MagazineRounds</c>, <c>CyclicFireAccumulator</c>,
/// <c>SuppressionCounter</c>, then that operator's
/// <c>ContactMemory</c>'s count and, for each entry, in the array's stored
/// order, <c>EnemyEntityId</c>, <c>LastKnownCellIndex</c>, <c>ContactTier</c>,
/// <c>LastSeenTick</c>.
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
/// </list>
/// <para>
/// Every array is folded in the order it is already stored in, never
/// re-sorted here — <see cref="MissionState"/>'s remarks place that ordering
/// obligation on whichever caller builds the state, not on this hasher.
/// Changing this fold order, or any field it folds, is a new preset version
/// with new golden expectations, per design section 4 and <c>CLAUDE.md</c>
/// section 5.
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
        SandataHash.Fold(ref hash, state.NextEntityId);
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
                SandataHash.Fold(ref hash, group.GroupId);
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

        return hash;
    }

    private static void FoldOperator(ref ulong hash, OperatorState operatorState)
    {
        SandataHash.Fold(ref hash, operatorState.EntityId);
        SandataHash.Fold(ref hash, operatorState.PositionX.RawValue);
        SandataHash.Fold(ref hash, operatorState.PositionY.RawValue);
        SandataHash.Fold(ref hash, (long)operatorState.Facing);
        SandataHash.Fold(ref hash, operatorState.AimAngle.Raw);
        SandataHash.Fold(ref hash, operatorState.Health);
        SandataHash.Fold(ref hash, operatorState.Faction);
        SandataHash.Fold(ref hash, operatorState.SquadSlotIndex);
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
        if (contactMemory.IsDefault)
        {
            return;
        }

        foreach (var entry in contactMemory)
        {
            SandataHash.Fold(ref hash, entry.EnemyEntityId);
            SandataHash.Fold(ref hash, entry.LastKnownCellIndex);
            SandataHash.Fold(ref hash, entry.ContactTier);
            SandataHash.Fold(ref hash, entry.LastSeenTick);
        }
    }
}
