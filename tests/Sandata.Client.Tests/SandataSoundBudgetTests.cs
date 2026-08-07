using Sandata.Client.Audio;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers plan task 39's test bar for <see cref="SandataSoundBudget"/> and
/// the sound player it backs: a reservation is held for exactly
/// <c>TailTicks</c>; sustained automatic fire from eight shooters holds
/// sixteen instances rather than one per round; and the same tick and
/// entity id always select the same variant. This is a pure state machine
/// over ticks — no graphics device, audio device, or wall clock appears
/// anywhere in this file.
/// </summary>
public sealed class SandataSoundBudgetTests
{
    [Fact]
    public void AReservationIsHeldForExactlyTailTicks()
    {
        var budget = new SandataSoundBudget();
        const long reserveTick = 100;
        const int tailTicks = 10;

        var reserved = budget.TryReserve(
            shooterEntityId: 1, SoundFamily.GunReport, reserveTick, tailTicks, out var isNew);

        Assert.True(reserved);
        Assert.True(isNew);

        // Held for every tick in [reserveTick, reserveTick + tailTicks).
        for (var tick = reserveTick; tick < reserveTick + tailTicks; tick++)
        {
            Assert.True(
                budget.IsHeld(1, SoundFamily.GunReport, tick),
                $"Expected the reservation to still be held at tick {tick}.");
        }

        // No longer held at reserveTick + tailTicks.
        Assert.False(budget.IsHeld(1, SoundFamily.GunReport, reserveTick + tailTicks));
    }

    [Fact]
    public void ReservingAgainBeforeExpiryRenewsRatherThanTakingASecondSlot()
    {
        var budget = new SandataSoundBudget();

        budget.TryReserve(shooterEntityId: 1, SoundFamily.GunLoop, currentTick: 0, tailTicks: 5, out var firstIsNew);
        budget.TryReserve(shooterEntityId: 1, SoundFamily.GunLoop, currentTick: 3, tailTicks: 5, out var secondIsNew);

        Assert.True(firstIsNew);
        Assert.False(secondIsNew);
        Assert.Equal(1, budget.CountActive(currentTick: 3));

        // The renewal at tick 3 extends the hold to tick 3 + 5 = 8, past the
        // original tick 0 + 5 = 5 expiry.
        Assert.True(budget.IsHeld(1, SoundFamily.GunLoop, currentTick: 7));
        Assert.False(budget.IsHeld(1, SoundFamily.GunLoop, currentTick: 8));
    }

    [Fact]
    public void AnExpiredReservationsSlotCanBeReusedByAnotherShooter()
    {
        var budget = new SandataSoundBudget(maximumInstances: 1);

        budget.TryReserve(shooterEntityId: 1, SoundFamily.GunReport, currentTick: 0, tailTicks: 5, out _);
        Assert.False(budget.IsHeld(1, SoundFamily.GunReport, currentTick: 10));

        var reserved = budget.TryReserve(
            shooterEntityId: 2, SoundFamily.GunReport, currentTick: 10, tailTicks: 5, out var isNew);

        Assert.True(reserved);
        Assert.True(isNew);
        Assert.True(budget.IsHeld(2, SoundFamily.GunReport, currentTick: 10));
    }

    [Fact]
    public void AFullyOccupiedPoolDeclinesANewReservation()
    {
        var budget = new SandataSoundBudget(maximumInstances: 1);

        budget.TryReserve(shooterEntityId: 1, SoundFamily.GunReport, currentTick: 0, tailTicks: 100, out _);
        var declined = budget.TryReserve(
            shooterEntityId: 2, SoundFamily.GunReport, currentTick: 1, tailTicks: 5, out var isNew);

        Assert.False(declined);
        Assert.False(isNew);
        Assert.Equal(1, budget.CountActive(currentTick: 1));
    }

    /// <summary>
    /// Sustained automatic fire from eight shooters holds sixteen
    /// instances — one loop plus one tail per shooter — rather than one per
    /// round, however many rounds each shooter fires. Drives
    /// <see cref="SandataSoundPlayer"/> directly through a recording
    /// <see cref="ISandataSoundOutput"/> so both the reservation count and
    /// the play count (one loop start per shooter, no tail until fire stops)
    /// are checked in the same test.
    /// </summary>
    [Fact]
    public void SustainedAutomaticFireFromEightShootersHoldsSixteenInstances()
    {
        var budget = new SandataSoundBudget();
        var output = new RecordingSoundOutput();
        var player = new SandataSoundPlayer(output, budget);

        const int shooterCount = 8;
        const int roundsPerShooter = 50;

        for (var round = 0; round < roundsPerShooter; round++)
        {
            for (ulong shooter = 1; shooter <= shooterCount; shooter++)
            {
                player.HandleShotFired(
                    Sandata.Core.Weapons.CaliberFamily.Cal556X45,
                    Sandata.Client.Audio.FireMode.Auto,
                    rangeWu: 100,
                    shooterIsIndoors: true,
                    suppressorFitted: false,
                    tick: round,
                    shooterEntityId: shooter);
            }
        }

        // One loop family reservation and one tail family reservation per
        // shooter: 8 * 2 = 16, never shooterCount * roundsPerShooter.
        Assert.Equal(16, budget.CountActive(currentTick: roundsPerShooter - 1));

        // Exactly one loop instance played per shooter across the whole
        // burst, never one per round.
        Assert.Equal(shooterCount, output.PlayCount(SoundFamily.GunLoop));

        // No tail has played yet: HandleAutomaticFireStopped was never
        // called, only rounds mid-burst.
        Assert.Equal(0, output.PlayCount(SoundFamily.GunTail));
    }

    /// <summary>
    /// Once a shooter's automatic burst stops, exactly one tail instance
    /// plays for that shooter, using the reservation the burst already held.
    /// </summary>
    [Fact]
    public void StoppingAutomaticFirePlaysExactlyOneTailInstance()
    {
        var budget = new SandataSoundBudget();
        var output = new RecordingSoundOutput();
        var player = new SandataSoundPlayer(output, budget);

        for (var round = 0; round < 5; round++)
        {
            player.HandleShotFired(
                Sandata.Core.Weapons.CaliberFamily.Cal556X45,
                Sandata.Client.Audio.FireMode.Auto,
                rangeWu: 100,
                shooterIsIndoors: true,
                suppressorFitted: false,
                tick: round,
                shooterEntityId: 1);
        }

        player.HandleAutomaticFireStopped(
            Sandata.Core.Weapons.CaliberFamily.Cal556X45,
            rangeWu: 100,
            shooterIsIndoors: true,
            suppressorFitted: false,
            tick: 5,
            shooterEntityId: 1);

        Assert.Equal(1, output.PlayCount(SoundFamily.GunLoop));
        Assert.Equal(1, output.PlayCount(SoundFamily.GunTail));
    }

    /// <summary>
    /// The bar this task's prompt states in exactly these words: the same
    /// tick and entity id always select the same variant.
    /// </summary>
    [Fact]
    public void TheSameTickAndEntityIdAlwaysSelectTheSameVariant()
    {
        var first = ShotSlotResolver.SelectVariantNumber(tick: 123, shooterEntityId: 456, variantCount: 6);
        var second = ShotSlotResolver.SelectVariantNumber(tick: 123, shooterEntityId: 456, variantCount: 6);
        var third = ShotSlotResolver.SelectVariantNumber(tick: 123, shooterEntityId: 456, variantCount: 6);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    /// <summary>
    /// A minimal in-memory <see cref="ISandataSoundOutput"/> that counts
    /// plays per family instead of touching any audio device — the seam
    /// <see cref="SandataSoundPlayer"/>'s remarks describe.
    /// </summary>
    private sealed class RecordingSoundOutput : ISandataSoundOutput
    {
        private readonly int[] _playCounts = new int[Enum.GetValues<SoundFamily>().Length];

        public bool Play(SoundSlot slot, int variantNumber, ulong shooterEntityId)
        {
            _playCounts[(int)slot.Family]++;
            return true;
        }

        public int PlayCount(SoundFamily family) => _playCounts[(int)family];
    }
}
