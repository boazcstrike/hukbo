using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Theming;

internal sealed partial class UiThemeCatalog
{
    public const int SupportedSchemaVersion = 1;

    private static readonly string[] RuntimeColorRoles =
        typeof(UiThemeColors)
            .GetProperties()
            .Select(property =>
                char.ToLowerInvariant(property.Name[0]) +
                property.Name[1..])
            .ToArray();

    private static readonly string[] RuntimeInteractionStates =
        Enum.GetNames<UiVisualInteractionState>()
            .Select(name =>
                char.ToLowerInvariant(name[0]) + name[1..])
            .ToArray();

    private readonly Dictionary<string, UiTheme> _themesById;

    private UiThemeCatalog(
        string defaultThemeId,
        IReadOnlyList<UiTheme> themes,
        UiThemeStandards standards)
    {
        DefaultThemeId = defaultThemeId;
        Themes = themes;
        Standards = standards;
        _themesById = themes.ToDictionary(
            theme => theme.Id,
            StringComparer.Ordinal);
    }

    public string DefaultThemeId { get; }

    public IReadOnlyList<UiTheme> Themes { get; }

    public UiThemeStandards Standards { get; }

    public static UiThemeCatalog Load(string path) =>
        LoadFromJson(File.ReadAllText(path));

    public static UiThemeCatalog LoadOrFallback(string path)
    {
        try
        {
            return Load(path);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException)
        {
            return CreateFallback();
        }
    }

    internal static UiThemeCatalog LoadFromJson(string json)
    {
        var document = JsonSerializer.Deserialize<CatalogDocument>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException("Theme catalog is empty.");

        var errors = ValidateDocument(document);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                errors));
        }

        var themes = document.Themes!
            .Select(CreateTheme)
            .ToArray();
        var standards = CreateStandards(document.Standards!);
        var catalog = new UiThemeCatalog(
            document.DefaultThemeId!,
            themes,
            standards);

        var themeErrors = themes
            .SelectMany(catalog.ValidateTheme)
            .ToArray();
        if (themeErrors.Length > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                themeErrors));
        }

        ValidateContrast(document, catalog);
        return catalog;
    }

    public UiTheme GetRequired(string id) =>
        _themesById.TryGetValue(id, out var theme)
            ? theme
            : throw new KeyNotFoundException($"Unknown UI theme '{id}'.");

    public bool TryGet(string? id, out UiTheme theme)
    {
        if (id is not null && _themesById.TryGetValue(id, out theme!))
        {
            return true;
        }

        theme = null!;
        return false;
    }

    internal IReadOnlyList<string> ValidateTheme(UiTheme theme)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(theme.Id))
        {
            errors.Add("Theme ID is required.");
        }

        if (string.IsNullOrWhiteSpace(theme.DisplayName))
        {
            errors.Add($"Theme '{theme.Id}' requires a display name.");
        }

        if (!IsWithin(
            theme.Metrics.BorderThickness,
            Standards.MetricRanges.BorderThickness))
        {
            errors.Add(
                $"Theme '{theme.Id}' border thickness is outside standards.");
        }

        if (!IsWithin(
            theme.Metrics.FocusThickness,
            Standards.MetricRanges.FocusThickness))
        {
            errors.Add(
                $"Theme '{theme.Id}' focus thickness is outside standards.");
        }

        if (!IsWithin(
            theme.Metrics.ShadowOffset,
            Standards.MetricRanges.ShadowOffset))
        {
            errors.Add(
                $"Theme '{theme.Id}' shadow offset is outside standards.");
        }

        return errors;
    }

    private static List<string> ValidateDocument(CatalogDocument document)
    {
        var errors = new List<string>();
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add(
                $"Unsupported theme catalog schema {document.SchemaVersion}.");
        }

        if (document.Standards is null)
        {
            errors.Add("Theme catalog requires typed standards.");
            return errors;
        }

        ValidateStandards(document.Standards, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        if (document.Themes is null ||
            document.Themes.Count != document.Standards.RequiredThemeCount)
        {
            errors.Add(
                "Theme catalog count does not match requiredThemeCount.");
            return errors;
        }

        var ids = document.Themes
            .Select(theme => theme.Id)
            .Where(id => id is not null)
            .ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            errors.Add("Theme IDs must be unique.");
        }

        if (!document.Standards.RequiredThemeIds!.Order()
            .SequenceEqual(ids.Order(StringComparer.Ordinal)))
        {
            errors.Add("Theme catalog contains missing or unknown theme IDs.");
        }

        if (document.DefaultThemeId is null ||
            !ids.Contains(document.DefaultThemeId, StringComparer.Ordinal))
        {
            errors.Add("Default theme ID must identify a catalog theme.");
        }

        foreach (var theme in document.Themes)
        {
            if (string.IsNullOrWhiteSpace(theme.DisplayName))
            {
                errors.Add($"Theme '{theme.Id}' requires a display name.");
            }

            if (theme.Colors is null)
            {
                errors.Add($"Theme '{theme.Id}' requires semantic colors.");
                continue;
            }

            foreach (var role in document.Standards.RequiredColorRoles!)
            {
                if (!theme.Colors.TryGetValue(role, out var value))
                {
                    errors.Add($"Theme '{theme.Id}' is missing color '{role}'.");
                    continue;
                }

                if (!TryParseColor(value, out _))
                {
                    errors.Add(
                        $"Theme '{theme.Id}' color '{role}' is invalid.");
                }
            }

            var unknownRoles = theme.Colors.Keys
                .Except(
                    document.Standards.RequiredColorRoles!,
                    StringComparer.Ordinal);
            foreach (var role in unknownRoles)
            {
                errors.Add($"Theme '{theme.Id}' has unknown color '{role}'.");
            }

            if (theme.Metrics is null)
            {
                errors.Add($"Theme '{theme.Id}' requires visual metrics.");
            }
        }

        if (document.ContrastPairs is null ||
            document.ContrastPairs.Count == 0)
        {
            errors.Add("Theme catalog requires contrast standards.");
        }
        else
        {
            ValidateContrastPairContract(document, errors);
        }

        return errors;
    }

    private static void ValidateStandards(
        StandardsDocument standards,
        List<string> errors)
    {
        if (standards.RequiredThemeCount <= 0 ||
            standards.RequiredThemeIds is null ||
            standards.RequiredThemeIds.Count !=
            standards.RequiredThemeCount ||
            standards.RequiredThemeIds.Any(string.IsNullOrWhiteSpace) ||
            standards.RequiredThemeIds.Distinct(StringComparer.Ordinal)
                .Count() != standards.RequiredThemeIds.Count)
        {
            errors.Add(
                "Standards require a unique theme ID for every required theme.");
        }

        if (standards.RequiredColorRoles is null ||
            !RuntimeColorRoles.Order(StringComparer.Ordinal).SequenceEqual(
                standards.RequiredColorRoles.Order(StringComparer.Ordinal)))
        {
            errors.Add(
                "Standards color roles must match the typed runtime contract.");
        }

        if (standards.RequiredInteractionStates is null ||
            !RuntimeInteractionStates.Order(StringComparer.Ordinal)
                .SequenceEqual(
                    standards.RequiredInteractionStates.Order(
                        StringComparer.Ordinal)))
        {
            errors.Add(
                "Standards interaction states must match the runtime states.");
        }

        if (standards.MetricRanges is null)
        {
            errors.Add("Standards require metric ranges.");
        }
        else
        {
            ValidateIntegerRange(
                standards.MetricRanges.BorderThickness,
                "borderThickness",
                errors);
            ValidateIntegerRange(
                standards.MetricRanges.FocusThickness,
                "focusThickness",
                errors);
            ValidateIntegerRange(
                standards.MetricRanges.ShadowOffset,
                "shadowOffset",
                errors,
                allowZero: true);
            ValidateIntegerRange(
                standards.MetricRanges.TargetSize,
                "targetSize",
                errors);
            ValidateRange(
                standards.MetricRanges.TextScale,
                "textScale",
                errors);
        }

        if (standards.AllowedFontAssetIds is null ||
            standards.AllowedFontAssetIds.Count == 0 ||
            standards.AllowedFontAssetIds.Any(string.IsNullOrWhiteSpace) ||
            standards.AllowedFontAssetIds.Distinct(StringComparer.Ordinal)
                .Count() != standards.AllowedFontAssetIds.Count)
        {
            errors.Add("Standards require unique allowed font asset IDs.");
        }

        if (standards.Shared is null)
        {
            errors.Add("Standards require shared UI configuration.");
            return;
        }

        if (standards.AllowedFontAssetIds is not null &&
            !standards.AllowedFontAssetIds.Contains(
                standards.Shared.FontAssetId,
                StringComparer.Ordinal))
        {
            errors.Add("Shared font asset ID is not allowed.");
        }

        ValidateSharedStandards(standards, errors);
    }

    private static void ValidateSharedStandards(
        StandardsDocument standards,
        List<string> errors)
    {
        var ranges = standards.MetricRanges;
        var shared = standards.Shared;
        if (ranges is null || shared is null)
        {
            return;
        }

        if (shared.Menu is null ||
            shared.Selector is null ||
            shared.TextScales is null)
        {
            errors.Add(
                "Shared standards require menu, selector, and text scales.");
            return;
        }

        if (shared.Menu.PanelWidth <= 0 ||
            shared.Menu.PanelHeight <= 0 ||
            shared.Menu.ButtonWidth <= 0 ||
            shared.Menu.ButtonGap < 0 ||
            !IsWithin(shared.Menu.ButtonHeight, ranges.TargetSize))
        {
            errors.Add("Shared menu layout is outside metric standards.");
        }

        if (shared.Selector.Height <= 0 ||
            shared.Selector.Padding < 0 ||
            !IsWithin(shared.Selector.ArrowWidth, ranges.TargetSize) ||
            !IsWithin(
                shared.Selector.MinimumTargetSize,
                ranges.TargetSize) ||
            shared.Selector.ArrowWidth <
            shared.Selector.MinimumTargetSize ||
            shared.Menu.ButtonHeight <
            shared.Selector.MinimumTargetSize ||
            shared.Selector.SwatchWidth <= 0 ||
            shared.Selector.SwatchHeight <= 0 ||
            shared.Selector.SwatchGap < 0)
        {
            errors.Add("Shared selector layout is outside metric standards.");
        }

        foreach (var scale in new[]
                 {
                     shared.TextScales.MenuTitle,
                     shared.TextScales.MenuSubtitle,
                     shared.TextScales.MenuButton,
                     shared.TextScales.MenuHelper,
                     shared.TextScales.SelectorArrow,
                     shared.TextScales.SelectorLabel,
                     shared.TextScales.SelectorName,
                     shared.TextScales.SelectorMarker,
                 })
        {
            if (!IsWithin(scale, ranges.TextScale))
            {
                errors.Add("Shared text scale is outside metric standards.");
                break;
            }
        }
    }

    private static void ValidateContrastPairContract(
        CatalogDocument document,
        List<string> errors)
    {
        var roles = document.Standards!.RequiredColorRoles!;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in document.ContrastPairs!)
        {
            if (pair.Foreground is null ||
                pair.Background is null ||
                !roles.Contains(pair.Foreground, StringComparer.Ordinal) ||
                !roles.Contains(pair.Background, StringComparer.Ordinal))
            {
                errors.Add("Contrast pair references an unknown color.");
                continue;
            }

            if (pair.MinimumRatio is <= 1d or > 21d)
            {
                errors.Add("Contrast pair minimum ratio is invalid.");
            }

            if (!keys.Add($"{pair.Foreground}\0{pair.Background}"))
            {
                errors.Add(
                    $"Duplicate contrast pair {pair.Foreground}/" +
                    $"{pair.Background}.");
            }
        }

        foreach (var required in GetRequiredRenderedContrastPairs(
                     document.Standards!))
        {
            if (!keys.Contains($"{required.Foreground}\0{required.Background}"))
            {
                errors.Add(
                    $"Missing rendered contrast pair {required.Foreground}/" +
                    $"{required.Background}.");
            }
        }
    }

    private static UiTheme CreateTheme(ThemeDocument theme)
    {
        var colors = theme.Colors!;
        Color Read(string role) => ParseColor(colors[role]);

        return new UiTheme(
            theme.Id!,
            theme.DisplayName!,
            new UiThemeColors(
                Read("canvasBackground"),
                Read("arenaSurface"),
                Read("arenaBorder"),
                Read("statusSurface"),
                Read("overlayScrim"),
                Read("panelSurface"),
                Read("panelAlternate"),
                Read("panelBorder"),
                Read("textPrimary"),
                Read("textSecondary"),
                Read("textDisabled"),
                Read("textInverse"),
                Read("actionDefault"),
                Read("actionHover"),
                Read("actionFocus"),
                Read("actionPressed"),
                Read("actionActive"),
                Read("actionDisabled"),
                Read("statusInfo"),
                Read("statusSuccess"),
                Read("statusWarning"),
                Read("statusDanger"),
                Read("teamA"),
                Read("teamB"),
                Read("otherFaction"),
                Read("selection"),
                Read("newEvent")),
            new UiThemeMetrics(
                theme.Metrics!.BorderThickness,
                theme.Metrics.FocusThickness,
                theme.Metrics.ShadowOffset));
    }

    private static UiThemeStandards CreateStandards(
        StandardsDocument standards) =>
        new(
            standards.RequiredThemeCount,
            standards.RequiredThemeIds!,
            standards.RequiredColorRoles!,
            standards.RequiredInteractionStates!,
            new UiMetricRanges(
                ToIntegerRange(standards.MetricRanges!.BorderThickness!),
                ToIntegerRange(standards.MetricRanges.FocusThickness!),
                ToIntegerRange(standards.MetricRanges.ShadowOffset!),
                ToIntegerRange(standards.MetricRanges.TargetSize!),
                new UiNumberRange(
                    standards.MetricRanges.TextScale!.Minimum,
                    standards.MetricRanges.TextScale.Maximum)),
            standards.AllowedFontAssetIds!,
            new UiSharedStandards(
                standards.Shared!.FontAssetId!,
                new UiMenuLayout(
                    standards.Shared.Menu!.PanelWidth,
                    standards.Shared.Menu.PanelHeight,
                    standards.Shared.Menu.ButtonWidth,
                    standards.Shared.Menu.ButtonHeight,
                    standards.Shared.Menu.ButtonGap,
                    standards.Shared.Menu.TitleTopOffset,
                    standards.Shared.Menu.SubtitleTopOffset,
                    standards.Shared.Menu.SelectorTopOffset,
                    standards.Shared.Menu.SelectorGap,
                    standards.Shared.Menu.HelperBottomOffset),
                new UiThemeSelectorLayout(
                    standards.Shared.Selector!.Height,
                    standards.Shared.Selector.ArrowWidth,
                    standards.Shared.Selector.Padding,
                    standards.Shared.Selector.MinimumTargetSize,
                    standards.Shared.Selector.LabelTopOffset,
                    standards.Shared.Selector.NameTopOffset,
                    standards.Shared.Selector.MarkerTopOffset,
                    standards.Shared.Selector.SwatchWidth,
                    standards.Shared.Selector.SwatchHeight,
                    standards.Shared.Selector.SwatchGap),
                new UiTextScales(
                    standards.Shared.TextScales!.MenuTitle,
                    standards.Shared.TextScales.MenuSubtitle,
                    standards.Shared.TextScales.MenuButton,
                    standards.Shared.TextScales.MenuHelper,
                    standards.Shared.TextScales.SelectorArrow,
                    standards.Shared.TextScales.SelectorLabel,
                    standards.Shared.TextScales.SelectorName,
                    standards.Shared.TextScales.SelectorMarker)));

    private static void ValidateContrast(
        CatalogDocument document,
        UiThemeCatalog catalog)
    {
        var errors = new List<string>();
        foreach (var themeDocument in document.Themes!)
        {
            var theme = catalog.GetRequired(themeDocument.Id!);
            foreach (var pair in document.ContrastPairs!)
            {
                if (!catalog.Standards.RequiredColorRoles.Contains(
                    pair.Foreground,
                    StringComparer.Ordinal) ||
                    !catalog.Standards.RequiredColorRoles.Contains(
                        pair.Background,
                        StringComparer.Ordinal))
                {
                    errors.Add("Contrast pair references an unknown color.");
                    continue;
                }

                var foreground = GetColor(theme.Colors, pair.Foreground!);
                var background = GetColor(theme.Colors, pair.Background!);
                var canvas = theme.Colors.CanvasBackground;
                var ratio = GetContrastRatio(
                    Composite(foreground, Composite(background, canvas)),
                    Composite(background, canvas));
                if (ratio + 0.001 < pair.MinimumRatio)
                {
                    errors.Add(
                        $"Theme '{theme.Id}' contrast {pair.Foreground}/" +
                        $"{pair.Background} is {ratio:0.00}, below " +
                        $"{pair.MinimumRatio:0.0}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                errors));
        }
    }

    private static IReadOnlyList<(string Foreground, string Background)>
        GetRequiredRenderedContrastPairs(StandardsDocument standards)
    {
        var pairs = new List<(string Foreground, string Background)>
        {
            ("textPrimary", "panelSurface"),
            ("textSecondary", "panelSurface"),
            ("textPrimary", "statusSurface"),
            ("actionFocus", "panelSurface"),
            ("panelBorder", "panelSurface"),
            ("arenaBorder", "arenaSurface"),
            ("textPrimary", "panelAlternate"),
            ("textSecondary", "panelAlternate"),
            ("textDisabled", "panelAlternate"),
            ("selection", "panelAlternate"),
            ("textInverse", "selection"),
            ("teamA", "panelAlternate"),
            ("teamB", "panelAlternate"),
            ("otherFaction", "panelAlternate"),
            ("statusSuccess", "panelAlternate"),
            ("newEvent", "panelAlternate"),
            ("textDisabled", "panelSurface"),
            ("statusInfo", "panelSurface"),
            ("statusWarning", "panelSurface"),
            ("statusDanger", "panelSurface"),
            ("otherFaction", "panelSurface"),
            ("statusSuccess", "panelSurface"),
        };

        foreach (var state in standards.RequiredInteractionStates!)
        {
            switch (state)
            {
                case "default":
                case "hover":
                case "pressed":
                case "active":
                    pairs.Add((
                        "textInverse",
                        $"action{char.ToUpperInvariant(state[0])}" +
                        state[1..]));
                    break;
                case "disabled":
                    pairs.Add(("textDisabled", "actionDisabled"));
                    break;
            }
        }

        return pairs;
    }

    private static UiIntegerRange ToIntegerRange(
        NumberRangeDocument range) =>
        new((int)range.Minimum, (int)range.Maximum);

    private static bool IsWithin(int value, UiIntegerRange range) =>
        value >= range.Minimum && value <= range.Maximum;

    private static bool IsWithin(
        double value,
        UiNumberRange range) =>
        value >= range.Minimum && value <= range.Maximum;

    private static bool IsWithin(
        int value,
        NumberRangeDocument? range) =>
        range is not null &&
        value >= range.Minimum &&
        value <= range.Maximum;

    private static bool IsWithin(
        double value,
        NumberRangeDocument? range) =>
        range is not null &&
        value >= range.Minimum &&
        value <= range.Maximum;

    private static void ValidateRange(
        NumberRangeDocument? range,
        string name,
        List<string> errors,
        bool allowZero = false)
    {
        if (range is null ||
            range.Minimum > range.Maximum ||
            range.Minimum < (allowZero ? 0d : double.Epsilon))
        {
            errors.Add($"Metric range '{name}' is invalid.");
        }
    }

    private static void ValidateIntegerRange(
        NumberRangeDocument? range,
        string name,
        List<string> errors,
        bool allowZero = false)
    {
        if (range is null ||
            range.Minimum != Math.Truncate(range.Minimum) ||
            range.Maximum != Math.Truncate(range.Maximum))
        {
            errors.Add($"Metric range '{name}' must use integer bounds.");
            return;
        }

        ValidateRange(range, name, errors, allowZero);
    }
}
