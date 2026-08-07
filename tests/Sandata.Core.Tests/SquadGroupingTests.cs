using System.Reflection;
using Sandata.Core.Collision;
using Sandata.Core.Simulation;
using Sandata.Core.Squads;

namespace Sandata.Core.Tests;

/// <summary>
/// Plan task 28: deterministic union-find squad grouping. Design section 8 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Grouping is
/// derived, not stored", is the contract under test — group identity is the
/// minimum entity ID in a component, leadership is the lowest living entity
/// ID, both are recomputed from nothing on every call, and neither is ever
/// written to <see cref="MissionState"/>.
/// </summary>
public sealed class SquadGroupingTests
{
    /// <summary>A chain 1-2-3-4-5 collapses into one component whose identity is the lowest ID, 1.</summary>
    [Fact]
    public void ChainOfPairsProducesOneGroupIdentifiedByTheLowestId()
    {
        ulong[] entityIds = [1, 2, 3, 4, 5];
        var isAlive = new[] { true, true, true, true, true };
        var factions = new[] { 0, 0, 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(3, 4),
            SandataCollisionPair.Create(4, 5),
        };

        var results = Compute(entityIds, isAlive, factions, pairs);

        Assert.All(results, result => Assert.Equal(1UL, result.GroupId));
    }

    /// <summary>A ring (chain plus the closing edge 5-1) is still exactly one component.</summary>
    [Fact]
    public void RingOfPairsProducesOneGroup()
    {
        ulong[] entityIds = [1, 2, 3, 4, 5];
        var isAlive = new[] { true, true, true, true, true };
        var factions = new[] { 0, 0, 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(3, 4),
            SandataCollisionPair.Create(4, 5),
            SandataCollisionPair.Create(1, 5),
        };

        var results = Compute(entityIds, isAlive, factions, pairs);

        Assert.All(results, result => Assert.Equal(1UL, result.GroupId));
    }

    /// <summary>Two disjoint clusters {1,2,3} and {10,11,12} stay two separate groups, each identified by its own lowest ID.</summary>
    [Fact]
    public void TwoDisjointClustersProduceTwoGroups()
    {
        ulong[] entityIds = [1, 2, 3, 10, 11, 12];
        var isAlive = new[] { true, true, true, true, true, true };
        var factions = new[] { 0, 0, 0, 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(10, 11),
            SandataCollisionPair.Create(11, 12),
        };

        var results = Compute(entityIds, isAlive, factions, pairs);

        var expectedGroupIds = new ulong[] { 1, 1, 1, 10, 10, 10 };
        Assert.Equal(expectedGroupIds, results.Select(result => result.GroupId));
    }

    /// <summary>
    /// A candidate pair between two different factions is never unioned, no
    /// matter how it was produced — design section 8's "two operators of the
    /// same faction ... are unioned."
    /// </summary>
    [Fact]
    public void PairsAcrossFactionsAreNeverUnioned()
    {
        ulong[] entityIds = [1, 2];
        var isAlive = new[] { true, true };
        var factions = new[] { 0, 1 };
        var pairs = new[] { SandataCollisionPair.Create(1, 2) };

        var results = Compute(entityIds, isAlive, factions, pairs);

        Assert.Equal(1UL, results[0].GroupId);
        Assert.Equal(2UL, results[1].GroupId);
    }

    /// <summary>
    /// Every permutation of the same pair set produces the identical result
    /// list, because set union is commutative and associative regardless of
    /// application order — the property the collision grid's own pair list
    /// already relies on.
    /// </summary>
    [Fact]
    public void PermutingPairOrderChangesNothing()
    {
        ulong[] entityIds = [1, 2, 3, 4, 5, 6];
        var isAlive = new[] { true, true, true, true, true, true };
        var factions = new[] { 0, 0, 0, 0, 0, 0 };

        var orderOne = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(3, 4),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(5, 6),
        };
        var orderTwo = new[]
        {
            SandataCollisionPair.Create(5, 6),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(3, 4),
        };
        var orderThree = new[]
        {
            SandataCollisionPair.Create(3, 4),
            SandataCollisionPair.Create(5, 6),
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
        };

        var resultsOne = Compute(entityIds, isAlive, factions, orderOne);
        var resultsTwo = Compute(entityIds, isAlive, factions, orderTwo);
        var resultsThree = Compute(entityIds, isAlive, factions, orderThree);

        Assert.Equal(resultsOne, resultsTwo);
        Assert.Equal(resultsOne, resultsThree);
    }

    /// <summary>
    /// Killing the current leader re-derives the leader to the next-lowest
    /// living ID in the very same call that reflects the death — there is no
    /// intermediate leaderless state, because leadership is never carried
    /// forward from a previous call. Group identity stays put at the
    /// original lowest ID, because that ID is still part of the component.
    /// </summary>
    [Fact]
    public void KillingTheLeaderRederivesTheNextLowestLivingIdOnTheSameTick()
    {
        ulong[] entityIds = [1, 2, 3];
        var factions = new[] { 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
        };

        var beforeDeath = Compute(entityIds, [true, true, true], factions, pairs);
        Assert.All(beforeDeath, result => Assert.Equal(1UL, result.GroupId));
        Assert.All(beforeDeath, result => Assert.Equal(1UL, result.LeaderEntityId));

        // Entity 1 dies. The same roster and the same pair list are given to
        // a fresh call — nothing about the previous call's leader is reused.
        var afterDeath = Compute(entityIds, [false, true, true], factions, pairs);

        Assert.All(afterDeath, result => Assert.Equal(1UL, result.GroupId));
        Assert.All(afterDeath, result => Assert.Equal(2UL, result.LeaderEntityId));
    }

    /// <summary>A component with no living member at all has no leader.</summary>
    [Fact]
    public void AllDeadComponentHasNoLeader()
    {
        ulong[] entityIds = [1, 2];
        var factions = new[] { 0, 0 };
        var pairs = new[] { SandataCollisionPair.Create(1, 2) };

        var results = Compute(entityIds, [false, false], factions, pairs);

        Assert.All(results, result => Assert.Null(result.LeaderEntityId));
    }

    /// <summary>Slot indices are assigned by ascending entity ID, zero-based, counting only living members.</summary>
    [Fact]
    public void SlotIndicesAreAssignedByAscendingEntityIdAmongLivingMembers()
    {
        ulong[] entityIds = [1, 2, 3, 4];
        var isAlive = new[] { true, true, true, true };
        var factions = new[] { 0, 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(3, 4),
        };

        var results = Compute(entityIds, isAlive, factions, pairs);

        var expectedSlotIndices = new int?[] { 0, 1, 2, 3 };
        Assert.Equal(expectedSlotIndices, results.Select(result => result.SlotIndex));
    }

    /// <summary>
    /// A dead member holds no slot, and the remaining living members'
    /// indices close the gap immediately — design section 8's "a death
    /// re-packs the slots deterministically on the next tick", proven here
    /// within one call rather than across two ticks of a running mission.
    /// </summary>
    [Fact]
    public void ADeadMemberHoldsNoSlotAndRemainingSlotsArePacked()
    {
        ulong[] entityIds = [1, 2, 3, 4];
        var isAlive = new[] { true, false, true, true };
        var factions = new[] { 0, 0, 0, 0 };
        var pairs = new[]
        {
            SandataCollisionPair.Create(1, 2),
            SandataCollisionPair.Create(2, 3),
            SandataCollisionPair.Create(3, 4),
        };

        var results = Compute(entityIds, isAlive, factions, pairs);

        Assert.Equal(0, results[0].SlotIndex);
        Assert.Null(results[1].SlotIndex);
        Assert.Equal(1, results[2].SlotIndex);
        Assert.Equal(2, results[3].SlotIndex);
    }

    /// <summary>An operator with no candidate pair at all is its own singleton group and, if alive, its own leader.</summary>
    [Fact]
    public void AnUnpairedOperatorIsItsOwnSingletonGroup()
    {
        ulong[] entityIds = [7];
        var isAlive = new[] { true };
        var factions = new[] { 0 };
        var pairs = Array.Empty<SandataCollisionPair>();

        var results = Compute(entityIds, isAlive, factions, pairs);

        Assert.Equal(7UL, results[0].GroupId);
        Assert.Equal(7UL, results[0].LeaderEntityId);
        Assert.Equal(0, results[0].SlotIndex);
    }

    /// <summary>A pair naming an entity ID absent from the roster is a caller defect, not a silently ignored input.</summary>
    [Fact]
    public void APairNamingAnUnknownEntityIdThrows()
    {
        ulong[] entityIds = [1, 2];
        var isAlive = new[] { true, true };
        var factions = new[] { 0, 0 };
        var pairs = new[] { SandataCollisionPair.Create(2, 99) };

        Assert.Throws<ArgumentException>(() => Compute(entityIds, isAlive, factions, pairs));
    }

    /// <summary>entityIds must already be strictly ascending; the derivation relies on that order to find each component's minimum.</summary>
    [Fact]
    public void NonAscendingEntityIdsThrows()
    {
        ulong[] entityIds = [2, 1];
        var isAlive = new[] { true, true };
        var factions = new[] { 0, 0 };

        Assert.Throws<ArgumentException>(
            () => Compute(entityIds, isAlive, factions, Array.Empty<SandataCollisionPair>()));
    }

    /// <summary>A parallel array of the wrong length is a caller defect.</summary>
    [Fact]
    public void MismatchedParallelArrayLengthThrows()
    {
        ulong[] entityIds = [1, 2];
        var isAlive = new[] { true };
        var factions = new[] { 0, 0 };

        Assert.Throws<ArgumentException>(
            () => Compute(entityIds, isAlive, factions, Array.Empty<SandataCollisionPair>()));
    }

    /// <summary>
    /// Reflection over <see cref="MissionState"/>'s whole reachable property
    /// graph finds no field named for group leadership. Design section 8 is
    /// explicit that a leader is never stored; unlike a group's identity —
    /// which legitimately doubles as the stable key on the pre-existing,
    /// authoritative <c>GroupPathState.GroupId</c> path-request record — a
    /// leader has no legitimate reason to appear anywhere in
    /// <see cref="MissionState"/> at all.
    /// </summary>
    [Fact]
    public void MissionStateReachableGraphStoresNoLeaderField()
    {
        var offendingMembers = new List<string>();
        var visitedTypes = new HashSet<Type>();
        CollectLeaderNamedMembers(typeof(MissionState), visitedTypes, offendingMembers);

        Assert.Empty(offendingMembers);
    }

    /// <summary>
    /// Reflection over <see cref="MissionState"/>'s whole reachable property
    /// graph finds no field that stores squad membership as a collection
    /// (for example, a list of the entity IDs belonging to a group). Squad
    /// membership is recomputed from positions and entity IDs by
    /// <see cref="SquadGrouping.Compute"/> on every call, never persisted.
    /// </summary>
    [Fact]
    public void MissionStateReachableGraphStoresNoMembershipCollection()
    {
        var offendingMembers = new List<string>();
        var visitedTypes = new HashSet<Type>();
        CollectMembershipNamedMembers(typeof(MissionState), visitedTypes, offendingMembers);

        Assert.Empty(offendingMembers);
    }

    /// <summary>
    /// Neither <see cref="SquadGrouping"/> nor <see cref="UnionFind"/> nor
    /// <see cref="SquadSlot"/> declares a mutable static field. A pure
    /// derivation function that cached anything across calls in a static
    /// field would defeat "neither stored" just as surely as writing it to
    /// <see cref="MissionState"/> would, only less visibly.
    /// </summary>
    [Fact]
    public void SquadTypesDeclareNoMutableStaticState()
    {
        Type[] squadTypes = [typeof(SquadGrouping), typeof(UnionFind), typeof(SquadSlot)];
        var offenders = new List<string>();

        foreach (var type in squadTypes)
        {
            foreach (var field in type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    offenders.Add($"{type.FullName}.{field.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static void CollectLeaderNamedMembers(
        Type type, HashSet<Type> visitedTypes, List<string> offendingMembers)
    {
        if (!visitedTypes.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name.Contains("Leader", StringComparison.OrdinalIgnoreCase))
            {
                offendingMembers.Add($"{type.FullName}.{property.Name}");
            }

            VisitElementType(property.PropertyType, visitedTypes, offendingMembers, CollectLeaderNamedMembers);
        }
    }

    private static void CollectMembershipNamedMembers(
        Type type, HashSet<Type> visitedTypes, List<string> offendingMembers)
    {
        if (!visitedTypes.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var isCollectionMemberField =
                property.Name.Contains("Member", StringComparison.OrdinalIgnoreCase) &&
                property.PropertyType != typeof(string);

            if (isCollectionMemberField)
            {
                offendingMembers.Add($"{type.FullName}.{property.Name}");
            }

            VisitElementType(property.PropertyType, visitedTypes, offendingMembers, CollectMembershipNamedMembers);
        }
    }

    /// <summary>
    /// Walks into the element type of a collection property (for example
    /// <c>ImmutableArray&lt;OperatorState&gt;</c> to <c>OperatorState</c>) or
    /// into a plain reference/record type declared by this project, so the
    /// scan reaches every record <see cref="MissionState"/> actually holds
    /// rather than stopping at its own four properties.
    /// </summary>
    private static void VisitElementType(
        Type propertyType,
        HashSet<Type> visitedTypes,
        List<string> offendingMembers,
        Action<Type, HashSet<Type>, List<string>> visit)
    {
        if (propertyType.IsGenericType)
        {
            foreach (var argument in propertyType.GetGenericArguments())
            {
                VisitElementType(argument, visitedTypes, offendingMembers, visit);
            }

            return;
        }

        if (propertyType.Namespace is not null &&
            propertyType.Namespace.StartsWith("Sandata.Core", StringComparison.Ordinal))
        {
            visit(propertyType, visitedTypes, offendingMembers);
        }
    }

    private static SquadSlot[] Compute(
        ReadOnlySpan<ulong> entityIds,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> factions,
        IReadOnlyList<SandataCollisionPair> pairs)
    {
        var results = new SquadSlot[entityIds.Length];
        SquadGrouping.Compute(entityIds, isAlive, factions, pairs, results);
        return results;
    }
}
