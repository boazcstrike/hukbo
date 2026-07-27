using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Counters describing how accepted attacks were resolved, either for one tick
/// or for a whole run. The five outcome counters are mutually exclusive and
/// jointly exhaustive, so they sum to <see cref="AcceptedAttacks"/>.
/// </summary>
/// <remarks>
/// <para>
/// These counters are <b>derived</b> observability data. They are never hashed,
/// never snapshotted, and never persisted, so they cannot influence a
/// simulation outcome. The seed-1 hash pair is recorded immediately before and
/// immediately after accumulation is wired in, which is the evidence that they
/// reach neither hash.
/// </para>
/// <para>
/// Two same-seed runs of the same build must produce identical values in every
/// field.
/// </para>
/// <para>
/// One type serves both roles. Every field is a plain count and
/// <see cref="DefenceAttributableShare"/> is derived from the counts alone, so
/// the value is meaningful at any level of aggregation.
/// </para>
/// </remarks>
/// <param name="AcceptedAttacks">
/// Attacks that passed the reach and cooldown gates and were therefore
/// resolved. Every accepted attack is already in reach, so no further
/// qualifier belongs in this denominator.
/// </param>
/// <param name="LandedAttacks">
/// Attacks resolved as <see cref="AttackResolution.Landed"/>. The only
/// resolution that applies damage.
/// </param>
/// <param name="ShieldBlockedAttacks">
/// Attacks resolved as <see cref="AttackResolution.ShieldBlocked"/>.
/// </param>
/// <param name="ParriedAttacks">
/// Attacks resolved as <see cref="AttackResolution.Parried"/>.
/// </param>
/// <param name="DeflectedAttacks">
/// Attacks resolved as <see cref="AttackResolution.Deflected"/>.
/// </param>
/// <param name="EvadedAttacks">
/// Attacks resolved as <see cref="AttackResolution.Evaded"/>.
/// </param>
public readonly record struct CombatMetrics(
    long AcceptedAttacks,
    long LandedAttacks,
    long ShieldBlockedAttacks,
    long ParriedAttacks,
    long DeflectedAttacks,
    long EvadedAttacks)
{
    /// <summary>
    /// Shield intercepts plus weapon intercepts plus voids, divided by
    /// accepted attacks. This is the quantity acceptance criterion one is
    /// measured over.
    /// </summary>
    /// <remarks>
    /// <b>Exactly zero when no attack was accepted.</b> That is a deliberate
    /// choice rather than a convenience: the criterion-one band test reads this
    /// value, and returning the band centre, or any value inside the band,
    /// would make that test pass vacuously on an empty run and leave it unable
    /// to fail if accumulation ever regressed to zero.
    /// </remarks>
    public double DefenceAttributableShare =>
        AcceptedAttacks == 0
            ? 0
            : (double)(ShieldBlockedAttacks +
                ParriedAttacks +
                DeflectedAttacks +
                EvadedAttacks) / AcceptedAttacks;
}

/// <summary>
/// Accumulates per-tick attack-resolution counts into the run aggregate
/// reported as a <see cref="CombatMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// A mutable struct so that a caller can hold it as a field and feed it once
/// per tick without allocating, matching
/// <see cref="CollisionMetricsAccumulator"/>. Running totals are held as
/// <see cref="long"/> and added in a <c>checked</c> context, so a run long
/// enough to overflow fails loudly instead of silently reporting a wrapped
/// count.
/// </para>
/// <para>
/// A default-constructed accumulator is the valid empty state, and
/// <see cref="Reset"/> returns an accumulator to exactly that state.
/// </para>
/// <para>
/// It deliberately does <b>not</b> check that the five outcome counts sum to
/// the accepted count. That invariant is asserted by a test against the
/// simulation; enforcing it here as well would make that test unable to fail.
/// </para>
/// </remarks>
internal struct CombatMetricsAccumulator
{
    private long _acceptedAttacks;
    private long _landedAttacks;
    private long _shieldBlockedAttacks;
    private long _parriedAttacks;
    private long _deflectedAttacks;
    private long _evadedAttacks;

    /// <summary>
    /// Returns the accumulator to the state of a freshly constructed one, so a
    /// reused instance cannot leak counts from a previous run.
    /// </summary>
    internal void Reset() => this = default;

    /// <summary>
    /// Folds one tick's attack-resolution counts into the run aggregate.
    /// </summary>
    /// <param name="acceptedAttacks">Attacks resolved this tick.</param>
    /// <param name="landed">Attacks that reached the defender this tick.</param>
    /// <param name="shieldBlocked">Attacks the shield took this tick.</param>
    /// <param name="parried">Attacks the weapon arrested this tick.</param>
    /// <param name="deflected">Attacks the weapon brushed aside this tick.</param>
    /// <param name="evaded">Attacks that met empty air this tick.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is negative. Every input is a count, so a negative value is
    /// a caller defect. The accumulator is left unchanged when an argument is
    /// rejected.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A running total would exceed <see cref="long.MaxValue"/>.
    /// </exception>
    internal void AddTick(
        int acceptedAttacks,
        int landed,
        int shieldBlocked,
        int parried,
        int deflected,
        int evaded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedAttacks);
        ArgumentOutOfRangeException.ThrowIfNegative(landed);
        ArgumentOutOfRangeException.ThrowIfNegative(shieldBlocked);
        ArgumentOutOfRangeException.ThrowIfNegative(parried);
        ArgumentOutOfRangeException.ThrowIfNegative(deflected);
        ArgumentOutOfRangeException.ThrowIfNegative(evaded);

        checked
        {
            _acceptedAttacks += acceptedAttacks;
            _landedAttacks += landed;
            _shieldBlockedAttacks += shieldBlocked;
            _parriedAttacks += parried;
            _deflectedAttacks += deflected;
            _evadedAttacks += evaded;
        }
    }

    /// <summary>
    /// Projects the accumulated counts into the reported value. Reading does
    /// not consume the accumulator, so it may be called any number of times.
    /// </summary>
    internal readonly CombatMetrics ToMetrics() =>
        new(
            _acceptedAttacks,
            _landedAttacks,
            _shieldBlockedAttacks,
            _parriedAttacks,
            _deflectedAttacks,
            _evadedAttacks);
}
