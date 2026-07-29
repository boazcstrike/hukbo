using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class ClientSettingsStoreTests
{
    private static readonly ArmyComposition SampleComposition = new(
        UnitsPerTeam: 80,
        DatuCount: 30,
        MaharlikaCount: 20,
        TimawaCount: 20,
        AlipingNamamahayCount: 10);

    private const string ValidCompositionJson =
        "\"composition\":{\"unitsPerTeam\":80,\"datuCount\":20," +
        "\"maharlikaCount\":20,\"timawaCount\":20," +
        "\"alipingNamamahayCount\":20}";

    [Fact]
    public void MissingFileReturnsProvidedDefault()
    {
        WithTemporarySettings((store, _) =>
        {
            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
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
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted));
            Assert.True(store.TrySave(
                "broadcast",
                ArmyComposition.Default,
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted));

            var settings = store.Load("command");

            Assert.Equal("broadcast", settings.SelectedThemeId);
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
        });
    }

    [Fact]
    public void UnparseableSettingsReturnDefault()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{");

            Assert.Equal("command", store.Load("command").SelectedThemeId);
        });
    }

    [Fact]
    public void ACurrentVersionFileWithNoCompositionReturnsDefault()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"}");

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
                "{\"schemaVersion\":" +
                (ClientSettingsStore.SupportedSchemaVersion + 1) +
                ",\"selectedThemeId\":\"signal\"," +
                "\"composition\":{\"unitsPerTeam\":80,\"datuCount\":20," +
                "\"maharlikaCount\":20,\"timawaCount\":20," +
                "\"alipingNamamahayCount\":20}}");

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
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted));

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
                GoreIntensity.Full,
                MotionIntensity.Full,
                AutoCameraMode.Assisted));

            var settings = store.Load("signal");

            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
        });
    }

    [Fact]
    public void SavedMotionIntensityRoundTripsThroughTheStore()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "command",
                SampleComposition,
                GoreIntensity.Stylized,
                MotionIntensity.Off,
                AutoCameraMode.Assisted));

            var settings = store.Load("signal");

            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
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
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
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
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
        });
    }

    [Fact]
    public void AFileMissingMotionIntensityLoadsCleanlyAndDefaultsItToFull()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson + ",\"goreIntensity\":2}");

            var settings = store.Load("command");

            // An absent field defaults rather than rejecting the file, so a
            // future field addition can be a backward-compatible bump again.
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
        });
    }

    [Fact]
    public void AnOutOfRangeMotionIntensityResetsOnlyThatField()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
        });
    }

    [Fact]
    public void AFileMissingAutoCameraModeDefaultsItToAssisted()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Assisted, settings.AutoCameraMode);
        });
    }

    /// <summary>
    /// Versions 3, 4, and 5 were accepted while the store could read them
    /// forward by defaulting absent fields. The 500-unit default composition
    /// (version 6) and the four-rank composition shape (version 7) cannot be
    /// read forward that way — a saved composition always overrides the
    /// default, and version 6's field names do not even exist on the version
    /// 7 record — so every earlier version is discarded whole.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EverySchemaVersionBeforeSevenIsDiscardedWhole(int schemaVersion)
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" + schemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":1}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Assisted, settings.AutoCameraMode);
        });
    }

    [Fact]
    public void AnOutOfRangeAutoCameraModeResetsOnlyThatField()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                ClientSettingsStore.SupportedSchemaVersion +
                ",\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Assisted, settings.AutoCameraMode);
        });
    }

    [Fact]
    public void ASavedAutoCameraModeSurvivesARoundTrip()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Follow));

            var settings = store.Load("command");

            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
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
                "\"composition\":{\"unitsPerTeam\":100,\"datuCount\":1," +
                "\"maharlikaCount\":1,\"timawaCount\":1," +
                "\"alipingNamamahayCount\":1},\"goreIntensity\":2,\"motionIntensity\":0}");

            var settings = store.Load("command");

            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
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
                "\"composition\":{\"unitsPerTeam\":100,\"datuCount\":10," +
                "\"maharlikaCount\":10,\"timawaCount\":10," +
                "\"alipingNamamahayCount\":10}}");

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
                GoreIntensity.Full,
                MotionIntensity.Full,
                AutoCameraMode.Assisted));
            using var locked = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            Assert.False(store.TrySave(
                "signal",
                ArmyComposition.Default,
                GoreIntensity.Off,
                MotionIntensity.Off,
                AutoCameraMode.Assisted));
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
