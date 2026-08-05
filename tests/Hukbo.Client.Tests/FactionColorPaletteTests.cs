using System.Linq;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="FactionColorPalette.GetContingentGroundTint"/>, the pure
/// helper T14 adds so the per-contingent ground-base tint of design section 7
/// is testable without a graphics device. <see cref="PawnRenderer"/> itself
/// stays untested here — its own presentation tests, if any, belong in a
/// <c>PawnRenderer</c>-scoped file; this file owns only the color
/// derivation.
/// </summary>
public sealed class FactionColorPaletteTests
{
    private static string BuiltInCatalogPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");

    private static readonly ContingentState[] NonNoneStates =
    [
        ContingentState.Advance,
        ContingentState.Hold,
        ContingentState.Close,
        ContingentState.Break,
    ];

    [Theory]
    [InlineData("command")]
    [InlineData("field-manual")]
    [InlineData("signal")]
    [InlineData("broadcast")]
    [InlineData("high-contrast")]
    [InlineData("datu-court")]
    public void ContingentZeroReturnsTheUnmodifiedBaseColorUnderEveryNonNoneState(
        string themeId)
    {
        var theme = LoadTheme(themeId);

        foreach (var state in NonNoneStates)
        {
            Assert.Equal(
                theme.Colors.TeamA,
                FactionColorPalette.GetContingentGroundTint(
                    theme.Colors.TeamA,
                    contingentId: 0,
                    state));
            Assert.Equal(
                theme.Colors.TeamB,
                FactionColorPalette.GetContingentGroundTint(
                    theme.Colors.TeamB,
                    contingentId: 0,
                    state));
        }
    }

    [Theory]
    [InlineData("command")]
    [InlineData("field-manual")]
    [InlineData("signal")]
    [InlineData("broadcast")]
    [InlineData("high-contrast")]
    [InlineData("datu-court")]
    public void ContingentStateNoneAlwaysReturnsTheUnmodifiedBaseColor(
        string themeId)
    {
        var theme = LoadTheme(themeId);

        for (var contingentId = 0;
            contingentId < FactionColorPalette.MaximumContingentTints;
            contingentId++)
        {
            // A nonzero contingentId under ContingentState.None is exactly
            // the frozen-preset case: FormationPlanner deals contingent
            // membership at spawn whether or not the active preset ever
            // reads it, so a real IndependentPursuitV1 run carries nonzero
            // ContingentId values on agents whose ContingentState is still
            // None. The gate must hold for those values too, not only for
            // contingentId 0, or the frozen preset would stop looking like
            // it looks today.
            Assert.Equal(
                theme.Colors.TeamA,
                FactionColorPalette.GetContingentGroundTint(
                    theme.Colors.TeamA,
                    contingentId,
                    ContingentState.None));
            Assert.Equal(
                theme.Colors.TeamB,
                FactionColorPalette.GetContingentGroundTint(
                    theme.Colors.TeamB,
                    contingentId,
                    ContingentState.None));
        }
    }

    [Theory]
    [InlineData("command")]
    [InlineData("field-manual")]
    [InlineData("signal")]
    [InlineData("broadcast")]
    [InlineData("high-contrast")]
    [InlineData("datu-court")]
    public void TheEightContingentTintsArePairwiseDistinctWithinAFaction(
        string themeId)
    {
        var theme = LoadTheme(themeId);

        AssertEightDistinctTints(theme.Colors.TeamA);
        AssertEightDistinctTints(theme.Colors.TeamB);

        static void AssertEightDistinctTints(Color baseColor)
        {
            var tints = new Color[FactionColorPalette.MaximumContingentTints];
            for (var contingentId = 0;
                contingentId < tints.Length;
                contingentId++)
            {
                tints[contingentId] = FactionColorPalette.GetContingentGroundTint(
                    baseColor,
                    contingentId,
                    ContingentState.Advance);
            }

            Assert.Equal(tints.Length, tints.Distinct().Count());
        }
    }

    [Theory]
    [InlineData("command")]
    [InlineData("field-manual")]
    [InlineData("signal")]
    [InlineData("broadcast")]
    [InlineData("high-contrast")]
    [InlineData("datu-court")]
    public void NoContingentTintCollidesWithTheOtherFactionsBaseColor(
        string themeId)
    {
        var theme = LoadTheme(themeId);

        for (var contingentId = 1;
            contingentId < FactionColorPalette.MaximumContingentTints;
            contingentId++)
        {
            var tintedA = FactionColorPalette.GetContingentGroundTint(
                theme.Colors.TeamA,
                contingentId,
                ContingentState.Hold);
            var tintedB = FactionColorPalette.GetContingentGroundTint(
                theme.Colors.TeamB,
                contingentId,
                ContingentState.Hold);

            Assert.NotEqual(theme.Colors.TeamB, tintedA);
            Assert.NotEqual(theme.Colors.TeamA, tintedB);
        }
    }

    [Fact]
    public void EveryTintIsDerivedFromTheThemeRoleRatherThanAFixedLiteral()
    {
        // "signal" is chosen deliberately: its TeamA/TeamB roles
        // (#26D9FF / #FF496C) differ from the fixed pawn-color literals
        // FactionColorPalette.GetPawnColor returns for every theme
        // (#40A4FF / #FF5B69). If GetContingentGroundTint's contingent-0
        // result matched the fixed literal instead of the theme role it was
        // handed, this fact would catch it.
        var signal = LoadTheme("signal");
        var fixedFactionAColor = FactionColorPalette.GetPawnColor(0);
        var fixedFactionBColor = FactionColorPalette.GetPawnColor(1);

        Assert.NotEqual(fixedFactionAColor, signal.Colors.TeamA);
        Assert.NotEqual(fixedFactionBColor, signal.Colors.TeamB);

        Assert.Equal(
            signal.Colors.TeamA,
            FactionColorPalette.GetContingentGroundTint(
                signal.Colors.TeamA,
                contingentId: 0,
                ContingentState.Advance));
        Assert.Equal(
            signal.Colors.TeamB,
            FactionColorPalette.GetContingentGroundTint(
                signal.Colors.TeamB,
                contingentId: 0,
                ContingentState.Advance));
        Assert.NotEqual(
            fixedFactionAColor,
            FactionColorPalette.GetContingentGroundTint(
                signal.Colors.TeamA,
                contingentId: 3,
                ContingentState.Advance));
    }

    [Fact]
    public void TheDerivationIsTotalAcrossAllSixShippedThemes()
    {
        var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);
        Assert.Equal(6, catalog.Themes.Count);

        foreach (var theme in catalog.Themes)
        {
            for (var contingentId = 0;
                contingentId < FactionColorPalette.MaximumContingentTints;
                contingentId++)
            {
                foreach (var state in NonNoneStates)
                {
                    // No exception, and every channel stays a valid byte —
                    // Color.Lerp already guarantees the latter, but the
                    // point of this fact is that every one of the six
                    // shipped themes reaches this call at all, for both
                    // factions and across the full contingent range.
                    _ = FactionColorPalette.GetContingentGroundTint(
                        theme.Colors.TeamA,
                        contingentId,
                        state);
                    _ = FactionColorPalette.GetContingentGroundTint(
                        theme.Colors.TeamB,
                        contingentId,
                        state);
                }
            }
        }
    }

    private static UiTheme LoadTheme(string themeId) =>
        UiThemeCatalog.Load(BuiltInCatalogPath).GetRequired(themeId);
}
