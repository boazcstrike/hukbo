using Sandata.Core.Mathematics;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 23 of Sandata's scaffold plan: pins
/// <see cref="TickConversion.ToTicks"/> for the seven millisecond figures
/// design section 9 publishes, exercises every zero-tick transition of
/// <see cref="WeaponChain.Advance"/> to show none swallows or double-advances
/// a tick, and derives the 800 rpm, 50 Hz interval pattern for
/// <see cref="CyclicFireAccumulator.Advance"/> by hand rather than reading it
/// back from the implementation.
/// </summary>
public sealed class WeaponChainTests
{
    private const int TickRate = 50;

    // ------------------------------------------------------------------
    // TickConversion — the pinned millisecond-to-tick formula.
    // ------------------------------------------------------------------

    /// <summary>
    /// Every figure design section 9 publishes for the timing chain, hand
    /// computed against the pinned formula
    /// <c>ticks = (milliseconds * tickRate + 500) / 1000</c> at 50 Hz:
    /// <c>(80*50+500)/1000=4</c>, <c>(150*50+500)/1000=8</c>,
    /// <c>(180*50+500)/1000=9</c>, <c>(335*50+500)/1000=17</c>,
    /// <c>(350*50+500)/1000=18</c>, <c>(405*50+500)/1000=20</c>,
    /// <c>(500*50+500)/1000=25</c>.
    /// </summary>
    [Theory]
    [InlineData(80, 4)] // ~80 ms 1911 ready time
    [InlineData(150, 8)] // pistol aim time, low end of the published 150-180 ms band
    [InlineData(180, 9)] // pistol aim time, high end of the published 150-180 ms band
    [InlineData(335, 17)] // rifle aim time
    [InlineData(350, 18)] // aim time under roughly 14 m
    [InlineData(405, 20)] // rifle ready time
    [InlineData(500, 25)] // designated marksman rifle aim time, low end of "500 ms and up"
    public void ToTicks_PinnedAt50Hz(int milliseconds, int expectedTicks)
    {
        Assert.Equal(expectedTicks, TickConversion.ToTicks(milliseconds, TickRate));
    }

    /// <summary>
    /// The formula's half-away-from-zero rounding at an exact tie: 10 ms at
    /// 50 Hz is exactly half a tick (<c>(10*50+500)/1000 = 1000/1000 = 1</c>),
    /// and the tie resolves up rather than down.
    /// </summary>
    [Fact]
    public void ToTicks_ExactHalfTickTie_RoundsUp()
    {
        Assert.Equal(1, TickConversion.ToTicks(10, TickRate));
    }

    /// <summary>
    /// Below the half-tick boundary, the value truncates to zero rather than
    /// rounding up: 5 ms at 50 Hz is a quarter of a tick
    /// (<c>(5*50+500)/1000 = 750/1000 = 0</c>).
    /// </summary>
    [Fact]
    public void ToTicks_BelowHalfTick_RoundsDownToZero()
    {
        Assert.Equal(0, TickConversion.ToTicks(5, TickRate));
    }

    [Fact]
    public void ToTicks_ZeroMilliseconds_IsZeroTicks()
    {
        Assert.Equal(0, TickConversion.ToTicks(0, TickRate));
    }

    [Fact]
    public void ToTicks_NegativeMilliseconds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickConversion.ToTicks(-1, TickRate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToTicks_NonPositiveTickRate_Throws(int tickRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickConversion.ToTicks(100, tickRate));
    }

    // ------------------------------------------------------------------
    // WeaponChain — the ordinary, non-zero-tick path, one tick per call.
    // ------------------------------------------------------------------

    /// <summary>
    /// A four-tick <see cref="WeaponChainPhase.Raising"/> (the pinned 80 ms
    /// pistol ready time) counts down exactly one tick per
    /// <see cref="WeaponChain.Advance"/> call — 4, 3, 2, 1 — and transitions
    /// away only once the fourth call finds the counter at zero, proving
    /// neither a swallowed tick (finishing early) nor a double-advance
    /// (finishing late).
    /// </summary>
    [Fact]
    public void Raising_WithFourTicks_CountsDownExactlyOnePerCall_ThenTransitions()
    {
        var readyTicks = TickConversion.ToTicks(80, TickRate);
        Assert.Equal(4, readyTicks);

        // Lowered -> Raising(4), on the tick the raise is requested.
        var afterRequest = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
            readyTicks, aimTicks: 10, resetTicks: 10);
        Assert.Equal((WeaponChainPhase.Raising, 4, false), (afterRequest.Phase, afterRequest.RemainingTicks, afterRequest.Fired));

        var phase = afterRequest.Phase;
        var remaining = afterRequest.RemainingTicks;
        var observedCounts = new List<int>();
        for (var tick = 0; tick < 4; tick++)
        {
            var result = WeaponChain.Advance(
                phase, remaining,
                forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
                readyTicks, aimTicks: 10, resetTicks: 10);
            phase = result.Phase;
            remaining = result.RemainingTicks;
            if (phase == WeaponChainPhase.Raising)
            {
                observedCounts.Add(remaining);
            }
        }

        // Three more calls stay in Raising at 3, 2, 1; the fourth call moves
        // on to Turning (arcWithinTolerance is false here on purpose, so the
        // transition stops there rather than cascading further).
        Assert.Equal([3, 2, 1], observedCounts);
        Assert.Equal(WeaponChainPhase.Turning, phase);
        Assert.Equal(0, remaining);
    }

    // ------------------------------------------------------------------
    // WeaponChain — every zero-tick transition.
    // ------------------------------------------------------------------

    /// <summary>
    /// <see cref="WeaponChainPhase.Raising"/> converted from a millisecond
    /// value so small it bakes to zero ticks resolves to
    /// <see cref="WeaponChainPhase.Turning"/> in the very same
    /// <see cref="WeaponChain.Advance"/> call that requested the raise —
    /// neither swallowing that tick (staying in <see cref="WeaponChainPhase.Lowered"/>
    /// or <see cref="WeaponChainPhase.Raising"/> for a tick it did not need)
    /// nor double-advancing (also entering <see cref="WeaponChainPhase.Aiming"/>
    /// before <see cref="WeaponChainPhase.Turning"/>'s own condition, the
    /// target's arc, has been evaluated at all).
    /// </summary>
    [Fact]
    public void Raising_ZeroTicks_TransitionsToTurning_InTheSameCall()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
            readyTicks: 0, aimTicks: 10, resetTicks: 10);

        Assert.Equal(WeaponChainPhase.Turning, result.Phase);
        Assert.Equal(0, result.RemainingTicks);
        Assert.False(result.Fired);
    }

    /// <summary>
    /// <see cref="WeaponChainPhase.Aiming"/> converted from a millisecond
    /// value so small it bakes to zero ticks fires and reaches
    /// <see cref="WeaponChainPhase.Resetting"/> in the very same call that
    /// entered it from <see cref="WeaponChainPhase.Turning"/> — the shot does
    /// not wait an extra tick it was never owed (swallowing), and
    /// <see cref="WeaponChainPhase.Resetting"/>'s own counter is exactly
    /// <c>resetTicks</c>, not <c>resetTicks - 1</c>, showing the newly
    /// entered phase is not charged the tick a second time (double-advancing).
    /// </summary>
    [Fact]
    public void Aiming_ZeroTicks_FiresAndReachesResetting_InTheSameCall()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Turning, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 10, aimTicks: 0, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Resetting, result.Phase);
        Assert.Equal(6, result.RemainingTicks);
        Assert.True(result.Fired);
    }

    /// <summary>
    /// <see cref="WeaponChainPhase.Resetting"/> converted from a millisecond
    /// value so small it bakes to zero ticks returns straight to
    /// <see cref="WeaponChainPhase.Aiming"/>, freshly re-armed at the full
    /// <c>aimTicks</c> count (not <c>aimTicks - 1</c>) in the same call that
    /// fired the shot, rather than holding in
    /// <see cref="WeaponChainPhase.Resetting"/> for a tick <c>resetTicks = 0</c>
    /// never asked for.
    /// </summary>
    [Fact]
    public void Resetting_ZeroTicks_ReturnsToAiming_InTheSameCall()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Aiming, remainingTicks: 1,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 10, aimTicks: 9, resetTicks: 0);

        Assert.Equal(WeaponChainPhase.Aiming, result.Phase);
        Assert.Equal(9, result.RemainingTicks);
        Assert.True(result.Fired);
    }

    /// <summary>
    /// Every timed phase converted to zero ticks at once — the fully
    /// degenerate weapon — still resolves exactly one shot per tick rather
    /// than looping forever inside a single <see cref="WeaponChain.Advance"/>
    /// call: <see cref="WeaponChainPhase.Lowered"/> cascades through
    /// <see cref="WeaponChainPhase.Raising"/>, <see cref="WeaponChainPhase.Turning"/>
    /// (already within tolerance), <see cref="WeaponChainPhase.Aiming"/>, and
    /// <see cref="WeaponChainPhase.Firing"/>, and stops at
    /// <see cref="WeaponChainPhase.Aiming"/> with zero remaining ticks having
    /// fired precisely once — the documented safety bound in
    /// <see cref="WeaponChain"/>'s remarks — rather than cycling
    /// Aiming/Firing/Resetting indefinitely.
    /// </summary>
    [Fact]
    public void AllZeroTicks_FromLowered_FiresExactlyOnce_AndStopsAtAiming()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 0, aimTicks: 0, resetTicks: 0);

        Assert.Equal(WeaponChainPhase.Aiming, result.Phase);
        Assert.Equal(0, result.RemainingTicks);
        Assert.True(result.Fired);
    }

    /// <summary>
    /// A degenerate all-zero-tick weapon held at <see cref="WeaponChainPhase.Aiming"/>
    /// with the target still in tolerance fires again on the very next tick's
    /// call — the bound in <see cref="AllZeroTicks_FromLowered_FiresExactlyOnce_AndStopsAtAiming"/>
    /// is a per-call limit, not a stall.
    /// </summary>
    [Fact]
    public void AllZeroTicks_FiresOnEveryCall_OneShotPerTick()
    {
        var phase = WeaponChainPhase.Aiming;
        var remaining = 0;
        var fired = 0;
        for (var tick = 0; tick < 5; tick++)
        {
            var result = WeaponChain.Advance(
                phase, remaining,
                forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
                readyTicks: 0, aimTicks: 0, resetTicks: 0);
            phase = result.Phase;
            remaining = result.RemainingTicks;
            if (result.Fired)
            {
                fired++;
            }
        }

        Assert.Equal(5, fired);
        Assert.Equal(WeaponChainPhase.Aiming, phase);
        Assert.Equal(0, remaining);
    }

    // ------------------------------------------------------------------
    // WeaponChain — Turning (angle-gated, not tick-counted) and Lowered.
    // ------------------------------------------------------------------

    [Fact]
    public void Lowered_WithoutARaiseRequest_HoldsIndefinitely()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: false, raiseRequested: false, arcWithinTolerance: true,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Lowered, result.Phase);
        Assert.Equal(0, result.RemainingTicks);
        Assert.False(result.Fired);
    }

    [Fact]
    public void Turning_OutOfTolerance_HoldsWithoutConsumingATickCounter()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Turning, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Turning, result.Phase);
        Assert.Equal(0, result.RemainingTicks);
        Assert.False(result.Fired);
    }

    [Fact]
    public void Turning_WithinTolerance_TransitionsToAiming_WithAFreshCounter()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Turning, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Aiming, result.Phase);
        Assert.Equal(9, result.RemainingTicks);
        Assert.False(result.Fired);
    }

    // ------------------------------------------------------------------
    // WeaponChain — the weapon-lowered rule overrides everything.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(WeaponChainPhase.Raising, 2)]
    [InlineData(WeaponChainPhase.Turning, 0)]
    [InlineData(WeaponChainPhase.Aiming, 5)]
    [InlineData(WeaponChainPhase.Resetting, 3)]
    public void ForceLowered_FromAnyPhase_ReturnsToLowered_AndCancelsAnyFire(
        WeaponChainPhase phase, int remainingTicks)
    {
        var result = WeaponChain.Advance(
            phase, remainingTicks,
            forceLowered: true, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Lowered, result.Phase);
        Assert.Equal(0, result.RemainingTicks);
        Assert.False(result.Fired);
    }

    /// <summary>
    /// Being forced lowered on the very tick a shot would otherwise resolve
    /// (<see cref="WeaponChainPhase.Aiming"/> with its counter at its last
    /// tick) cancels that shot rather than letting the same-tick cascade
    /// complete it first.
    /// </summary>
    [Fact]
    public void ForceLowered_OnTheTickAShotWouldResolve_CancelsTheShot()
    {
        var result = WeaponChain.Advance(
            WeaponChainPhase.Aiming, remainingTicks: 1,
            forceLowered: true, raiseRequested: true, arcWithinTolerance: true,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(WeaponChainPhase.Lowered, result.Phase);
        Assert.False(result.Fired);
    }

    // ------------------------------------------------------------------
    // WeaponChain — validation.
    // ------------------------------------------------------------------

    [Fact]
    public void Advance_NegativeRemainingTicks_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeaponChain.Advance(
            WeaponChainPhase.Raising, remainingTicks: -1,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
            readyTicks: 4, aimTicks: 9, resetTicks: 6));
    }

    [Fact]
    public void Advance_NegativeDurationParameter_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: false,
            readyTicks: -1, aimTicks: 9, resetTicks: 6));
    }

    // ------------------------------------------------------------------
    // CyclicFireAccumulator — the 800 rpm / 50 Hz interval pattern.
    // ------------------------------------------------------------------

    /// <summary>
    /// 800 rpm is 13.33 shots per second; at 50 Hz that is one shot every
    /// 3.75 ticks, hand-derived here as <c>shotTick(n) = floor(n * 3.75)</c>
    /// for consecutive rounds <c>n = 1, 2, 3, ...</c>: ticks 3, 7, 11, 15, 18,
    /// 22, 26, 30, 33, 37 — whose consecutive gaps are 4, 4, 4, 3 repeating
    /// from the very first interval. <see cref="CyclicFireAccumulator.Advance"/>
    /// reproduces the same repeating gap pattern (offset by a constant one
    /// tick, an artifact of where the running accumulator starts rather than
    /// a different schedule): fires land at ticks 4, 8, 12, 16, 19, 23, 27,
    /// 31, 34, 38 of the 40 simulated, ten shots total, with the accumulator
    /// left at 50,000 of the required 75,000 microseconds toward an eleventh.
    /// </summary>
    [Fact]
    public void Advance_800Rpm_At50Hz_ProducesTheHandDerived4443TickPattern()
    {
        const int cyclicRpm = 800;
        var accumulator = 0;
        var shotTicks = new List<int>();

        for (var tick = 1; tick <= 40; tick++)
        {
            var result = CyclicFireAccumulator.Advance(accumulator, TickRate, cyclicRpm);
            accumulator = result.Accumulator;
            Assert.InRange(result.ShotsFired, 0, 1);
            if (result.ShotsFired == 1)
            {
                shotTicks.Add(tick);
            }
        }

        Assert.Equal(
            [4, 8, 12, 16, 19, 23, 27, 31, 34, 38],
            shotTicks);

        var gaps = new List<int>();
        for (var i = 1; i < shotTicks.Count; i++)
        {
            gaps.Add(shotTicks[i] - shotTicks[i - 1]);
        }

        Assert.Equal([4, 4, 4, 3, 4, 4, 4, 3, 4], gaps);
        Assert.Equal(50_000, accumulator);
    }

    [Fact]
    public void Advance_NegativeAccumulator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CyclicFireAccumulator.Advance(-1, TickRate, 800));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Advance_NonPositiveTickRate_Throws(int tickRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CyclicFireAccumulator.Advance(0, tickRate, 800));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Advance_NonPositiveCyclicRpm_Throws(int cyclicRpm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CyclicFireAccumulator.Advance(0, TickRate, cyclicRpm));
    }

    // ------------------------------------------------------------------
    // WeaponChain.IsArcWithinTolerance — task 74's second rule: design
    // section 9's |ShortestArc| <= AimToleranceBam comparison, which
    // previously had no call site anywhere in Sandata.Core.
    // ------------------------------------------------------------------

    /// <summary>Mirrors <c>SandataRuleset.ModernTacticalV1.AimToleranceBam</c>; not read from the ruleset.</summary>
    private const ushort AimToleranceBam = 1024;

    /// <summary>
    /// Both directions around the ring, both sides of the boundary: exactly
    /// <see cref="AimToleranceBam"/> away compares within tolerance, and one
    /// raw unit further does not, whether the arc is positive or negative.
    /// </summary>
    [Theory]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    [InlineData(-1024, true)]
    [InlineData(-1025, false)]
    public void IsArcWithinTolerance_AtExactlyTheTolerance_TrueAndOneRawUnitBeyond_False(
        int arcOffset, bool expected)
    {
        var currentAimBam = new Bam16(0);
        var targetAimBam = new Bam16(unchecked((ushort)arcOffset));

        Assert.Equal(expected, WeaponChain.IsArcWithinTolerance(currentAimBam, targetAimBam, AimToleranceBam));
    }

    /// <summary>
    /// The same boundary proven through <see cref="WeaponChain.Advance"/>'s
    /// <see cref="WeaponChainPhase.Turning"/> transition: feeding
    /// <see cref="WeaponChain.IsArcWithinTolerance"/>'s own result in as
    /// <c>arcWithinTolerance</c> transitions to <see cref="WeaponChainPhase.Aiming"/>
    /// exactly at <see cref="AimToleranceBam"/> and holds at
    /// <see cref="WeaponChainPhase.Turning"/> one raw unit beyond it.
    /// </summary>
    [Theory]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public void Turning_TransitionsExactlyAtAimTolerance_NotOneRawUnitBeyond(int arcOffset, bool expectedWithinTolerance)
    {
        var currentAimBam = new Bam16(0);
        var targetAimBam = new Bam16(unchecked((ushort)arcOffset));
        var withinTolerance = WeaponChain.IsArcWithinTolerance(currentAimBam, targetAimBam, AimToleranceBam);
        Assert.Equal(expectedWithinTolerance, withinTolerance);

        var result = WeaponChain.Advance(
            WeaponChainPhase.Turning, remainingTicks: 0,
            forceLowered: false, raiseRequested: true, arcWithinTolerance: withinTolerance,
            readyTicks: 4, aimTicks: 9, resetTicks: 6);

        Assert.Equal(
            expectedWithinTolerance ? WeaponChainPhase.Aiming : WeaponChainPhase.Turning,
            result.Phase);
    }

    /// <summary>
    /// A signed arc of exactly zero — the aim point and the target already
    /// coincide — is trivially within any non-negative tolerance, including
    /// a tolerance of zero.
    /// </summary>
    [Fact]
    public void IsArcWithinTolerance_ZeroArc_IsWithinZeroTolerance()
    {
        var aimBam = new Bam16(12_345);

        Assert.True(WeaponChain.IsArcWithinTolerance(aimBam, aimBam, aimToleranceBam: 0));
    }

    /// <summary>
    /// An arc that wraps across raw angle zero compares identically to an
    /// equivalent arc that does not, because <see cref="Bam16.ShortestArc"/>
    /// already resolves the wrap before <see cref="WeaponChain.IsArcWithinTolerance"/>
    /// ever sees the value. A naive, unwrapped subtraction of the two raw
    /// angles does not have that property: it reports this same arc as tens
    /// of thousands of raw units, not 16, and would fail the very tolerance
    /// check the correct implementation passes.
    /// </summary>
    [Fact]
    public void IsArcWithinTolerance_ArcWrappingAcrossZero_ComparesIdenticallyToAnEquivalentNonWrappingArc()
    {
        const ushort tolerance = 20;

        // The true shortest arc from 10 to 65,530 is -16 (16 raw units
        // backward through the zero boundary), not the 65,520-unit forward
        // distance a naive, unwrapped subtraction would compute.
        var wrapping = WeaponChain.IsArcWithinTolerance(new Bam16(10), new Bam16(65_530), tolerance);

        // The same 16-unit backward arc, entirely inside the ring with no
        // wrap at all.
        var nonWrapping = WeaponChain.IsArcWithinTolerance(new Bam16(100), new Bam16(84), tolerance);

        Assert.True(wrapping);
        Assert.Equal(nonWrapping, wrapping);

        var naiveUnwrappedDelta = 65_530 - 10;
        Assert.True(naiveUnwrappedDelta > tolerance);
    }
}
