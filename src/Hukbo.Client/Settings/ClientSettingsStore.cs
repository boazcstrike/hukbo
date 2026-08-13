using System.Text.Json;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Settings;

internal sealed class ClientSettingsStore
{
    /// <summary>
    /// Raised from 2 to 3 by combat preset V2, which turned four weapon
    /// categories into six roster-entry categories and renamed all of them.
    /// A version 2 file is discarded rather than migrated — a deliberate
    /// reset recorded in <see cref="ArmyComposition"/>, not an oversight.
    /// Raised again from 3 to 4 by the <see cref="MotionIntensity"/> setting.
    /// Unlike the 2-to-3 bump, this one is backward compatible: a version 3
    /// file loads cleanly through <see cref="AcceptedSchemaVersions"/> with
    /// the new field defaulting, because the shape did not change — only a
    /// field was added. This is the version <see cref="TrySave"/> always
    /// writes.
    /// Raised again from 4 to 5 by the <see cref="AutoCameraMode"/> setting,
    /// backward compatible on the same terms as the 3-to-4 bump.
    /// Raised again from 5 to 6 by the 500-unit default composition. This one
    /// behaves like the 2-to-3 bump rather than the field-adding bumps: the
    /// shape is unchanged and an older file would still parse, but a saved
    /// composition always overrides <see cref="ArmyComposition.Default"/>, so
    /// accepting the old file would silently keep the old army size. A second
    /// deliberate reset, recorded on <see cref="ArmyComposition"/>.
    /// Raised again from 6 to 7 when the shipped default combat preset moved
    /// to V4 and the composition's six roster-entry categories became four
    /// rank categories. This behaves like the 2-to-3 and 5-to-6 bumps rather
    /// than a field-adding bump: the shape changed and every field was
    /// renamed, so an older file cannot be read forward under any
    /// interpretation. A third deliberate reset, recorded on
    /// <see cref="ArmyComposition"/>.
    /// Raised again from 7 to 8 by the <see cref="UiScale"/> and
    /// <see cref="StartupDisplayMode"/> settings. This is backward compatible:
    /// a version 7 file loads through <see cref="AcceptedSchemaVersions"/>
    /// with only those absent fields defaulting.
    /// </summary>
    public const int SupportedSchemaVersion = 8;

    /// <summary>
    /// Schema versions <see cref="Load"/> accepts without discarding the
    /// whole file. Version 7 and the current version qualify because the
    /// 7-to-8 change only adds independently defaulted fields. Versions before
    /// 7 remain incompatible because of the deliberate composition resets
    /// recorded on <see cref="ArmyComposition"/>.
    /// </summary>
    private static readonly int[] AcceptedSchemaVersions =
        [7, SupportedSchemaVersion];

    // Moved from Stylized to Full on 2026-08-13
    // (docs/plans/2026-08-13-lethal-blow-legibility-design.md) on the
    // explicit request of the person the presentation is for. This only
    // changes which level a spectator gets on a fresh install or after a
    // settings file with no recorded gore level; the enum's numeric values
    // are unchanged, so an existing settings file that already recorded a
    // level keeps resolving to that same level.
    private const GoreIntensity DefaultGoreIntensity = GoreIntensity.Full;
    private const MotionIntensity DefaultMotionIntensity = MotionIntensity.Full;

    private const AutoCameraMode DefaultAutoCameraMode =
        AutoCameraMode.Assisted;

    private const UiScale DefaultUiScale = UiScale.Auto;

    private const StartupDisplayMode DefaultStartupDisplayMode =
        StartupDisplayMode.Windowed;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly DiagnosticLog _log;

    public ClientSettingsStore(string settingsPath, DiagnosticLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
        _log = log ?? DiagnosticLog.Disabled;
    }

    public static ClientSettingsStore CreateDefault(DiagnosticLog? log = null)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hukbo");
        return new ClientSettingsStore(
            Path.Combine(directory, "settings.json"),
            log);
    }

    public ClientSettings Load(string defaultThemeId)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                LogDefaulted(defaultThemeId, "missing");
                return Default(defaultThemeId);
            }

            var raw = JsonSerializer.Deserialize<RawClientSettings>(
                File.ReadAllText(_settingsPath),
                SerializerOptions);

            // Schema, theme, and composition validate together: a mismatch in
            // any of them means the file cannot be trusted as a whole. Gore
            // intensity and motion intensity validate independently below so
            // that a settings file written before either field existed - or
            // with a corrupt value in one of those fields - still keeps the
            // spectator's saved theme.
            if (raw is not
                {
                    SelectedThemeId.Length: > 0,
                    Composition: not null,
                } ||
                !AcceptedSchemaVersions.Contains(raw.SchemaVersion) ||
                !raw.Composition.IsValid())
            {
                // A rejected file is replaced by defaults in memory, which
                // looks exactly like a first run from the outside. Saying which
                // field failed is the difference between a two-minute fix and
                // an afternoon.
                _log.Write(
                    LogLevel.Warning,
                    LogChannel.Settings,
                    LogEvents.SettingsInvalid,
                    "path",
                    _settingsPath,
                    "schemaVersion",
                    raw?.SchemaVersion ?? -1,
                    "supportedSchemaVersion",
                    SupportedSchemaVersion,
                    "hasThemeId",
                    raw?.SelectedThemeId is { Length: > 0 },
                    "hasComposition",
                    raw?.Composition is not null,
                    "compositionValid",
                    raw?.Composition?.IsValid() ?? false);
                return Default(defaultThemeId);
            }

            var settings = new ClientSettings(
                SupportedSchemaVersion,
                raw.SelectedThemeId,
                raw.Composition,
                ResolveGoreIntensity(raw.GoreIntensity),
                ResolveMotionIntensity(raw.MotionIntensity),
                ResolveAutoCameraMode(raw.AutoCameraMode),
                ResolveUiScale(raw.UiScale),
                ResolveStartupDisplayMode(raw.StartupDisplayMode));
            _log.Write(
                LogLevel.Debug,
                LogChannel.Settings,
                LogEvents.SettingsLoaded,
                "path",
                _settingsPath,
                "schemaVersion",
                settings.SchemaVersion,
                "themeId",
                settings.SelectedThemeId,
                "gore",
                settings.GoreIntensity.ToString(),
                "motion",
                settings.MotionIntensity.ToString(),
                "autoCamera",
                settings.AutoCameraMode.ToString(),
                "defaulted",
                false);
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            _log.Write(
                LogLevel.Warning,
                LogChannel.Settings,
                LogEvents.SettingsInvalid,
                "path",
                _settingsPath,
                "reason",
                exception.GetType().Name,
                "msg",
                exception.Message);
            return Default(defaultThemeId);
        }
    }

    public bool TrySave(
        string selectedThemeId,
        ArmyComposition composition,
        GoreIntensity goreIntensity,
        MotionIntensity motionIntensity,
        AutoCameraMode autoCameraMode,
        UiScale uiScale,
        StartupDisplayMode startupDisplayMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedThemeId);
        ArgumentNullException.ThrowIfNull(composition);

        var directory = Path.GetDirectoryName(_settingsPath);
        if (directory is null)
        {
            return false;
        }

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            var settings = new ClientSettings(
                SupportedSchemaVersion,
                selectedThemeId,
                composition,
                ResolveGoreIntensity(goreIntensity),
                ResolveMotionIntensity(motionIntensity),
                ResolveAutoCameraMode(autoCameraMode),
                ResolveUiScale(uiScale),
                ResolveStartupDisplayMode(startupDisplayMode));
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(
                    temporaryPath,
                    _settingsPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _settingsPath);
            }

            _log.Write(
                LogLevel.Information,
                LogChannel.Settings,
                LogEvents.SettingsSaved,
                "path",
                _settingsPath,
                "themeId",
                selectedThemeId,
                "gore",
                goreIntensity.ToString(),
                "motion",
                motionIntensity.ToString(),
                "autoCamera",
                autoCameraMode.ToString(),
                "uiScale",
                uiScale.ToString(),
                "startupDisplayMode",
                startupDisplayMode.ToString());
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            _log.Write(
                LogLevel.Warning,
                LogChannel.Settings,
                LogEvents.SettingsSaveFailed,
                "path",
                _settingsPath,
                "reason",
                exception.GetType().Name,
                "msg",
                exception.Message);
            return false;
        }
    }

    /// <summary>
    /// Re-reads the complete record and writes a caller-selected change so
    /// every independent settings writer preserves sibling preferences.
    /// </summary>
    public bool TryUpdate(
        string defaultThemeId,
        Func<ClientSettings, ClientSettings> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultThemeId);
        ArgumentNullException.ThrowIfNull(update);

        var next = update(Load(defaultThemeId));
        return TrySave(
            next.SelectedThemeId,
            next.Composition,
            next.GoreIntensity,
            next.MotionIntensity,
            next.AutoCameraMode,
            next.UiScale,
            next.StartupDisplayMode);
    }

    private void LogDefaulted(string defaultThemeId, string reason) =>
        _log.Write(
            LogLevel.Information,
            LogChannel.Settings,
            LogEvents.SettingsLoaded,
            "path",
            _settingsPath,
            "themeId",
            defaultThemeId,
            "reason",
            reason,
            "defaulted",
            true);

    private static ClientSettings Default(string defaultThemeId) =>
        new(
            SupportedSchemaVersion,
            defaultThemeId,
            ArmyComposition.Default,
            DefaultGoreIntensity,
            DefaultMotionIntensity,
            DefaultAutoCameraMode,
            DefaultUiScale,
            DefaultStartupDisplayMode);

    /// <summary>
    /// A missing or out-of-range gore level resolves to the default without
    /// invalidating any sibling field.
    /// </summary>
    private static GoreIntensity ResolveGoreIntensity(
        GoreIntensity? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultGoreIntensity;

    /// <summary>
    /// A missing or out-of-range motion level resolves to the default
    /// without invalidating any sibling field. Missing is also what a
    /// version 3 file - written before this field existed - looks like.
    /// </summary>
    private static MotionIntensity ResolveMotionIntensity(
        MotionIntensity? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultMotionIntensity;

    /// <summary>
    /// A missing or out-of-range camera mode resolves to the default without
    /// invalidating any sibling field. Missing is also what a version 3 or 4
    /// file - written before this field existed - looks like.
    /// </summary>
    private static AutoCameraMode ResolveAutoCameraMode(
        AutoCameraMode? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultAutoCameraMode;

    /// <summary>
    /// A missing or out-of-range UI scale resolves to automatic selection
    /// without invalidating any sibling field. Missing is what a version 7
    /// file - written before this field existed - looks like.
    /// </summary>
    private static UiScale ResolveUiScale(UiScale? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultUiScale;

    /// <summary>
    /// A missing or out-of-range startup display mode resolves to the current
    /// windowed behavior without invalidating any sibling field. Missing is
    /// what a version 7 file - written before this field existed - looks like.
    /// </summary>
    private static StartupDisplayMode ResolveStartupDisplayMode(
        StartupDisplayMode? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultStartupDisplayMode;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// The on-disk shape, deserialized before validation. Every field is
    /// nullable or unvalidated on purpose: it keeps each field's validation
    /// independent instead of coupling them through
    /// <see cref="ClientSettings"/>'s non-nullable constructor, so a field
    /// added by a later feature cannot discard an older file's saved values.
    /// </summary>
    private sealed record RawClientSettings(
        int SchemaVersion,
        string? SelectedThemeId,
        ArmyComposition? Composition,
        GoreIntensity? GoreIntensity,
        MotionIntensity? MotionIntensity,
        AutoCameraMode? AutoCameraMode,
        UiScale? UiScale,
        StartupDisplayMode? StartupDisplayMode);
}
