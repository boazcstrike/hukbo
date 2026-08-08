namespace Sandata.Core.Weapons;

/// <summary>
/// The weapon-chain state machine's phases, design section 9's
/// "<c>Lowered → Raising → Turning → Aiming → Firing → Resetting →
/// Aiming</c>" — the timing chain Door Kickers' gunfights are built on.
/// Whoever completes ready, turn, aim, fire first wins an engagement, and
/// this enum names the five distinct waits (plus the momentary sixth,
/// <see cref="Firing"/>) that chain resolves through.
/// </summary>
/// <remarks>
/// This is the named type for the raw <c>int</c> backing value
/// <c>Sandata.Core.Simulation.OperatorState.WeaponChainPhase</c> already
/// holds as hashed, per-operator state (task 17). Every numeric value below
/// is therefore part of the same replay contract <c>FirearmId</c>'s remarks
/// describe: append-only, never reordered, never renumbered without a new
/// preset version.
/// </remarks>
public enum WeaponChainPhase
{
    /// <summary>
    /// The weapon is down, whether by the operator's own choice or by the
    /// weapon-lowered rule (design section 9). No tick counter runs in this
    /// phase; it holds until something requests a raise, and re-imposes the
    /// full <c>ReadyTicks</c> wait the next time it does.
    /// </summary>
    Lowered = 0,

    /// <summary>
    /// The weapon is coming up out of <see cref="Lowered"/>. Counts down the
    /// tick form of the definition's authored <c>ReadyMs</c> — around 4
    /// ticks for a rifle's roughly 405 ms at 50 Hz, versus roughly 1 tick for
    /// a pistol drawn from a holster around 80 ms.
    /// </summary>
    Raising = 1,

    /// <summary>
    /// The weapon is up and the operator is rotating the aim point onto the
    /// target. Holds until the target's shortest arc from the current aim
    /// falls within <c>SandataRuleset.AimToleranceBam</c>. Carries no tick
    /// counter of its own — unlike every other timed phase, its exit
    /// condition is angular, not a fixed duration.
    /// </summary>
    Turning = 2,

    /// <summary>
    /// The target is within tolerance and the operator is settling the aim
    /// before the shot breaks. Counts down the tick form of the definition's
    /// authored <c>AimBaseMs</c> plus its per-Bam off-centre term. This is
    /// also the phase an engagement returns to after <see cref="Resetting"/>,
    /// for every follow-up shot at the same target.
    /// </summary>
    Aiming = 3,

    /// <summary>
    /// The shot resolves. This phase is never itself observed as state held
    /// between ticks: <see cref="WeaponChain.Advance"/> always resolves it
    /// and continues on to <see cref="Resetting"/> within the very same tick
    /// it is entered, per design section 9's zero-tick rule.
    /// </summary>
    Firing = 4,

    /// <summary>
    /// Recoil recovery between shots at the same engagement. Counts down the
    /// tick form of the definition's authored <c>ResetMs</c>, then returns to
    /// <see cref="Aiming"/> rather than back to <see cref="Turning"/> — a
    /// completed engagement does not re-turn between its own shots.
    /// </summary>
    Resetting = 5,
}
