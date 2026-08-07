using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

/// <summary>
/// One in-flight shot: hitscan with flight time rather than a simulated
/// moving body. It carries no velocity and no collision radius and is never
/// grid-indexed; it is a decrementing tick countdown that resolves through
/// the same <see cref="Combat.HitLocationResolver"/> and
/// <see cref="Combat.ClashResolver"/> path a melee blow uses, folding the
/// launch tick rather than the impact tick so that the roll a spectator sees
/// resolve on the arrival tick is reproducible from the tick the shot was
/// actually loosed on.
/// </summary>
/// <remarks>
/// A <c>readonly record struct</c>, in the same family as
/// <see cref="Movement.CollisionBody"/>: every field is an integer or a
/// small enum, nothing is a reference, and the type lives only in a flat
/// pooled array owned by <see cref="BattleSimulation"/> —
/// see <see cref="Scenario.MaximumProjectilesInFlight"/>. Phase 1: a
/// projectile passes through allies and through every enemy but its target;
/// there is no line of sight and no interception.
/// </remarks>
/// <param name="SourceEntityId">The launching warrior's entity ID.</param>
/// <param name="TargetEntityId">
/// The warrior this shot was aimed at when it launched. Resolution still
/// reads the current state at that entity ID at impact — including a target
/// that has since died, which is a <see cref="BattleEvent.Miss"/> rather
/// than an <see cref="BattleEvent.Attack"/> — exactly as a melee proposal
/// buffered earlier in the same tick already does.
/// </param>
/// <param name="LaunchTick">
/// The authoritative tick this shot was loosed on. Folded into the clash and
/// hit-location seed at resolution instead of the impact tick, so a shot's
/// outcome is fixed the instant it leaves the bow, spear, or matchlock.
/// </param>
/// <param name="TicksRemaining">
/// Ticks left before this shot resolves. Decremented once per tick by the
/// new gather pass; resolves the tick it reaches zero.
/// </param>
/// <param name="OriginXRaw">
/// The launching warrior's X position, as a raw fixed-point value, at the
/// instant of launch. Not the warrior's current position — the shot does not
/// track a launcher that moves after loosing it.
/// </param>
/// <param name="OriginYRaw">
/// The launching warrior's Y position, as a raw fixed-point value, at the
/// instant of launch.
/// </param>
/// <param name="Weapon">The weapon this shot was loosed from.</param>
/// <param name="DamageAtLaunch">
/// <see cref="Combat.WeaponProfile.DamagePerAttack"/> for <see cref="Weapon"/>,
/// recorded at launch so a later, unrelated preset or loadout change could
/// never retroactively alter a shot already in flight.
/// </param>
public readonly record struct Projectile(
    ulong SourceEntityId,
    ulong TargetEntityId,
    long LaunchTick,
    int TicksRemaining,
    int OriginXRaw,
    int OriginYRaw,
    WeaponId Weapon,
    int DamageAtLaunch);
