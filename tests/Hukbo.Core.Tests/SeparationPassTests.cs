using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// The separation pass of <c>docs/plans/2026-11-21-soft-separation-design.md</c>
/// section 3.1. Every constant below is a provisional test value, not a tuned
/// gameplay number.
/// </summary>
public sealed class SeparationPassTests
{
    private const int Radius = 4_608;
    private const int Diameter = 2 * Radius;
    private const int Arena = 102_400;
    private const int Centre = 50_000;
    private const int Overlap1216Distance = 8_000;

    private readonly record struct Body(
        ulong Id,
        int X,
        int Y,
        int Weight = 1,
        bool IsCommitted = false,
        bool IsAlive = true);

    // Stiffness 100% and a cap of one radius, with no sideways push, so the
    // expected integers are the bare weight share.
    private static SeparationParameters Plain() =>
        new(
            BodyRadiusRaw: Radius,
            SoftBandBasisPoints: 1_500,
            PushCapRadiusBasisPoints: 10_000,
            StiffnessBasisPoints: 10_000,
            CommittedWeightMultiplier: 3,
            SidewaysBasisPoints: 0,
            ArenaWidthRaw: Arena,
            ArenaHeightRaw: Arena);

    private static Dictionary<ulong, (int X, int Y)> RunPass(
        SeparationPass pass,
        IReadOnlyList<Body> bodies,
        SeparationParameters parameters)
    {
        var x = bodies.Select(b => b.X).ToArray();
        var y = bodies.Select(b => b.Y).ToArray();
        var alive = bodies.Select(b => b.IsAlive).ToArray();
        var weight = bodies.Select(b => b.Weight).ToArray();
        var committed = bodies.Select(b => b.IsCommitted).ToArray();
        var ids = bodies.Select(b => b.Id).ToArray();

        pass.Run(x, y, alive, weight, committed, ids, parameters);

        var result = new Dictionary<ulong, (int X, int Y)>();
        for (var i = 0; i < bodies.Count; i++)
        {
            result[bodies[i].Id] = (pass.OutX[i], pass.OutY[i]);
        }

        return result;
    }

    private static Dictionary<ulong, (int X, int Y)> RunPass(
        IReadOnlyList<Body> bodies,
        SeparationParameters parameters) =>
        RunPass(new SeparationPass(), bodies, parameters);

    [Fact]
    public void LightBodyMovesMoreByTheWeightShare()
    {
        var result = RunPass(
            [
                new Body(1, Centre, Centre, Weight: 3),
                new Body(2, Centre + Overlap1216Distance, Centre, Weight: 1),
            ],
            Plain());

        // overlap 1216; heavy takes 1/4, light takes 3/4.
        Assert.Equal((Centre - 304, Centre), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 912, Centre), result[2]);
    }

    [Fact]
    public void CommittedBodyCountsForTheMultiplierTimesItsWeight()
    {
        var result = RunPass(
            [
                new Body(1, Centre, Centre, Weight: 1, IsCommitted: true),
                new Body(2, Centre + Overlap1216Distance, Centre, Weight: 1),
            ],
            Plain());

        Assert.Equal((Centre - 304, Centre), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 912, Centre), result[2]);
    }

    [Fact]
    public void StiffnessScalesThePush()
    {
        var parameters = Plain() with { StiffnessBasisPoints = 5_000 };

        var result = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + Overlap1216Distance, Centre),
            ],
            parameters);

        Assert.Equal((Centre - 304, Centre), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 304, Centre), result[2]);
    }

    [Fact]
    public void PushIsAveragedOverTheContactCount()
    {
        var result = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + Overlap1216Distance, Centre),
                new Body(3, Centre, Centre + Overlap1216Distance),
            ],
            Plain());

        // Body 1 takes 608 from each neighbour; two contacts average to 304.
        Assert.Equal((Centre - 304, Centre - 304), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 608, Centre), result[2]);
        Assert.Equal((Centre, Centre + Overlap1216Distance + 608), result[3]);
    }

    [Fact]
    public void PushIsCappedAtTheRadiusFraction()
    {
        // Cap = 4608 * 1000 / 10000 = 460. The uncapped push would be 608.
        var parameters = Plain() with { PushCapRadiusBasisPoints = 1_000 };

        var result = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + Overlap1216Distance, Centre),
            ],
            parameters);

        Assert.Equal((Centre - 460, Centre), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 460, Centre), result[2]);
    }

    [Fact]
    public void DiagonalPushNeverExceedsTheCap()
    {
        var parameters = Plain() with { PushCapRadiusBasisPoints = 1_000 };
        var cap = parameters.PushCapRaw;
        var pass = new SeparationPass();

        RunPass(
            pass,
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + 5_000, Centre + 3_000),
            ],
            parameters);

        for (var i = 0; i < 2; i++)
        {
            var lengthSquared = ((long)pass.PushX[i] * pass.PushX[i])
                + ((long)pass.PushY[i] * pass.PushY[i]);
            Assert.True(lengthSquared <= cap * cap);
            Assert.True(lengthSquared > 0);
        }
    }

    [Fact]
    public void CoincidentPairSeparatesDeterministicallyAndOppositely()
    {
        var parameters = Plain() with { StiffnessBasisPoints = 5_000 };
        var first = RunPass(
            [new Body(5, Centre, Centre), new Body(9, Centre, Centre)],
            parameters);
        var swapped = RunPass(
            [new Body(9, Centre, Centre), new Body(5, Centre, Centre)],
            parameters);
        var repeated = RunPass(
            [new Body(5, Centre, Centre), new Body(9, Centre, Centre)],
            parameters);

        Assert.Equal(first, swapped);
        Assert.Equal(first, repeated);

        var lowX = first[5].X - Centre;
        var lowY = first[5].Y - Centre;
        Assert.Equal(-lowX, first[9].X - Centre);
        Assert.Equal(-lowY, first[9].Y - Centre);

        // overlap 2R, share 1/2, stiffness 1/2 gives a push of R / 2 = 2304.
        var length = Math.Sqrt(((double)lowX * lowX) + ((double)lowY * lowY));
        Assert.InRange(length, 2_300, 2_304);
    }

    [Fact]
    public void CoincidentPairDirectionVariesWithTheIds()
    {
        var directions = new HashSet<(int, int)>();
        for (ulong id = 2; id < 40; id++)
        {
            var result = RunPass(
                [new Body(1, Centre, Centre), new Body(id, Centre, Centre)],
                Plain());
            directions.Add((result[1].X - Centre, result[1].Y - Centre));
        }

        Assert.True(directions.Count > 4);
    }

    [Fact]
    public void HeadOnPairInTheSoftBandGetsOppositeSidewaysPushes()
    {
        var parameters = Plain() with { SidewaysBasisPoints = 2_000 };

        var result = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + Overlap1216Distance, Centre),
            ],
            parameters);

        // Sideways = 1216 * 2000 / 10000 = 243.2, truncated toward zero.
        Assert.Equal((Centre - 608, Centre - 243), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 608, Centre + 243), result[2]);
    }

    [Fact]
    public void SidewaysSideFollowsTheLowerIdNotTheStorageOrder()
    {
        var parameters = Plain() with { SidewaysBasisPoints = 2_000 };
        var lowOnLeft = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + Overlap1216Distance, Centre),
            ],
            parameters);
        var lowOnRight = RunPass(
            [
                new Body(2, Centre, Centre),
                new Body(1, Centre + Overlap1216Distance, Centre),
            ],
            parameters);

        Assert.Equal(lowOnLeft[1].Y - Centre, -(lowOnRight[1].Y - Centre));
        Assert.Equal(lowOnLeft[2].Y - Centre, -(lowOnRight[2].Y - Centre));
    }

    [Theory]
    [InlineData(5_000)]
    [InlineData(7_833)]
    public void NoSidewaysPushAtOrBelowTheFloor(int distance)
    {
        var parameters = Plain() with { SidewaysBasisPoints = 5_000 };
        Assert.Equal(7_833, parameters.FloorRaw);

        var result = RunPass(
            [new Body(1, Centre, Centre), new Body(2, Centre + distance, Centre)],
            parameters);

        Assert.Equal(Centre, result[1].Y);
        Assert.Equal(Centre, result[2].Y);
    }

    [Theory]
    [InlineData(Diameter)]
    [InlineData(Diameter + 1)]
    [InlineData(3 * Radius * 2)]
    public void NoPushWhenTheDistanceIsAtLeastTwoRadii(int distance)
    {
        var result = RunPass(
            [new Body(1, Centre, Centre), new Body(2, Centre + distance, Centre)],
            Plain() with { SidewaysBasisPoints = 5_000 });

        Assert.Equal((Centre, Centre), result[1]);
        Assert.Equal((Centre + distance, Centre), result[2]);
    }

    [Fact]
    public void DeadBodiesNeitherPushNorMove()
    {
        var result = RunPass(
            [
                new Body(1, Centre, Centre),
                new Body(2, Centre + 1_000, Centre, IsAlive: false),
                new Body(3, Centre + Overlap1216Distance, Centre),
            ],
            Plain());

        Assert.Equal((Centre + 1_000, Centre), result[2]);
        Assert.Equal((Centre - 608, Centre), result[1]);
        Assert.Equal((Centre + Overlap1216Distance + 608, Centre), result[3]);
    }

    [Fact]
    public void BodiesAreClampedToTheArena()
    {
        const int high = Arena - 5_000;
        var result = RunPass(
            [
                new Body(1, 5_000, Centre),
                new Body(2, 5_000 + Overlap1216Distance, Centre),
                new Body(3, high, Centre),
                new Body(4, high - Overlap1216Distance, Centre),
            ],
            Plain());

        Assert.Equal(Radius, result[1].X);
        Assert.Equal(Arena - Radius, result[3].X);
        Assert.Equal(5_000 + Overlap1216Distance + 608, result[2].X);
    }

    [Fact]
    public void StorageOrderDoesNotChangeAnyBodysResult()
    {
        var bodies = BuildCloud(seed: 7, count: 300, withCoincidentPairs: true);
        var expected = RunPass(bodies, Plain() with { SidewaysBasisPoints = 1_500 });
        var shuffler = new SplitMix64(99);

        for (var round = 0; round < 8; round++)
        {
            var shuffled = bodies.ToArray();
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var j = shuffler.NextInt(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            var actual = RunPass(
                shuffled,
                Plain() with { SidewaysBasisPoints = 1_500 });
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ADenseCloudReallyMoves()
    {
        var bodies = BuildCloud(seed: 7, count: 300, withCoincidentPairs: true);
        var result = RunPass(bodies, Plain());

        Assert.True(bodies.Count(b => result[b.Id] != (b.X, b.Y)) > 100);
    }

    [Fact]
    public void PairsAtOrAboveTheFloorStayAboveTheFloorMinusTwoCaps()
    {
        var parameters = Plain() with
        {
            SidewaysBasisPoints = 3_000,
            PushCapRadiusBasisPoints = 500,
        };
        var floor = parameters.FloorRaw;
        var bound = floor - (2 * parameters.PushCapRaw);
        var pass = new SeparationPass();
        var movedTotal = 0;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var bodies = BuildPackedCloud(seed, floor);
            var result = RunPass(pass, bodies, parameters);

            for (var a = 0; a < bodies.Count; a++)
            {
                movedTotal += result[bodies[a].Id] != (bodies[a].X, bodies[a].Y) ? 1 : 0;
                for (var b = a + 1; b < bodies.Count; b++)
                {
                    var dx = (long)result[bodies[a].Id].X - result[bodies[b].Id].X;
                    var dy = (long)result[bodies[a].Id].Y - result[bodies[b].Id].Y;
                    Assert.True(
                        (dx * dx) + (dy * dy) >= bound * bound,
                        $"seed {seed}: bodies {bodies[a].Id} and {bodies[b].Id} closer than {bound}.");
                }
            }
        }

        Assert.True(movedTotal > 0, "The packed clouds never exercised the pass.");
    }

    // A realistic cap, so the bound B = floor - 2 * cap is positive and the
    // inward-motion limit is live.
    private static SeparationParameters Capped() =>
        Plain() with { PushCapRadiusBasisPoints = 500, SidewaysBasisPoints = 2_500 };

    private static long Bound(SeparationParameters parameters) =>
        parameters.FloorRaw - (2 * parameters.PushCapRaw);

    private static long DistanceOf(
        Dictionary<ulong, (int X, int Y)> result,
        ulong first,
        ulong second) =>
        FixedIntegerSquareRoot(Squared(
            (long)result[first].X - result[second].X,
            (long)result[first].Y - result[second].Y));

    private static long FixedIntegerSquareRoot(long value) =>
        (long)Math.Floor(Math.Sqrt(value));

    [Fact]
    public void APairAtTheBoundStaysAtTheBoundWhenThirdPartiesSqueezeFromBothSides()
    {
        var parameters = Capped();
        var bound = (int)Bound(parameters);

        // Each outer body overlaps its neighbour deeply and, averaged with the
        // pair's own mild push, drives it toward the other half of the pair.
        var result = RunPass(
            [
                new Body(1, Centre - 6_000, Centre),
                new Body(2, Centre, Centre),
                new Body(3, Centre + bound, Centre),
                new Body(4, Centre + bound + 6_000, Centre),
            ],
            parameters);

        Assert.True(DistanceOf(result, 2, 3) >= bound);
        Assert.Equal((Centre, Centre), result[2]);
        Assert.Equal((Centre + bound, Centre), result[3]);
    }

    [Fact]
    public void NoInwardPushIsAppliedToAPairBelowTheBoundPlusTheMargin()
    {
        var parameters = Capped();
        var bound = (int)Bound(parameters);
        var start = bound + SeparationPass.ShrinkMarginRaw - 1;

        var result = RunPass(
            [
                new Body(1, Centre - 6_000, Centre),
                new Body(2, Centre, Centre),
                new Body(3, Centre + start, Centre),
            ],
            parameters);

        // Body 2 would be pushed toward body 3 by body 1; allow is zero, so it
        // is held exactly, not merely slowed.
        Assert.Equal((Centre, Centre), result[2]);
    }

    [Fact]
    public void AnInwardPushAboveTheAllowanceIsScaledNotDropped()
    {
        var parameters = Capped();
        const int distance = 7_500;
        var bound = Bound(parameters);
        var cap = parameters.PushCapRaw;
        var allow = (distance - bound - SeparationPass.ShrinkMarginRaw) * distance / 2;
        var toward = cap * distance;
        Assert.True(toward > allow && allow > 0, "The fixture must need scaling.");

        var result = RunPass(
            [
                new Body(1, Centre - 5_000, Centre),
                new Body(2, Centre, Centre),
                new Body(3, Centre + distance, Centre),
            ],
            parameters);

        Assert.Equal((int)(Centre + (cap * allow / toward)), result[2].X);
    }

    [Fact]
    public void ScaledPushesStayInsideTheArenaAndKeepTheBound()
    {
        var parameters = Capped();
        var bound = Bound(parameters);
        var pass = new SeparationPass();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var bodies = BuildPackedCloud(seed, bound, Radius + 2_000);
            var result = RunPass(pass, bodies, parameters);

            foreach (var body in bodies)
            {
                Assert.InRange(result[body.Id].X, Radius, Arena - Radius);
                Assert.InRange(result[body.Id].Y, Radius, Arena - Radius);
            }

            AssertEveryPairAtLeast(bodies, result, bound, seed);
        }
    }

    [Fact]
    public void PairsSpacedAtTheBoundStayAtTheBoundWithoutAnyHelp()
    {
        var parameters = Capped();
        var bound = Bound(parameters);
        var pass = new SeparationPass();
        var movedTotal = 0;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var bodies = BuildPackedCloud(seed, bound, Centre - (60 * 1_024 / 2));
            var result = RunPass(pass, bodies, parameters);

            movedTotal += bodies.Count(b => result[b.Id] != (b.X, b.Y));
            AssertEveryPairAtLeast(bodies, result, bound, seed);
        }

        Assert.True(movedTotal > 0, "The packed clouds never exercised the pass.");
    }

    [Fact]
    public void StorageOrderDoesNotChangeAnyResultWhileTheInwardLimitIsLive()
    {
        var parameters = Capped();
        var bodies = BuildPackedCloud(seed: 5, Bound(parameters), Centre - (60 * 1_024 / 2));
        var expected = RunPass(bodies, parameters);
        var shuffler = new SplitMix64(123);

        for (var round = 0; round < 8; round++)
        {
            var shuffled = bodies.ToArray();
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var j = shuffler.NextInt(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            Assert.Equal(expected, RunPass(shuffled, parameters));
        }

        Assert.True(bodies.Count(b => expected[b.Id] != (b.X, b.Y)) > 0);
    }

    private static void AssertEveryPairAtLeast(
        List<Body> bodies,
        Dictionary<ulong, (int X, int Y)> result,
        long bound,
        ulong seed)
    {
        for (var a = 0; a < bodies.Count; a++)
        {
            for (var b = a + 1; b < bodies.Count; b++)
            {
                var dx = (long)result[bodies[a].Id].X - result[bodies[b].Id].X;
                var dy = (long)result[bodies[a].Id].Y - result[bodies[b].Id].Y;
                Assert.True(
                    (dx * dx) + (dy * dy) >= bound * bound,
                    $"seed {seed}: bodies {bodies[a].Id} and {bodies[b].Id} closer than {bound}.");
            }
        }
    }

    [Fact]
    public void WarmPassAllocatesNothing()
    {
        var bodies = BuildCloud(seed: 3, count: 400, withCoincidentPairs: true);
        var x = bodies.Select(b => b.X).ToArray();
        var y = bodies.Select(b => b.Y).ToArray();
        var alive = bodies.Select(b => b.IsAlive).ToArray();
        var weight = bodies.Select(b => b.Weight).ToArray();
        var committed = bodies.Select(b => b.IsCommitted).ToArray();
        var ids = bodies.Select(b => b.Id).ToArray();
        var parameters = Plain() with { SidewaysBasisPoints = 1_500 };
        var pass = new SeparationPass();
        pass.Run(x, y, alive, weight, committed, ids, parameters);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 0; run < 50; run++)
        {
            pass.Run(x, y, alive, weight, committed, ids, parameters);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void RejectsInvalidInput()
    {
        var pass = new SeparationPass();
        int[] one = [Centre];
        bool[] yes = [true];
        ulong[] id = [1];

        Assert.Throws<ArgumentException>(() =>
            pass.Run(one, [], yes, one, yes, id, Plain()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pass.Run(one, one, yes, [0], yes, id, Plain()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pass.Run(one, one, yes, one, yes, id, Plain() with { BodyRadiusRaw = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pass.Run(
                one,
                one,
                yes,
                one,
                yes,
                id,
                Plain() with { StiffnessBasisPoints = 10_001 }));
    }

    // ----- Helpers -----

    private static List<Body> BuildCloud(ulong seed, int count, bool withCoincidentPairs)
    {
        var random = new SplitMix64(seed);
        var bodies = new List<Body>(count);
        for (var i = 0; i < count; i++)
        {
            // A 50 x 50 unit patch is dense enough for every body to have contacts.
            var x = Centre + random.NextInt(50 * 1_024);
            var y = Centre + random.NextInt(50 * 1_024);
            if (withCoincidentPairs && i % 25 == 1)
            {
                x = bodies[i - 1].X;
                y = bodies[i - 1].Y;
            }

            bodies.Add(new Body(
                (ulong)(i + 1),
                x,
                y,
                Weight: 1 + random.NextInt(5),
                IsCommitted: random.NextInt(4) == 0,
                IsAlive: random.NextInt(10) != 0));
        }

        return bodies;
    }

    private static List<Body> BuildPackedCloud(ulong seed, long floor) =>
        BuildPackedCloud(seed, floor, Centre - (60 * 1_024 / 2));

    private static List<Body> BuildPackedCloud(ulong seed, long floor, int origin)
    {
        var random = new SplitMix64(seed);
        var bodies = new List<Body>();
        var span = 60 * 1_024;
        for (var attempt = 0; attempt < 4_000; attempt++)
        {
            var x = origin + random.NextInt(span);
            var y = origin + random.NextInt(span);
            if (bodies.Any(b => Squared(b.X - x, b.Y - y) < floor * floor))
            {
                continue;
            }

            bodies.Add(new Body(
                (ulong)(bodies.Count + 1),
                x,
                y,
                Weight: 1 + random.NextInt(5),
                IsCommitted: random.NextInt(3) == 0));
        }

        return bodies;
    }

    private static long Squared(long dx, long dy) => (dx * dx) + (dy * dy);
}
