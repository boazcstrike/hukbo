using System.Collections.Immutable;
using Hukbo.Core.Determinism;

namespace Hukbo.Core.Combat;

/// <summary>
/// Immutable, versioned combat targeting configuration: general and
/// per-weapon body-part target weights, per-shield defense multipliers,
/// the deterministic warrior loadout roster, and the defensive-interception
/// clash profile an accepted attack is resolved against. Gameplay tuning values
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
    private readonly IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), EffectiveWeightTable> _effectiveWeights;

    public CombatRuleset(
        CombatPresetId id,
        int version,
        TargetWeightProfile generalTargets,
        IReadOnlyDictionary<WeaponId, TargetWeightProfile> weaponTargets,
        IReadOnlyList<ArmorId> armors,
        IReadOnlyDictionary<ShieldId, TargetWeightProfile> shieldMultipliers,
        IReadOnlyList<CombatLoadout> roster,
        ClashProfile? clashProfile = null)
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

        // Not a compile-time constant, so the parameter is nullable rather
        // than carrying ClashProfile.Neutral as a C# default. Optional at all
        // because existing named-argument constructions must keep compiling
        // untouched.
        ClashProfile = clashProfile ?? ClashProfile.Neutral;
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

        _effectiveWeights = BuildEffectiveWeightTables();
        ValidateResolvedTotals();
        ContentHash = ComputeContentHash();
    }

    public CombatPresetId Id { get; }

    public int Version { get; }

    public ulong ContentHash { get; }

    public TargetWeightProfile GeneralTargets => _generalTargets;

    /// <summary>
    /// The defensive-interception tuning data this ruleset resolves an
    /// accepted attack against. Every clash value is reached through this
    /// profile's own accessors, so there is one place a value lives.
    /// <see cref="ClashProfile.Neutral"/> when the constructor was given none.
    /// </summary>
    public ClashProfile ClashProfile { get; }

    public IReadOnlyList<CombatLoadout> Roster => _roster;

    /// <summary>
    /// Returns a copy of this ruleset carrying <paramref name="profile"/> and
    /// every other field unchanged.
    /// </summary>
    /// <remarks>
    /// This exists so that an injected ruleset is provably the preset except
    /// for its clash profile. Reassembling the six constructor arguments by
    /// hand would mean sixteen weapon-weight reads per weapon, twenty-six
    /// defense-multiplier reads, and a guessed armor list, because the armor
    /// set has no accessor yet is folded into the content hash. That guess
    /// happens to be faithful today only because <see cref="ArmorId"/> has one
    /// member.
    /// </remarks>
    /// <param name="profile">The clash profile the copy carries.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profile"/> is null.
    /// </exception>
    public CombatRuleset WithClashProfile(ClashProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new CombatRuleset(
            Id,
            Version,
            _generalTargets,
            _weaponTargets,
            _armors,
            _shieldMultipliers,
            _roster,
            profile);
    }

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
