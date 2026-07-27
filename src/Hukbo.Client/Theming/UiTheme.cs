using Microsoft.Xna.Framework;

namespace Hukbo.Client.Theming;

internal sealed record UiTheme(
    string Id,
    string DisplayName,
    UiThemeColors Colors,
    UiThemeMetrics Metrics);

internal sealed record UiThemeColors(
    Color CanvasBackground,
    Color ArenaSurface,
    Color ArenaBorder,
    Color StatusSurface,
    Color OverlayScrim,
    Color PanelSurface,
    Color PanelAlternate,
    Color PanelBorder,
    Color TextPrimary,
    Color TextSecondary,
    Color TextDisabled,
    Color TextInverse,
    Color ActionDefault,
    Color ActionHover,
    Color ActionFocus,
    Color ActionPressed,
    Color ActionActive,
    Color ActionDisabled,
    Color StatusInfo,
    Color StatusSuccess,
    Color StatusWarning,
    Color StatusDanger,
    Color TeamA,
    Color TeamB,
    Color OtherFaction,
    Color Selection,
    Color NewEvent);

internal sealed record UiThemeMetrics(
    int BorderThickness,
    int FocusThickness,
    int ShadowOffset);

internal sealed record UiThemeStandards(
    int RequiredThemeCount,
    IReadOnlyList<string> RequiredThemeIds,
    IReadOnlyList<string> RequiredColorRoles,
    IReadOnlyList<string> RequiredInteractionStates,
    UiMetricRanges MetricRanges,
    IReadOnlyList<string> AllowedFontAssetIds,
    UiSharedStandards Shared);

internal sealed record UiMetricRanges(
    UiIntegerRange BorderThickness,
    UiIntegerRange FocusThickness,
    UiIntegerRange ShadowOffset,
    UiIntegerRange TargetSize);

internal sealed record UiIntegerRange(int Minimum, int Maximum);

internal sealed record UiSharedStandards(
    UiMenuLayout Menu,
    UiThemeSelectorLayout Selector,
    UiArmyCompositionLayout ArmyComposition,
    UiFontAssignments Fonts,
    UiTextRoles TextRoles);

internal sealed record UiMenuLayout(
    int PanelWidth,
    int PanelHeight,
    int ButtonWidth,
    int ButtonHeight,
    int ButtonGap,
    int TitleTopOffset,
    int SubtitleTopOffset,
    int SelectorTopOffset,
    int SelectorGap,
    int HelperBottomOffset);

internal sealed record UiThemeSelectorLayout(
    int Height,
    int ArrowWidth,
    int Padding,
    int MinimumTargetSize,
    int LabelTopOffset,
    int NameTopOffset,
    int MarkerTopOffset,
    int SwatchWidth,
    int SwatchHeight,
    int SwatchGap);

internal sealed record UiArmyCompositionLayout(
    int PanelWidth,
    int PanelHeight,
    int RowHeight,
    int RowGap,
    int StepperWidth,
    int ArrowWidth);

/// <summary>
/// The content-pipeline asset id assigned to each <see cref="UiFontRole"/>,
/// as read from the theme catalog's <c>shared.fonts</c> map.
/// </summary>
internal sealed record UiFontAssignments(
    string Caption,
    string Body,
    string Label,
    string Subtitle,
    string Title,
    string Display);

/// <summary>
/// The resolved <see cref="UiFontRole"/> for each of the eight theme text
/// slots, as read from the theme catalog's <c>shared.textRoles</c> map.
/// </summary>
internal sealed record UiTextRoles(
    UiFontRole MenuTitle,
    UiFontRole MenuSubtitle,
    UiFontRole MenuButton,
    UiFontRole MenuHelper,
    UiFontRole SelectorArrow,
    UiFontRole SelectorLabel,
    UiFontRole SelectorName,
    UiFontRole SelectorMarker);

internal enum UiVisualInteractionState
{
    Default,
    Hover,
    Focus,
    Pressed,
    Active,
    Disabled,
}
