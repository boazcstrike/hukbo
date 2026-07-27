namespace Hukbo.Core.Combat;

/// <summary>
/// Version 1 of the pre-colonial Philippine combat preset: four warrior
/// target-weighting profiles derived from the supplied research brief, one
/// light-organic armor identity, and one tall-hardwood shield profile.
/// </summary>
/// <remarks>
/// Frozen. This preset declares no weapon attributes and no clash profile, so
/// neither block is mixed into its content hash and that hash is the same value
/// it was before either feature existed. Every replay recorded against version
/// 1 still verifies. New combat behavior goes into
/// <see cref="PhilippineCombatPresetV2"/>, which is what
/// <c>Scenario.CombatPreset</c> defaults to.
/// <para>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph. Combined
/// weapon-comparison names (Kampilan, Panabas, Kris) are provisional
/// evidence-metadata cross-references, not player-facing sixteenth-century
/// identifications; see docs/research/HISTORICAL_1500s_WEAPONS.md. Shield
/// multipliers are provisional gameplay tuning values, not historical
/// measurements.
/// </para>
/// </remarks>
public static class PhilippineCombatPreset
{
    public const int Version = 1;

    private const int DefaultMultiplierBasisPoints = 1_000;

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

        var weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>
        {
            // Historically cautious display name: "Great Blade". PROVISIONAL
            // evidence cross-reference: comparable in silhouette to sources
            // describing a kampilan-type long, single-edged blade; see
            // docs/research/HISTORICAL_1500s_WEAPONS.md. Not a player-facing
            // sixteenth-century identification.
            [WeaponId.Kampilan] = BuildProfile(general, new Dictionary<BodyPart, int>
            {
                [BodyPart.Head] = 10,
                [BodyPart.Neck] = 10,
                [BodyPart.Shoulder] = 9,
                [BodyPart.WeaponArm] = 8,
                [BodyPart.ShieldArm] = 8,
                [BodyPart.Chest] = 8,
            }),

            // Historically cautious display name: "Heavy Chopper". PROVISIONAL
            // evidence cross-reference: comparable in silhouette to sources
            // describing a panabas-type forward-weighted chopping blade; see
            // docs/research/HISTORICAL_1500s_WEAPONS.md. Not a player-facing
            // sixteenth-century identification.
            [WeaponId.Wasay] = BuildProfile(general, new Dictionary<BodyPart, int>
            {
                [BodyPart.Shoulder] = 10,
                [BodyPart.Head] = 9,
                [BodyPart.WeaponArm] = 9,
                [BodyPart.ShieldArm] = 9,
            }),

            // Historically cautious display name: "Thrusting Blade". PROVISIONAL
            // evidence cross-reference: comparable in silhouette to sources
            // describing a kris-type thrusting blade; see
            // docs/research/HISTORICAL_1500s_WEAPONS.md. Not a player-facing
            // sixteenth-century identification.
            [WeaponId.Kalis] = BuildProfile(general, new Dictionary<BodyPart, int>
            {
                [BodyPart.Abdomen] = 10,
                [BodyPart.Chest] = 9,
                [BodyPart.Neck] = 8,
            }),

            // Enum identity "Bolo"; player-facing display name is the plain
            // descriptor "Work Blade", not this identifier. PROVISIONAL
            // evidence cross-reference: a general local utility-blade
            // tradition rather than one sixteenth-century object; see
            // docs/research/HISTORICAL_1500s_WEAPONS.md.
            [WeaponId.Itak] = BuildProfile(general, new Dictionary<BodyPart, int>
            {
                [BodyPart.WeaponArm] = 10,
                [BodyPart.ShieldArm] = 10,
                [BodyPart.Hands] = 9,
                [BodyPart.Neck] = 8,
                [BodyPart.Face] = 8,
            }),
        };

        var armors = new[] { ArmorId.LightOrganic };

        // Light-organic armor (quilted cotton, bark, leather, and similar
        // light protection) applies no additional targeting multiplier in
        // this first profile: the general warrior weights already encode
        // increased shoulder/chest/abdomen exposure relative to
        // plate-armored warfare.
        var shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>
        {
            [ShieldId.None] = BuildProfile(new Dictionary<BodyPart, int>()),

            // PROVISIONAL gameplay tuning, not a historical measurement:
            // a tall hardwood shield halves (500 of 1000 basis points)
            // chest and abdomen targeting weight, raising the relative
            // probability of arm, leg, head, neck, and face hits without
            // inventing bonuses for those parts.
            [ShieldId.TallHardwood] = BuildProfile(new Dictionary<BodyPart, int>
            {
                [BodyPart.Chest] = 500,
                [BodyPart.Abdomen] = 500,
            }),
        };

        var roster = new CombatLoadout[]
        {
            new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
            new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
            new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
        };

        return new CombatRuleset(
            CombatPresetId.PrecolonialPhilippinesV1,
            Version,
            general,
            weaponTargets,
            armors,
            shieldMultipliers,
            roster);
    }

    private static TargetWeightProfile BuildProfile(
        TargetWeightProfile fallback,
        IReadOnlyDictionary<BodyPart, int> overrides)
    {
        var entries = new List<(BodyPart Part, int Value)>(BodyPartCatalog.Ordered.Length);
        foreach (var part in BodyPartCatalog.Ordered)
        {
            entries.Add((
                part,
                overrides.TryGetValue(part, out var value) ? value : fallback.Get(part)));
        }

        return new TargetWeightProfile(entries);
    }

    private static TargetWeightProfile BuildProfile(
        IReadOnlyDictionary<BodyPart, int> overrides)
    {
        var entries = new List<(BodyPart Part, int Value)>(BodyPartCatalog.Ordered.Length);
        foreach (var part in BodyPartCatalog.Ordered)
        {
            entries.Add((
                part,
                overrides.TryGetValue(part, out var value)
                    ? value
                    : DefaultMultiplierBasisPoints));
        }

        return new TargetWeightProfile(entries);
    }
}
