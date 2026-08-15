using System;
using System.IO;
using System.Linq;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Client.Tests;

/// <summary>
/// The shield size against projectile size design's section 8 "shipped
/// defaults" requirement: the client actually fields combat preset
/// <see cref="CombatPresetId.PrecolonialPhilippinesV7"/> and movement preset
/// <see cref="MovementPresetId.ShieldEncumbranceV16"/>, and the default army
/// composition fields the new <see cref="ShieldId.NarrowBreastHigh"/> rows
/// for Kalis and Itak, because a shield nobody carries cannot be discovered
/// by watching. Every assertion here uses the pure-helper pattern: no
/// <c>ArenaGame</c>, no graphics device, no sprite batch, no window.
/// </summary>
public sealed class ShieldSizeDefaultsTests
{
    private const string ValidCompositionJson =
        "\"composition\":{\"unitsPerTeam\":80,\"datuCount\":20," +
        "\"maharlikaCount\":20,\"timawaCount\":20," +
        "\"alipingNamamahayCount\":20}";

    /// <summary>
    /// <c>ArenaGame.BuildScenario</c> hardcodes the combat preset rather
    /// than exposing it as a testable constant, exactly as
    /// <c>ScriptDefaultsTests</c> reads <c>verify.ps1</c>'s own text rather
    /// than executing it. Reading the source text is the only way to pin
    /// this without constructing <c>ArenaGame</c>.
    /// </summary>
    [Fact]
    public void ShippedCombatPresetIsV7()
    {
        var source = ReadSource("ArenaGame.cs");

        Assert.Contains(
            "CombatPreset = CombatPresetId.PrecolonialPhilippinesV7,",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The client's movement default lives in
    /// <see cref="ClientSettingsStore"/>, reached the same way a first-run
    /// spectator reaches it: <see cref="ClientSettingsStore.Load"/> against a
    /// settings path that has no file yet.
    /// </summary>
    /// <remarks>
    /// The shipped movement default is <b>not</b> this package's preset. The
    /// in-fight evasive footwork package landed
    /// <see cref="MovementPresetId.EvasiveFootworkV14"/> as the client default
    /// first, and the two are mutually exclusive: both restate
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/> and only one
    /// preset can be selected at a time. Overriding another package's shipped
    /// default would silently remove its feature from the only build a
    /// spectator runs, so this package leaves it alone and ships
    /// <see cref="MovementPresetId.ShieldEncumbranceV16"/> as a selectable
    /// option instead.
    /// <para>
    /// What this costs is only the movement half of the shield package —
    /// encumbrance and the block-recovery window. Shield sizes, the
    /// size-aware interception rule, and projectile bulk all live in combat
    /// preset <see cref="CombatPresetId.PrecolonialPhilippinesV7"/>, which the
    /// client does ship, so the blocking behaviour itself is live by default.
    /// </para>
    /// </remarks>
    [Fact]
    public void ShippedMovementPresetIsTheEvasivePresetAndNotTheShieldPreset()
    {
        WithTemporarySettings((store, _) =>
        {
            var settings = store.Load("command");

            Assert.Equal(
                MovementPresetId.EvasiveFootworkV14,
                settings.MovementPreset);
        });
    }

    /// <summary>
    /// The shield-encumbrance preset must at least be reachable, or the
    /// movement half of this package could never be seen at all.
    /// </summary>
    [Fact]
    public void TheShieldEncumbrancePresetIsRegisteredAndSelectable()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.ShieldEncumbranceV16));
        Assert.Contains(
            MovementPresetId.ShieldEncumbranceV16,
            Hukbo.Client.UI.ArmyCompositionPanel.MovementPresetOptions);
    }

    [Fact]
    public void DefaultCompositionFieldsBothNarrowShieldRows()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV7)
            .Roster;
        var counts = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            ArmyComposition.Default);

        var kalisNarrowIndex = IndexOf(
            roster, WeaponId.Kalis, ShieldId.NarrowBreastHigh);
        var itakNarrowIndex = IndexOf(
            roster, WeaponId.Itak, ShieldId.NarrowBreastHigh);

        Assert.True(
            kalisNarrowIndex >= 0,
            "V7's roster carries no Kalis + narrow-shield row.");
        Assert.True(
            itakNarrowIndex >= 0,
            "V7's roster carries no Itak + narrow-shield row.");
        Assert.True(
            counts[kalisNarrowIndex] > 0,
            "The default composition fields zero narrow-shield Kalis.");
        Assert.True(
            counts[itakNarrowIndex] > 0,
            "The default composition fields zero narrow-shield Itak.");
    }

    /// <summary>
    /// 250 is today's total: 48 Datu + 47 Maharlika + 110 Timawa + 45
    /// Aliping Namamahay, unchanged by this package. The narrow-shield rows
    /// take their counts out of the existing tall-hardwood weight rather
    /// than growing the army.
    /// </summary>
    [Fact]
    public void DefaultCompositionTotalIsUnchanged()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV7)
            .Roster;
        var counts = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            ArmyComposition.Default);

        Assert.Equal(250, counts.Sum());
        Assert.Equal(
            ArmyComposition.Default.DatuCount +
                ArmyComposition.Default.MaharlikaCount +
                ArmyComposition.Default.TimawaCount +
                ArmyComposition.Default.AlipingNamamahayCount,
            counts.Sum());
    }

    /// <summary>
    /// The shipped movement preset must not use equipment-relative footwork,
    /// and must carry no loadout movement rows.
    /// <para>
    /// This test replaces one that asserted the opposite invariant — that
    /// every fielded loadout resolves a movement profile under the shipped
    /// preset. That assertion was only ever satisfiable by a defect. The
    /// shield-size package first built this preset as equipment-relative with
    /// eight loadout rows, and equipment-relative footwork resolves a profile
    /// for every warrior including the ranged ones, which
    /// <c>CanonicalLoadoutIndex</c> maps no key for. The result was a hard
    /// crash on the first Bangkaw warrior, reproduced through the headless
    /// runner at combat preset 5 with movement preset 14 while combat preset
    /// 7 with movement preset 13 ran clean.
    /// </para>
    /// <para>
    /// The preset now restates <see cref="MovementPresetId.CohortLateralSpreadV13"/>
    /// and expresses shield encumbrance by scaling agent movement speed
    /// instead. Under a preset with no loadout rows,
    /// <c>ResolveLoadoutProfile</c> is never called by any gameplay path and
    /// throws for every loadout by design — which is what the neighbouring
    /// stale-settings test already records about V13. Asserting the preset's
    /// shape is therefore the real invariant; asserting that the resolver
    /// succeeds would only pass again if the crash came back.
    /// </para>
    /// </summary>
    [Fact]
    public void TheShippedMovementPresetCarriesNoLoadoutRowsAndIsNotEquipmentRelative()
    {
        var movement = MovementPresetRegistry.Get(
            MovementPresetId.ShieldEncumbranceV16);
        var shipped = MovementPresetRegistry.Get(
            MovementPresetId.CohortLateralSpreadV13);

        Assert.False(movement.UsesEquipmentRelativeFootwork);
        Assert.Empty(movement.LoadoutMovementProfiles);

        // The same shape as the preset it descends from, so the shield
        // package cannot have moved the footwork pipeline sideways.
        Assert.Equal(
            shipped.UsesEquipmentRelativeFootwork,
            movement.UsesEquipmentRelativeFootwork);
        Assert.Equal(
            shipped.LoadoutMovementProfiles.Length,
            movement.LoadoutMovementProfiles.Length);
    }

    /// <summary>
    /// The default composition must actually field both shields, or the whole
    /// shield-size package is invisible to the only person who would ever see
    /// it. Reads the counts the client really expands rather than the slider
    /// values, so a row that apportions to zero fails this.
    /// </summary>
    [Fact]
    public void TheDefaultCompositionFieldsBothShieldsAndAtLeastOneRangedWeapon()
    {
        var roster = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV7)
            .Roster;
        var counts = ArenaGame.ExpandCompositionToRosterCounts(
            roster,
            ArmyComposition.Default);

        var fieldedShields = new HashSet<ShieldId>();
        var fieldedRangedCount = 0;
        for (var index = 0; index < roster.Count; index++)
        {
            if (counts[index] <= 0)
            {
                continue;
            }

            fieldedShields.Add(roster[index].Shield);
            if (roster[index].Weapon is WeaponId.Bangkaw
                or WeaponId.Busog
                or WeaponId.Arquebus)
            {
                fieldedRangedCount += counts[index];
            }
        }

        Assert.Contains(ShieldId.TallHardwood, fieldedShields);
        Assert.Contains(ShieldId.NarrowBreastHigh, fieldedShields);
        Assert.Contains(ShieldId.None, fieldedShields);
        Assert.True(
            fieldedRangedCount > 0,
            "No ranged warrior is fielded, so no projectile is ever launched " +
            "and the projectile half of the feature cannot be seen.");
    }

    /// <summary>
    /// The ordering hazard: a settings file saved before this change can
    /// still carry <see cref="MovementPresetId.CohortLateralSpreadV13"/>.
    /// V13 registers <c>UsesEquipmentRelativeFootwork: false</c>, so no code
    /// path ever calls <c>ResolveLoadoutProfile</c> under it regardless of
    /// what the default composition fields; the stale file is read back
    /// verbatim rather than redirected, and loading it must not throw.
    /// </summary>
    [Fact]
    public void StaleV13SettingsFileStillLoadsWithoutCrashing()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                    ClientSettingsStoreSupportedSchemaVersion() +
                    ",\"selectedThemeId\":\"signal\"," +
                    ValidCompositionJson +
                    ",\"goreIntensity\":2,\"motionIntensity\":0," +
                    "\"movementPreset\":13}");

            var exception = Record.Exception(() => store.Load("command"));

            Assert.True(
                exception is null,
                $"Loading a stale V13 settings file threw: {exception}");
        });
    }

    /// <summary>
    /// The narrower, real crash risk: a stale file naming one of the three
    /// registered presets that turn equipment-relative footwork on with
    /// only <see cref="MovementRuleset.CanonicalLoadoutCount"/> rows and no
    /// narrow-shield entry --
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>. Loading
    /// it must fall back to the shipped default rather than crash.
    /// </summary>
    [Fact]
    public void StaleSixRowFootworkSettingsFileFallsBackRatherThanCrashing()
    {
        WithTemporarySettings((store, settingsPath) =>
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                "{\"schemaVersion\":" +
                    ClientSettingsStoreSupportedSchemaVersion() +
                    ",\"selectedThemeId\":\"signal\"," +
                    ValidCompositionJson +
                    ",\"goreIntensity\":2,\"motionIntensity\":0," +
                    "\"movementPreset\":6}");

            var exception = Record.Exception(() => store.Load("command"));
            Assert.True(
                exception is null,
                $"Loading a stale V6 settings file threw: {exception}");

            // Falls back to whatever the client's shipped default is, which
            // is the evasive preset rather than this package's — see the
            // remarks on ShippedMovementPresetIsTheEvasivePresetAndNotThe
            // ShieldPreset for why this package does not claim that default.
            var settings = store.Load("command");
            Assert.Equal(
                MovementPresetId.EvasiveFootworkV14,
                settings.MovementPreset);
        });
    }

    private static int ClientSettingsStoreSupportedSchemaVersion() =>
        ClientSettingsStore.SupportedSchemaVersion;

    private static int IndexOf(
        System.Collections.Generic.IReadOnlyList<CombatLoadout> roster,
        WeaponId weapon,
        ShieldId shield)
    {
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].Weapon == weapon && roster[index].Shield == shield)
            {
                return index;
            }
        }

        return -1;
    }

    private static string ReadSource(string fileName)
    {
        var directory = AppContext.BaseDirectory;
        for (var probe = directory; probe is not null; probe = Path.GetDirectoryName(probe))
        {
            var candidate = Path.Combine(
                probe, "src", "Hukbo.Client", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {fileName} above {directory}.");
    }

    private static void WithTemporarySettings(
        Action<ClientSettingsStore, string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"hukbo-shield-defaults-tests-{Guid.NewGuid():N}");
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
