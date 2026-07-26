using System.Text.Json;

namespace Hukbo.Client.Settings;

internal sealed class ClientSettingsStore
{
    public const int SupportedSchemaVersion = 2;

    private const GoreIntensity DefaultGoreIntensity = GoreIntensity.Stylized;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public ClientSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public static ClientSettingsStore CreateDefault()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hukbo");
        return new ClientSettingsStore(
            Path.Combine(directory, "settings.json"));
    }

    public ClientSettings Load(string defaultThemeId)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return Default(defaultThemeId);
            }

            var raw = JsonSerializer.Deserialize<RawClientSettings>(
                File.ReadAllText(_settingsPath),
                SerializerOptions);

            // Schema, theme, and composition validate together: a mismatch in
            // any of them means the file cannot be trusted as a whole. Gore
            // intensity validates independently below so that a settings file
            // written before the field existed - or with a corrupt value in
            // that one field - still keeps the spectator's saved theme.
            if (raw is not
                {
                    SchemaVersion: SupportedSchemaVersion,
                    SelectedThemeId.Length: > 0,
                    Composition: not null,
                } ||
                !raw.Composition.IsValid())
            {
                return Default(defaultThemeId);
            }

            return new ClientSettings(
                raw.SchemaVersion,
                raw.SelectedThemeId,
                raw.Composition,
                ResolveGoreIntensity(raw.GoreIntensity));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return Default(defaultThemeId);
        }
    }

    public bool TrySave(
        string selectedThemeId,
        ArmyComposition composition,
        GoreIntensity goreIntensity)
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
                ResolveGoreIntensity(goreIntensity));
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

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            return false;
        }
    }

    private static ClientSettings Default(string defaultThemeId) =>
        new(
            SupportedSchemaVersion,
            defaultThemeId,
            ArmyComposition.Default,
            DefaultGoreIntensity);

    /// <summary>
    /// A missing or out-of-range gore level resolves to the default without
    /// invalidating any sibling field.
    /// </summary>
    private static GoreIntensity ResolveGoreIntensity(
        GoreIntensity? persisted) =>
        persisted is { } value && Enum.IsDefined(value)
            ? value
            : DefaultGoreIntensity;

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
        GoreIntensity? GoreIntensity);
}
