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
    /// </summary>
    public const int SupportedSchemaVersion = 4;

    /// <summary>
    /// Schema versions <see cref="Load"/> accepts without discarding the
    /// whole file. Version 3 predates <see cref="MotionIntensity"/> and is
    /// accepted because the field-defaulting path already handles an absent
    /// value the same way it handles an absent <see cref="GoreIntensity"/>.
    /// Versions before 3 remain discarded per the deliberate reset recorded
    /// on <see cref="ArmyComposition"/>.
    /// </summary>
    private static readonly int[] AcceptedSchemaVersions =
        [3, SupportedSchemaVersion];

    private const GoreIntensity DefaultGoreIntensity = GoreIntensity.Stylized;
    private const MotionIntensity DefaultMotionIntensity = MotionIntensity.Full;

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
                raw.SchemaVersion,
                raw.SelectedThemeId,
                raw.Composition,
                ResolveGoreIntensity(raw.GoreIntensity),
                ResolveMotionIntensity(raw.MotionIntensity));
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
        MotionIntensity motionIntensity)
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
                ResolveMotionIntensity(motionIntensity));
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
                motionIntensity.ToString());
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
            DefaultMotionIntensity);

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
        MotionIntensity? MotionIntensity);
}
