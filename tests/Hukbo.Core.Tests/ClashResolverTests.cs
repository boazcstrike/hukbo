using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

/// <summary>
/// Coverage for the defensive-resolution mixer, the six-step channel
/// computation, and the five-way interval walk.
/// </summary>
/// <remarks>
/// <para>
/// <b>No case in this file reads <see cref="PhilippineCombatPreset"/>.</b> Every
/// profile below is an explicit literal written out here from design section
/// 3.3. Two reasons, both from the plan. A sweep reading the preset would have
/// the resolver and the oracle agree on all 6,400 tuples while the preset still
/// carries <see cref="ClashProfile.Neutral"/>, passing green while proving
/// nothing. And a vector pinned against the preset could not have its expected
/// values derived until the preset tables are populated, so they would end up
/// pasted from output afterward, which is exactly the self-fulfilling golden the
/// independent oracle exists to prevent. Literal profiles also decouple every
/// case here from any later re-tune of the shipped tables.
/// </para>
/// <para>
/// <b>PROVISIONAL.</b> Every number below is a gameplay tuning choice, not a
/// historical measurement. The research is explicit that all sixteen cells of
/// the weapon-intercept matrix have no evidentiary confidence whatsoever.
/// </para>
/// </remarks>
public sealed class ClashResolverTests
{
    /// <summary>
    /// The tuple whose roll the interval-edge cases are built around. Its
    /// expected roll is pinned independently by
    /// <see cref="MixClash_MatchesEveryPinnedVector"/>, so the edge profiles
    /// below can place a cumulative boundary exactly on it.
    /// </summary>
    /// <remarks>
    /// The pinned row is the <see cref="ShieldId.TallHardwood"/> one, because
    /// <see cref="ResolveAtEdge"/> must resolve with a shield for the first
    /// interval edge to be reachable at all. Both the helper that folds this
    /// tuple and the helper that resolves it therefore have to name the same
    /// shield: an earlier revision folded <see cref="ShieldId.None"/> here
    /// while resolving with <see cref="ShieldId.TallHardwood"/>, which pinned
    /// this constant to one row of the theory and the edge walk to another and
    /// made the two mutually unsatisfiable for any mixer that depends on the
    /// shield at all.
    /// </remarks>
    private const ulong EdgeSeed = 0;

    private const long EdgeTick = 0;
    private const ulong EdgeSource = 60;
    private const ulong EdgeTarget = 1;
    private const int EdgeRoll = 1_261;

    /// <summary>
    /// A tuple whose roll is exactly zero, found by walking ticks for a fixed
    /// seed and loadout triple. It is the only input that can select a
    /// zero-width channel through a <c>roll &lt;= cumulative</c> comparison, so
    /// the zero-width case sweeps it explicitly rather than hoping to hit it.
    /// </summary>
    private const ulong ZeroRollSeed = 1;

    private const long ZeroRollTick = 9_779;
    private const ulong ZeroRollSource = 1;
    private const ulong ZeroRollTarget = 2;

    private static readonly ClashProfile ShippedTables = BuildShippedTables();

    // Independently calculated golden vectors. Derivation method for each row:
    // expectedRoll is the 64-bit FNV-1a fold (offset basis
    // 14695981039346656037, prime 1099511628211) of, in order, the fixed tag
    // 0x484B424F5F434C53 ("HKBO_CLS"), seed, tick (reinterpreted as an unsigned
    // 64-bit bit pattern), sourceEntityId, targetEntityId, the attacking weapon
    // cast to ulong, the defending weapon cast to ulong, and the defending
    // shield cast to ulong -- each value byte-folded low byte first across eight
    // rounds of XOR-then-multiply, exactly as HitLocationResolver.MixAttack
    // folds its six words -- then taken modulo 10,000. expectedResolution is
    // that roll walked through the cumulative interval
    // [0, shield) [shield, +hard) [+soft) [+void) rest, against the channels the
    // literal shipped tables below produce for that loadout pairing. Both
    // columns were computed by a separate transcription of design section 3.3
    // rather than by running the resolver.
    //
    // Coverage the plan requires: seed 0 and the maximum unsigned seed, tick 0
    // and the maximum tick, all four weapons on both sides, both shields, and at
    // least one row per resolution -- all five. The void interval is roughly
    // 1,000 of 10,000 and is the only interval bounded on both sides, so eight
    // arbitrary tuples could easily never produce an Evaded and would leave the
    // newest interval unexercised. Row two is also the one loadout whose total
    // exceeds the 5,500 ceiling -- 2,200 plus 2,400 plus 1,000 -- so the step
    // four rescale branch is exercised by a pinned vector and not only by the
    // synthetic clamp profiles.
    //
    // These rolls were re-derived once. The first revision folded every enum as
    // a zero-based ordinal, that is the declared value minus one, which
    // contradicts both the sentence above and HitLocationResolver.MixAttack at
    // line 100, the mixer design section 3.3 says this one copies the shape of.
    // The resolution column was correct throughout and each row still resolves
    // as it did; only the encoding moved, and with it the entity identifiers,
    // which were re-chosen so that every row keeps the resolution it was
    // selected to demonstrate. The rolls below were produced by a transcription
    // of section 3.3 validated against all six pre-existing
    // HitLocationResolverTests vectors, never by running ClashResolver.
    [Theory]
    [InlineData(0UL, 0L, 60UL, 1UL, WeaponId.Kampilan, WeaponId.Kampilan, ShieldId.None, 670, AttackResolution.Parried)]
    [InlineData(0UL, 0L, 60UL, 1UL, WeaponId.Kampilan, WeaponId.Kampilan, ShieldId.TallHardwood, 1_261, AttackResolution.ShieldBlocked)]
    [InlineData(0UL, 0L, 60UL, 1UL, WeaponId.Wasay, WeaponId.Itak, ShieldId.TallHardwood, 2_811, AttackResolution.Evaded)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0x7FFFFFFFFFFFFFFFL, 4UL, 2UL, WeaponId.Kalis, WeaponId.Itak, ShieldId.TallHardwood, 33, AttackResolution.ShieldBlocked)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0x7FFFFFFFFFFFFFFFL, 4UL, 2UL, WeaponId.Itak, WeaponId.Wasay, ShieldId.None, 6_995, AttackResolution.Landed)]
    [InlineData(1UL, 1L, 10UL, 15UL, WeaponId.Kalis, WeaponId.Kalis, ShieldId.None, 566, AttackResolution.Deflected)]
    [InlineData(1UL, 1L, 15UL, 10UL, WeaponId.Wasay, WeaponId.Kalis, ShieldId.None, 55, AttackResolution.Parried)]
    [InlineData(0xDEADBEEFUL, 99L, 198UL, 200UL, WeaponId.Wasay, WeaponId.Wasay, ShieldId.TallHardwood, 2_539, AttackResolution.Parried)]
    [InlineData(987654321UL, 1234L, 87UL, 18UL, WeaponId.Kampilan, WeaponId.Wasay, ShieldId.None, 2_046, AttackResolution.Evaded)]
    [InlineData(987654321UL, 1234L, 87UL, 18UL, WeaponId.Itak, WeaponId.Itak, ShieldId.TallHardwood, 9_886, AttackResolution.Landed)]
    public void MixClash_MatchesEveryPinnedVector(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield,
        int expectedRoll,
        AttackResolution expectedResolution)
    {
        var roll = ClashResolver.MixClash(
            seed,
            tick,
            sourceEntityId,
            targetEntityId,
            attackerWeapon,
            defenderWeapon,
            defenderShield);
        var resolved = ClashResolver.Resolve(
            ShippedTables,
            seed,
            tick,
            sourceEntityId,
            targetEntityId,
            attackerWeapon,
            defenderWeapon,
            defenderShield);

        Assert.Equal(expectedRoll, roll);
        Assert.Equal(expectedResolution, resolved);
    }

    // Seven single-word isolation cases, one per folded word other than the
    // constant domain tag. A dropped word is invisible to any distribution test:
    // the roll stays uniform, the outcome frequencies stay correct, and only the
    // dependence on that one input disappears.

    [Fact]
    public void MixClash_ChangesWhenOnlyTheSeedChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                2,
                7,
                11,
                12,
                WeaponId.Kampilan,
                WeaponId.Itak,
                ShieldId.TallHardwood),
            "the scenario seed");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheTickChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                8,
                11,
                12,
                WeaponId.Kampilan,
                WeaponId.Itak,
                ShieldId.TallHardwood),
            "the tick");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheSourceEntityIdChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                7,
                13,
                12,
                WeaponId.Kampilan,
                WeaponId.Itak,
                ShieldId.TallHardwood),
            "the source entity ID");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheTargetEntityIdChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                7,
                11,
                14,
                WeaponId.Kampilan,
                WeaponId.Itak,
                ShieldId.TallHardwood),
            "the target entity ID");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheAttackerWeaponChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                7,
                11,
                12,
                WeaponId.Wasay,
                WeaponId.Itak,
                ShieldId.TallHardwood),
            "the attacking weapon");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheDefenderWeaponChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                7,
                11,
                12,
                WeaponId.Kampilan,
                WeaponId.Kalis,
                ShieldId.TallHardwood),
            "the defending weapon");

    [Fact]
    public void MixClash_ChangesWhenOnlyTheDefenderShieldChanges() =>
        AssertRollsDiffer(
            BaselineRoll(),
            ClashResolver.MixClash(
                1,
                7,
                11,
                12,
                WeaponId.Kampilan,
                WeaponId.Itak,
                ShieldId.None),
            "the defending shield");

    /// <summary>
    /// The resolver against the independent naive reference across the whole
    /// roster matrix. Both read the same profile, which is data rather than
    /// behaviour, and the roll is supplied by the mixer, which is pinned
    /// separately above. This is the only case that catches a sign error, a
    /// rescale that charges the ceiling to one channel, or a truncation that
    /// leaks a basis point.
    /// </summary>
    [Fact]
    public void Resolve_MatchesTheNaiveReferenceAcrossTheWholeRosterMatrix() =>
        AssertMatchesNaiveReference(ShippedTables, nameof(ShippedTables));

    /// <summary>
    /// The same sweep over profiles whose channels sum past the interception
    /// ceiling. The shipped tables top out well below it for every roster
    /// loadout, so a sweep restricted to them barely enters the rescale branch
    /// at all, and the truncating three-way rescale is the most error-prone step
    /// in the pipeline.
    /// </summary>
    [Fact]
    public void Resolve_MatchesTheNaiveReferenceOnOverCeilingProfiles()
    {
        var profiles = new[]
        {
            (Name: "shield-heavy", Profile: BuildUniformProfile(
                shieldIntercept: 3_000,
                weaponIntercept: 2_600,
                voidChannel: 900,
                hardShare: 5_000,
                ceiling: 5_500)),
            (Name: "weapon-heavy", Profile: BuildUniformProfile(
                shieldIntercept: 900,
                weaponIntercept: 6_000,
                voidChannel: 1_500,
                hardShare: 2_500,
                ceiling: 5_500)),
            (Name: "void-heavy", Profile: BuildUniformProfile(
                shieldIntercept: 1_000,
                weaponIntercept: 1_000,
                voidChannel: 7_777,
                hardShare: 9_999,
                ceiling: 4_321)),
        };

        foreach (var (name, profile) in profiles)
        {
            AssertMatchesNaiveReference(profile, name);
        }
    }

    /// <summary>
    /// The roll exactly at each of the four cumulative boundaries selects the
    /// documented side, and one basis point either way moves it. Each profile
    /// places a boundary exactly on the pinned roll of one fixed tuple, so the
    /// comparison is a plain equality rather than a distribution.
    /// </summary>
    [Fact]
    public void Resolve_SelectsTheOutcomeAtEveryIntervalEdge()
    {
        Assert.Equal(EdgeRoll, EdgeRollValue());

        // Edge one, the shield boundary. The shield interval is [0, shield), so
        // a roll equal to the shield width belongs to the next channel.
        Assert.Equal(
            AttackResolution.Parried,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: EdgeRoll,
                weaponIntercept: 1_000,
                voidChannel: 500,
                hardShare: ClashProfile.BasisPointScale)));
        Assert.Equal(
            AttackResolution.ShieldBlocked,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: EdgeRoll + 1,
                weaponIntercept: 1_000,
                voidChannel: 500,
                hardShare: ClashProfile.BasisPointScale)));

        // Edge two, the hard boundary. A hard share of 9,993 against a 1,262
        // basis point weapon channel truncates to 1,261, leaving a single basis
        // point of soft, so the roll sits exactly on the parry/deflect boundary.
        Assert.Equal(
            AttackResolution.Deflected,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: EdgeRoll + 1,
                voidChannel: 500,
                hardShare: 9_993)));
        Assert.Equal(
            AttackResolution.Parried,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: EdgeRoll + 1,
                voidChannel: 500,
                hardShare: ClashProfile.BasisPointScale)));

        // Edge three, the soft boundary. A hard share of zero puts the whole
        // weapon channel into soft, and the zero-width hard interval in front of
        // it must be stepped over rather than selected.
        Assert.Equal(
            AttackResolution.Evaded,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: EdgeRoll,
                voidChannel: 500,
                hardShare: 0)));
        Assert.Equal(
            AttackResolution.Deflected,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: EdgeRoll + 1,
                voidChannel: 500,
                hardShare: 0)));

        // Edge four, the void boundary, which is the only interval bounded on
        // both sides and therefore the one most likely to be wrong.
        Assert.Equal(
            AttackResolution.Landed,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: 0,
                voidChannel: EdgeRoll,
                hardShare: 0)));
        Assert.Equal(
            AttackResolution.Evaded,
            ResolveAtEdge(BuildEdgeProfile(
                shieldIntercept: 0,
                weaponIntercept: 0,
                voidChannel: EdgeRoll + 1,
                hardShare: 0)));
    }

    /// <summary>
    /// A total of exactly the ceiling is left alone. Off-by-one here would
    /// rescale a legal profile and silently shrink every channel.
    /// </summary>
    [Fact]
    public void Clamp_LeavesATotalOfExactlyFiveThousandFiveHundredUnscaled()
    {
        var profile = BuildUniformProfile(
            shieldIntercept: 2_400,
            weaponIntercept: 2_100,
            voidChannel: 1_000,
            hardShare: 5_000,
            ceiling: 5_500);

        var channels = ClashResolver.ComputeChannels(
            profile,
            WeaponId.Kampilan,
            WeaponId.Itak,
            ShieldId.TallHardwood);

        Assert.Equal(2_400, channels.Shield);
        Assert.Equal(2_100, channels.Weapon);
        Assert.Equal(1_000, channels.Void);
        Assert.Equal(5_500, channels.Shield + channels.Weapon + channels.Void);
    }

    /// <summary>
    /// One basis point past the ceiling rescales, and the rescale divides each
    /// channel independently so the proportions survive.
    /// </summary>
    /// <remarks>
    /// Expected values derived by hand from design section 3.3 step four, every
    /// division truncating toward zero. 2400/2101/1000 sums to 5501 against a
    /// 5500 ceiling, giving 2400*5500/5501 = 2399, 2101*5500/5501 = 2100, and
    /// 1000*5500/5501 = 999. 3000/2600/900 sums to 6500, giving
    /// 3000*5500/6500 = 2538, 2600*5500/6500 = 2200, and 900*5500/6500 = 761.
    /// Both land below the ceiling, with the small unallocated residue
    /// truncation leaves becoming additional Landed probability.
    /// </remarks>
    [Fact]
    public void Clamp_PreservesChannelProportionsOnRescale()
    {
        var justOver = ClashResolver.ComputeChannels(
            BuildUniformProfile(
                shieldIntercept: 2_400,
                weaponIntercept: 2_101,
                voidChannel: 1_000,
                hardShare: 5_000,
                ceiling: 5_500),
            WeaponId.Kampilan,
            WeaponId.Itak,
            ShieldId.TallHardwood);

        Assert.Equal(2_399, justOver.Shield);
        Assert.Equal(2_100, justOver.Weapon);
        Assert.Equal(999, justOver.Void);

        var wellOver = ClashResolver.ComputeChannels(
            BuildUniformProfile(
                shieldIntercept: 3_000,
                weaponIntercept: 2_600,
                voidChannel: 900,
                hardShare: 5_000,
                ceiling: 5_500),
            WeaponId.Kampilan,
            WeaponId.Itak,
            ShieldId.TallHardwood);

        Assert.Equal(2_538, wellOver.Shield);
        Assert.Equal(2_200, wellOver.Weapon);
        Assert.Equal(761, wellOver.Void);

        // The ordering of the three channels survives the rescale, which is what
        // "proportions are preserved" has to mean for truncating integers.
        Assert.True(wellOver.Shield > wellOver.Weapon);
        Assert.True(wellOver.Weapon > wellOver.Void);
    }

    /// <summary>
    /// Neither hard-share clamp bound is reachable with shipped values, so both
    /// are exercised against synthetic profiles. Passing the full basis-point
    /// scale as the weapon channel makes the returned hard half equal the
    /// resolved hard share exactly.
    /// </summary>
    [Fact]
    public void HardShare_BindsAtBothClampBounds()
    {
        var belowFloor = BuildClampProfile(hardShareBase: 100);
        var aboveCeiling = BuildClampProfile(hardShareBase: ClashProfile.BasisPointScale);

        var floored = ClashResolver.SplitWeaponChannel(
            belowFloor,
            ClashProfile.BasisPointScale,
            WeaponId.Kampilan,
            WeaponId.Itak);
        var capped = ClashResolver.SplitWeaponChannel(
            aboveCeiling,
            ClashProfile.BasisPointScale,
            WeaponId.Kampilan,
            WeaponId.Itak);

        Assert.Equal(500, floored.Hard);
        Assert.Equal(ClashProfile.BasisPointScale - 500, floored.Soft);
        Assert.Equal(6_000, capped.Hard);
        Assert.Equal(ClashProfile.BasisPointScale - 6_000, capped.Soft);
    }

    /// <summary>
    /// A zero-width channel is never selected. With
    /// <see cref="ShieldId.None"/> the shield interval is <c>[0, 0)</c>, and a
    /// comparison written <c>roll &lt;= cumulative</c> rather than
    /// <c>roll &lt; cumulative</c> would select it on a roll of zero. That is a
    /// one-character defect no distribution test would surface, so the sweep
    /// includes a tuple whose roll is exactly zero.
    /// </summary>
    [Fact]
    public void Resolve_NeverSelectsAZeroWidthChannel()
    {
        // Only the soft half has any width: no shield on the defender, a hard
        // share of zero, and no void channel.
        var profile = BuildUniformProfile(
            shieldIntercept: 2_400,
            weaponIntercept: 500,
            voidChannel: 0,
            hardShare: 0,
            ceiling: ClashProfile.BasisPointScale);

        Assert.Equal(
            0,
            ClashResolver.MixClash(
                ZeroRollSeed,
                ZeroRollTick,
                ZeroRollSource,
                ZeroRollTarget,
                WeaponId.Kampilan,
                WeaponId.Kampilan,
                ShieldId.None));

        var selected = new HashSet<AttackResolution>
        {
            ClashResolver.Resolve(
                profile,
                ZeroRollSeed,
                ZeroRollTick,
                ZeroRollSource,
                ZeroRollTarget,
                WeaponId.Kampilan,
                WeaponId.Kampilan,
                ShieldId.None),
        };

        foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
            {
                for (var tick = 1L; tick <= 200; tick++)
                {
                    selected.Add(
                        ClashResolver.Resolve(
                            profile,
                            ZeroRollSeed,
                            tick,
                            ZeroRollSource,
                            ZeroRollTarget,
                            attackerWeapon,
                            defenderWeapon,
                            ShieldId.None));
                }
            }
        }

        Assert.DoesNotContain(AttackResolution.ShieldBlocked, selected);
        Assert.DoesNotContain(AttackResolution.Parried, selected);
        Assert.DoesNotContain(AttackResolution.Evaded, selected);
    }

    /// <summary>
    /// A profile with no interception at all always lands. This is the property
    /// the zero-interception control run depends on.
    /// </summary>
    [Fact]
    public void Resolve_AlwaysLandsAtZeroTotalInterception()
    {
        foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
            {
                foreach (var defenderShield in Enum.GetValues<ShieldId>())
                {
                    for (var tick = 1L; tick <= 200; tick++)
                    {
                        Assert.Equal(
                            AttackResolution.Landed,
                            ClashResolver.Resolve(
                                ClashProfile.Neutral,
                                seed: 9,
                                tick,
                                sourceEntityId: 5,
                                targetEntityId: 6,
                                attackerWeapon,
                                defenderWeapon,
                                defenderShield));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The post-rescale total never exceeds the ceiling and no channel goes
    /// negative, across the shipped tables and every over-ceiling profile.
    /// </summary>
    [Fact]
    public void Clamp_NeverExceedsTheCeiling()
    {
        var profiles = new[]
        {
            ShippedTables,
            BuildUniformProfile(
                shieldIntercept: 3_000,
                weaponIntercept: 2_600,
                voidChannel: 900,
                hardShare: 5_000,
                ceiling: 5_500),
            BuildUniformProfile(
                shieldIntercept: ClashProfile.BasisPointScale,
                weaponIntercept: ClashProfile.BasisPointScale,
                voidChannel: ClashProfile.BasisPointScale,
                hardShare: ClashProfile.BasisPointScale,
                ceiling: 1),
        };

        foreach (var profile in profiles)
        {
            foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
            {
                foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
                {
                    foreach (var defenderShield in Enum.GetValues<ShieldId>())
                    {
                        var channels = ClashResolver.ComputeChannels(
                            profile,
                            attackerWeapon,
                            defenderWeapon,
                            defenderShield);

                        Assert.True(channels.Shield >= 0);
                        Assert.True(channels.Weapon >= 0);
                        Assert.True(channels.Hard >= 0);
                        Assert.True(channels.Soft >= 0);
                        Assert.True(channels.Void >= 0);
                        Assert.True(
                            channels.Shield + channels.Weapon + channels.Void <=
                                profile.MaximumInterceptionBasisPoints,
                            "The post-rescale total exceeded the interception " +
                            "ceiling.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// PROVISIONAL gameplay-tuning comparison, not a historical claim. The
    /// shield is the only defensive channel with sixteenth-century documentary
    /// support, so a shielded defender must take blows on the shield far more
    /// often than the weapon arrests them. Asserted as a band rather than a
    /// figure.
    /// </summary>
    [Fact]
    public void Resolve_TallHardwoodBlocksMoreOftenThanItParries()
    {
        var blocked = 0;
        var parried = 0;

        foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
            {
                for (var tick = 1L; tick <= 500; tick++)
                {
                    var resolution = ClashResolver.Resolve(
                        ShippedTables,
                        seed: 4_242,
                        tick,
                        sourceEntityId: 21,
                        targetEntityId: 22,
                        attackerWeapon,
                        defenderWeapon,
                        ShieldId.TallHardwood);

                    if (resolution == AttackResolution.ShieldBlocked)
                    {
                        blocked++;
                    }
                    else if (resolution == AttackResolution.Parried)
                    {
                        parried++;
                    }
                }
            }
        }

        Assert.True(
            blocked > parried * 2,
            "PROVISIONAL band. Expected a tall hardwood shield to take at " +
            $"least twice as many blows as the weapon arrests, but counted " +
            $"{blocked} blocks against {parried} parries.");
    }

    /// <summary>
    /// The split of the weapon channel neither leaks nor invents a basis point.
    /// The comparison is against the <c>Weapon</c> member of the computed
    /// channels, which is the post-rescale value the interval walk uses, under a
    /// precondition that it is positive. Without that member the invariant would
    /// read <c>hard + soft == hard + soft</c>, pass on zeros, and never be able
    /// to fail.
    /// </summary>
    [Fact]
    public void SplitWeaponChannel_HardPlusSoftEqualsTheRescaledWeaponChannel()
    {
        var profiles = new[]
        {
            ShippedTables,
            BuildUniformProfile(
                shieldIntercept: 3_000,
                weaponIntercept: 2_600,
                voidChannel: 900,
                hardShare: 5_000,
                ceiling: 5_500),
        };

        foreach (var profile in profiles)
        {
            foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
            {
                foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
                {
                    foreach (var defenderShield in Enum.GetValues<ShieldId>())
                    {
                        var channels = ClashResolver.ComputeChannels(
                            profile,
                            attackerWeapon,
                            defenderWeapon,
                            defenderShield);

                        Assert.True(
                            channels.Weapon > 0,
                            "The weapon channel must be positive for this " +
                            "invariant to say anything. Attacker " +
                            $"{attackerWeapon}, defender {defenderWeapon}, " +
                            $"shield {defenderShield}.");

                        var split = ClashResolver.SplitWeaponChannel(
                            profile,
                            channels.Weapon,
                            attackerWeapon,
                            defenderWeapon);

                        Assert.Equal(channels.Weapon, split.Hard + split.Soft);
                        Assert.Equal(channels.Hard, split.Hard);
                        Assert.Equal(channels.Soft, split.Soft);
                    }
                }
            }
        }
    }

    /// <summary>
    /// PROVISIONAL gameplay-tuning comparison, not a historical claim. The
    /// heaviest incoming weapon meeting the only instrument capable of arresting
    /// it is the roster extreme, and the lightest pair is the opposite one. The
    /// sixfold spread is the most legible contrast the roster offers and
    /// flattening it would discard the thing that makes the system readable.
    /// </summary>
    [Fact]
    public void HardShare_SpansTheRosterRangeFromHeavyPairToLightPair()
    {
        var heavyPair = ClashResolver.SplitWeaponChannel(
            ShippedTables,
            ClashProfile.BasisPointScale,
            WeaponId.Wasay,
            WeaponId.Kampilan);
        var lightPair = ClashResolver.SplitWeaponChannel(
            ShippedTables,
            ClashProfile.BasisPointScale,
            WeaponId.Kalis,
            WeaponId.Itak);

        // 4000 * 1150 / 1000 and 1200 * 700 / 1000, both inside the 500 to 6000
        // clamp, so neither bound binds here.
        Assert.Equal(4_600, heavyPair.Hard);
        Assert.Equal(840, lightPair.Hard);
        Assert.True(
            heavyPair.Hard >= lightPair.Hard * 5,
            "PROVISIONAL band. Expected at least a fivefold spread between " +
            $"the heaviest and lightest pairing, but measured {heavyPair.Hard} " +
            $"against {lightPair.Hard}.");
    }

    /// <summary>
    /// A defender carrying no shield is never recorded as having blocked with
    /// one. A stub that never blocks already satisfies this; it exists to catch
    /// a later regression, not to drive the implementation.
    /// </summary>
    [Fact]
    public void Resolve_NeverBlocksWithoutAShield()
    {
        foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
            {
                for (var tick = 1L; tick <= 500; tick++)
                {
                    Assert.NotEqual(
                        AttackResolution.ShieldBlocked,
                        ClashResolver.Resolve(
                            ShippedTables,
                            seed: 4_242,
                            tick,
                            sourceEntityId: 21,
                            targetEntityId: 22,
                            attackerWeapon,
                            defenderWeapon,
                            ShieldId.None));
                }
            }
        }
    }

    private static void AssertMatchesNaiveReference(ClashProfile profile, string profileName)
    {
        var mismatches = new List<string>();

        foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
            {
                foreach (var defenderShield in Enum.GetValues<ShieldId>())
                {
                    for (var tick = 1L; tick <= 200 && mismatches.Count < 10; tick++)
                    {
                        var roll = ClashResolver.MixClash(
                            seed: 1,
                            tick,
                            sourceEntityId: 31,
                            targetEntityId: 32,
                            attackerWeapon,
                            defenderWeapon,
                            defenderShield);
                        var expected = NaiveClashResolution.Resolve(
                            profile,
                            roll,
                            attackerWeapon,
                            defenderWeapon,
                            defenderShield);
                        var actual = ClashResolver.Resolve(
                            profile,
                            seed: 1,
                            tick,
                            sourceEntityId: 31,
                            targetEntityId: 32,
                            attackerWeapon,
                            defenderWeapon,
                            defenderShield);

                        if (expected != actual)
                        {
                            mismatches.Add(
                                $"tick {tick}, attacker {attackerWeapon}, " +
                                $"defender {defenderWeapon}, shield " +
                                $"{defenderShield}, roll {roll}: reference " +
                                $"{expected} against resolver {actual}");
                        }
                    }
                }
            }
        }

        Assert.True(
            mismatches.Count == 0,
            $"The resolver disagreed with the naive reference on profile " +
            $"'{profileName}'. First divergences: " +
            string.Join("; ", mismatches));
    }

    private static void AssertRollsDiffer(int baseline, int changed, string word) =>
        Assert.True(
            baseline != changed,
            $"The clash mixer ignored {word}: both tuples rolled {baseline}.");

    private static int BaselineRoll() =>
        ClashResolver.MixClash(
            seed: 1,
            tick: 7,
            sourceEntityId: 11,
            targetEntityId: 12,
            WeaponId.Kampilan,
            WeaponId.Itak,
            ShieldId.TallHardwood);

    /// <summary>
    /// Folds the same shield <see cref="ResolveAtEdge"/> resolves with. The two
    /// must agree: <see cref="EdgeRoll"/> is the roll the edge profiles place a
    /// boundary on, so a mixer that depends on the shield produces a different
    /// roll here than the walk consumes if the two helpers disagree.
    /// </summary>
    private static int EdgeRollValue() =>
        ClashResolver.MixClash(
            EdgeSeed,
            EdgeTick,
            EdgeSource,
            EdgeTarget,
            WeaponId.Kampilan,
            WeaponId.Kampilan,
            ShieldId.TallHardwood);

    /// <summary>
    /// Resolved with <see cref="ShieldId.TallHardwood"/> so the shield channel
    /// has a width at all: <see cref="ClashProfile.ResolveShieldIntercept"/>
    /// returns zero for a defender carrying no shield, which would make the
    /// first interval edge unreachable.
    /// </summary>
    private static AttackResolution ResolveAtEdge(ClashProfile profile) =>
        ClashResolver.Resolve(
            profile,
            EdgeSeed,
            EdgeTick,
            EdgeSource,
            EdgeTarget,
            WeaponId.Kampilan,
            WeaponId.Kampilan,
            ShieldId.TallHardwood);

    /// <summary>
    /// A profile with no clamp and no reachable ceiling, so each interval edge
    /// lands exactly where the arithmetic at the call site puts it.
    /// </summary>
    private static ClashProfile BuildEdgeProfile(
        int shieldIntercept,
        int weaponIntercept,
        int voidChannel,
        int hardShare) =>
        BuildUniformProfile(
            shieldIntercept,
            weaponIntercept,
            voidChannel,
            hardShare,
            ceiling: ClashProfile.BasisPointScale);

    private static ClashProfile BuildClampProfile(int hardShareBase) =>
        BuildProfile(
            cell: (_, _) => ClashProfile.BasisPointScale,
            shieldIntercept: 0,
            voidValue: _ => 0,
            hardShareBase: _ => hardShareBase,
            hardShareMultiplier: _ => ClashProfile.HardShareMultiplierScale,
            minimumHardShare: 500,
            maximumHardShare: 6_000,
            ceiling: ClashProfile.BasisPointScale);

    private static ClashProfile BuildUniformProfile(
        int shieldIntercept,
        int weaponIntercept,
        int voidChannel,
        int hardShare,
        int ceiling) =>
        BuildProfile(
            cell: (_, _) => weaponIntercept,
            shieldIntercept: shieldIntercept,
            voidValue: _ => voidChannel,
            hardShareBase: _ => hardShare,
            hardShareMultiplier: _ => ClashProfile.HardShareMultiplierScale,
            minimumHardShare: 0,
            maximumHardShare: ClashProfile.BasisPointScale,
            ceiling: ceiling);

    /// <summary>
    /// The design section 3.3 tables, written out here rather than read from
    /// the preset. See the remarks on this class for why.
    /// </summary>
    private static ClashProfile BuildShippedTables()
    {
        var weaponIntercept = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>
        {
            [(WeaponId.Kampilan, WeaponId.Kampilan)] = 2_200,
            [(WeaponId.Kampilan, WeaponId.Wasay)] = 1_900,
            [(WeaponId.Kampilan, WeaponId.Kalis)] = 1_600,
            [(WeaponId.Kampilan, WeaponId.Itak)] = 2_000,
            [(WeaponId.Wasay, WeaponId.Kampilan)] = 1_500,
            [(WeaponId.Wasay, WeaponId.Wasay)] = 1_300,
            [(WeaponId.Wasay, WeaponId.Kalis)] = 1_100,
            [(WeaponId.Wasay, WeaponId.Itak)] = 1_400,
            [(WeaponId.Kalis, WeaponId.Kampilan)] = 500,
            [(WeaponId.Kalis, WeaponId.Wasay)] = 400,
            [(WeaponId.Kalis, WeaponId.Kalis)] = 600,
            [(WeaponId.Kalis, WeaponId.Itak)] = 600,
            [(WeaponId.Itak, WeaponId.Kampilan)] = 400,
            [(WeaponId.Itak, WeaponId.Wasay)] = 300,
            [(WeaponId.Itak, WeaponId.Kalis)] = 500,
            [(WeaponId.Itak, WeaponId.Itak)] = 500,
        };

        return new ClashProfile(
            weaponIntercept,
            shieldIntercept: 2_400,
            voidChannel: new Dictionary<WeaponId, int>
            {
                [WeaponId.Kampilan] = 1_000,
                [WeaponId.Wasay] = 900,
                [WeaponId.Kalis] = 1_000,
                [WeaponId.Itak] = 1_100,
            },
            hardShareBases: new Dictionary<WeaponId, int>
            {
                [WeaponId.Kampilan] = 3_300,
                [WeaponId.Wasay] = 4_000,
                [WeaponId.Kalis] = 1_200,
                [WeaponId.Itak] = 1_800,
            },
            hardShareMultipliers: new Dictionary<WeaponId, int>
            {
                [WeaponId.Kampilan] = 1_150,
                [WeaponId.Wasay] = 1_050,
                [WeaponId.Kalis] = 750,
                [WeaponId.Itak] = 700,
            },
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);
    }

    private static ClashProfile BuildProfile(
        Func<WeaponId, WeaponId, int> cell,
        int shieldIntercept,
        Func<WeaponId, int> voidValue,
        Func<WeaponId, int> hardShareBase,
        Func<WeaponId, int> hardShareMultiplier,
        int minimumHardShare,
        int maximumHardShare,
        int ceiling)
    {
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = cell(defender, attacker);
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept,
            weapons.ToDictionary(weapon => weapon, voidValue),
            weapons.ToDictionary(weapon => weapon, hardShareBase),
            weapons.ToDictionary(weapon => weapon, hardShareMultiplier),
            minimumHardShare,
            maximumHardShare,
            ceiling);
    }
}
