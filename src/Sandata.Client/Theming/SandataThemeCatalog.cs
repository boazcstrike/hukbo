using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Sandata.Client.Theming;

/// <summary>
/// Loads and validates the Sandata theme catalog document. The validation
/// technique mirrors <c>Hukbo.Client.Theming.UiThemeCatalog</c> exactly: the
/// set of required color roles is derived by reflection from the typed
/// record (<see cref="RuntimeColorRoles"/>) rather than hand-copied into the
/// JSON, so the JSON's declared <c>requiredColorRoles</c> list is itself
/// checked against the runtime contract, and every shipped theme's
/// <c>colors</c> map is checked against that same list for both a missing
/// role and an unrecognized one. Kept as a single file: Sandata's catalog
/// has no font, menu, selector, or army-composition layout concerns to
/// justify Hukbo's four-way split.
/// </summary>
internal sealed class SandataThemeCatalog
{
    public const int SupportedSchemaVersion = 1;

    private static readonly string[] RuntimeColorRoles =
        typeof(SandataThemeColors)
            .GetProperties()
            .Select(property =>
                char.ToLowerInvariant(property.Name[0]) +
                property.Name[1..])
            .ToArray();

    private readonly Dictionary<string, SandataTheme> _themesById;

    private SandataThemeCatalog(
        string defaultThemeId,
        IReadOnlyList<SandataTheme> themes)
    {
        DefaultThemeId = defaultThemeId;
        Themes = themes;
        _themesById = themes.ToDictionary(
            theme => theme.Id,
            StringComparer.Ordinal);
    }

    public string DefaultThemeId { get; }

    public IReadOnlyList<SandataTheme> Themes { get; }

    public static SandataThemeCatalog Load(string path) =>
        LoadFromJson(File.ReadAllText(path));

    internal static SandataThemeCatalog LoadFromJson(string json)
    {
        var document = JsonSerializer.Deserialize<CatalogDocument>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException(
                "Sandata theme catalog is empty.");

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
        var catalog = new SandataThemeCatalog(
            document.DefaultThemeId!,
            themes);

        ValidateFactionColorsAreThemeIndependent(themes, errors);
        ValidateContrast(document, catalog, errors);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                errors));
        }

        return catalog;
    }

    public SandataTheme GetRequired(string id) =>
        _themesById.TryGetValue(id, out var theme)
            ? theme
            : throw new KeyNotFoundException($"Unknown Sandata theme '{id}'.");

    public bool TryGet(string? id, out SandataTheme theme)
    {
        if (id is not null && _themesById.TryGetValue(id, out theme!))
        {
            return true;
        }

        theme = null!;
        return false;
    }

    /// <summary>
    /// The contrast pairs every shipped theme must clear, named as
    /// (foreground role, background role). Mirrors the intent of
    /// <c>UiThemeCatalog.GetRequiredRenderedContrastPairs</c>: a fixed,
    /// code-owned list rather than something a theme author could silently
    /// drop by editing JSON, covering text legibility, panel structure, and
    /// the tactical overlay roles that have no Hukbo equivalent.
    /// </summary>
    /// <remarks>
    /// <see cref="SandataThemeColors.Friendly"/>,
    /// <see cref="SandataThemeColors.Hostile"/>, and
    /// <see cref="SandataThemeColors.UnknownContact"/> are deliberately
    /// absent from this list. They are fixed constants
    /// (<see cref="ValidateFactionColorsAreThemeIndependent"/>) rather than
    /// per-theme roles a theme author can retune, so a panel background dark
    /// enough for one theme and light enough for another can never both be
    /// satisfied by the same fixed foreground — Hukbo does not contrast-test
    /// its equivalent fixed pawn palette either, for the same reason.
    /// <see cref="SandataThemeColors.SelectedTrooper"/> stays in the list: it
    /// is the one repurposed role a theme author is still free to tune.
    /// </remarks>
    private static IReadOnlyList<(string Foreground, string Background)>
        GetRequiredContrastPairs() =>
    [
        ("textPrimary", "panelSurface"),
        ("textSecondary", "panelSurface"),
        ("textDisabled", "panelSurface"),
        ("textInverse", "actionDefault"),
        ("actionFocus", "panelSurface"),
        ("panelBorder", "panelSurface"),
        ("arenaBorder", "arenaSurface"),
        ("statusInfo", "panelSurface"),
        ("statusSuccess", "panelSurface"),
        ("statusWarning", "panelSurface"),
        ("statusDanger", "panelSurface"),
        ("newEvent", "panelAlternate"),
        ("selectedTrooper", "panelAlternate"),
        ("suppressed", "arenaSurface"),
        ("downed", "arenaSurface"),
        ("orderPath", "arenaSurface"),
        ("waypoint", "arenaSurface"),
        ("coverGood", "arenaSurface"),
        ("coverNone", "arenaSurface"),
        ("breachPoint", "arenaSurface"),
        ("fireConeEdge", "arenaSurface"),

        // The weapon role is checked against three backgrounds rather than
        // one. It is drawn on top of an operator, so it has to separate from
        // both faction colours, and it also overhangs the operator onto bare
        // ground, so it has to separate from the arena as well. The two
        // faction ratios are deliberately low: this is a silhouette read at a
        // dozen pixels, not text, and demanding a text-grade ratio against a
        // saturated blue and a saturated red at once leaves only black or
        // white.
        ("weapon", "arenaSurface"),
        ("weapon", "friendly"),
        ("weapon", "hostile"),

        ("alertCalm", "statusSurface"),
        ("alertRaised", "statusSurface"),
        ("alertBreach", "statusSurface"),
    ];

    private static List<string> ValidateDocument(CatalogDocument document)
    {
        var errors = new List<string>();
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add(
                $"Unsupported Sandata theme catalog schema " +
                $"{document.SchemaVersion}.");
        }

        if (document.Standards is null)
        {
            errors.Add("Sandata theme catalog requires typed standards.");
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
                "Sandata theme catalog count does not match " +
                "requiredThemeCount.");
            return errors;
        }

        var ids = document.Themes
            .Select(theme => theme.Id)
            .Where(id => id is not null)
            .ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            errors.Add("Sandata theme IDs must be unique.");
        }

        if (!document.Standards.RequiredThemeIds!.Order()
            .SequenceEqual(ids.Order(StringComparer.Ordinal)))
        {
            errors.Add(
                "Sandata theme catalog contains missing or unknown theme " +
                "IDs.");
        }

        if (document.DefaultThemeId is null ||
            !ids.Contains(document.DefaultThemeId, StringComparer.Ordinal))
        {
            errors.Add("Default Sandata theme ID must identify a catalog theme.");
        }

        foreach (var theme in document.Themes)
        {
            ValidateThemeDocument(theme, document.Standards, errors);
        }

        if (document.ContrastPairs is null ||
            document.ContrastPairs.Count == 0)
        {
            errors.Add("Sandata theme catalog requires contrast standards.");
        }
        else
        {
            ValidateContrastPairContract(document, errors);
        }

        return errors;
    }

    private static void ValidateThemeDocument(
        ThemeDocument theme,
        StandardsDocument standards,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(theme.DisplayName))
        {
            errors.Add($"Theme '{theme.Id}' requires a display name.");
        }

        if (theme.Colors is null)
        {
            errors.Add($"Theme '{theme.Id}' requires semantic colors.");
            return;
        }

        // Missing role: required by the typed contract but absent from this
        // theme's JSON.
        foreach (var role in standards.RequiredColorRoles!)
        {
            if (!theme.Colors.TryGetValue(role, out var value))
            {
                errors.Add($"Theme '{theme.Id}' is missing color '{role}'.");
                continue;
            }

            if (!TryParseColor(value, out _))
            {
                errors.Add($"Theme '{theme.Id}' color '{role}' is invalid.");
            }
        }

        // Unknown role: present in this theme's JSON but not part of the
        // typed 39-role contract.
        var unknownRoles = theme.Colors.Keys
            .Except(standards.RequiredColorRoles!, StringComparer.Ordinal);
        foreach (var role in unknownRoles)
        {
            errors.Add($"Theme '{theme.Id}' has unknown color '{role}'.");
        }

        if (theme.Metrics is null)
        {
            errors.Add($"Theme '{theme.Id}' requires visual metrics.");
        }
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
                "Standards require a unique theme ID for every required " +
                "theme.");
        }

        if (standards.RequiredColorRoles is null ||
            !RuntimeColorRoles.Order(StringComparer.Ordinal).SequenceEqual(
                standards.RequiredColorRoles.Order(StringComparer.Ordinal)))
        {
            errors.Add(
                "Standards color roles must match the typed 39-role " +
                "runtime contract.");
        }

        if (standards.MetricRanges is null)
        {
            errors.Add("Standards require metric ranges.");
            return;
        }

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

        foreach (var required in GetRequiredContrastPairs())
        {
            if (!keys.Contains($"{required.Foreground}\0{required.Background}"))
            {
                errors.Add(
                    $"Missing required contrast pair {required.Foreground}/" +
                    $"{required.Background}.");
            }
        }
    }

    /// <summary>
    /// Enforces that <see cref="SandataThemeColors.Friendly"/>,
    /// <see cref="SandataThemeColors.Hostile"/>, and
    /// <see cref="SandataThemeColors.UnknownContact"/> are identical in
    /// every shipped theme and equal to <see cref="SandataFactionPalette"/>.
    /// A theme is never allowed to make friend and foe harder to tell apart.
    /// </summary>
    private static void ValidateFactionColorsAreThemeIndependent(
        IReadOnlyList<SandataTheme> themes,
        List<string> errors)
    {
        foreach (var theme in themes)
        {
            if (theme.Colors.Friendly != SandataFactionPalette.Friendly)
            {
                errors.Add(
                    $"Theme '{theme.Id}' friendly color must match the " +
                    "theme-independent constant.");
            }

            if (theme.Colors.Hostile != SandataFactionPalette.Hostile)
            {
                errors.Add(
                    $"Theme '{theme.Id}' hostile color must match the " +
                    "theme-independent constant.");
            }

            if (theme.Colors.UnknownContact !=
                SandataFactionPalette.UnknownContact)
            {
                errors.Add(
                    $"Theme '{theme.Id}' unknown-contact color must match " +
                    "the theme-independent constant.");
            }
        }
    }

    private static SandataTheme CreateTheme(ThemeDocument theme)
    {
        var colors = theme.Colors!;
        Color Read(string role) => ParseColor(colors[role]);

        return new SandataTheme(
            theme.Id!,
            theme.DisplayName!,
            new SandataThemeColors(
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
                Read("newEvent"),
                Read("friendly"),
                Read("hostile"),
                Read("unknownContact"),
                Read("selectedTrooper"),
                Read("suppressed"),
                Read("downed"),
                Read("orderPath"),
                Read("waypoint"),
                Read("coverGood"),
                Read("coverNone"),
                Read("breachPoint"),
                Read("fireConeFill"),
                Read("fireConeEdge"),
                Read("weapon"),
                Read("alertCalm"),
                Read("alertRaised"),
                Read("alertBreach")),
            new SandataThemeMetrics(
                theme.Metrics!.BorderThickness,
                theme.Metrics.FocusThickness,
                theme.Metrics.ShadowOffset));
    }

    private static void ValidateContrast(
        CatalogDocument document,
        SandataThemeCatalog catalog,
        List<string> errors)
    {
        foreach (var themeDocument in document.Themes!)
        {
            var theme = catalog.GetRequired(themeDocument.Id!);
            foreach (var pair in document.ContrastPairs!)
            {
                if (!RuntimeColorRoles.Contains(
                    pair.Foreground,
                    StringComparer.Ordinal) ||
                    !RuntimeColorRoles.Contains(
                        pair.Background,
                        StringComparer.Ordinal))
                {
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
    }

    private static Color GetColor(SandataThemeColors colors, string role) =>
        role switch
        {
            "canvasBackground" => colors.CanvasBackground,
            "arenaSurface" => colors.ArenaSurface,
            "arenaBorder" => colors.ArenaBorder,
            "statusSurface" => colors.StatusSurface,
            "overlayScrim" => colors.OverlayScrim,
            "panelSurface" => colors.PanelSurface,
            "panelAlternate" => colors.PanelAlternate,
            "panelBorder" => colors.PanelBorder,
            "textPrimary" => colors.TextPrimary,
            "textSecondary" => colors.TextSecondary,
            "textDisabled" => colors.TextDisabled,
            "textInverse" => colors.TextInverse,
            "actionDefault" => colors.ActionDefault,
            "actionHover" => colors.ActionHover,
            "actionFocus" => colors.ActionFocus,
            "actionPressed" => colors.ActionPressed,
            "actionActive" => colors.ActionActive,
            "actionDisabled" => colors.ActionDisabled,
            "statusInfo" => colors.StatusInfo,
            "statusSuccess" => colors.StatusSuccess,
            "statusWarning" => colors.StatusWarning,
            "statusDanger" => colors.StatusDanger,
            "newEvent" => colors.NewEvent,
            "friendly" => colors.Friendly,
            "hostile" => colors.Hostile,
            "unknownContact" => colors.UnknownContact,
            "selectedTrooper" => colors.SelectedTrooper,
            "suppressed" => colors.Suppressed,
            "downed" => colors.Downed,
            "orderPath" => colors.OrderPath,
            "waypoint" => colors.Waypoint,
            "coverGood" => colors.CoverGood,
            "coverNone" => colors.CoverNone,
            "breachPoint" => colors.BreachPoint,
            "fireConeFill" => colors.FireConeFill,
            "fireConeEdge" => colors.FireConeEdge,
            "weapon" => colors.Weapon,
            "alertCalm" => colors.AlertCalm,
            "alertRaised" => colors.AlertRaised,
            "alertBreach" => colors.AlertBreach,
            _ => throw new InvalidDataException($"Unknown color role '{role}'."),
        };

    private static Color ParseColor(string value) =>
        TryParseColor(value, out var color)
            ? color
            : throw new InvalidDataException($"Invalid color '{value}'.");

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (value is null ||
            value.Length is not (7 or 9) ||
            value[0] != '#')
        {
            return false;
        }

        if (!uint.TryParse(
            value.AsSpan(1),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out var rgba))
        {
            return false;
        }

        if (value.Length == 7)
        {
            rgba = (rgba << 8) | 0xFF;
        }

        color = new Color(
            (byte)(rgba >> 24),
            (byte)(rgba >> 16),
            (byte)(rgba >> 8),
            (byte)rgba);
        return true;
    }

    private static Color Composite(Color foreground, Color background)
    {
        var alpha = foreground.A / 255d;
        var backgroundAlpha = background.A / 255d;
        var outputAlpha = alpha + (backgroundAlpha * (1d - alpha));
        if (outputAlpha <= 0d)
        {
            return Color.Transparent;
        }

        byte Blend(byte foregroundChannel, byte backgroundChannel) =>
            (byte)Math.Round(
                ((foregroundChannel * alpha) +
                 (backgroundChannel * backgroundAlpha * (1d - alpha))) /
                outputAlpha);

        return new Color(
            Blend(foreground.R, background.R),
            Blend(foreground.G, background.G),
            Blend(foreground.B, background.B),
            (byte)Math.Round(outputAlpha * 255d));
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(GetLuminance(first), GetLuminance(second));
        var darker = Math.Min(GetLuminance(first), GetLuminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double GetLuminance(Color color)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045d
                ? value / 12.92d
                : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Linearize(color.R)) +
            (0.7152d * Linearize(color.G)) +
            (0.0722d * Linearize(color.B));
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

        if (range.Minimum > range.Maximum ||
            range.Minimum < (allowZero ? 0d : double.Epsilon))
        {
            errors.Add($"Metric range '{name}' is invalid.");
        }
    }

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

        public MetricRangesDocument? MetricRanges { get; init; }
    }

    private sealed class MetricRangesDocument
    {
        public NumberRangeDocument? BorderThickness { get; init; }

        public NumberRangeDocument? FocusThickness { get; init; }

        public NumberRangeDocument? ShadowOffset { get; init; }
    }

    private sealed class NumberRangeDocument
    {
        public double Minimum { get; init; }

        public double Maximum { get; init; }
    }
}
