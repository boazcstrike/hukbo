using System.Collections.Immutable;
using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;

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
    /// <summary>
    /// The constructor parameter roster validation blames. Spelled out because
    /// the validation runs in a helper where the parameter itself is out of
    /// scope, so <c>nameof</c> is unavailable.
    /// </summary>
    private const string ClashProfileParameterName = "clashProfile";

    /// <summary>
    /// The constructor parameter rank-level validation blames, mirroring
    /// <see cref="ClashProfileParameterName"/> above for the same reason:
    /// validation runs in a helper where the parameter itself is out of
    /// scope, so <c>nameof</c> is unavailable.
    /// </summary>
    private const string RankLevelsParameterName = "rankLevels";

    private readonly TargetWeightProfile _generalTargets;
    private readonly IReadOnlyDictionary<WeaponId, TargetWeightProfile> _weaponTargets;
    private readonly IReadOnlyList<ArmorId> _armors;
    private readonly IReadOnlyDictionary<ShieldId, TargetWeightProfile> _shieldMultipliers;
    private readonly IReadOnlyList<CombatLoadout> _roster;
    private readonly IReadOnlyDictionary<WeaponId, WeaponAttributes>? _weaponAttributes;
    private readonly ClashProfile? _declaredClashProfile;
    private readonly IReadOnlyDictionary<RankId, int>? _rankLevels;
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
        IReadOnlyDictionary<WeaponId, WeaponAttributes>? weaponAttributes = null,
        ClashProfile? clashProfile = null,
        IReadOnlyDictionary<RankId, int>? rankLevels = null)
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
        //
        // Kept in both forms deliberately. The public property is the single
        // read path for every clash value and never needs a null check, while
        // the nullable field is what ComputeContentHash tests to decide
        // whether this preset declared a profile at all.
        _declaredClashProfile = clashProfile;
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
        _weaponAttributes = weaponAttributes is null
            ? null
            : new Dictionary<WeaponId, WeaponAttributes>(weaponAttributes);
        _rankLevels = rankLevels is null
            ? null
            : new Dictionary<RankId, int>(rankLevels);

        ValidateWeaponAttributes();
        ValidateRankLevels();
        _effectiveWeights = BuildEffectiveWeightTables();
        ValidateResolvedTotals();
        ValidateClashProfileCoversTheRoster();
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

        // Both trailing parameters are named. Passing the profile positionally
        // would land it in the weapon-attribute slot, and carrying the
        // attributes forward is not optional: dropping them would silently
        // return a copy whose weapons had lost their per-weapon damage, reach,
        // and cooldown.
        return new CombatRuleset(
            Id,
            Version,
            _generalTargets,
            _weaponTargets,
            _armors,
            _shieldMultipliers,
            _roster,
            weaponAttributes: _weaponAttributes,
            clashProfile: profile,
            rankLevels: _rankLevels);
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

    /// <summary>
    /// Whether this preset declares per-weapon attributes at all. False for
    /// version 1, which predates them and whose warriors take damage, reach,
    /// and cooldown from the scenario instead. A preset that declares none
    /// hashes none, which is what keeps version 1's content hash — and
    /// therefore its replays — exactly where it was.
    /// </summary>
    public bool HasWeaponProfiles => _weaponAttributes is not null;

    /// <summary>
    /// Whether this preset declares a per-rank fighter level. False for
    /// versions 1 through 3, which predate rank and whose warriors all
    /// resolve to <see cref="Simulation.Scenario.PlaceholderFighterLevel"/>
    /// instead. A preset that declares none hashes none, which is what keeps
    /// versions 1 through 3's content hash — and therefore their replays —
    /// exactly where they were.
    /// </summary>
    public bool HasRankLevels => _rankLevels is not null;

    /// <summary>
    /// The fighter level this preset declares for <paramref name="rank"/>,
    /// bounding an active attack combination's maximum length alongside
    /// <see cref="WeaponProfile.ComboMaxSteps"/> exactly as
    /// <see cref="Simulation.Scenario.PlaceholderFighterLevel"/> does for a
    /// preset that declares none.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This preset declares no rank levels; check <see cref="HasRankLevels"/>
    /// first.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rank"/> has no declared level in this preset.
    /// </exception>
    public int ResolveLevel(RankId rank)
    {
        if (_rankLevels is null)
        {
            throw new InvalidOperationException(
                $"Combat preset {Id} declares no rank levels; check " +
                $"{nameof(HasRankLevels)} before resolving a level.");
        }

        if (!_rankLevels.TryGetValue(rank, out var level))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unknown rank identity for this combat ruleset.");
        }

        return level;
    }

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
    /// The grip a weapon is fought with, or <see langword="null"/> when this
    /// preset declares no attributes for that weapon. The non-throwing form
    /// of <see cref="ResolveWeaponGrip"/>, for a caller asking about a weapon
    /// that a given preset may simply not field — an older melee-only preset
    /// asked about a ranged weapon, for instance.
    /// </summary>
    public WeaponGrip? TryResolveWeaponGrip(WeaponId weapon) =>
        _weaponAttributes is not null &&
        _weaponAttributes.TryGetValue(weapon, out var attributes)
            ? attributes.Grip
            : null;

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

    /// <summary>
    /// Enforces the two configuration invariants a misconfigured rank-level
    /// table would otherwise express as an agent that resolves to a level
    /// nobody chose, or a level that cannot bound anything: every declared
    /// level is at least 1, and every rank the roster actually fields has a
    /// declared level. Mirrors <see cref="ValidateWeaponAttributes"/>.
    /// </summary>
    private void ValidateRankLevels()
    {
        if (_rankLevels is null)
        {
            return;
        }

        foreach (var rank in _rankLevels.Keys.OrderBy(id => (int)id))
        {
            var level = _rankLevels[rank];
            if (level < 1)
            {
                throw new ArgumentException(
                    $"Rank {rank} declares level {level}. Every declared " +
                    "level must be at least 1.",
                    RankLevelsParameterName);
            }
        }

        foreach (var loadout in _roster)
        {
            if (!_rankLevels.ContainsKey(loadout.Rank))
            {
                throw new ArgumentException(
                    $"Roster fields rank {loadout.Rank}, which has no " +
                    "declared level. A preset that declares rank levels " +
                    "must declare one for every rank its roster actually " +
                    "fields.",
                    RankLevelsParameterName);
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

    /// <summary>
    /// Fails construction if the clash profile cannot answer for a loadout
    /// this ruleset actually fields. Validated at the boundary rather than at
    /// the first attack, because a profile missing one roster weapon would
    /// otherwise throw part-way through a battle, on a tick that depends on
    /// which entity IDs happened to come into reach.
    /// </summary>
    private void ValidateClashProfileCoversTheRoster()
    {
        foreach (var defender in _roster)
        {
            foreach (var attacker in _roster)
            {
                try
                {
                    ClashProfile.ResolveWeaponIntercept(
                        defender.Weapon,
                        defender.Shield,
                        attacker.Weapon);
                    ClashProfile.ResolveHardShareBase(attacker.Weapon);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    throw new ArgumentException(
                        "The clash profile carries no value for roster " +
                        $"defender weapon {defender.Weapon} with shield " +
                        $"{defender.Shield} against attacker weapon " +
                        $"{attacker.Weapon}.",
                        ClashProfileParameterName,
                        exception);
                }
            }

            try
            {
                ClashProfile.ResolveVoid(defender.Weapon, defender.Shield);
                ClashProfile.ResolveHardShareMultiplier(defender.Weapon);
                ClashProfile.ResolveShieldIntercept(defender.Shield);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new ArgumentException(
                    "The clash profile carries no row for roster weapon " +
                    $"{defender.Weapon} with shield {defender.Shield}.",
                    ClashProfileParameterName,
                    exception);
            }
        }
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

        // Contributed only by a profile that declares ranged fields, on the
        // same terms as the weapon-attribute and clash-profile blocks below:
        // a melee profile mixes nothing here at all — not even a zero
        // count — which is what leaves every preset up to and including V4,
        // none of which declare a ranged weapon, at its pinned content hash.
        // Folding these three unconditionally would move every one of those
        // presets the moment a ranged tuning value changed, even though none
        // of them reference it. The flag itself is not folded: inside this
        // branch it is always true, so folding it would contribute a
        // constant and discriminate nothing; the branch is what records it.
        var isRangedDeclaration = profile.ProjectileSpeedRaw != 0 ||
            profile.StandoffDistanceRaw != 0 ||
            profile.FlightTickCeiling != 0;
        if (isRangedDeclaration)
        {
            Fnv1a.Add(
                ref hash,
                unchecked((ulong)(uint)profile.ProjectileSpeedRaw));
            Fnv1a.Add(
                ref hash,
                unchecked((ulong)(uint)profile.StandoffDistanceRaw));
            Fnv1a.Add(
                ref hash,
                unchecked((ulong)(uint)profile.FlightTickCeiling));
        }
    }

    /// <summary>
    /// Folds every clash tuning value into the content hash. Every table is
    /// read through the profile's ordered accessors, which sort by ascending
    /// enum value, so two rulesets carrying identical tuning data supplied in
    /// different dictionary order hash identically. Without that a replay
    /// would refuse a save that is in fact the same configuration.
    /// </summary>
    /// <remarks>
    /// Fold order per D3.1: roster (already folded above), weapon attributes
    /// (already folded above), weapon intercepts, shield scalar, void
    /// channels, hard-share rows. The weapon-intercept fold carries the
    /// defender's shield alongside the defender and attacker weapons — the
    /// key that stops the T13A hole, where a profile differing only in
    /// whether a cell describes a shielded or a bare defender would otherwise
    /// hash identically. The void channel folds separately from the
    /// hard-share rows because the void channel is keyed on
    /// (weapon, shield) while the hard-share tables stay weapon-keyed; the two
    /// no longer join into one row per weapon.
    /// </remarks>
    private void FoldClashProfile(ref ulong hash)
    {
        var cells = ClashProfile.OrderedWeaponIntercepts.ToArray();
        Fnv1a.Add(ref hash, (ulong)cells.Length);
        foreach (var (key, value) in cells)
        {
            Fnv1a.Add(ref hash, (ulong)key.Defender);
            Fnv1a.Add(ref hash, (ulong)key.DefenderShield);
            Fnv1a.Add(ref hash, (ulong)key.Attacker);
            Fnv1a.Add(ref hash, (ulong)value);
        }

        Fnv1a.Add(ref hash, (ulong)ClashProfile.ShieldInterceptBasisPoints);

        var voidCells = ClashProfile.OrderedVoidChannels.ToArray();
        Fnv1a.Add(ref hash, (ulong)voidCells.Length);
        foreach (var (key, value) in voidCells)
        {
            Fnv1a.Add(ref hash, (ulong)key.Weapon);
            Fnv1a.Add(ref hash, (ulong)key.Shield);
            Fnv1a.Add(ref hash, (ulong)value);
        }

        var hardShareRows = ClashProfile.OrderedHardShareRows.ToArray();
        Fnv1a.Add(ref hash, (ulong)hardShareRows.Length);
        foreach (var (weapon, hardShareBase, hardShareMultiplier) in hardShareRows)
        {
            Fnv1a.Add(ref hash, (ulong)weapon);
            Fnv1a.Add(ref hash, (ulong)hardShareBase);
            Fnv1a.Add(ref hash, (ulong)hardShareMultiplier);
        }

        Fnv1a.Add(ref hash, (ulong)ClashProfile.MinimumHardShareBasisPoints);
        Fnv1a.Add(ref hash, (ulong)ClashProfile.MaximumHardShareBasisPoints);
        Fnv1a.Add(ref hash, (ulong)ClashProfile.MaximumInterceptionBasisPoints);
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

            // Contributed only by a preset that declares rank levels, on the
            // same terms as the weapon-attribute and clash-profile blocks
            // below: a preset that does not declare levels (V1 through V3)
            // folds nothing extra per roster entry, which is what keeps its
            // content hash exactly where it was. Every warrior such a preset
            // fields resolves to the same RankId.Timawa default in any case,
            // so folding it unconditionally would only ever hash a constant.
            if (_rankLevels is not null)
            {
                Fnv1a.Add(ref hash, (ulong)loadout.Rank);
            }
        }

        // Both trailing blocks are contributed only by a preset that declares
        // the data, and the weapon-attribute block always precedes the clash
        // block. That order is arbitrary but fixed: reordering these two would
        // move every content hash without changing a single tuning value, so a
        // reorder requires a new preset version and fresh golden expectations
        // exactly as a retune would.

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

        // Contributed only by a preset that declares a clash profile, on the
        // same terms and for the same reason as the block above: preset V1 is
        // frozen and declares none, so nothing is mixed here at all — not even
        // a zero count — and its pinned content hash survives this feature
        // untouched. A ruleset that resolves every clash channel to zero
        // because it was given no profile and one that was handed
        // ClashProfile.Neutral explicitly are the same configuration, and they
        // hash the same.
        if (_declaredClashProfile is not null)
        {
            FoldClashProfile(ref hash);
        }

        // Contributed only by a preset that declares rank levels, on the
        // same terms as the two blocks above: a preset that declares none
        // (V1 through V3) mixes nothing here at all — not even a zero
        // count — which is what leaves its content hash untouched by this
        // feature.
        if (_rankLevels is not null)
        {
            var rankIds = _rankLevels.Keys.OrderBy(id => (int)id).ToArray();
            Fnv1a.Add(ref hash, (ulong)rankIds.Length);
            foreach (var rank in rankIds)
            {
                Fnv1a.Add(ref hash, (ulong)rank);
                Fnv1a.Add(ref hash, (ulong)_rankLevels[rank]);
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
