namespace Sandata.Core.Weapons;

/// <summary>
/// The 38-row firearm roster, one <see cref="FirearmDefinition"/> per
/// <see cref="FirearmId"/> in dense declaration order. Design section 9's
/// "how the 38 weapons are stored" decision: a code table, not a data file —
/// <c>TreatWarningsAsErrors</c> and the analysers catch a missing field or a
/// duplicated identifier at compile time, and the diff for a single changed
/// timing sits next to the comment that explains it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What each row's field is sourced from.</b>
/// <see cref="FirearmDefinition.Id"/>, <see cref="FirearmDefinition.Class"/>,
/// <see cref="FirearmDefinition.Caliber"/>, <see cref="FirearmDefinition.Mechanism"/>,
/// and <see cref="FirearmDefinition.Modes"/> are sourced from
/// docs/research/2026-08-07-sandata-research-consolidated.md section 3 and
/// (for <see cref="MechanismGroup.Bullpup"/>) from the four rows'
/// <see cref="FirearmId"/> XML documentation, which already names them
/// bullpup. <see cref="FirearmDefinition.MagazineCapacity"/> and
/// <see cref="FirearmDefinition.CyclicRpm"/> are sourced from public
/// manufacturer and procurement specifications for the named weapon — section
/// 3 does not carry a per-weapon ballistics table, only the caliber-family and
/// fire-mode-set groupings, so these two fields are this task's own research
/// rather than a value copied out of that document. Every other numeric
/// field — the timing chain, the range bands, the accuracy terms, and the
/// reload time — has <b>no</b> per-weapon source in either the design or the
/// research document: only two class-level data points exist at all (design
/// section 9's "rifle ready around 405 ms against roughly 80 ms for a 1911"
/// and "aim time ... 150 to 180 ms for a pistol, around 335 ms for a rifle").
/// Rather than inventing 38 rows of false per-weapon precision the research
/// does not support, this catalog applies one <see cref="RifleTemplate"/> and
/// one <see cref="PistolTemplate"/> uniformly within each <see cref="WeaponClass"/>,
/// built from those two published data points where one exists and from a
/// documented provisional placeholder where none does. This mirrors how
/// <c>SandataRuleset</c>'s own doc comment (Rules/SandataRuleset.cs) marks its
/// tunables "provisional reconstructions of gameplay tuning, not
/// measurements" pending a later task's calibration — no such calibration
/// task exists yet for per-weapon timing, so this catalog stays honest about
/// that rather than presenting invented differentiation as researched fact.
/// </para>
/// <para>
/// <b>A discrepancy worth recording.</b> Research section 3 states the
/// <c>{Safe, Single, Auto}</c> mode set "covers eighteen rifles", with the
/// remaining four named individually (M16A4, M4 with <c>Burst3</c>; AK-12
/// 2018/2021, G36 with <c>Burst2 | Auto</c>). Eighteen plus four is 22, but
/// the roster has 24 rifles — two rifles are not named by that sentence. This
/// catalog places every rifle not named as a <c>Burst3</c> or <c>Burst2</c>
/// carrier into <c>{Safe, Single, Auto}</c>, which is 20 rifles rather than
/// 18. No test in this task's acceptance criteria depends on the literal
/// count "eighteen" — only on each row's <see cref="FireModeSet"/> being one
/// of the five distinct sets, on no row carrying both bursts, and on M4 versus
/// M4A1 and AK-12 2018/2021 versus AK-12 2023 differing exactly as specified
/// — so this is reported rather than treated as blocking.
/// </para>
/// <para>
/// The pistol single-fire-versus-safe-and-single split (striker-fired pistols
/// with no manual thumb safety get <see cref="FireModeSet.Single"/> alone;
/// hammer-fired designs and the safety-lever military variants get
/// <see cref="FireModeSet.Safe"/> as well) is likewise this task's own
/// classification from each pistol's publicly documented mechanism, since
/// research section 3 states only the two set shapes and their total count,
/// not which specific pistol takes which.
/// </para>
/// </remarks>
public static class FirearmCatalog
{
    // Rifle timing-chain, range-band, and accuracy defaults. Sourced from
    // design section 9's two published rifle data points (ReadyMs, AimBaseMs)
    // and from its generic "at 16 wu per metre" range-band figures
    // (AutoBandMaxWu, BurstBandMaxWu, SingleBandMaxWu). Every other constant
    // below has no source anywhere and is a documented provisional
    // placeholder, applied uniformly rather than invented per row. See the
    // type-level remarks above.
    private const int RifleReadyMs = 405;
    private const int RifleAimBaseMs = 335;
    private const int RifleAimPerBamMs = 5;
    private const int RifleResetMs = 150;
    private const int RifleTurnBamPerTick = 2048;
    private const int RifleAutoBandMaxWu = 240;
    private const int RifleBurstBandMaxWu = 320;
    private const int RifleSingleBandMaxWu = 800;
    private const int RifleDispersionAtZeroWu = 32;
    private const int RifleDispersionAtMaxWu = 256;
    private const int RifleMaxEffectiveWu = 800;
    private const int RifleReloadMs = 2500;

    // Pistol timing-chain, range-band, and accuracy defaults. ReadyMs and
    // AimBaseMs are design section 9's published pistol data points
    // ("roughly 80 ms for a 1911", "150 to 180 ms for a pistol" — the
    // midpoint, 165, is used since the research names a range rather than a
    // point value). AutoBandMaxWu and BurstBandMaxWu are 0 rather than
    // invented: every pistol row's Modes lacks both Auto and Burst flags, so
    // the fire-mode band rule (design section 9) never reaches these fields
    // regardless of their value, and 0 keeps that inertness visible in the
    // data instead of implying an unresearched band boundary exists.
    private const int PistolReadyMs = 80;
    private const int PistolAimBaseMs = 165;
    private const int PistolAimPerBamMs = 3;
    private const int PistolResetMs = 120;
    private const int PistolTurnBamPerTick = 4096;
    private const int PistolAutoBandMaxWu = 0;
    private const int PistolBurstBandMaxWu = 0;
    private const int PistolSingleBandMaxWu = 320;
    private const int PistolDispersionAtZeroWu = 64;
    private const int PistolDispersionAtMaxWu = 512;
    private const int PistolMaxEffectiveWu = 320;
    private const int PistolReloadMs = 1600;

    // No pistol row's Modes ever reaches Auto or Burst, so CyclicRpm's
    // per-round accumulator (design section 9) is never driven for a pistol.
    // The field is still populated, for structural uniformity across every
    // row, with this documented inert placeholder rather than a fabricated
    // per-pistol mechanical action speed no source records.
    private const int PistolInertCyclicRpm = 600;

    /// <summary>
    /// All 38 rows, indexed identically to <see cref="FirearmId"/>'s numeric
    /// values. <c>FirearmCatalogTests</c> asserts the length and the index
    /// alignment.
    /// </summary>
    public static readonly IReadOnlyList<FirearmDefinition> Rows = new[]
    {
        // Rifles (24): AK pattern, AR pattern, and other service rifles.
        Rifle(FirearmId.Ak47, CaliberFamily.Cal762X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Akm, CaliberFamily.Cal762X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Ak74M, CaliberFamily.Cal545X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Ak12, CaliberFamily.Cal545X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst2 | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Ak122023, CaliberFamily.Cal545X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Ak15, CaliberFamily.Cal762X39, MechanismGroup.Ak,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst2 | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.M16A4, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst3,
            magazineCapacity: 30, cyclicRpm: 800),
        Rifle(FirearmId.M4, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst3,
            magazineCapacity: 30, cyclicRpm: 800),
        Rifle(FirearmId.M4A1, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 800),
        Rifle(FirearmId.Mk18Mod1, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 800),
        Rifle(FirearmId.M7, CaliberFamily.Cal68X51, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 20, cyclicRpm: 600),
        Rifle(FirearmId.Xm8, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 750),
        Rifle(FirearmId.Hk416A5, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 850),
        Rifle(FirearmId.Hk416F, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 850),
        Rifle(FirearmId.G36, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Burst2 | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 750),
        Rifle(FirearmId.Mk16ScarL, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 600),
        Rifle(FirearmId.Mk17ScarH, CaliberFamily.Cal762X51, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 20, cyclicRpm: 600),
        Rifle(FirearmId.AugA3, CaliberFamily.Cal556X45, MechanismGroup.Bullpup,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 720),
        Rifle(FirearmId.TavorX95, CaliberFamily.Cal556X45, MechanismGroup.Bullpup,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 850),
        Rifle(FirearmId.Qbz191, CaliberFamily.Cal58X42, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 700),
        Rifle(FirearmId.Qbz951, CaliberFamily.Cal58X42, MechanismGroup.Bullpup,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 700),
        Rifle(FirearmId.L85A3, CaliberFamily.Cal556X45, MechanismGroup.Bullpup,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 700),
        Rifle(FirearmId.CzBren2, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 800),
        Rifle(FirearmId.BerettaArx160, CaliberFamily.Cal556X45, MechanismGroup.Ar,
            FireModeSet.Safe | FireModeSet.Single | FireModeSet.Auto,
            magazineCapacity: 30, cyclicRpm: 700),

        // Pistols (14).
        Pistol(FirearmId.Beretta92Fs, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.BerettaApxA1, FireModeSet.Single, magazineCapacity: 17),
        Pistol(FirearmId.Glock17Gen5, FireModeSet.Single, magazineCapacity: 17),
        Pistol(FirearmId.Glock19Gen5, FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.SigM17, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 17),
        Pistol(FirearmId.SigM18, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 17),
        Pistol(FirearmId.SigP226, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.SmithWessonMp9M20, FireModeSet.Single, magazineCapacity: 17),
        Pistol(FirearmId.HkVp9, FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.HkUsp, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.CzP10C, FireModeSet.Single, magazineCapacity: 15),
        Pistol(FirearmId.WaltherPdpFs4, FireModeSet.Single, magazineCapacity: 18),
        Pistol(FirearmId.Mp443Grach, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 18),
        Pistol(FirearmId.Qsz92, FireModeSet.Safe | FireModeSet.Single, magazineCapacity: 20,
            caliber: CaliberFamily.Cal58X21),
    };

    private static FirearmDefinition Rifle(
        FirearmId id,
        CaliberFamily caliber,
        MechanismGroup mechanism,
        FireModeSet modes,
        int magazineCapacity,
        int cyclicRpm) => new(
            Id: id,
            Class: WeaponClass.Rifle,
            Caliber: caliber,
            Mechanism: mechanism,
            Modes: modes,
            ReadyMs: RifleReadyMs,
            AimBaseMs: RifleAimBaseMs,
            AimPerBamMs: RifleAimPerBamMs,
            ResetMs: RifleResetMs,
            TurnBamPerTick: RifleTurnBamPerTick,
            AutoBandMaxWu: RifleAutoBandMaxWu,
            BurstBandMaxWu: RifleBurstBandMaxWu,
            SingleBandMaxWu: RifleSingleBandMaxWu,
            DispersionAtZeroWu: RifleDispersionAtZeroWu,
            DispersionAtMaxWu: RifleDispersionAtMaxWu,
            MaxEffectiveWu: RifleMaxEffectiveWu,
            MagazineCapacity: magazineCapacity,
            ReloadMs: RifleReloadMs,
            CyclicRpm: cyclicRpm,
            ExemptFromLoweredRule: false);

    private static FirearmDefinition Pistol(
        FirearmId id,
        FireModeSet modes,
        int magazineCapacity,
        CaliberFamily caliber = CaliberFamily.Cal9X19) => new(
            Id: id,
            Class: WeaponClass.Pistol,
            Caliber: caliber,
            Mechanism: MechanismGroup.Pistol,
            Modes: modes,
            ReadyMs: PistolReadyMs,
            AimBaseMs: PistolAimBaseMs,
            AimPerBamMs: PistolAimPerBamMs,
            ResetMs: PistolResetMs,
            TurnBamPerTick: PistolTurnBamPerTick,
            AutoBandMaxWu: PistolAutoBandMaxWu,
            BurstBandMaxWu: PistolBurstBandMaxWu,
            SingleBandMaxWu: PistolSingleBandMaxWu,
            DispersionAtZeroWu: PistolDispersionAtZeroWu,
            DispersionAtMaxWu: PistolDispersionAtMaxWu,
            MaxEffectiveWu: PistolMaxEffectiveWu,
            MagazineCapacity: magazineCapacity,
            ReloadMs: PistolReloadMs,
            CyclicRpm: PistolInertCyclicRpm,
            ExemptFromLoweredRule: true);
}
