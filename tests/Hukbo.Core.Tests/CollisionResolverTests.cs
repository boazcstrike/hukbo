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
/// policy decision record: one common body radius of 4.25 world units, a
/// diameter of nine, and a movement speed of three, so a mover can never cover
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

        // The stationary body sits one diameter plus half a movement step past
        // the mover's start: clear of the mover's start (precondition), but
        // inside the mover's full preferred step, leaving exactly the rung-one
        // truncated candidate as the first legal one.
        var stationaryXRaw = 50_000 + DiameterRaw + (MovementSpeedRaw / 2);

        resolver.Resolve(
        [
            Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
            Stationary(9UL, stationaryXRaw, 50_000),
        ]);

        var mover = ResultOf(resolver, 1UL);

        AssertResult(resolver, 9UL, stationaryXRaw, 50_000, MovementResolution.None);
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

        // The gap between the two starts is one diameter plus one and a half
        // movement steps: wide enough that the lower entity ID's full preferred
        // step still clears the higher ID's pending start (so it commits
        // unchanged), narrow enough that the higher ID's own full step would
        // then overlap the committed lower body and must truncate.
        var startXRaw = 50_000 + DiameterRaw + MovementSpeedRaw + (MovementSpeedRaw / 2);

        resolver.Resolve(
        [
            Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
            Mover(2UL, startXRaw, 50_000, startXRaw - MovementSpeedRaw, 50_000),
        ]);

        var advanced = ResultOf(resolver, 1UL);
        var truncated = ResultOf(resolver, 2UL);

        AssertResult(resolver, 1UL, 50_000 + MovementSpeedRaw, 50_000, MovementResolution.Moved);
        Assert.Equal(MovementResolution.Truncated, truncated.Resolution);
        Assert.True(truncated.XRaw < startXRaw, "The converging mover did not advance.");
        Assert.True(
            startXRaw - truncated.XRaw < advanced.XRaw - 50_000,
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
        // Each starting point sits exactly one diameter from the *other*
        // mover's own final destination -- legally touching, not overlapping
        // -- so the losing mover has no legal candidate at all and is
        // Blocked outright rather than truncated onto a short legal step.
        // Widened for task C1 (docs/plans/2026-07-28-collision-report-and-
        // shell.md), which enlarged the diameter from 8,192 raw to 9,216; the
        // old literals 90,000 and 106,192 were exactly the other mover's
        // final destination minus and plus the old diameter.
        var lowerStartYRaw = (98_000 + 192) - DiameterRaw;
        var upperStartYRaw = 98_000 + DiameterRaw;
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

    /// <summary>
    /// Guards the no-copy contract on <c>CollisionResolver.Grow</c>: growing a
    /// per-tick buffer replaces it with a fresh array instead of copying the
    /// old one, which is only correct because <c>Reset</c> refills every slot
    /// that array exposes before anything reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first <see cref="CollisionResolver.Resolve"/> call uses 36 requests,
    /// below the resolver's initial buffer capacity of 64, so its mover buffers
    /// keep their construction-time array and end the call holding real,
    /// non-default priority keys and request indices in their mover prefix.
    /// </para>
    /// <para>
    /// The second call uses 72 requests -- every one of them a mover, and past
    /// the 64-slot capacity -- which forces <c>Grow</c> to replace every
    /// per-tick buffer with a brand new, larger array. A correct <c>Reset</c>
    /// then writes every slot of that new array this tick's read loops touch.
    /// </para>
    /// <para>
    /// This is the failure mode the test rules out: if <c>Reset</c>'s refill
    /// loop were ever shortened, skipped, or bounded incorrectly after a grow,
    /// the untouched slots of the fresh array would keep the CLR's own zero
    /// default rather than this tick's data. For the mover-index buffer, a
    /// zero default means every one of those movers would read back request
    /// index 0 -- so <c>Commit</c> would only ever be called for request 0,
    /// and every other request in this all-mover crowd would keep its own
    /// <c>_results</c> slot at its own array default: entity ID 0 and
    /// <see cref="MovementResolution.None"/>. Both assertions below would
    /// immediately catch that: the entity ID would no longer match its own
    /// request, and a mover -- which always resolves to
    /// <see cref="MovementResolution.Moved"/>,
    /// <see cref="MovementResolution.Slid"/>,
    /// <see cref="MovementResolution.Truncated"/>, or
    /// <see cref="MovementResolution.Blocked"/> -- would report
    /// <see cref="MovementResolution.None"/> instead, a resolution only a
    /// stationary body can legitimately report.
    /// </para>
    /// </remarks>
    [Fact]
    public void Resolve_RefillsEveryGrownBufferSlotBeforeAnyReadOfIt()
    {
        var resolver = NewResolver();

        resolver.Resolve(GenerateCrowd(seed: 5UL, columns: 6, rows: 6));

        var requests = GenerateAllMoverCrowd(seed: 5UL, columns: 9, rows: 8);

        resolver.Resolve(requests);

        Assert.Equal(requests.Length, resolver.Results.Count);
        AssertNoOverlap(resolver);

        for (var index = 0; index < requests.Length; index++)
        {
            var result = resolver.Results[index];

            Assert.Equal(requests[index].EntityId, result.EntityId);
            Assert.NotEqual(MovementResolution.None, result.Resolution);
        }
    }

    /// <summary>
    /// The resolver orders movers by their priority key, not by their entity ID.
    /// Two allies converge on ground only one of them can take; giving the
    /// higher-ID mover the lower key hands it the ground, which the old
    /// ascending-ID order could never do.
    /// </summary>
    [Fact]
    public void ContestedGroundFollowsThePriorityKeyRatherThanTheEntityId()
    {
        const int contestedXRaw = 40 * FixedPoint.Scale;
        const int contestedYRaw = 40 * FixedPoint.Scale;
        var resolver = NewResolver();

        // Entity 2 carries the lower key, so it resolves first and takes the
        // ground even though entity 1 would have won under ascending entity ID.
        // The keys differ in their high half, which is the half the resolver
        // reads; it stamps the entity ID into the low half itself.
        var requests = new[]
        {
            Mover(
                1,
                contestedXRaw - DiameterRaw,
                contestedYRaw,
                contestedXRaw,
                contestedYRaw,
                priorityKey: 20UL << 32),
            Mover(
                2,
                contestedXRaw + DiameterRaw,
                contestedYRaw,
                contestedXRaw,
                contestedYRaw,
                priorityKey: 10UL << 32),
        };

        resolver.Resolve(requests);

        Assert.Equal(MovementResolution.Moved, ResultOf(resolver, 2).Resolution);
        Assert.NotEqual(MovementResolution.Moved, ResultOf(resolver, 1).Resolution);
        Assert.Equal(contestedXRaw, ResultOf(resolver, 2).XRaw);
    }

    // ------------------------------------------------------------- helpers

    /// <summary>
    /// The acceptance test for the collision scaling work. The resolver answers
    /// its obstacle queries through two bounded uniform-grid lookups rather than
    /// two linear scans, on the argument that an existential question over a
    /// finite set cannot depend on how the set is enumerated. If that argument
    /// holds, the entire result list is unchanged — positions and
    /// <see cref="MovementResolution"/> values alike, in request order — and this
    /// asserts exactly that against <see cref="NaiveCollisionResolution"/>.
    /// </summary>
    /// <remarks>
    /// This is deliberately stronger than the recorded state hashes and localizes
    /// a failure to the resolver rather than to the tick as a whole. Both layouts
    /// are exercised: a jittered lattice where most movers have room, and a
    /// tangent-packed lattice where most do not and the truncation ladder and the
    /// hold-position fallback carry the work.
    /// </remarks>
    [Fact]
    public void Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout()
    {
        for (ulong seed = 1UL; seed <= 24UL; seed++)
        {
            foreach (var packed in new[] { false, true })
            {
                var requests = GenerateLayout(seed, columns: 8, packed);
                var resolver = NewResolver();

                resolver.Resolve(requests);

                var expected = NaiveCollisionResolution.Resolve(
                    requests,
                    BodyRadiusRaw,
                    MapDimensionRaw,
                    MapDimensionRaw);

                Assert.Equal(expected.Length, resolver.Results.Count);

                for (var index = 0; index < expected.Length; index++)
                {
                    Assert.Equal(expected[index], resolver.Results[index]);
                }

                AssertNoOverlap(resolver);
            }
        }
    }

    /// <summary>
    /// Pins the boundary of the pending index, which is the one place the
    /// scaling work can hide a bug: a mover is removed from the index at the top
    /// of its own iteration, so the index holds exactly the movers that have not
    /// resolved yet.
    /// </summary>
    /// <remarks>
    /// Both failure directions are observable and both are asserted. Remove a
    /// mover too late and it is an obstacle to itself, so a mover alone on an
    /// empty field cannot move at all. Remove it too early — taking the next
    /// mover with it — and a mover can claim ground that a later mover has not
    /// vacated, which the later mover's hold-position fallback then overlaps.
    /// </remarks>
    [Fact]
    public void Resolve_KeepsThePendingSetExactlyAtTheMoversThatHaveNotResolved()
    {
        // Too late: a lone mover with the whole map to itself must move. It is
        // pending at its own start position, and if that were still visible to
        // its own candidate test every rung would be refused.
        var lone = NewResolver();

        lone.Resolve([Mover(1UL, 50 * FixedPoint.Scale, 50 * FixedPoint.Scale,
            (50 * FixedPoint.Scale) + MovementSpeedRaw, 50 * FixedPoint.Scale)]);

        AssertResult(
            lone,
            1UL,
            (50 * FixedPoint.Scale) + MovementSpeedRaw,
            50 * FixedPoint.Scale,
            MovementResolution.Moved);

        // Too early: two movers approaching head-on from exact tangency. The
        // first resolved must not take ground the second has not left, and the
        // second's fallback is to hold its start position, so an over-eager
        // removal shows up as a strict overlap between the two committed bodies.
        var headOn = NewResolver();
        var leftXRaw = 50 * FixedPoint.Scale;
        var rightXRaw = leftXRaw + DiameterRaw;

        headOn.Resolve(
        [
            Mover(1UL, leftXRaw, 50 * FixedPoint.Scale,
                leftXRaw + MovementSpeedRaw, 50 * FixedPoint.Scale),
            Mover(2UL, rightXRaw, 50 * FixedPoint.Scale,
                rightXRaw - MovementSpeedRaw, 50 * FixedPoint.Scale),
        ]);

        AssertNoOverlap(headOn);
    }

    /// <summary>
    /// Builds a legal request list on a lattice. The resolver's precondition is
    /// that no two start positions strictly overlap, so the spacing and the
    /// jitter are chosen to guarantee it rather than to be checked afterwards:
    /// at a lattice pitch of <c>2 * D</c> and a per-axis jitter bounded by
    /// <c>D / 2 - 1</c>, two neighbours are at least <c>D + 2</c> apart, and the
    /// packed variant places bodies at exactly one diameter, which is tangency
    /// and therefore legal.
    /// </summary>
    private static CollisionMoveRequest[] GenerateLayout(
        ulong seed,
        int columns,
        bool packed)
    {
        var random = new SplitMix64(seed);
        var pitchRaw = packed ? DiameterRaw : 2 * DiameterRaw;
        var jitterRaw = packed ? 0 : (DiameterRaw / 2) - 1;
        var requests = new CollisionMoveRequest[columns * columns];

        for (var index = 0; index < requests.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var startXRaw = DiameterRaw + (column * pitchRaw) +
                (jitterRaw == 0 ? 0 : random.NextInt((2 * jitterRaw) + 1) - jitterRaw);
            var startYRaw = DiameterRaw + (row * pitchRaw) +
                (jitterRaw == 0 ? 0 : random.NextInt((2 * jitterRaw) + 1) - jitterRaw);

            // One agent in five stands still, so both resolver passes run.
            var hasProposal = random.NextInt(5) != 0;
            var stepXRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;
            var stepYRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;

            requests[index] = new CollisionMoveRequest(
                EntityId: (ulong)index + 1UL,
                StartXRaw: startXRaw,
                StartYRaw: startYRaw,
                PreferredXRaw: hasProposal ? startXRaw + stepXRaw : startXRaw,
                PreferredYRaw: hasProposal ? startYRaw + stepYRaw : startYRaw,
                HasProposal: hasProposal,

                // A high half that does not track the entity ID, so the
                // resolution order is a genuine shuffle rather than ascending ID.
                PriorityKey: (ulong)random.NextInt(int.MaxValue) << 32);
        }

        return requests;
    }

    private static CollisionResolver NewResolver() =>
        new(BodyRadiusRaw, MapDimensionRaw, MapDimensionRaw);

    private static CollisionMoveRequest Stationary(ulong entityId, int xRaw, int yRaw) =>
        new(entityId, xRaw, yRaw, xRaw, yRaw, HasProposal: false, PriorityKey: entityId);

    /// <summary>
    /// A mover whose contested-ground priority equals its entity ID, so these
    /// fixtures keep expressing the old ascending-ID order explicitly. The
    /// battle simulation supplies a per-tick shuffled key instead; that the
    /// resolver honours the key rather than the ID is asserted separately.
    /// </summary>
    private static CollisionMoveRequest Mover(
        ulong entityId,
        int startXRaw,
        int startYRaw,
        int preferredXRaw,
        int preferredYRaw,
        ulong? priorityKey = null) =>
        new(
            entityId,
            startXRaw,
            startYRaw,
            preferredXRaw,
            preferredYRaw,
            HasProposal: true,
            PriorityKey: priorityKey ?? entityId);

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

        // Truncated: a stationary blocker leaves room for a shorter step. Same
        // geometry as Resolve_KeepsTheGroundOfAStationaryAgentWithAHigherEntityId:
        // one diameter plus half a movement step past the mover's start.
        yield return (
            NewResolver(),
            [
                Mover(1UL, 50_000, 50_000, 50_000 + MovementSpeedRaw, 50_000),
                Stationary(9UL, 50_000 + DiameterRaw + (MovementSpeedRaw / 2), 50_000),
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
                HasProposal: hasProposal,
                // A real per-tick key, not the entity ID: the randomized
                // invariant tests must fuzz the shuffled resolution order the
                // battle actually uses, not the ascending-ID order it retired.
                // This consumes no draw, so the crowd itself is unchanged.
                PriorityKey: CollisionPriority.Resolve(
                    seed: 1,
                    tick: 1,
                    entityId: (ulong)((index * 3) + 1)));
        }

        return requests;
    }

    /// <summary>
    /// The same tangent lattice as <see cref="GenerateCrowd"/>, except every
    /// request is a mover. Used to drive <see cref="CollisionResolver"/> past
    /// its per-tick buffers' initial capacity while keeping every request's
    /// resolution meaningfully checkable: a mover always resolves to
    /// <see cref="MovementResolution.Moved"/>,
    /// <see cref="MovementResolution.Slid"/>,
    /// <see cref="MovementResolution.Truncated"/>, or
    /// <see cref="MovementResolution.Blocked"/>, never
    /// <see cref="MovementResolution.None"/>, which a stationary body can
    /// report.
    /// </summary>
    private static CollisionMoveRequest[] GenerateAllMoverCrowd(ulong seed, int columns, int rows)
    {
        const int SpacingRaw = DiameterRaw;
        const int OriginRaw = 40_000;

        var random = new SplitMix64(seed);
        var requests = new CollisionMoveRequest[columns * rows];

        for (var index = 0; index < requests.Length; index++)
        {
            var startXRaw = OriginRaw + ((index % columns) * SpacingRaw);
            var startYRaw = OriginRaw + ((index / columns) * SpacingRaw);
            var deltaXRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;
            var deltaYRaw = random.NextInt((2 * MovementSpeedRaw) + 1) - MovementSpeedRaw;

            requests[index] = new CollisionMoveRequest(
                EntityId: (ulong)((index * 3) + 1),
                StartXRaw: startXRaw,
                StartYRaw: startYRaw,
                PreferredXRaw: startXRaw + deltaXRaw,
                PreferredYRaw: startYRaw + deltaYRaw,
                HasProposal: true,
                PriorityKey: CollisionPriority.Resolve(
                    seed: 1,
                    tick: 1,
                    entityId: (ulong)((index * 3) + 1)));
        }

        return requests;
    }
}
