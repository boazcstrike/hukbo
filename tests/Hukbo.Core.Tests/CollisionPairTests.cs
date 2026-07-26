using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class CollisionPairTests
{
    private const int BodyRadiusRaw = 4 * FixedPoint.Scale;

    private const int DiameterRaw = 2 * BodyRadiusRaw;

    /// <summary>
    /// A radius chosen so that a 3-4-5 triangle scaled by
    /// <see cref="FixedPoint.Scale"/> lands exactly on the contact threshold,
    /// which lets the diagonal tangent case be asserted without any rounding.
    /// </summary>
    private const int PythagoreanRadiusRaw = 5 * FixedPoint.Scale / 2;

    [Fact]
    public void CreateNormalisesArgumentOrderIntoLowThenHigh()
    {
        var ascending = CollisionPair.Create(3UL, 9UL);
        var descending = CollisionPair.Create(9UL, 3UL);

        Assert.Equal(3UL, ascending.LowEntityId);
        Assert.Equal(9UL, ascending.HighEntityId);
        Assert.Equal(ascending, descending);
    }

    [Fact]
    public void CreateRejectsEqualEntityIds()
    {
        Assert.Throws<ArgumentException>(() => CollisionPair.Create(7UL, 7UL));
        Assert.Throws<ArgumentException>(() => CollisionPair.Create(0UL, 0UL));
    }

    [Fact]
    public void CompareToOrdersByLowEntityIdThenHighEntityId()
    {
        var first = CollisionPair.Create(1UL, 2UL);
        var second = CollisionPair.Create(1UL, 3UL);
        var third = CollisionPair.Create(2UL, 3UL);

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(third) < 0);
        Assert.True(first.CompareTo(third) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(CollisionPair.Create(2UL, 1UL)));
    }

    [Fact]
    public void CompareToIsAntisymmetricAndConsistentAcrossEveryCombination()
    {
        var ordered = new[]
        {
            CollisionPair.Create(1UL, 2UL),
            CollisionPair.Create(1UL, 9UL),
            CollisionPair.Create(2UL, 3UL),
            CollisionPair.Create(2UL, 4UL),
            CollisionPair.Create(10UL, 11UL),
            CollisionPair.Create(ulong.MaxValue - 1UL, ulong.MaxValue),
        };

        for (var leftIndex = 0; leftIndex < ordered.Length; leftIndex++)
        {
            for (var rightIndex = 0; rightIndex < ordered.Length; rightIndex++)
            {
                var forward = Math.Sign(ordered[leftIndex].CompareTo(ordered[rightIndex]));
                var backward = Math.Sign(ordered[rightIndex].CompareTo(ordered[leftIndex]));

                Assert.Equal(-backward, forward);
                Assert.Equal(leftIndex == rightIndex, forward == 0);
                Assert.Equal(Math.Sign(leftIndex.CompareTo(rightIndex)), forward);
            }
        }
    }

    [Fact]
    public void EnumerateReturnsNothingForFewerThanTwoLivingBodies()
    {
        Assert.Empty(NaiveCollisionPairs.Enumerate([], BodyRadiusRaw));
        Assert.Empty(
            NaiveCollisionPairs.Enumerate([Living(1UL, 0, 0)], BodyRadiusRaw));
        Assert.Empty(
            NaiveCollisionPairs.Enumerate(
                [Living(1UL, 0, 0), Dead(2UL, 0, 0)],
                BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateTreatsExactTangentAsContact()
    {
        var bodies = new[] { Living(1UL, 0, 0), Living(2UL, DiameterRaw, 0) };

        Assert.Equal(
            new[] { CollisionPair.Create(1UL, 2UL) },
            NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateExcludesBodiesOneRawUnitPastTangent()
    {
        var bodies = new[] { Living(1UL, 0, 0), Living(2UL, DiameterRaw + 1, 0) };

        Assert.Empty(NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateTreatsExactDiagonalTangentAsContact()
    {
        var bodies = new[]
        {
            Living(1UL, 0, 0),
            Living(2UL, 3 * FixedPoint.Scale, 4 * FixedPoint.Scale),
        };

        Assert.Equal(
            new[] { CollisionPair.Create(1UL, 2UL) },
            NaiveCollisionPairs.Enumerate(bodies, PythagoreanRadiusRaw));
    }

    [Fact]
    public void EnumerateExcludesBodiesOneRawUnitPastDiagonalTangent()
    {
        var bodies = new[]
        {
            Living(1UL, 0, 0),
            Living(2UL, (3 * FixedPoint.Scale) + 1, 4 * FixedPoint.Scale),
        };

        Assert.Empty(NaiveCollisionPairs.Enumerate(bodies, PythagoreanRadiusRaw));
    }

    [Fact]
    public void EnumerateIncludesCoincidentBodies()
    {
        var bodies = new[]
        {
            Living(5UL, 12_345, -6_789),
            Living(3UL, 12_345, -6_789),
            Living(8UL, 12_345, -6_789),
        };

        Assert.Equal(
            new[]
            {
                CollisionPair.Create(3UL, 5UL),
                CollisionPair.Create(3UL, 8UL),
                CollisionPair.Create(5UL, 8UL),
            },
            NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateSkipsDeadBodiesEvenWhenTheyOverlapLivingBodies()
    {
        var bodies = new[]
        {
            Living(1UL, 0, 0),
            Dead(2UL, 0, 0),
            Dead(3UL, BodyRadiusRaw, 0),
            Living(4UL, 0, 0),
            Dead(5UL, 0, BodyRadiusRaw),
        };

        Assert.Equal(
            new[] { CollisionPair.Create(1UL, 4UL) },
            NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateSkipsPairsOfDeadBodies()
    {
        var bodies = new[] { Dead(1UL, 0, 0), Dead(2UL, 0, 0) };

        Assert.Empty(NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateReturnsTheKnownAnswerForAHandBuiltWorld()
    {
        Assert.Equal(
            ExpectedMixedWorldPairs(),
            NaiveCollisionPairs.Enumerate(BuildMixedWorld(), BodyRadiusRaw));
    }

    [Fact]
    public void EnumerateEmitsEachUnorderedPairExactlyOnce()
    {
        var clique = new[]
        {
            Living(4UL, 0, 0),
            Living(1UL, BodyRadiusRaw, 0),
            Living(3UL, 0, BodyRadiusRaw),
            Living(2UL, BodyRadiusRaw, BodyRadiusRaw),
        };

        var pairs = NaiveCollisionPairs.Enumerate(clique, BodyRadiusRaw);

        Assert.Equal(
            new[]
            {
                CollisionPair.Create(1UL, 2UL),
                CollisionPair.Create(1UL, 3UL),
                CollisionPair.Create(1UL, 4UL),
                CollisionPair.Create(2UL, 3UL),
                CollisionPair.Create(2UL, 4UL),
                CollisionPair.Create(3UL, 4UL),
            },
            pairs);
        Assert.Equal(pairs, pairs.Distinct());
        Assert.All(pairs, pair => Assert.True(pair.LowEntityId < pair.HighEntityId));
    }

    [Fact]
    public void EnumerateProducesTheIdenticalOrderedResultForEveryInputPermutation()
    {
        var world = BuildMixedWorld();
        var expected = ExpectedMixedWorldPairs();

        Assert.NotEmpty(expected);

        for (var permutation = 0; permutation < 64; permutation++)
        {
            var shuffled = DeterministicShuffle(world, (ulong)permutation);
            var pairs = NaiveCollisionPairs.Enumerate(shuffled, BodyRadiusRaw);

            Assert.Equal(expected, pairs);
            Assert.Equal(pairs, pairs.Distinct());
        }
    }

    /// <summary>
    /// A hand-built world mixing a dense cluster, an isolated tangent pair, a
    /// lone body, dead bodies that would collide if they were alive, and
    /// deliberately non-sequential entity IDs listed out of order.
    /// </summary>
    private static CollisionBody[] BuildMixedWorld() =>
        [
            Living(11UL, 0, 0),
            Living(4UL, BodyRadiusRaw, 0),
            Dead(9UL, 0, BodyRadiusRaw),
            Living(2UL, DiameterRaw, 0),
            Living(30UL, 0, 0),
            Dead(1UL, 100, 100),
            Living(7UL, 500_000, 500_000),
            Living(23UL, 500_000, 500_000 + DiameterRaw),
            Living(15UL, 900_000, 0),
        ];

    private static CollisionPair[] ExpectedMixedWorldPairs() =>
        [
            CollisionPair.Create(2UL, 4UL),
            CollisionPair.Create(2UL, 11UL),
            CollisionPair.Create(2UL, 30UL),
            CollisionPair.Create(4UL, 11UL),
            CollisionPair.Create(4UL, 30UL),
            CollisionPair.Create(7UL, 23UL),
            CollisionPair.Create(11UL, 30UL),
        ];

    /// <summary>
    /// Fisher-Yates using the project-owned <see cref="SplitMix64"/>.
    /// <c>System.Random</c> is banned because its sequence is not guaranteed
    /// across major .NET versions, which would make this test irreproducible.
    /// </summary>
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
