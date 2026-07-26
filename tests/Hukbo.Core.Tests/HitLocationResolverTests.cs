using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

public sealed class HitLocationResolverTests
{
    private static readonly CombatRuleset Rules = PhilippineCombatPreset.Rules;

    // Independently calculated golden vectors. Derivation method for each
    // row: expectedRoll is the 64-bit FNV-1a fold (offset basis
    // 14695981039346656037, prime 1099511628211) of, in order, the fixed tag
    // 0x484B424F5F484954, seed, tick (reinterpreted as an unsigned 64-bit
    // bit pattern), sourceEntityId, targetEntityId, and the attacking
    // weapon cast to ulong -- each value byte-folded low byte first across
    // eight rounds of XOR-then-multiply (see
    // HitLocationResolver.MixAttack). expectedBodyPart is that roll modulo
    // the ruleset's resolved total weight for (weapon, defenderShield),
    // walked through BodyPartCatalog.Ordered while accumulating each part's
    // (weapon target weight * shield defense multiplier) until the running
    // sum exceeds the rolled value. These pin the FNV-1a mixer field order,
    // byte encoding, enum values, unchecked overflow behavior, modulo, and
    // cumulative-interval selection. Any deliberate change to the mixer or
    // targeting data requires new vectors and a new preset version.
    [Theory]
    [InlineData(1UL, 1L, 1UL, 2UL, WeaponId.GreatBlade, ShieldId.None, 0x5AB2E78583A95197UL, BodyPart.Shin)]
    [InlineData(1UL, 1L, 2UL, 1UL, WeaponId.HeavyChopper, ShieldId.TallHardwood, 0x84BF4CC561D9E994UL, BodyPart.ShieldArm)]
    [InlineData(42UL, 17L, 7UL, 12UL, WeaponId.ThrustingBlade, ShieldId.TallHardwood, 0x2A18A5AFEF928686UL, BodyPart.Knee)]
    [InlineData(42UL, 17L, 12UL, 7UL, WeaponId.Bolo, ShieldId.None, 0xA98FBA5910945501UL, BodyPart.Thigh)]
    [InlineData(0xDEADBEEFUL, 99L, 199UL, 200UL, WeaponId.GreatBlade, ShieldId.TallHardwood, 0x56E9870A427F50A6UL, BodyPart.Knee)]
    [InlineData(0UL, 0L, 3UL, 4UL, WeaponId.ThrustingBlade, ShieldId.None, 0x295B7F1E45FC5AB1UL, BodyPart.Chest)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0x7FFFFFFFFFFFFFFFL, 4UL, 3UL, WeaponId.HeavyChopper, ShieldId.None, 0x4F91245EAE04F060UL, BodyPart.Neck)]
    [InlineData(987654321UL, 1234L, 88UL, 17UL, WeaponId.Bolo, ShieldId.TallHardwood, 0x7B598081E38B044FUL, BodyPart.Thigh)]
    public void GoldenVectors_MatchTheIndependentlyCalculatedRollAndBodyPart(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId weapon,
        ShieldId defenderShield,
        ulong expectedRoll,
        BodyPart expectedBodyPart)
    {
        var attacker = new CombatLoadout(weapon, ArmorId.LightOrganic, ShieldId.None);
        var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, defenderShield);

        var roll = HitLocationResolver.MixAttack(seed, tick, sourceEntityId, targetEntityId, weapon);
        var resolved = HitLocationResolver.Resolve(
            Rules,
            attacker,
            defender,
            seed,
            tick,
            sourceEntityId,
            targetEntityId);

        Assert.Equal(expectedRoll, roll);
        Assert.Equal(expectedBodyPart, resolved);
    }

    [Fact]
    public void Resolve_IsDeterministicForIdenticalInputs()
    {
        var attacker = new CombatLoadout(WeaponId.Bolo, ArmorId.LightOrganic, ShieldId.None);
        var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.TallHardwood);

        var first = HitLocationResolver.Resolve(Rules, attacker, defender, 55UL, 12L, 3UL, 9UL);
        var second = HitLocationResolver.Resolve(Rules, attacker, defender, 55UL, 12L, 3UL, 9UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Resolve_AlwaysReturnsADefinedBodyPart()
    {
        var attacker = new CombatLoadout(WeaponId.ThrustingBlade, ArmorId.LightOrganic, ShieldId.None);
        var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.TallHardwood);

        for (ulong seed = 1; seed <= 25; seed++)
        {
            var result = HitLocationResolver.Resolve(Rules, attacker, defender, seed, (long)seed, 1UL, 2UL);
            Assert.True(Enum.IsDefined(result));
        }
    }

    [Fact]
    public void Resolve_ReachesMoreThanOneBodyPartAcrossAFixedTupleMatrix()
    {
        var results = new HashSet<BodyPart>();
        var weapons = new[] { WeaponId.GreatBlade, WeaponId.HeavyChopper, WeaponId.ThrustingBlade, WeaponId.Bolo };
        var shields = new[] { ShieldId.None, ShieldId.TallHardwood };

        foreach (var weapon in weapons)
        {
            var attacker = new CombatLoadout(weapon, ArmorId.LightOrganic, ShieldId.None);
            foreach (var shield in shields)
            {
                var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, shield);
                for (ulong tick = 1; tick <= 20; tick++)
                {
                    results.Add(HitLocationResolver.Resolve(
                        Rules,
                        attacker,
                        defender,
                        seed: 3,
                        tick: (long)tick,
                        sourceEntityId: 1,
                        targetEntityId: 2));
                }
            }
        }

        Assert.True(results.Count > 1);
    }

    [Fact]
    public void WeaponOverrides_CanChangeTheResolvedBodyPartForTheSameTuple()
    {
        var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None);
        var results = new HashSet<BodyPart>();

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var attacker = new CombatLoadout(weapon, ArmorId.LightOrganic, ShieldId.None);
            results.Add(HitLocationResolver.Resolve(Rules, attacker, defender, 9UL, 4L, 5UL, 6UL));
        }

        Assert.True(results.Count > 1);
    }

    [Fact]
    public void TallHardwoodShield_LowersChestAndAbdomenSelectionFrequency_Provisional()
    {
        // PROVISIONAL gameplay-tuning comparison, not a historical claim.
        // Broad inequality over a large fixed tuple matrix, not an exact
        // percentage, to avoid brittle statistical assertions.
        var attacker = new CombatLoadout(WeaponId.ThrustingBlade, ArmorId.LightOrganic, ShieldId.None);
        const int SampleTickCount = 500;

        var unshieldedChestOrAbdomen = CountChestOrAbdomenHits(attacker, ShieldId.None, SampleTickCount);
        var shieldedChestOrAbdomen = CountChestOrAbdomenHits(attacker, ShieldId.TallHardwood, SampleTickCount);

        Assert.True(
            shieldedChestOrAbdomen < unshieldedChestOrAbdomen,
            $"Expected fewer chest/abdomen hits with TallHardwood ({shieldedChestOrAbdomen}) " +
            $"than with no shield ({unshieldedChestOrAbdomen}).");
    }

    private static int CountChestOrAbdomenHits(
        CombatLoadout attacker,
        ShieldId defenderShield,
        int sampleTickCount)
    {
        var defender = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, defenderShield);
        var count = 0;

        for (long tick = 1; tick <= sampleTickCount; tick++)
        {
            var part = HitLocationResolver.Resolve(
                Rules,
                attacker,
                defender,
                seed: 123,
                tick: tick,
                sourceEntityId: 11,
                targetEntityId: 22);

            if (part is BodyPart.Chest or BodyPart.Abdomen)
            {
                count++;
            }
        }

        return count;
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Resolve_RejectsNegativeTicks(long tick)
    {
        var loadout = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitLocationResolver.Resolve(Rules, loadout, loadout, 1UL, tick, 1UL, 2UL));
    }

    [Fact]
    public void Resolve_RejectsAZeroSourceEntityId()
    {
        var loadout = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitLocationResolver.Resolve(Rules, loadout, loadout, 1UL, 1L, 0UL, 2UL));
    }

    [Fact]
    public void Resolve_RejectsAZeroTargetEntityId()
    {
        var loadout = new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitLocationResolver.Resolve(Rules, loadout, loadout, 1UL, 1L, 1UL, 0UL));
    }

    [Fact]
    public void MixAttack_ChangesWhenAnySingleFieldChanges()
    {
        var baseline = HitLocationResolver.MixAttack(1UL, 1L, 1UL, 2UL, WeaponId.GreatBlade);

        Assert.NotEqual(baseline, HitLocationResolver.MixAttack(2UL, 1L, 1UL, 2UL, WeaponId.GreatBlade));
        Assert.NotEqual(baseline, HitLocationResolver.MixAttack(1UL, 2L, 1UL, 2UL, WeaponId.GreatBlade));
        Assert.NotEqual(baseline, HitLocationResolver.MixAttack(1UL, 1L, 3UL, 2UL, WeaponId.GreatBlade));
        Assert.NotEqual(baseline, HitLocationResolver.MixAttack(1UL, 1L, 1UL, 3UL, WeaponId.GreatBlade));
        Assert.NotEqual(baseline, HitLocationResolver.MixAttack(1UL, 1L, 1UL, 2UL, WeaponId.Bolo));
    }
}
