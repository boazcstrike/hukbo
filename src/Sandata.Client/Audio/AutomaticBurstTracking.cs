using System.Collections.Immutable;

namespace Sandata.Client.Audio;

/// <summary>
/// Which shooters the client still considers mid-burst, as a pure function of
/// the previous tick's set, the shooters that fired an automatic round this
/// tick, and the bursts <see cref="SandataSoundPlayer.HandleAutomaticFireStopped"/>
/// actually ended.
/// </summary>
/// <remarks>
/// <para>
/// Before the 2026-08-14 lowered-weapon and automatic-fire design's decision
/// D4, the client reported a burst as ended on the first tick a shooter did
/// not fire on, and then forgot the shooter. Automatic fire at 600 rounds per
/// minute is one round every five ticks, so that first quiet tick is almost
/// never the end of anything: it is the gap between two rounds of one burst.
/// </para>
/// <para>
/// The window that tells those apart lives in
/// <see cref="SandataSoundPlayer"/>, in one place, so the client's half is to
/// keep reporting a shooter's possible stop on every quiet tick until the
/// player accepts one. A shooter therefore stays in the mid-burst set until
/// its burst genuinely ends, rather than until it misses a single tick.
/// </para>
/// </remarks>
internal static class AutomaticBurstTracking
{
    /// <summary>
    /// The shooters that were mid-burst and fired no automatic round this
    /// tick — the ones the caller reports a possible burst end for.
    /// </summary>
    internal static ImmutableArray<ulong> QuietShooters(
        ImmutableArray<ulong> midBurst,
        ImmutableArray<ulong> firedThisTick)
    {
        if (midBurst.IsDefaultOrEmpty)
        {
            return ImmutableArray<ulong>.Empty;
        }

        var quiet = ImmutableArray.CreateBuilder<ulong>();
        foreach (var shooterEntityId in midBurst)
        {
            if (!Contains(firedThisTick, shooterEntityId))
            {
                quiet.Add(shooterEntityId);
            }
        }

        return quiet.ToImmutable();
    }

    /// <summary>
    /// The mid-burst set for the next tick: everything still mid-burst whose
    /// burst has not ended, plus every shooter that fired this tick.
    /// </summary>
    /// <param name="midBurst">This tick's incoming mid-burst set.</param>
    /// <param name="firedThisTick">Shooters that fired an automatic round this tick.</param>
    /// <param name="endedThisTick">
    /// Shooters whose burst the sound player actually ended this tick, plus
    /// any the caller could no longer resolve to a living operator — a shooter
    /// that has left the map never reports another round and would otherwise
    /// be carried forever.
    /// </param>
    internal static ImmutableArray<ulong> NextMidBurst(
        ImmutableArray<ulong> midBurst,
        ImmutableArray<ulong> firedThisTick,
        ImmutableArray<ulong> endedThisTick)
    {
        var next = ImmutableArray.CreateBuilder<ulong>();

        if (!midBurst.IsDefaultOrEmpty)
        {
            foreach (var shooterEntityId in midBurst)
            {
                if (!Contains(endedThisTick, shooterEntityId))
                {
                    next.Add(shooterEntityId);
                }
            }
        }

        if (!firedThisTick.IsDefaultOrEmpty)
        {
            foreach (var shooterEntityId in firedThisTick)
            {
                if (next.IndexOf(shooterEntityId) < 0)
                {
                    next.Add(shooterEntityId);
                }
            }
        }

        return next.ToImmutable();
    }

    private static bool Contains(ImmutableArray<ulong> shooters, ulong shooterEntityId)
    {
        if (shooters.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var candidate in shooters)
        {
            if (candidate == shooterEntityId)
            {
                return true;
            }
        }

        return false;
    }
}
