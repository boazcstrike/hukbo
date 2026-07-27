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
/// <b>all sixteen cells of the weapon-intercept matrix have no evidentiary
/// confidence whatsoever</b> — only their relative ordering is argued, and
/// weakly. The shield channel is the only defensive channel with
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

    private readonly IReadOnlyDictionary<(WeaponId Defender, WeaponId Attacker), int>
        _weaponIntercept;

    private readonly IReadOnlyDictionary<WeaponId, int> _voidChannel;
    private readonly IReadOnlyDictionary<WeaponId, int> _hardShareBases;
    private readonly IReadOnlyDictionary<WeaponId, int> _hardShareMultipliers;

    /// <param name="weaponIntercept">
    /// Sixteen cells keyed by (defending weapon, attacking weapon), in basis
    /// points. Every ordered pair of declared weapons is required.
    /// </param>
    /// <param name="shieldIntercept">
    /// Flat basis points intercepted by any shield other than
    /// <see cref="ShieldId.None"/>, across every attacker. One value rather
    /// than a per-attacker row: the research states plainly that the spread it
    /// suggests has no source behind it.
    /// </param>
    /// <param name="voidChannel">
    /// Basis points the defender evades outright, keyed by defending weapon.
    /// Keyed on the weapon rather than on the loadout pair because weapon and
    /// shield are correlated one to one in the shipped roster.
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
    /// <exception cref="ArgumentNullException">Any table is null.</exception>
    /// <exception cref="ArgumentException">
    /// A table omits a declared weapon, names an undeclared one, or the clamp
    /// bounds are inverted.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any value falls outside its declared range.
    /// </exception>
    public ClashProfile(
        IReadOnlyDictionary<(WeaponId Defender, WeaponId Attacker), int> weaponIntercept,
        int shieldIntercept,
        IReadOnlyDictionary<WeaponId, int> voidChannel,
        IReadOnlyDictionary<WeaponId, int> hardShareBases,
        IReadOnlyDictionary<WeaponId, int> hardShareMultipliers,
        int minimumHardShareBasisPoints,
        int maximumHardShareBasisPoints,
        int maximumInterceptionBasisPoints)
    {
        ArgumentNullException.ThrowIfNull(weaponIntercept);
        ArgumentNullException.ThrowIfNull(voidChannel);
        ArgumentNullException.ThrowIfNull(hardShareBases);
        ArgumentNullException.ThrowIfNull(hardShareMultipliers);

        ValidateMatrix(weaponIntercept);
        ValidateWeaponTable(voidChannel, nameof(voidChannel));
        ValidateWeaponTable(hardShareBases, nameof(hardShareBases));
        ValidateWeaponTable(hardShareMultipliers, nameof(hardShareMultipliers));
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

        // Defensive copies. A caller retaining the dictionary it passed in
        // could otherwise mutate tuning data after construction while
        // CombatRuleset.ContentHash keeps reporting the value computed from
        // the original, letting resolution drift out of sync with the hash a
        // replay depends on.
        _weaponIntercept =
            new Dictionary<(WeaponId, WeaponId), int>(weaponIntercept);
        _voidChannel = new Dictionary<WeaponId, int>(voidChannel);
        _hardShareBases = new Dictionary<WeaponId, int>(hardShareBases);
        _hardShareMultipliers = new Dictionary<WeaponId, int>(hardShareMultipliers);

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
    /// The clamp bounds and the ceiling are structural rather than tuning, so
    /// they keep legal values here: the ceiling may not be zero, and a
    /// zero-width hard-share window would be meaningless. With every channel
    /// at zero neither is ever consulted.
    /// </remarks>
    public static ClashProfile Neutral { get; } = CreateNeutral();

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
    /// Basis points the defending weapon intercepts against one attacking
    /// weapon, before the hard and soft split.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either weapon is unknown to this profile.
    /// </exception>
    public int ResolveWeaponIntercept(WeaponId defenderWeapon, WeaponId attackerWeapon)
    {
        if (_weaponIntercept.TryGetValue((defenderWeapon, attackerWeapon), out var value))
        {
            return value;
        }

        throw new ArgumentOutOfRangeException(
            nameof(defenderWeapon),
            (defenderWeapon, attackerWeapon),
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
    /// Basis points the defender evades outright, by defending weapon.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The weapon is unknown to this profile.
    /// </exception>
    public int ResolveVoid(WeaponId defenderWeapon) =>
        Resolve(_voidChannel, defenderWeapon, nameof(defenderWeapon));

    /// <summary>
    /// Hard-share base for one attacking weapon, before the defender
    /// multiplier and before the clamp.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The weapon is unknown to this profile.
    /// </exception>
    public int ResolveHardShareBase(WeaponId attackerWeapon) =>
        Resolve(_hardShareBases, attackerWeapon, nameof(attackerWeapon));

    /// <summary>
    /// Per-thousand hard-share multiplier for one defending weapon.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The weapon is unknown to this profile.
    /// </exception>
    public int ResolveHardShareMultiplier(WeaponId defenderWeapon) =>
        Resolve(_hardShareMultipliers, defenderWeapon, nameof(defenderWeapon));

    /// <summary>
    /// Every (defending weapon, attacking weapon) cell in ascending key order,
    /// so a caller folding this profile into a content hash cannot inherit the
    /// supply order of the dictionary it was built from.
    /// </summary>
    internal IEnumerable<((WeaponId Defender, WeaponId Attacker) Key, int Value)>
        OrderedWeaponIntercepts =>
        _weaponIntercept
            .OrderBy(entry => (int)entry.Key.Defender)
            .ThenBy(entry => (int)entry.Key.Attacker)
            .Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// The void, hard-share base, and hard-share multiplier rows for one
    /// weapon, in ascending weapon order, for the same reason.
    /// </summary>
    internal IEnumerable<(WeaponId Weapon, int Void, int HardShareBase, int HardShareMultiplier)>
        OrderedWeaponRows =>
        _voidChannel
            .Keys
            .OrderBy(weapon => (int)weapon)
            .Select(weapon => (
                weapon,
                _voidChannel[weapon],
                _hardShareBases[weapon],
                _hardShareMultipliers[weapon]));

    private static ClashProfile CreateNeutral()
    {
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId, WeaponId), int>();
        var rows = new Dictionary<WeaponId, int>();

        foreach (var defender in weapons)
        {
            rows[defender] = 0;
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = 0;
            }
        }

        return new ClashProfile(
            weaponIntercept: matrix,
            shieldIntercept: 0,
            voidChannel: rows,
            hardShareBases: rows,
            hardShareMultipliers: rows,
            minimumHardShareBasisPoints: MinimumValue,
            maximumHardShareBasisPoints: MaximumValue,
            maximumInterceptionBasisPoints: MaximumValue);
    }

    private static int Resolve(
        IReadOnlyDictionary<WeaponId, int> table,
        WeaponId weapon,
        string parameterName)
    {
        if (table.TryGetValue(weapon, out var value))
        {
            return value;
        }

        throw new ArgumentOutOfRangeException(
            parameterName,
            weapon,
            "Unknown weapon identity for this clash profile.");
    }

    private static void ValidateMatrix(
        IReadOnlyDictionary<(WeaponId Defender, WeaponId Attacker), int> matrix)
    {
        var weapons = Enum.GetValues<WeaponId>();
        var expected = weapons.Length * weapons.Length;
        if (matrix.Count != expected)
        {
            throw new ArgumentException(
                $"The weapon-intercept matrix requires exactly {expected} cells, " +
                $"one per ordered weapon pair, but carries {matrix.Count}.",
                nameof(matrix));
        }

        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                if (!matrix.TryGetValue((defender, attacker), out var value))
                {
                    throw new ArgumentException(
                        "The weapon-intercept matrix omits the cell for " +
                        $"defender {defender} against attacker {attacker}.",
                        nameof(matrix));
                }

                ValidateValue(value, nameof(matrix));
            }
        }
    }

    private static void ValidateWeaponTable(
        IReadOnlyDictionary<WeaponId, int> table,
        string parameterName)
    {
        var weapons = Enum.GetValues<WeaponId>();
        if (table.Count != weapons.Length)
        {
            throw new ArgumentException(
                $"'{parameterName}' requires exactly one value per declared " +
                $"weapon, {weapons.Length} in total, but carries {table.Count}.",
                parameterName);
        }

        foreach (var weapon in weapons)
        {
            if (!table.TryGetValue(weapon, out var value))
            {
                throw new ArgumentException(
                    $"'{parameterName}' omits weapon {weapon}.",
                    parameterName);
            }

            ValidateValue(value, parameterName);
        }
    }

    private static void ValidateValue(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, MinimumValue, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumValue, parameterName);
    }
}
