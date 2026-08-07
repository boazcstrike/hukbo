using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

public enum BattleEventKind
{
    Move = 0,
    Attack = 1,
    Damage = 2,
    Death = 3,
    Outcome = 4,

    /// <summary>
    /// A ranged shot has left its launcher. Non-attack: constructed only
    /// through <see cref="BattleEvent.NonAttack"/>, so
    /// <see cref="BattleEvent.Weapon"/>, <see cref="BattleEvent.Shield"/>,
    /// <see cref="BattleEvent.HitLocation"/>, and
    /// <see cref="BattleEvent.Resolution"/> are always <c>null</c>.
    /// <see cref="BattleEvent.Value"/> carries the flight time in ticks. No
    /// emission site exists yet.
    /// </summary>
    Release = 5,

    /// <summary>
    /// A ranged shot spent itself without landing. Non-attack: constructed
    /// only through <see cref="BattleEvent.NonAttack"/>, so
    /// <see cref="BattleEvent.Weapon"/>, <see cref="BattleEvent.Shield"/>,
    /// <see cref="BattleEvent.HitLocation"/>, and
    /// <see cref="BattleEvent.Resolution"/> are always <c>null</c>.
    /// <see cref="BattleEvent.Value"/> is always <c>0</c>. No emission site
    /// exists yet.
    /// </summary>
    Miss = 6,
}

/// <summary>
/// One authoritative simulation event. <see cref="Weapon"/>,
/// <see cref="Shield"/>, <see cref="HitLocation"/>, and
/// <see cref="Resolution"/> are populated only for <see cref="Kind"/>
/// <see cref="BattleEventKind.Attack"/>; every other kind carries all four as
/// <c>null</c>. Construct instances only through
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
        BodyPart? hitLocation,
        AttackResolution? resolution,
        int? comboPosition)
    {
        Sequence = sequence;
        Tick = tick;
        Kind = kind;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        Value = value;
        FactionId = factionId;
        _combatContext = weapon is { } presentWeapon
            ? ((long)resolution!.Value << ResolutionShift) |
                ((long)presentWeapon << WeaponShift) |
                ((long)shield!.Value << ShieldShift) |
                (long)hitLocation!.Value |
                ((long)(comboPosition ?? 0) << ComboPositionShift)
            : CombatContextAbsent;
    }

    private const long CombatContextAbsent = 0;
    private const int ResolutionShift = 24;
    private const int WeaponShift = 16;
    private const int ShieldShift = 8;

    /// <summary>
    /// Bits 32-39 of the widened <see cref="_combatContext"/>, immediately
    /// above the four original byte fields (bits 0-31) and not overlapping
    /// any of them. Holds the chain position of a landed blow that is part
    /// of an active attack combination, or <c>0</c> — meaning "not part of a
    /// chain" — for every other attack. See <see cref="ComboPosition"/>.
    /// </summary>
    private const int ComboPositionShift = 32;

    private const long FieldMask = 0xFF;

    /// <summary>
    /// <see cref="Resolution"/>, <see cref="Weapon"/>, <see cref="Shield"/>,
    /// <see cref="HitLocation"/>, and <see cref="ComboPosition"/> packed into
    /// one field, or <see cref="CombatContextAbsent"/> for an event that
    /// carries no combat context.
    /// </summary>
    /// <remarks>
    /// Five separate nullable fields would cost eight bytes each and would be
    /// the bulk of per-tick allocation, which
    /// <c>RepeatedCollisionTicksHaveBoundedAllocations</c> budgets. Packed
    /// this way the five together cost eight bytes — the field widened from
    /// <c>int</c> to <c>long</c> when the chain-position byte was added,
    /// which is still far cheaper than five boxed nullables.
    /// <para>
    /// None of the four original enums exceeds 255, so a byte apiece is
    /// sufficient and the four occupy the low 32 bits exactly. A test pins
    /// that range assumption. The chain position occupies the next byte,
    /// bits 32-39; see <see cref="ComboPositionShift"/>.
    /// </para>
    /// <para>
    /// Absence of the whole combat context is tested on the whole field
    /// rather than on any one byte, which is what makes
    /// <see cref="AttackResolution.Landed"/> safe at numeric zero: a landed
    /// attack contributes nothing to the resolution byte, but its weapon
    /// byte is always nonzero because <see cref="WeaponId"/> starts
    /// numbering at one. An event carrying no combat context is the only
    /// value for which the whole field is zero.
    /// </para>
    /// <para>
    /// The chain-position byte cannot reuse that same whole-field trick,
    /// because most attacks carry a combat context yet are not part of any
    /// chain even though their weapon/hit-location/resolution bytes are
    /// always present. Instead it is independently nullable within an
    /// already-present combat context: valid chain positions start at
    /// <c>1</c>, so <c>0</c> in that byte means "not part of a chain",
    /// checked only after confirming the whole field is not
    /// <see cref="CombatContextAbsent"/>. See <see cref="ComboPosition"/>.
    /// </para>
    /// </remarks>
    private readonly long _combatContext;

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
    /// How the defender met the blow. Populated only for
    /// <see cref="BattleEventKind.Attack"/>. This value is folded into the
    /// headless event hash.
    /// </summary>
    public AttackResolution? Resolution =>
        _combatContext == CombatContextAbsent
            ? null
            : (AttackResolution)((_combatContext >> ResolutionShift) & FieldMask);

    /// <summary>
    /// The position, starting at <c>1</c>, of this landed blow within an
    /// active attack combination — or <c>null</c> for an attack that is not
    /// part of any chain, which is most attacks, including every one under a
    /// combat preset that never opens a chain. Populated only for
    /// <see cref="BattleEventKind.Attack"/>; always <c>null</c> for a blow
    /// that did not land, since <see cref="BattleSimulation"/> only ever asks
    /// this question of a landed blow. This value is folded into the
    /// headless event hash.
    /// </summary>
    public int? ComboPosition
    {
        get
        {
            if (_combatContext == CombatContextAbsent)
            {
                return null;
            }

            var raw = (int)((_combatContext >> ComboPositionShift) & FieldMask);
            return raw == 0 ? null : raw;
        }
    }

    /// <summary>
    /// Creates a validated <see cref="BattleEventKind.Attack"/> event.
    /// <paramref name="weapon"/>, <paramref name="shield"/>, and
    /// <paramref name="hitLocation"/> are all required and must be defined
    /// enum values.
    /// </summary>
    /// <param name="resolution">
    /// How the defender met the blow. Optional, defaulting to
    /// <see cref="AttackResolution.Landed"/>, because this factory has twenty
    /// call sites across eleven files and a required parameter would break the
    /// whole solution build. The default benefits test call sites only:
    /// production code reaches this factory through
    /// <c>BattleSimulation.AddAttackEvent</c>, where the resolution is a
    /// required parameter so the default can never mask a missing wire-up.
    /// </param>
    /// <param name="comboPosition">
    /// This blow's position within an active attack combination, or
    /// <c>null</c> when it is not part of one. Optional and defaulting to
    /// <c>null</c> for the same reason <paramref name="resolution"/>
    /// defaults: this factory's many pre-existing call sites do not know
    /// about attack combinations and must keep compiling unchanged.
    /// </param>
    public static BattleEvent Attack(
        long sequence,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        int factionId,
        WeaponId weapon,
        ShieldId shield,
        BodyPart hitLocation,
        AttackResolution resolution = AttackResolution.Landed,
        int? comboPosition = null)
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

        if (!Enum.IsDefined(resolution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                "An attack event requires a defined resolution.");
        }

        if (comboPosition is { } presentComboPosition && presentComboPosition < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(comboPosition),
                comboPosition,
                "A combo position, when present, must be at least 1; " +
                "0 is reserved to mean \"not part of a chain\" and a " +
                "combo's first blow is position 1.");
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
            hitLocation,
            resolution,
            comboPosition);
    }

    /// <summary>
    /// Creates a validated event for every kind other than
    /// <see cref="BattleEventKind.Attack"/>. Rejects
    /// <see cref="BattleEventKind.Attack"/>, since only an
    /// <see cref="Attack"/> event may carry combat context. The weapon, shield,
    /// hit location, and resolution are forced to <c>null</c> here rather than
    /// accepted from the caller.
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
            hitLocation: null,
            resolution: null,
            comboPosition: null);
    }
}
