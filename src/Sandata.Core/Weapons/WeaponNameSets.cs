namespace Sandata.Core.Weapons;

/// <summary>
/// Selects which of <see cref="WeaponNameSets"/>'s two parallel string tables
/// supplies a firearm's shipped display name. Design section 9's "single
/// configurable field" answer to the trademark question research section 3
/// raises: real names stay in the data and the documentation, and the shipped
/// display set is one field, not a second data file or a build variant.
/// </summary>
/// <remarks>
/// Not part of <see cref="FirearmDefinition"/> and not folded per-row into
/// <see cref="FirearmRuleset.ContentHash"/> — it is a single, game-wide
/// presentation choice, not a simulation input. A display name never reaches
/// the state hash or a replay in the first place, so this identifier has no
/// determinism obligation of its own.
/// </remarks>
public enum WeaponNameSetId
{
    /// <summary>
    /// Real manufacturer and commercial names — "AK-47", "Glock 17 Gen5",
    /// "Heckler &amp; Koch VP9" — matching the names in <see cref="FirearmId"/>'s
    /// own XML documentation. Carries the trademark and trade-dress risk
    /// research section 3 records for Glock, Heckler &amp; Koch, Beretta, SIG
    /// Sauer, FN Herstal, Steyr, and IWI.
    /// </summary>
    Manufacturer = 0,

    /// <summary>
    /// Safe aliases with the flagged brand words removed or replaced by the
    /// weapon's own government or model-number designation where one exists
    /// — the "M4", "Mk 18", "L85", "QBZ-191", "M7", "MP-443" pattern research
    /// section 3 calls "materially safer". This is the set a shipped build
    /// selects by default.
    /// </summary>
    Generic = 1,
}

/// <summary>
/// The two parallel display-name tables <see cref="WeaponNameSetId"/> selects
/// between, one entry per <see cref="FirearmId"/> in dense order. Neither
/// table is a simulation input: both exist purely so the client can print a
/// name, and switching <see cref="WeaponNameSetId"/> changes what a player
/// sees without touching <see cref="FirearmCatalog"/>, the ruleset, or any
/// hash.
/// </summary>
/// <remarks>
/// Research section 8, open question 4, records "real weapon names versus
/// generic aliases in shipped display strings" as unresolved by the user, and
/// research section 3 does not carry a literal per-weapon alias table — only
/// the recommendation to keep this switch as one field. The <see cref="Generic"/>
/// strings below are therefore this task's own construction, built by
/// stripping the seven flagged brand words research section 3 names
/// (Glock, Heckler &amp; Koch, Beretta, SIG Sauer, FN Herstal, Steyr, IWI) and
/// substituting each weapon's own government or model-number designation
/// where the roster already carries one, or a plain descriptive noun phrase
/// where it does not. Revisit both tables once the open question above is
/// answered; nothing here should be read as a settled trademark decision.
/// </remarks>
public static class WeaponNameSets
{
    /// <summary>
    /// Real manufacturer and commercial names, indexed by <see cref="FirearmId"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> Manufacturer = new[]
    {
        "AK-47",
        "AKM",
        "AK-74M",
        "AK-12 (2018/2021)",
        "AK-12 (2023)",
        "AK-15",
        "M16A4",
        "M4",
        "M4A1",
        "Mk 18 Mod 1",
        "M7",
        "XM8",
        "HK416 A5",
        "HK416F",
        "G36",
        "FN SCAR-L (Mk 16)",
        "FN SCAR-H (Mk 17)",
        "Steyr AUG A3",
        "IWI Tavor X95",
        "QBZ-191",
        "QBZ-95-1",
        "L85A3",
        "CZ BREN 2",
        "Beretta ARX160",
        "Beretta 92FS / M9",
        "Beretta APX A1",
        "Glock 17 Gen5",
        "Glock 19 Gen5",
        "SIG Sauer M17",
        "SIG Sauer M18",
        "SIG Sauer P226",
        "Smith & Wesson M&P9 M2.0",
        "Heckler & Koch VP9",
        "Heckler & Koch USP",
        "CZ P-10 C",
        "Walther PDP (Full-Size, 4-inch)",
        "MP-443 Grach",
        "QSZ-92",
    };

    /// <summary>
    /// Safe aliases, indexed by <see cref="FirearmId"/>. See the remarks on
    /// <see cref="WeaponNameSets"/> for how each entry was derived.
    /// </summary>
    public static readonly IReadOnlyList<string> Generic = new[]
    {
        "AK-47",
        "AKM",
        "AK-74M",
        "AK-12 (2018/2021)",
        "AK-12 (2023)",
        "AK-15",
        "M16A4",
        "M4",
        "M4A1",
        "Mk 18 Mod 1",
        "M7",
        "XM8",
        "5.56 Modular Carbine A5",
        "5.56 Modular Carbine F",
        "G36",
        "Mk 16",
        "Mk 17",
        "Bullpup Service Rifle A3",
        "Bullpup Carbine X95",
        "QBZ-191",
        "QBZ-95-1",
        "L85A3",
        "BREN 2",
        "ARX160 Service Rifle",
        "M9 Service Pistol",
        "APX A1 Service Pistol",
        "9mm Compact Service Pistol 17",
        "9mm Compact Service Pistol 19",
        "M17",
        "M18",
        "P226 Service Pistol",
        "M&P9 M2.0",
        "VP9 Service Pistol",
        "USP Service Pistol",
        "P-10 C",
        "PDP Full-Size 4-inch",
        "MP-443",
        "QSZ-92",
    };

    /// <summary>
    /// Looks up <paramref name="id"/>'s display name in the table
    /// <paramref name="set"/> selects.
    /// </summary>
    public static string GetName(FirearmId id, WeaponNameSetId set)
    {
        var table = set switch
        {
            WeaponNameSetId.Manufacturer => Manufacturer,
            WeaponNameSetId.Generic => Generic,
            _ => throw new ArgumentOutOfRangeException(nameof(set), set, "Unknown weapon name set."),
        };

        return table[(int)id];
    }
}
