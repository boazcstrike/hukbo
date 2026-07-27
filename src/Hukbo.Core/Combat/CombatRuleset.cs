using System.Collections.Immutable;
using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Combat;

/// <summary>
/// Immutable, versioned combat targeting configuration: general and
/// per-weapon body-part target weights, per-shield defense multipliers,
/// and the deterministic warrior loadout roster. Gameplay tuning values
/// here (for example shield multipliers) are provisional balance
/// starting points, not historical measurements; see
/// docs/research/HISTORICAL_1500s_WEAPONS.md for evidence context.
/// </summary>
public sealed class CombatRuleset
{
    private readonly TargetWeightProfile _generalTargets;
    private readonly IReadOnlyDictionary<WeaponId, TargetWeightProfile> _weaponTargets;
    private readonly IReadOnlyList<ArmorId> _armors;
    private readonly IReadOnlyDictionary<ShieldId, TargetWeightProfile> _shieldMultipliers;
    private readonly IReadOnlyList<CombatLoadout> _roster;
    private readonly IReadOnlyDictionary<WeaponId, WeaponAttributes>? _weaponAttributes;
    private readonly IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), EffectiveWeightTable> _effectiveWeights;

    /// <summary>
    /// The shortest reach a weapon profile may declare and still be able to
    /// strike. <c>BuildMovementProposal</c> stops an advancing warrior at two
    /// body radii so opposing front ranks make body contact rather than
    /// halting at weapon reach, so a profile at or below that distance
    /// produces a warrior who advances into contact and can then never
    /// attack. Asserted per profile, not per weapon: every one-handed
    /// weapon's paired reach is shorter than its solo reach, so a retune that
    /// shaves a world unit off a paired profile is the most likely way anyone
    /// ever trips this floor.
    /// </summary>
    public const int MinimumProfileReachRawExclusive =
        2 * CollisionRules.DefaultBodyRadiusRaw;

    public CombatRuleset(
        CombatPresetId id,
        int version,
        TargetWeightProfile generalTargets,
        IReadOnlyDictionary<WeaponId, TargetWeightProfile> weaponTargets,
        IReadOnlyList<ArmorId> armors,
        IReadOnlyDictionary<ShieldId, TargetWeightProfile> shieldMultipliers,
        IReadOnlyList<CombatLoadout> roster,
        IReadOnlyDictionary<WeaponId, WeaponAttributes>? weaponAttributes = null)
    {
        ArgumentNullException.ThrowIfNull(generalTargets);
        ArgumentNullException.ThrowIfNull(weaponTargets);
        ArgumentNullException.ThrowIfNull(armors);
        ArgumentNullException.ThrowIfNull(shieldMultipliers);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        if (weaponTargets.Count == 0)
        {
            throw new ArgumentException(
                "At least one weapon target profile is required.",
                nameof(weaponTargets));
        }

        if (armors.Count == 0)
        {
            throw new ArgumentException(
                "At least one armor identity is required.",
                nameof(armors));
        }

        if (shieldMultipliers.Count == 0)
        {
            throw new ArgumentException(
                "At least one shield multiplier profile is required.",
                nameof(shieldMultipliers));
        }

        if (roster.Count == 0)
        {
            throw new ArgumentException(
                "At least one roster loadout is required.",
                nameof(roster));
        }

        Id = id;
        Version = version;
        _generalTargets = generalTargets;

        // Defensive copies: a caller retaining the collection it passed in
        // could otherwise mutate targeting data after construction while
        // ContentHash keeps reporting the value computed here, letting
        // Resolve* behavior silently drift out of sync with the hash a
        // replay or snapshot depends on.
        _weaponTargets = new Dictionary<WeaponId, TargetWeightProfile>(
            weaponTargets);

        // Sorted and deduplicated, matching the weapon/shield keys (which a
        // dictionary already deduplicates and which are always iterated via
        // `.OrderBy(id => (int)id)`). Otherwise two equivalent rulesets
        // built with the same armor set in a different caller-supplied
        // order would compute different ContentHash values.
        _armors = NormalizeArmors(armors);
        _shieldMultipliers = new Dictionary<ShieldId, TargetWeightProfile>(
            shieldMultipliers);
        _roster = roster.ToArray();
        _weaponAttributes = weaponAttributes is null
            ? null
            : new Dictionary<WeaponId, WeaponAttributes>(weaponAttributes);

        ValidateWeaponAttributes();
        _effectiveWeights = BuildEffectiveWeightTables();
        ValidateResolvedTotals();
        ContentHash = ComputeContentHash();
    }

    public CombatPresetId Id { get; }

    public int Version { get; }

    public ulong ContentHash { get; }

    public TargetWeightProfile GeneralTargets => _generalTargets;

    public IReadOnlyList<CombatLoadout> Roster => _roster;

    public int ResolveWeaponWeight(WeaponId weapon, BodyPart bodyPart)
    {
        if (!_weaponTargets.TryGetValue(weapon, out var profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Unknown weapon identity for this combat ruleset.");
        }

        return profile.Get(bodyPart);
    }

    public int ResolveDefenseMultiplier(ShieldId shield, BodyPart bodyPart)
    {
        if (!_shieldMultipliers.TryGetValue(shield, out var profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                "Unknown shield identity for this combat ruleset.");
        }

        return profile.Get(bodyPart);
    }

    /// <summary>
    /// Whether this preset declares per-weapon attributes at all. False for
    /// version 1, which predates them and whose warriors take damage, reach,
    /// and cooldown from the scenario instead. A preset that declares none
    /// hashes none, which is what keeps version 1's content hash — and
    /// therefore its replays — exactly where it was.
    /// </summary>
    public bool HasWeaponProfiles => _weaponAttributes is not null;

    /// <summary>
    /// The largest <see cref="WeaponProfile.DamagePerAttack"/> any profile in
    /// this preset declares, or <c>null</c> when the preset declares no
    /// profiles. <see cref="Hukbo.Core.Simulation.Scenario"/> checks its
    /// same-tick damage accumulation guard against this rather than against
    /// its own global damage value, which a per-weapon profile can exceed.
    /// </summary>
    public int? MaximumProfileDamagePerAttack { get; private set; }

    /// <summary>
    /// The grip a weapon is fought with.
    /// </summary>
    public WeaponGrip ResolveWeaponGrip(WeaponId weapon) =>
        RequireAttributes(weapon).Grip;

    /// <summary>
    /// The attribute profile one warrior fights with, given their own weapon
    /// and their own shield: the solo profile when carrying no shield, the
    /// paired profile otherwise.
    /// </summary>
    /// <remarks>
    /// This is deliberately the narrowest possible seam, and it is the only
    /// place grip and shield meet. No call site reads a profile directly, so
    /// a later preset can replace this one rule with per-shield resolution
    /// without touching any of them.
    /// <para>
    /// Both arguments describe the <em>same</em> warrior. That is what makes
    /// this a different product from
    /// <see cref="ResolveEffectiveWeights"/>, whose weapon is the attacker's
    /// and whose shield is the defender's. Merging the two would let a
    /// defender's shield change the attacker's damage.
    /// </para>
    /// </remarks>
    public WeaponProfile ResolveWeaponProfile(WeaponId weapon, ShieldId shield)
    {
        var attributes = RequireAttributes(weapon);
        if (shield == ShieldId.None)
        {
            return attributes.Solo;
        }

        // Non-null by the construction invariant in ValidateWeaponAttributes:
        // a one-handed weapon must declare a paired profile, and a two-handed
        // weapon can never reach here because no roster entry may pair one
        // with a shield.
        return attributes.Paired ??
            throw new InvalidOperationException(
                $"Weapon {weapon} has no paired profile for shield {shield}.");
    }

    private WeaponAttributes RequireAttributes(WeaponId weapon)
    {
        if (_weaponAttributes is null)
        {
            throw new InvalidOperationException(
                $"Combat preset {Id} declares no weapon attributes; check " +
                $"{nameof(HasWeaponProfiles)} before resolving a profile.");
        }

        if (!_weaponAttributes.TryGetValue(weapon, out var attributes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Unknown weapon identity for this combat ruleset.");
        }

        return attributes;
    }

    /// <summary>
    /// Enforces the three configuration invariants a misconfigured preset
    /// would otherwise express as a battle that quietly cannot happen: a
    /// warrior who cannot exist, a silent fallback to the wrong profile, or a
    /// warrior who advances into contact and can never strike.
    /// </summary>
    private void ValidateWeaponAttributes()
    {
        if (_weaponAttributes is null)
        {
            return;
        }

        foreach (var weapon in _weaponTargets.Keys.OrderBy(id => (int)id))
        {
            if (!_weaponAttributes.ContainsKey(weapon))
            {
                throw new ArgumentException(
                    $"Weapon {weapon} has target weights but no attribute " +
                    "profile. A preset declares attributes for every weapon " +
                    "or for none.",
                    "weaponAttributes");
            }
        }

        var maximumDamage = 0;
        foreach (var weapon in _weaponAttributes.Keys.OrderBy(id => (int)id))
        {
            var attributes = _weaponAttributes[weapon];
            ValidateWeaponAttribute(weapon, attributes);
            maximumDamage = Math.Max(
                maximumDamage,
                attributes.Solo.DamagePerAttack);
            if (attributes.Paired is { } paired)
            {
                maximumDamage = Math.Max(
                    maximumDamage,
                    paired.DamagePerAttack);
            }
        }

        MaximumProfileDamagePerAttack = maximumDamage;

        foreach (var loadout in _roster)
        {
            if (loadout.Shield != ShieldId.None &&
                RequireAttributes(loadout.Weapon).Grip == WeaponGrip.TwoHanded)
            {
                throw new ArgumentException(
                    $"Roster entry pairs two-handed weapon {loadout.Weapon} " +
                    $"with shield {loadout.Shield}. A two-handed weapon " +
                    "occupies both hands, so a shield is forbidden rather " +
                    "than merely absent.",
                    "roster");
            }
        }
    }

    private static void ValidateWeaponAttribute(
        WeaponId weapon,
        WeaponAttributes attributes)
    {
        attributes.Solo.Validate($"{weapon}.{nameof(WeaponAttributes.Solo)}");
        ValidateProfileReach(weapon, "solo", attributes.Solo);

        switch (attributes.Grip)
        {
            case WeaponGrip.OneHanded when attributes.Paired is null:
                throw new ArgumentException(
                    $"One-handed weapon {weapon} declares no paired profile. " +
                    "A missing paired profile is an error, not a silent " +
                    "fallback to the solo one.",
                    nameof(weapon));

            case WeaponGrip.TwoHanded when attributes.Paired is not null:
                throw new ArgumentException(
                    $"Two-handed weapon {weapon} declares a paired profile, " +
                    "but no shield may be carried with it.",
                    nameof(weapon));

            case WeaponGrip.OneHanded:
                var paired = attributes.Paired!.Value;
                paired.Validate(
                    $"{weapon}.{nameof(WeaponAttributes.Paired)}");
                ValidateProfileReach(weapon, "paired", paired);
                break;
        }
    }

    private static void ValidateProfileReach(
        WeaponId weapon,
        string gripLabel,
        WeaponProfile profile)
    {
        if (profile.AttackRangeRaw <= MinimumProfileReachRawExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile.AttackRangeRaw,
                $"The {gripLabel} profile of weapon {weapon} has a reach of " +
                $"{profile.AttackRangeRaw} raw, at or below the floor of " +
                $"{MinimumProfileReachRawExclusive}. Such a warrior would " +
                "advance into body contact and then never be able to strike.");
        }
    }

    public CombatLoadout ResolveLoadout(ulong entityId)
    {
        if (entityId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entityId),
                entityId,
                "Entity ID must be positive.");
        }

        var index = (int)((entityId - 1) % (ulong)_roster.Count);
        return _roster[index];
    }

    /// <summary>
    /// Resolves the precomputed, immutable per-<see cref="BodyPart"/>
    /// effective-weight table (weapon target weight times shield defense
    /// multiplier) and its total for one weapon/shield pair. Built once at
    /// construction from the same values <see cref="ResolveWeaponWeight"/>
    /// and <see cref="ResolveDefenseMultiplier"/> would return, so
    /// <see cref="HitLocationResolver.Resolve"/> can select a body part with
    /// direct array reads instead of two dictionary lookups per candidate
    /// part on every accepted attack. Not a runtime cache: the table never
    /// changes after construction and this ruleset is itself immutable.
    /// </summary>
    internal EffectiveWeightTable ResolveEffectiveWeights(WeaponId weapon, ShieldId shield)
    {
        if (_effectiveWeights.TryGetValue((weapon, shield), out var table))
        {
            return table;
        }

        if (!_weaponTargets.ContainsKey(weapon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Unknown weapon identity for this combat ruleset.");
        }

        throw new ArgumentOutOfRangeException(
            nameof(shield),
            shield,
            "Unknown shield identity for this combat ruleset.");
    }

    private void ValidateResolvedTotals()
    {
        foreach (var weapon in _weaponTargets.Keys.OrderBy(id => (int)id))
        {
            foreach (var shield in _shieldMultipliers.Keys.OrderBy(id => (int)id))
            {
                if (_effectiveWeights[(weapon, shield)].Total == 0)
                {
                    throw new InvalidOperationException(
                        "Resolved target weight total for weapon " +
                        $"{weapon} and shield {shield} must be positive.");
                }
            }
        }
    }

    private Dictionary<(WeaponId Weapon, ShieldId Shield), EffectiveWeightTable> BuildEffectiveWeightTables()
    {
        var tables = new Dictionary<(WeaponId, ShieldId), EffectiveWeightTable>();
        foreach (var weapon in _weaponTargets.Keys)
        {
            foreach (var shield in _shieldMultipliers.Keys)
            {
                var weights = ImmutableArray.CreateBuilder<ulong>(BodyPartCatalog.Ordered.Length);
                var total = 0UL;
                foreach (var part in BodyPartCatalog.Ordered)
                {
                    var weight = checked((ulong)ResolveWeaponWeight(weapon, part));
                    var multiplier = checked((ulong)ResolveDefenseMultiplier(shield, part));
                    var effective = checked(weight * multiplier);
                    weights.Add(effective);
                    total = checked(total + effective);
                }

                tables[(weapon, shield)] = new EffectiveWeightTable(weights.MoveToImmutable(), total);
            }
        }

        return tables;
    }

    private static void AddProfile(ref ulong hash, WeaponProfile profile)
    {
        Fnv1a.Add(ref hash, unchecked((ulong)(uint)profile.DamagePerAttack));
        Fnv1a.Add(ref hash, unchecked((ulong)(uint)profile.AttackRangeRaw));
        Fnv1a.Add(
            ref hash,
            unchecked((ulong)(uint)profile.AttackCooldownTicks));
    }

    private static IReadOnlyList<ArmorId> NormalizeArmors(IReadOnlyList<ArmorId> armors)
    {
        var sorted = armors.Distinct().OrderBy(id => (int)id).ToArray();
        if (sorted.Length != armors.Count)
        {
            throw new ArgumentException(
                "Armor identities must not contain duplicates.",
                nameof(armors));
        }

        return sorted;
    }

    private ulong ComputeContentHash()
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, (ulong)Id);
        Fnv1a.Add(ref hash, (ulong)Version);

        Fnv1a.Add(ref hash, (ulong)BodyPartCatalog.Ordered.Length);
        foreach (var part in BodyPartCatalog.Ordered)
        {
            Fnv1a.Add(ref hash, (ulong)part);
            Fnv1a.Add(ref hash, (ulong)_generalTargets.Get(part));
        }

        var weaponIds = _weaponTargets.Keys.OrderBy(id => (int)id).ToArray();
        Fnv1a.Add(ref hash, (ulong)weaponIds.Length);
        foreach (var weapon in weaponIds)
        {
            Fnv1a.Add(ref hash, (ulong)weapon);
            var profile = _weaponTargets[weapon];
            foreach (var part in BodyPartCatalog.Ordered)
            {
                Fnv1a.Add(ref hash, (ulong)part);
                Fnv1a.Add(ref hash, (ulong)profile.Get(part));
            }
        }

        // _armors is already sorted ascending and deduplicated by
        // NormalizeArmors, so two equivalent rulesets built with the same
        // armor set in a different caller-supplied order hash identically.
        Fnv1a.Add(ref hash, (ulong)_armors.Count);
        foreach (var armor in _armors)
        {
            Fnv1a.Add(ref hash, (ulong)armor);
        }

        var shieldIds = _shieldMultipliers.Keys.OrderBy(id => (int)id).ToArray();
        Fnv1a.Add(ref hash, (ulong)shieldIds.Length);
        foreach (var shield in shieldIds)
        {
            Fnv1a.Add(ref hash, (ulong)shield);
            var profile = _shieldMultipliers[shield];
            foreach (var part in BodyPartCatalog.Ordered)
            {
                Fnv1a.Add(ref hash, (ulong)part);
                Fnv1a.Add(ref hash, (ulong)profile.Get(part));
            }
        }

        Fnv1a.Add(ref hash, (ulong)_roster.Count);
        foreach (var loadout in _roster)
        {
            Fnv1a.Add(ref hash, (ulong)loadout.Weapon);
            Fnv1a.Add(ref hash, (ulong)loadout.Armor);
            Fnv1a.Add(ref hash, (ulong)loadout.Shield);
        }

        // Contributed only by a preset that declares attributes. A preset
        // without them mixes nothing here at all — not even a zero count —
        // which is what leaves version 1's content hash, and therefore every
        // replay recorded against it, exactly where it was.
        if (_weaponAttributes is not null)
        {
            var attributeIds = _weaponAttributes.Keys
                .OrderBy(id => (int)id)
                .ToArray();
            Fnv1a.Add(ref hash, (ulong)attributeIds.Length);
            foreach (var weapon in attributeIds)
            {
                var attributes = _weaponAttributes[weapon];
                Fnv1a.Add(ref hash, (ulong)weapon);
                Fnv1a.Add(ref hash, (ulong)attributes.Grip);
                AddProfile(ref hash, attributes.Solo);
                Fnv1a.Add(ref hash, attributes.Paired is null ? 0UL : 1UL);
                if (attributes.Paired is { } paired)
                {
                    AddProfile(ref hash, paired);
                }
            }
        }

        return hash;
    }
}

/// <summary>
/// Immutable, precomputed per-<see cref="BodyPart"/> effective-weight row
/// (weapon target weight times shield defense multiplier, in
/// <see cref="BodyPartCatalog.Ordered"/> index order) plus its total, for
/// one weapon/shield pair. Built once by <see cref="CombatRuleset"/> at
/// construction; never mutated afterward.
/// </summary>
internal readonly record struct EffectiveWeightTable(ImmutableArray<ulong> Weights, ulong Total);
