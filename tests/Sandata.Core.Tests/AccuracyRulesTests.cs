using Sandata.Core.Combat;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 31 of Sandata's scaffold plan: pins
/// <see cref="AccuracyRules.Dispersion"/> against design section 9's linear
/// integer interpolation formula by hand computation, and proves
/// <see cref="AccuracyRules.DrawAngularErrorBam"/> is a reproducible,
/// bounded draw from the same seed and entity id — never a numeric draw
/// value read back from the implementation, since design section 9 states
/// only "draw from the <c>Accuracy</c> stream", not a pinned expected
/// output for any given key.
/// </summary>
/// <remarks>
/// <see cref="Dispersion_HandComputed"/>'s cases reuse the rifle and pistol
/// dispersion constants <c>FirearmCatalog</c> already bakes
/// (<c>DispersionAtZeroWu = 32</c>, <c>DispersionAtMaxWu = 256</c>,
/// <c>MaxEffectiveWu = 800</c> for a rifle; <c>64</c>, <c>512</c>, <c>320</c>
/// for a pistol), and every expected value is derived by hand against the
/// pinned formula <c>DispersionAtZeroWu + (DispersionAtMaxWu -
/// DispersionAtZeroWu) * min(range, MaxEffectiveWu) / MaxEffectiveWu</c>
/// using C#'s truncating integer division:
/// <list type="bullet">
/// <item><description>Rifle at range 0: <c>32 + 224 * 0 / 800 = 32</c>.</description></item>
/// <item><description>Rifle at range 333 (a non-exact truncation case): <c>224 * 333 = 74,592</c>; <c>74,592 / 800 = 93</c> (truncated from 93.24); <c>32 + 93 = 125</c>.</description></item>
/// <item><description>Rifle at range 800 (exactly <c>MaxEffectiveWu</c>): <c>224 * 800 / 800 = 224</c>; <c>32 + 224 = 256</c>.</description></item>
/// <item><description>Rifle at range 5,000 (beyond <c>MaxEffectiveWu</c>, clamped to 800): same arithmetic as the row above, <c>256</c>.</description></item>
/// <item><description>Pistol at range 0: <c>64 + 448 * 0 / 320 = 64</c>.</description></item>
/// <item><description>Pistol at range 77 (a non-exact truncation case): <c>448 * 77 = 34,496</c>; <c>34,496 / 320 = 107</c> (truncated from 107.8); <c>64 + 107 = 171</c>.</description></item>
/// <item><description>Pistol at range 320 (exactly <c>MaxEffectiveWu</c>): <c>448 * 320 / 320 = 448</c>; <c>64 + 448 = 512</c>.</description></item>
/// <item><description>Pistol at range 900 (beyond <c>MaxEffectiveWu</c>, clamped to 320): same arithmetic as the row above, <c>512</c>.</description></item>
/// </list>
/// </remarks>
public sealed class AccuracyRulesTests
{
    private const int RifleDispersionAtZeroWu = 32;
    private const int RifleDispersionAtMaxWu = 256;
    private const int RifleMaxEffectiveWu = 800;

    private const int PistolDispersionAtZeroWu = 64;
    private const int PistolDispersionAtMaxWu = 512;
    private const int PistolMaxEffectiveWu = 320;

    // ------------------------------------------------------------------
    // Dispersion — hand-computed against the pinned formula. See the
    // type-level remarks for the arithmetic behind every expected value.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, RifleMaxEffectiveWu, 32)]
    [InlineData(333, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, RifleMaxEffectiveWu, 125)]
    [InlineData(RifleMaxEffectiveWu, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, RifleMaxEffectiveWu, 256)]
    [InlineData(5_000, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, RifleMaxEffectiveWu, 256)]
    [InlineData(0, PistolDispersionAtZeroWu, PistolDispersionAtMaxWu, PistolMaxEffectiveWu, 64)]
    [InlineData(77, PistolDispersionAtZeroWu, PistolDispersionAtMaxWu, PistolMaxEffectiveWu, 171)]
    [InlineData(PistolMaxEffectiveWu, PistolDispersionAtZeroWu, PistolDispersionAtMaxWu, PistolMaxEffectiveWu, 512)]
    [InlineData(900, PistolDispersionAtZeroWu, PistolDispersionAtMaxWu, PistolMaxEffectiveWu, 512)]
    public void Dispersion_HandComputed(
        int rangeWu, int dispersionAtZeroWu, int dispersionAtMaxWu, int maxEffectiveWu, int expected)
    {
        Assert.Equal(expected, AccuracyRules.Dispersion(rangeWu, dispersionAtZeroWu, dispersionAtMaxWu, maxEffectiveWu));
    }

    [Fact]
    public void Dispersion_NegativeRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccuracyRules.Dispersion(-1, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, RifleMaxEffectiveWu));
    }

    [Fact]
    public void Dispersion_NegativeDispersionAtZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccuracyRules.Dispersion(0, -1, RifleDispersionAtMaxWu, RifleMaxEffectiveWu));
    }

    [Fact]
    public void Dispersion_NegativeDispersionAtMax_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccuracyRules.Dispersion(0, RifleDispersionAtZeroWu, -1, RifleMaxEffectiveWu));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Dispersion_NonPositiveMaxEffectiveWu_Throws(int maxEffectiveWu)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccuracyRules.Dispersion(0, RifleDispersionAtZeroWu, RifleDispersionAtMaxWu, maxEffectiveWu));
    }

    // ------------------------------------------------------------------
    // DrawAngularErrorBam — reproducibility and structural bounds. No
    // literal draw value is pinned here: design section 9 names the stream
    // the draw comes from, not an expected output for any given key, and
    // the honest way to test a keyed deterministic draw without reading a
    // number back from the implementation is to prove it reproduces and
    // stays inside the contract's own stated bounds.
    // ------------------------------------------------------------------

    [Fact]
    public void DrawAngularErrorBam_SameSeedAndEntityId_IsReproducible()
    {
        var first = AccuracyRules.DrawAngularErrorBam(missionSeed: 12345UL, entityId: 7, dispersionBam: 256);
        var second = AccuracyRules.DrawAngularErrorBam(missionSeed: 12345UL, entityId: 7, dispersionBam: 256);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DrawAngularErrorBam_SameSeedAndEntityId_ReproducibleAcrossSeveralDispersions()
    {
        foreach (var dispersionBam in new[] { 0, 1, 32, 171, 512, 4096 })
        {
            var first = AccuracyRules.DrawAngularErrorBam(missionSeed: 999UL, entityId: 42, dispersionBam);
            var second = AccuracyRules.DrawAngularErrorBam(missionSeed: 999UL, entityId: 42, dispersionBam);

            Assert.Equal(first, second);
        }
    }

    /// <summary>
    /// <see cref="AccuracyRules.DrawAngularErrorBam"/>'s own documented
    /// contract: the draw is uniform over <c>2 * dispersionBam + 1</c>
    /// integers in <c>[-dispersionBam, +dispersionBam]</c>, a structural
    /// property of <c>SplitMix64.NextInt</c>'s <c>[0, exclusiveUpperBound)</c>
    /// contract shifted by <c>-dispersionBam</c>, provable without reading
    /// any specific draw value back from the implementation.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(171)]
    [InlineData(512)]
    [InlineData(32_767)]
    public void DrawAngularErrorBam_StaysWithinTheClosedDispersionSpan(int dispersionBam)
    {
        var draw = AccuracyRules.DrawAngularErrorBam(missionSeed: 55UL, entityId: 3, dispersionBam);

        Assert.InRange(draw, -dispersionBam, dispersionBam);
    }

    /// <summary>
    /// At <c>dispersionBam == 0</c> the span collapses to exactly one
    /// integer, <c>0</c>: <c>SplitMix64.NextInt(1)</c> always returns
    /// <c>0</c> (its exclusive upper bound is 1, so no other value is
    /// reachable), and <c>0 - 0 == 0</c>. This is derivable purely from
    /// that contract, for any seed and entity id, not read back from a run.
    /// </summary>
    [Fact]
    public void DrawAngularErrorBam_ZeroDispersion_IsAlwaysZero()
    {
        Assert.Equal(0, AccuracyRules.DrawAngularErrorBam(missionSeed: 1UL, entityId: 1, dispersionBam: 0));
        Assert.Equal(0, AccuracyRules.DrawAngularErrorBam(missionSeed: ulong.MaxValue, entityId: ulong.MaxValue, dispersionBam: 0));
        Assert.Equal(0, AccuracyRules.DrawAngularErrorBam(missionSeed: 0UL, entityId: 0, dispersionBam: 0));
    }

    [Fact]
    public void DrawAngularErrorBam_NegativeDispersion_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccuracyRules.DrawAngularErrorBam(missionSeed: 1UL, entityId: 1, dispersionBam: -1));
    }

    /// <summary>
    /// Task 78 widened <see cref="AccuracyRules.DrawAngularErrorBam"/>'s
    /// <c>entityId</c> parameter from <see langword="int"/> to
    /// <see langword="ulong"/>, to remove the <c>unchecked((int)...)</c> cast
    /// its one call site in <c>SandataSimulation.ProposeFire</c> applied
    /// against <c>OperatorState.EntityId</c>. Because the fold this method
    /// runs seeds the named <c>Accuracy</c> RNG stream design section 4's
    /// "ordering and randomness" rule governs, a parameter-width change that
    /// altered the folded value for any id that already fit in a
    /// non-negative <see langword="int"/> would move that stream — a
    /// preset-version change, not a refactor. These three triples were
    /// captured by calling the pre-change, <see langword="int"/>-parameter
    /// <see cref="AccuracyRules.DrawAngularErrorBam"/> with the same
    /// arguments immediately before the widening edit landed (a temporary
    /// <c>ITestOutputHelper</c> fact in this file printed the three values,
    /// then was deleted once they were recorded here) and are asserted again
    /// now, against the widened method, byte for byte.
    /// </summary>
    [Theory]
    [InlineData(12345UL, 7UL, 256, -122)]
    [InlineData(999UL, 42UL, 171, -147)]
    [InlineData(55UL, 3UL, 32_767, 30472)]
    public void DrawAngularErrorBam_PinnedDraws_MatchThePreWideningValues(
        ulong missionSeed, ulong entityId, int dispersionBam, int expectedDraw)
    {
        Assert.Equal(
            expectedDraw, AccuracyRules.DrawAngularErrorBam(missionSeed, entityId, dispersionBam));
    }
}
