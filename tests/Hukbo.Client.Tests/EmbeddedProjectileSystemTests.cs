using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// The bounded population of projectiles left standing in what they struck
/// (task B1 of the archived "Projectile props and embedded projectiles" plan).
/// </summary>
public sealed class EmbeddedProjectileSystemTests
{
    [Theory]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    public void StartContact_EmbedsAShaftedProjectileThatLanded(WeaponId weapon)
    {
        var system = new EmbeddedProjectileSystem();

        system.StartContact(Contact(weapon, AttackResolution.Landed, BodyPart.Chest));

        var embedded = Assert.Single(system.ActiveProjectiles.ToArray());
        Assert.Equal(weapon, embedded.Weapon);
        Assert.Equal(BodyPart.Chest, embedded.HitLocation);
        Assert.False(embedded.OnShield);
    }

    [Fact]
    public void StartContact_EmbedsNothingForAnArquebus()
    {
        var system = new EmbeddedProjectileSystem();

        // A lead ball does not stand out of a wound. This asymmetry is the
        // reason the design splits the in-flight prop from the embedded pool.
        system.StartContact(
            Contact(WeaponId.Arquebus, AttackResolution.Landed, BodyPart.Chest));

        Assert.Empty(system.ActiveProjectiles.ToArray());
    }

    [Theory]
    [InlineData(AttackResolution.Evaded)]
    [InlineData(AttackResolution.Parried)]
    [InlineData(AttackResolution.Deflected)]
    public void StartContact_EmbedsNothingForABlowThatNeverArrived(
        AttackResolution resolution)
    {
        var system = new EmbeddedProjectileSystem();

        system.StartContact(Contact(WeaponId.Busog, resolution, BodyPart.Chest));

        Assert.Empty(system.ActiveProjectiles.ToArray());
    }

    [Fact]
    public void StartContact_RecordsAShieldBlockOnTheShieldRatherThanTheBodyPart()
    {
        var system = new EmbeddedProjectileSystem();

        // A shield-blocked attack still carries a hit location. Using it would
        // put the arrow in the warrior rather than in the board that stopped
        // it — the same trap SoundDirector documents for its own hit class.
        system.StartContact(
            Contact(WeaponId.Busog, AttackResolution.ShieldBlocked, BodyPart.Chest));

        var embedded = Assert.Single(system.ActiveProjectiles.ToArray());
        Assert.True(embedded.OnShield);
        Assert.Null(embedded.HitLocation);
    }

    [Fact]
    public void StartContact_NeverExceedsCapacity()
    {
        var system = new EmbeddedProjectileSystem();

        for (var sequence = 0; sequence < 1_000; sequence++)
        {
            system.StartContact(
                Contact(
                    WeaponId.Busog,
                    AttackResolution.Landed,
                    BodyPart.Chest,
                    sequence));
        }

        Assert.Equal(
            EmbeddedProjectileSystem.Capacity,
            system.ActiveProjectiles.Length);
    }

    [Fact]
    public void StartContact_EvictsOldestFirstOnceFull()
    {
        var system = new EmbeddedProjectileSystem(capacity: 3);

        for (var sequence = 0; sequence < 4; sequence++)
        {
            system.StartContact(
                Contact(
                    WeaponId.Busog,
                    AttackResolution.Landed,
                    BodyPart.Chest,
                    sequence));
        }

        var sequences = system.ActiveProjectiles
            .ToArray()
            .Select(projectile => projectile.Sequence)
            .Order()
            .ToArray();

        // Sequence 0 is gone; the three most recent hits are what remains.
        Assert.Equal([1L, 2L, 3L], sequences);
    }

    [Fact]
    public void Clear_EmptiesThePoolAndResetsTheRingCursor()
    {
        var system = new EmbeddedProjectileSystem(capacity: 2);
        for (var sequence = 0; sequence < 5; sequence++)
        {
            system.StartContact(
                Contact(
                    WeaponId.Busog,
                    AttackResolution.Landed,
                    BodyPart.Chest,
                    sequence));
        }

        system.Clear();
        Assert.Empty(system.ActiveProjectiles.ToArray());

        // The cursor has to reset with the count, or the first insert after a
        // round reset would land in a slot the ring thinks is still occupied.
        system.StartContact(
            Contact(WeaponId.Busog, AttackResolution.Landed, BodyPart.Chest, 99));
        var embedded = Assert.Single(system.ActiveProjectiles.ToArray());
        Assert.Equal(99L, embedded.Sequence);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void Embeds_IsFalseForEveryMeleeWeapon(WeaponId weapon)
    {
        // False because they are melee, not because they were forgotten. The
        // switch behind this is exhaustive, so a weapon added later cannot
        // silently start or stop embedding.
        Assert.False(
            EmbeddedProjectileSystem.Embeds(weapon, AttackResolution.Landed));
    }

    private static AttackContactBundle Contact(
        WeaponId weapon,
        AttackResolution resolution,
        BodyPart hitLocation,
        long sequence = 1) =>
        new(
            sequence,
            Tick: 10,
            AttackerEntityId: 1,
            DefenderEntityId: 2,
            Damage: 5,
            FactionId: 0,
            weapon,
            AttackerShield: ShieldId.None,
            hitLocation,
            resolution,
            ComboPosition: null,
            IsLethal: false);
}
