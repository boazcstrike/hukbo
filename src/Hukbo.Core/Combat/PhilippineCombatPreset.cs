namespace Hukbo.Core.Combat;

/// <summary>
/// Version 2 of the pre-colonial Philippine combat preset: four warrior
/// target-weighting profiles derived from the supplied research brief, one
/// light-organic armor identity, one tall-hardwood shield profile, and the
/// defensive-interception clash profile an accepted attack is resolved
/// against.
/// </summary>
/// <remarks>
/// Configuration is written as explicit, hand-authored data rather than a
/// deserialized or reflection-driven configuration graph. Combined
/// weapon-comparison names (Kampilan, Panabas, Kris) are provisional
/// evidence-metadata cross-references, not player-facing sixteenth-century
/// identifications; see docs/research/HISTORICAL_1500s_WEAPONS.md. Shield
/// multipliers are provisional gameplay tuning values, not historical
/// measurements.
/// </remarks>
public static class PhilippineCombatPreset
{
    /// <summary>
    /// Raised from 1 to 2 when the clash tables landed. The clash values are
    /// folded into <see cref="CombatRuleset.ContentHash"/> and reach the
    /// authoritative event stream, so a ruleset carrying them is a different
    /// preset version even though its identity is unchanged.
    /// </summary>
    public const int Version = 2;

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
            roster,
            BuildClashProfile());
    }

    /// <summary>
    /// The thirty-two defensive-interception tuning values: sixteen weapon
    /// intercept cells, one flat shield intercept, four void values, four
    /// hard-share bases, four hard-share multipliers, two hard-share clamp
    /// bounds, and one interception ceiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PROVISIONAL.</b> Every value returned here is a gameplay tuning
    /// choice, not a historical measurement. The research states plainly that
    /// <b>all sixteen cells of the weapon-intercept matrix are provisional
    /// reconstructions with no evidentiary confidence</b>: no source,
    /// Philippine or otherwise, describes these four loadouts — or any four
    /// loadouts — fighting one another, and only their relative ordering is
    /// argued from evidence, and only weakly. See
    /// docs/research/WEAPON_CLASH_1500s.md section 5.3 and CLAUDE.md section 7.
    /// </para>
    /// <para>
    /// The shield channel is the only defensive channel with sixteenth-century
    /// documentary support, and even there the figure is a tuning choice rather
    /// than a measured rate. Interception is also deliberately set below what
    /// the historical record would suggest, because Hukbo has no morale model
    /// and must therefore reach a decision by attrition, a mechanism that
    /// historically did not decide battles. That is a design compensation and
    /// must never be read back as evidence about how often people parried.
    /// </para>
    /// </remarks>
    private static ClashProfile BuildClashProfile()
    {
        // PROVISIONAL. Defender row against attacker column, in basis points.
        // Zero evidentiary confidence; see the remarks above.
        var weaponIntercept = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>
        {
            [(WeaponId.GreatBlade, WeaponId.GreatBlade)] = 2_200,
            [(WeaponId.GreatBlade, WeaponId.HeavyChopper)] = 1_900,
            [(WeaponId.GreatBlade, WeaponId.ThrustingBlade)] = 1_600,
            [(WeaponId.GreatBlade, WeaponId.Bolo)] = 2_000,
            [(WeaponId.HeavyChopper, WeaponId.GreatBlade)] = 1_500,
            [(WeaponId.HeavyChopper, WeaponId.HeavyChopper)] = 1_300,
            [(WeaponId.HeavyChopper, WeaponId.ThrustingBlade)] = 1_100,
            [(WeaponId.HeavyChopper, WeaponId.Bolo)] = 1_400,
            [(WeaponId.ThrustingBlade, WeaponId.GreatBlade)] = 500,
            [(WeaponId.ThrustingBlade, WeaponId.HeavyChopper)] = 400,
            [(WeaponId.ThrustingBlade, WeaponId.ThrustingBlade)] = 600,
            [(WeaponId.ThrustingBlade, WeaponId.Bolo)] = 600,
            [(WeaponId.Bolo, WeaponId.GreatBlade)] = 400,
            [(WeaponId.Bolo, WeaponId.HeavyChopper)] = 300,
            [(WeaponId.Bolo, WeaponId.ThrustingBlade)] = 500,
            [(WeaponId.Bolo, WeaponId.Bolo)] = 500,
        };

        // PROVISIONAL. Basis points the defender steps off the line entirely,
        // by defending weapon. Zero evidentiary confidence.
        var voidChannel = new Dictionary<WeaponId, int>
        {
            [WeaponId.GreatBlade] = 1_000,
            [WeaponId.HeavyChopper] = 900,
            [WeaponId.ThrustingBlade] = 1_000,
            [WeaponId.Bolo] = 1_100,
        };

        // PROVISIONAL. Share of the weapon channel that arrests rather than
        // brushes, by incoming attacker weapon. Zero evidentiary confidence.
        var hardShareBases = new Dictionary<WeaponId, int>
        {
            [WeaponId.GreatBlade] = 3_300,
            [WeaponId.HeavyChopper] = 4_000,
            [WeaponId.ThrustingBlade] = 1_200,
            [WeaponId.Bolo] = 1_800,
        };

        // PROVISIONAL. Per-thousand scaling of that share by the defending
        // instrument. Zero evidentiary confidence.
        var hardShareMultipliers = new Dictionary<WeaponId, int>
        {
            [WeaponId.GreatBlade] = 1_150,
            [WeaponId.HeavyChopper] = 1_050,
            [WeaponId.ThrustingBlade] = 750,
            [WeaponId.Bolo] = 700,
        };

        return new ClashProfile(
            weaponIntercept,

            // PROVISIONAL. Flat across every attacker: the research states
            // plainly that the per-attacker spread it suggests has no source
            // behind it.
            shieldIntercept: 2_400,
            voidChannel,
            hardShareBases,
            hardShareMultipliers,

            // Both clamp bounds are guard-only and neither binds with these
            // tables: the hard-share product spans 840 to 4,600. They exist so
            // that a future tuning pass cannot produce a degenerate split.
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,

            // Likewise a guard. The largest total these tables produce is
            // 4,000, so the rescale branch is unreachable in production.
            maximumInterceptionBasisPoints: 5_500);
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
