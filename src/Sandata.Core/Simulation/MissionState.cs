using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Simulation;

/// <summary>
/// One remembered enemy on an <see cref="OperatorState"/>'s contact memory —
/// design section 4's "for each remembered enemy, the last known cell, the
/// contact tier, and the tick it was last seen". <see cref="EnemyEntityId"/>
/// is <see langword="ulong"/> to match <c>Hukbo.Core.Simulation.AgentState.EntityId</c>
/// and <see cref="OperatorState.EntityId"/> below — it names an operator, so
/// it shares that type (task 64's 2026-08-07 identifier-widening pass).
/// <see cref="ContactTier"/> is a raw backing value for the not-yet-declared
/// <c>ContactTier</c> enum that task 35 (contact memory, alert levels,
/// hearing rules) owns; <see cref="LastKnownCellIndex"/> is a raw nav-grid
/// cell index — <c>int</c>, matching <c>NavGrid</c>'s own <c>nodeIndex</c>
/// type, not an entity or group identifier — for the same reason task 10's
/// nav grid is not visible from this file.
/// </summary>
public readonly record struct ContactMemoryEntry(
    ulong EnemyEntityId,
    int LastKnownCellIndex,
    int ContactTier,
    long LastSeenTick);

/// <summary>
/// One faction's alert state — design section 4's "per faction: alert level,
/// one of Calm, Raised, Breach." <see cref="FactionId"/> stays
/// <see langword="int"/>: it is not an entity or group identifier but a
/// two-valued faction selector, matching <c>OperatorState.Faction</c> below
/// and <c>Mission.MissionFactionSetup.FactionId</c>, both already
/// <see langword="int"/> and both constrained to exactly <c>0</c> or
/// <c>1</c> (task 64's 2026-08-07 identifier-widening pass left this field
/// alone on purpose). <see cref="AlertLevel"/> is a raw backing
/// value for the not-yet-declared <c>AlertLevel</c> enum task 35 owns; by
/// that design line's own listed order, <c>0</c> is <c>Calm</c>, <c>1</c> is
/// <c>Raised</c>, and <c>2</c> is <c>Breach</c>, but that numbering is task
/// 35's to confirm, not this file's to enforce.
/// </summary>
public readonly record struct FactionAlertState(int FactionId, int AlertLevel);

/// <summary>
/// One door's authoritative state — design section 4's "per door: open or
/// closed, and the tick it last changed." <see cref="DoorId"/> is an
/// implementer addition: design names no door identifier, and one is needed
/// to give this collection a stable, total order (parallel to
/// <c>Hukbo.Core.Simulation.AgentState.EntityId</c> in role, not in type).
/// It stays <see langword="int"/> under task 64's 2026-08-07
/// identifier-widening pass: a door is not an operator and never appears in
/// <c>SandataCollisionBody</c>, <c>SandataCollisionPair</c>, or a squad's
/// derived group id, so nothing requires it to share the operator entity-id
/// space or that space's <see langword="ulong"/> range. Whichever future
/// task authors real doors from the map format should confirm this
/// identifier against its own.
/// </summary>
public readonly record struct DoorState(int DoorId, bool IsOpen, long LastChangedTick);

/// <summary>
/// One group's path state — design section 4's "per group: the destination
/// record and the outstanding path request
/// <c>(groupId, startCellIndex, goalCellIndex, requestTick)</c>."
/// <see cref="GroupId"/> is <see langword="ulong"/>: design section 8 defines
/// group identity as "the minimum entity id in the component", so it holds
/// an <see cref="OperatorState.EntityId"/> value and must share that type
/// (task 64's 2026-08-07 identifier-widening pass).
/// <see cref="HasOutstandingRequest"/> is an implementer addition: the design
/// line calls the request "outstanding", implying it need not always exist,
/// and a raw <see cref="ImmutableArray{T}"/> cannot express "absent" without
/// either a nullable struct or a presence flag. <see cref="DestinationCellIndex"/>,
/// <see cref="StartCellIndex"/>, and <see cref="GoalCellIndex"/> stay
/// <see langword="int"/>: they are <c>NavGrid</c> cell indices, not entity or
/// group identifiers, and <c>NavGrid</c>'s own <c>nodeIndex</c> is
/// <see langword="int"/>. <see cref="DestinationCellIndex"/> stands in for
/// the richer destination record task 27 (path service) and task 28 (squad
/// grouping) will own.
/// </summary>
public readonly record struct GroupPathState(
    ulong GroupId,
    int DestinationCellIndex,
    bool HasOutstandingRequest,
    int StartCellIndex,
    int GoalCellIndex,
    long RequestTick);

/// <summary>
/// One RNG stream's state — design section 4's "per RNG stream: algorithm
/// identifier, root seed, and stream state." <see cref="StreamId"/> is an
/// implementer addition standing in for design's own <c>SystemTag</c>
/// concept (<c>Accuracy</c>, <c>Reaction</c>, <c>Sidestep</c>,
/// <c>SpawnJitter</c>), which is not yet a declared type in this worktree.
/// Neither <see cref="StreamId"/> nor <see cref="AlgorithmId"/> is an entity
/// or group identifier, so task 64's 2026-08-07 identifier-widening pass
/// left both <see langword="int"/>. <see cref="AlgorithmId"/> is a raw
/// backing value; <c>SplitMix64</c> is the only algorithm Sandata defines
/// today, but the identifier still folds into the hash so a future second
/// algorithm is a hash-visible change rather than a silent one.
/// </summary>
public readonly record struct RngStreamState(
    int StreamId,
    int AlgorithmId,
    ulong RootSeed,
    ulong StreamState);

/// <summary>
/// One operator's authoritative, per-tick state. Every field here is on
/// design section 4's authoritative list, in the order that section states
/// them, plus <see cref="EntityId"/>, an implementer addition needed to give
/// <see cref="MissionState.Operators"/> a stable total order — the same role
/// <c>Hukbo.Core.Simulation.AgentState.EntityId</c> plays for
/// <c>StateHasher</c>. <see cref="EntityId"/> is <see langword="ulong"/> to
/// match that Hukbo type and Sandata's own <c>SandataCollisionBody</c>,
/// <c>SandataCollisionPair</c>, and <c>SandataCollisionMoveRequest</c>, which
/// already use <see langword="ulong"/> for entity and group ids — this record
/// was the outlier and is corrected by task 64's 2026-08-07
/// identifier-widening pass. <see cref="Faction"/> stays
/// <see langword="int"/>: it is a two-valued faction selector, not an entity
/// or group identifier — see <see cref="FactionAlertState"/>'s remarks.
/// <b>Design section 4's originally-listed <c>SquadSlotIndex</c> field is
/// removed</b>: design section 8 states plainly that group id, leader,
/// membership, and slot index are all derived each tick from positions and
/// entity ids, and stores nothing per group, so nothing may be stored here
/// either. Task 64's 2026-08-07 correction to design section 4 resolves the
/// contradiction in the design's favour of section 8; <c>SquadGrouping.Compute</c>
/// (task 28) derives the slot every tick instead.
/// </summary>
/// <remarks>
/// <see cref="Intent"/> is a raw backing value for the not-yet-declared
/// <c>OperatorIntent</c> enum task 44 owns. <see cref="WeaponChainPhase"/> is
/// a raw backing value for the not-yet-declared <c>WeaponChainPhase</c> enum
/// task 23 owns. <see cref="IsCrouched"/> is design's "posture (standing or
/// crouched)" — a plain <see cref="bool"/> rather than a new two-member enum,
/// since a binary posture needs no enum and no future task's file list
/// claims a <c>Posture</c> type.
/// <para>
/// This type holds <see cref="ContactMemory"/>, an <see cref="ImmutableArray{T}"/>,
/// so — exactly like <c>Hukbo.Core.Simulation.Scenario</c> — it must override
/// <c>Equals</c>/<c>GetHashCode</c> rather than rely on the record default,
/// which would compare the backing array by reference.
/// </para>
/// </remarks>
public sealed record OperatorState(
    ulong EntityId,
    FixedPoint PositionX,
    FixedPoint PositionY,
    Facing16 Facing,
    Bam16 AimAngle,
    int Health,
    int Faction,
    int Intent,
    bool IsCrouched,
    bool WeaponLowered,
    int WeaponChainPhase,
    int WeaponChainRemainingTicks,
    int MagazineRounds,
    int CyclicFireAccumulator,
    int SuppressionCounter)
{
    /// <summary>
    /// Every remembered enemy, in the order the owning state chooses to
    /// store them. This type does not itself enforce an order; the caller
    /// that builds a <see cref="MissionState"/> is responsible for a total,
    /// deterministic order (by convention, ascending
    /// <see cref="ContactMemoryEntry.EnemyEntityId"/>), the same way
    /// <c>BattleSimulation</c>, not <c>AgentState</c>, owns
    /// <c>Hukbo.Core.Simulation.AgentState</c>'s ordering.
    /// </summary>
    public ImmutableArray<ContactMemoryEntry> ContactMemory { get; init; } =
        ImmutableArray<ContactMemoryEntry>.Empty;

    public bool Equals(OperatorState? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EntityId == other.EntityId &&
            PositionX == other.PositionX &&
            PositionY == other.PositionY &&
            Facing == other.Facing &&
            AimAngle == other.AimAngle &&
            Health == other.Health &&
            Faction == other.Faction &&
            Intent == other.Intent &&
            IsCrouched == other.IsCrouched &&
            WeaponLowered == other.WeaponLowered &&
            WeaponChainPhase == other.WeaponChainPhase &&
            WeaponChainRemainingTicks == other.WeaponChainRemainingTicks &&
            MagazineRounds == other.MagazineRounds &&
            CyclicFireAccumulator == other.CyclicFireAccumulator &&
            SuppressionCounter == other.SuppressionCounter &&
            ContactMemorySpan.SequenceEqual(other.ContactMemorySpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EntityId);
        hash.Add(PositionX);
        hash.Add(PositionY);
        hash.Add(Facing);
        hash.Add(AimAngle);
        hash.Add(Health);
        hash.Add(Faction);
        hash.Add(Intent);
        hash.Add(IsCrouched);
        hash.Add(WeaponLowered);
        hash.Add(WeaponChainPhase);
        hash.Add(WeaponChainRemainingTicks);
        hash.Add(MagazineRounds);
        hash.Add(CyclicFireAccumulator);
        hash.Add(SuppressionCounter);
        foreach (var entry in ContactMemorySpan)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<ContactMemoryEntry> ContactMemorySpan =>
        ContactMemory.IsDefault ? ReadOnlySpan<ContactMemoryEntry>.Empty : ContactMemory.AsSpan();
}

/// <summary>
/// Sandata's mutable, tick-varying mission state — the authoritative content
/// design section 4 lists as hashed and snapshotted, held live during a
/// running mission. <see cref="Determinism.SandataStateHasher"/> folds this
/// type together with the <see cref="Mission"/> it belongs to and the
/// <see cref="Rules.SandataRuleset"/> it runs under;
/// <see cref="Simulation.MissionSnapshot"/> is this type's save/resume DTO.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Phase"/> and <see cref="Winner"/> are raw backing values: design
/// section 4 lists them ("Tick, Phase, Winner, NextEntityId,
/// NextEventSequence") without pinning a concrete type, and no future task's
/// file list in the plan claims a mission-phase or winner enum for this file.
/// <see cref="Winner"/> follows the convention design's own outcome language
/// implies and <c>Hukbo.Core.Simulation.BattleOutcome</c> already establishes:
/// <c>-1</c> means no winner decided yet, and <c>0</c>/<c>1</c> name a
/// faction.
/// </para>
/// <para>
/// Every collection below holds a plain <see cref="ImmutableArray{T}"/> whose
/// order this type does not enforce; the owning caller is responsible for a
/// deterministic order — ascending by the natural id named in each element
/// type's own remarks. Nothing here validates that responsibility, mirroring
/// how <c>Hukbo.Core.Simulation.AgentState</c> is a plain data holder and
/// <c>BattleSimulation</c>, not the state type, owns ordering.
/// </para>
/// <para>
/// <see cref="NextEntityId"/> is <see langword="ulong"/>, matching
/// <see cref="OperatorState.EntityId"/> (task 64's 2026-08-07
/// identifier-widening pass): it is the counter that assigns the next
/// entity id, so it must be able to hold every value that id can, not merely
/// widen from it. <see cref="Phase"/> and <see cref="Winner"/> are not
/// identifiers and stay <see langword="int"/>, per this type's own remarks
/// below.
/// </para>
/// <para>
/// This type holds five <see cref="ImmutableArray{T}"/> collections, so —
/// exactly like <c>Hukbo.Core.Simulation.Scenario</c> and
/// <see cref="OperatorState"/> above — it overrides
/// <c>Equals</c>/<c>GetHashCode</c> rather than relying on the record
/// default.
/// </para>
/// </remarks>
public sealed record MissionState(
    long Tick,
    int Phase,
    int Winner,
    ulong NextEntityId,
    long NextEventSequence)
{
    /// <summary>Ordered ascending by <see cref="OperatorState.EntityId"/>.</summary>
    public ImmutableArray<OperatorState> Operators { get; init; } =
        ImmutableArray<OperatorState>.Empty;

    /// <summary>Ordered ascending by <see cref="FactionAlertState.FactionId"/>.</summary>
    public ImmutableArray<FactionAlertState> FactionAlerts { get; init; } =
        ImmutableArray<FactionAlertState>.Empty;

    /// <summary>Ordered ascending by <see cref="DoorState.DoorId"/>.</summary>
    public ImmutableArray<DoorState> Doors { get; init; } =
        ImmutableArray<DoorState>.Empty;

    /// <summary>Ordered ascending by <see cref="GroupPathState.GroupId"/>.</summary>
    public ImmutableArray<GroupPathState> Groups { get; init; } =
        ImmutableArray<GroupPathState>.Empty;

    /// <summary>Ordered ascending by <see cref="RngStreamState.StreamId"/>.</summary>
    public ImmutableArray<RngStreamState> RngStreams { get; init; } =
        ImmutableArray<RngStreamState>.Empty;

    public bool Equals(MissionState? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Tick == other.Tick &&
            Phase == other.Phase &&
            Winner == other.Winner &&
            NextEntityId == other.NextEntityId &&
            NextEventSequence == other.NextEventSequence &&
            OperatorsSpan.SequenceEqual(other.OperatorsSpan) &&
            FactionAlertsSpan.SequenceEqual(other.FactionAlertsSpan) &&
            DoorsSpan.SequenceEqual(other.DoorsSpan) &&
            GroupsSpan.SequenceEqual(other.GroupsSpan) &&
            RngStreamsSpan.SequenceEqual(other.RngStreamsSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Tick);
        hash.Add(Phase);
        hash.Add(Winner);
        hash.Add(NextEntityId);
        hash.Add(NextEventSequence);
        foreach (var operatorState in OperatorsSpan)
        {
            hash.Add(operatorState);
        }

        foreach (var factionAlert in FactionAlertsSpan)
        {
            hash.Add(factionAlert);
        }

        foreach (var door in DoorsSpan)
        {
            hash.Add(door);
        }

        foreach (var group in GroupsSpan)
        {
            hash.Add(group);
        }

        foreach (var rngStream in RngStreamsSpan)
        {
            hash.Add(rngStream);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<OperatorState> OperatorsSpan =>
        Operators.IsDefault ? ReadOnlySpan<OperatorState>.Empty : Operators.AsSpan();

    private ReadOnlySpan<FactionAlertState> FactionAlertsSpan =>
        FactionAlerts.IsDefault ? ReadOnlySpan<FactionAlertState>.Empty : FactionAlerts.AsSpan();

    private ReadOnlySpan<DoorState> DoorsSpan =>
        Doors.IsDefault ? ReadOnlySpan<DoorState>.Empty : Doors.AsSpan();

    private ReadOnlySpan<GroupPathState> GroupsSpan =>
        Groups.IsDefault ? ReadOnlySpan<GroupPathState>.Empty : Groups.AsSpan();

    private ReadOnlySpan<RngStreamState> RngStreamsSpan =>
        RngStreams.IsDefault ? ReadOnlySpan<RngStreamState>.Empty : RngStreams.AsSpan();
}
