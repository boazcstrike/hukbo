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
/// <see cref="HitLocation"/>, and <see cref="Resolution"/> are populated only
/// for <see cref="Kind"/> <see cref="BattleEventKind.Attack"/>; every other
/// kind carries all three as <c>null</c>. Construct instances only through
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
        BodyPart? hitLocation,
        AttackResolution? resolution)
    {
        Sequence = sequence;
        Tick = tick;
        Kind = kind;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        Value = value;
        FactionId = factionId;
        Weapon = weapon;
        HitLocation = hitLocation;
        Resolution = resolution;
    }

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
    public WeaponId? Weapon { get; }

    /// <summary>
    /// The resolved body part struck. Populated only for
    /// <see cref="BattleEventKind.Attack"/>.
    /// </summary>
    public BodyPart? HitLocation { get; }

    /// <summary>
    /// How the defender met the blow. Populated only for
    /// <see cref="BattleEventKind.Attack"/>. This value is folded into the
    /// headless event hash.
    /// </summary>
    public AttackResolution? Resolution { get; }

    /// <summary>
    /// Creates a validated <see cref="BattleEventKind.Attack"/> event. Both
    /// <paramref name="weapon"/> and <paramref name="hitLocation"/> are
    /// required and must be defined enum values.
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
    public static BattleEvent Attack(
        long sequence,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        int factionId,
        WeaponId weapon,
        BodyPart hitLocation,
        AttackResolution resolution = AttackResolution.Landed)
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

        return new BattleEvent(
            sequence,
            tick,
            BattleEventKind.Attack,
            sourceEntityId,
            targetEntityId,
            damage,
            factionId,
            weapon,
            hitLocation,
            resolution);
    }

    /// <summary>
    /// Creates a validated event for every kind other than
    /// <see cref="BattleEventKind.Attack"/>. Rejects
    /// <see cref="BattleEventKind.Attack"/>, since only an
    /// <see cref="Attack"/> event may carry combat context. The weapon, hit
    /// location, and resolution are forced to <c>null</c> here rather than
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
            hitLocation: null,
            resolution: null);
    }
}
