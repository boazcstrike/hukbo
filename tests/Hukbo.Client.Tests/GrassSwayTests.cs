using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class GrassSwayTests
{
    // --- R-W5.5: exact Vector2.Zero at amplitude 0 ---

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0f, 1.2f)]
    [InlineData(3.7f, 0f)]
    [InlineData(9_999f, 6.28f)]
    [InlineData(-4.5f, -1.1f)]
    public void GrassSwayOffset_AmplitudeZero_IsExactlyVectorZero(
        float timeSeconds,
        float phase)
    {
        var offset = GrassSway.GrassSwayOffset(timeSeconds, phase, amplitudeScale: 0f);

        Assert.Equal(Vector2.Zero, offset);
    }

    // --- R-W5.4: pure-function determinism from the same time/phase pair ---

    [Theory]
    [InlineData(0.5f, 0.3f, 1f)]
    [InlineData(12.25f, 4.1f, 0.5f)]
    public void GrassSwayOffset_SameInputsProduceIdenticalOffsets(
        float timeSeconds,
        float phase,
        float amplitudeScale)
    {
        var first = GrassSway.GrassSwayOffset(timeSeconds, phase, amplitudeScale);
        var second = GrassSway.GrassSwayOffset(timeSeconds, phase, amplitudeScale);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GrassSwayOffset_DifferentPhasesAtTheSameTimeProduceDifferentOffsets()
    {
        var first = GrassSway.GrassSwayOffset(1f, phase: 0f, amplitudeScale: 1f);
        var second = GrassSway.GrassSwayOffset(1f, phase: 1.5f, amplitudeScale: 1f);

        Assert.NotEqual(first, second);
    }

    // --- exact values pinned at chosen times (sine wave, OD-W4-b) ---

    [Fact]
    public void GrassSwayOffset_AtZeroTimeAndZeroPhase_IsPinnedToTheVerticalComponentOnly()
    {
        // angle = (Tau * frequency * 0) + 0 = 0 -> sin(0) = 0, cos(0) = 1, so
        // only the (smaller) vertical component contributes: 1.5 * 0.4 = 0.6.
        var offset = GrassSway.GrassSwayOffset(
            timeSeconds: 0f,
            phase: 0f,
            amplitudeScale: 1f);

        Assert.Equal(0f, offset.X, precision: 5);
        Assert.Equal(0.6f, offset.Y, precision: 4);
    }

    [Fact]
    public void GrassSwayOffset_AtQuarterPeriod_ReachesTheFullPinnedAmplitudeOnTheHorizontalAxis()
    {
        // angle = Tau/4 -> sin = 1, cos = 0, so the offset lands exactly on
        // the horizontal axis at the pinned amplitude bound.
        var quarterPeriodSeconds = 1f / (4f * GrassSway.SwayFrequencyHz);

        var offset = GrassSway.GrassSwayOffset(
            quarterPeriodSeconds,
            phase: 0f,
            amplitudeScale: 1f);

        Assert.Equal(GrassSway.SwayAmplitudePixels, offset.X, precision: 4);
        Assert.Equal(0f, offset.Y, precision: 4);
    }

    // --- R-W5.1: sub-1 Hz frequency, named PROVISIONAL constant ---

    [Fact]
    public void SwayFrequencyHz_StaysBelowOneHertz()
    {
        Assert.True(GrassSway.SwayFrequencyHz > 0f);
        Assert.True(GrassSway.SwayFrequencyHz < 1f);
    }

    // --- R-W5.2: amplitude bound at most ~2 screen pixels at zoom 1 ---

    [Fact]
    public void SwayAmplitudePixels_StaysWithinTheDesignsTwoPixelCeiling()
    {
        Assert.True(GrassSway.SwayAmplitudePixels > 0f);
        Assert.True(GrassSway.SwayAmplitudePixels <= 2f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.37f)]
    [InlineData(1f)]
    [InlineData(2.5f)]
    [InlineData(100f)]
    public void GrassSwayOffset_MagnitudeNeverExceedsThePinnedAmplitudeBound(
        float timeSeconds)
    {
        for (var phase = 0f; phase < MathF.Tau; phase += 0.25f)
        {
            var offset = GrassSway.GrassSwayOffset(timeSeconds, phase, amplitudeScale: 1f);

            Assert.True(
                offset.Length() <= GrassSway.SwayAmplitudePixels + 0.001f,
                $"magnitude {offset.Length()} exceeded the pinned bound at " +
                $"time={timeSeconds}, phase={phase}");
        }
    }

    [Fact]
    public void GrassSwayOffset_HalfAmplitudeScalesTheOffsetLinearly()
    {
        var full = GrassSway.GrassSwayOffset(1f, phase: 0.4f, amplitudeScale: 1f);
        var half = GrassSway.GrassSwayOffset(1f, phase: 0.4f, amplitudeScale: 0.5f);

        Assert.Equal(full.X * 0.5f, half.X, precision: 4);
        Assert.Equal(full.Y * 0.5f, half.Y, precision: 4);
    }

    // --- border-clip step (design's "Wind and motion"): the amplitude bound
    // stays far inside the grass-free border margin at every zoom, so a
    // swayed tuft can never cross the map border by construction ---

    [Fact]
    public void SwayAmplitudePixels_StaysWellInsideTheGrassFreeBorderMargin()
    {
        Assert.True(
            GrassSway.SwayAmplitudePixels < GrassGeometry.GrassFreeBorderMargin);
    }

    // --- argument validation ---

    [Theory]
    [InlineData(float.NaN, 0f, 1f)]
    [InlineData(float.PositiveInfinity, 0f, 1f)]
    [InlineData(0f, float.NaN, 1f)]
    [InlineData(0f, float.NegativeInfinity, 1f)]
    [InlineData(0f, 0f, float.NaN)]
    [InlineData(0f, 0f, -0.01f)]
    [InlineData(0f, 0f, 1.01f)]
    public void GrassSwayOffset_RejectsNonFiniteOrOutOfRangeArguments(
        float timeSeconds,
        float phase,
        float amplitudeScale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GrassSway.GrassSwayOffset(timeSeconds, phase, amplitudeScale));
    }

    // --- VIS-031: ResolveAmplitudeFactor, the single amplitude source
    // combining every gate (R-W5.6, R-W5.7, R-W5.8, R-W5.9). Full truth
    // table: 3 settings x 2 high-contrast states x 3 zoom bands x 2
    // suppression states = 36 rows, every one asserted. ---

    [Theory]
    // band = Far forces 0 regardless of every other input (R-W5.6).
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Far, true, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Far, true, 0f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Far, true, 0f)]
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Far, false, 0f)]
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Far, true, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Far, true, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Far, true, 0f)]
    // isHighContrastTheme = true forces 0 regardless of setting or
    // suppression, in the Mid and Near bands (R-W5.7).
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Mid, false, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Mid, false, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Mid, false, 0f)]
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Near, false, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Near, false, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Near, false, 0f)]
    [InlineData(MotionIntensity.Off, true, GrassZoomBand.Near, true, 0f)]
    [InlineData(MotionIntensity.Reduced, true, GrassZoomBand.Near, true, 0f)]
    [InlineData(MotionIntensity.Full, true, GrassZoomBand.Near, true, 0f)]
    // isSuppressed = true forces 0 regardless of setting, once band and
    // theme have not already zeroed it (R-W5.9, R-W4.7).
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Mid, true, 0f)]
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Near, true, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Near, true, 0f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Near, true, 0f)]
    // Nothing zeroing: the MotionIntensity setting decides (R-W5.8) — Off
    // 0, Reduced exactly one-half, Full 1 — in both the Mid and Near bands.
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Mid, false, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Mid, false, 0.5f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Mid, false, 1f)]
    [InlineData(MotionIntensity.Off, false, GrassZoomBand.Near, false, 0f)]
    [InlineData(MotionIntensity.Reduced, false, GrassZoomBand.Near, false, 0.5f)]
    [InlineData(MotionIntensity.Full, false, GrassZoomBand.Near, false, 1f)]
    public void ResolveAmplitudeFactor_TruthTable_MatchesEveryDocumentedCombination(
        MotionIntensity setting,
        bool isHighContrastTheme,
        GrassZoomBand zoomBand,
        bool isSuppressed,
        float expected)
    {
        var actual = GrassSway.ResolveAmplitudeFactor(
            setting,
            isHighContrastTheme,
            zoomBand,
            isSuppressed);

        Assert.Equal(expected, actual, precision: 5);
    }

    [Theory]
    [InlineData(MotionIntensity.Off)]
    [InlineData(MotionIntensity.Reduced)]
    [InlineData(MotionIntensity.Full)]
    public void ResolveAmplitudeFactor_HighContrastTheme_ForcesZeroRegardlessOfSetting(
        MotionIntensity setting)
    {
        var actual = GrassSway.ResolveAmplitudeFactor(
            setting,
            isHighContrastTheme: true,
            GrassZoomBand.Near,
            isSuppressed: false);

        Assert.Equal(0f, actual);
    }

    [Theory]
    [InlineData(MotionIntensity.Off)]
    [InlineData(MotionIntensity.Reduced)]
    [InlineData(MotionIntensity.Full)]
    public void ResolveAmplitudeFactor_FarBand_ForcesZeroRegardlessOfSettingOrTheme(
        MotionIntensity setting)
    {
        var actual = GrassSway.ResolveAmplitudeFactor(
            setting,
            isHighContrastTheme: false,
            GrassZoomBand.Far,
            isSuppressed: false);

        Assert.Equal(0f, actual);
    }

    [Theory]
    [InlineData(MotionIntensity.Off)]
    [InlineData(MotionIntensity.Reduced)]
    [InlineData(MotionIntensity.Full)]
    public void ResolveAmplitudeFactor_Suppression_ForcesZeroRegardlessOfSetting(
        MotionIntensity setting)
    {
        var actual = GrassSway.ResolveAmplitudeFactor(
            setting,
            isHighContrastTheme: false,
            GrassZoomBand.Mid,
            isSuppressed: true);

        Assert.Equal(0f, actual);
    }

    [Fact]
    public void ResolveAmplitudeFactor_ReducedWithNothingElseZeroing_YieldsExactlyOneHalf()
    {
        var actual = GrassSway.ResolveAmplitudeFactor(
            MotionIntensity.Reduced,
            isHighContrastTheme: false,
            GrassZoomBand.Mid,
            isSuppressed: false);

        Assert.Equal(0.5f, actual);
    }

    // --- OD-9 (resolved 2026-07-28): if dust ships (VIS-029, optional),
    // its truth table gains a row distinct in shape from sway's — dust
    // spawning is a boolean gate, not a scaled amplitude, so
    // MotionIntensity.Reduced leaves it fully unchanged rather than
    // halving it the way ResolveAmplitudeFactor halves sway. Pinned here,
    // ahead of VIS-029, so that task can build against an already-tested
    // decision instead of re-deriving it. ---

    [Theory]
    [InlineData(MotionIntensity.Off, false)]
    [InlineData(MotionIntensity.Reduced, true)]
    [InlineData(MotionIntensity.Full, true)]
    public void MotionIntensity_DustSpawningRow_Off0SuppressesReduced1LeavesUnchanged(
        MotionIntensity setting,
        bool dustSpawningRemainsEnabled)
    {
        // The decided dust gate (OD-9): suppressed only at Off, unchanged
        // at every other level. VIS-029 has not landed, so there is no
        // production dust-spawn predicate to call yet — this pins the
        // decision itself against the shared MotionIntensity contract.
        var shouldSpawnDust = setting != MotionIntensity.Off;

        Assert.Equal(dustSpawningRemainsEnabled, shouldSpawnDust);
    }
}
