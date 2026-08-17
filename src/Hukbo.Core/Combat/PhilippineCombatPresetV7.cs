using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Combat;

/// <summary>
/// Version 7 pre-colonial Philippine combat preset: version 5's target
/// weights, weapon attributes, shield multipliers, per-rank fighter levels,
/// and clash profile restated without modification, with armor made
/// authoritative through a per-(<see cref="ArmorId"/>, <see cref="BodyPart"/>)
/// damage-mitigation table and a roster that fields five armor baselines
/// instead of repeating <see cref="ArmorId.LightOrganic"/> on every row.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph, matching the
/// convention every earlier preset version follows. Version 7 is a frozen
/// snapshot in the same sense versions 1 through 6 are: version 5's values
/// are restated here rather than referenced, so retuning version 7 can never
/// reach back and move a hash version 5's replays depend on.
/// <para>
/// This preset descends from version 5, not version 6. Version 5 is the
/// preset the client ships and the only one that declares ranged weapons and
/// shielded roster rows; descending from the higher-numbered version 6 would
/// have silently dropped both from the player's build. Versions 1 through 6
/// stay registered and unmodified so their replays remain reproducible.
/// </para>
/// <para>
/// The roster is a mechanical armor expansion of version 5's nine base
/// (<see cref="WeaponId"/>, <see cref="ShieldId"/>, <see cref="RankId"/>)
/// loadouts, one for each of the five non-legacy <see cref="ArmorId"/>
/// baselines this preset fields — see <see cref="BaseLoadouts"/> and
/// <see cref="FieldedArmors"/>. That yields forty-five roster entries and
/// makes the "this preset never fields <see cref="ArmorId.LightOrganic"/>"
/// invariant structural rather than a fact a reviewer must re-check by eye.
/// </para>
/// <para>
/// Every armor-mitigation value in <see cref="BuildArmorMitigation"/> is a
/// PROVISIONAL gameplay-tuning reconstruction under CLAUDE.md section 7,
/// never a historical measurement: no source in the period states what any
/// armor stopped. See the armor identity design document and
/// docs/research/HISTORICAL_1500s_ARMOR.md for evidence context.
/// </para>
/// </remarks>
public static class PhilippineCombatPresetV7
{
    public const int Version = 1;

    /// <summary>
    /// Version 5's nine base (<see cref="WeaponId"/>, <see cref="ShieldId"/>,
    /// <see cref="RankId"/>) loadouts, restated verbatim from
    /// <see cref="PhilippineCombatPresetV5"/>'s roster and order. Each is
    /// crossed with every entry of <see cref="FieldedArmors"/> to build this
    /// preset's own roster.
    /// </summary>
    private static readonly (WeaponId Weapon, ShieldId Shield, RankId Rank)[] BaseLoadouts =
    [
        (WeaponId.Kampilan, ShieldId.None, RankId.Datu),
        (WeaponId.Wasay, ShieldId.None, RankId.Maharlika),
        (WeaponId.Kalis, ShieldId.None, RankId.Timawa),
        (WeaponId.Itak, ShieldId.None, RankId.AlipingNamamahay),
        (WeaponId.Bangkaw, ShieldId.None, RankId.Timawa),
        (WeaponId.Busog, ShieldId.None, RankId.Timawa),
        (WeaponId.Arquebus, ShieldId.None, RankId.Timawa),
        (WeaponId.Kalis, ShieldId.TallHardwood, RankId.Timawa),
        (WeaponId.Itak, ShieldId.TallHardwood, RankId.AlipingNamamahay),
    ];

    /// <summary>
    /// The five non-legacy <see cref="ArmorId"/> baselines this preset
    /// fields, in ascending <see cref="ArmorId"/> order.
    /// <see cref="ArmorId.LightOrganic"/> is deliberately excluded — see the
    /// class remarks and <see cref="ArmorId"/>'s own documentation.
    /// </summary>
    private static readonly ArmorId[] FieldedArmors =
    [
        ArmorId.None,
        ArmorId.QuiltedCotton,
        ArmorId.HideCorselet,
        ArmorId.RigidCuirass,
        ArmorId.ImportedIron,
    ];

    public static CombatRuleset Rules { get; } = Build();

    private static CombatRuleset Build()
    {
        var general = new TargetWeightProfile(
            [
                (BodyPart.WeaponArm, 10),
                (BodyPart.ShieldArm, 8),
                (BodyPart.Shoulder, 9),
                (BodyPart.Head, 9),
                (BodyPart.Neck, 9),
                (BodyPart.Face, 8),
                (BodyPart.Chest, 7),
                (BodyPart.Abdomen, 7),
                (BodyPart.Thigh, 8),
                (BodyPart.Knee, 7),
                (BodyPart.Shin, 7),
                (BodyPart.Hands, 8),
                (BodyPart.Feet, 2),
            ]);

        // Target weights, per weapon. Restated verbatim from
        // PhilippineCombatPresetV5.
        var weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>
        {
            // Kampilan — Great Blade. A long single-edged blade aimed at the
            // head, neck, and shoulder line.
            [WeaponId.Kampilan] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Head] = 10,
                    [BodyPart.Neck] = 10,
                    [BodyPart.Shoulder] = 9,
                    [BodyPart.WeaponArm] = 8,
                    [BodyPart.ShieldArm] = 8,
                    [BodyPart.Chest] = 8,
                }),

            // Wasay — War Axe. A hafted, broad-headed axe favoring the
            // shoulder and the limb that carries the blow.
            [WeaponId.Wasay] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Shoulder] = 10,
                    [BodyPart.Head] = 9,
                    [BodyPart.WeaponArm] = 9,
                    [BodyPart.ShieldArm] = 9,
                }),

            // Kalis — Wave Blade. A thrusting-capable blade favoring the
            // torso.
            [WeaponId.Kalis] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Abdomen] = 10,
                    [BodyPart.Chest] = 9,
                    [BodyPart.Neck] = 8,
                }),

            // Itak — Field Blade. A utility blade favoring the limbs and the
            // hands that carry a shield or a weapon.
            [WeaponId.Itak] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.WeaponArm] = 10,
                    [BodyPart.ShieldArm] = 10,
                    [BodyPart.Hands] = 9,
                    [BodyPart.Neck] = 8,
                    [BodyPart.Face] = 8,
                }),

            // Bangkaw — Thrown Spear. A thrown weapon aimed at the largest
            // available target, the torso, rather than a precise line.
            [WeaponId.Bangkaw] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 9,
                    [BodyPart.Shoulder] = 7,
                }),

            // Busog — Bow. A loosed arrow aimed center-mass, with a shallow
            // preference for the head at the archer's most careful shots.
            [WeaponId.Busog] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 8,
                    [BodyPart.Head] = 7,
                }),

            // Arquebus — Matchlock. A slow, heavy, imprecise shot aimed
            // broadly at the torso; the least discriminating of the seven.
            [WeaponId.Arquebus] = TargetWeightProfiles.FromOverrides(
                general,
                new Dictionary<BodyPart, int>
                {
                    [BodyPart.Chest] = 10,
                    [BodyPart.Abdomen] = 9,
                    [BodyPart.Head] = 8,
                }),
        };

        // Damage/reach/cooldown and the four combo fields, restated verbatim
        // from PhilippineCombatPresetV5.
        var weaponAttributes = new Dictionary<WeaponId, WeaponAttributes>
        {
            [WeaponId.Kampilan] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 15,
                    reachWorldUnits: 16,
                    cooldownTicks: 7,
                    comboOpenChanceBasisPoints: 2_000,
                    comboContinueChanceBasisPoints: 3_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 4)),

            [WeaponId.Wasay] = WeaponAttributes.TwoHanded(
                Profile(
                    damage: 18,
                    reachWorldUnits: 13,
                    cooldownTicks: 8,
                    comboOpenChanceBasisPoints: 1_000,
                    comboContinueChanceBasisPoints: 2_000,
                    comboMaxSteps: 2,
                    comboCooldownTicks: 5)),

            [WeaponId.Kalis] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 11,
                    reachWorldUnits: 13,
                    cooldownTicks: 5,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 3),
                Profile(
                    damage: 10,
                    reachWorldUnits: 12,
                    cooldownTicks: 5,
                    comboOpenChanceBasisPoints: 3_500,
                    comboContinueChanceBasisPoints: 4_500,
                    comboMaxSteps: 4,
                    comboCooldownTicks: 3)),

            [WeaponId.Itak] = WeaponAttributes.OneHanded(
                Profile(
                    damage: 9,
                    reachWorldUnits: 11,
                    cooldownTicks: 4,
                    comboOpenChanceBasisPoints: 3_000,
                    comboContinueChanceBasisPoints: 4_000,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2),
                Profile(
                    damage: 8,
                    reachWorldUnits: 10,
                    cooldownTicks: 4,
                    comboOpenChanceBasisPoints: 3_000,
                    comboContinueChanceBasisPoints: 4_000,
                    comboMaxSteps: 5,
                    comboCooldownTicks: 2)),

            [WeaponId.Bangkaw] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 10,
                    reachWorldUnits: 48,
                    cooldownTicks: 25,
                    projectileSpeedWorldUnitsPerTick: 8,
                    standoffWorldUnits: 36,
                    flightTickCeiling: 10)),

            [WeaponId.Busog] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 8,
                    reachWorldUnits: 80,
                    cooldownTicks: 45,
                    projectileSpeedWorldUnitsPerTick: 14,
                    standoffWorldUnits: 60,
                    flightTickCeiling: 10)),

            [WeaponId.Arquebus] = WeaponAttributes.TwoHanded(
                RangedProfile(
                    damage: 30,
                    reachWorldUnits: 112,
                    cooldownTicks: 240,
                    projectileSpeedWorldUnitsPerTick: 40,
                    standoffWorldUnits: 84,
                    flightTickCeiling: 6)),
        };

        // Five non-legacy armor baselines — see FieldedArmors above.
        var armors = FieldedArmors;

        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = TargetWeightProfiles.FromMultiplierOverrides(
                new Dictionary<BodyPart, int>()),
            [ShieldId.TallHardwood] =
                TargetWeightProfiles.FromMultiplierOverrides(
                    new Dictionary<BodyPart, int>
                    {
                        [BodyPart.Chest] = 500,
                        [BodyPart.Abdomen] = 500,
                    }),
        };

        // Content-hash-affecting per Scenario.RosterCounts. Every entry
        // restates one of version 5's nine base (Weapon, Shield, Rank)
        // loadouts crossed with every entry of FieldedArmors, in ascending
        // ArmorId order within each base loadout. See the class remarks for
        // why this is generated rather than hand-typed.
        var roster = BaseLoadouts
            .SelectMany(baseLoadout => FieldedArmors.Select(armor =>
                new CombatLoadout(
                    baseLoadout.Weapon,
                    armor,
                    baseLoadout.Shield,
                    baseLoadout.Rank)))
            .ToArray();

        // Per-rank fighter levels restated exactly from
        // PhilippineCombatPresetV5.
        var rankLevels = new Dictionary<RankId, int>
        {
            [RankId.Datu] = 3,
            [RankId.Maharlika] = 2,
            [RankId.Timawa] = 2,
            [RankId.AlipingNamamahay] = 1,
            [RankId.Ayuey] = 1,
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV7,
            Version,
            general,
            weaponTargets,
            armors,
            shieldMultipliers,
            roster,
            weaponAttributes: weaponAttributes,
            clashProfile: BuildClashProfile(),
            rankLevels: rankLevels,
            armorMitigation: BuildArmorMitigation());
    }

    /// <summary>
    /// Builds the per-(<see cref="ArmorId"/>, <see cref="BodyPart"/>)
    /// damage-mitigation table, in basis points out of
    /// <see cref="ClashProfile.BasisPointScale"/>. Covers every declared
    /// <see cref="ArmorId"/> member, including <see cref="ArmorId.LightOrganic"/>,
    /// because <see cref="CombatRuleset"/>'s construction-time validation
    /// requires a row for every declared member, not only the ones this
    /// preset's own roster fields.
    /// </summary>
    /// <remarks>
    /// <b>Every value here is a PROVISIONAL gameplay-tuning reconstruction,
    /// never a historical measurement.</b> No source from the period states
    /// what any armor actually stopped. Values follow the armor identity
    /// design document section 6.1's recommended starting table.
    /// <para>
    /// Coverage is per body-part group, not uniform, because that is where
    /// the sources actually differentiate: a corselet is a torso garment, so
    /// the hide corselet and the rigid cuirass protect the torso and nothing
    /// else, while Alvarado's 1548 account describes a sleeved, full-length
    /// cotton corselet reaching to the feet, so quilted cotton protects less
    /// per hit but protects everywhere. The Camarines report describes
    /// imported iron arriving with greaves, wristlets, and gauntlets, so
    /// imported iron is the one baseline extended to the limbs as well as
    /// the torso — see docs/research/HISTORICAL_1500s_ARMOR.md section 4.6
    /// for that report's contested status.
    /// </para>
    /// <para>
    /// Head mitigation is zero in every row because Hukbo has no helmet
    /// identity; when one exists it supplies its own mitigation and this
    /// column stops being a placeholder.
    /// </para>
    /// </remarks>
    private static IReadOnlyDictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>> BuildArmorMitigation() =>
        new Dictionary<ArmorId, IReadOnlyDictionary<BodyPart, int>>
        {
            // No body armor at all. Bare-torso fighting is documented at
            // Mactan, 1521. No mitigation.
            [ArmorId.None] = MitigationRow(
                torsoBasisPoints: 0,
                armsBasisPoints: 0,
                legsBasisPoints: 0,
                headBasisPoints: 0),

            // Legacy identity, retained unchanged so every preset up to and
            // including PrecolonialPhilippinesV6 keeps replaying. This
            // preset's own roster never fields it — see FieldedArmors.
            [ArmorId.LightOrganic] = MitigationRow(
                torsoBasisPoints: 0,
                armsBasisPoints: 0,
                legsBasisPoints: 0,
                headBasisPoints: 0),

            // Layered plant-fibre or cotton garment.
            [ArmorId.QuiltedCotton] = MitigationRow(
                torsoBasisPoints: 1_200,
                armsBasisPoints: 600,
                legsBasisPoints: 600,
                headBasisPoints: 0),

            // Carabao or buffalo hide corselet. Torso only.
            [ArmorId.HideCorselet] = MitigationRow(
                torsoBasisPoints: 1_800,
                armsBasisPoints: 0,
                legsBasisPoints: 0,
                headBasisPoints: 0),

            // Rigid hardwood, horn, or bamboo plate over the torso. Torso
            // only, and the highest torso mitigation among the non-iron
            // baselines.
            [ArmorId.RigidCuirass] = MitigationRow(
                torsoBasisPoints: 2_500,
                armsBasisPoints: 0,
                legsBasisPoints: 0,
                headBasisPoints: 0),

            // Imported iron. Rare, and historically contested — see
            // docs/research/HISTORICAL_1500s_ARMOR.md section 4.6. The one
            // baseline extended to the limbs.
            [ArmorId.ImportedIron] = MitigationRow(
                torsoBasisPoints: 3_200,
                armsBasisPoints: 1_600,
                legsBasisPoints: 1_600,
                headBasisPoints: 0),
        };

    /// <summary>
    /// Expands one armor baseline's four body-part-group mitigation values
    /// into a row covering every <see cref="BodyPart"/> member.
    /// </summary>
    /// <param name="torsoBasisPoints">
    /// Applied to <see cref="BodyPart.Chest"/>, <see cref="BodyPart.Abdomen"/>,
    /// and <see cref="BodyPart.Shoulder"/>.
    /// </param>
    /// <param name="armsBasisPoints">
    /// Applied to <see cref="BodyPart.WeaponArm"/>,
    /// <see cref="BodyPart.ShieldArm"/>, and <see cref="BodyPart.Hands"/>.
    /// </param>
    /// <param name="legsBasisPoints">
    /// Applied to <see cref="BodyPart.Thigh"/>, <see cref="BodyPart.Knee"/>,
    /// <see cref="BodyPart.Shin"/>, and <see cref="BodyPart.Feet"/>.
    /// </param>
    /// <param name="headBasisPoints">
    /// Applied to <see cref="BodyPart.Head"/>, <see cref="BodyPart.Neck"/>,
    /// and <see cref="BodyPart.Face"/>.
    /// </param>
    private static IReadOnlyDictionary<BodyPart, int> MitigationRow(
        int torsoBasisPoints,
        int armsBasisPoints,
        int legsBasisPoints,
        int headBasisPoints) =>
        new Dictionary<BodyPart, int>
        {
            [BodyPart.Chest] = torsoBasisPoints,
            [BodyPart.Abdomen] = torsoBasisPoints,
            [BodyPart.Shoulder] = torsoBasisPoints,

            [BodyPart.WeaponArm] = armsBasisPoints,
            [BodyPart.ShieldArm] = armsBasisPoints,
            [BodyPart.Hands] = armsBasisPoints,

            [BodyPart.Thigh] = legsBasisPoints,
            [BodyPart.Knee] = legsBasisPoints,
            [BodyPart.Shin] = legsBasisPoints,
            [BodyPart.Feet] = legsBasisPoints,

            [BodyPart.Head] = headBasisPoints,
            [BodyPart.Neck] = headBasisPoints,
            [BodyPart.Face] = headBasisPoints,
        };

    /// <summary>
    /// Defensive-interception tuning data, restated verbatim from
    /// <see cref="PhilippineCombatPresetV5"/>. Armor does not change which
    /// (defender weapon, defender shield, attacker weapon) keys the clash
    /// profile must cover — <see cref="ClashProfile"/> is keyed by weapon and
    /// shield only — so version 5's clash profile already covers every
    /// defender key this preset's forty-five-entry roster fields.
    /// </summary>
    /// <remarks>
    /// PROVISIONAL. Every value here is a gameplay tuning choice, not a
    /// historical measurement — see docs/research/WEAPON_CLASH_1500s.md and
    /// CLAUDE.md section 7.
    /// </remarks>
    private static ClashProfile BuildClashProfile()
    {
        var weaponIntercept =
            new Dictionary<(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>
            {
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Kampilan)] = 2_200,
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Wasay)] = 1_900,
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Kalis)] = 1_600,
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Itak)] = 2_000,

                [(WeaponId.Wasay, ShieldId.None, WeaponId.Kampilan)] = 1_500,
                [(WeaponId.Wasay, ShieldId.None, WeaponId.Wasay)] = 1_300,
                [(WeaponId.Wasay, ShieldId.None, WeaponId.Kalis)] = 1_100,
                [(WeaponId.Wasay, ShieldId.None, WeaponId.Itak)] = 1_400,

                [(WeaponId.Kalis, ShieldId.None, WeaponId.Kampilan)] = 1_200,
                [(WeaponId.Kalis, ShieldId.None, WeaponId.Wasay)] = 1_000,
                [(WeaponId.Kalis, ShieldId.None, WeaponId.Kalis)] = 1_500,
                [(WeaponId.Kalis, ShieldId.None, WeaponId.Itak)] = 1_500,

                [(WeaponId.Itak, ShieldId.None, WeaponId.Kampilan)] = 1_100,
                [(WeaponId.Itak, ShieldId.None, WeaponId.Wasay)] = 1_000,
                [(WeaponId.Itak, ShieldId.None, WeaponId.Kalis)] = 1_400,
                [(WeaponId.Itak, ShieldId.None, WeaponId.Itak)] = 1_400,

                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Bangkaw)] = 2_250,
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Busog)] = 1_750,
                [(WeaponId.Kampilan, ShieldId.None, WeaponId.Arquebus)] = 750,

                [(WeaponId.Wasay, ShieldId.None, WeaponId.Bangkaw)] = 2_000,
                [(WeaponId.Wasay, ShieldId.None, WeaponId.Busog)] = 1_500,
                [(WeaponId.Wasay, ShieldId.None, WeaponId.Arquebus)] = 625,

                [(WeaponId.Kalis, ShieldId.None, WeaponId.Bangkaw)] = 1_750,
                [(WeaponId.Kalis, ShieldId.None, WeaponId.Busog)] = 1_375,
                [(WeaponId.Kalis, ShieldId.None, WeaponId.Arquebus)] = 625,

                [(WeaponId.Itak, ShieldId.None, WeaponId.Bangkaw)] = 1_625,
                [(WeaponId.Itak, ShieldId.None, WeaponId.Busog)] = 1_250,
                [(WeaponId.Itak, ShieldId.None, WeaponId.Arquebus)] = 500,

                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Kampilan)] = 1_500,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Wasay)] = 1_350,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Kalis)] = 1_350,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Itak)] = 1_350,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Bangkaw)] = 1_500,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Busog)] = 1_200,
                [(WeaponId.Bangkaw, ShieldId.None, WeaponId.Arquebus)] = 450,

                [(WeaponId.Busog, ShieldId.None, WeaponId.Kampilan)] = 1_050,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Wasay)] = 900,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Kalis)] = 900,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Itak)] = 900,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Bangkaw)] = 1_050,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Busog)] = 1_050,
                [(WeaponId.Busog, ShieldId.None, WeaponId.Arquebus)] = 300,

                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Kampilan)] = 450,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Wasay)] = 375,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Kalis)] = 375,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Itak)] = 375,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Bangkaw)] = 450,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Busog)] = 375,
                [(WeaponId.Arquebus, ShieldId.None, WeaponId.Arquebus)] = 450,

                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kampilan)] = 500,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Wasay)] = 400,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Kalis)] = 600,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Itak)] = 600,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Bangkaw)] = 700,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Busog)] = 550,
                [(WeaponId.Kalis, ShieldId.TallHardwood, WeaponId.Arquebus)] = 250,

                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kampilan)] = 400,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Wasay)] = 300,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Kalis)] = 500,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Itak)] = 500,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Bangkaw)] = 575,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Busog)] = 450,
                [(WeaponId.Itak, ShieldId.TallHardwood, WeaponId.Arquebus)] = 175,
            };

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>
        {
            [(WeaponId.Kampilan, ShieldId.None)] = 1_000,
            [(WeaponId.Wasay, ShieldId.None)] = 900,
            [(WeaponId.Kalis, ShieldId.None)] = 1_350,
            [(WeaponId.Itak, ShieldId.None)] = 1_450,

            [(WeaponId.Bangkaw, ShieldId.None)] = 1_600,
            [(WeaponId.Busog, ShieldId.None)] = 1_700,
            [(WeaponId.Arquebus, ShieldId.None)] = 1_800,

            [(WeaponId.Kalis, ShieldId.TallHardwood)] = 1_000,
            [(WeaponId.Itak, ShieldId.TallHardwood)] = 1_100,
        };

        var hardShareBases = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 3_300,
            [WeaponId.Wasay] = 4_000,
            [WeaponId.Kalis] = 1_200,
            [WeaponId.Itak] = 1_800,

            [WeaponId.Bangkaw] = 2_200,
            [WeaponId.Busog] = 900,
            [WeaponId.Arquebus] = 4_500,
        };

        var hardShareMultipliers = new Dictionary<WeaponId, int>
        {
            [WeaponId.Kampilan] = 1_150,
            [WeaponId.Wasay] = 1_050,
            [WeaponId.Kalis] = 750,
            [WeaponId.Itak] = 700,

            [WeaponId.Bangkaw] = 600,
            [WeaponId.Busog] = 400,
            [WeaponId.Arquebus] = 300,
        };

        return new ClashProfile(
            weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel,
            hardShareBases,
            hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    /// <summary>
    /// Authors a melee <see cref="WeaponProfile"/> from world units rather
    /// than raw fixed-point values, restated exactly from
    /// PhilippineCombatPresetV5's private helper of the same name and shape.
    /// </summary>
    private static WeaponProfile Profile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks,
        int comboOpenChanceBasisPoints,
        int comboContinueChanceBasisPoints,
        int comboMaxSteps,
        int comboCooldownTicks) =>
        new(
            damage,
            reachWorldUnits * FixedPoint.Scale,
            cooldownTicks,
            comboOpenChanceBasisPoints,
            comboContinueChanceBasisPoints,
            comboMaxSteps,
            comboCooldownTicks);

    /// <summary>
    /// Authors a ranged <see cref="WeaponProfile"/> from world units and
    /// world-units-per-tick rather than raw fixed-point values, restated
    /// exactly from PhilippineCombatPresetV5's private helper of the same
    /// name and shape.
    /// </summary>
    private static WeaponProfile RangedProfile(
        int damage,
        int reachWorldUnits,
        int cooldownTicks,
        int projectileSpeedWorldUnitsPerTick,
        int standoffWorldUnits,
        int flightTickCeiling) =>
        new(
            damage,
            reachWorldUnits * FixedPoint.Scale,
            cooldownTicks,
            ComboOpenChanceBasisPoints: 0,
            ComboContinueChanceBasisPoints: 0,
            ComboMaxSteps: 1,
            ComboCooldownTicks: 1,
            ProjectileSpeedRaw: projectileSpeedWorldUnitsPerTick * FixedPoint.Scale,
            StandoffDistanceRaw: standoffWorldUnits * FixedPoint.Scale,
            FlightTickCeiling: flightTickCeiling);
}
