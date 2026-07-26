using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Equivalence tests for the uniform grid broad phase. The grid is only ever
/// allowed to be a faster way of computing what
/// <see cref="NaiveCollisionPairs.Enumerate"/> already computes, so almost every
/// test here asserts exact equality against that oracle rather than against a
/// hand-written expectation. A hand-written expectation would encode the same
/// misunderstanding twice if the author got the geometry wrong.
/// </summary>
public sealed class CollisionUniformGridTests
{
    private const int BodyRadiusRaw = 4 * FixedPoint.Scale;

    private const int DiameterRaw = 2 * BodyRadiusRaw;

    private const int MaximumCoordinateRaw =
        Scenario.MaximumMapDimension * FixedPoint.Scale;

    [Fact]
    public void Constructor_ExposesTheCellSizeItWasGiven()
    {
        Assert.Equal(DiameterRaw, new CollisionUniformGrid(DiameterRaw).CellSizeRaw);
        Assert.Equal(2, new CollisionUniformGrid(2).CellSizeRaw);
    }

    [Fact]
    public void Constructor_RejectsANonPositiveCellSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CollisionUniformGrid(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CollisionUniformGrid(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollisionUniformGrid(int.MinValue));
    }

    [Fact]
    public void Rebuild_RejectsANullBodyList()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.Throws<ArgumentNullException>(() => grid.Rebuild(null!, BodyRadiusRaw));
    }

    [Fact]
    public void Rebuild_RejectsACellSmallerThanTheBodyDiameter()
    {
        var grid = new CollisionUniformGrid(DiameterRaw - 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => grid.Rebuild([], BodyRadiusRaw));
    }

    [Fact]
    public void Rebuild_AcceptsACellExactlyOneBodyDiameterWide()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild([Living(1UL, 0, 0), Living(2UL, DiameterRaw, 0)], BodyRadiusRaw);

        Assert.Equal([CollisionPair.Create(1UL, 2UL)], grid.Pairs);
    }

    [Fact]
    public void Rebuild_RejectsANegativeBodyRadius()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Rebuild([], -1));
    }

    [Fact]
    public void Rebuild_RejectsNegativeCoordinatesOnEitherAxis()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => grid.Rebuild([Living(1UL, -1, 0)], BodyRadiusRaw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => grid.Rebuild([Living(1UL, 0, -1)], BodyRadiusRaw));
    }

    [Fact]
    public void Rebuild_ReturnsNoPairsForFewerThanTwoLivingBodies()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild([], BodyRadiusRaw);
        Assert.Empty(grid.Pairs);

        grid.Rebuild([Living(1UL, 0, 0)], BodyRadiusRaw);
        Assert.Empty(grid.Pairs);

        grid.Rebuild([Living(1UL, 0, 0), Dead(2UL, 0, 0)], BodyRadiusRaw);
        Assert.Empty(grid.Pairs);
    }

    [Fact]
    public void Rebuild_MatchesTheOracleForBodiesAtTheOrigin()
    {
        AssertMatchesOracle(
            DiameterRaw,
            [
                Living(3UL, 0, 0),
                Living(1UL, 0, 0),
                Living(2UL, DiameterRaw, 0),
                Living(4UL, 0, DiameterRaw),
                Living(5UL, DiameterRaw + 1, DiameterRaw + 1),
            ]);
    }

    [Fact]
    public void Rebuild_MatchesTheOracleAtTheMaximumValidatedCoordinate()
    {
        AssertMatchesOracle(
            DiameterRaw,
            [
                Living(9UL, MaximumCoordinateRaw, MaximumCoordinateRaw),
                Living(4UL, MaximumCoordinateRaw - DiameterRaw, MaximumCoordinateRaw),
                Living(6UL, MaximumCoordinateRaw, MaximumCoordinateRaw - DiameterRaw),
                Living(
                    2UL,
                    MaximumCoordinateRaw - DiameterRaw - 1,
                    MaximumCoordinateRaw),
                Living(7UL, 0, 0),
            ]);
    }

    /// <summary>
    /// The contact radius equals the cell size in this configuration, so every
    /// interesting case is a pair that spans a cell edge: one raw unit apart
    /// across the edge, exactly tangent across the edge, one raw unit past
    /// tangent across the edge, and a pair two cells apart that the three-by-three
    /// neighbourhood must correctly report as clear.
    /// </summary>
    [Fact]
    public void Rebuild_MatchesTheOracleForBodiesStraddlingACellBoundary()
    {
        AssertMatchesOracle(
            DiameterRaw,
            [
                Living(1UL, DiameterRaw - 1, 0),
                Living(2UL, DiameterRaw, 0),
                Living(3UL, 0, DiameterRaw),
                Living(4UL, 0, (2 * DiameterRaw) + 1),
                Living(5UL, (3 * DiameterRaw) - 1, DiameterRaw - 1),
                Living(6UL, 3 * DiameterRaw, DiameterRaw),
                Living(7UL, 5 * DiameterRaw, 0),
                Living(8UL, 7 * DiameterRaw, 0),
            ]);
    }

    [Fact]
    public void Rebuild_MatchesTheOracleForCoincidentBodies()
    {
        AssertMatchesOracle(
            DiameterRaw,
            [
                Living(5UL, 12_345, 6_789),
                Living(3UL, 12_345, 6_789),
                Living(8UL, 12_345, 6_789),
                Dead(4UL, 12_345, 6_789),
                Living(9UL, 500_000, 500_000),
            ]);
    }

    [Fact]
    public void Rebuild_MatchesTheOracleForManyBodiesCrowdedIntoOneCell()
    {
        var crowd = new CollisionBody[24];

        for (var index = 0; index < crowd.Length; index++)
        {
            crowd[index] = Living(
                (ulong)((crowd.Length - index) * 3),
                index * 7,
                index * 11);
        }

        AssertMatchesOracle(DiameterRaw, crowd);
    }

    [Fact]
    public void Rebuild_ExcludesDeadBodiesFromEveryPair()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild(
            [
                Living(1UL, 0, 0),
                Dead(2UL, 0, 0),
                Dead(3UL, BodyRadiusRaw, 0),
                Living(4UL, 0, 0),
                Dead(5UL, 0, BodyRadiusRaw),
            ],
            BodyRadiusRaw);

        Assert.Equal([CollisionPair.Create(1UL, 4UL)], grid.Pairs);
    }

    [Theory]
    [InlineData(DiameterRaw)]
    [InlineData(DiameterRaw + 1)]
    [InlineData(3 * DiameterRaw)]
    public void Rebuild_MatchesTheOracleForGeneratedWorldsAcrossFixedSeeds(int cellSizeRaw)
    {
        var producedAtLeastOnePair = false;

        for (var seed = 1UL; seed <= 8UL; seed++)
        {
            var world = GenerateWorld(seed, bodyCount: 40, extentRaw: 24 * DiameterRaw);
            var expected = AssertMatchesOracle(cellSizeRaw, world);

            producedAtLeastOnePair |= expected.Count > 0;
        }

        Assert.True(producedAtLeastOnePair, "The generated worlds produced no pairs.");
    }

    [Fact]
    public void Rebuild_ProducesTheIdenticalOrderedResultForEveryInputPermutation()
    {
        var world = GenerateWorld(seed: 99UL, bodyCount: 32, extentRaw: 12 * DiameterRaw);
        var expected = NaiveCollisionPairs.Enumerate(world, BodyRadiusRaw);
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.NotEmpty(expected);

        for (var permutation = 0UL; permutation < 64UL; permutation++)
        {
            grid.Rebuild(DeterministicShuffle(world, permutation), BodyRadiusRaw);

            Assert.Equal(expected, grid.Pairs);
        }
    }

    [Fact]
    public void Rebuild_EmitsEachUnorderedPairExactlyOnceInAscendingOrder()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild(
            GenerateWorld(seed: 7UL, bodyCount: 48, extentRaw: 6 * DiameterRaw),
            BodyRadiusRaw);

        Assert.NotEmpty(grid.Pairs);
        Assert.Equal(grid.Pairs, grid.Pairs.Distinct());
        Assert.All(grid.Pairs, pair => Assert.True(pair.LowEntityId < pair.HighEntityId));

        for (var index = 1; index < grid.Pairs.Count; index++)
        {
            Assert.True(grid.Pairs[index - 1].CompareTo(grid.Pairs[index]) < 0);
        }
    }

    [Fact]
    public void Rebuild_DiscardsThePairsOfThePreviousCall()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild([Living(1UL, 0, 0), Living(2UL, 0, 0)], BodyRadiusRaw);
        Assert.NotEmpty(grid.Pairs);

        grid.Rebuild(
            [Living(1UL, 0, 0), Living(2UL, 10 * DiameterRaw, 0)],
            BodyRadiusRaw);

        Assert.Empty(grid.Pairs);
    }

    [Fact]
    public void Rebuild_IsRepeatableAcrossManyCallsOnTheSameInstance()
    {
        var world = GenerateWorld(seed: 11UL, bodyCount: 36, extentRaw: 10 * DiameterRaw);
        var expected = NaiveCollisionPairs.Enumerate(world, BodyRadiusRaw);
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.NotEmpty(expected);

        for (var repetition = 0; repetition < 5; repetition++)
        {
            grid.Rebuild(world, BodyRadiusRaw);

            Assert.Equal(expected, grid.Pairs);
        }
    }

    [Fact]
    public void AnyContact_IsFalseOnAnEmptyGrid()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.False(grid.AnyContact(0, 0, BodyRadiusRaw, excludeEntityId: 0UL));
        Assert.False(grid.AnyContact(
            MaximumCoordinateRaw,
            MaximumCoordinateRaw,
            BodyRadiusRaw,
            excludeEntityId: 1UL));
    }

    [Fact]
    public void AnyContact_IsTrueAtExactTangent()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, 0, 0));

        Assert.True(grid.AnyContact(DiameterRaw, 0, BodyRadiusRaw, excludeEntityId: 2UL));
        Assert.True(grid.AnyContact(0, DiameterRaw, BodyRadiusRaw, excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyContact_IsFalseOneRawUnitPastTangent()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, 0, 0));

        Assert.False(
            grid.AnyContact(DiameterRaw + 1, 0, BodyRadiusRaw, excludeEntityId: 2UL));
        Assert.False(
            grid.AnyContact(0, DiameterRaw + 1, BodyRadiusRaw, excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyContact_IgnoresTheExcludedEntity()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw));

        Assert.False(grid.AnyContact(
            4 * DiameterRaw,
            4 * DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 1UL));
        Assert.True(grid.AnyContact(
            4 * DiameterRaw,
            4 * DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyContact_FindsContactAcrossACellBoundary()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, DiameterRaw - 1, DiameterRaw - 1));

        Assert.True(grid.AnyContact(
            DiameterRaw,
            DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyContact_FindsContactFromEveryNeighbouringCell()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var probeXRaw = 4 * DiameterRaw;
        var probeYRaw = 4 * DiameterRaw;

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                grid.Clear();
                grid.Insert(Living(
                    1UL,
                    probeXRaw + (offsetX * BodyRadiusRaw),
                    probeYRaw + (offsetY * BodyRadiusRaw)));

                Assert.True(grid.AnyContact(
                    probeXRaw,
                    probeYRaw,
                    BodyRadiusRaw,
                    excludeEntityId: 2UL));
            }
        }
    }

    [Fact]
    public void AnyContact_IgnoresDeadBodies()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Dead(1UL, 0, 0));

        Assert.False(grid.AnyContact(0, 0, BodyRadiusRaw, excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyContact_RejectsInvalidArguments()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var narrowGrid = new CollisionUniformGrid(DiameterRaw - 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyContact(-1, 0, BodyRadiusRaw, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyContact(0, -1, BodyRadiusRaw, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyContact(0, 0, -1, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = narrowGrid.AnyContact(0, 0, BodyRadiusRaw, excludeEntityId: 0UL); });
    }

    [Fact]
    public void Insert_RejectsNegativeCoordinates()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Insert(Living(1UL, -1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Insert(Living(1UL, 0, -1)));
    }

    [Fact]
    public void Clear_RemovesEveryInsertedBodyAndEveryPair()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Rebuild([Living(1UL, 0, 0), Living(2UL, 0, 0)], BodyRadiusRaw);
        Assert.NotEmpty(grid.Pairs);

        grid.Clear();

        Assert.Empty(grid.Pairs);
        Assert.False(grid.AnyContact(0, 0, BodyRadiusRaw, excludeEntityId: 9UL));
    }

    [Fact]
    public void ClearThenInsert_ReproducesTheContactsOfAFullRebuild()
    {
        var world = GenerateWorld(seed: 5UL, bodyCount: 30, extentRaw: 8 * DiameterRaw);
        var rebuilt = new CollisionUniformGrid(DiameterRaw);
        var incremental = new CollisionUniformGrid(DiameterRaw);

        rebuilt.Rebuild(world, BodyRadiusRaw);
        incremental.Clear();

        foreach (var body in world)
        {
            incremental.Insert(body);
        }

        var expected = rebuilt.Pairs.Select(pair => pair.LowEntityId).Distinct().ToList();

        Assert.NotEmpty(expected);
        Assert.All(
            expected,
            entityId => Assert.True(incremental.AnyContact(
                BodyOf(world, entityId).XRaw,
                BodyOf(world, entityId).YRaw,
                BodyRadiusRaw,
                entityId)));
    }

    /// <summary>
    /// Runs the grid over <paramref name="bodies"/> and asserts exact equality
    /// with the reference oracle, returning the oracle result so a caller can
    /// assert that the case was not vacuous.
    /// </summary>
    private static List<CollisionPair> AssertMatchesOracle(
        int cellSizeRaw,
        IReadOnlyList<CollisionBody> bodies)
    {
        var expected = NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw);
        var grid = new CollisionUniformGrid(cellSizeRaw);

        grid.Rebuild(bodies, BodyRadiusRaw);

        Assert.Equal(expected, grid.Pairs);
        Assert.Equal(grid.Pairs, grid.Pairs.Distinct());

        return expected;
    }

    private static CollisionBody BodyOf(
        IReadOnlyList<CollisionBody> bodies,
        ulong entityId) =>
        bodies.Single(body => body.EntityId == entityId);

    /// <summary>
    /// Builds a crowded world of non-negative coordinates with non-sequential
    /// entity IDs and roughly one dead body in four, using the project-owned
    /// <see cref="SplitMix64"/>. <c>System.Random</c> is banned because its
    /// sequence is not guaranteed across major .NET versions, which would make
    /// these tests irreproducible.
    /// </summary>
    private static CollisionBody[] GenerateWorld(
        ulong seed,
        int bodyCount,
        int extentRaw)
    {
        var random = new SplitMix64(seed);
        var bodies = new CollisionBody[bodyCount];

        for (var index = 0; index < bodyCount; index++)
        {
            bodies[index] = new CollisionBody(
                EntityId: (ulong)((index * 7) + 3),
                XRaw: random.NextInt(extentRaw),
                YRaw: random.NextInt(extentRaw),
                IsAlive: random.NextInt(4) != 0);
        }

        return DeterministicShuffle(bodies, seed);
    }

    private static CollisionBody[] DeterministicShuffle(
        IReadOnlyList<CollisionBody> bodies,
        ulong seed)
    {
        var shuffled = bodies.ToArray();
        var random = new SplitMix64(seed);

        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swapIndex = random.NextInt(index + 1);
            (shuffled[index], shuffled[swapIndex]) =
                (shuffled[swapIndex], shuffled[index]);
        }

        return shuffled;
    }

    private static CollisionBody Living(ulong entityId, int xRaw, int yRaw) =>
        new(entityId, xRaw, yRaw, IsAlive: true);

    private static CollisionBody Dead(ulong entityId, int xRaw, int yRaw) =>
        new(entityId, xRaw, yRaw, IsAlive: false);
}
