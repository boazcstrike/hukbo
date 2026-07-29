namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The regional corpus a warrior's personal name is drawn from. A region is
/// a source dossier, not a modern province: each member names one body of
/// records from one place and one decade, because the naming research
/// (docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md, section 8 rule 1) forbids
/// joining a Tagalog element to a Visayan or Mindanao element merely because
/// both are now Philippine.
/// </summary>
internal enum WarriorNameRegion
{
    /// <summary>
    /// Central Philippines and northeastern Mindanao as recorded by Pigafetta
    /// in 1521 and by the Legazpi expedition relations in 1565.
    /// </summary>
    VisayanCentral1521,

    /// <summary>
    /// Tondo and neighbouring communities as recorded in the 1589 conspiracy
    /// proceedings, plus Chirino's 1604 Tagalog naming examples.
    /// </summary>
    Tagalog1589,

    /// <summary>
    /// The Mindanao River dossier recorded by Captain Gabriel de Ribera's
    /// expedition in 1579.
    /// </summary>
    MindanaoRiver1579,
}

/// <summary>
/// What a source actually presents a recorded form as. The distinction is the
/// research's own (section 2): a form the sources attach to one particular
/// person is weaker evidence for a reusable pool than a form an author offers
/// as an example of how people were named.
/// </summary>
internal enum WarriorNameKind
{
    /// <summary>
    /// A source names a particular historical person carrying this form. Its
    /// procedural reuse for a generated warrior is a
    /// <see cref="VisualEvidenceTier.ProvisionalReconstruction"/> in itself,
    /// however securely the bearer is attested.
    /// </summary>
    RecordedBearer,

    /// <summary>
    /// A source explicitly presents this form as an example of how people were
    /// named, rather than as one person's identity. This is the stronger
    /// evidence for a reusable pool.
    /// </summary>
    NamingExample,
}

/// <summary>
/// What the source records about the gender of the example or bearer. This
/// records the evidence only. It never claims a name was linguistically
/// restricted to that gender: the research's data model (section 6) keeps
/// <c>recordedGender</c> and <c>genderRestriction</c> apart, and no source
/// consulted establishes a restriction for any form in this catalog.
/// </summary>
internal enum WarriorNameGenderEvidence
{
    /// <summary>The source records a man carrying or exemplifying the form.</summary>
    RecordedMan,

    /// <summary>The source records a woman carrying or exemplifying the form.</summary>
    RecordedWoman,

    /// <summary>The source does not say.</summary>
    Unspecified,
}

/// <summary>
/// One personal name a generated warrior may carry, with the evidence behind
/// it kept alongside it rather than discarded at the point of display.
/// </summary>
/// <remarks>
/// Presentation-only, exactly like <see cref="VisualCatalogEntry"/> and
/// <c>PawnAppearance</c>: nothing here is constructed inside, passed into, or
/// read by <c>Hukbo.Core</c>, nothing here reaches the state hash or the event
/// hash, and a warrior's name can never influence targeting, damage, or the
/// battle outcome. The fields mirror the research's own
/// "research-ready metadata" list (section 10), minus the fields that only a
/// later archival pass can fill.
/// </remarks>
/// <param name="Id">
/// Stable machine key, <c>name.&lt;region&gt;.&lt;form&gt;</c>, pinned forever
/// once shipped in the spirit of <c>LogEvents</c> identifiers.
/// </param>
/// <param name="Index">
/// Stable ordinal within the region's pool, pinned forever once shipped
/// because the selection stream reduces modulo the pool count.
/// </param>
/// <param name="DisplayForm">The spelling shown to a spectator.</param>
/// <param name="RecordedForm">
/// The spelling the opened translation prints, including the variants it
/// prints for the same person. Kept separate from
/// <paramref name="DisplayForm"/> because modernizing a colonial spelling is
/// an editorial act, not a mechanical cleanup (research section 3.1).
/// </param>
/// <param name="Region">The corpus this form belongs to.</param>
/// <param name="EvidenceTier">
/// How far the evidence behind the form itself reaches, at its own source's
/// date. Clearance for procedural reuse is a separate matter and is recorded
/// in <paramref name="ReuseNote"/>.
/// </param>
/// <param name="Kind">What the source presents the form as.</param>
/// <param name="RecordedGender">What the source says about gender.</param>
/// <param name="SourceCitation">The document and volume the form comes from.</param>
/// <param name="ReuseNote">
/// The spectator-facing note explaining what is and is not being claimed by
/// giving this name to a generated warrior.
/// </param>
internal sealed record WarriorNameEntry(
    string Id,
    int Index,
    string DisplayForm,
    string RecordedForm,
    WarriorNameRegion Region,
    VisualEvidenceTier EvidenceTier,
    WarriorNameKind Kind,
    WarriorNameGenderEvidence RecordedGender,
    string SourceCitation,
    string ReuseNote)
{
    public string Id { get; init; } = ValidateId(Id);

    public int Index { get; init; } = ValidateIndex(Index);

    public string DisplayForm { get; init; } = ValidateText(DisplayForm, nameof(DisplayForm));

    public string RecordedForm { get; init; } = ValidateText(RecordedForm, nameof(RecordedForm));

    public WarriorNameRegion Region { get; init; } = ValidateRegion(Region);

    public VisualEvidenceTier EvidenceTier { get; init; } = ValidateEvidenceTier(EvidenceTier);

    public WarriorNameKind Kind { get; init; } = ValidateKind(Kind);

    public WarriorNameGenderEvidence RecordedGender { get; init; } =
        ValidateGenderEvidence(RecordedGender);

    public string SourceCitation { get; init; } =
        ValidateText(SourceCitation, nameof(SourceCitation));

    public string ReuseNote { get; init; } = ValidateText(ReuseNote, nameof(ReuseNote));

    private static string ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var segments = id.Split('.');
        if (segments.Length != 3 || segments[0] != "name")
        {
            throw new ArgumentException(
                $"'{id}' does not match the warrior-name identifier grammar " +
                "'name.<region>.<form>'.",
                nameof(id));
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || !char.IsAsciiLetterLower(segment[0]))
            {
                throw new ArgumentException(
                    $"'{id}' has a segment that is empty or does not start " +
                    "with a lower-case ASCII letter.",
                    nameof(id));
            }
        }

        return id;
    }

    private static int ValidateIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index;
    }

    private static string ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static WarriorNameRegion ValidateRegion(WarriorNameRegion region)
    {
        if (!Enum.IsDefined(region))
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                "Region must be a defined WarriorNameRegion member.");
        }

        return region;
    }

    private static VisualEvidenceTier ValidateEvidenceTier(VisualEvidenceTier tier)
    {
        if (!Enum.IsDefined(tier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tier),
                tier,
                "Evidence tier must be a defined VisualEvidenceTier member.");
        }

        return tier;
    }

    private static WarriorNameKind ValidateKind(WarriorNameKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Kind must be a defined WarriorNameKind member.");
        }

        return kind;
    }

    private static WarriorNameGenderEvidence ValidateGenderEvidence(
        WarriorNameGenderEvidence gender)
    {
        if (!Enum.IsDefined(gender))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gender),
                gender,
                "Recorded gender must be a defined WarriorNameGenderEvidence member.");
        }

        return gender;
    }
}

/// <summary>
/// The three regional pools of sixteenth-century personal names a generated
/// warrior can draw from, and the two selection streams that draw from them.
/// </summary>
/// <remarks>
/// <para>
/// Every form here is printed by one of the opened translations listed in
/// docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md section 12. Nothing here is
/// invented, compounded from dictionary roots, or carried over from a modern
/// name list. The catalog deliberately implements the research's Approach B
/// core only — a region-scoped ledger of recorded forms — and none of its
/// optional layers:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>No titles.</b> <c>Datu</c>, <c>Raja</c>, <c>Gat</c>, <c>Lakan</c>, and
/// <c>Dayang</c> encode standing and are never prefixed to a generated
/// warrior (research section 8 rule 2, and its warning against assigning
/// elite titles to ordinary people for flavour).
/// </description></item>
/// <item><description>
/// <b>No parenthood forms.</b> <c>Amanicalao</c>, <c>Amarlangagui</c>, and
/// <c>Amaghicon</c> are recorded, but a parenthood name refers to a specific
/// firstborn and may only be generated when that child exists (rule 3). A
/// battle roster has no family tree, so these forms are excluded from the
/// pools and appear only in <see cref="ParenthoodResearchNote"/>.
/// </description></item>
/// <item><description>
/// <b>No reputation or friendship names.</b> Colin's 1663 <c>Pamagat</c> and
/// <c>Casolasi</c> material is later comparison only and is excluded from a
/// historically labelled 1500s roster (rule 4).
/// </description></item>
/// <item><description>
/// <b>No Christian-plus-local forms.</b> Those belong to a dated contact
/// context (rule 5), and a scenario carries no date yet.
/// </description></item>
/// <item><description>
/// <b>No famous historical bearers.</b> Lapulapu, Humabon, Zula, Colambu,
/// Tupas, Sikatuna, Soliman, Magat Salamat, and Limasancay stay
/// reference-only, so a roster never looks like a bag of copies of famous
/// figures (rule 7, research section 3.4). <c>WarriorNameCatalogTests</c>
/// pins that exclusion.
/// </description></item>
/// </list>
/// <para>
/// What remains is still an editorial choice rather than a neutral fact: a
/// pool built from recorded people is overwhelmingly a pool of chiefs,
/// envoys, and defendants, because those are the people colonial records name
/// (research section 3.2), and the surviving record holds almost no women's
/// local birth names at all (section 3.3). Every entry therefore carries its
/// own <see cref="WarriorNameEntry.ReuseNote"/>, and the inspector shows it,
/// so the imbalance stays visible instead of being quietly smoothed over.
/// </para>
/// </remarks>
internal static class WarriorNameCatalog
{
    private const string Pigafetta1521 =
        "Pigafetta, First Voyage Around the World, 1521 " +
        "(Blair and Robertson, Volume 33)";

    private const string Legazpi1565 =
        "Legazpi expedition relations, 1565 " +
        "(Blair and Robertson, Volume 2)";

    private const string Tondo1589 =
        "Conspiracy Against the Spaniards, 1589 " +
        "(Blair and Robertson, Volume 7)";

    private const string Chirino1604 =
        "Chirino, Relacion de las Islas Filipinas, 1604, chapter LXXX";

    private const string Mindanao1579 =
        "Records of the Mindanao expedition, 1579 " +
        "(Blair and Robertson, Volume 4)";

    /// <summary>
    /// The note every recorded-bearer form carries: the source names a person,
    /// and lending that person's name to a generated warrior is the
    /// reconstruction, not the attestation.
    /// </summary>
    private const string BearerReuse =
        "Recorded bearer; procedural reuse for a generated warrior is a " +
        "Provisional reconstruction, not a claim that this person fought here.";

    /// <summary>
    /// The note every naming-example form carries: the source offers the form
    /// as an example of how people were named, which is what makes it
    /// reusable, but the example postdates the 1500s.
    /// </summary>
    private const string ExampleReuse =
        "Naming example rather than one person's identity, which is what " +
        "makes it reusable; the example is from 1604, so its use in a 1500s " +
        "roster is a Provisional reconstruction.";

    /// <summary>
    /// The standalone research note the inspector always appends for a named
    /// warrior, independent of which form resolved. It records the two
    /// structures this catalog deliberately does not implement, so a spectator
    /// can tell an absence of evidence from an absence of effort.
    /// </summary>
    internal const string ParenthoodResearchNote =
        "Research note: the sources also record parenthood names — Ama ni " +
        "[firstborn], Ina ni [firstborn], and the fused Tondo forms " +
        "Amanicalao, Amarlangagui, and Amaghicon — and honorific titles such " +
        "as Datu, Raja, Gat, Lakan, and Dayang. Neither is generated here. A " +
        "parenthood name refers to a specific firstborn a battle roster does " +
        "not have, and a title encodes standing that an ordinary warrior did " +
        "not carry.";

    /// <summary>
    /// The standalone note recording the documentary gap in women's names, so
    /// the imbalance in this catalog reads as a property of the surviving
    /// record rather than a design decision.
    /// </summary>
    internal const string WomensNamesResearchNote =
        "Research note: the opened sixteenth-century sources name chiefs, " +
        "envoys, and defendants, and record no local birth name for any " +
        "woman. Iloguin, from Chirino in 1604, is the earliest explicit " +
        "women's naming example available, and the sources give no basis for " +
        "generating more by appending a suffix to men's names. A recorded " +
        "gender above describes the bearer or example the source names; no " +
        "source consulted establishes that any of these forms was restricted " +
        "to one gender.";

    /// <summary>
    /// Central Philippines and northeastern Mindanao, 1521 and 1565.
    /// Twenty forms: Pigafetta's Cebu, Cinghapola, Mandaui, Lalan, Lalutan,
    /// and Quipit names, plus the Legazpi expedition's Cebu, Bohol, and Leyte
    /// names. The famous bearers of these same dossiers — Humabon, Lapulapu,
    /// Zula, Colambu, Tupas, Sikatuna — are excluded.
    /// </summary>
    internal static IReadOnlyList<WarriorNameEntry> VisayanCentral1521 { get; } =
    [
        new("name.visayan.cadaio", 0, "Cadaio", "Cadaio",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A brother of the ruler at Cebu. " + BearerReuse),
        new("name.visayan.simiut", 1, "Simiut", "Simiut",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "One of Cebu's principal men. " + BearerReuse),
        new("name.visayan.sibuaia", 2, "Sibuaia", "Sibuaia",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "One of Cebu's principal men. " + BearerReuse),
        new("name.visayan.sisacai", 3, "Sisacai", "Sisacai",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "One of Cebu's principal men. " + BearerReuse),
        new("name.visayan.maghalibe", 4, "Maghalibe", "Maghalibe",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "One of Cebu's principal men. " + BearerReuse),
        new("name.visayan.cilaton", 5, "Cilaton", "Cilaton",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A chief of Cinghapola. The opening Ci is not assumed to be a " +
            "separable honorific; the recorded spelling is kept whole. " +
            BearerReuse),
        new("name.visayan.ciguibucan", 6, "Ciguibucan", "Ciguibucan",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A chief of Cinghapola, recorded spelling only. " + BearerReuse),
        new("name.visayan.cimaningha", 7, "Cimaningha", "Cimaningha",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A chief of Cinghapola, recorded spelling only. " + BearerReuse),
        new("name.visayan.cimatichat", 8, "Cimatichat", "Cimatichat",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A chief of Cinghapola, recorded spelling only. " + BearerReuse),
        new("name.visayan.cicanbul", 9, "Cicanbul", "Cicanbul",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "A chief of Cinghapola, recorded spelling only. " + BearerReuse),
        new("name.visayan.apanoaan", 10, "Apanoaan", "Apanoaan / Apanoan",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "The chief of Mandaui; a second Apanoan is reported at Puzzo, so " +
            "the form may be a repeated name, a title, or scribal variation. " +
            BearerReuse),
        new("name.visayan.theteu", 11, "Theteu", "Theteu",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "The chief of Lalan, recorded spelling only. " + BearerReuse),
        new("name.visayan.tapan", 12, "Tapan", "Tapan",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "The chief of Lalutan, recorded spelling only. " + BearerReuse),
        new("name.visayan.calanao", 13, "Calanao", "Calanao",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "The ruler at Quipit, recorded as Raia Calanao; the title stays " +
            "with the ruler and is never generated here. " + BearerReuse),
        new("name.visayan.siaui", 14, "Siaui", "Siaui / Siain / Siani / Siagu",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Pigafetta1521,
            "The second of two rulers met at Mazaua; manuscripts and editors " +
            "give four spellings for the one person. " + BearerReuse),
        new("name.visayan.simaquio", 15, "Simaquio", "Simaquio",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Legazpi1565,
            "A Cebu chief, husband, and father. " + BearerReuse),
        new("name.visayan.canatuan", 16, "Canatuan", "Canatuan",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Legazpi1565,
            "A chief at Cabalian, Leyte. " + BearerReuse),
        new("name.visayan.malate", 17, "Malate", "Malate",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Legazpi1565,
            "The principal chief at Cabalian, Leyte. Not to be confused with " +
            "the later Manila place name. " + BearerReuse),
        new("name.visayan.saripara", 18, "Saripara", "Saripara / Sarriparra",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Legazpi1565,
            "Named as an earlier ruler at Cebu; the source form and the " +
            "identification both vary. " + BearerReuse),
        new("name.visayan.sigala", 19, "Sigala", "Çigala / Sigala",
            WarriorNameRegion.VisayanCentral1521,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Legazpi1565,
            "A chief at Bohol. The recorded and normalized spellings are " +
            "kept side by side. " + BearerReuse),
    ];

    /// <summary>
    /// Tondo and neighbouring communities, 1589, plus Chirino's 1604 Tagalog
    /// naming examples. Thirteen locally recorded second elements from the
    /// conspiracy proceedings and seven of Chirino's worked examples. The
    /// Christian first names those elements are printed with (Agustin,
    /// Phelipe, Joan) belong to a dated contact context and are not generated;
    /// Magat Salamat is excluded as a famous bearer, Sumaelob as a Cuyo form
    /// that the source itself does not place in Tagalog territory, and the
    /// three parenthood forms for the reason given in
    /// <see cref="ParenthoodResearchNote"/>.
    /// </summary>
    internal static IReadOnlyList<WarriorNameEntry> Tagalog1589 { get; } =
    [
        new("name.tagalog.panga", 0, "Panga", "Panga / Pangan",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "Recorded for the governor of Tondo; the spelling varies within " +
            "the one record. " + BearerReuse),
        new("name.tagalog.manuguit", 1, "Manuguit", "Manuguit",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.salalila", 2, "Salalila", "Salalila",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.banal", 3, "Banal", "Banal",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.surabao", 4, "Surabao", "Surabao",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.bassi", 5, "Bassi", "Bassi",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.tuambacan", 6, "Tuambacan",
            "Tuambaçan / Tuambacan / Tuam Basar",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "Three spellings appear for the one man, so the segmentation is " +
            "genuinely uncertain. " + BearerReuse),
        new("name.tagalog.acta", 7, "Acta", "Acta",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.pitongatan", 8, "Pitongatan", "Pitongatan",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A single recorded form for a named defendant. " + BearerReuse),
        new("name.tagalog.bolingui", 9, "Bolingui", "Bolingui",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "Recorded for a chief of Pandaca; other editions may print the " +
            "form differently. " + BearerReuse),
        new("name.tagalog.calao", 10, "Calao", "Calao",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "Recorded as a son in the proceedings; his father's fused " +
            "parenthood form is not generated. " + BearerReuse),
        new("name.tagalog.capolo", 11, "Capolo", "Capolo",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.salonga", 12, "Salonga", "Salonga",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.Documented,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Tondo1589,
            "A locally recorded element, printed with a Christian first name " +
            "the roster does not reproduce. " + BearerReuse),
        new("name.tagalog.maliuag", 13, "Maliuag", "Maliuag",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.Unspecified,
            Chirino1604,
            "Chirino's example of a name given for a circumstance of birth, " +
            "\"difficult\"; he does not say whether the child was a boy or a " +
            "girl. " + ExampleReuse),
        new("name.tagalog.malacas", 14, "Malacas", "Malacas",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.Unspecified,
            Chirino1604,
            "Chirino's example of a name given for a hoped-for quality, " +
            "\"strong\". " + ExampleReuse),
        new("name.tagalog.daan", 15, "Daan", "Daan",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.Unspecified,
            Chirino1604,
            "Chirino's example of an ordinary word used as a name, " +
            "\"road\". " + ExampleReuse),
        new("name.tagalog.babui", 16, "Babui", "Babui",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.Unspecified,
            Chirino1604,
            "Chirino's example of an ordinary word used as a name, " +
            "\"pig\". " + ExampleReuse),
        new("name.tagalog.manug", 17, "Manug", "Manug",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.Unspecified,
            Chirino1604,
            "Chirino's example of an ordinary word used as a name, " +
            "\"fowl\". " + ExampleReuse),
        new("name.tagalog.ilog", 18, "Ilog", "Ilog",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.RecordedMan,
            Chirino1604,
            "From ilog, \"river\"; the man's side of Chirino's one worked " +
            "male and female pair. " + ExampleReuse),
        new("name.tagalog.iloguin", 19, "Iloguin", "Iloguin",
            WarriorNameRegion.Tagalog1589,
            VisualEvidenceTier.ProvisionalReconstruction,
            WarriorNameKind.NamingExample,
            WarriorNameGenderEvidence.RecordedWoman,
            Chirino1604,
            "The woman's side of Chirino's one worked pair, and the earliest " +
            "explicit women's naming example in the opened sources. The " +
            "suffix is not generalized to any other form. " + ExampleReuse),
    ];

    /// <summary>
    /// The Mindanao River dossier, 1579. Ten forms from the notarial record of
    /// Ribera's expedition. Limasancay, the ruler the dossier is about, is
    /// excluded as its prominent bearer, and the two forms the record prints
    /// with a leading Dato are excluded because the title is not a name root.
    /// The languages actually represented along the river need specialist
    /// review, which is why these forms are kept in their own region and never
    /// mixed into a Visayan or Tagalog pool.
    /// </summary>
    internal static IReadOnlyList<WarriorNameEntry> MindanaoRiver1579 { get; } =
    [
        new("name.mindanao.asututan", 0, "Asututan", "Asututan",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "Named as a deceased ruler's father. " + BearerReuse),
        new("name.mindanao.umapas", 1, "Umapas", "Umapas",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "An envoy or intermediary in the proceedings. " + BearerReuse),
        new("name.mindanao.sicuyrey", 2, "Sicuyrey", "Sicuyrey",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A chief and cousin of the river's ruler. " + BearerReuse),
        new("name.mindanao.siproa", 3, "Siproa", "Siproa",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "The ruler's father-in-law. " + BearerReuse),
        new("name.mindanao.batala", 4, "Batala", "Batala",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A chief and master of Sidurman. No relation to Tagalog " +
            "religious vocabulary is inferred from the spelling. " +
            BearerReuse),
        new("name.mindanao.sidurman", 5, "Sidurman", "Sidurman",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A dependent of the chief above. " + BearerReuse),
        new("name.mindanao.atagayta", 6, "Atagayta", "Atagayta",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A dependent of the river's ruler. " + BearerReuse),
        new("name.mindanao.laquidan", 7, "Laquidan", "Laquidan / Laquian",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "The interpreter named in the proceedings; the spelling varies. " +
            BearerReuse),
        new("name.mindanao.sihauil", 8, "Sihauil", "Sihauil",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A man from a hostile chief's town. " + BearerReuse),
        new("name.mindanao.simangary", 9, "Simangary", "Simangary",
            WarriorNameRegion.MindanaoRiver1579,
            VisualEvidenceTier.DocumentedFormUncertain,
            WarriorNameKind.RecordedBearer,
            WarriorNameGenderEvidence.RecordedMan,
            Mindanao1579,
            "A messenger named in the proceedings. " + BearerReuse),
    ];

    /// <summary>
    /// The regions a faction can be assigned, in the fixed order the
    /// assignment stream reduces modulo. Pinned: reordering this list
    /// reshuffles every region a shipped seed already assigned.
    /// </summary>
    internal static IReadOnlyList<WarriorNameRegion> RegionAssignmentTable { get; } =
    [
        WarriorNameRegion.VisayanCentral1521,
        WarriorNameRegion.Tagalog1589,
        WarriorNameRegion.MindanaoRiver1579,
    ];

    /// <summary>Every entry in every region, in region and index order.</summary>
    internal static IReadOnlyList<WarriorNameEntry> All { get; } =
    [
        .. VisayanCentral1521,
        .. Tagalog1589,
        .. MindanaoRiver1579,
    ];

    /// <summary>The pool for one region. Never empty, never null.</summary>
    internal static IReadOnlyList<WarriorNameEntry> GetPool(WarriorNameRegion region) =>
        region switch
        {
            WarriorNameRegion.VisayanCentral1521 => VisayanCentral1521,
            WarriorNameRegion.Tagalog1589 => Tagalog1589,
            WarriorNameRegion.MindanaoRiver1579 => MindanaoRiver1579,
            _ => throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                null),
        };

    /// <summary>
    /// The plain-English label for a region, as the inspector shows it. Names
    /// the source dossier's place and date rather than a modern province, so a
    /// spectator reads it as a body of records rather than an ethnic claim.
    /// </summary>
    internal static string GetRegionLabel(WarriorNameRegion region) =>
        region switch
        {
            WarriorNameRegion.VisayanCentral1521 =>
                "Central Philippines, 1521 and 1565 records",
            WarriorNameRegion.Tagalog1589 =>
                "Tondo and Tagalog records, 1589 and 1604",
            WarriorNameRegion.MindanaoRiver1579 =>
                "Mindanao River records, 1579",
            _ => throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                null),
        };

    /// <summary>
    /// The whole faction's name region, derived from
    /// <paramref name="scenarioSeed"/> and <paramref name="factionId"/> so
    /// every warrior under one banner shares one regional grammar and the same
    /// seed always reproduces the same assignment. Mixes
    /// <c>scenarioSeed XOR PresentationSalts.WarriorNameRegionSalt XOR
    /// (ulong)factionId</c> through the SplitMix64 finalizer and reduces
    /// modulo <see cref="RegionAssignmentTable"/>'s count. Two factions in one
    /// match may land on the same region; nothing excludes it, exactly as the
    /// sibling appearance-block assignment allows.
    /// </summary>
    internal static WarriorNameRegion SelectRegion(ulong scenarioSeed, int factionId)
    {
        var mixed = Mix(scenarioSeed ^
            PresentationSalts.WarriorNameRegionSalt ^
            unchecked((ulong)factionId));
        var index = (int)(mixed % (ulong)RegionAssignmentTable.Count);
        return RegionAssignmentTable[index];
    }

    /// <summary>
    /// One warrior's name within an already assigned region, derived from
    /// <paramref name="entityId"/> alone so it is stable across frames, across
    /// a pause and resume, and across a replay of the same seed. Mixes
    /// <c>entityId XOR PresentationSalts.WarriorNameSelectionSalt</c> through
    /// the SplitMix64 finalizer — its own stream, never correlated with the
    /// appearance, weapon-tint, or shield-skin draws for the same warrior —
    /// and reduces modulo the region pool's count. Pools are smaller than a
    /// roster, so repeated names within one faction are expected; the entity
    /// identifier shown beside a name is what keeps two warriors apart.
    /// </summary>
    internal static WarriorNameEntry SelectName(ulong entityId, WarriorNameRegion region)
    {
        var pool = GetPool(region);
        var mixed = Mix(entityId ^ PresentationSalts.WarriorNameSelectionSalt);
        return pool[(int)(mixed % (ulong)pool.Count)];
    }

    // The SplitMix64 finalizer, duplicated here per this codebase's own
    // convention of one local mixer per presentation-salt consumer
    // (PawnAppearanceFactory.Mix, WeaponVisualCatalog.Mix,
    // AppearancePresets.Mix all carry their own copy).
    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
