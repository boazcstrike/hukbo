using System.Collections.Immutable;
using Sandata.Client.Audio;

namespace Sandata.Client.Tests;

/// <summary>
/// Decision D4's client half: which shooters the client still considers
/// mid-burst, and therefore which ones it reports a possible burst end for on
/// a quiet tick. Pure — nothing here constructs a <c>SandataGame</c>, a
/// <c>GraphicsDevice</c>, or a <c>SpriteBatch</c>.
/// </summary>
/// <remarks>
/// The defect these tests bind is not that the old code reported the wrong
/// shooters; it is that it reported each one exactly once, on the first tick
/// it fell silent, and then dropped it. A burst at 600 rounds per minute is
/// one round every five ticks, so that first silent tick is a gap inside the
/// burst rather than its end — the report arrived while the burst was still
/// running and was never made again once it really finished.
/// </remarks>
public sealed class AutomaticBurstTrackingTests
{
    private static ImmutableArray<ulong> Shooters(params ulong[] entityIds) =>
        [.. entityIds];

    [Fact]
    public void QuietShooters_ReportsAMidBurstShooterThatDidNotFireThisTick()
    {
        var quiet = AutomaticBurstTracking.QuietShooters(
            Shooters(1UL, 2UL), Shooters(2UL));

        Assert.Equal(Shooters(1UL).ToArray(), quiet.ToArray());
    }

    [Fact]
    public void QuietShooters_ReportsNothingForAShooterStillFiring()
    {
        var quiet = AutomaticBurstTracking.QuietShooters(
            Shooters(1UL), Shooters(1UL));

        Assert.Empty(quiet);
    }

    /// <summary>
    /// The tick-gap case, stated as the tracking rule rather than as a sound:
    /// a shooter that fired five ticks ago and whose burst the player has not
    /// ended is still reported, on this tick and on every tick after it, so
    /// the player's grace window is given the chance to expire.
    /// </summary>
    [Fact]
    public void NextMidBurst_KeepsAQuietShooterUntilItsBurstIsReportedEnded()
    {
        var midBurst = Shooters(1UL);

        for (var quietTick = 0; quietTick < 5; quietTick++)
        {
            Assert.Equal(
                Shooters(1UL).ToArray(),
                AutomaticBurstTracking.QuietShooters(midBurst, ImmutableArray<ulong>.Empty).ToArray());

            midBurst = AutomaticBurstTracking.NextMidBurst(
                midBurst, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty);
        }

        Assert.Equal(Shooters(1UL).ToArray(), midBurst.ToArray());
    }

    [Fact]
    public void NextMidBurst_DropsAShooterWhoseBurstEnded()
    {
        var next = AutomaticBurstTracking.NextMidBurst(
            Shooters(1UL, 2UL), ImmutableArray<ulong>.Empty, Shooters(1UL));

        Assert.Equal(Shooters(2UL).ToArray(), next.ToArray());
    }

    [Fact]
    public void NextMidBurst_AddsAShooterThatFiredThisTick()
    {
        var next = AutomaticBurstTracking.NextMidBurst(
            ImmutableArray<ulong>.Empty, Shooters(3UL), ImmutableArray<ulong>.Empty);

        Assert.Equal(Shooters(3UL).ToArray(), next.ToArray());
    }

    [Fact]
    public void NextMidBurst_DoesNotListAShooterTwice()
    {
        var next = AutomaticBurstTracking.NextMidBurst(
            Shooters(1UL), Shooters(1UL), ImmutableArray<ulong>.Empty);

        Assert.Equal(Shooters(1UL).ToArray(), next.ToArray());
    }

    /// <summary>
    /// A shooter that ends its burst and fires again on the same tick stays
    /// tracked: the fresh round is a new burst, and dropping it here would
    /// lose the shooter for the whole of it.
    /// </summary>
    [Fact]
    public void NextMidBurst_AShooterThatEndedAndFiredAgainStaysTracked()
    {
        var next = AutomaticBurstTracking.NextMidBurst(
            Shooters(1UL), Shooters(1UL), Shooters(1UL));

        Assert.Equal(Shooters(1UL).ToArray(), next.ToArray());
    }

    [Fact]
    public void QuietShooters_HandlesAnEmptyMidBurstSet()
    {
        var quiet = AutomaticBurstTracking.QuietShooters(
            ImmutableArray<ulong>.Empty, Shooters(1UL));

        Assert.Empty(quiet);
    }
}
