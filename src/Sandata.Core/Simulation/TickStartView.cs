using System.Collections.Immutable;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Simulation;

/// <summary>
/// The frozen, read-only snapshot of the living-operator roster that stages 5
/// through 9 of <see cref="SandataSimulation.RunTick"/> read, and only those
/// stages — see <see cref="TickStage"/>'s own remarks for the two binding
/// rules this type exists to enforce. Captured once, by
/// <see cref="SandataSimulation"/>, from the <c>MissionState</c> that
/// <see cref="TickStage.OrderApplication"/> and
/// <see cref="TickStage.SpawnAndDespawn"/> left behind, and released exactly
/// once, between stage 9 and stage 10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a snapshot rather than a live reference into <c>MissionState</c>.</b>
/// <c>MissionState</c> is an immutable record, so a stage that held a plain
/// reference to it could never observe a write from a later stage even without
/// this type existing at all. This type exists for two reasons a plain
/// reference cannot satisfy: it pre-converts every position into both the raw
/// fixed-point domain <see cref="Squads.SquadGrouping.Compute"/> and
/// <see cref="Collision.SandataCollisionGrid"/> need and the world-unit domain
/// <see cref="Navigation.NavGrid"/>, <see cref="Navigation.WallBuckets"/>, and
/// every rule built on them need, so no stage-5-through-9 caller repeats that
/// conversion; and it carries this tick's collision broad-phase candidate
/// pairs (<see cref="Pairs"/>), computed once from the same frozen positions,
/// so stage 6 never has to rebuild the broad phase itself. Enforcing "stages
/// 10 through 14 may never read this data" is the other half of the reason:
/// <see cref="Release"/> makes every accessor below throw
/// <see cref="InvalidOperationException"/> once called, so a stage-10-or-later
/// caller that reaches for a frozen fact fails loudly at the call site rather
/// than silently reading stale data a later stage has already superseded.
/// </para>
/// <para>
/// Every array-shaped accessor below is parallel and index-aligned to
/// <see cref="EntityIds"/>, which is ascending with no duplicate — the order
/// <c>MissionState.Operators</c> is documented to hold. <see cref="IndexOf"/>
/// is the one caller-facing way to turn an entity id into that shared index;
/// nothing in this type uses a <c>Dictionary&lt;&gt;</c> or a
/// <c>HashSet&lt;&gt;</c>, per <c>CLAUDE.md</c> section 5's ban on both inside
/// <c>Sandata.Core</c> — membership and lookup are both a binary search over
/// the already-ascending <see cref="EntityIds"/> array.
/// </para>
/// </remarks>
internal sealed class TickStartView
{
    private readonly ImmutableArray<ulong> _entityId;
    private readonly ImmutableArray<bool> _isAlive;
    private readonly ImmutableArray<int> _faction;
    private readonly ImmutableArray<int> _positionXRaw;
    private readonly ImmutableArray<int> _positionYRaw;
    private readonly ImmutableArray<long> _positionXWu;
    private readonly ImmutableArray<long> _positionYWu;
    private readonly ImmutableArray<Facing16> _facing;
    private readonly ImmutableArray<Bam16> _aimAngle;
    private readonly ImmutableArray<int> _health;
    private readonly ImmutableArray<int> _suppressionCounter;
    private readonly ImmutableArray<ImmutableArray<ContactMemoryEntry>> _contactMemory;
    private readonly IReadOnlyList<SandataCollisionPair> _pairs;
    private bool _released;

    /// <summary>
    /// Captures one frozen snapshot from <paramref name="state"/>'s current
    /// operator roster and <paramref name="pairs"/>, this tick's collision
    /// broad-phase candidate list already computed against those same
    /// positions.
    /// </summary>
    internal TickStartView(MissionState state, IReadOnlyList<SandataCollisionPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pairs);

        var operators = state.Operators;
        var count = operators.IsDefaultOrEmpty ? 0 : operators.Length;

        var entityId = ImmutableArray.CreateBuilder<ulong>(count);
        var isAlive = ImmutableArray.CreateBuilder<bool>(count);
        var faction = ImmutableArray.CreateBuilder<int>(count);
        var positionXRaw = ImmutableArray.CreateBuilder<int>(count);
        var positionYRaw = ImmutableArray.CreateBuilder<int>(count);
        var positionXWu = ImmutableArray.CreateBuilder<long>(count);
        var positionYWu = ImmutableArray.CreateBuilder<long>(count);
        var facing = ImmutableArray.CreateBuilder<Facing16>(count);
        var aimAngle = ImmutableArray.CreateBuilder<Bam16>(count);
        var health = ImmutableArray.CreateBuilder<int>(count);
        var suppressionCounter = ImmutableArray.CreateBuilder<int>(count);
        var contactMemory = ImmutableArray.CreateBuilder<ImmutableArray<ContactMemoryEntry>>(count);

        for (var index = 0; index < count; index++)
        {
            var operatorState = operators[index];

            entityId.Add(operatorState.EntityId);
            isAlive.Add(operatorState.Health > 0);
            faction.Add(operatorState.Faction);
            positionXRaw.Add(operatorState.PositionX.RawValue);
            positionYRaw.Add(operatorState.PositionY.RawValue);
            positionXWu.Add(WorldUnits.FromFixedPoint(operatorState.PositionX));
            positionYWu.Add(WorldUnits.FromFixedPoint(operatorState.PositionY));
            facing.Add(operatorState.Facing);
            aimAngle.Add(operatorState.AimAngle);
            health.Add(operatorState.Health);
            suppressionCounter.Add(operatorState.SuppressionCounter);
            contactMemory.Add(operatorState.ContactMemory);
        }

        _entityId = entityId.MoveToImmutable();
        _isAlive = isAlive.MoveToImmutable();
        _faction = faction.MoveToImmutable();
        _positionXRaw = positionXRaw.MoveToImmutable();
        _positionYRaw = positionYRaw.MoveToImmutable();
        _positionXWu = positionXWu.MoveToImmutable();
        _positionYWu = positionYWu.MoveToImmutable();
        _facing = facing.MoveToImmutable();
        _aimAngle = aimAngle.MoveToImmutable();
        _health = health.MoveToImmutable();
        _suppressionCounter = suppressionCounter.MoveToImmutable();
        _contactMemory = contactMemory.MoveToImmutable();
        _pairs = pairs;
    }

    /// <summary>How many living-and-dead operators this snapshot holds — the whole roster, not only the living.</summary>
    internal int Count
    {
        get
        {
            EnsureNotReleased();
            return _entityId.Length;
        }
    }

    /// <summary>Every operator id this snapshot holds, ascending with no duplicate.</summary>
    internal ReadOnlySpan<ulong> EntityIds
    {
        get
        {
            EnsureNotReleased();
            return _entityId.AsSpan();
        }
    }

    /// <summary>
    /// This tick's collision broad-phase candidate pairs, computed once from
    /// the same positions this snapshot froze. Not itself index-aligned to
    /// <see cref="EntityIds"/> — see <see cref="Collision.SandataCollisionPair"/>.
    /// </summary>
    internal IReadOnlyList<SandataCollisionPair> Pairs
    {
        get
        {
            EnsureNotReleased();
            return _pairs;
        }
    }

    /// <summary>
    /// Locates <paramref name="entityId"/> within <see cref="EntityIds"/> by
    /// binary search, or returns a negative value when it is absent — the one
    /// caller-facing way to turn an entity id into the shared index every
    /// other accessor below takes.
    /// </summary>
    internal int IndexOf(ulong entityId)
    {
        EnsureNotReleased();
        return _entityId.AsSpan().BinarySearch(entityId);
    }

    internal bool IsAlive(int index)
    {
        EnsureNotReleased();
        return _isAlive[index];
    }

    internal int Faction(int index)
    {
        EnsureNotReleased();
        return _faction[index];
    }

    /// <summary>That operator's X position, raw fixed-point units — the domain <see cref="Squads.SquadGrouping.Compute"/> and <see cref="Collision.SandataCollisionGrid"/> take.</summary>
    internal int PositionXRaw(int index)
    {
        EnsureNotReleased();
        return _positionXRaw[index];
    }

    /// <summary>That operator's Y position, raw fixed-point units. See <see cref="PositionXRaw"/>.</summary>
    internal int PositionYRaw(int index)
    {
        EnsureNotReleased();
        return _positionYRaw[index];
    }

    /// <summary>That operator's X position, whole world units — the domain <see cref="Navigation.NavGrid"/>, <see cref="Navigation.WallBuckets"/>, and the rules built on them take.</summary>
    internal long PositionXWu(int index)
    {
        EnsureNotReleased();
        return _positionXWu[index];
    }

    /// <summary>That operator's Y position, whole world units. See <see cref="PositionXWu"/>.</summary>
    internal long PositionYWu(int index)
    {
        EnsureNotReleased();
        return _positionYWu[index];
    }

    internal Facing16 Facing(int index)
    {
        EnsureNotReleased();
        return _facing[index];
    }

    internal Bam16 AimAngle(int index)
    {
        EnsureNotReleased();
        return _aimAngle[index];
    }

    internal int Health(int index)
    {
        EnsureNotReleased();
        return _health[index];
    }

    internal int SuppressionCounter(int index)
    {
        EnsureNotReleased();
        return _suppressionCounter[index];
    }

    /// <summary>That operator's contact memory as of the start of this tick, before stage 5 updates it.</summary>
    internal ImmutableArray<ContactMemoryEntry> ContactMemory(int index)
    {
        EnsureNotReleased();
        return _contactMemory[index];
    }

    /// <summary>
    /// Releases this snapshot. Called exactly once, by
    /// <see cref="SandataSimulation.RunTick"/>, between stage 9 and stage 10.
    /// Every accessor above throws <see cref="InvalidOperationException"/>
    /// after this call — see <see cref="TickStage"/>'s own remarks for why.
    /// </summary>
    internal void Release()
    {
        _released = true;
    }

    private void EnsureNotReleased()
    {
        if (_released)
        {
            throw new InvalidOperationException(
                "TickStartView was released after stage 9 and may not be read by stage 10 or later.");
        }
    }
}
