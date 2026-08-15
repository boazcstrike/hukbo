using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The pure in-fight footwork arithmetic of design section 5.1: the duty phase
/// that rate-limits a mechanic without any timer state, the alternation sign
/// that makes consecutive fires cancel, and the perpendicular offset that takes
/// a warrior's aim point off the straight line to its enemy. Every expected
/// value asserted here was computed by hand from the formulas in
/// <see cref="EvasionRules"/>, never by calling the method under test.
/// </summary>
public sealed class EvasionRulesTests
{
    /// <summary>
    /// The default living body radius, 4,352 raw, which every offset multiplier
    /// of <see cref="EvasionRules"/> scales. Read from the collision rules
    /// rather than restated so that a change to the body radius reaches these
    /// legibility assertions.
    /// </summary>
    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;

    // ----- FiresThisTick: the duty phase -----

    /// <summary>
    /// For a fixed warrior, exactly one tick out of every
    /// <c>periodTicks</c> consecutive ticks fires. This is the whole contract
    /// of the duty phase — it is a rate limiter, so it must neither skip a
    /// window nor fire twice inside one.
    /// </summary>
    [Theory]
    [InlineData(1, 0UL)]
    [InlineData(1, 7UL)]
    [InlineData(8, 0UL)]
    [InlineData(8, 3UL)]
    [InlineData(8, 199UL)]
    [InlineData(12, 5UL)]
    [InlineData(12, 4_294_967_296UL)]
    [InlineData(12, ulong.MaxValue)]
    public void FiresThisTickFiresExactlyOncePerPeriodForOneWarrior(
        int periodTicks, ulong entityId)
    {
        // Five consecutive windows, offset by a prime so the walk does not
        // begin on a window boundary and hide an off-by-one in the modulus.
        for (var window = 0; window < 5; window++)
        {
            var firstTick = 37L + (window * periodTicks);
            var fires = 0;

            for (var offset = 0; offset < periodTicks; offset++)
            {
                if (EvasionRules.FiresThisTick(firstTick + offset, entityId, periodTicks))
                {
                    fires++;
                }
            }

            Assert.Equal(1, fires);
        }
    }

    /// <summary>
    /// Warriors with consecutive entity ids fire on distinct ticks, which is
    /// what stops a whole rank from stepping in unison. Over one window, the
    /// ids <c>0</c> through <c>periodTicks - 1</c> claim every tick of the
    /// window exactly once between them.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    public void FiresThisTickStaggersConsecutiveEntityIdsAcrossTheWholeWindow(
        int periodTicks)
    {
        var claimedTick = new long[periodTicks];

        for (var id = 0; id < periodTicks; id++)
        {
            var firingTicks = 0;

            for (var tick = 0L; tick < periodTicks; tick++)
            {
                if (!EvasionRules.FiresThisTick(tick, (ulong)id, periodTicks))
                {
                    continue;
                }

                claimedTick[id] = tick;
                firingTicks++;
            }

            Assert.Equal(1, firingTicks);
        }

        // Every tick of the window is claimed by exactly one id, so the
        // stagger is a permutation rather than merely "not all the same".
        Assert.Equal(
            Enumerable.Range(0, periodTicks).Select(tick => (long)tick).ToArray(),
            claimedTick.Order().ToArray());
    }

    /// <summary>
    /// Only the entity id's residue modulo the period decides its phase, so two
    /// warriors a whole period apart in id share a phase. This documents that
    /// the stagger repeats rather than growing without bound, which is why a
    /// two-hundred-agent battle spreads over eight phases and not two hundred.
    /// </summary>
    [Fact]
    public void FiresThisTickPhasesOnTheEntityIdResidueOnly()
    {
        for (var tick = 0L; tick < 32; tick++)
        {
            Assert.Equal(
                EvasionRules.FiresThisTick(tick, entityId: 3UL, periodTicks: 8),
                EvasionRules.FiresThisTick(tick, entityId: 11UL, periodTicks: 8));
        }
    }

    /// <summary>
    /// A period of one is the degenerate duty phase the break-off and dodge
    /// rungs use: it fires on every tick for every warrior.
    /// </summary>
    [Fact]
    public void FiresThisTickAlwaysFiresAtAPeriodOfOne()
    {
        for (var tick = 0L; tick < 16; tick++)
        {
            Assert.True(EvasionRules.FiresThisTick(tick, entityId: 5UL, periodTicks: 1));
        }
    }

    /// <summary>
    /// A warrior's phase within a window is its entity id's residue, asserted
    /// against a hand-computed value rather than against another call.
    /// </summary>
    [Fact]
    public void FiresThisTickFiresOnTheHandComputedTickOfTheWindow()
    {
        // ulong.MaxValue is 2^64 - 1, and 2^64 is divisible by 8, so the
        // residue is 7: the last tick of every eight-tick window.
        Assert.True(EvasionRules.FiresThisTick(tick: 7, entityId: ulong.MaxValue, periodTicks: 8));
        Assert.True(EvasionRules.FiresThisTick(tick: 15, entityId: ulong.MaxValue, periodTicks: 8));
        Assert.False(EvasionRules.FiresThisTick(tick: 6, entityId: ulong.MaxValue, periodTicks: 8));
    }

    [Fact]
    public void FiresThisTickRejectsANonPositivePeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.FiresThisTick(tick: 0, entityId: 0UL, periodTicks: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.FiresThisTick(tick: 0, entityId: 0UL, periodTicks: -1));
    }

    [Fact]
    public void FiresThisTickRejectsANegativeTick()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.FiresThisTick(tick: -1, entityId: 0UL, periodTicks: 8));
    }

    // ----- DutySign: the alternation -----

    /// <summary>
    /// The sign is constant for every tick inside one duty window. A warrior
    /// whose phase lands anywhere in the window therefore gets the same
    /// direction as any other warrior firing in that window.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    public void DutySignIsConstantWithinOneDutyWindow(int periodTicks)
    {
        for (var window = 0; window < 4; window++)
        {
            var firstTick = (long)window * periodTicks;
            var expected = EvasionRules.DutySign(firstTick, periodTicks);

            for (var offset = 1; offset < periodTicks; offset++)
            {
                Assert.Equal(expected, EvasionRules.DutySign(firstTick + offset, periodTicks));
            }
        }
    }

    /// <summary>
    /// Consecutive duty windows carry opposite signs, which is the property
    /// that makes lateral displacement over two consecutive fires cancel, and
    /// therefore makes the anti-drift bar of design section 8 structural.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(12)]
    public void DutySignAlternatesBetweenConsecutiveDutyWindows(int periodTicks)
    {
        for (var window = 0; window < 6; window++)
        {
            var thisWindow = EvasionRules.DutySign((long)window * periodTicks, periodTicks);
            var nextWindow = EvasionRules.DutySign((long)(window + 1) * periodTicks, periodTicks);

            Assert.Equal(-thisWindow, nextWindow);
        }
    }

    /// <summary>
    /// The first window is positive and the second is negative, pinned against
    /// hand-computed values so the alternation cannot silently invert.
    /// </summary>
    [Fact]
    public void DutySignIsPositiveInTheFirstWindowAndNegativeInTheSecond()
    {
        // 0 / 8 = 0, whose low bit is zero, so the sign is +1.
        Assert.Equal(1, EvasionRules.DutySign(tick: 0, periodTicks: 8));
        Assert.Equal(1, EvasionRules.DutySign(tick: 7, periodTicks: 8));

        // 8 / 8 = 1, whose low bit is one, so the sign is -1.
        Assert.Equal(-1, EvasionRules.DutySign(tick: 8, periodTicks: 8));
        Assert.Equal(-1, EvasionRules.DutySign(tick: 15, periodTicks: 8));

        // 16 / 8 = 2, back to +1.
        Assert.Equal(1, EvasionRules.DutySign(tick: 16, periodTicks: 8));
    }

    /// <summary>
    /// The sign is only ever <c>+1</c> or <c>-1</c>, never zero and never any
    /// other magnitude, because every caller multiplies an offset by it.
    /// </summary>
    [Fact]
    public void DutySignIsAlwaysExactlyPlusOrMinusOne()
    {
        for (var tick = 0L; tick < 64; tick++)
        {
            var sign = EvasionRules.DutySign(tick, periodTicks: 12);

            Assert.True(sign is 1 or -1);
        }
    }

    [Fact]
    public void DutySignRejectsANonPositivePeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.DutySign(tick: 0, periodTicks: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.DutySign(tick: 0, periodTicks: -12));
    }

    [Fact]
    public void DutySignRejectsANegativeTick()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.DutySign(tick: -1, periodTicks: 8));
    }

    // ----- PerpendicularOffset -----

    /// <summary>
    /// The returned vector is at right angles to the input vector, up to the
    /// error introduced by truncating each component once.
    /// </summary>
    /// <remarks>
    /// The tolerance is derived, not chosen. Before truncation the exact
    /// components are <c>ox* = -dy * k / d</c> and <c>oy* = dx * k / d</c>, and
    /// their dot product with <c>(dx, dy)</c> is <c>dx * (-dy * k / d) +
    /// dy * (dx * k / d)</c>, which is exactly zero for any divisor. Truncation
    /// moves each component by strictly less than one raw unit, so the observed
    /// dot product is <c>dx * e1 + dy * e2</c> with <c>|e1|</c> and <c>|e2|</c>
    /// below one, and is therefore bounded by <c>|dx| + |dy|</c>. That is a
    /// truncation bound, not a floating-point epsilon: no value in this file is
    /// approximate, and the bound tightens to zero whenever the division is
    /// exact.
    /// </remarks>
    [Theory]
    [InlineData(3_000L, 4_000L, 5_000L, 8_704, 1)]
    [InlineData(7_331L, -4_127L, 8_412L, 8_704, 1)]
    [InlineData(7_331L, -4_127L, 8_412L, 13_056, -1)]
    [InlineData(-9_999L, -1L, 9_999L, 1_024, 1)]
    [InlineData(1L, 0L, 1L, 384, -1)]
    public void PerpendicularOffsetIsPerpendicularWithinTruncation(
        long deltaXRaw, long deltaYRaw, long distanceRaw, int offsetRaw, int sign)
    {
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaXRaw, deltaYRaw, distanceRaw, offsetRaw, sign);

        var dotProduct = (deltaXRaw * offsetXRaw) + (deltaYRaw * offsetYRaw);
        var truncationBound = Math.Abs(deltaXRaw) + Math.Abs(deltaYRaw);

        Assert.InRange(dotProduct, -truncationBound, truncationBound);
    }

    /// <summary>
    /// On an input whose division is exact, the returned length is exactly the
    /// requested length. Asserted on squared magnitudes so that no square root
    /// — and therefore no floating point — appears anywhere in the check.
    /// </summary>
    [Fact]
    public void PerpendicularOffsetHasExactlyTheRequestedMagnitudeOnAnExactCase()
    {
        // A 3-4-5 triple scaled by 1,000, with an offset that divides the
        // distance: ox = -4000 * 10000 / 5000 = -8000 and
        // oy = 3000 * 10000 / 5000 = 6000, both exact.
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaXRaw: 3_000,
            deltaYRaw: 4_000,
            distanceRaw: 5_000,
            offsetRaw: 10_000,
            sign: 1);

        Assert.Equal(-8_000, offsetXRaw);
        Assert.Equal(6_000, offsetYRaw);

        var squaredMagnitude = ((long)offsetXRaw * offsetXRaw) + ((long)offsetYRaw * offsetYRaw);

        Assert.Equal(100_000_000L, squaredMagnitude);
    }

    /// <summary>
    /// On an input whose division is inexact, the returned length is at most
    /// the requested length and falls short of it by less than two raw units.
    /// </summary>
    /// <remarks>
    /// Truncation toward zero can only shorten a component, so the length never
    /// exceeds the request. The floor is derived: with exact components
    /// <c>a</c> and <c>b</c> satisfying <c>a² + b² = k²</c>, truncation leaves
    /// at least <c>(a - 1)² + (b - 1)² = k² - 2(a + b) + 2</c>, and
    /// <c>a + b</c> is at most <c>k√2</c>, so the shortest possible length is
    /// about <c>k - √2</c>, which floors to <c>k - 2</c> in integers. The brief
    /// for this task stated the bound as one raw unit; one raw unit is the
    /// per-component bound, and the vector bound is the √2 combination of the
    /// two. The case below loses about 0.4 of a raw unit, well inside both.
    /// </remarks>
    [Fact]
    public void PerpendicularOffsetMagnitudeStaysWithinTruncationOfTheRequest()
    {
        const int offsetRaw = 8_704;

        // The same 3-4-5 triple, now with an offset that does not divide the
        // distance: ox = -4000 * 8704 / 5000 = -6963.2, truncating to -6963,
        // and oy = 3000 * 8704 / 5000 = 5222.4, truncating to 5222.
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaXRaw: 3_000,
            deltaYRaw: 4_000,
            distanceRaw: 5_000,
            offsetRaw: offsetRaw,
            sign: 1);

        Assert.Equal(-6_963, offsetXRaw);
        Assert.Equal(5_222, offsetYRaw);

        var squaredMagnitude = ((long)offsetXRaw * offsetXRaw) + ((long)offsetYRaw * offsetYRaw);

        Assert.InRange(
            squaredMagnitude,
            (long)(offsetRaw - 2) * (offsetRaw - 2),
            (long)offsetRaw * offsetRaw);
    }

    /// <summary>
    /// A zero distance yields the zero vector, which every rung reads as "this
    /// mechanic yields and the pre-existing proposal stands". A negative
    /// distance, which no caller can produce from a real measurement, takes the
    /// same branch rather than dividing by a nonsense value.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void PerpendicularOffsetIsTheZeroVectorAtANonPositiveDistance(long distanceRaw)
    {
        var offset = EvasionRules.PerpendicularOffset(
            deltaXRaw: 3_000,
            deltaYRaw: 4_000,
            distanceRaw: distanceRaw,
            offsetRaw: 8_704,
            sign: 1);

        Assert.Equal((0, 0), offset);
    }

    /// <summary>
    /// A zero offset yields the zero vector even at a healthy distance, so a
    /// caller that computes a zero displacement gets no movement rather than a
    /// division artefact.
    /// </summary>
    [Fact]
    public void PerpendicularOffsetIsTheZeroVectorAtAZeroOffset()
    {
        Assert.Equal(
            (0, 0),
            EvasionRules.PerpendicularOffset(
                deltaXRaw: 3_000,
                deltaYRaw: 4_000,
                distanceRaw: 5_000,
                offsetRaw: 0,
                sign: -1));
    }

    /// <summary>
    /// The two signs produce exactly negated vectors, so the alternation of
    /// <see cref="EvasionRules.DutySign"/> cancels a displacement exactly
    /// rather than approximately. Truncation toward zero is symmetric about
    /// zero, which is why this is an equality and not a bound.
    /// </summary>
    [Theory]
    [InlineData(3_000L, 4_000L, 5_000L, 8_704)]
    [InlineData(7_331L, -4_127L, 8_412L, 13_056)]
    [InlineData(-1L, -9_999L, 9_999L, 1_024)]
    public void PerpendicularOffsetSignsProduceExactlyNegatedVectors(
        long deltaXRaw, long deltaYRaw, long distanceRaw, int offsetRaw)
    {
        var positive = EvasionRules.PerpendicularOffset(
            deltaXRaw, deltaYRaw, distanceRaw, offsetRaw, sign: 1);
        var negative = EvasionRules.PerpendicularOffset(
            deltaXRaw, deltaYRaw, distanceRaw, offsetRaw, sign: -1);

        Assert.Equal(-positive.XRaw, negative.XRaw);
        Assert.Equal(-positive.YRaw, negative.YRaw);
    }

    /// <summary>
    /// The widened multiply survives inputs at the top of the
    /// <see langword="int"/> range, which is the reason the implementation
    /// widens at all.
    /// </summary>
    /// <remarks>
    /// The deltas below are a 3-4-5 triple scaled by four hundred million, so
    /// the distance is exact, and the offset is
    /// <see cref="int.MaxValue"/>. The intermediate product
    /// <c>1,600,000,000 × 2,147,483,647</c> is 3,435,973,835,200,000,000, which
    /// overflows a 32-bit multiply by nine orders of magnitude and fits a
    /// 64-bit one with room to spare. Dividing by the distance brings it back
    /// inside <see langword="int"/>, which is why the final cast is safe for
    /// any input whose distance is consistent with its components.
    /// </remarks>
    [Fact]
    public void PerpendicularOffsetDoesNotOverflowAtTheLargestDeltasAndOffset()
    {
        var (offsetXRaw, offsetYRaw) = EvasionRules.PerpendicularOffset(
            deltaXRaw: 1_200_000_000,
            deltaYRaw: 1_600_000_000,
            distanceRaw: 2_000_000_000,
            offsetRaw: int.MaxValue,
            sign: 1);

        // -1,600,000,000 * 2,147,483,647 / 2,000,000,000 = -1,717,986,917.6,
        // truncating toward zero to -1,717,986,917.
        Assert.Equal(-1_717_986_917, offsetXRaw);

        // 1,200,000,000 * 2,147,483,647 / 2,000,000,000 = 1,288,490,188.2,
        // truncating toward zero to 1,288,490,188.
        Assert.Equal(1_288_490_188, offsetYRaw);
    }

    [Fact]
    public void PerpendicularOffsetRejectsANegativeOffset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.PerpendicularOffset(
                deltaXRaw: 3_000,
                deltaYRaw: 4_000,
                distanceRaw: 5_000,
                offsetRaw: -1,
                sign: 1));
    }

    /// <summary>
    /// Only the two perpendicular sides exist, so anything but <c>+1</c> or
    /// <c>-1</c> is rejected rather than silently scaling the offset.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    [InlineData(int.MinValue)]
    public void PerpendicularOffsetRejectsASignThatIsNotPlusOrMinusOne(int sign)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.PerpendicularOffset(
                deltaXRaw: 3_000,
                deltaYRaw: 4_000,
                distanceRaw: 5_000,
                offsetRaw: 8_704,
                sign: sign));
    }

    /// <summary>
    /// The guards fire before the zero-distance short circuit, so bad arguments
    /// are always rejected rather than being swallowed by an early return.
    /// </summary>
    [Fact]
    public void PerpendicularOffsetValidatesItsArgumentsEvenAtAZeroDistance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.PerpendicularOffset(0, 0, distanceRaw: 0, offsetRaw: -1, sign: 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EvasionRules.PerpendicularOffset(0, 0, distanceRaw: 0, offsetRaw: 1, sign: 0));
    }

    // ----- The tuning constants -----

    /// <summary>
    /// Every constant that becomes a visible displacement clears the gait
    /// legibility floor.
    /// </summary>
    /// <remarks>
    /// The bound is asserted against the literal <c>384</c> rather than against
    /// <see cref="EvasionRules.MinimumLegibleStepRaw"/>. A threshold read out of
    /// the constant under test moves with it and proves nothing: raising the
    /// floor would keep such a test green while the displacements it guards
    /// stayed exactly where they were. The literal is the whole point.
    /// </remarks>
    [Fact]
    public void EveryDisplacementConstantClearsTheLegibilityFloorLiteral()
    {
        Assert.True(
            EvasionRules.BreakOffOffsetMultiplier * BodyRadiusRaw >= 384,
            "The break-off displacement must animate.");
        Assert.True(
            EvasionRules.SlipOffsetMultiplier * BodyRadiusRaw >= 384,
            "The lateral slip displacement must animate.");
        Assert.True(
            EvasionRules.DodgeOffsetMultiplier * BodyRadiusRaw >= 384,
            "The missile dodge displacement must animate.");
        Assert.True(
            EvasionRules.GiveGroundStepRaw >= 384,
            "The give-ground step must animate.");
    }

    /// <summary>
    /// The legibility floor itself is pinned to its literal, so a later change
    /// to it is a deliberate edit to this test rather than a silent slide.
    /// </summary>
    [Fact]
    public void TheLegibilityFloorIsPinnedToItsLiteral()
    {
        Assert.Equal(384, EvasionRules.MinimumLegibleStepRaw);
    }

    /// <summary>
    /// The offset multipliers resolve to the raw distances design section 5
    /// quotes at the default body radius of 4,352 raw.
    /// </summary>
    [Fact]
    public void TheOffsetMultipliersResolveToTheDesignsQuotedRawDistances()
    {
        Assert.Equal(4_352, BodyRadiusRaw);
        Assert.Equal(13_056, EvasionRules.BreakOffOffsetMultiplier * BodyRadiusRaw);
        Assert.Equal(8_704, EvasionRules.SlipOffsetMultiplier * BodyRadiusRaw);
        Assert.Equal(8_704, EvasionRules.DodgeOffsetMultiplier * BodyRadiusRaw);
    }

    /// <summary>
    /// The give-ground step is exactly one world unit at
    /// <c>FixedPoint.Scale</c>, which is what bounds the backward movement of
    /// design section 5.5.
    /// </summary>
    [Fact]
    public void TheGiveGroundStepIsExactlyOneWorldUnit()
    {
        Assert.Equal(1_024, EvasionRules.GiveGroundStepRaw);
    }

    /// <summary>
    /// The duty periods and the dodge imminence window are pinned to the values
    /// design section 5 states, in ticks.
    /// </summary>
    [Fact]
    public void TheDutyPeriodsArePinnedToTheDesignsValues()
    {
        Assert.Equal(8, EvasionRules.SlipPeriodTicks);
        Assert.Equal(12, EvasionRules.GiveGroundPeriodTicks);
        Assert.Equal(2, EvasionRules.DodgeImminenceTicks);
    }

    /// <summary>
    /// The slip radius is twice a warrior's own reach, expressed in basis
    /// points against the same denominator the other rules use.
    /// </summary>
    [Fact]
    public void TheSlipRadiusIsTwiceTheWarriorsOwnReach()
    {
        Assert.Equal(10_000L, EvasionRules.BasisPointDenominator);
        Assert.Equal(
            2L * EvasionRules.BasisPointDenominator,
            (long)EvasionRules.SlipRadiusBasisPoints);

        // At the default attack range of 12,288 raw the slip radius is 24,576.
        const int attackRangeRaw = 12_288;
        var slipRadiusRaw =
            attackRangeRaw * EvasionRules.SlipRadiusBasisPoints / EvasionRules.BasisPointDenominator;

        Assert.Equal(24_576L, slipRadiusRaw);
    }
}
