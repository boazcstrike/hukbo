using System.Globalization;
using Hukbo.Diagnostics;
using Sandata.Client.Audio;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Builds the audio generation manifest that <c>scripts/sfx-manifest.ps1</c>
/// prints and writes to disk. Plan task 40 permits exactly three files for
/// this work — <c>scripts/sfx-manifest.ps1</c>, <c>scripts/sfx.ps1</c>, and
/// this test file — so the manifest's row-building logic has nowhere else to
/// live. There is no fourth "generator" project. <c>scripts/sfx-manifest.ps1</c>
/// runs the <see cref="SoundManifestTests.WriteManifestArtifact"/> fact below
/// through <c>dotnet test --filter</c>, which materialises the manifest as a
/// CSV file the script then reads to print the row count and the credit
/// estimate. Nothing in this class, or in <c>scripts/sfx-manifest.ps1</c>,
/// ever calls ElevenLabs or any other network endpoint — see plan task 40's
/// hard stop and design section 10, "Generation is gated, and the gate is a
/// hard stop."
/// </summary>
/// <remarks>
/// Every value here is derived from <see cref="SandataSoundCatalog.Rows"/>,
/// the catalog task 24 built. Nothing is guessed: the plan row's "484-slot
/// matrix" and the research consolidation's total of 484 both predate task
/// 24's implementation and do not match what the catalog actually declares.
/// <see cref="SoundManifestTests.RowCountMatchesCatalogRowCount"/> and
/// <see cref="SoundManifestTests.VariantCountsMatchCatalogDeclaredCounts"/>
/// pin the real numbers so a future edit to the catalog cannot silently drift
/// from the manifest without a red test.
/// </remarks>
internal static class SoundManifestGenerator
{
    /// <summary>
    /// Where <see cref="WriteCsv"/> writes, relative to the repository root.
    /// <c>artifacts/</c> is gitignored (case-insensitively) by the root
    /// <c>.gitignore</c>, matching how <c>artifacts/logs</c> already holds
    /// generated, untracked output.
    /// </summary>
    public const string ManifestRelativePath = "artifacts/audio/sandata-sound-manifest.csv";

    /// <summary>ElevenLabs' published price per sound-generation call. Design section 10.</summary>
    public const int CreditsPerGeneration = 200;

    private static readonly string[] CsvHeader =
    [
        "RowIndex",
        "Family",
        "FamilyKeyToken",
        "ModeToken",
        "EnvironmentToken",
        "BaseFileName",
        "VariantCount",
        "TailTicks",
        "DurationSeconds",
        "TrimThresholdPercent",
        "Prompt",
    ];

    /// <summary>One manifest row: one declared <see cref="SoundSlot"/>, not one variant file.</summary>
    public readonly record struct ManifestRow(
        int RowIndex,
        string Family,
        string FamilyKeyToken,
        string ModeToken,
        string EnvironmentToken,
        string BaseFileName,
        int VariantCount,
        int TailTicks,
        double DurationSeconds,
        double TrimThresholdPercent,
        string Prompt);

    /// <summary>
    /// Builds one manifest row per row <see cref="SandataSoundCatalog"/>
    /// declares, in the catalog's own build order. The base file name is
    /// read from <see cref="SandataSoundCatalog.GetBaseName"/> directly
    /// rather than reconstructed by hand, so a future naming change cannot
    /// leave the manifest describing a file the catalog would never resolve.
    /// </summary>
    public static IReadOnlyList<ManifestRow> BuildRows()
    {
        var catalogRows = SandataSoundCatalog.Rows;
        var rows = new List<ManifestRow>(catalogRows.Length);

        for (var index = 0; index < catalogRows.Length; index++)
        {
            var slot = catalogRows[index];
            var baseFileName = SandataSoundCatalog.GetBaseName(slot);

            // GetBaseName joins four tokens with '-', and none of the token
            // tables in SandataSoundCatalog contain a '-' (calibers use 'x',
            // e.g. "556x45"), so this split reliably recovers the four axes
            // without needing access to the catalog's private token tables.
            var tokens = baseFileName.Split('-');

            rows.Add(new ManifestRow(
                RowIndex: index,
                Family: tokens[0],
                FamilyKeyToken: tokens[1],
                ModeToken: tokens[2],
                EnvironmentToken: tokens[3],
                BaseFileName: baseFileName,
                VariantCount: slot.VariantCount,
                TailTicks: slot.TailTicks,
                DurationSeconds: GetDurationSeconds(slot.Family),
                TrimThresholdPercent: GetTrimThresholdPercent(slot.Family),
                Prompt: BuildPrompt(slot)));
        }

        return rows;
    }

    /// <summary>
    /// Writes <paramref name="rows"/> as a CSV file, creating the containing
    /// directory if needed. Every field is quoted and embedded quotes are
    /// doubled, so no field-splitting ambiguity survives a comma or a quote
    /// inside a generated prompt.
    /// </summary>
    public static void WriteCsv(string path, IReadOnlyList<ManifestRow> rows)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine(string.Join(',', CsvHeader.Select(QuoteCsvField)));

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(
                ',',
                QuoteCsvField(row.RowIndex.ToString(CultureInfo.InvariantCulture)),
                QuoteCsvField(row.Family),
                QuoteCsvField(row.FamilyKeyToken),
                QuoteCsvField(row.ModeToken),
                QuoteCsvField(row.EnvironmentToken),
                QuoteCsvField(row.BaseFileName),
                QuoteCsvField(row.VariantCount.ToString(CultureInfo.InvariantCulture)),
                QuoteCsvField(row.TailTicks.ToString(CultureInfo.InvariantCulture)),
                QuoteCsvField(row.DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture)),
                QuoteCsvField(row.TrimThresholdPercent.ToString("0.0", CultureInfo.InvariantCulture)),
                QuoteCsvField(row.Prompt)));
        }
    }

    private static string QuoteCsvField(string field) =>
        "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// Authoring duration in seconds, per family. Not a catalog field: the
    /// catalog only ever carries <see cref="SoundSlot.TailTicks"/>, the
    /// in-game pool reservation, which is a playback concern rather than a
    /// generation-request length. These are starting points for a person to
    /// override once a take is heard, exactly as
    /// <c>scripts/sfx.ps1</c>'s existing <c>$defaultPrompts</c> table is for
    /// the melee catalog.
    /// </summary>
    private static double GetDurationSeconds(SoundFamily family) => family switch
    {
        SoundFamily.GunReport => 0.5,   // the API's own floor; a crack is trimmed back from it
        SoundFamily.GunLoop => 3.0,     // enough sustained fire to loop convincingly
        SoundFamily.GunTail => 1.5,     // the decay after a loop ends
        SoundFamily.Mechanism => 0.6,
        SoundFamily.Dry => 0.5,
        SoundFamily.Impact => 0.5,
        SoundFamily.Casing => 0.6,
        SoundFamily.Ui => 0.5,
        _ => throw new ArgumentOutOfRangeException(
            nameof(family), family, "Every sound family must declare an authoring duration."),
    };

    /// <summary>
    /// Per-family trim threshold, percent of the take's own peak. Design
    /// section 10: "Gunshot variants carrying reverb or echo need 1 to 2
    /// percent" rather than the 5 percent melee default
    /// (<c>scripts/sfx.ps1</c>'s existing <c>$SilenceThreshold</c> default).
    /// <c>scripts/sfx.ps1</c> declares its own copy of this same table for
    /// its (never-run) batch mode; the two are independent, matching how
    /// <c>VariantIndexDigits</c> is already duplicated with a comment across
    /// the Hukbo and Sandata catalogs rather than shared.
    /// </summary>
    private static double GetTrimThresholdPercent(SoundFamily family) => family switch
    {
        SoundFamily.GunReport => 1.5,
        SoundFamily.GunLoop => 1.5,
        SoundFamily.GunTail => 1.5,
        SoundFamily.Mechanism => 5.0,
        SoundFamily.Dry => 5.0,
        SoundFamily.Impact => 5.0,
        SoundFamily.Casing => 5.0,
        SoundFamily.Ui => 2.0,          // matches the tonal UI convention in sfx.ps1's $defaultPrompts
        _ => throw new ArgumentOutOfRangeException(
            nameof(family), family, "Every sound family must declare a trim threshold."),
    };

    /// <summary>
    /// Builds one English prompt per row, following the house rules in
    /// <c>.claude/skills/hukbo-sound-effects/SKILL.md</c>: one sound event,
    /// not a scene; music and voice excluded explicitly; the surface and
    /// space stated; no realism claims. Sandata is a modern setting, so
    /// unlike Hukbo's melee prompts there is no historical-accuracy naming
    /// constraint here — prompts describe caliber and mechanism class, never
    /// a specific manufacturer or model name.
    /// </summary>
    private static string BuildPrompt(SoundSlot slot) => slot.Family switch
    {
        SoundFamily.GunReport => string.Format(
            CultureInfo.InvariantCulture,
            "{0} from a {1} firearm, {2}, no music, no voice",
            GetFireModeReportPhrase(slot.Mode),
            GetCaliberPhrase((CaliberFamily)slot.FamilyKey),
            GetEnvironmentPhrase(slot.Environment)),

        SoundFamily.GunLoop => string.Format(
            CultureInfo.InvariantCulture,
            "the looping body of sustained automatic fire from a {0} rifle, {1}, no music, no voice",
            GetCaliberPhrase((CaliberFamily)slot.FamilyKey),
            GetEnvironmentPhrase(slot.Environment)),

        SoundFamily.GunTail => string.Format(
            CultureInfo.InvariantCulture,
            "the trailing decay that follows a burst of automatic {0} rifle fire, {1}, no music, no voice",
            GetCaliberPhrase((CaliberFamily)slot.FamilyKey),
            GetEnvironmentPhrase(slot.Environment)),

        SoundFamily.Mechanism => GetMechanismPrompt(
            (MechanismGroup)slot.FamilyKey, slot.Mode),

        SoundFamily.Dry => string.Format(
            CultureInfo.InvariantCulture,
            "a dry-fire click from a {0} weapon with no round chambered, thin and hollow, no music, no voice",
            GetCaliberPhrase((CaliberFamily)slot.FamilyKey)),

        SoundFamily.Impact => string.Format(
            CultureInfo.InvariantCulture,
            "a single bullet striking {0}, one sharp impact, dry open air, no music, no voice",
            GetImpactSurfacePhrase((ImpactSurface)slot.FamilyKey)),

        SoundFamily.Casing => string.Format(
            CultureInfo.InvariantCulture,
            "one spent {0} cartridge casing landing on {1}, small metallic clink, no music, no voice",
            GetWeaponClassPhrase((WeaponClass)slot.FamilyKey),
            GetCasingSurfacePhrase(slot.Mode)),

        SoundFamily.Ui => "one very short soft interface tick, subtle and clean, no reverb, no music, no voice",

        _ => throw new ArgumentOutOfRangeException(
            nameof(slot), slot.Family, "Every sound family must declare a prompt template."),
    };

    private static string GetFireModeReportPhrase(FireMode mode) => mode switch
    {
        FireMode.Single => "one single gunshot crack",
        FireMode.Burst2 => "a tight two-round burst",
        FireMode.Burst3 => "a tight three-round burst",
        _ => throw new ArgumentOutOfRangeException(
            nameof(mode), mode, "GunReport rows are only ever Single, Burst2, or Burst3."),
    };

    private static string GetCaliberPhrase(CaliberFamily caliber) => caliber switch
    {
        CaliberFamily.Cal762X39 => "7.62x39mm",
        CaliberFamily.Cal545X39 => "5.45x39mm",
        CaliberFamily.Cal556X45 => "5.56x45mm",
        CaliberFamily.Cal762X51 => "7.62x51mm",
        CaliberFamily.Cal68X51 => "6.8x51mm",
        CaliberFamily.Cal58X42 => "5.8x42mm",
        CaliberFamily.Cal9X19 => "9x19mm",
        CaliberFamily.Cal58X21 => "5.8x21mm",
        _ => throw new ArgumentOutOfRangeException(
            nameof(caliber), caliber, "Every CaliberFamily member must have a human-readable phrase."),
    };

    private static string GetEnvironmentPhrase(SoundEnvironment environment) => environment switch
    {
        SoundEnvironment.CloseDry => "close range in open air, no reverb",
        SoundEnvironment.IndoorTail => "indoors, with a short reflective echo",
        SoundEnvironment.OutdoorTail => "outdoors, with a longer open-air tail",
        SoundEnvironment.Distant => "heard from a distance, attenuated and soft",
        SoundEnvironment.Suppressed => "fired through a suppressor, a muted thump rather than a crack",
        _ => throw new ArgumentOutOfRangeException(
            nameof(environment), environment, "Every real SoundEnvironment member must have a phrase."),
    };

    private static string GetMechanismPrompt(MechanismGroup group, FireMode action)
    {
        var groupPhrase = group switch
        {
            MechanismGroup.Ak => "an AK-pattern rifle",
            MechanismGroup.Ar => "an AR-pattern rifle",
            MechanismGroup.Bullpup => "a bullpup rifle",
            MechanismGroup.Pistol => "a pistol",
            _ => throw new ArgumentOutOfRangeException(
                nameof(group), group, "Every MechanismGroup member must have a phrase."),
        };

        return action switch
        {
            FireMode.Selector => string.Format(
                CultureInfo.InvariantCulture,
                "{0}'s selector or safety detent clicking, dry mechanical action, no music, no voice",
                groupPhrase),
            FireMode.MagazineOut => string.Format(
                CultureInfo.InvariantCulture,
                "a loaded magazine dropping free from {0}, metal-on-metal slide, no music, no voice",
                groupPhrase),
            FireMode.MagazineIn => string.Format(
                CultureInfo.InvariantCulture,
                "a magazine seating firmly into {0}, one solid click, no music, no voice",
                groupPhrase),
            FireMode.BoltRack => string.Format(
                CultureInfo.InvariantCulture,
                "{0}'s bolt or charging handle racking a round, metallic mechanical action, no music, no voice",
                groupPhrase),
            _ => throw new ArgumentOutOfRangeException(
                nameof(action), action, "Mechanism rows are only ever Selector, MagazineOut, MagazineIn, or BoltRack."),
        };
    }

    private static string GetImpactSurfacePhrase(ImpactSurface surface) => surface switch
    {
        ImpactSurface.Concrete => "a concrete or masonry surface",
        ImpactSurface.Metal => "a metal surface",
        ImpactSurface.Wood => "a wood surface",
        ImpactSurface.Flesh => "soft tissue, muffled and wet",
        ImpactSurface.Ricochet => "a hard surface and glancing off with a ricochet whine",
        _ => throw new ArgumentOutOfRangeException(
            nameof(surface), surface, "Every ImpactSurface member must have a phrase."),
    };

    private static string GetWeaponClassPhrase(WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.Rifle => "rifle",
        WeaponClass.Pistol => "pistol",
        _ => throw new ArgumentOutOfRangeException(
            nameof(weaponClass), weaponClass, "Every WeaponClass member must have a phrase."),
    };

    private static string GetCasingSurfacePhrase(FireMode surface) => surface switch
    {
        FireMode.GroundConcrete => "concrete",
        FireMode.GroundDirt => "packed dirt",
        _ => throw new ArgumentOutOfRangeException(
            nameof(surface), surface, "Casing rows are only ever GroundConcrete or GroundDirt."),
    };
}

/// <summary>
/// Covers plan task 40's test bar: "SoundManifestTests asserts the
/// manifest's per-slot variant counts equal the catalog's declared counts."
/// Also proves the row count and writes the artifact
/// <c>scripts/sfx-manifest.ps1</c> reads, since no fourth file exists to
/// carry that responsibility separately.
/// </summary>
public sealed class SoundManifestTests
{
    /// <summary>
    /// The manifest must describe every row the catalog declares — no more,
    /// no fewer. This is the number the task's "done when" calls "the row
    /// count the catalog declares": 114 since 2026-08-14 and 106 before it,
    /// not the 484 the plan row and the design document both still say, and
    /// not the 577 individual variant files those 114 rows expand to
    /// (asserted separately below).
    /// </summary>
    [Fact]
    public void RowCountMatchesCatalogRowCount()
    {
        var manifestRows = SoundManifestGenerator.BuildRows();

        Assert.Equal(SandataSoundCatalog.Rows.Length, manifestRows.Count);
    }

    /// <summary>
    /// Every manifest row's variant count must equal the declared count on
    /// the catalog row it was built from, in the same order. This is what
    /// stops the manifest generator from mistranscribing a count for even
    /// one of the 114 rows.
    /// </summary>
    [Fact]
    public void VariantCountsMatchCatalogDeclaredCounts()
    {
        var manifestRows = SoundManifestGenerator.BuildRows();
        var catalogRows = SandataSoundCatalog.Rows;

        Assert.Equal(catalogRows.Length, manifestRows.Count);
        for (var index = 0; index < catalogRows.Length; index++)
        {
            Assert.Equal(catalogRows[index].VariantCount, manifestRows[index].VariantCount);
        }
    }

    /// <summary>
    /// The sum of every row's variant count is the true number of individual
    /// files a full generation run would produce: 577, not the plan row's
    /// and the design document's 484, not the 524 an earlier revision of this
    /// catalog declared, not the 540 that stood until 2026-08-14, and not the
    /// 572 that stood until 2026-08-15. Three separate edits carried it here.
    /// The first raised four single-shot
    /// gun-report rows — 7.62x39 and 9x19, each in close-dry and indoor-tail —
    /// from 6 declared takes to 10, since those four rows are the only ones
    /// with real generated audio on disk; see
    /// <see cref="SandataSoundCatalog"/>'s <c>GeneratedGunReportVariantCount</c>
    /// doc comment. The second is decision D5 of the 2026-08-14 lowered-weapon
    /// and automatic-fire design, which declares the automatic loop and tail
    /// rows for every caliber family rather than the six rifle calibers alone,
    /// closing a <c>KeyNotFoundException</c> an automatic-capable pistol would
    /// have hit on its first round. That adds eight rows — two calibers, two
    /// environments, loop and tail — of four variants each, so 106 rows
    /// expanding to 540 files became 114 rows expanding to 572. The third,
    /// on 2026-08-15, raised 7.62x39 close-dry alone from ten declared takes
    /// to fifteen, matching five further generated files kept from a run that
    /// asked for audible bolt-carrier cycling after the report; the row count
    /// is unchanged at 114 and no other row moved.
    /// <b>Declaring a row generates nothing.</b> No ElevenLabs spend is
    /// authorized by this number; it is the size of a hypothetical full run.
    /// Pinned here so a catalog edit that moves it is caught by a red test
    /// rather than a stale document.
    /// </summary>
    [Fact]
    public void TotalVariantFileCountIsFiveHundredSeventySeven()
    {
        var totalVariants = SandataSoundCatalog.Rows.Sum(row => (int)row.VariantCount);

        Assert.Equal(577, totalVariants);
    }

    /// <summary>
    /// Exactly the four single-shot gun-report rows that have real generated
    /// audio on disk today — 7.62x39 and 9x19, each in close-dry and
    /// indoor-tail — must declare the elevated variant count, and every other
    /// <see cref="SoundFamily.GunReport"/> / <see cref="FireMode.Single"/> row
    /// must still declare the ordinary one. This is the assertion that
    /// catches a later edit that widens or narrows
    /// <c>SandataSoundCatalog.IsGeneratedGunReportRow</c> by accident.
    /// </summary>
    [Fact]
    public void OnlyTheFourGeneratedGunReportRowsCarryTheElevatedVariantCount()
    {
        var elevatedCalibers = new[] { CaliberFamily.Cal762X39, CaliberFamily.Cal9X19 };
        var elevatedEnvironments = new[] { SoundEnvironment.CloseDry, SoundEnvironment.IndoorTail };

        var singleShotGunReportRows = SandataSoundCatalog.Rows
            .Where(row => row.Family == SoundFamily.GunReport && row.Mode == FireMode.Single)
            .ToList();

        // Four elevated rows expected: 2 calibers x 2 environments.
        var elevatedRows = 0;

        foreach (var row in singleShotGunReportRows)
        {
            var caliber = (CaliberFamily)row.FamilyKey;
            var isElevatedRow = elevatedCalibers.Contains(caliber) && elevatedEnvironments.Contains(row.Environment);

            if (isElevatedRow)
            {
                elevatedRows++;

                // 7.62x39 close-dry is the one row with fifteen files rather
                // than ten, per SandataSoundCatalog's AkCloseDryVariantCount.
                var expected = caliber == CaliberFamily.Cal762X39 && row.Environment == SoundEnvironment.CloseDry
                    ? 15
                    : 10;

                Assert.Equal(expected, row.VariantCount);
            }
            else
            {
                Assert.Equal(6, row.VariantCount);
            }
        }

        Assert.Equal(4, elevatedRows);
    }

    /// <summary>
    /// Every field required by the task ("every file name, prompt, duration,
    /// and trim threshold") is present and non-empty on every row.
    /// </summary>
    [Fact]
    public void EveryRowCarriesAllRequiredManifestFields()
    {
        foreach (var row in SoundManifestGenerator.BuildRows())
        {
            Assert.False(string.IsNullOrWhiteSpace(row.BaseFileName));
            Assert.False(string.IsNullOrWhiteSpace(row.Prompt));
            Assert.True(row.DurationSeconds > 0);
            Assert.InRange(row.TrimThresholdPercent, 0.5, 50.0);
            Assert.InRange(row.VariantCount, 1, 99);
        }
    }

    /// <summary>
    /// The artifact-writing step <c>scripts/sfx-manifest.ps1</c> invokes
    /// through <c>dotnet test --filter</c>. Writes the CSV, then reads it
    /// back and confirms the header and row count round-trip, so a broken
    /// writer is caught here rather than only downstream in the script.
    /// </summary>
    [Fact]
    public void WriteManifestArtifact()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(
            root,
            SoundManifestGenerator.ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var rows = SoundManifestGenerator.BuildRows();
        SoundManifestGenerator.WriteCsv(path, rows);

        Assert.True(File.Exists(path));

        var lines = File.ReadAllLines(path);
        // One header line plus one line per declared catalog row.
        Assert.Equal(rows.Count + 1, lines.Length);
    }

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the manifest artifact cannot be written under artifacts/.");
        return root!;
    }
}
