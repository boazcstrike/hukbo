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
/// <b>Measured ceiling.</b> <see cref="DefaultMaximumInstances"/> is no longer
/// a placeholder. Task 53 ran <c>tools/Sandata.Tools.AudioPool</c> on named
/// hardware and recorded the instance count at which
/// <c>InstancePlayLimitException</c> first fires: the 257th concurrent
/// <c>SoundEffectInstance</c> throws, so 256 is the usable pool. That figure
/// is MonoGame's own DesktopGL pool limit rather than a property of one sound
/// card, so a different machine is not expected to report a different number;
/// what was genuinely machine-specific, and is recorded alongside it in
/// <c>docs/development/testing.md</c>, is that eight shooters sustained
/// automatic fire for ten seconds holding sixteen instances with zero
/// refusals.
/// </para>
/// <para>
/// <b>Why the constant equals the ceiling rather than sitting below it.</b>
/// This budget refuses the 257th reservation, which is the same instance
/// MonoGame would have thrown on, so no headroom is needed — but that holds
/// only while every played instance is reserved here first. Every cue path on
/// <see cref="SandataSoundPlayer"/> goes through <see cref="TryReserve"/>
/// before <see cref="ISandataSoundOutput.Play"/> today, and a future task that
/// adds a play path bypassing this budget reintroduces the exception this
/// constant exists to prevent.
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
    /// The measured pool ceiling: the 257th concurrent
    /// <c>SoundEffectInstance</c> throws <c>InstancePlayLimitException</c>, so
    /// 256 is the last reservation this budget may grant. Recorded by task 53
    /// against named hardware — see this type's remarks.
    /// </summary>
    public const int DefaultMaximumInstances = 256;

    private readonly Reservation[] _reservations;

    public SandataSoundBudget(int maximumInstances = DefaultMaximumInstances)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInstances);

        MaximumInstances = maximumInstances;
        _reservations = new Reservation[maximumInstances];
    }

    /// <summary>
    /// The pool ceiling this instance was constructed with, defaulting to the
    /// measured <see cref="DefaultMaximumInstances"/>. See this type's remarks.
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
