using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

public enum BattleEventKind
{
    Move = 0,
    Attack = 1,
    Damage = 2,
    Death = 3,
    Outcome = 4,
}

/// <summary>
/// One authoritative simulation event. <see cref="Weapon"/>,
/// <see cref="Shield"/>, and <see cref="HitLocation"/> are populated only for
/// <see cref="Kind"/> <see cref="BattleEventKind.Attack"/>; every other kind
/// carries all three as <c>null</c>. Construct instances only through
/// <see cref="Attack"/> or <see cref="NonAttack"/>; both validate the
/// combat-context invariant.
/// </summary>
/// <remarks>
/// Record structs always expose an implicit public parameterless
/// constructor in addition to any declared constructor, so
/// <c>default(BattleEvent)</c> bypasses this validation. That default
/// value is never a valid authoritative event and must not be produced by
/// simulation or presentation code.
/// </remarks>
public readonly record struct BattleEvent
{
    private BattleEvent(
        long sequence,
        long tick,
        BattleEventKind kind,
        ulong sourceEntityId,
        ulong? targetEntityId,
        int value,
        int? factionId,
        WeaponId? weapon,
        ShieldId? shield,
        BodyPart? hitLocation)
    {
        Sequence = sequence;
        Tick = tick;
        Kind = kind;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        Value = value;
        FactionId = factionId;
        _combatContext = weapon is { } presentWeapon
            ? ((int)presentWeapon << WeaponShift) |
                ((int)shield!.Value << ShieldShift) |
                (int)hitLocation!.Value
            : CombatContextAbsent;
    }

    private const int CombatContextAbsent = 0;
    private const int WeaponShift = 16;
    private const int ShieldShift = 8;
    private const int FieldMask = 0xFF;

    /// <summary>
    /// <see cref="Weapon"/>, <see cref="Shield"/>, and
    /// <see cref="HitLocation"/> packed into one field, or
    /// <see cref="CombatContextAbsent"/> for an event that carries no combat
    /// context.
    /// </summary>
    /// <remarks>
    /// Three separate nullable enum fields cost eight bytes each and are the
    /// bulk of per-tick allocation, which
    /// <c>RepeatedCollisionTicksHaveBoundedAllocations</c> budgets. Packed
    /// this way the three together cost four bytes, so adding the shield made
    /// the event smaller than it was with only two of them.
    /// <para>
    /// Every one of the three enums starts numbering at one and none exceeds
    /// 255, so a byte apiece is sufficient and a zero weapon field is an
    /// unambiguous "absent" marker rather than a real value. A test pins that
    /// range assumption.
    /// </para>
    /// </remarks>
    private readonly int _combatContext;

    public long Sequence { get; }

    public long Tick { get; }

    public BattleEventKind Kind { get; }

    public ulong SourceEntityId { get; }

    public ulong? TargetEntityId { get; }

    public int Value { get; }

    public int? FactionId { get; }

    /// <summary>
    /// The attacking weapon. Populated only for <see cref="BattleEventKind.Attack"/>.
    /// </summary>
    public WeaponId? Weapon =>
        _combatContext == CombatContextAbsent
            ? null
            : (WeaponId)((_combatContext >> WeaponShift) & FieldMask);

    /// <summary>
    /// The shield the <em>attacker</em> was carrying, which is what decides
    /// which of the weapon's profiles produced this blow. Populated only for
    /// <see cref="BattleEventKind.Attack"/>.
    /// </summary>
    /// <remarks>
    /// Carried on the event rather than looked up later because a feed line
    /// is read long after the tick that produced it, and because loadout
    /// assignment depends on the scenario's roster counts — so there is no
    /// reliable way to recover it from an entity ID alone. Without it, the
    /// same weapon label would mean either of two different damage values.
    /// </remarks>
    public ShieldId? Shield =>
        _combatContext == CombatContextAbsent
            ? null
            : (ShieldId)((_combatContext >> ShieldShift) & FieldMask);

    /// <summary>
    /// The resolved body part struck. Populated only for
    /// <see cref="BattleEventKind.Attack"/>.
    /// </summary>
    public BodyPart? HitLocation =>
        _combatContext == CombatContextAbsent
            ? null
            : (BodyPart)(_combatContext & FieldMask);

    /// <summary>
    /// Creates a validated <see cref="BattleEventKind.Attack"/> event.
    /// <paramref name="weapon"/>, <paramref name="shield"/>, and
    /// <paramref name="hitLocation"/> are all required and must be defined
    /// enum values.
    /// </summary>
    public static BattleEvent Attack(
        long sequence,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        int factionId,
        WeaponId weapon,
        ShieldId shield,
        BodyPart hitLocation)
    {
        if (targetEntityId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetEntityId),
                targetEntityId,
                "An attack event requires a nonzero target entity ID.");
        }

        if (!Enum.IsDefined(weapon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "An attack event requires a defined weapon.");
        }

        if (!Enum.IsDefined(shield))
        {
            throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                "An attack event requires a defined shield.");
        }

        if (!Enum.IsDefined(hitLocation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hitLocation),
                hitLocation,
                "An attack event requires a defined hit location.");
        }

        return new BattleEvent(
            sequence,
            tick,
            BattleEventKind.Attack,
            sourceEntityId,
            targetEntityId,
            damage,
            factionId,
            weapon,
            shield,
            hitLocation);
    }

    /// <summary>
    /// Creates a validated event for every kind other than
    /// <see cref="BattleEventKind.Attack"/>. Rejects
    /// <see cref="BattleEventKind.Attack"/>, since only an
    /// <see cref="Attack"/> event may carry combat context.
    /// </summary>
    public static BattleEvent NonAttack(
        long sequence,
        long tick,
        BattleEventKind kind,
        ulong sourceEntityId,
        ulong? targetEntityId,
        int value,
        int? factionId)
    {
        if (kind == BattleEventKind.Attack)
        {
            throw new ArgumentException(
                "Use BattleEvent.Attack to construct Attack events.",
                nameof(kind));
        }

        return new BattleEvent(
            sequence,
            tick,
            kind,
            sourceEntityId,
            targetEntityId,
            value,
            factionId,
            weapon: null,
            shield: null,
            hitLocation: null);
    }
}
