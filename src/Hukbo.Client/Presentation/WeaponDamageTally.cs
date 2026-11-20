using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One tally row: a (faction, weapon, shield) combination, its damage after
/// armour, the blows that landed, and how many of its warriors are alive.
/// </summary>
internal readonly record struct WeaponDamageTallyRow(
    int FactionId,
    WeaponId Weapon,
    ShieldId Shield,
    string Label,
    long Damage,
    int BlowsLanded,
    int Alive,
    int Fielded);

/// <summary>
/// The weapon damage tally behind the damage card: damage dealt, blows
/// landed, and living count per faction, weapon, and shield. Owned by
/// <c>PresentationCoordinator</c> and fed once per tick from <c>IngestTick</c>
/// with the raw per-tick event list, never through <c>BattleEventFeed</c>,
/// which truncates to its last 200 entries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Damage is after armour.</b> The weapon damage tally design's section
/// 3.1 check (<c>WeaponDamageArmorEqualityTests</c>) showed that applying
/// <see cref="CombatRuleset.ApplyArmorMitigation"/> to every landed
/// <see cref="BattleEventKind.Attack"/>, with the target's armour and the
/// event's hit location, reproduces each tick's
/// <see cref="BattleEventKind.Damage"/> total per target exactly. The tally
/// therefore applies the ruleset's own rule and never a rule of its own.
/// Damage still includes overkill, because the event stream does not carry a
/// target's health before each blow.
/// </para>
/// <para>
/// A landed blow whose target is absent from the agent list of the same
/// ingest call is ignored entirely, because its armour is unknown. The
/// simulation always lists every agent, living or dead, so this is a guard
/// for callers that pass a partial list.
/// </para>
/// <para>
/// Storage is a set of fixed arrays indexed by faction, weapon, and shield,
/// sized from the enums, so no dictionary iteration order decides any
/// result and <see cref="Ingest"/> allocates nothing once the armour lookup
/// has seen the whole roster.
/// </para>
/// </remarks>
internal sealed class WeaponDamageTally
{
    public const int FactionCount = 2;

    private static readonly WeaponId[] Weapons = Enum.GetValues<WeaponId>();

    private static readonly ShieldId[] Shields = Enum.GetValues<ShieldId>();

    private static readonly int WeaponSlots = Weapons.Max(weapon => (int)weapon) + 1;

    private static readonly int ShieldSlots = Shields.Max(shield => (int)shield) + 1;

    /// <summary>
    /// The most rows <see cref="GetRows"/> can return for one faction: one
    /// per weapon and shield combination.
    /// </summary>
    public static int MaximumRowsPerFaction { get; } = Weapons.Length * Shields.Length;

    private readonly CombatRuleset _rules;
    private readonly Dictionary<ulong, ArmorId> _armorByEntity = [];
    private readonly long[] _damage;
    private readonly int[] _blows;
    private readonly int[] _alive;
    private readonly int[] _fielded;
    private readonly string?[] _labels;

    /// <param name="rules">
    /// The combat ruleset the battle runs under. Its
    /// <see cref="CombatRuleset.ApplyArmorMitigation"/> is the only damage
    /// rule the tally uses, so it must be the same ruleset the simulation
    /// resolves with.
    /// </param>
    public WeaponDamageTally(CombatRuleset rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        var slotCount = FactionCount * WeaponSlots * ShieldSlots;
        _damage = new long[slotCount];
        _blows = new int[slotCount];
        _alive = new int[slotCount];
        _fielded = new int[slotCount];
        _labels = new string?[slotCount];
    }

    /// <summary>
    /// Folds one tick into the tally. Never retains either list beyond this
    /// call: the simulation double-buffers its event list.
    /// </summary>
    /// <param name="events">The full event list of one completed tick.</param>
    /// <param name="agents">
    /// Every agent's view for that tick. Supplies each target's armour and
    /// the per-row living and fielded counts, which are recounted on every
    /// call rather than accumulated.
    /// </param>
    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        RecountRoster(agents);
        for (var index = 0; index < events.Count; index++)
        {
            ProcessEvent(events[index]);
        }
    }

    /// <summary>
    /// Clears every counter and the armour lookup, for a new battle.
    /// </summary>
    public void Reset()
    {
        _armorByEntity.Clear();
        Array.Clear(_damage);
        Array.Clear(_blows);
        Array.Clear(_alive);
        Array.Clear(_fielded);
    }

    /// <summary>
    /// Writes the rows of one faction into <paramref name="destination"/>,
    /// sorted by damage, highest first, with equal damage ordered by label
    /// (ordinal). A combination appears only once it has a fielded warrior
    /// or a landed blow.
    /// </summary>
    /// <param name="destination">
    /// At least <see cref="MaximumRowsPerFaction"/> long.
    /// </param>
    /// <returns>The number of rows written.</returns>
    public int GetRows(int factionId, Span<WeaponDamageTallyRow> destination)
    {
        if ((uint)factionId >= FactionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(factionId), factionId, null);
        }

        if (destination.Length < MaximumRowsPerFaction)
        {
            throw new ArgumentException(
                $"Destination must hold at least {MaximumRowsPerFaction} rows.",
                nameof(destination));
        }

        var count = 0;
        foreach (var weapon in Weapons)
        {
            foreach (var shield in Shields)
            {
                var slot = SlotOf(factionId, weapon, shield);
                if (_fielded[slot] == 0 && _blows[slot] == 0)
                {
                    continue;
                }

                destination[count] = new WeaponDamageTallyRow(
                    factionId,
                    weapon,
                    shield,
                    LabelOf(slot, weapon, shield),
                    _damage[slot],
                    _blows[slot],
                    _alive[slot],
                    _fielded[slot]);
                count++;
            }
        }

        SortByDamageThenLabel(destination[..count]);
        return count;
    }

    /// <summary>
    /// The faction's total damage, the denominator of a row's share.
    /// </summary>
    public long GetFactionDamage(int factionId)
    {
        if ((uint)factionId >= FactionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(factionId), factionId, null);
        }

        long total = 0;
        var start = SlotAt(factionId, 0, 0);
        for (var offset = 0; offset < WeaponSlots * ShieldSlots; offset++)
        {
            total += _damage[start + offset];
        }

        return total;
    }

    private void RecountRoster(IReadOnlyList<AgentView> agents)
    {
        Array.Clear(_alive);
        Array.Clear(_fielded);
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _armorByEntity[agent.EntityId] = agent.Loadout.Armor;
            if (!IsTracked(agent.FactionId, agent.Loadout.Weapon, agent.Loadout.Shield))
            {
                continue;
            }

            var slot = SlotOf(agent.FactionId, agent.Loadout.Weapon, agent.Loadout.Shield);
            _fielded[slot]++;
            if (agent.IsAlive)
            {
                _alive[slot]++;
            }
        }
    }

    private void ProcessEvent(BattleEvent battleEvent)
    {
        if (battleEvent.Kind != BattleEventKind.Attack ||
            battleEvent.Resolution != AttackResolution.Landed ||
            battleEvent.FactionId is not { } factionId ||
            battleEvent.Weapon is not { } weapon ||
            battleEvent.Shield is not { } shield ||
            battleEvent.HitLocation is not { } hitLocation ||
            battleEvent.TargetEntityId is not { } targetId ||
            !IsTracked(factionId, weapon, shield) ||
            !_armorByEntity.TryGetValue(targetId, out var armor))
        {
            return;
        }

        var slot = SlotOf(factionId, weapon, shield);
        _blows[slot]++;
        _damage[slot] += _rules.ApplyArmorMitigation(battleEvent.Value, armor, hitLocation);
    }

    private static bool IsTracked(int factionId, WeaponId weapon, ShieldId shield) =>
        (uint)factionId < FactionCount &&
        (uint)weapon < (uint)WeaponSlots &&
        (uint)shield < (uint)ShieldSlots;

    private static int SlotOf(int factionId, WeaponId weapon, ShieldId shield) =>
        SlotAt(factionId, (int)weapon, (int)shield);

    private static int SlotAt(int factionId, int weapon, int shield) =>
        ((factionId * WeaponSlots) + weapon) * ShieldSlots + shield;

    // The label is computed once per slot: GetWeaponLabel builds a string.
    private string LabelOf(int slot, WeaponId weapon, ShieldId shield) =>
        _labels[slot] ??= BattleEventFormatter.GetWeaponLabel(weapon, shield);

    // Insertion sort: at most MaximumRowsPerFaction rows, and no delegate.
    private static void SortByDamageThenLabel(Span<WeaponDamageTallyRow> rows)
    {
        for (var index = 1; index < rows.Length; index++)
        {
            var row = rows[index];
            var position = index - 1;
            while (position >= 0 && Precedes(row, rows[position]))
            {
                rows[position + 1] = rows[position];
                position--;
            }

            rows[position + 1] = row;
        }
    }

    private static bool Precedes(WeaponDamageTallyRow candidate, WeaponDamageTallyRow other) =>
        candidate.Damage != other.Damage
            ? candidate.Damage > other.Damage
            : string.CompareOrdinal(candidate.Label, other.Label) < 0;
}
