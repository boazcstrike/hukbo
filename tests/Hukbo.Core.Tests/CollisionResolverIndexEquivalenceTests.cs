using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Proves that the resolver's single committed-plus-pending index and its
/// per-mover gathered neighbourhood return exactly what the linear-scan oracle
/// <see cref="NaiveCollisionResolution"/> returns, on layouts built to stress the
/// places a spatial index can be wrong: packed crowds, exact co-location,
/// positions on and past the map edge, odd hard floors, and steps long enough to
/// leave the gathered neighbourhood's proven range.
/// </summary>
/// <remarks>
/// The oracle shares no code with the resolver beyond <see cref="CollisionGeometry"/>
/// and the request and result types. Every obstacle query in it is a loop over a
/// plain list.
/// </remarks>
public sealed class CollisionResolverIndexEquivalenceTests
{
    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;

    private const int DiameterRaw = 2 * BodyRadiusRaw;

    private const int MapWidthRaw = 14 * DiameterRaw;

    private const int MapHeightRaw = 10 * DiameterRaw;

    private const int AgentTarget = 90;

    private const int PlacementAttemptsPerAgent = 60;

    private const ulong FirstSeed = 1UL;

    private const ulong SeedCount = 150UL;

    private const int FloorPercentNone = 100;

    private const int FloorPercentToOne = 0;

    private static readonly int[] ExpectedAcceptedResolutions =
    [
        (int)MovementResolution.Moved,
        (int)MovementResolution.Truncated,
        (int)MovementResolution.Slid,
    ];

    /// <param name="floorPercent">
    /// 100 builds the resolver with no hard floor. 0 builds it with a floor of
    /// one raw unit. Anything else is that percentage of the diameter, forced odd
    /// so the resolver's round-up of an odd floor is exercised.
    /// </param>
    /// <param name="stepRadii">
    /// The per-axis step bound in body radii. Three is far beyond what scenario
    /// validation allows and pushes movers past the gathered neighbourhood's
    /// proven range, which must fall back to direct queries without a change in
    /// any result.
    /// </param>
    [Theory]
    [InlineData(FloorPercentNone, 1)]
    [InlineData(85, 1)]
    [InlineData(50, 1)]
    [InlineData(FloorPercentToOne, 1)]
    [InlineData(FloorPercentNone, 3)]
    [InlineData(85, 3)]
    public void Resolve_MatchesTheLinearScanOracleOnEverySeededCrowd(
        int floorPercent,
        int stepRadii)
    {
        var hardFloorRaw = ToHardFloor(floorPercent);
        var overlapDistanceRaw = ((hardFloorRaw ?? DiameterRaw) + 1) / 2 * 2;

        // One resolver across every seed: a warm resolver is the production case,
        // and it is where stale tags and removed slots would show.
        var resolver = new CollisionResolver(
            BodyRadiusRaw,
            MapWidthRaw,
            MapHeightRaw,
            hardFloorRaw);

        for (var seed = FirstSeed; seed < FirstSeed + SeedCount; seed++)
        {
            var requests = GenerateCrowd(seed, overlapDistanceRaw, stepRadii * BodyRadiusRaw);

            resolver.Resolve(requests);

            var expected = NaiveCollisionResolution.Resolve(
                requests,
                BodyRadiusRaw,
                MapWidthRaw,
                MapHeightRaw,
                hardFloorRaw);

            Assert.Equal(expected.Length, resolver.Results.Count);

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.True(
                    expected[index] == resolver.Results[index],
                    $"seed {seed} request {index}: expected {expected[index]}, got {resolver.Results[index]}");
            }

            Assert.Equal(
                expected.Count(result =>
                    ExpectedAcceptedResolutions.Contains((int)result.Resolution)),
                resolver.AcceptedMoveCount);
            Assert.Equal(
                expected.Count(result => result.Resolution == MovementResolution.Blocked),
                resolver.BlockedCount);
        }
    }

    /// <summary>
    /// The co-location repair asks about committed bodies only. A mover still
    /// pending at the same centre as a stationary body is an obstacle to every
    /// candidate, but it is not a committed body, so it must not make the
    /// stationary body count as co-located and be displaced.
    /// </summary>
    [Fact]
    public void Resolve_DoesNotSeparateAStationaryBodySharingACentreWithAPendingMover()
    {
        const int centreRaw = 5 * DiameterRaw;
        var resolver = new CollisionResolver(BodyRadiusRaw, MapWidthRaw, MapHeightRaw);

        resolver.Resolve(
        [
            Stationary(1UL, centreRaw, centreRaw),
            Mover(2UL, centreRaw, centreRaw, centreRaw + BodyRadiusRaw, centreRaw),
        ]);

        Assert.Equal(
            new CollisionMoveResult(1UL, centreRaw, centreRaw, MovementResolution.None),
            resolver.Results[0]);
    }

    /// <summary>
    /// Positive control for the test above: two stationary bodies at one centre
    /// are both committed, so the second is co-located with a committed body and
    /// is displaced by one diameter.
    /// </summary>
    [Fact]
    public void Resolve_SeparatesAStationaryBodySharingACentreWithACommittedBody()
    {
        const int centreRaw = 5 * DiameterRaw;
        var resolver = new CollisionResolver(BodyRadiusRaw, MapWidthRaw, MapHeightRaw);

        resolver.Resolve(
        [
            Stationary(1UL, centreRaw, centreRaw),
            Stationary(2UL, centreRaw, centreRaw),
        ]);

        Assert.Equal(MovementResolution.None, resolver.Results[0].Resolution);
        Assert.Equal(MovementResolution.Separated, resolver.Results[1].Resolution);
        Assert.Equal(centreRaw + DiameterRaw, resolver.Results[1].XRaw);
        Assert.Equal(centreRaw, resolver.Results[1].YRaw);
    }

    /// <summary>
    /// A mover that starts outside the legal centre band, here asking to stay put, has its candidates
    /// clamped up to a radius away from the start, farther than the step alone
    /// suggests. The obstacle that refuses the clamped slides is two cells from
    /// the start cell on the map edge, outside the block a step-sized gather would
    /// cover, so the resolver must query the grid directly for such a mover.
    /// </summary>
    [Fact]
    public void Resolve_RefusesClampedCandidatesFromAMoverStartingOutsideTheLegalBand()
    {
        const int rowRaw = 5 * DiameterRaw;
        var obstacleXRaw = MapWidthRaw - (3 * BodyRadiusRaw) + 1;
        var requests = new[]
        {
            Stationary(1UL, obstacleXRaw, rowRaw),
            Mover(2UL, MapWidthRaw, rowRaw, MapWidthRaw, rowRaw),
        };
        var resolver = new CollisionResolver(BodyRadiusRaw, MapWidthRaw, MapHeightRaw);

        resolver.Resolve(requests);

        Assert.Equal(
            new CollisionMoveResult(2UL, MapWidthRaw, rowRaw, MovementResolution.Blocked),
            resolver.Results[1]);
        Assert.Equal(
            NaiveCollisionResolution.Resolve(requests, BodyRadiusRaw, MapWidthRaw, MapHeightRaw)[1],
            resolver.Results[1]);
    }

    private static int? ToHardFloor(int floorPercent) => floorPercent switch
    {
        FloorPercentNone => null,
        FloorPercentToOne => 1,
        _ => (DiameterRaw * floorPercent / 100) | 1,
    };

    /// <summary>
    /// Random sequential placement on a small map. A fraction of the starts sit
    /// within a radius of an edge or past it, one stationary body in ten copies
    /// another body's centre exactly, and a third of the bodies stand still.
    /// </summary>
    private static CollisionMoveRequest[] GenerateCrowd(
        ulong seed,
        int overlapDistanceRaw,
        int stepBoundRaw)
    {
        var random = new SplitMix64(seed);
        var starts = new List<(int X, int Y)>();
        var requests = new List<CollisionMoveRequest>();

        for (var attempt = 0;
            attempt < AgentTarget * PlacementAttemptsPerAgent && requests.Count < AgentTarget;
            attempt++)
        {
            var hasProposal = random.NextInt(3) != 0;
            var duplicate = !hasProposal && starts.Count > 0 && random.NextInt(10) == 0;
            (int X, int Y) start = duplicate
                ? starts[random.NextInt(starts.Count)]
                : (EdgeBiased(random, MapWidthRaw), EdgeBiased(random, MapHeightRaw));

            if (!duplicate && OverlapsAny(starts, start))
            {
                continue;
            }

            starts.Add(start);
            requests.Add(new CollisionMoveRequest(
                EntityId: (ulong)requests.Count + 1UL,
                StartXRaw: start.X,
                StartYRaw: start.Y,
                PreferredXRaw: hasProposal ? start.X + Step(random, stepBoundRaw) : start.X,
                PreferredYRaw: hasProposal ? start.Y + Step(random, stepBoundRaw) : start.Y,
                HasProposal: hasProposal,
                PriorityKey: (ulong)random.NextInt(8) << 32));
        }

        return [.. requests];

        bool OverlapsAny(List<(int X, int Y)> placed, (int X, int Y) candidate) =>
            placed.Any(other =>
                CollisionGeometry.SquaredDistance(candidate.X, candidate.Y, other.X, other.Y) <
                (long)overlapDistanceRaw * overlapDistanceRaw);
    }

    /// <summary>
    /// A coordinate on <c>[0, dimensionRaw]</c>, with one draw in three confined
    /// to a diameter of either edge so clamping and out-of-band starts are common.
    /// </summary>
    private static int EdgeBiased(SplitMix64 random, int dimensionRaw)
    {
        var offsetRaw = random.NextInt(DiameterRaw + 1);

        return random.NextInt(6) switch
        {
            0 => offsetRaw,
            1 => dimensionRaw - offsetRaw,
            _ => random.NextInt(dimensionRaw + 1),
        };
    }

    private static int Step(SplitMix64 random, int boundRaw) =>
        random.NextInt((2 * boundRaw) + 1) - boundRaw;

    private static CollisionMoveRequest Stationary(ulong entityId, int xRaw, int yRaw) =>
        new(entityId, xRaw, yRaw, xRaw, yRaw, HasProposal: false, PriorityKey: entityId);

    private static CollisionMoveRequest Mover(
        ulong entityId,
        int startXRaw,
        int startYRaw,
        int preferredXRaw,
        int preferredYRaw) =>
        new(
            entityId,
            startXRaw,
            startYRaw,
            preferredXRaw,
            preferredYRaw,
            HasProposal: true,
            PriorityKey: entityId);
}
