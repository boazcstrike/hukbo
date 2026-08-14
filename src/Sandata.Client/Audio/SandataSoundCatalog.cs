using Sandata.Core.Weapons;

namespace Sandata.Client.Audio;

/// <summary>
/// The naming contract between Sandata's (not yet generated) audio folder and
/// the game. Design section 10: a data-table catalog rather than an enum,
/// because gunfire's weapon-by-fire-mode-by-environment axis cannot be
/// expressed by <c>Hukbo.Client.Audio.SoundCatalog</c>'s hit-location variant
/// axis. <c>Hukbo.Client.Audio</c> is untouched by this type; the melee
/// catalog keeps its own shape exactly as it is.
/// </summary>
/// <remarks>
/// The lookup from a declared tuple to its row is a flat array indexed by a
/// single computed offset, not a <c>Dictionary</c>, per the plan task's own
/// test bar. Building that array also doubles as the catalog's own
/// uniqueness check: two rows sharing a tuple make the static field
/// initializer throw, so a duplicate cannot ship silently.
/// </remarks>
internal static class SandataSoundCatalog
{
    /// <summary>
    /// The only supported container, matching
    /// <c>Hukbo.Client.Audio.SoundCatalog.SupportedExtension</c>.
    /// </summary>
    public const string SupportedExtension = ".wav";

    /// <summary>
    /// The zero-padded width of a variant index in a file name. Matches
    /// <c>Hukbo.Client.Audio.SoundCatalog.VariantIndexDigits</c> exactly, which
    /// keeps one naming convention across both games and keeps the 99-variant
    /// cap identical.
    /// </summary>
    public const int VariantIndexDigits = 2;

    /// <summary>The largest <see cref="SoundSlot.VariantCount"/> the filename format can express.</summary>
    public const int MaxVariantCount = 99;

    /// <summary>The smallest legal <see cref="SoundSlot.VariantCount"/>.</summary>
    public const int MinVariantCount = 1;

    /// <summary>
    /// The declared take count for every row in the catalog that has no
    /// generated audio yet. It is a placeholder, not a measurement: nothing
    /// has been recorded against it, so it stays at the number the design
    /// document originally picked until that family is actually generated.
    /// </summary>
    private const byte OrdinaryVariantCount = 6;

    /// <summary>
    /// The declared take count for the four single-shot gun-report rows that
    /// already have real generated audio on disk: 7.62x39 and 9x19, each in
    /// close-dry and indoor-tail. These are the only slots in the catalog
    /// whose declared count is not theoretical, a repeated gunshot is the
    /// most audible repetition in the game, and <see cref="ShotSlotResolver"/>
    /// only ever plays a variant this row claims to have — so these four rows
    /// carry more takes than the rest of the catalog on purpose, to widen the
    /// rotation the player actually hears.
    /// </summary>
    private const byte GeneratedGunReportVariantCount = 10;

    /// <summary>
    /// The declared take count for 7.62x39 close-dry alone, which has fifteen
    /// real takes on disk rather than ten: the ten generated on 2026-08-11 and
    /// 2026-08-12, plus five kept from the 2026-08-15 run that asked for
    /// audible bolt-carrier cycling after the report. This is a separate
    /// constant from <see cref="GeneratedGunReportVariantCount"/> rather than a
    /// raise of it, because the other three generated rows still have ten files
    /// each and <see cref="ShotSlotResolver"/> plays any variant a row claims to
    /// have — one shared constant raised to fifteen would make five shots in
    /// every fifteen silent on those rows.
    /// </summary>
    private const byte AkCloseDryVariantCount = 15;

    // Dimension sizes for the flat lookup index below. FamilyKeySpan is sized
    // to the widest FamilyKey axis in use (CaliberFamily, at 8 members) so
    // every family fits in the same flat array without a per-family shape.
    private const int FamilySpan = 8; // SoundFamily
    private const int FamilyKeySpan = 8; // widest: CaliberFamily
    private const int ModeSpan = 11; // FireMode
    private const int EnvironmentSpan = 6; // SoundEnvironment

    private const int LookupLength =
        FamilySpan * FamilyKeySpan * ModeSpan * EnvironmentSpan;

    // Design section 5's caliber-driven baked-burst rows: the burst2 and
    // burst3 samples belong to specific rifles rather than to every caliber,
    // per docs/research/2026-08-07-sandata-research-consolidated.md section 6.
    private static readonly SoundEnvironment[] NearEnvironments =
    [
        SoundEnvironment.CloseDry,
        SoundEnvironment.IndoorTail,
        SoundEnvironment.OutdoorTail,
    ];

    private static readonly CaliberFamily[] Burst2Calibers =
    [
        CaliberFamily.Cal545X39,
        CaliberFamily.Cal556X45,
    ];

    private static readonly FireMode[] MechanismActions =
    [
        FireMode.MagazineOut,
        FireMode.MagazineIn,
        FireMode.BoltRack,
    ];

    private static readonly FireMode[] CasingSurfaces =
    [
        FireMode.GroundConcrete,
        FireMode.GroundDirt,
    ];

    // Provisional per-family tail reservations in ticks. Not yet measured
    // against real hardware; design section 10 requires a hand-run harness
    // under tools/ before these stop being placeholders.
    private const int GunReportTailTicks = 12;
    private const int GunLoopTailTicks = 60;
    private const int GunTailTailTicks = 30;
    private const int MechanismTailTicks = 8;
    private const int DryTailTicks = 6;
    private const int ImpactTailTicks = 10;
    private const int CasingTailTicks = 8;

    private static readonly string[] FamilyTokens =
    [
        "gun",
        "gunloop",
        "guntail",
        "mech",
        "dry",
        "impact",
        "casing",
        "ui",
    ];

    private static readonly string[] CaliberTokens =
    [
        "762x39",
        "545x39",
        "556x45",
        "762x51",
        "68x51",
        "58x42",
        "9x19",
        "58x21",
    ];

    private static readonly string[] MechanismGroupTokens =
    [
        "ak",
        "ar",
        "bullpup",
        "pistol",
    ];

    private static readonly string[] ImpactSurfaceTokens =
    [
        "concrete",
        "metal",
        "wood",
        "flesh",
        "ricochet",
    ];

    private static readonly string[] WeaponClassTokens =
    [
        "rifle",
        "pistol",
    ];

    private static readonly string[] ModeTokens =
    [
        "none",
        "single",
        "burst2",
        "burst3",
        "auto",
        "selector",
        "magout",
        "magin",
        "bolt",
        "concrete",
        "dirt",
    ];

    private static readonly string[] EnvironmentTokens =
    [
        "none",
        "close",
        "indoor",
        "outdoor",
        "distant",
        "suppressed",
    ];

    /// <summary>Every declared row, in build order. Fixed at type load; never mutated afterward.</summary>
    public static readonly SoundSlot[] Rows = BuildRows();

    private static readonly int[] LookupIndex = BuildLookupIndex(Rows);

    /// <summary>
    /// Finds the row declared for a tuple, or throws when none was declared.
    /// </summary>
    public static SoundSlot Find(
        SoundFamily family,
        int familyKey,
        FireMode mode,
        SoundEnvironment environment)
    {
        if (!TryFind(family, familyKey, mode, environment, out var slot))
        {
            throw new KeyNotFoundException(
                $"No Sandata sound slot declared for " +
                $"{family}/{familyKey}/{mode}/{environment}.");
        }

        return slot;
    }

    /// <summary>
    /// Attempts to find the row declared for a tuple. Reads a single flat
    /// array offset; there is no dictionary anywhere on this path.
    /// </summary>
    public static bool TryFind(
        SoundFamily family,
        int familyKey,
        FireMode mode,
        SoundEnvironment environment,
        out SoundSlot slot)
    {
        var offset = GetLookupOffset(family, familyKey, mode, environment);
        if (offset < 0 || offset >= LookupLength)
        {
            slot = default;
            return false;
        }

        var rowIndex = LookupIndex[offset];
        if (rowIndex < 0)
        {
            slot = default;
            return false;
        }

        slot = Rows[rowIndex];
        return true;
    }

    /// <summary>
    /// The base file name for a row, without the variant number or
    /// extension: <c>&lt;family&gt;-&lt;key&gt;-&lt;mode&gt;-&lt;environment&gt;</c>.
    /// </summary>
    public static string GetBaseName(SoundSlot slot) =>
        string.Join(
            '-',
            GetFamilyToken(slot.Family),
            GetKeyToken(slot.Family, slot.FamilyKey),
            GetModeToken(slot.Mode),
            GetEnvironmentToken(slot.Environment));

    /// <summary>
    /// The exact file name of one numbered variant of a row, e.g.
    /// <c>gun-556x45-single-indoor-03.wav</c>.
    /// </summary>
    public static string GetVariantFileName(SoundSlot slot, int variantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variantIndex);
        if (variantIndex > slot.VariantCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(variantIndex),
                variantIndex,
                "Variant index exceeds this slot's declared VariantCount.");
        }

        return GetBaseName(slot) +
            "-" +
            variantIndex.ToString("D" + VariantIndexDigits) +
            SupportedExtension;
    }

    private static int GetLookupOffset(
        SoundFamily family,
        int familyKey,
        FireMode mode,
        SoundEnvironment environment)
    {
        if (familyKey < 0 || familyKey >= FamilyKeySpan)
        {
            return -1;
        }

        return (((int)family * FamilyKeySpan) + familyKey) * ModeSpan * EnvironmentSpan +
            ((int)mode * EnvironmentSpan) +
            (int)environment;
    }

    private static string GetFamilyToken(SoundFamily family) =>
        FamilyTokens[(int)family];

    private static string GetKeyToken(SoundFamily family, int familyKey) =>
        family switch
        {
            SoundFamily.GunReport or
            SoundFamily.GunLoop or
            SoundFamily.GunTail or
            SoundFamily.Dry => CaliberTokens[familyKey],
            SoundFamily.Mechanism => MechanismGroupTokens[familyKey],
            SoundFamily.Impact => ImpactSurfaceTokens[familyKey],
            SoundFamily.Casing => WeaponClassTokens[familyKey],
            SoundFamily.Ui => familyKey.ToString(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                "Every sound family must declare a key-token mapping."),
        };

    private static string GetModeToken(FireMode mode) =>
        ModeTokens[(int)mode];

    private static string GetEnvironmentToken(SoundEnvironment environment) =>
        EnvironmentTokens[(int)environment];

    private static SoundSlot[] BuildRows()
    {
        var rows = new List<SoundSlot>();

        AddSingleShotReports(rows);
        AddBakedBurst3(rows);
        AddBakedBurst2(rows);
        AddAutomaticLoopAndTail(rows);
        AddMechanismSelectors(rows);
        AddMechanismActions(rows);
        AddDryFire(rows);
        AddImpacts(rows);
        AddCasings(rows);

        return [.. rows];
    }

    private static void AddSingleShotReports(List<SoundSlot> rows)
    {
        foreach (var caliber in Enum.GetValues<CaliberFamily>())
        {
            foreach (var environment in AllRealEnvironments())
            {
                var variantCount = GetSingleShotVariantCount(caliber, environment);

                rows.Add(new SoundSlot(
                    SoundFamily.GunReport,
                    (int)caliber,
                    FireMode.Single,
                    environment,
                    variantCount,
                    GunReportTailTicks));
            }
        }
    }

    /// <summary>
    /// How many numbered takes one single-shot gun-report row declares. Three
    /// tiers, each backed by what is actually on disk: 7.62x39 close-dry has
    /// fifteen files, the other three generated rows have ten, and every
    /// remaining caliber and environment has none at all and keeps the
    /// theoretical placeholder.
    /// </summary>
    private static byte GetSingleShotVariantCount(CaliberFamily caliber, SoundEnvironment environment)
    {
        if (caliber == CaliberFamily.Cal762X39 && environment == SoundEnvironment.CloseDry)
        {
            return AkCloseDryVariantCount;
        }

        return IsGeneratedGunReportRow(caliber, environment)
            ? GeneratedGunReportVariantCount
            : OrdinaryVariantCount;
    }

    /// <summary>
    /// True for exactly the four single-shot gun-report rows that have real
    /// generated audio on disk today, per <see cref="GeneratedGunReportVariantCount"/>'s
    /// doc comment. Kept as its own named predicate rather than inlined so a
    /// later edit that widens this set has one place to change and one test,
    /// <c>SoundManifestTests.OnlyTheFourGeneratedGunReportRowsCarryTheElevatedVariantCount</c>,
    /// that fails if it drifts.
    /// </summary>
    private static bool IsGeneratedGunReportRow(CaliberFamily caliber, SoundEnvironment environment) =>
        (caliber == CaliberFamily.Cal762X39 || caliber == CaliberFamily.Cal9X19) &&
        (environment == SoundEnvironment.CloseDry || environment == SoundEnvironment.IndoorTail);

    private static void AddBakedBurst3(List<SoundSlot> rows)
    {
        foreach (var environment in NearEnvironments)
        {
            rows.Add(new SoundSlot(
                SoundFamily.GunReport,
                (int)CaliberFamily.Cal556X45,
                FireMode.Burst3,
                environment,
                4,
                GunReportTailTicks));
        }
    }

    private static void AddBakedBurst2(List<SoundSlot> rows)
    {
        foreach (var caliber in Burst2Calibers)
        {
            foreach (var environment in NearEnvironments)
            {
                rows.Add(new SoundSlot(
                    SoundFamily.GunReport,
                    (int)caliber,
                    FireMode.Burst2,
                    environment,
                    4,
                    GunReportTailTicks));
            }
        }
    }

    private static void AddAutomaticLoopAndTail(List<SoundSlot> rows)
    {
        SoundEnvironment[] autoEnvironments =
        [
            SoundEnvironment.IndoorTail,
            SoundEnvironment.OutdoorTail,
        ];

        // Every caliber family, not just the six rifle calibers: an
        // automatic-capable pistol (Cal9X19 or Cal58X21) must resolve a
        // GunLoop and GunTail row exactly as a rifle does, or
        // ShotSlotResolver.FindWithFallback's last resort,
        // SandataSoundCatalog.Find, throws KeyNotFoundException on its first
        // shot. Declaring the row does not create an audio file; a missing
        // file already plays as silence through the existing negative-cache
        // path in MonoGameSandataSoundOutput.
        foreach (var caliber in Enum.GetValues<CaliberFamily>())
        {
            foreach (var environment in autoEnvironments)
            {
                rows.Add(new SoundSlot(
                    SoundFamily.GunLoop,
                    (int)caliber,
                    FireMode.Auto,
                    environment,
                    4,
                    GunLoopTailTicks));
                rows.Add(new SoundSlot(
                    SoundFamily.GunTail,
                    (int)caliber,
                    FireMode.Auto,
                    environment,
                    4,
                    GunTailTailTicks));
            }
        }
    }

    private static void AddMechanismSelectors(List<SoundSlot> rows)
    {
        foreach (var group in Enum.GetValues<MechanismGroup>())
        {
            rows.Add(new SoundSlot(
                SoundFamily.Mechanism,
                (int)group,
                FireMode.Selector,
                SoundEnvironment.None,
                4,
                MechanismTailTicks));
        }
    }

    private static void AddMechanismActions(List<SoundSlot> rows)
    {
        foreach (var group in Enum.GetValues<MechanismGroup>())
        {
            foreach (var action in MechanismActions)
            {
                rows.Add(new SoundSlot(
                    SoundFamily.Mechanism,
                    (int)group,
                    action,
                    SoundEnvironment.None,
                    4,
                    MechanismTailTicks));
            }
        }
    }

    private static void AddDryFire(List<SoundSlot> rows)
    {
        foreach (var caliber in Enum.GetValues<CaliberFamily>())
        {
            rows.Add(new SoundSlot(
                SoundFamily.Dry,
                (int)caliber,
                FireMode.None,
                SoundEnvironment.None,
                3,
                DryTailTicks));
        }
    }

    private static void AddImpacts(List<SoundSlot> rows)
    {
        foreach (var surface in Enum.GetValues<ImpactSurface>())
        {
            rows.Add(new SoundSlot(
                SoundFamily.Impact,
                (int)surface,
                FireMode.None,
                SoundEnvironment.None,
                8,
                ImpactTailTicks));
        }
    }

    private static void AddCasings(List<SoundSlot> rows)
    {
        foreach (var ammoClass in Enum.GetValues<WeaponClass>())
        {
            foreach (var surface in CasingSurfaces)
            {
                rows.Add(new SoundSlot(
                    SoundFamily.Casing,
                    (int)ammoClass,
                    surface,
                    SoundEnvironment.None,
                    6,
                    CasingTailTicks));
            }
        }
    }

    private static SoundEnvironment[] AllRealEnvironments() =>
    [
        SoundEnvironment.CloseDry,
        SoundEnvironment.IndoorTail,
        SoundEnvironment.OutdoorTail,
        SoundEnvironment.Distant,
        SoundEnvironment.Suppressed,
    ];

    private static int[] BuildLookupIndex(SoundSlot[] rows)
    {
        var index = new int[LookupLength];
        Array.Fill(index, -1);

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var offset = GetLookupOffset(
                row.Family,
                row.FamilyKey,
                row.Mode,
                row.Environment);

            if (offset < 0 || offset >= LookupLength)
            {
                throw new InvalidOperationException(
                    $"Sandata sound slot row {rowIndex} has an out-of-range " +
                    $"tuple {row.Family}/{row.FamilyKey}/{row.Mode}/" +
                    $"{row.Environment}.");
            }

            if (index[offset] >= 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate Sandata sound slot tuple " +
                    $"{row.Family}/{row.FamilyKey}/{row.Mode}/" +
                    $"{row.Environment} at rows {index[offset]} and " +
                    $"{rowIndex}.");
            }

            index[offset] = rowIndex;
        }

        return index;
    }
}
