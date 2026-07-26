using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class ClientSettingsStoreTests
{
    [Fact]
    public void MissingFileReturnsProvidedDefault()
    {
        WithTemporarySettings((store, _) =>
        {
            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
        });
    }

    [Fact]
    public void SavedThemeRoundTripsAndReplacesPreviousValue()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave("signal"));
            Assert.True(store.TrySave("broadcast"));

            var settings = store.Load("command");

            Assert.Equal("broadcast", settings.SelectedThemeId);
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
        });
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":2,\"selectedThemeId\":\"signal\"}")]
    public void InvalidSettingsReturnDefault(string contents)
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, contents);

            Assert.Equal("command", store.Load("command").SelectedThemeId);
        });
    }

    [Fact]
    public void FailedSavePreservesPreviousFileAndCleansTemporaryFile()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Assert.True(store.TrySave("command"));
            using var locked = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            Assert.False(store.TrySave("signal"));
            Assert.Empty(
                Directory.GetFiles(
                    Path.GetDirectoryName(settingsPath)!,
                    "*.tmp"));
            locked.Dispose();
            Assert.Equal("command", store.Load("signal").SelectedThemeId);
        });
    }

    private static void WithTemporarySettings(
        Action<ClientSettingsStore, string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"hukbo-settings-tests-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            action(new ClientSettingsStore(settingsPath), settingsPath);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
