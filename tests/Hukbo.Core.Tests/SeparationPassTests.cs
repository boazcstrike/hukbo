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

    // Stiffness 100% and a cap of 60% of a radius (2,764 raw, above every
    // push the fixtures below expect, so none is capped), with no sideways push,
    // so the expected integers are the bare weight share. The bound
    // floor - 2 * cap is 2,305, which Validate requires to be positive.
    private static SeparationParameters Plain() =>
        new(
            BodyRadiusRaw: Radius,
            SoftBandBasisPoints: 1_500,
            PushCapRadiusBasisPoints: 6_000,
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
        // R = 4608, cap = 230, B = 7373, m = 4. Body 2 averages a 2,108 push from
        // body 1 and a 858 push the other way into 625, which the cap cuts to 230
        // toward body 3 at d = 7,500. Then
        //   allow  = (7500 - 7373 - 4) * 7500 / 2 = 461,250
        //   toward = 230 * 7500                   = 1,725,000
        //   delta  = 230 * 461,250 / 1,725,000    = 61.5, truncated to 61.
        var parameters = Capped();
        Assert.Equal(230, parameters.PushCapRaw);
        Assert.Equal(7_373, Bound(parameters));

        var result = RunPass(
            [
                new Body(1, Centre - 5_000, Centre),
                new Body(2, Centre, Centre),
                new Body(3, Centre + 7_500, Centre),
            ],
            parameters);

        Assert.Equal((Centre + 61, Centre), result[2]);
    }

    // Found by a bounded scan over weight-1 triples against the pass before the
    // post-scale check existed, then pinned. Body 2 sits in the arena corner, so
    // the clamp holds it still. Body 1 starts 7,373.004 from it (B = 7,373,
    // squared distance B^2 + 60). Bodies 2 and 3 push body 1 to (-11, 5), which
    // is not toward body 2, so body 2 is skipped when the scale is chosen. Body 3
    // at 7,378 forces a scale far below one, and truncating (-11, 5) * f gave
    // (-4, 1), which is toward body 2 and closed the pair to 7,372.80.
    [Fact]
    public void TruncatingTheScaleCannotPushAPairBelowTheBound()
    {
        var parameters = Capped();
        var bound = Bound(parameters);
        Assert.Equal(7_373, bound);

        var pass = new SeparationPass();
        var bodies = new List<Body>
        {
            new(1, Radius + 2_142, Radius + 7_055),
            new(2, Radius, Radius),
            new(3, Radius + 2_142 + 2_336, Radius + 7_055 + 6_999),
        };
        Assert.Equal(
            (bound * bound) + 60,
            Squared(bodies[0].X - bodies[1].X, bodies[0].Y - bodies[1].Y));

        var result = RunPass(pass, bodies, parameters);

        AssertEveryPairAtLeast(bodies, result, bound, seed: 0);
        Assert.Equal((Radius + 2_142, Radius + 7_055), result[1]);
        Assert.Equal((Radius, Radius), result[2]);
        Assert.True(pass.LimitedBodyCount > 0, "The inward-motion limit never engaged.");
    }

    // Body 1 starts 100 raw inside the left edge and is pushed to (-217, 73)
    // (length 229, under the cap of 230). The clamp cuts that to (-100, 73),
    // and body 3 at d = 7,500 then scales it:
    //   allow  = (7500 - 7373 - 4) * 7500 / 2     = 461,250
    //   toward = (-100 * -100) + (73 * 7500)      = 557,500
    //   delta  = (-100, 73) * 461,250 / 557,500   = (-82.7, 60.4) -> (-82, 60).
    // Limiting before the clamp, or never clamping, would scale (-217, 73) and
    // land on (-175, 59) instead.
    [Fact]
    public void TheArenaClampRunsBeforeTheInwardLimit()
    {
        var pass = new SeparationPass();
        var bodies = new List<Body>
        {
            new(1, Radius + 100, Centre, Weight: 100),
            new(2, Radius + 100 + 5_500, Centre - 5_800, Weight: 100),
            new(3, Radius, Centre + 7_500, Weight: 1),
        };

        var result = RunPass(pass, bodies, Capped());

        Assert.Equal((-217, 73), (pass.PushX[0], pass.PushY[0]));
        Assert.Equal((Radius + 100 - 82, Centre + 60), result[1]);
        Assert.Equal(1, pass.LimitedBodyCount);
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
        var pass = new SeparationPass();
        var expected = RunPass(pass, bodies, parameters);
        Assert.True(pass.LimitedBodyCount > 0, "The inward-motion limit never engaged.");
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
        var parameters = Plain() with { SidewaysBasisPoints = 1_500 };

        WarmAllocationWindow.AssertEventually(
            () =>
            {
                var bodies = BuildCloud(seed: 3, count: 400, withCoincidentPairs: true);
                var x = bodies.Select(b => b.X).ToArray();
                var y = bodies.Select(b => b.Y).ToArray();
                var alive = bodies.Select(b => b.IsAlive).ToArray();
                var weight = bodies.Select(b => b.Weight).ToArray();
                var committed = bodies.Select(b => b.IsCommitted).ToArray();
                var ids = bodies.Select(b => b.Id).ToArray();
                var pass = new SeparationPass();
                pass.Run(x, y, alive, weight, committed, ids, parameters);

                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var run = 0; run < 50; run++)
                {
                    pass.Run(x, y, alive, weight, committed, ids, parameters);
                }

                return GC.GetAllocatedBytesForCurrentThread() - before;
            },
            allocatedBytes => allocatedBytes == 0
                ? null
                : $"warm passes allocated {allocatedBytes:N0} bytes; expected none.",
            "Warm separation passes");
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

    [Theory]
    [InlineData(10_000, 1_500)]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    public void ValidateRejectsAnUnusableBound(int capBasisPoints, int softBandBasisPoints)
    {
        var parameters = Plain() with
        {
            PushCapRadiusBasisPoints = capBasisPoints,
            SoftBandBasisPoints = softBandBasisPoints,
        };

        // Either the bound is below one, or bound + margin exceeds the diameter.
        Assert.True(
            parameters.BoundRaw < 1
            || parameters.BoundRaw + SeparationPass.ShrinkMarginRaw > parameters.DiameterRaw);
        Assert.Throws<ArgumentOutOfRangeException>(parameters.Validate);
    }

    [Fact]
    public void ValidateAcceptsTheEdgeOfTheUsableBound()
    {
        // No soft band and a cap of 2 raw units give B = 2R - 4, so B + 4 = 2R.
        var widest = Plain() with { SoftBandBasisPoints = 0, PushCapRadiusBasisPoints = 5 };
        Assert.Equal(2, widest.PushCapRaw);
        Assert.Equal(Diameter - 4, widest.BoundRaw);

        widest.Validate();
        Plain().Validate();
    }

    [Fact]
    public void ValidateRejectsARadiusWhoseSquaresCouldOverflow()
    {
        var parameters = Plain() with
        {
            BodyRadiusRaw = SeparationParameters.MaximumBodyRadiusRaw + 1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(parameters.Validate);
        (Plain() with { BodyRadiusRaw = SeparationParameters.MaximumBodyRadiusRaw }).Validate();
    }

    [Fact]
    public void ValidateAcceptsTheProductionValues()
    {
        var parameters = new SeparationParameters(
            BodyRadiusRaw: 4_352,
            SoftBandBasisPoints: 1_500,
            PushCapRadiusBasisPoints: 500,
            StiffnessBasisPoints: 5_000,
            CommittedWeightMultiplier: 3,
            SidewaysBasisPoints: 2_500,
            ArenaWidthRaw: Arena,
            ArenaHeightRaw: Arena);

        parameters.Validate();

        Assert.Equal(7_398, parameters.FloorRaw);
        Assert.Equal(217, parameters.PushCapRaw);
        Assert.Equal(6_964, parameters.BoundRaw);
    }

    [Fact]
    public void TwoBodiesSharingAnIdAtOnePositionAreRejectedAndLeaveTheGridClear()
    {
        var pass = new SeparationPass();
        var twins = new List<Body>
        {
            new(7, Centre, Centre),
            new(7, Centre, Centre),
        };

        Assert.Throws<InvalidOperationException>(() => RunPass(pass, twins, Plain()));
        Assert.True(pass.GridIsClear, "A failed pass left grid cells linked.");

        // The same instance must still give a fresh instance's answer.
        List<Body> pair =
        [
            new(1, Centre, Centre),
            new(2, Centre + Overlap1216Distance, Centre),
        ];
        Assert.Equal(RunPass(pair, Plain()), RunPass(pass, pair, Plain()));
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
