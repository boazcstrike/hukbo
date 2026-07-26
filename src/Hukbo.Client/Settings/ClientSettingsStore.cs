using System.Text.Json;

namespace Hukbo.Client.Settings;

internal sealed class ClientSettingsStore
{
    public const int SupportedSchemaVersion = 1;

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

            var settings = JsonSerializer.Deserialize<ClientSettings>(
                File.ReadAllText(_settingsPath),
                SerializerOptions);
            return settings is
            {
                SchemaVersion: SupportedSchemaVersion,
                SelectedThemeId.Length: > 0,
            }
                ? settings
                : Default(defaultThemeId);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return Default(defaultThemeId);
        }
    }

    public bool TrySave(string selectedThemeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedThemeId);

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
                selectedThemeId);
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
        new(SupportedSchemaVersion, defaultThemeId);

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
}
