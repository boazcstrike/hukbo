namespace Sandata.Client.Audio;

/// <summary>
/// A pure, tick-driven pool of playback reservations for the Sandata audio
/// layer. Design section 10, "The MonoGame instance pool is the real
/// ceiling": a gunshot's tail outlives the frame that started it, so this
/// budget holds a reservation for a slot's own <see cref="SoundSlot.TailTicks"/>
/// rather than clearing every frame the way <c>Hukbo.Client.Audio.SoundCueBudget</c>
/// does. There is no frame concept here at all — every operation takes the
/// current tick explicitly, and a reservation's occupancy is a pure function
/// of that tick, never of wall-clock time or call order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Provisional ceiling.</b> <see cref="DefaultMaximumInstances"/> is a
/// placeholder, not a measurement. Design section 10 assigns the real pool
/// ceiling to a later measurement task against named hardware — the plan
/// document's own task-39 row literally says "task 49 measures them", but
/// that number disagrees with the risk register's own entry for this exact
/// risk and with the task that reports its outcome, both of which name task
/// 53. This budget follows the risk register and the reporting task, not the
/// row's literal text, and remains provisional under either number: no task
/// before the measurement task may present this constant as tuned to real
/// hardware.
/// </para>
/// <para>
/// <b>Renewal, not accumulation.</b> A reservation is keyed by
/// (<c>shooterEntityId</c>, <c>family</c>). Calling <see cref="TryReserve"/>
/// again for a pair that already holds an unexpired reservation renews that
/// same slot's expiry rather than taking a second one — this is what lets
/// sustained automatic fire hold exactly one loop instance and one tail
/// instance per shooter for as long as the shooter keeps firing, instead of
/// growing by one reservation per round.
/// </para>
/// </remarks>
internal sealed class SandataSoundBudget
{
    /// <summary>
    /// Provisional pool ceiling. See this type's remarks — not a measurement
    /// until the pool-ceiling measurement task records one.
    /// </summary>
    public const int DefaultMaximumInstances = 64;

    private readonly Reservation[] _reservations;

    public SandataSoundBudget(int maximumInstances = DefaultMaximumInstances)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInstances);

        MaximumInstances = maximumInstances;
        _reservations = new Reservation[maximumInstances];
    }

    /// <summary>
    /// The provisional pool ceiling this instance was constructed with. See
    /// this type's remarks.
    /// </summary>
    public int MaximumInstances { get; }

    /// <summary>
    /// Reserves one pool slot for <paramref name="shooterEntityId"/> and
    /// <paramref name="family"/>, held through <paramref name="currentTick"/>
    /// plus <paramref name="tailTicks"/>. If that pair already holds an
    /// unexpired reservation, this renews its expiry to the new value instead
    /// of taking a second slot. Returns <see langword="false"/> without
    /// changing any state when every slot is already held by a different
    /// pair — the pool is exhausted and the caller must decline the cue
    /// rather than queue it.
    /// </summary>
    /// <param name="isNewReservation">
    /// <see langword="true"/> when this call took a previously free slot;
    /// <see langword="false"/> when it renewed an existing one, or when it
    /// declined. The caller uses this to know whether a sustained automatic
    /// loop is starting for the first time or simply continuing.
    /// </param>
    public bool TryReserve(
        ulong shooterEntityId,
        SoundFamily family,
        long currentTick,
        int tailTicks,
        out bool isNewReservation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tailTicks);

        var expiresAtTick = currentTick + tailTicks;

        for (var index = 0; index < _reservations.Length; index++)
        {
            ref var reservation = ref _reservations[index];
            if (reservation.Occupied &&
                reservation.ShooterEntityId == shooterEntityId &&
                reservation.Family == family &&
                reservation.ExpiresAtTick > currentTick)
            {
                reservation.ExpiresAtTick = expiresAtTick;
                isNewReservation = false;
                return true;
            }
        }

        for (var index = 0; index < _reservations.Length; index++)
        {
            ref var reservation = ref _reservations[index];
            if (!reservation.Occupied || reservation.ExpiresAtTick <= currentTick)
            {
                reservation = new Reservation
                {
                    Occupied = true,
                    ShooterEntityId = shooterEntityId,
                    Family = family,
                    ExpiresAtTick = expiresAtTick,
                };
                isNewReservation = true;
                return true;
            }
        }

        isNewReservation = false;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="shooterEntityId"/> and <paramref name="family"/>
    /// currently hold an unexpired reservation at <paramref name="currentTick"/>.
    /// A reservation taken or last renewed at tick <c>t</c> with
    /// <c>tailTicks</c> ticks reads as held for exactly <c>tailTicks</c> ticks —
    /// <c>t</c> through <c>t + tailTicks - 1</c> — and no longer held at
    /// <c>t + tailTicks</c>.
    /// </summary>
    public bool IsHeld(ulong shooterEntityId, SoundFamily family, long currentTick)
    {
        for (var index = 0; index < _reservations.Length; index++)
        {
            ref readonly var reservation = ref _reservations[index];
            if (reservation.Occupied &&
                reservation.ShooterEntityId == shooterEntityId &&
                reservation.Family == family &&
                reservation.ExpiresAtTick > currentTick)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The number of reservations still unexpired at <paramref name="currentTick"/>.</summary>
    public int CountActive(long currentTick)
    {
        var count = 0;
        for (var index = 0; index < _reservations.Length; index++)
        {
            ref readonly var reservation = ref _reservations[index];
            if (reservation.Occupied && reservation.ExpiresAtTick > currentTick)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// One pool slot's occupant. A flat, fixed-size array of this struct is
    /// the whole budget — no <c>Dictionary&lt;&gt;</c>, matching the flat-array
    /// convention <c>SandataSoundCatalog</c> already set for this folder.
    /// </summary>
    private struct Reservation
    {
        public bool Occupied;
        public ulong ShooterEntityId;
        public SoundFamily Family;
        public long ExpiresAtTick;
    }
}
