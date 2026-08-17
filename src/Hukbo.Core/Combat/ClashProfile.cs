namespace Hukbo.Core.Combat;

/// <summary>
/// Immutable defensive-interception tuning data: how often an accepted attack
/// is taken by the defender's shield, turned by the defender's weapon, or
/// stepped off the line entirely. All arithmetic is integer basis points out
/// of <see cref="BasisPointScale"/>; no fixed-point and no floating-point
/// value enters the clash path.
/// </summary>
/// <remarks>
/// <para>
/// <b>PROVISIONAL.</b> Every value carried by this type is a gameplay tuning
/// choice, not a historical measurement. The research is explicit that
/// <b>all sixteen legacy cells of the weapon-intercept matrix have no
/// evidentiary confidence whatsoever</b> — only their relative ordering is
/// argued, and weakly. The shield channel is the only defensive channel with
/// sixteenth-century documentary support. See
/// docs/research/WEAPON_CLASH_1500s.md and CLAUDE.md section 7.
/// </para>
/// <para>
/// The three channels are mutually exclusive and jointly exhaustive of the
/// defence: the chance a blow lands is one minus the sum of the shield,
/// weapon, and void channels. They are never summed on top of a separate base
/// clash probability.
/// </para>
/// <para>
/// The instance is built once as definition data, hashed into
/// <see cref="CombatRuleset.ContentHash"/>, and never mutated. It is not a
/// cache and there is nothing to invalidate.
/// </para>
/// <para>
/// <b>Coverage is not validated here.</b> Under D4, this type validates only
/// value ranges and internal consistency — never that a table covers every
/// weapon or every roster loadout. Roster coverage is validated by the only
/// type that knows what the roster is:
/// <see cref="CombatRuleset"/>'s <c>ValidateClashProfileCoversTheRoster</c>.
/// A missing cell surfaces there, at construction, naming the exact defender
/// weapon, defender shield, and attacker weapon.
/// </para>
/// </remarks>
public sealed class ClashProfile
{
    /// <summary>
    /// Denominator for every interception probability on this type. A cell of
    /// 2400 means 0.24.
    /// </summary>
    public const int BasisPointScale = 10_000;

    /// <summary>
    /// Denominator for <see cref="ResolveHardShareMultiplier"/>, which is a
    /// per-thousand scaling of a hard-share base rather than a probability.
    /// </summary>
    public const int HardShareMultiplierScale = 1_000;

    private const int MinimumValue = 0;
    private const int MaximumValue = BasisPointScale;
    private const int MinimumCeiling = 1;

    /// <summary>
    /// Keyed by (defending weapon, defending shield, attacking weapon), per
    /// D3. The defender's shield joins the key because preset V2 breaks the
    /// one-to-one weapon/shield correlation the original key assumed: Kalis
    /// and Itak each field both a solo and a shield-paired loadout, and the
    /// two resolve to materially different intercept values.
    /// </summary>
    private readonly IReadOnlyDictionary<
        (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>
        _weaponIntercept;

    /// <summary>
    /// Keyed by (defending weapon, defending shield), for the same reason.
    /// </summary>
    private readonly IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), int>
        _voidChannel;

    /// <summary>
    /// Keyed by attacking weapon alone. Unaffected by D3: the research drives
    /// the hard-versus-soft split from weapon identity — the mass and
    /// leverage of the blow — not from whether the defender also carries a
    /// shield, so a solo and a shield-paired loadout of the same weapon share
    /// one row without contradiction.
    /// </summary>
    private readonly IReadOnlyDictionary<WeaponId, int> _hardShareBases;

    /// <summary>Keyed by defending weapon alone, for the same reason.</summary>
    private readonly IReadOnlyDictionary<WeaponId, int> _hardShareMultipliers;

    /// <summary>
    /// PROVISIONAL, size-aware shield interception, added by V7. Keyed by
    /// shield identity: the flat basis points that shield's span alone
    /// intercepts before any weapon-bulk reduction, used by
    /// <see cref="ResolveShieldIntercept(ShieldId, WeaponId)"/>. Null for
    /// every preset through V6, which keeps declaring only the flat
    /// <see cref="ShieldInterceptBasisPoints"/> value. Null is the gate that
    /// keeps <see cref="CombatRuleset"/> folding nothing new into V1 through
    /// V6's content hash — see <see cref="DeclaresSizeAwareShieldIntercept"/>.
    /// </summary>
    private readonly IReadOnlyDictionary<ShieldId, int>? _shieldInterceptBaseBasisPoints;

    /// <summary>
    /// PROVISIONAL, size-aware shield interception, added by V7. Keyed by
    /// shield identity: that shield's physical span as a raw fixed-point
    /// value — world units multiplied by
    /// <see cref="Hukbo.Core.Mathematics.FixedPoint.Scale"/> — used as the
    /// denominator term in <see cref="ResolveShieldIntercept(ShieldId, WeaponId)"/>.
    /// Must be strictly positive for every non-<see cref="ShieldId.None"/>
    /// shield it declares: a shield with zero or negative span cannot
    /// meaningfully intercept anything. Null for every preset through V6.
    /// </summary>
    private readonly IReadOnlyDictionary<ShieldId, int>? _shieldSpanRaw;

    /// <summary>
    /// PROVISIONAL, size-aware shield interception, added by V7. Keyed by
    /// attacking weapon: that weapon's <see cref="WeaponProfile.ShieldDefeatBulkRaw"/>
    /// mirrored here so <see cref="ResolveShieldIntercept(ShieldId, WeaponId)"/>
    /// can read it without a back-reference into the weapon tables. Null for
    /// every preset through V6. A missing key resolves to zero bulk rather
    /// than throwing, matching a melee weapon's authored zero.
    /// </summary>
    private readonly IReadOnlyDictionary<WeaponId, int>? _shieldDefeatBulkRaw;

    /// <summary>
    /// True only for <see cref="Neutral"/>. Every weapon-keyed resolver
    /// short-circuits to zero for an unrecognised key rather than throwing,
    /// which is what lets <see cref="Neutral"/> answer for any weapon and
    /// shield combination without needing to know the roster — ClashProfile
    /// itself has no roster to enumerate. Every other profile keeps throwing
    /// on a missing key, because a real preset's coverage gaps must surface
    /// as a construction-time failure in
    /// <c>CombatRuleset.ValidateClashProfileCoversTheRoster</c>, not resolve
    /// silently to zero.
    /// </summary>
    private readonly bool _resolvesUnknownKeysToZero;

    /// <param name="weaponIntercept">
    /// Cells keyed by (defending weapon, defending shield, attacking weapon),
    /// in basis points. Coverage is validated by the caller, not here — see
    /// the type remarks.
    /// </param>
    /// <param name="shieldIntercept">
    /// Flat basis points intercepted by any shield other than
    /// <see cref="ShieldId.None"/>, across every attacker. One value rather
    /// than a per-attacker row: the research states plainly that the spread it
    /// suggests has no source behind it.
    /// </param>
    /// <param name="voidChannel">
    /// Basis points the defender evades outright, keyed by (defending weapon,
    /// defending shield).
    /// </param>
    /// <param name="hardShareBases">
    /// Share of the weapon channel that arrests rather than brushes, keyed by
    /// the <em>attacking</em> weapon, before the defender multiplier.
    /// </param>
    /// <param name="hardShareMultipliers">
    /// Per-thousand scaling of the hard share, keyed by the <em>defending</em>
    /// weapon. Divided by <see cref="HardShareMultiplierScale"/>.
    /// </param>
    /// <param name="minimumHardShareBasisPoints">
    /// Lower clamp on the resolved hard share.
    /// </param>
    /// <param name="maximumHardShareBasisPoints">
    /// Upper clamp on the resolved hard share. Must not be below the lower
    /// clamp.
    /// </param>
    /// <param name="maximumInterceptionBasisPoints">
    /// Ceiling on the summed shield, weapon, and void channels. A guard
    /// against a future tuning pass rather than a value the shipped tables
    /// reach. Must be at least one: a ceiling of zero would force every
    /// channel to zero and make the whole type inert.
    /// </param>
    /// <param name="shieldInterceptBaseBasisPoints">
    /// PROVISIONAL. Optional, size-aware shield interception added by V7:
    /// per-shield base basis points, keyed by <see cref="ShieldId"/>. Null
    /// (the default) declares nothing size-aware, and
    /// <see cref="ResolveShieldIntercept(ShieldId, WeaponId)"/> falls back to
    /// the flat <paramref name="shieldIntercept"/> value — the same fallback
    /// every preset through V6 already resolves to. A caller declaring this
    /// must also declare <paramref name="shieldSpanRaw"/>; either both are
    /// null or both are non-null.
    /// </param>
    /// <param name="shieldSpanRaw">
    /// PROVISIONAL. Optional, size-aware shield interception added by V7:
    /// per-shield span, as a raw fixed-point value, keyed by
    /// <see cref="ShieldId"/>. Must be strictly positive for every
    /// non-<see cref="ShieldId.None"/> key. Null (the default) declares
    /// nothing size-aware; see <paramref name="shieldInterceptBaseBasisPoints"/>.
    /// </param>
    /// <param name="shieldDefeatBulkRaw">
    /// PROVISIONAL. Optional, size-aware shield interception added by V7:
    /// per-attacker-weapon shield-defeat bulk, as a raw fixed-point value,
    /// keyed by <see cref="WeaponId"/>. Null (the default) is equivalent to
    /// every weapon declaring zero bulk. May be non-null even when
    /// <paramref name="shieldInterceptBaseBasisPoints"/> and
    /// <paramref name="shieldSpanRaw"/> are null; it is inert in that case
    /// because <see cref="ResolveShieldIntercept(ShieldId, WeaponId)"/> never
    /// reads it while the base and span tables are unset.
    /// </param>
    /// <exception cref="ArgumentNullException">Any table is null.</exception>
    /// <exception cref="ArgumentException">The clamp bounds are inverted.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any value falls outside its declared range.
    /// </exception>
    public ClashProfile(
        IReadOnlyDictionary<(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int> weaponIntercept,
        int shieldIntercept,
        IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), int> voidChannel,
        IReadOnlyDictionary<WeaponId, int> hardShareBases,
        IReadOnlyDictionary<WeaponId, int> hardShareMultipliers,
        int minimumHardShareBasisPoints,
        int maximumHardShareBasisPoints,
        int maximumInterceptionBasisPoints,
        IReadOnlyDictionary<ShieldId, int>? shieldInterceptBaseBasisPoints = null,
        IReadOnlyDictionary<ShieldId, int>? shieldSpanRaw = null,
        IReadOnlyDictionary<WeaponId, int>? shieldDefeatBulkRaw = null)
        : this(
            weaponIntercept,
            shieldIntercept,
            voidChannel,
            hardShareBases,
            hardShareMultipliers,
            minimumHardShareBasisPoints,
            maximumHardShareBasisPoints,
            maximumInterceptionBasisPoints,
            resolvesUnknownKeysToZero: false,
            shieldInterceptBaseBasisPoints,
            shieldSpanRaw,
            shieldDefeatBulkRaw)
    {
    }

    private ClashProfile(
        IReadOnlyDictionary<(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int> weaponIntercept,
        int shieldIntercept,
        IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), int> voidChannel,
        IReadOnlyDictionary<WeaponId, int> hardShareBases,
        IReadOnlyDictionary<WeaponId, int> hardShareMultipliers,
        int minimumHardShareBasisPoints,
        int maximumHardShareBasisPoints,
        int maximumInterceptionBasisPoints,
        bool resolvesUnknownKeysToZero,
        IReadOnlyDictionary<ShieldId, int>? shieldInterceptBaseBasisPoints = null,
        IReadOnlyDictionary<ShieldId, int>? shieldSpanRaw = null,
        IReadOnlyDictionary<WeaponId, int>? shieldDefeatBulkRaw = null)
    {
        ArgumentNullException.ThrowIfNull(weaponIntercept);
        ArgumentNullException.ThrowIfNull(voidChannel);
        ArgumentNullException.ThrowIfNull(hardShareBases);
        ArgumentNullException.ThrowIfNull(hardShareMultipliers);

        ValidateValues(weaponIntercept.Values, nameof(weaponIntercept));
        ValidateValues(voidChannel.Values, nameof(voidChannel));
        ValidateValues(hardShareBases.Values, nameof(hardShareBases));
        ValidateValues(hardShareMultipliers.Values, nameof(hardShareMultipliers));
        ValidateValue(shieldIntercept, nameof(shieldIntercept));
        ValidateValue(minimumHardShareBasisPoints, nameof(minimumHardShareBasisPoints));
        ValidateValue(maximumHardShareBasisPoints, nameof(maximumHardShareBasisPoints));

        if (maximumHardShareBasisPoints < minimumHardShareBasisPoints)
        {
            throw new ArgumentException(
                "The upper hard-share clamp must not be below the lower clamp.",
                nameof(maximumHardShareBasisPoints));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumInterceptionBasisPoints,
            MinimumCeiling);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            maximumInterceptionBasisPoints,
            MaximumValue);

        if ((shieldInterceptBaseBasisPoints is null) != (shieldSpanRaw is null))
        {
            throw new ArgumentException(
                $"{nameof(shieldInterceptBaseBasisPoints)} and " +
                $"{nameof(shieldSpanRaw)} must either both be declared or " +
                "both be null: a size-aware base without a span, or a span " +
                "without a base, cannot resolve an intercept.",
                nameof(shieldInterceptBaseBasisPoints));
        }

        if (shieldInterceptBaseBasisPoints is not null)
        {
            ValidateValues(
                shieldInterceptBaseBasisPoints.Values,
                nameof(shieldInterceptBaseBasisPoints));
        }

        if (shieldSpanRaw is not null)
        {
            foreach (var (shield, span) in shieldSpanRaw)
            {
                if (shield == ShieldId.None)
                {
                    continue;
                }

                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                    span,
                    0,
                    nameof(shieldSpanRaw));
            }
        }

        if (shieldDefeatBulkRaw is not null)
        {
            foreach (var bulk in shieldDefeatBulkRaw.Values)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(bulk, nameof(shieldDefeatBulkRaw));
            }
        }

        // Defensive copies. A caller retaining the dictionary it passed in
        // could otherwise mutate tuning data after construction while
        // CombatRuleset.ContentHash keeps reporting the value computed from
        // the original, letting resolution drift out of sync with the hash a
        // replay depends on.
        _weaponIntercept = new Dictionary<
            (WeaponId, ShieldId, WeaponId), int>(weaponIntercept);
        _voidChannel = new Dictionary<(WeaponId, ShieldId), int>(voidChannel);
        _hardShareBases = new Dictionary<WeaponId, int>(hardShareBases);
        _hardShareMultipliers = new Dictionary<WeaponId, int>(hardShareMultipliers);
        _resolvesUnknownKeysToZero = resolvesUnknownKeysToZero;
        _shieldInterceptBaseBasisPoints = shieldInterceptBaseBasisPoints is null
            ? null
            : new Dictionary<ShieldId, int>(shieldInterceptBaseBasisPoints);
        _shieldSpanRaw = shieldSpanRaw is null
            ? null
            : new Dictionary<ShieldId, int>(shieldSpanRaw);
        _shieldDefeatBulkRaw = shieldDefeatBulkRaw is null
            ? null
            : new Dictionary<WeaponId, int>(shieldDefeatBulkRaw);

        ShieldInterceptBasisPoints = shieldIntercept;
        MinimumHardShareBasisPoints = minimumHardShareBasisPoints;
        MaximumHardShareBasisPoints = maximumHardShareBasisPoints;
        MaximumInterceptionBasisPoints = maximumInterceptionBasisPoints;
    }

    /// <summary>
    /// The all-zero profile: no shield intercept, no weapon intercept, no
    /// void, so every accepted attack resolves to
    /// <see cref="AttackResolution.Landed"/>. It is the profile a
    /// <see cref="CombatRuleset"/> carries when its caller supplies none, and
    /// it is the profile the zero-interception control run injects to prove
    /// that the clash seam changed no pre-existing value.
    /// </summary>
    /// <remarks>
    /// Resolves to zero for any weapon or shield, not only a declared roster
    /// combination — see <see cref="_resolvesUnknownKeysToZero"/>. The clamp
    /// bounds and the ceiling are structural rather than tuning, so they keep
    /// legal values here: the ceiling may not be zero, and a zero-width
    /// hard-share window would be meaningless. With every channel at zero
    /// neither is ever consulted.
    /// </remarks>
    public static ClashProfile Neutral { get; } = new ClashProfile(
        weaponIntercept: new Dictionary<(WeaponId, ShieldId, WeaponId), int>(),
        shieldIntercept: 0,
        voidChannel: new Dictionary<(WeaponId, ShieldId), int>(),
        hardShareBases: new Dictionary<WeaponId, int>(),
        hardShareMultipliers: new Dictionary<WeaponId, int>(),
        minimumHardShareBasisPoints: MinimumValue,
        maximumHardShareBasisPoints: MaximumValue,
        maximumInterceptionBasisPoints: MaximumValue,
        resolvesUnknownKeysToZero: true);

    /// <summary>
    /// Flat basis points intercepted by any shield other than
    /// <see cref="ShieldId.None"/>.
    /// </summary>
    public int ShieldInterceptBasisPoints { get; }

    /// <summary>Lower clamp on the resolved hard share.</summary>
    public int MinimumHardShareBasisPoints { get; }

    /// <summary>Upper clamp on the resolved hard share.</summary>
    public int MaximumHardShareBasisPoints { get; }

    /// <summary>
    /// Ceiling on the summed shield, weapon, and void channels.
    /// </summary>
    public int MaximumInterceptionBasisPoints { get; }

    /// <summary>
    /// True when this profile declares the optional, size-aware shield
    /// tables added by V7. <see cref="CombatRuleset"/> uses this to gate the
    /// tables' contribution to <see cref="CombatRuleset.ContentHash"/>: false
    /// for every preset through V6, so those hashes fold nothing new and stay
    /// byte-identical to their recorded baselines.
    /// </summary>
    public bool DeclaresSizeAwareShieldIntercept =>
        _shieldInterceptBaseBasisPoints is not null;

    /// <summary>
    /// Basis points the defending weapon and shield intercept against one
    /// attacking weapon, before the hard and soft split.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The key is unknown to this profile and this is not
    /// <see cref="Neutral"/>.
    /// </exception>
    public int ResolveWeaponIntercept(
        WeaponId defenderWeapon,
        ShieldId defenderShield,
        WeaponId attackerWeapon)
    {
        if (_weaponIntercept.TryGetValue(
                (defenderWeapon, defenderShield, attackerWeapon),
                out var value))
        {
            return value;
        }

        if (_resolvesUnknownKeysToZero)
        {
            return 0;
        }

        throw new ArgumentOutOfRangeException(
            nameof(defenderWeapon),
            (defenderWeapon, defenderShield, attackerWeapon),
            "Unknown weapon pairing for this clash profile.");
    }

    /// <summary>
    /// Basis points the defender's shield intercepts.
    /// <see cref="ShieldId.None"/> intercepts nothing: that is structural, not
    /// a tuning value, because a shield the warrior does not carry cannot take
    /// a blow.
    /// </summary>
    public int ResolveShieldIntercept(ShieldId defenderShield) =>
        defenderShield == ShieldId.None ? 0 : ShieldInterceptBasisPoints;

    /// <summary>
    /// PROVISIONAL. Basis points the defender's shield intercepts against one
    /// attacking weapon, taking the shield's physical span and the weapon's
    /// shield-defeat bulk into account. <see cref="ShieldId.None"/>
    /// intercepts nothing, structurally, same as the flat overload. When this
    /// profile declares no size-aware tables (<see cref="_shieldInterceptBaseBasisPoints"/>
    /// or <see cref="_shieldSpanRaw"/> is null), this falls back to
    /// <see cref="ResolveShieldIntercept(ShieldId)"/> exactly — the behaviour
    /// every preset through V6 keeps.
    /// </summary>
    /// <remarks>
    /// Formula, all <see langword="long"/> arithmetic, truncating division,
    /// result clamped to <c>[0, <see cref="BasisPointScale"/>]</c>:
    /// <c>base(shield) * span(shield) / (span(shield) + bulk(attackerWeapon))</c>.
    /// A weapon with no declared bulk resolves to zero bulk, so the formula
    /// reduces to the flat base value for a melee attacker. No
    /// <see langword="float"/>, no <see langword="double"/>, no epsilon.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="defenderShield"/> is not <see cref="ShieldId.None"/>
    /// and this profile's size-aware tables declare no base or no span for
    /// it, and this is not <see cref="Neutral"/>.
    /// </exception>
    public int ResolveShieldIntercept(ShieldId defenderShield, WeaponId attackerWeapon)
    {
        if (defenderShield == ShieldId.None)
        {
            return 0;
        }

        if (_shieldInterceptBaseBasisPoints is null || _shieldSpanRaw is null)
        {
            return ResolveShieldIntercept(defenderShield);
        }

        if (!_shieldInterceptBaseBasisPoints.TryGetValue(defenderShield, out var baseIntercept))
        {
            if (_resolvesUnknownKeysToZero)
            {
                return 0;
            }

            throw new ArgumentOutOfRangeException(
                nameof(defenderShield),
                defenderShield,
                "This clash profile declares no size-aware shield intercept " +
                "base for this shield.");
        }

        if (!_shieldSpanRaw.TryGetValue(defenderShield, out var span))
        {
            if (_resolvesUnknownKeysToZero)
            {
                return 0;
            }

            throw new ArgumentOutOfRangeException(
                nameof(defenderShield),
                defenderShield,
                "This clash profile declares no size-aware shield span for " +
                "this shield.");
        }

        var bulk = _shieldDefeatBulkRaw is not null &&
            _shieldDefeatBulkRaw.TryGetValue(attackerWeapon, out var declaredBulk)
                ? declaredBulk
                : 0;

        long numerator = (long)baseIntercept * span;
        long denominator = (long)span + bulk;
        long result = numerator / denominator;

        return (int)Math.Clamp(result, MinimumValue, MaximumValue);
    }

    /// <summary>
    /// Basis points the defender evades outright, by defending weapon and
    /// shield.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The key is unknown to this profile and this is not
    /// <see cref="Neutral"/>.
    /// </exception>
    public int ResolveVoid(WeaponId defenderWeapon, ShieldId defenderShield)
    {
        if (_voidChannel.TryGetValue((defenderWeapon, defenderShield), out var value))
        {
            return value;
        }

        if (_resolvesUnknownKeysToZero)
        {
            return 0;
        }

        throw new ArgumentOutOfRangeException(
            nameof(defenderWeapon),
            (defenderWeapon, defenderShield),
            "Unknown weapon and shield pairing for this clash profile.");
    }

    /// <summary>
    /// Hard-share base for one attacking weapon, before the defender
    /// multiplier and before the clamp.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The weapon is unknown to this profile and this is not
    /// <see cref="Neutral"/>.
    /// </exception>
    public int ResolveHardShareBase(WeaponId attackerWeapon) =>
        ResolveWeaponKeyed(_hardShareBases, attackerWeapon, nameof(attackerWeapon));

    /// <summary>
    /// Per-thousand hard-share multiplier for one defending weapon.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The weapon is unknown to this profile and this is not
    /// <see cref="Neutral"/>.
    /// </exception>
    public int ResolveHardShareMultiplier(WeaponId defenderWeapon) =>
        ResolveWeaponKeyed(_hardShareMultipliers, defenderWeapon, nameof(defenderWeapon));

    /// <summary>
    /// Every (defending weapon, defending shield, attacking weapon) cell in
    /// ascending key order, so a caller folding this profile into a content
    /// hash cannot inherit the supply order of the dictionary it was built
    /// from. The defender's shield takes part in the ordering, per D3.1: an
    /// ordering that ignored it could not distinguish a profile where only a
    /// shielded-versus-bare cell differs.
    /// </summary>
    internal IEnumerable<
        ((WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker) Key, int Value)>
        OrderedWeaponIntercepts =>
        _weaponIntercept
            .OrderBy(entry => (int)entry.Key.Defender)
            .ThenBy(entry => (int)entry.Key.DefenderShield)
            .ThenBy(entry => (int)entry.Key.Attacker)
            .Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// Every (defending weapon, defending shield) void-channel cell in
    /// ascending key order. Separated from <see cref="OrderedHardShareRows"/>
    /// per D3.1: once the void channel is keyed on shield as well as weapon
    /// while the hard-share tables stay weapon-keyed, the two no longer join
    /// into one row per weapon.
    /// </summary>
    internal IEnumerable<((WeaponId Weapon, ShieldId Shield) Key, int Value)>
        OrderedVoidChannels =>
        _voidChannel
            .OrderBy(entry => (int)entry.Key.Weapon)
            .ThenBy(entry => (int)entry.Key.Shield)
            .Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// The hard-share base and multiplier row for one weapon, in ascending
    /// weapon order. Both tables stay weapon-keyed under D3.1: the research
    /// drives the hard-versus-soft split from weapon identity alone, not from
    /// whether the defender also carries a shield.
    /// </summary>
    internal IEnumerable<(WeaponId Weapon, int HardShareBase, int HardShareMultiplier)>
        OrderedHardShareRows =>
        _hardShareBases
            .Keys
            .OrderBy(weapon => (int)weapon)
            .Select(weapon => (
                weapon,
                _hardShareBases[weapon],
                _hardShareMultipliers.TryGetValue(weapon, out var multiplier)
                    ? multiplier
                    : 0));

    /// <summary>
    /// Every (shield, base basis points) cell of the size-aware shield table
    /// in ascending shield order. Empty when
    /// <see cref="DeclaresSizeAwareShieldIntercept"/> is false, so a caller
    /// folding this into a content hash folds nothing for V1 through V6.
    /// </summary>
    internal IEnumerable<(ShieldId Shield, int Value)> OrderedShieldInterceptBases =>
        _shieldInterceptBaseBasisPoints is null
            ? []
            : _shieldInterceptBaseBasisPoints
                .OrderBy(entry => (int)entry.Key)
                .Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// Every (shield, span raw) cell of the size-aware shield table in
    /// ascending shield order. Empty when
    /// <see cref="DeclaresSizeAwareShieldIntercept"/> is false.
    /// </summary>
    internal IEnumerable<(ShieldId Shield, int Value)> OrderedShieldSpans =>
        _shieldSpanRaw is null
            ? []
            : _shieldSpanRaw
                .OrderBy(entry => (int)entry.Key)
                .Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// Every (weapon, shield-defeat bulk raw) cell in ascending weapon order.
    /// Empty when the table is null, which includes every preset through V6
    /// and any V7+ preset that declares no bulk table of its own.
    /// </summary>
    internal IEnumerable<(WeaponId Weapon, int Value)> OrderedShieldDefeatBulks =>
        _shieldDefeatBulkRaw is null
            ? []
            : _shieldDefeatBulkRaw
                .OrderBy(entry => (int)entry.Key)
                .Select(entry => (entry.Key, entry.Value));

    private int ResolveWeaponKeyed(
        IReadOnlyDictionary<WeaponId, int> table,
        WeaponId weapon,
        string parameterName)
    {
        if (table.TryGetValue(weapon, out var value))
        {
            return value;
        }

        if (_resolvesUnknownKeysToZero)
        {
            return 0;
        }

        throw new ArgumentOutOfRangeException(
            parameterName,
            weapon,
            "Unknown weapon identity for this clash profile.");
    }

    private static void ValidateValues(IEnumerable<int> values, string parameterName)
    {
        foreach (var value in values)
        {
            ValidateValue(value, parameterName);
        }
    }

    private static void ValidateValue(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, MinimumValue, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumValue, parameterName);
    }
}
