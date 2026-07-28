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

    [Fact]
    public void AnyOverlap_AgreesWithANaiveScanAcrossManySeededWorlds()
    {
        const int ExtentRaw = 6 * DiameterRaw;

        for (ulong seed = 1UL; seed <= 40UL; seed++)
        {
            var world = GenerateWorld(seed, bodyCount: 40, extentRaw: ExtentRaw);
            var grid = new CollisionUniformGrid(DiameterRaw);

            foreach (var body in world)
            {
                grid.Insert(body);
            }

            var probes = new SplitMix64(seed + 1_000UL);

            for (var probe = 0; probe < 200; probe++)
            {
                var xRaw = probes.NextInt(ExtentRaw);
                var yRaw = probes.NextInt(ExtentRaw);
                var excludeEntityId = world[probes.NextInt(world.Length)].EntityId;

                Assert.Equal(
                    NaiveAnyOverlap(world, xRaw, yRaw, excludeEntityId),
                    grid.AnyOverlap(xRaw, yRaw, BodyRadiusRaw, excludeEntityId));
            }
        }
    }

    [Fact]
    public void AnyOverlap_TreatsExactTangencyAsFree()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw));

        // Tangency is a legal resting position. This is the single behavioural
        // difference from AnyContact and the resolver depends on it: a packed
        // front settles at tangency instead of jittering forever.
        Assert.False(grid.AnyOverlap(
            (4 * DiameterRaw) + DiameterRaw,
            4 * DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
        Assert.True(grid.AnyOverlap(
            (4 * DiameterRaw) + DiameterRaw - 1,
            4 * DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyOverlap_HandlesTheDegenerateGrids()
    {
        var empty = new CollisionUniformGrid(DiameterRaw);
        var single = new CollisionUniformGrid(DiameterRaw);
        var corner = new CollisionUniformGrid(DiameterRaw);

        Assert.False(empty.AnyOverlap(0, 0, BodyRadiusRaw, excludeEntityId: 0UL));
        Assert.False(empty.AnyOverlap(
            MaximumCoordinateRaw,
            MaximumCoordinateRaw,
            BodyRadiusRaw,
            excludeEntityId: 0UL));

        single.Insert(Living(1UL, 0, 0));
        Assert.True(single.AnyOverlap(0, 0, BodyRadiusRaw, excludeEntityId: 2UL));
        Assert.False(single.AnyOverlap(0, 0, BodyRadiusRaw, excludeEntityId: 1UL));

        // Both map corners, where the neighbourhood runs off the indexed
        // quadrant on two sides at once.
        corner.Insert(Living(1UL, 0, 0));
        corner.Insert(Living(2UL, MaximumCoordinateRaw, MaximumCoordinateRaw));
        Assert.True(corner.AnyOverlap(1, 1, BodyRadiusRaw, excludeEntityId: 9UL));
        Assert.True(corner.AnyOverlap(
            MaximumCoordinateRaw - 1,
            MaximumCoordinateRaw - 1,
            BodyRadiusRaw,
            excludeEntityId: 9UL));
    }

    [Fact]
    public void AnyOverlap_FindsAnOverlapFromEveryNeighbouringCell()
    {
        var probeXRaw = 4 * DiameterRaw;
        var probeYRaw = 4 * DiameterRaw;

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var grid = new CollisionUniformGrid(DiameterRaw);

                grid.Insert(Living(
                    1UL,
                    probeXRaw + (offsetX * BodyRadiusRaw),
                    probeYRaw + (offsetY * BodyRadiusRaw)));

                Assert.True(grid.AnyOverlap(
                    probeXRaw,
                    probeYRaw,
                    BodyRadiusRaw,
                    excludeEntityId: 2UL));
            }
        }
    }

    [Fact]
    public void AnyOverlap_FindsAnOverlapAcrossACellBoundary()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, DiameterRaw - 1, DiameterRaw - 1));

        Assert.True(grid.AnyOverlap(
            DiameterRaw,
            DiameterRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyOverlap_IgnoresDeadBodies()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Dead(1UL, 0, 0));

        Assert.False(grid.AnyOverlap(0, 0, BodyRadiusRaw, excludeEntityId: 2UL));
    }

    [Fact]
    public void AnyOverlap_RejectsInvalidArguments()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var narrowGrid = new CollisionUniformGrid(DiameterRaw - 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyOverlap(-1, 0, BodyRadiusRaw, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyOverlap(0, -1, BodyRadiusRaw, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyOverlap(0, 0, -1, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = narrowGrid.AnyOverlap(0, 0, BodyRadiusRaw, excludeEntityId: 0UL); });
    }

    [Fact]
    public void AnyCoincident_AgreesWithANaiveScanAcrossManySeededWorlds()
    {
        // A tight extent so that exact coincidences actually occur; a sparse
        // world would make every probe a trivial false.
        const int ExtentRaw = 2 * DiameterRaw;

        for (ulong seed = 1UL; seed <= 40UL; seed++)
        {
            var world = GenerateWorld(seed, bodyCount: 40, extentRaw: ExtentRaw);
            var grid = new CollisionUniformGrid(DiameterRaw);

            foreach (var body in world)
            {
                grid.Insert(body);
            }

            foreach (var body in world)
            {
                Assert.Equal(
                    NaiveAnyCoincident(world, body.XRaw, body.YRaw, body.EntityId),
                    grid.AnyCoincident(body.XRaw, body.YRaw, body.EntityId));
            }

            var probes = new SplitMix64(seed + 2_000UL);

            for (var probe = 0; probe < 200; probe++)
            {
                var xRaw = probes.NextInt(ExtentRaw);
                var yRaw = probes.NextInt(ExtentRaw);
                var excludeEntityId = world[probes.NextInt(world.Length)].EntityId;

                Assert.Equal(
                    NaiveAnyCoincident(world, xRaw, yRaw, excludeEntityId),
                    grid.AnyCoincident(xRaw, yRaw, excludeEntityId));
            }
        }
    }

    [Fact]
    public void AnyCoincident_IsTrueOnlyAtTheExactCentre()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw));

        Assert.True(grid.AnyCoincident(
            4 * DiameterRaw,
            4 * DiameterRaw,
            excludeEntityId: 2UL));
        Assert.False(grid.AnyCoincident(
            (4 * DiameterRaw) + 1,
            4 * DiameterRaw,
            excludeEntityId: 2UL));
        Assert.False(grid.AnyCoincident(
            4 * DiameterRaw,
            4 * DiameterRaw,
            excludeEntityId: 1UL));
    }

    [Fact]
    public void AnyCoincident_IgnoresDeadBodiesAndRejectsNegativeCoordinates()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);

        grid.Insert(Dead(1UL, 0, 0));

        Assert.False(grid.AnyCoincident(0, 0, excludeEntityId: 2UL));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyCoincident(-1, 0, excludeEntityId: 0UL); });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = grid.AnyCoincident(0, -1, excludeEntityId: 0UL); });
    }

    [Fact]
    public void Remove_MakesABodyInvisibleToEveryQuery()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var body = Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw);

        grid.Insert(body);
        grid.Remove(body);

        Assert.False(grid.AnyOverlap(
            body.XRaw,
            body.YRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
        Assert.False(grid.AnyContact(
            body.XRaw,
            body.YRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
        Assert.False(grid.AnyCoincident(body.XRaw, body.YRaw, excludeEntityId: 2UL));
    }

    [Fact]
    public void Remove_LeavesTheOtherBodiesOfTheSameCellReachable()
    {
        // Four bodies in one cell is the packing bound, so this exercises an
        // unlink at the head, in the middle, and at the tail of a full chain.
        var originXRaw = 4 * DiameterRaw;
        var originYRaw = 4 * DiameterRaw;

        for (var removedIndex = 0; removedIndex < 4; removedIndex++)
        {
            var grid = new CollisionUniformGrid(DiameterRaw);
            var bodies = new List<CollisionBody>();

            for (var index = 0; index < 4; index++)
            {
                var body = Living(
                    (ulong)index + 1UL,
                    originXRaw + (index % 2),
                    originYRaw + (index / 2));

                bodies.Add(body);
                grid.Insert(body);
            }

            grid.Remove(bodies[removedIndex]);

            for (var index = 0; index < 4; index++)
            {
                Assert.Equal(
                    index != removedIndex,
                    grid.AnyCoincident(
                        bodies[index].XRaw,
                        bodies[index].YRaw,
                        excludeEntityId: 99UL));
            }
        }
    }

    [Fact]
    public void Remove_EmptiesTheGridWhenEveryBodyIsRemoved()
    {
        var world = GenerateWorld(seed: 11UL, bodyCount: 40, extentRaw: 6 * DiameterRaw);
        var grid = new CollisionUniformGrid(DiameterRaw);

        foreach (var body in world)
        {
            grid.Insert(body);
        }

        foreach (var body in world)
        {
            grid.Remove(body);
        }

        foreach (var body in world)
        {
            Assert.False(grid.AnyOverlap(
                body.XRaw,
                body.YRaw,
                BodyRadiusRaw,
                excludeEntityId: 99UL));
        }
    }

    [Fact]
    public void Remove_ThenInsert_MakesTheBodyVisibleAgain()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var body = Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw);

        grid.Insert(body);
        grid.Remove(body);
        grid.Insert(body);

        Assert.True(grid.AnyOverlap(
            body.XRaw,
            body.YRaw,
            BodyRadiusRaw,
            excludeEntityId: 2UL));
    }

    /// <summary>
    /// Removing something that was never inserted is a documented no-op rather
    /// than a throw, and a dead body is ignored on removal exactly as it is on
    /// insertion. Both are pinned here so a later change cannot quietly turn
    /// either into an exception.
    /// </summary>
    [Fact]
    public void Remove_IsANoOpForAnAbsentOrDeadBody()
    {
        var grid = new CollisionUniformGrid(DiameterRaw);
        var present = Living(1UL, 4 * DiameterRaw, 4 * DiameterRaw);

        grid.Insert(present);

        grid.Remove(Living(2UL, 4 * DiameterRaw, 4 * DiameterRaw));
        grid.Remove(Living(3UL, 40 * DiameterRaw, 40 * DiameterRaw));
        grid.Remove(Dead(1UL, present.XRaw, present.YRaw));

        Assert.True(grid.AnyOverlap(
            present.XRaw,
            present.YRaw,
            BodyRadiusRaw,
            excludeEntityId: 9UL));
    }

    private static bool NaiveAnyOverlap(
        IReadOnlyList<CollisionBody> bodies,
        int xRaw,
        int yRaw,
        ulong excludeEntityId)
    {
        for (var index = 0; index < bodies.Count; index++)
        {
            var body = bodies[index];

            if (body.IsAlive &&
                body.EntityId != excludeEntityId &&
                CollisionGeometry.Overlaps(
                    xRaw,
                    yRaw,
                    body.XRaw,
                    body.YRaw,
                    BodyRadiusRaw))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NaiveAnyCoincident(
        IReadOnlyList<CollisionBody> bodies,
        int xRaw,
        int yRaw,
        ulong excludeEntityId)
    {
        for (var index = 0; index < bodies.Count; index++)
        {
            var body = bodies[index];

            if (body.IsAlive &&
                body.EntityId != excludeEntityId &&
                CollisionGeometry.IsCoincident(xRaw, yRaw, body.XRaw, body.YRaw))
            {
                return true;
            }
        }

        return false;
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
