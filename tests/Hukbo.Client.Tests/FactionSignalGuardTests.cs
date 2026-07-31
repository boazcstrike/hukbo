using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// VIS-033 — the R-W6.10 color-blind no-regression guard and the R-W6.11
/// theme-contrast-continuity confirmation
/// (implementation-plan-draft.md, "Color-blind no-regression guard and
/// theme contrast continuity"; visual-system-integration-design.md,
/// "Color-blind shape redundancy"). Per OD-7 (resolved 2026-07-28) this task
/// holds the no-regression floor only — no shape-redundant faction marker
/// ships in this pass — so the sweep below records the true relationship
/// between every color the visual-improvement package introduced and the
/// three fixed faction constants, exactly as <see cref="DyePalette"/>,
/// <see cref="WeaponVisualCatalog"/>, and <see cref="ShieldVisualCatalog"/>'s
/// own remarks already do for the sibling ground/clothing envelope, rather
/// than silently patching a shortfall this task's prohibited scope forbids
/// fixing by changing a faction constant.
///
/// R-W6.11's own theme contrast-pair validation is <see cref="UiThemeCatalog"/>
/// territory and is not touched here — this package added no theme roles, so
/// <see cref="UiThemeCatalogTests"/>'s own green run against all six themes
/// is the recorded confirmation this task's step 3 asks for.
/// </summary>
public sealed class FactionSignalGuardTests
{
    // --- Reference colors mirroring production sources, matching
    // AppearanceComponentCatalogTests' and WeaponVisualCatalogTests' own
    // "mirrors ContrastEnvelopeTests' own reference set" convention. ---

    private static readonly Color FactionBlue = new(64, 164, 255);
    private static readonly Color FactionRed = new(255, 91, 105);
    private static readonly Color FactionGold = new(231, 199, 84);
    private static readonly Color[] BattleFactionConstants = [FactionBlue, FactionRed];

    // The six shipped themes' ArenaSurface/ArenaBorder pair. Hex values
    // mirror src/Hukbo.Client/Content/Themes/ui-theme-standards.json
    // (command, field-manual, signal, broadcast, high-contrast, datu-court)
    // — the same
    // mirroring convention WeaponVisualCatalogTests' own GroundShadeXxx
    // constants use. Unlike those pre-lerped constants, the shades below are
    // computed with the real <see cref="Color.Lerp"/> call at test time
    // (matching production exactly, byte-truncation and all), because this
    // sweep runs right up against the 80-unit threshold for one theme and a
    // hand-rounded approximation is not safe there.
    private static readonly (string Id, Color Surface, Color Border)[] Themes =
    [
        ("command", new Color(19, 29, 43), new Color(116, 143, 178)),
        ("field-manual", new Color(229, 212, 170), new Color(100, 88, 62)),
        ("signal", new Color(7, 16, 19), new Color(92, 135, 142)),
        ("broadcast", new Color(242, 245, 248), new Color(104, 116, 130)),
        ("high-contrast", new Color(0, 0, 0), new Color(255, 255, 255)),
        ("datu-court", new Color(74, 81, 56), new Color(195, 163, 90)),
    ];

    // The Field Manual theme's parchment-tan ground is the one shade family
    // that sits structurally close to the third ("other faction") gold
    // constant — the same warm-tan collision the catalog exceptions below
    // record, now confirmed at the actual runtime Color.Lerp output rather
    // than a hand-computed approximation. Every interpolation this package
    // ships above the bare ground (0.00) falls inside this set; only the
    // bare ground itself (0.00) clears.
    private static readonly HashSet<float> FieldManualKnownGoldCollisionInterpolations =
    [
        0.06f, 0.10f, 0.12f, 0.14f, 0.18f, 0.22f,
    ];

    private static IEnumerable<(string ThemeId, Color Surface, Color Border, string Source, float Interpolation)>
        AllShadeInterpolationPoints()
    {
        foreach (var theme in Themes)
        {
            foreach (var t in PlainsBackdropGeometry.GroundShadeInterpolation)
            {
                yield return (theme.Id, theme.Surface, theme.Border, "ground", t);
            }

            foreach (var t in GrassGeometry.GrassShadeInterpolation)
            {
                yield return (theme.Id, theme.Surface, theme.Border, "grass", t);
            }

            yield return (
                theme.Id, theme.Surface, theme.Border, "trample", GrassGeometry.TrampleMarkShadeInterpolation);
            yield return (theme.Id, theme.Surface, theme.Border, "dust", DustGeometry.ShadeInterpolation);
        }
    }

    public static TheoryData<string, Color> AllShadeData()
    {
        var data = new TheoryData<string, Color>();
        foreach (var point in AllShadeInterpolationPoints())
        {
            var shade = Color.Lerp(point.Surface, point.Border, point.Interpolation);
            data.Add($"{point.ThemeId}/{point.Source}@{point.Interpolation:0.00}", shade);
        }

        return data;
    }

    public static TheoryData<string, Color, bool> ShadeDataWithExpectedFullClearance()
    {
        var data = new TheoryData<string, Color, bool>();
        foreach (var point in AllShadeInterpolationPoints())
        {
            var shade = Color.Lerp(point.Surface, point.Border, point.Interpolation);
            var expectedClears = point.ThemeId != "field-manual" ||
                !FieldManualKnownGoldCollisionInterpolations.Contains(point.Interpolation);
            data.Add($"{point.ThemeId}/{point.Source}@{point.Interpolation:0.00}", shade, expectedClears);
        }

        return data;
    }

    // --- FactionSignalConstants / KeepsFactionSignalDistance: the guard
    // mechanism itself ---

    [Fact]
    public void FactionSignalConstants_IsExactlyTheThreeFixedFactionColors()
    {
        Assert.Equal(3, AppearancePresetValidator.FactionSignalConstants.Count);
        Assert.Contains(FactionBlue, AppearancePresetValidator.FactionSignalConstants);
        Assert.Contains(FactionRed, AppearancePresetValidator.FactionSignalConstants);
        Assert.Contains(FactionGold, AppearancePresetValidator.FactionSignalConstants);
    }

    [Theory]
    [MemberData(nameof(SyntheticFactionColoredEntries))]
    public void KeepsFactionSignalDistance_RejectsADeliberatelyFactionColoredSyntheticEntry(Color candidate)
    {
        // The sweep's own negative control (implementation-plan-draft.md
        // VIS-033, "Automated verification": "a deliberately faction-colored
        // synthetic entry fails"): a color equal to a fixed faction constant
        // is zero channel-distance from itself, so it can never clear
        // ContrastEnvelope.MinimumFactionDyeDistance.
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(candidate));
    }

    public static TheoryData<Color> SyntheticFactionColoredEntries()
    {
        var data = new TheoryData<Color>();
        data.Add(FactionBlue);
        data.Add(FactionRed);
        data.Add(FactionGold);
        return data;
    }

    [Fact]
    public void KeepsFactionSignalDistance_AcceptsAColorFarFromEveryFactionConstant()
    {
        // Positive control: a neutral mid-grey, chosen only to be visibly
        // distant from all three faction hues, must clear.
        Assert.True(AppearancePresetValidator.KeepsFactionSignalDistance(new Color(128, 128, 128)));
    }

    // --- DyePalette (VIS-017): every constant except the documented
    // GoldAccent/TurmericYellow exception clears the full envelope ---

    [Theory]
    [MemberData(nameof(NonExceptionDyeColors))]
    public void DyePalette_NonExceptionColorsClearTheFactionSignalEnvelope(Color dye)
    {
        Assert.True(AppearancePresetValidator.KeepsFactionSignalDistance(dye));
    }

    public static TheoryData<Color> NonExceptionDyeColors()
    {
        var data = new TheoryData<Color>();
        data.Add(DyePalette.UndyedCream);
        data.Add(DyePalette.IndigoBlue);
        data.Add(DyePalette.DeepBlueBlack);
        data.Add(DyePalette.SappanRed);
        data.Add(DyePalette.BarkBrown);
        data.Add(DyePalette.IronBlueBlack);
        return data;
    }

    // GoldAccent and TurmericYellow are the documented, accepted exception
    // (DyePalette's own remarks; AppearanceComponentCatalogTests already
    // records the ContrastEnvelope-level relationship). Recorded again here
    // through the guard itself so KeepsFactionSignalDistance's behavior on
    // the known exception is pinned, not just ContrastEnvelope's.
    [Fact]
    public void DyePalette_GoldAccentDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(DyePalette.GoldAccent));
    }

    [Fact]
    public void DyePalette_GoldAccentClearsOnlyTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.GoldAccent, BattleFactionConstants, ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.GoldAccent, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    [Fact]
    public void DyePalette_TurmericYellowDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(DyePalette.TurmericYellow));
    }

    [Fact]
    public void DyePalette_TurmericYellowClearsOnlyTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.TurmericYellow, BattleFactionConstants, ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.TurmericYellow, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    // --- WeaponVisualCatalog palette (VIS-010/011): CharredWoodBrown,
    // IronWornGrey, and PalmRattanOchre clear the full envelope.
    // GripWarmOchre and RattanLashingTone do not — see the dedicated facts
    // below, including GripWarmOchre's own newly-discovered near-miss
    // against the Red battle-faction constant itself. ---

    [Theory]
    [MemberData(nameof(NonExceptionWeaponTones))]
    public void WeaponVisualCatalog_NonExceptionTonesClearTheFactionSignalEnvelope(Color tone)
    {
        Assert.True(AppearancePresetValidator.KeepsFactionSignalDistance(tone));
    }

    public static TheoryData<Color> NonExceptionWeaponTones()
    {
        var data = new TheoryData<Color>();
        data.Add(WeaponVisualCatalog.CharredWoodBrown);
        data.Add(WeaponVisualCatalog.IronWornGrey);
        data.Add(WeaponVisualCatalog.PalmRattanOchre);
        return data;
    }

    // GripWarmOchre (VIS-010, OD-W1-b): a newly-discovered shortfall this
    // sweep is the first to check for — it was tuned only against
    // ContrastEnvelope.MinimumGroundDistance and MinimumClothingDistance
    // (WeaponVisualCatalog.cs's own remarks), never against a faction
    // constant. It clears Blue and (barely) fails Red at 79.42 channel-
    // distance units against the 80-unit floor — a 0.58-unit shortfall
    // against a live battle-faction color, not merely the unreachable third
    // constant the other exceptions below collide with. Retuning
    // GripWarmOchre is a WeaponVisualCatalog.cs change, outside this task's
    // file list (VIS-033 owns the guard, not WeaponVisualCatalog.cs); this
    // sweep records the true relationship and surfaces it for a follow-up
    // task rather than silently asserting past it.
    [Fact]
    public void WeaponVisualCatalog_GripWarmOchreDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(WeaponVisualCatalog.GripWarmOchre));
    }

    [Fact]
    public void WeaponVisualCatalog_GripWarmOchreClearsOnlyTheBlueFactionConstant()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.GripWarmOchre, [FactionBlue], ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.GripWarmOchre, [FactionRed], ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.GripWarmOchre, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    // RattanLashingTone (VIS-011): the Wasay's lashing-band accent, another
    // warm-ochre tone this sweep is the first to check against a faction
    // constant. Clears both battle factions; fails only the unreachable
    // third (Gold) constant, the same structural collision DyePalette's
    // GoldAccent/TurmericYellow already record.
    [Fact]
    public void WeaponVisualCatalog_RattanLashingToneDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(WeaponVisualCatalog.RattanLashingTone));
    }

    [Fact]
    public void WeaponVisualCatalog_RattanLashingToneClearsOnlyTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.RattanLashingTone,
            BattleFactionConstants,
            ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.RattanLashingTone, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    // --- ShieldVisualCatalog palette (VIS-013/014): ResinBrownTone clears
    // the full envelope. PalmWoodPale and LightHardwoodTan do not — both
    // pale/mid wood tones, the same warm-tan collision as above. ---

    [Theory]
    [MemberData(nameof(NonExceptionShieldTones))]
    public void ShieldVisualCatalog_NonExceptionTonesClearTheFactionSignalEnvelope(Color tone)
    {
        Assert.True(AppearancePresetValidator.KeepsFactionSignalDistance(tone));
    }

    public static TheoryData<Color> NonExceptionShieldTones()
    {
        var data = new TheoryData<Color>();
        data.Add(ShieldVisualCatalog.ResinBrownTone);
        return data;
    }

    [Fact]
    public void ShieldVisualCatalog_PalmWoodPaleDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(ShieldVisualCatalog.PalmWoodPale));
    }

    [Fact]
    public void ShieldVisualCatalog_PalmWoodPaleClearsOnlyTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.PalmWoodPale,
            BattleFactionConstants,
            ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.PalmWoodPale, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    [Fact]
    public void ShieldVisualCatalog_LightHardwoodTanDoesNotClearTheFactionSignalEnvelope()
    {
        Assert.False(AppearancePresetValidator.KeepsFactionSignalDistance(ShieldVisualCatalog.LightHardwoodTan));
    }

    [Fact]
    public void ShieldVisualCatalog_LightHardwoodTanClearsOnlyTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.LightHardwoodTan,
            BattleFactionConstants,
            ContrastEnvelope.MinimumFactionDyeDistance));
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.LightHardwoodTan, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    // --- Theme-derived grass, ground, trample, and dust shades
    // (battlefield-environment-design.md): every shade this package draws is
    // Color.Lerp(ArenaSurface, ArenaBorder, t) for a t at or below
    // PlainsBackdropGeometry.MaximumBackdropInterpolation (0.22), read
    // straight from the shipped interpolation ladders
    // (PlainsBackdropGeometry.GroundShadeInterpolation,
    // GrassGeometry.GrassShadeInterpolation,
    // GrassGeometry.TrampleMarkShadeInterpolation,
    // DustGeometry.ShadeInterpolation) so this sweep can never drift from
    // what the renderers actually draw. ---

    [Theory]
    [MemberData(nameof(AllShadeData))]
    public void ThemeDerivedShade_AlwaysClearsTheTwoBattleFactionConstants(string label, Color shade)
    {
        Assert.True(
            ContrastEnvelope.IsWithinEnvelope(
                shade, BattleFactionConstants, ContrastEnvelope.MinimumFactionDyeDistance),
            label);
    }

    // Field Manual's parchment-tan ground is the one theme whose shade,
    // once interpolated at all toward ArenaBorder, sits close enough to the
    // third (Gold) faction constant to fail — the same warm-tan collision
    // the catalog exceptions above record, confirmed here at the actual
    // runtime Color.Lerp output. Every other theme, and Field Manual's own
    // bare ground (interpolation 0.00), clear the full three-constant
    // envelope everywhere this package draws.
    [Theory]
    [MemberData(nameof(ShadeDataWithExpectedFullClearance))]
    public void ThemeDerivedShade_ClearsTheFullFactionSignalEnvelopeExceptTheDocumentedFieldManualGap(
        string label, Color shade, bool expectedClears)
    {
        Assert.True(
            AppearancePresetValidator.KeepsFactionSignalDistance(shade) == expectedClears,
            label);
    }
}
