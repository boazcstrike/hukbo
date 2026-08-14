using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="CollapsePose"/>: the death collapse's curve and its
/// resting angle (the 2026-08-14 death-collapse design, section 3). Pure
/// arithmetic — no clock, no store, no graphics device.
/// </summary>
public sealed class CollapsePoseTests
{
    private const float Tolerance = 1e-4f;

    /// <summary>
    /// The final angle asserted against, taken from the resolver rather than
    /// from <see cref="CollapsePose.ProneRotationRadians"/>: a threshold read
    /// out of the constant it is testing moves with that constant and stops
    /// being a test.
    /// </summary>
    private static float Final(bool fallsRight = true, ulong entityId = 7) =>
        CollapsePose.ResolveFinalRotation(fallsRight, entityId);

    [Fact]
    public void Resolve_IsZeroAtTheMomentTheCollapseBegins()
    {
        Assert.Equal(0f, CollapsePose.Resolve(0f, Final()));
    }

    [Fact]
    public void Resolve_IsZeroForANegativeOrNonFiniteAge()
    {
        Assert.Equal(0f, CollapsePose.Resolve(-1f, Final()));
        Assert.Equal(0f, CollapsePose.Resolve(float.NaN, Final()));
    }

    /// <summary>
    /// The prone phase needs no code of its own: it is this curve evaluated
    /// past its own end, and it must be exactly the resting angle rather than
    /// approximately it, or a corpse would creep for the rest of the battle.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(600f)]
    public void Resolve_IsExactlyTheRestingAngleAtAndAfterTheEnd(float multiplier)
    {
        var final = Final();

        Assert.Equal(
            final,
            CollapsePose.Resolve(CollapsePose.CollapseSeconds * multiplier, final));
    }

    /// <summary>
    /// The fall accelerates and never backs up. Sampled through the fall
    /// segment only — the settle segment after it is deliberately the one place
    /// the magnitude decreases.
    /// </summary>
    [Fact]
    public void Resolve_TurnsStrictlyFurtherThroughTheFallSegment()
    {
        var final = Final();
        var impactSeconds = CollapsePose.CollapseSeconds * CollapsePose.ImpactShare;
        var previous = 0f;

        for (var step = 1; step <= 40; step++)
        {
            var current = MathF.Abs(
                CollapsePose.Resolve(impactSeconds * (step / 40f), final));
            Assert.True(
                current > previous,
                $"step {step}: {current} was not beyond {previous}");
            previous = current;
        }
    }

    /// <summary>
    /// A body accelerates under its own weight rather than lowering itself. At
    /// the halfway point of the fall an eased-in curve has covered noticeably
    /// less than half the angle; a linear or eased-out one would not.
    /// </summary>
    [Fact]
    public void Resolve_EasesIntoTheFallRatherThanOutOfIt()
    {
        var final = Final();
        var impactSeconds = CollapsePose.CollapseSeconds * CollapsePose.ImpactShare;

        var halfway = MathF.Abs(CollapsePose.Resolve(impactSeconds * 0.5f, final));

        Assert.True(
            halfway < MathF.Abs(final) * 0.4f,
            $"halfway angle {halfway} is not below 40% of {MathF.Abs(final)}");
    }

    /// <summary>
    /// The overshoot is real — the body arrives on the ground with mass — and
    /// it is bounded, because an overshoot large enough to read as a bounce
    /// would also read, at its peak, as a body lying at an angle.
    /// </summary>
    [Fact]
    public void Resolve_OvershootsTheRestingAngleByAtMostTheSettleAllowance()
    {
        var final = Final();
        var peak = 0f;

        for (var step = 0; step <= 200; step++)
        {
            peak = MathF.Max(
                peak,
                MathF.Abs(CollapsePose.Resolve(
                    CollapsePose.CollapseSeconds * (step / 200f),
                    final)));
        }

        Assert.True(
            peak > MathF.Abs(final),
            $"peak {peak} never passed the resting angle {MathF.Abs(final)}");
        Assert.True(
            peak <= MathF.Abs(final) + CollapsePose.SettleOvershootRadians + Tolerance,
            $"peak {peak} exceeded the settle allowance");
    }

    /// <summary>
    /// The two segments meet. A discontinuity here would read as the body
    /// jumping at the moment of impact.
    /// </summary>
    [Fact]
    public void Resolve_IsContinuousAcrossTheImpactBoundary()
    {
        var final = Final();
        var impactSeconds = CollapsePose.CollapseSeconds * CollapsePose.ImpactShare;

        var before = CollapsePose.Resolve(impactSeconds - 0.0005f, final);
        var after = CollapsePose.Resolve(impactSeconds + 0.0005f, final);

        Assert.Equal(before, after, 0.01f);
    }

    /// <summary>
    /// A quarter turn is what puts a body flat. The design is explicit that a
    /// partial turn is the defect being fixed, so the jitter may soften the
    /// angle and may never replace it.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(97UL)]
    [InlineData(4096UL)]
    [InlineData(ulong.MaxValue)]
    public void ResolveFinalRotation_StaysWithinTheJitterBudgetOfAQuarterTurn(
        ulong entityId)
    {
        var deviation = MathF.Abs(
            MathF.Abs(CollapsePose.ResolveFinalRotation(true, entityId)) -
            CollapsePose.ProneRotationRadians);

        Assert.True(
            deviation <= CollapsePose.FallJitterRadians + Tolerance,
            $"entity {entityId} deviated by {deviation}");
    }

    [Fact]
    public void ResolveFinalRotation_TakesItsSignFromTheFallDirection()
    {
        Assert.True(CollapsePose.ResolveFinalRotation(true, 11) > 0f);
        Assert.True(CollapsePose.ResolveFinalRotation(false, 11) < 0f);
        Assert.Equal(
            CollapsePose.ResolveFinalRotation(true, 11),
            -CollapsePose.ResolveFinalRotation(false, 11));
    }

    /// <summary>
    /// One warrior lies the same way for the whole battle and across runs. A
    /// corpse that changed which way it was lying between frames would be worse
    /// than one that never fell.
    /// </summary>
    [Fact]
    public void ResolveFinalRotation_IsStablePerWarrior()
    {
        Assert.Equal(
            CollapsePose.ResolveFinalRotation(true, 12345),
            CollapsePose.ResolveFinalRotation(true, 12345));
    }

    /// <summary>
    /// Without real variation a field of dead reads as one body stamped
    /// repeatedly. Sixty-four warriors must not all come to rest on the same
    /// handful of angles.
    /// </summary>
    [Fact]
    public void ResolveFinalRotation_VariesAcrossWarriors()
    {
        var angles = new HashSet<float>();
        for (ulong entityId = 0; entityId < 64; entityId++)
        {
            angles.Add(CollapsePose.ResolveFinalRotation(true, entityId));
        }

        Assert.True(angles.Count >= 32, $"only {angles.Count} distinct angles in 64");
    }
}
