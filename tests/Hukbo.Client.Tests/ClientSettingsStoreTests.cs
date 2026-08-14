using Hukbo.Client.Settings;
using Hukbo.Core.Movement;

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
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Full, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Assisted, settings.AutoCameraMode);
            Assert.Equal(UiScale.Auto, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Windowed,
                settings.StartupDisplayMode);
            Assert.Equal(
                MovementPresetId.CohortLateralSpreadV13,
                settings.MovementPreset);
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
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));
            Assert.True(store.TrySave(
                "broadcast",
                ArmyComposition.Default,
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

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
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

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
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            var settings = store.Load("signal");

            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
        });
    }

    /// <summary>
    /// Proves the 2026-08-13 default change moved which level a spectator
    /// gets when they never chose one, and nothing else: a settings file that
    /// already recorded <c>Stylized</c> keeps resolving to <c>Stylized</c>,
    /// not to the new default.
    /// </summary>
    [Fact]
    public void AStoredStylizedGoreIntensitySurvivesTheNewDefaultBeingFull()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "command",
                SampleComposition,
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            var settings = store.Load("signal");

            Assert.Equal(GoreIntensity.Stylized, settings.GoreIntensity);
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
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

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
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
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
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
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
    /// 7 record — so every version through 7 is discarded whole. Version 7
    /// itself moved into this theory when the 8-to-9 bump narrowed the
    /// accepted window to <c>[8, 9]</c>, the same precedent the 7-to-8 bump
    /// set for version 6. Versions 8 and 9 moved into this theory in turn
    /// when the 9-to-10 bump — the fourth deliberate composition reset,
    /// recorded on <see cref="ArmyComposition"/> — narrowed the accepted
    /// window to version 10 alone: a saved composition always overrides the
    /// new calibrated default, so an old even-split composition can no longer
    /// be allowed to survive a load. The 10-to-11 bump widened the window
    /// back to <c>[10, 11]</c> without moving any version in this theory,
    /// because it only adds an independently defaulted chrome-style field.
    /// Version 10 moved into this theory in turn when the 11-to-12 bump
    /// narrowed the accepted window to <c>[11, 12]</c>: that bump only adds
    /// an independently defaulted weapon-visual-style field too, but the
    /// window still slides forward by one rather than widening, on the same
    /// terms every field-adding bump before it has followed.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void EverySchemaVersionBeforeTenIsDiscardedWhole(int schemaVersion)
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
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
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
                AutoCameraMode.Follow,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            var settings = store.Load("command");

            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
        });
    }

    [Theory]
    [InlineData(UiScale.Auto)]
    [InlineData(UiScale.Percent100)]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void EveryUiScaleValueSurvivesARoundTrip(UiScale uiScale)
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                uiScale,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Reduced, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(uiScale, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Fullscreen,
                settings.StartupDisplayMode);
        });
    }

    [Theory]
    [InlineData(StartupDisplayMode.Windowed)]
    [InlineData(StartupDisplayMode.Fullscreen)]
    public void EveryStartupDisplayModeSurvivesARoundTrip(
        StartupDisplayMode startupDisplayMode)
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                UiScale.Percent150,
                startupDisplayMode,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Reduced, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiScale.Percent150, settings.UiScale);
            Assert.Equal(startupDisplayMode, settings.StartupDisplayMode);
        });
    }

    [Fact]
    public void AnOutOfRangeUiScaleResetsOnlyThatField()
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
                "\"autoCameraMode\":2,\"uiScale\":175," +
                "\"startupDisplayMode\":1}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiScale.Auto, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Fullscreen,
                settings.StartupDisplayMode);
        });
    }

    [Fact]
    public void AnOutOfRangeStartupDisplayModeResetsOnlyThatField()
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
                "\"autoCameraMode\":2,\"uiScale\":125," +
                "\"startupDisplayMode\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiScale.Percent125, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Windowed,
                settings.StartupDisplayMode);
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
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
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
    public void IndependentUpdatesPreserveEverySiblingPreference()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "command",
                ArmyComposition.Default,
                GoreIntensity.Stylized,
                MotionIntensity.Full,
                AutoCameraMode.Assisted,
                UiScale.Auto,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));

            Assert.True(store.TryUpdate(
                "command",
                current => current with { SelectedThemeId = "signal" }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with { Composition = SampleComposition }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with { GoreIntensity = GoreIntensity.Full }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with { MotionIntensity = MotionIntensity.Off }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with
                {
                    AutoCameraMode = AutoCameraMode.Off,
                }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with { UiScale = UiScale.Percent150 }));
            Assert.True(store.TryUpdate(
                "command",
                current => current with
                {
                    StartupDisplayMode = StartupDisplayMode.Fullscreen,
                }));

            var settings = store.Load("command");
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Off, settings.AutoCameraMode);
            Assert.Equal(UiScale.Percent150, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Fullscreen,
                settings.StartupDisplayMode);
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
                AutoCameraMode.Assisted,
                UiScale.Percent150,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));
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
                AutoCameraMode.Assisted,
                UiScale.Percent100,
                StartupDisplayMode.Windowed,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural));
            Assert.Empty(
                Directory.GetFiles(
                    Path.GetDirectoryName(settingsPath)!,
                    "*.tmp"));
            locked.Dispose();

            var settings = store.Load("signal");
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(UiScale.Percent150, settings.UiScale);
            Assert.Equal(
                StartupDisplayMode.Fullscreen,
                settings.StartupDisplayMode);
        });
    }

    [Fact]
    public void ASavedMovementPresetSurvivesARoundTrip()
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                UiScale.Percent150,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.EquipmentRelativeFootworkV7,
                UiChromeStyle.Procedural));

            var settings = store.Load("command");

            Assert.Equal(
                MovementPresetId.EquipmentRelativeFootworkV7,
                settings.MovementPreset);
        });
    }

    [Fact]
    public void AFileMissingMovementPresetLoadsCleanlyAndDefaultsIt()
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

            // An absent field defaults rather than rejecting the file, so a
            // version 8 file — written before this setting existed — still
            // loads cleanly.
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(
                MovementPresetId.CohortLateralSpreadV13,
                settings.MovementPreset);
        });
    }

    [Fact]
    public void AnOutOfRangeMovementPresetResetsOnlyThatField()
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
                "\"movementPreset\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(
                MovementPresetId.CohortLateralSpreadV13,
                settings.MovementPreset);
        });
    }

    /// <summary>
    /// No registered <see cref="MovementPresetId"/> value is currently
    /// unregistered — <see cref="MovementPresetRegistry.IsRegistered"/>
    /// returns true for every named value 1 through 10 — so an out-of-range
    /// numeric value is the only reachable way to exercise "unregistered"
    /// today, and it exercises <c>Enum.IsDefined</c> and
    /// <see cref="MovementPresetRegistry.IsRegistered"/> together rather than
    /// in isolation. What this test pins is the observable contract: a value
    /// <c>Scenario.Validate</c> would reject never reaches
    /// <see cref="ClientSettings.MovementPreset"/>.
    /// </summary>
    [Fact]
    public void AnUnregisteredMovementPresetFallsBackToTheDefaultRatherThanFailingValidation()
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
                "\"movementPreset\":11}");

            var settings = store.Load("command");

            Assert.True(Enum.IsDefined(settings.MovementPreset));
            Assert.True(
                MovementPresetRegistry.IsRegistered(settings.MovementPreset));
            Assert.Equal(
                MovementPresetId.LastStandEngagementV11,
                settings.MovementPreset);
        });
    }

    /// <summary>
    /// Before the 9-to-10 composition reset, a version 9 file was fully
    /// shape-compatible with the current record — every field, including
    /// <see cref="ClientSettings.MovementPreset"/>, already existed and
    /// would have loaded verbatim. This test pins that the reset discards
    /// it anyway: shape compatibility alone is not enough, because a saved
    /// composition always overrides <see cref="ArmyComposition.Default"/>,
    /// and an old even-split composition would otherwise survive the load
    /// and silently defeat the calibrated default.
    /// </summary>
    [Fact]
    public void ASchemaNineFileWithAFullMovementPresetIsStillDiscardedByTheCompositionReset()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":9,\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":2,\"uiScale\":2," +
                "\"startupDisplayMode\":1,\"movementPreset\":11}");

            var settings = store.Load("command");

            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
            Assert.Equal("command", settings.SelectedThemeId);
            Assert.Equal(ArmyComposition.Default, settings.Composition);
            Assert.Equal(
                MovementPresetId.CohortLateralSpreadV13,
                settings.MovementPreset);
        });
    }

    [Theory]
    [InlineData(UiChromeStyle.Procedural)]
    [InlineData(UiChromeStyle.NineSlice)]
    public void EveryUiChromeStyleValueSurvivesARoundTrip(
        UiChromeStyle uiChromeStyle)
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                UiScale.Percent150,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.LastStandEngagementV11,
                uiChromeStyle));

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(uiChromeStyle, settings.UiChromeStyle);
        });
    }

    /// <summary>
    /// A version 11 file predates both <c>PawnVisualStyle</c> and
    /// <c>WeaponVisualStyle</c>, and loads with both at their defaults. The
    /// version 10 case, which additionally predates <see cref="UiChromeStyle"/>,
    /// is covered by
    /// <see cref="AVersionTenFileLoadsCleanlyAndDefaultsTheChromeStyle"/>;
    /// widening the accepted window with each field-adding bump is what keeps
    /// both of them loadable rather than discarded.
    /// </summary>
    [Fact]
    public void AVersionElevenFileLoadsCleanlyAndDefaultsTheWeaponVisualStyle()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":11,\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":2,\"uiScale\":2," +
                "\"startupDisplayMode\":1,\"movementPreset\":11," +
                "\"uiChromeStyle\":1}");

            var settings = store.Load("command");

            // An absent field defaults rather than rejecting the file, so a
            // version 11 file — written before this field existed — still
            // loads cleanly.
            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(
                MovementPresetId.LastStandEngagementV11,
                settings.MovementPreset);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                WeaponVisualStyle.Procedural,
                settings.WeaponVisualStyle);
        });
    }

    /// <summary>
    /// A version 10 file predates this field, so it looks exactly like a file
    /// with the field absent: it loads cleanly rather than being discarded,
    /// and the style defaults to <see cref="UiChromeStyle.Procedural"/>
    /// without disturbing any sibling field. Version 9 is not used here even
    /// though this field was planned against a 9-to-10 bump: the composition
    /// reset took version 10 first, and a version 9 file is discarded whole.
    /// </summary>
    [Fact]
    public void AVersionTenFileLoadsCleanlyAndDefaultsTheChromeStyle()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":10,\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":2,\"uiScale\":2," +
                "\"startupDisplayMode\":1,\"movementPreset\":11}");

            var settings = store.Load("command");

            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(
                MovementPresetId.LastStandEngagementV11,
                settings.MovementPreset);
            Assert.Equal(UiChromeStyle.Procedural, settings.UiChromeStyle);
        });
    }

    [Fact]
    public void AnOutOfRangeUiChromeStyleResetsOnlyThatField()
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
                "\"autoCameraMode\":2,\"uiChromeStyle\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiChromeStyle.Procedural, settings.UiChromeStyle);
        });
    }

    /// <summary>
    /// The literal <c>13</c> rather than
    /// <see cref="ClientSettingsStore.SupportedSchemaVersion"/> is asserted
    /// against here, so this test still catches the schema window narrowing
    /// unexpectedly even if the constant itself moves in the same change. It
    /// was literal <c>12</c> before the 12-to-13 bump added the weapon
    /// visual style field, and literal <c>11</c> before that, before the
    /// 11-to-12 bump added the pawn visual style field. The input file
    /// itself stays at schema version 11 - a load always normalizes the
    /// loaded settings' <c>SchemaVersion</c> to whatever is currently
    /// supported, so the source file predating both the pawn visual style
    /// field and the weapon visual style field is what this test still
    /// exercises.
    /// </summary>
    [Fact]
    public void ASchemaVersionElevenFileLoadsAndRoundTripsTheChromeStyle()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":11,\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":2,\"uiChromeStyle\":1}");

            var settings = store.Load("command");

            Assert.Equal(13, settings.SchemaVersion);
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
        });
    }

    [Theory]
    [InlineData(PawnVisualStyle.Procedural)]
    [InlineData(PawnVisualStyle.SpriteBody)]
    public void EveryPawnVisualStyleValueSurvivesARoundTrip(
        PawnVisualStyle pawnVisualStyle)
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                UiScale.Percent150,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural,
                pawnVisualStyle));

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(pawnVisualStyle, settings.PawnVisualStyle);
        });
    }

    [Theory]
    [InlineData(WeaponVisualStyle.Procedural)]
    [InlineData(WeaponVisualStyle.Sprite)]
    public void EveryWeaponVisualStyleValueSurvivesARoundTrip(
        WeaponVisualStyle weaponVisualStyle)
    {
        WithTemporarySettings((store, _) =>
        {
            Assert.True(store.TrySave(
                "signal",
                SampleComposition,
                GoreIntensity.Full,
                MotionIntensity.Reduced,
                AutoCameraMode.Follow,
                UiScale.Percent150,
                StartupDisplayMode.Fullscreen,
                MovementPresetId.LastStandEngagementV11,
                UiChromeStyle.Procedural,
                weaponVisualStyle: weaponVisualStyle));

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(SampleComposition, settings.Composition);
            Assert.Equal(weaponVisualStyle, settings.WeaponVisualStyle);
        });
    }

    /// <summary>
    /// A file at the current schema version but written before the pawn
    /// visual style field existed looks exactly like one with the field
    /// absent: it loads cleanly rather than being discarded, and the style
    /// defaults to <see cref="PawnVisualStyle.Procedural"/> without
    /// disturbing any sibling field.
    /// </summary>
    [Fact]
    public void AFileMissingPawnVisualStyleLoadsCleanlyAndDefaultsIt()
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
                "\"autoCameraMode\":2,\"uiChromeStyle\":1}");

            var settings = store.Load("command");

            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                PawnVisualStyle.Procedural,
                settings.PawnVisualStyle);
        });
    }

    /// <summary>
    /// A file at the current schema version but written before the weapon
    /// visual style field existed looks exactly like one with the field
    /// absent: it loads cleanly rather than being discarded, and the style
    /// defaults to <see cref="WeaponVisualStyle.Procedural"/> without
    /// disturbing any sibling field.
    /// </summary>
    [Fact]
    public void AFileMissingWeaponVisualStyleLoadsCleanlyAndDefaultsItToProcedural()
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
                "\"autoCameraMode\":2,\"uiChromeStyle\":1}");

            var settings = store.Load("command");

            // An absent field defaults rather than rejecting the file, so a
            // future field addition can be a backward-compatible bump again.
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                WeaponVisualStyle.Procedural,
                settings.WeaponVisualStyle);
        });
    }

    [Fact]
    public void AnOutOfRangePawnVisualStyleResetsOnlyThatField()
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
                "\"autoCameraMode\":2,\"uiChromeStyle\":1," +
                "\"pawnVisualStyle\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                PawnVisualStyle.Procedural,
                settings.PawnVisualStyle);
        });
    }

    [Fact]
    public void AnOutOfRangeWeaponVisualStyleResetsOnlyThatField()
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
                "\"autoCameraMode\":2,\"uiChromeStyle\":1," +
                "\"weaponVisualStyle\":99}");

            var settings = store.Load("command");

            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(80, settings.Composition.UnitsPerTeam);
            Assert.Equal(GoreIntensity.Full, settings.GoreIntensity);
            Assert.Equal(MotionIntensity.Off, settings.MotionIntensity);
            Assert.Equal(AutoCameraMode.Follow, settings.AutoCameraMode);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                WeaponVisualStyle.Procedural,
                settings.WeaponVisualStyle);
        });
    }

    /// <summary>
    /// A version 11 file predates the pawn visual style field, so it looks
    /// exactly like a file with the field absent: it loads cleanly rather
    /// than being discarded, and the style defaults to
    /// <see cref="PawnVisualStyle.Procedural"/> without disturbing any
    /// sibling field.
    /// </summary>
    [Fact]
    public void ASchemaVersionElevenFileLoadsAndGetsPawnVisualStyleDefaulted()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":11,\"selectedThemeId\":\"signal\"," +
                ValidCompositionJson +
                ",\"goreIntensity\":2,\"motionIntensity\":0," +
                "\"autoCameraMode\":2,\"uiChromeStyle\":1}");

            var settings = store.Load("command");

            Assert.Equal(
                ClientSettingsStore.SupportedSchemaVersion,
                settings.SchemaVersion);
            Assert.Equal("signal", settings.SelectedThemeId);
            Assert.Equal(UiChromeStyle.NineSlice, settings.UiChromeStyle);
            Assert.Equal(
                PawnVisualStyle.Procedural,
                settings.PawnVisualStyle);
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
