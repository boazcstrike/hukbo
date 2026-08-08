using Sandata.Core.Combat;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 31 of docs/plans/2026-08-07-sandata-scaffold.md: pins
/// <see cref="FireModeSelection.SelectMode"/> against design section 9's
/// ordered, total fire-mode band-selection rule, by hand-computed expected
/// values rather than by reading the implementation back. Every expected
/// value below is derived directly from the published rule and from the
/// rifle and pistol range-band constants <c>FirearmCatalog</c> already bakes
/// (rifle: <c>AutoBandMaxWu = 240</c>, <c>BurstBandMaxWu = 320</c>,
/// <c>SingleBandMaxWu = 800</c>; pistol: <c>AutoBandMaxWu = 0</c>,
/// <c>BurstBandMaxWu = 0</c>, <c>SingleBandMaxWu = 320</c>), one weapon per
/// each of the five distinct <see cref="FireModeSet"/> combinations design
/// section 9 records across the 38-row roster:
/// <list type="bullet">
/// <item><description><see cref="FirearmId.Ak47"/> — <c>Safe | Single | Auto</c>.</description></item>
/// <item><description><see cref="FirearmId.M4"/> — <c>Safe | Single | Burst3</c>.</description></item>
/// <item><description><see cref="FirearmId.Ak12"/> — <c>Safe | Single | Burst2 | Auto</c>.</description></item>
/// <item><description><see cref="FirearmId.Glock17Gen5"/> — <c>Single</c> alone.</description></item>
/// <item><description><see cref="FirearmId.Beretta92Fs"/> — <c>Safe | Single</c>.</description></item>
/// </list>
/// </summary>
public sealed class FireModeSelectionTests
{
    private const int RifleAutoBandMaxWu = 240;
    private const int RifleBurstBandMaxWu = 320;
    private const int RifleSingleBandMaxWu = 800;

    private const int PistolAutoBandMaxWu = 0;
    private const int PistolBurstBandMaxWu = 0;
    private const int PistolSingleBandMaxWu = 320;

    // ------------------------------------------------------------------
    // Ak47 — Safe | Single | Auto, rifle bands (240, 320, 800).
    //
    // Hand-derived: at range 240, 240 <= 240 and Modes has Auto -> Auto.
    // At 241, 241 > 240 (no Auto); no Burst3 or Burst2 flag at all, so both
    // burst branches are false at every range; 241 <= 800 and Modes has
    // Single -> Single. The burst-band boundary (320/321) produces no
    // observable change for this weapon, since it never carries a burst
    // flag: both sides resolve to Single by the same fallthrough. At 800,
    // 800 <= 800 and Modes has Single -> Single. At 801, 801 > 800 and no
    // remaining branch matches -> no engagement.
    // ------------------------------------------------------------------

    private const FireModeSet Ak47Modes = FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto;

    [Fact]
    public void Ak47_AtAutoBandBoundary_SelectsAuto()
    {
        Assert.Equal(
            FireModeSet.Auto,
            FireModeSelection.SelectMode(Ak47Modes, RifleAutoBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak47_JustBeyondAutoBandBoundary_FallsThroughToSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Ak47Modes, RifleAutoBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak47_AtSingleBandBoundary_SelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Ak47Modes, RifleSingleBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak47_JustBeyondSingleBandBoundary_ProducesNoEngagement()
    {
        Assert.Null(
            FireModeSelection.SelectMode(Ak47Modes, RifleSingleBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    // ------------------------------------------------------------------
    // M4 — Safe | Single | Burst3, rifle bands (240, 320, 800).
    //
    // Hand-derived: no Auto flag at all, so the auto-band boundary produces
    // no observable change (both 240 and 241 fall through to the Burst3
    // test). At 320, 320 <= 320 and Modes has Burst3 -> Burst3. At 321,
    // 321 > 320 (no Burst3 or Burst2); 321 <= 800 and Modes has Single ->
    // Single. At 800, Single. At 801, no engagement.
    // ------------------------------------------------------------------

    private const FireModeSet M4Modes = FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst3;

    [Fact]
    public void M4_AtAutoBandBoundary_HasNoAutoFlag_FallsThroughToBurst3()
    {
        Assert.Equal(
            FireModeSet.Burst3,
            FireModeSelection.SelectMode(M4Modes, RifleAutoBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void M4_AtBurstBandBoundary_SelectsBurst3()
    {
        Assert.Equal(
            FireModeSet.Burst3,
            FireModeSelection.SelectMode(M4Modes, RifleBurstBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void M4_JustBeyondBurstBandBoundary_FallsThroughToSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(M4Modes, RifleBurstBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void M4_AtSingleBandBoundary_SelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(M4Modes, RifleSingleBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void M4_JustBeyondSingleBandBoundary_ProducesNoEngagement()
    {
        Assert.Null(
            FireModeSelection.SelectMode(M4Modes, RifleSingleBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    // ------------------------------------------------------------------
    // Ak12 (2018/2021) — Safe | Single | Burst2 | Auto, rifle bands
    // (240, 320, 800). This is the one representative weapon whose bands
    // and flags make all three boundaries independently observable.
    //
    // Hand-derived: at 240, 240 <= 240 and Modes has Auto -> Auto. At 241,
    // 241 > 240 (no Auto); no Burst3 flag; 241 <= 320 and Modes has Burst2
    // -> Burst2. At 320, 320 <= 320 and Modes has Burst2 -> Burst2 (Auto
    // already excluded since 320 > 240). At 321, 321 > 320 (no Burst2);
    // 321 <= 800 and Modes has Single -> Single. At 800, Single. At 801, no
    // engagement.
    // ------------------------------------------------------------------

    private const FireModeSet Ak12Modes =
        FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst2 | FireModeSet.Auto;

    [Fact]
    public void Ak12_AtAutoBandBoundary_SelectsAuto()
    {
        Assert.Equal(
            FireModeSet.Auto,
            FireModeSelection.SelectMode(Ak12Modes, RifleAutoBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak12_JustBeyondAutoBandBoundary_SelectsBurst2()
    {
        Assert.Equal(
            FireModeSet.Burst2,
            FireModeSelection.SelectMode(Ak12Modes, RifleAutoBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak12_AtBurstBandBoundary_SelectsBurst2()
    {
        Assert.Equal(
            FireModeSet.Burst2,
            FireModeSelection.SelectMode(Ak12Modes, RifleBurstBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak12_JustBeyondBurstBandBoundary_FallsThroughToSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Ak12Modes, RifleBurstBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak12_AtSingleBandBoundary_SelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Ak12Modes, RifleSingleBandMaxWu, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    [Fact]
    public void Ak12_JustBeyondSingleBandBoundary_ProducesNoEngagement()
    {
        Assert.Null(
            FireModeSelection.SelectMode(Ak12Modes, RifleSingleBandMaxWu + 1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }

    // ------------------------------------------------------------------
    // Glock17Gen5 — Single alone, pistol bands (0, 0, 320).
    //
    // Hand-derived: no Auto or Burst flag at all, so the degenerate
    // auto/burst-band boundary at 0 produces no observable change (0 and 1
    // both fall through to the Single test: 0 <= 320 and 1 <= 320). At 320,
    // 320 <= 320 and Modes has Single -> Single. At 321, 321 > 320 and no
    // remaining branch matches -> no engagement.
    // ------------------------------------------------------------------

    private const FireModeSet Glock17Gen5Modes = FireModeSet.Single;

    [Fact]
    public void Glock17Gen5_AtDegenerateZeroBandBoundary_FallsThroughToSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Glock17Gen5Modes, PistolAutoBandMaxWu, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Glock17Gen5_JustBeyondDegenerateZeroBandBoundary_StillSelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Glock17Gen5Modes, PistolAutoBandMaxWu + 1, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Glock17Gen5_AtSingleBandBoundary_SelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Glock17Gen5Modes, PistolSingleBandMaxWu, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Glock17Gen5_JustBeyondSingleBandBoundary_ProducesNoEngagement()
    {
        Assert.Null(
            FireModeSelection.SelectMode(Glock17Gen5Modes, PistolSingleBandMaxWu + 1, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    // ------------------------------------------------------------------
    // Beretta92Fs — Safe | Single, pistol bands (0, 0, 320). Identical
    // arithmetic to Glock17Gen5 above: Safe plays no part in the rule, so
    // adding it changes nothing about which mode is selected.
    // ------------------------------------------------------------------

    private const FireModeSet Beretta92FsModes = FireModeSet.Safe | FireModeSet.Single;

    [Fact]
    public void Beretta92Fs_AtDegenerateZeroBandBoundary_FallsThroughToSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Beretta92FsModes, PistolAutoBandMaxWu, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Beretta92Fs_JustBeyondDegenerateZeroBandBoundary_StillSelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Beretta92FsModes, PistolAutoBandMaxWu + 1, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Beretta92Fs_AtSingleBandBoundary_SelectsSingle()
    {
        Assert.Equal(
            FireModeSet.Single,
            FireModeSelection.SelectMode(Beretta92FsModes, PistolSingleBandMaxWu, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    [Fact]
    public void Beretta92Fs_JustBeyondSingleBandBoundary_ProducesNoEngagement()
    {
        Assert.Null(
            FireModeSelection.SelectMode(Beretta92FsModes, PistolSingleBandMaxWu + 1, PistolAutoBandMaxWu, PistolBurstBandMaxWu, PistolSingleBandMaxWu));
    }

    // ------------------------------------------------------------------
    // The ordering rule itself: Burst3 tested before Burst2. Design
    // section 9 records that no row in the current 38-weapon roster
    // carries both flags, so this fact is exercised against a synthetic
    // mode set rather than a catalog weapon, per that same section's own
    // reasoning: the ordering must never become load-bearing silently.
    // ------------------------------------------------------------------

    [Fact]
    public void SyntheticWeaponCarryingBothBurstFlags_SelectsBurst3OverBurst2()
    {
        const FireModeSet bothBurstFlags = FireModeSet.Burst2 | FireModeSet.Burst3;

        Assert.Equal(
            FireModeSet.Burst3,
            FireModeSelection.SelectMode(
                bothBurstFlags,
                rangeWu: 50,
                autoBandMaxWu: 0,
                burstBandMaxWu: 100,
                singleBandMaxWu: 200));
    }

    // ------------------------------------------------------------------
    // Argument validation.
    // ------------------------------------------------------------------

    [Fact]
    public void SelectMode_NegativeRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FireModeSelection.SelectMode(Ak47Modes, -1, RifleAutoBandMaxWu, RifleBurstBandMaxWu, RifleSingleBandMaxWu));
    }
}
