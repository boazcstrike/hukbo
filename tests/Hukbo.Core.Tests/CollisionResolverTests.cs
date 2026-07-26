using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Acceptance tests for the solid-disc collision resolver.
/// </summary>
/// <remarks>
/// <para>
/// Every scenario is written against the approved constants of the collision
/// policy decision record: one common body radius of four world units, a
/// diameter of eight, and a movement speed of three, so a mover can never cover
/// more than one radius in a tick.
/// </para>
/// <para>
/// The resolver's stated precondition is that no two request start positions
/// strictly overlap, which is what spawn resolution guarantees in a real battle.
/// Every scenario here honours that precondition except the two that
/// deliberately exercise the exact co-location repair.
/// </para>
/// </remarks>
public sealed class CollisionResolverTests
{
    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;

    private const int DiameterRaw = 2 * BodyRadiusRaw;

    /// <summary>The approved movement speed, three world units per tick.</summary>
    private const int MovementSpeedRaw = 3 * FixedPoint.Scale;

    private const int MapDimensionRaw = 200 * FixedPoint.Scale;

    /// <summary>The largest legal centre coordinate on either axis.</summary>
    private const int MaximumCenterRaw = MapDimensionRaw - BodyRadiusRaw;

    [Fact]
    public void Constructor_RejectsANonPositiveBodyRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollisionResolver(0, MapDimensionRaw, MapDimensionRaw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollisionResolver(-1, MapDimensionRaw, MapDimensionRaw));
    }

    [Fact]
    public void Constructor_RejectsAMapNarrowerThanOneBodyOnEitherAxis()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollisionResolver(BodyRadiusRaw, DiameterRaw - 1, MapDimensionRaw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CollisionResolver(BodyRadiusRaw, MapDimensionRaw, DiameterRaw - 1));
    }

    [Fact]
    public void Constructor_AcceptsAMapExactlyOneBodyWide()
    {
        var resolver = new CollisionResolver(BodyRadiusRaw, DiameterRaw, DiameterRaw);

        resolver.Resolve([]);

        Assert.Empty(resolver.Results);
    }

    [Fact]
    public void Resolve_RejectsANullRequestList()
    {
        Assert.Throws<ArgumentNullException>(() => NewResolver().Resolve(null!));
    }

    [Fact]
    public void Resolve_RejectsRequestsThatAreNotStrictlyAscendingByEntityId()
    {
        var resolver = NewResolver();

        Assert.Throws<ArgumentException>(() => resolver.Resolve(
            [Stationary(2UL, 50_000, 50_000), Stationary(1UL, 80_000, 80_000)]));
        Assert.Throws<ArgumentException>(() => resolver.Resolve(
            [Stationary(1UL, 50_000, 50_000), Stationary(1UL, 80_000, 80_000)]));
    }

    [Fact]
    public void Resolve_RejectsANegativeStartCoordinate()
    {
        var resolver = NewResolver();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.Resolve([Stationary(1UL, -1, 50_000)]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.Resolve([Stationary(1UL, 50_000, -1)]));
    }

    [Fact]
    public void Resolve_ProducesOneResultPerRequestInTheSameOrder()
    {
        var requests = new[]
        {
            Mover(1UL, 50_000, 50_000, 53_072, 50_000),
            Stationary(4UL, 90_000, 90_000),
            Mover(7UL, 120_000, 120_000, 123_072, 120_000),
        };
        var resolver = NewResolver();

        resolver.Resolve(requests);

        Assert.Equal(requests.Length, resolver.Results.Count);
        Assert.Equal(
            requests.Select(request => request.EntityId),
            resolver.Results.Select(result => result.EntityId));
    }

    [Fact]
    public void Resolve_DiscardsTheResultsOfThePreviousCall()
    {
        var resolver = NewResolver();

        resolver.Resolve([Stationary(1UL, 50_000, 50_000), Stationary(2UL, 90_000, 90_000)]);
        Assert.Equal(2, resolver.Results.Count);

        resolver.Resolve([]);

        Assert.Empty(resolver.Results);
        Assert.Equal(0, resolver.AcceptedMoveCount);
        Assert.Equal(0, resolver.BlockedCount);
    }

    // ---------------------------------------------------------------- tangent

    /// <summary>
    /// Acceptance row "tangent start". Exactly touching is clearance, not
    /// collision, so two bodies one diameter apart are already at a legal resting
    /// position and neither is repaired.
    /// </summary>
    [Fact]
    public void Resolve_TreatsAnExactlyTangentStartAsALegalRestingPosition()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, 50_000, 50_000),
            Stationary(2UL, 50_000 + DiameterRaw, 50_000),
        ]);

        AssertResult(resolver, 1UL, 50_000, 50_000, MovementResolution.None);
        AssertResult(resolver, 2UL, 50_000 + DiameterRaw, 50_000, MovementResolution.None);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "tangent start", movement half. A mover is allowed to stop
    /// exactly on the tangent circle, because the rejection test is the strict
    /// <c>squaredDistance &lt; (2R)^2</c>.
    /// </summary>
    [Fact]
    public void Resolve_AcceptsADestinationThatLandsExactlyOnTangency()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, 50_000, 50_000),
            Mover(
                2UL,
                50_000 + DiameterRaw + MovementSpeedRaw,
                50_000,
                50_000 + DiameterRaw,
                50_000),
        ]);

        AssertResult(resolver, 2UL, 50_000 + DiameterRaw, 50_000, MovementResolution.Moved);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "one-raw-unit penetration attempt". One raw unit inside
    /// tangency is penetration and must be refused, however small.
    /// </summary>
    [Fact]
    public void Resolve_RefusesADestinationThatPenetratesByOneRawUnit()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, 50_000, 50_000),
            Mover(
                2UL,
                50_000 + DiameterRaw + MovementSpeedRaw + 1,
                50_000,
                50_000 + DiameterRaw - 1,
                50_000),
        ]);

        var mover = ResultOf(resolver, 2UL);

        Assert.Equal(MovementResolution.Truncated, mover.Resolution);
        Assert.True(
            mover.XRaw > 50_000 + DiameterRaw - 1,
            "The refused destination was committed anyway.");
        AssertNoOverlap(resolver);
    }

    // ----------------------------------------------------------- approach

    /// <summary>
    /// Acceptance row "head-on approach". Two tangent movers walking into each
    /// other have no legal candidate at all, so both hold their ground and the
    /// tangent contact survives the tick.
    /// </summary>
    [Fact]
    public void Resolve_BlocksBothAgentsInAHeadOnApproach()
    {
        var resolver = NewResolver();
        var leftXRaw = 50_000;
        var rightXRaw = leftXRaw + DiameterRaw;

        resolver.Resolve(
        [
            Mover(1UL, leftXRaw, 50_000, leftXRaw + MovementSpeedRaw, 50_000),
            Mover(2UL, rightXRaw, 50_000, rightXRaw - MovementSpeedRaw, 50_000),
        ]);

        AssertResult(resolver, 1UL, leftXRaw, 50_000, MovementResolution.Blocked);
        AssertResult(resolver, 2UL, rightXRaw, 50_000, MovementResolution.Blocked);
        Assert.Equal(0, resolver.AcceptedMoveCount);
        Assert.Equal(2, resolver.BlockedCount);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "attempted crossing / swap". Even when both proposals name
    /// the other agent's exact start position, neither may pass through the other.
    /// </summary>
    [Fact]
    public void Resolve_PreventsTwoAgentsFromSwappingPositions()
    {
        var resolver = NewResolver();
        var leftXRaw = 50_000;
        var rightXRaw = leftXRaw + DiameterRaw;

        resolver.Resolve(
        [
            Mover(1UL, leftXRaw, 50_000, rightXRaw, 50_000),
            Mover(2UL, rightXRaw, 50_000, leftXRaw, 50_000),
        ]);

        AssertResult(resolver, 1UL, leftXRaw, 50_000, MovementResolution.Blocked);
        AssertResult(resolver, 2UL, rightXRaw, 50_000, MovementResolution.Blocked);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "stationary blocker". The stationary agent carries the
    /// higher entity ID, so it would lose its ground to the mover if stationary
    /// bodies were not committed before any mover is considered.
    /// </summary>
    [Fact]
    public void Resolve_KeepsTheGroundOfAStationaryAgentWithAHigherEntityId()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
            Stationary(9UL, 58_500, 50_000),
        ]);

        var mover = ResultOf(resolver, 1UL);

        AssertResult(resolver, 9UL, 58_500, 50_000, MovementResolution.None);
        Assert.Equal(MovementResolution.Truncated, mover.Resolution);
        Assert.True(mover.XRaw < 50_000 + MovementSpeedRaw, "The mover was not truncated.");
        Assert.True(mover.XRaw > 50_000, "The mover did not advance at all.");
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "two converging movers". The lower entity ID takes the
    /// contested ground unchanged; the higher one advances as far as the remaining
    /// clearance allows.
    /// </summary>
    [Fact]
    public void Resolve_LetsTheLowerEntityIdAdvanceAndTruncatesTheConvergingMover()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
            Mover(2UL, 62_000, 50_000, 62_000 - MovementSpeedRaw, 50_000),
        ]);

        var advanced = ResultOf(resolver, 1UL);
        var truncated = ResultOf(resolver, 2UL);

        AssertResult(resolver, 1UL, 50_000 + MovementSpeedRaw, 50_000, MovementResolution.Moved);
        Assert.Equal(MovementResolution.Truncated, truncated.Resolution);
        Assert.True(truncated.XRaw < 62_000, "The converging mover did not advance.");
        Assert.True(
            62_000 - truncated.XRaw < advanced.XRaw - 50_000,
            "The converging mover was not the one that gave way.");
        AssertNoOverlap(resolver);
    }

    // ------------------------------------------------------- co-location

    /// <summary>
    /// Acceptance row "exactly coincident centres". The higher entity ID is
    /// displaced by exactly one diameter, trying <c>+X</c> first.
    /// </summary>
    [Fact]
    public void Resolve_SeparatesExactlyCoincidentCentresAlongPositiveX()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, 50_000, 50_000),
            Stationary(2UL, 50_000, 50_000),
        ]);

        AssertResult(resolver, 1UL, 50_000, 50_000, MovementResolution.None);
        AssertResult(
            resolver,
            2UL,
            50_000 + DiameterRaw,
            50_000,
            MovementResolution.Separated);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// The separation direction order is <c>+X, -X, +Y, -Y</c>, so an agent
    /// pinned against the right edge falls through to <c>-X</c>.
    /// </summary>
    [Fact]
    public void Resolve_FallsBackToNegativeXWhenPositiveXLeavesTheMap()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, MaximumCenterRaw, 50_000),
            Stationary(2UL, MaximumCenterRaw, 50_000),
        ]);

        AssertResult(
            resolver,
            2UL,
            MaximumCenterRaw - DiameterRaw,
            50_000,
            MovementResolution.Separated);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// When no separation direction is legal the agent stays where it is and
    /// reports <see cref="MovementResolution.Blocked"/> rather than throwing. A
    /// map exactly one body wide leaves a single legal centre, so every direction
    /// leaves the map.
    /// </summary>
    [Fact]
    public void Resolve_BlocksACoincidentAgentWhenNoSeparationDirectionIsLegal()
    {
        var resolver = new CollisionResolver(BodyRadiusRaw, DiameterRaw, DiameterRaw);

        resolver.Resolve(
        [
            Stationary(1UL, BodyRadiusRaw, BodyRadiusRaw),
            Stationary(2UL, BodyRadiusRaw, BodyRadiusRaw),
        ]);

        AssertResult(resolver, 1UL, BodyRadiusRaw, BodyRadiusRaw, MovementResolution.None);
        AssertResult(resolver, 2UL, BodyRadiusRaw, BodyRadiusRaw, MovementResolution.Blocked);
        Assert.Equal(1, resolver.BlockedCount);
    }

    /// <summary>
    /// Two stationary bodies that merely overlap without sharing a centre are left
    /// exactly where they are. Spawn resolution makes this unreachable in a real
    /// battle, and the resolver deliberately does not invent a repair for it.
    /// </summary>
    [Fact]
    public void Resolve_LeavesMerelyOverlappingStationaryBodiesInPlace()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Stationary(1UL, 50_000, 50_000),
            Stationary(2UL, 50_000 + 1, 50_000),
        ]);

        AssertResult(resolver, 1UL, 50_000, 50_000, MovementResolution.None);
        AssertResult(resolver, 2UL, 50_000 + 1, 50_000, MovementResolution.None);
    }

    // ----------------------------------------------------------- crowding

    /// <summary>
    /// Acceptance row "multiple blockers". Four stationary bodies one diameter
    /// away on each axis leave the mover no legal candidate in its preferred
    /// direction, so it holds position.
    /// </summary>
    [Fact]
    public void Resolve_BlocksAMoverEnclosedByMultipleBlockers()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Mover(1UL, 100_000, 100_000, 100_000 + MovementSpeedRaw, 100_000),
            Stationary(2UL, 100_000 + DiameterRaw, 100_000),
            Stationary(3UL, 100_000 - DiameterRaw, 100_000),
            Stationary(4UL, 100_000, 100_000 + DiameterRaw),
            Stationary(5UL, 100_000, 100_000 - DiameterRaw),
        ]);

        AssertResult(resolver, 1UL, 100_000, 100_000, MovementResolution.Blocked);
        Assert.Equal(0, resolver.AcceptedMoveCount);
        Assert.Equal(1, resolver.BlockedCount);
        AssertNoOverlap(resolver);
    }

    /// <summary>
    /// Acceptance row "corner contact on both axes". Both axes clamp in the same
    /// tick; there is no special corner rule.
    /// </summary>
    [Theory]
    [InlineData(
        MaximumCenterRaw - 1_000,
        MaximumCenterRaw - 1_000,
        MaximumCenterRaw + 5_000,
        MaximumCenterRaw + 5_000,
        MaximumCenterRaw,
        MaximumCenterRaw)]
    [InlineData(
        BodyRadiusRaw + 1_000,
        BodyRadiusRaw + 1_000,
        BodyRadiusRaw - 5_000,
        BodyRadiusRaw - 5_000,
        BodyRadiusRaw,
        BodyRadiusRaw)]
    public void Resolve_ClampsACornerDestinationOnBothAxes(
        int startXRaw,
        int startYRaw,
        int preferredXRaw,
        int preferredYRaw,
        int expectedXRaw,
        int expectedYRaw)
    {
        var resolver = NewResolver();

        resolver.Resolve(
            [Mover(1UL, startXRaw, startYRaw, preferredXRaw, preferredYRaw)]);

        AssertResult(resolver, 1UL, expectedXRaw, expectedYRaw, MovementResolution.Moved);
    }

    /// <summary>
    /// Acceptance row "entity ID priority". The same geometry resolves in favour
    /// of whichever agent holds the lower entity ID, and nothing else about the
    /// scenario changes.
    /// </summary>
    [Fact]
    public void Resolve_AwardsAContestedDestinationToTheLowerEntityId()
    {
        var lowerStartYRaw = 90_000;
        var upperStartYRaw = 106_192;
        var contestedYRaw = 98_000;

        var lowerWins = NewResolver();
        lowerWins.Resolve(
        [
            Mover(1UL, 100_000, lowerStartYRaw, 100_000, contestedYRaw),
            Mover(2UL, 100_000, upperStartYRaw, 100_000, contestedYRaw + 192),
        ]);

        AssertResult(lowerWins, 1UL, 100_000, contestedYRaw, MovementResolution.Moved);
        AssertResult(lowerWins, 2UL, 100_000, upperStartYRaw, MovementResolution.Blocked);
        AssertNoOverlap(lowerWins);

        var upperWins = NewResolver();
        upperWins.Resolve(
        [
            Mover(1UL, 100_000, upperStartYRaw, 100_000, contestedYRaw + 192),
            Mover(2UL, 100_000, lowerStartYRaw, 100_000, contestedYRaw),
        ]);

        AssertResult(upperWins, 1UL, 100_000, contestedYRaw + 192, MovementResolution.Moved);
        AssertResult(upperWins, 2UL, 100_000, lowerStartYRaw, MovementResolution.Blocked);
        AssertNoOverlap(upperWins);
    }

    /// <summary>
    /// A diagonal proposal whose destination is blocked but whose X axis is clear
    /// resolves to a single-axis slide.
    /// </summary>
    [Fact]
    public void Resolve_SlidesAlongOneAxisWhenTheDiagonalDestinationIsBlocked()
    {
        var resolver = NewResolver();

        resolver.Resolve(
        [
            Mover(
                1UL,
                100_000,
                100_000,
                100_000 + MovementSpeedRaw,
                100_000 + MovementSpeedRaw),
            Stationary(2UL, 100_000 + MovementSpeedRaw, 111_000),
        ]);

        AssertResult(
            resolver,
            1UL,
            100_000 + MovementSpeedRaw,
            100_000,
            MovementResolution.Slid);
        AssertNoOverlap(resolver);
    }

    // ------------------------------------------------------------ invariants

    /// <summary>
    /// Acceptance row "every <see cref="MovementResolution"/> value reachable".
    /// </summary>
    [Fact]
    public void Resolve_ReachesEveryMovementResolutionValue()
    {
        var observed = new HashSet<MovementResolution>();

        foreach (var scenario in ResolutionCoverageScenarios())
        {
            var resolver = scenario.Resolver;

            resolver.Resolve(scenario.Requests);

            foreach (var result in resolver.Results)
            {
                observed.Add(result.Resolution);
            }
        }

        Assert.Equal(
            Enum.GetValues<MovementResolution>().OrderBy(value => value),
            observed.OrderBy(value => value));
    }

    /// <summary>
    /// Acceptance row "movement budget never exceeded". Collision may only reduce
    /// displacement, so every committed position is at least as close to the start
    /// as the preferred destination was. The comparison is on squared distances,
    /// which is order-preserving for non-negative lengths and needs no square root.
    /// <see cref="MovementResolution.Separated"/> is the one documented exemption.
    /// </summary>
    [Fact]
    public void Resolve_NeverExceedsTheRequestedMovementBudget()
    {
        var checkedAtLeastOneMover = false;

        for (var seed = 1UL; seed <= 8UL; seed++)
        {
            var requests = GenerateCrowd(seed, columns: 6, rows: 6);
            var resolver = NewResolver();

            resolver.Resolve(requests);

            for (var index = 0; index < requests.Length; index++)
            {
                var request = requests[index];
                var result = resolver.Results[index];

                Assert.Equal(request.EntityId, result.EntityId);

                if (result.Resolution == MovementResolution.Separated)
                {
                    continue;
                }

                var committed = CollisionGeometry.SquaredDistance(
                    request.StartXRaw,
                    request.StartYRaw,
                    result.XRaw,
                    result.YRaw);
                var requested = CollisionGeometry.SquaredDistance(
                    request.StartXRaw,
                    request.StartYRaw,
                    request.PreferredXRaw,
                    request.PreferredYRaw);

                Assert.True(
                    committed <= requested,
                    $"Entity {result.EntityId} moved further than it asked to.");

                checkedAtLeastOneMover |= request.HasProposal;
            }
        }

        Assert.True(checkedAtLeastOneMover, "The generated crowds contained no movers.");
    }

    /// <summary>
    /// The authoritative post-tick invariant of the collision policy decision
    /// record, asserted by brute force over every ordered pair so that a defect in
    /// the uniform grid cannot hide behind the same grid.
    /// </summary>
    [Fact]
    public void Resolve_LeavesNoTwoCommittedBodiesStrictlyOverlapping()
    {
        var sawAcceptedMove = false;
        var sawBlocked = false;

        for (var seed = 1UL; seed <= 12UL; seed++)
        {
            var resolver = NewResolver();

            resolver.Resolve(GenerateCrowd(seed, columns: 6, rows: 6));

            AssertNoOverlap(resolver);

            sawAcceptedMove |= resolver.AcceptedMoveCount > 0;
            sawBlocked |= resolver.BlockedCount > 0;
        }

        Assert.True(sawAcceptedMove, "No generated crowd produced an accepted move.");
        Assert.True(sawBlocked, "No generated crowd produced a blocked agent.");
    }

    [Fact]
    public void Resolve_CountsAcceptedMovesAndBlockedAgentsConsistentlyWithTheResults()
    {
        for (var seed = 1UL; seed <= 6UL; seed++)
        {
            var resolver = NewResolver();

            resolver.Resolve(GenerateCrowd(seed, columns: 5, rows: 5));

            var accepted = resolver.Results.Count(result =>
                result.Resolution is MovementResolution.Moved
                    or MovementResolution.Truncated
                    or MovementResolution.Slid);
            var blocked = resolver.Results.Count(result =>
                result.Resolution == MovementResolution.Blocked);

            Assert.Equal(accepted, resolver.AcceptedMoveCount);
            Assert.Equal(blocked, resolver.BlockedCount);
        }
    }

    /// <summary>
    /// Acceptance row "determinism". The same requests must give the same results
    /// on a reused instance and on a fresh one, so no buffer may leak state from a
    /// previous call.
    /// </summary>
    [Fact]
    public void Resolve_IsRepeatableAcrossCallsAndAcrossInstances()
    {
        var requests = GenerateCrowd(seed: 3UL, columns: 6, rows: 6);
        var reused = NewResolver();

        reused.Resolve(requests);

        var expected = reused.Results.ToArray();
        var expectedAccepted = reused.AcceptedMoveCount;
        var expectedBlocked = reused.BlockedCount;

        Assert.NotEmpty(expected);

        for (var repetition = 0; repetition < 4; repetition++)
        {
            reused.Resolve(GenerateCrowd(seed: 9UL, columns: 4, rows: 4));
            reused.Resolve(requests);

            Assert.Equal(expected, reused.Results);
            Assert.Equal(expectedAccepted, reused.AcceptedMoveCount);
            Assert.Equal(expectedBlocked, reused.BlockedCount);
        }

        var fresh = NewResolver();

        fresh.Resolve(requests);

        Assert.Equal(expected, fresh.Results);
        Assert.Equal(expectedAccepted, fresh.AcceptedMoveCount);
        Assert.Equal(expectedBlocked, fresh.BlockedCount);
    }

    // ------------------------------------------------------------- helpers

    private static CollisionResolver NewResolver() =>
        new(BodyRadiusRaw, MapDimensionRaw, MapDimensionRaw);

    private static CollisionMoveRequest Stationary(ulong entityId, int xRaw, int yRaw) =>
        new(entityId, xRaw, yRaw, xRaw, yRaw, HasProposal: false);

    private static CollisionMoveRequest Mover(
        ulong entityId,
        int startXRaw,
        int startYRaw,
        int preferredXRaw,
        int preferredYRaw) =>
        new(entityId, startXRaw, startYRaw, preferredXRaw, preferredYRaw, HasProposal: true);

    private static CollisionMoveResult ResultOf(CollisionResolver resolver, ulong entityId) =>
        resolver.Results.Single(result => result.EntityId == entityId);

    private static void AssertResult(
        CollisionResolver resolver,
        ulong entityId,
        int expectedXRaw,
        int expectedYRaw,
        MovementResolution expectedResolution) =>
        Assert.Equal(
            new CollisionMoveResult(entityId, expectedXRaw, expectedYRaw, expectedResolution),
            ResultOf(resolver, entityId));

    /// <summary>
    /// Brute-force check of the authoritative post-tick invariant over every
    /// ordered pair of committed positions, independent of the uniform grid.
    /// </summary>
    private static void AssertNoOverlap(CollisionResolver resolver)
    {
        var results = resolver.Results;

        for (var left = 0; left < results.Count; left++)
        {
            for (var right = left + 1; right < results.Count; right++)
            {
                Assert.False(
                    CollisionGeometry.Overlaps(
                        results[left].XRaw,
                        results[left].YRaw,
                        results[right].XRaw,
                        results[right].YRaw,
                        BodyRadiusRaw),
                    $"Entities {results[left].EntityId} and {results[right].EntityId} " +
                    "finished the tick overlapping.");
            }
        }
    }

    /// <summary>
    /// Small hand-built scenarios that between them exercise all six
    /// <see cref="MovementResolution"/> values.
    /// </summary>
    private static IEnumerable<(CollisionResolver Resolver, CollisionMoveRequest[] Requests)>
        ResolutionCoverageScenarios()
    {
        // None for the untouched stationary body, Separated for the coincident one.
        yield return (
            NewResolver(),
            [
                Stationary(1UL, 50_000, 50_000),
                Stationary(2UL, 50_000, 50_000),
                Stationary(3UL, 150_000, 150_000),
            ]);

        // Moved: an unobstructed mover.
        yield return (
            NewResolver(),
            [Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000)]);

        // Slid: the diagonal destination is blocked but the X axis is clear.
        yield return (
            NewResolver(),
            [
                Mover(
                    1UL,
                    100_000,
                    100_000,
                    100_000 + MovementSpeedRaw,
                    100_000 + MovementSpeedRaw),
                Stationary(2UL, 100_000 + MovementSpeedRaw, 111_000),
            ]);

        // Truncated: a stationary blocker leaves room for a shorter step.
        yield return (
            NewResolver(),
            [
                Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
                Stationary(9UL, 58_500, 50_000),
            ]);

        // Blocked: a head-on approach from exact tangency.
        yield return (
            NewResolver(),
            [
                Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
                Mover(
                    2UL,
                    50_000 + DiameterRaw,
                    50_000,
                    50_000 + DiameterRaw - MovementSpeedRaw,
                    50_000),
            ]);
    }

    /// <summary>
    /// Builds a packed lattice crowd whose start positions are exactly tangent, so
    /// the resolver's precondition holds — tangency is clearance, not overlap —
    /// while leaving no slack at all on either axis. Roughly three agents in four
    /// get a proposal no longer than the approved movement speed on each axis.
    /// </summary>
    /// <remarks>
    /// The project-owned <see cref="SplitMix64"/> is used because
    /// <c>System.Random</c> is banned: Microsoft does not guarantee its sequence
    /// across major .NET versions, which would make these tests irreproducible.
    /// </remarks>
    private static CollisionMoveRequest[] GenerateCrowd(ulong seed, int columns, int rows)
    {
        const int SpacingRaw = DiameterRaw;
        const int OriginRaw = 40_000;

        var random = new SplitMix64(seed);
        var requests = new CollisionMoveRequest[columns * rows];

        for (var index = 0; index < requests.Length; index++)
        {
            var startXRaw = OriginRaw + ((index % columns) * SpacingRaw);
            var startYRaw = OriginRaw + ((index / columns) * SpacingRaw);
            var hasProposal = random.NextInt(4) != 0;
            var deltaXRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;
            var deltaYRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;

            requests[index] = new CollisionMoveRequest(
                EntityId: (ulong)((index * 3) + 1),
                StartXRaw: startXRaw,
                StartYRaw: startYRaw,
                PreferredXRaw: hasProposal ? startXRaw + deltaXRaw : startXRaw,
                PreferredYRaw: hasProposal ? startYRaw + deltaYRaw : startYRaw,
                HasProposal: hasProposal);
        }

        return requests;
    }
}
