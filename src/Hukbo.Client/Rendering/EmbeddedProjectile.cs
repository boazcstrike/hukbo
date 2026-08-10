using Hukbo.Core.Combat;

namespace Hukbo.Client.Rendering;

/// <summary>
/// One projectile left standing in what it struck, riding that warrior as it
/// moves. Presentation only: the simulation neither knows nor is told that
/// anything is stuck in anyone, and nothing here reaches a hash, a snapshot,
/// or an event.
/// </summary>
/// <param name="Sequence">
/// The originating attack contact's sequence. Orders two projectiles that
/// embedded on the same tick, and seeds the per-projectile angle so the same
/// hit always draws at the same angle.
/// </param>
/// <param name="HostEntityId">
/// The warrior carrying it. The projectile is drawn inside that warrior's own
/// pass through the pawn loop, so a host the loop skips — culled, or dead past
/// its lethal hold — draws nothing, with no code here to produce that.
/// </param>
/// <param name="AttackerEntityId">
/// Who fired it. Carried only to seed the angle, matching the three-part mix
/// <c>BloodGeometry</c> uses for a burst.
/// </param>
/// <param name="Weapon">
/// The launching weapon, which decides the silhouette. Only
/// <see cref="WeaponId.Bangkaw"/> and <see cref="WeaponId.Busog"/> ever reach
/// here: an arquebus ball does not stand out of a wound, so it embeds nothing.
/// </param>
/// <param name="HitLocation">
/// The body part struck, or <see langword="null"/> when
/// <see cref="OnShield"/> is set and the projectile is standing in the board
/// rather than in the warrior.
/// </param>
/// <param name="OnShield">
/// Whether the shield took it. <c>AttackResolution.ShieldBlocked</c> is the
/// only thing that sets this, and a shield-blocked attack still carries a hit
/// location, which is why the two are recorded separately rather than one
/// being inferred from the other.
/// </param>
internal readonly record struct EmbeddedProjectile(
    long Sequence,
    ulong HostEntityId,
    ulong AttackerEntityId,
    WeaponId Weapon,
    BodyPart? HitLocation,
    bool OnShield);
