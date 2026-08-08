using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One atomic audiovisual contact. Every presentation channel consumes this
/// same value so coalescing cannot leave a pose, effect, reaction, lethal
/// hold, or sound detached from the event that produced it.
/// </summary>
internal readonly record struct AttackContactBundle(
    long Sequence,
    long Tick,
    ulong AttackerEntityId,
    ulong DefenderEntityId,
    int Damage,
    int FactionId,
    WeaponId Weapon,
    ShieldId AttackerShield,
    BodyPart HitLocation,
    AttackResolution Resolution,
    int? ComboPosition,
    bool IsLethal);
