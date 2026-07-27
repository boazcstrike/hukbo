namespace Hukbo.Client.Theming;

/// <summary>
/// Plain JSON deserialization targets for the theme catalog document. These
/// are intentionally untyped/nullable-heavy — <see cref="UiThemeCatalog"/>'s
/// validation pass is what turns them into the typed runtime contract
/// (<see cref="UiTheme"/>, <see cref="UiThemeStandards"/>, etc.). Split out
/// of the main validator file to keep it under the file-size cap.
/// </summary>
internal sealed partial class UiThemeCatalog
{
    private sealed class CatalogDocument
    {
        public int SchemaVersion { get; init; }

        public string? DefaultThemeId { get; init; }

        public StandardsDocument? Standards { get; init; }

        public List<ContrastPairDocument>? ContrastPairs { get; init; }

        public List<ThemeDocument>? Themes { get; init; }
    }

    private sealed class ContrastPairDocument
    {
        public string? Foreground { get; init; }

        public string? Background { get; init; }

        public double MinimumRatio { get; init; }
    }

    private sealed class ThemeDocument
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public Dictionary<string, string>? Colors { get; init; }

        public MetricsDocument? Metrics { get; init; }
    }

    private sealed class MetricsDocument
    {
        public int BorderThickness { get; init; }

        public int FocusThickness { get; init; }

        public int ShadowOffset { get; init; }
    }

    private sealed class StandardsDocument
    {
        public int RequiredThemeCount { get; init; }

        public List<string>? RequiredThemeIds { get; init; }

        public List<string>? RequiredColorRoles { get; init; }

        public List<string>? RequiredInteractionStates { get; init; }

        public MetricRangesDocument? MetricRanges { get; init; }

        public List<string>? AllowedFontAssetIds { get; init; }

        public SharedStandardsDocument? Shared { get; init; }
    }

    private sealed class MetricRangesDocument
    {
        public NumberRangeDocument? BorderThickness { get; init; }

        public NumberRangeDocument? FocusThickness { get; init; }

        public NumberRangeDocument? ShadowOffset { get; init; }

        public NumberRangeDocument? TargetSize { get; init; }
    }

    private sealed class NumberRangeDocument
    {
        public double Minimum { get; init; }

        public double Maximum { get; init; }
    }

    private sealed class SharedStandardsDocument
    {
        public MenuLayoutDocument? Menu { get; init; }

        public SelectorLayoutDocument? Selector { get; init; }

        public ArmyCompositionLayoutDocument? ArmyComposition { get; init; }

        /// <summary>
        /// Role-name to content-pipeline asset-id map, one entry per
        /// <see cref="UiFontRole"/> (keyed on the camel-cased role name, for
        /// example <c>"caption"</c>).
        /// </summary>
        public Dictionary<string, string>? Fonts { get; init; }

        /// <summary>
        /// Slot-name to font-role-name map for the eight theme text slots
        /// (for example <c>"menuTitle"</c> to <c>"Display"</c>). Each value
        /// must parse via <see cref="UiFontRamp.Parse"/>.
        /// </summary>
        public Dictionary<string, string>? TextRoles { get; init; }
    }

    private sealed class MenuLayoutDocument
    {
        public int PanelWidth { get; init; }

        public int PanelHeight { get; init; }

        public int ButtonWidth { get; init; }

        public int ButtonHeight { get; init; }

        public int ButtonGap { get; init; }

        public int TitleTopOffset { get; init; }

        public int SubtitleTopOffset { get; init; }

        public int SelectorTopOffset { get; init; }

        public int SelectorGap { get; init; }

        public int HelperBottomOffset { get; init; }
    }

    private sealed class SelectorLayoutDocument
    {
        public int Height { get; init; }

        public int ArrowWidth { get; init; }

        public int Padding { get; init; }

        public int MinimumTargetSize { get; init; }

        public int LabelTopOffset { get; init; }

        public int NameTopOffset { get; init; }

        public int MarkerTopOffset { get; init; }

        public int SwatchWidth { get; init; }

        public int SwatchHeight { get; init; }

        public int SwatchGap { get; init; }
    }

    private sealed class ArmyCompositionLayoutDocument
    {
        public int PanelWidth { get; init; }

        public int PanelHeight { get; init; }

        public int RowHeight { get; init; }

        public int RowGap { get; init; }

        public int StepperWidth { get; init; }

        public int ArrowWidth { get; init; }
    }
}
