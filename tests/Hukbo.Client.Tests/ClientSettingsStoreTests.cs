using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class ClientSettingsStoreTests
{
    private static readonly ArmyComposition SampleComposition = new(
        UnitsPerTeam: 80,
        GreatBladeCount: 40,
        HeavyChopperCount: 10,
        ThrustingBladeCount: 10,
        WorkBladeCount: 20);

    private const string ValidCompositionJson =
        "\"composition\":{\"unitsPerTeam\":80,\"greatBladeCount\":20," +
        "\"heavyChopperCount\":20,\"thrustingBladeCount\":20," +
        "\"workBladeCount\":20}";

    [Fact]
    public void MissingFileReturnsProvidedDefault()
    {
        WithTemporarySettings((store, _) =>
        {
            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
        });
    }

    [Fact]
    public void SavedThemeRoundTripsAndReplacesPreviousValue()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                ArmyComposition.Default,
                GoreIntensity.Stylized));
            Assert.True(store.TrySave(
                "broadcast",
                ArmyComposition.Default,
                GoreIntensity.Stylized));

            var settings = store.Load("command");

            Assert.Equal("broadcast", settings.SelectedThemeId);
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
        });
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":3,\"selectedThemeId\":\"signal\"}")]
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
    public void LoadTreatsASchemaVersionOneFileAsMissingAndReturnsDefaults()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":1,\"selectedThemeId\":\"signal\"}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
        });
    }

    [Fact]
    public void LoadRejectsASchemaVersionNewerThanSupported()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":3,\"selectedThemeId\":\"signal\"," +
                "\"composition\":{\"unitsPerTeam\":80,\"greatBladeCount\":20," +
                "\"heavyChopperCount\":20,\"thrustingBladeCount\":20," +
                "\"workBladeCount\":20}}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
        });
    }

    [Fact]
    public void SavedCompositionRoundTripsThroughTheStore()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Stylized));

            var settings = store.Load("command");

            Assert.Equal(SampleComposition, settings.Composition);
        });
    }

    [Fact]
    public void SavedGoreIntensityRoundTripsThroughTheStore()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "command",
                SampleComposition,
                GoreIntensity.Full));

            var settings = store.Load("signal");

            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
        });
    }

    [Fact]
    public void AFileWrittenBeforeGoreExistedKeepsItsThemeAndDefaultsGore()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson + "}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
        });
    }

    [Fact]
    public void AnOutOfRangeGoreIntensityResetsOnlyThatField()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson + ",\"goreIntensity\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
        });
    }

    [Fact]
    public void AnInvalidCompositionStillResetsTheWholeFileIncludingGore()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                "\"composition\":{\"unitsPerTeam\":100,\"greatBladeCount\":1," +
                "\"heavyChopperCount\":1,\"thrustingBladeCount\":1," +
                "\"workBladeCount\":1},\"goreIntensity\":2}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
        });
    }

    [Fact]
    public void LoadReturnsDefaultsForACompositionThatDoesNotSumToItsTotal()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":2,\"selectedThemeId\":\"signal\"," +
                "\"composition\":{\"unitsPerTeam\":100,\"greatBladeCount\":10," +
                "\"heavyChopperCount\":10,\"thrustingBladeCount\":10," +
                "\"workBladeCount\":10}}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
        });
    }

    [Fact]
    public void AFailedSaveLeavesThePreviousValidFileIntact()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Assert.True(store.TrySave(
                "command",
                SampleComposition,
                GoreIntensity.Full));
            using var locked = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            Assert.False(store.TrySave(
                "signal",
                ArmyComposition.Default,
                GoreIntensity.Off));
            Assert.Empty(
                Directory.GetFiles(
                    Path.GetDirectoryName(settingsPath)!,
                    "*.tmp"));
            locked.Dispose();

            var settings = store.Load("signal");
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
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
