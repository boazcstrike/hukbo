using System.Reflection;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// <see cref="PawnAppearanceCache"/> against its declaration (GPU-017): the
/// cold-cache equivalence the cache is only allowed to exist if it satisfies,
/// the size bound that keeps it from ever growing, and the loadout-immutability
/// assumption every one of its hits silently rests on.
/// </summary>
public sealed class PawnAppearanceCacheTests
{
    private static readonly WeaponId[] Weapons =
    [
        WeaponId.Kampilan,
        WeaponId.Wasay,
        WeaponId.Kalis,
        WeaponId.Itak,
    ];

    private static readonly ShieldId[] Shields =
    [
        ShieldId.None,
        ShieldId.TallHardwood,
    ];

    /// <summary>
    /// The cache's whole reason to exist is that a warm read is
    /// indistinguishable from a cold one. Every agent in a full-capacity
    /// roster is resolved three times — once against an empty cache, then
    /// twice more against the warm one — and every result must equal what
    /// <see cref="PawnAppearanceFactory"/>, the single authority, produces on
    /// its own. Index 0 deliberately carries entity ID 0, so a slot holding a
    /// genuine zero identity is covered rather than mistaken for an empty
    /// slot.
    /// </summary>
    [Fact]
    public void ColdAndWarmCachesResolveTheFactoryAppearanceForEveryAgent()
    {
        var cache = new PawnAppearanceCache(NullRenderMetricsRecorder.Instance);

        for (var frame = 0; frame < 3; frame++)
        {
            for (var ordinal = 0;
                ordinal < PawnAppearanceCache.Capacity;
                ordinal++)
            {
                var (entityId, weapon, shield) = Agent(ordinal);

                Assert.Equal(
                    PawnAppearanceFactory.Create(entityId, weapon, shield),
                    cache.Resolve(ordinal, entityId, weapon, shield, isLeader: false));
            }
        }
    }

    /// <summary>
    /// The size bound, pinned two ways. The constant itself must stay
    /// <c>2 * ArmyCompositionStepper.MaximumUnitsPerTeam</c>, and the cache
    /// must actually behave as though it cannot grow: filling every slot and
    /// then reading past the bound leaves the fill exactly where it was,
    /// while still answering correctly from the factory. A negative ordinal
    /// is covered for the same reason — it addresses no slot either.
    /// </summary>
    [Fact]
    public void TheCacheCannotGrowBeyondTwiceTheMaximumUnitsPerTeam()
    {
        Assert.Equal(
            2 * ArmyCompositionStepper.MaximumUnitsPerTeam,
            PawnAppearanceCache.Capacity);

        var recorder = new SpriteBatchRenderMetricsRecorder();
        var cache = new PawnAppearanceCache(recorder);

        for (var ordinal = 0;
            ordinal < PawnAppearanceCache.Capacity;
            ordinal++)
        {
            var (entityId, weapon, shield) = Agent(ordinal);
            cache.Resolve(ordinal, entityId, weapon, shield, isLeader: false);
        }

        Assert.Equal(PawnAppearanceCache.Capacity, cache.Fill);

        const int beyond = 16;

        for (var ordinal = PawnAppearanceCache.Capacity;
            ordinal < PawnAppearanceCache.Capacity + beyond;
            ordinal++)
        {
            var (entityId, weapon, shield) = Agent(ordinal);

            Assert.Equal(
                PawnAppearanceFactory.Create(entityId, weapon, shield),
                cache.Resolve(ordinal, entityId, weapon, shield, isLeader: false));
        }

        var (outOfRangeId, outOfRangeWeapon, outOfRangeShield) = Agent(3);

        Assert.Equal(
            PawnAppearanceFactory.Create(
                outOfRangeId,
                outOfRangeWeapon,
                outOfRangeShield),
            cache.Resolve(
                -1,
                outOfRangeId,
                outOfRangeWeapon,
                outOfRangeShield,
                isLeader: false));

        Assert.Equal(PawnAppearanceCache.Capacity, cache.Fill);

        var snapshot = recorder.Snapshot();

        Assert.Equal(PawnAppearanceCache.Capacity, snapshot.AppearanceCacheFills);
        Assert.Equal(
            PawnAppearanceCache.Capacity + beyond + 1,
            snapshot.AppearanceCacheMisses);
        Assert.Equal(0, snapshot.AppearanceCacheHits);
    }

    /// <summary>
    /// The load-bearing assumption, asserted rather than assumed. Every cache
    /// hit returns an appearance computed from a loadout read on some earlier
    /// frame, and that is only correct because a warrior's loadout cannot
    /// change while a battle runs. If <c>AgentView.Loadout</c> or
    /// <c>CombatLoadout</c> ever becomes mutable — a settable property, a
    /// non-readonly field, a struct that stops being <c>readonly</c>, or a
    /// mutable class in place of the value type — this test fails, and it
    /// fails at build time rather than as an intermittent wrong-looking pawn
    /// that nobody can reproduce. If it does fail, the correct response is to
    /// invalidate this cache, not to relax this test.
    /// </summary>
    [Fact]
    public void TheCacheIsOnlyValidBecauseALoadoutCannotChangeMidBattle()
    {
        AssertTypeIsDeeplyImmutable(typeof(AgentView));
        AssertTypeIsDeeplyImmutable(typeof(CombatLoadout));

        AssertPropertyCannotBeAssignedAfterConstruction(
            typeof(AgentView),
            nameof(AgentView.EntityId));
        AssertPropertyCannotBeAssignedAfterConstruction(
            typeof(AgentView),
            nameof(AgentView.Loadout));
        AssertPropertyCannotBeAssignedAfterConstruction(
            typeof(CombatLoadout),
            nameof(CombatLoadout.Weapon));
        AssertPropertyCannotBeAssignedAfterConstruction(
            typeof(CombatLoadout),
            nameof(CombatLoadout.Shield));

        // The control. Every check above must be capable of failing, or this
        // test would keep passing through exactly the change it exists to
        // catch.
        Assert.False(IsReadOnlyStruct(typeof(MutableLoadoutControl)));

        var controlSetter =
            typeof(MutableLoadoutControl)
                .GetProperty(nameof(MutableLoadoutControl.Weapon))!
                .SetMethod!;

        Assert.False(IsInitOnlySetter(controlSetter));

        Assert.Contains(
            typeof(MutableLoadoutControl).GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic),
            field => !field.IsInitOnly);
    }

    /// <summary>
    /// The ordinal is an address, not the key. A slot that has come to mean a
    /// different agent must miss and recompute rather than hand back the
    /// appearance it happens to be holding — that is the entire reason the key
    /// is stored beside the value. All three key components are varied
    /// independently, because keying on identity alone is exactly the mistake
    /// the declaration rules out.
    /// </summary>
    [Fact]
    public void AnOrdinalThatNowMeansADifferentKeyMissesRatherThanReturningStaleness()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();
        var cache = new PawnAppearanceCache(recorder);

        cache.Resolve(0, 7UL, WeaponId.Kampilan, ShieldId.TallHardwood, isLeader: false);

        Assert.Equal(
            PawnAppearanceFactory.Create(
                9UL,
                WeaponId.Kampilan,
                ShieldId.TallHardwood),
            cache.Resolve(0, 9UL, WeaponId.Kampilan, ShieldId.TallHardwood, isLeader: false));

        Assert.Equal(
            PawnAppearanceFactory.Create(
                9UL,
                WeaponId.Kalis,
                ShieldId.TallHardwood),
            cache.Resolve(0, 9UL, WeaponId.Kalis, ShieldId.TallHardwood, isLeader: false));

        Assert.Equal(
            PawnAppearanceFactory.Create(
                9UL,
                WeaponId.Kalis,
                ShieldId.None),
            cache.Resolve(0, 9UL, WeaponId.Kalis, ShieldId.None, isLeader: false));

        var snapshot = recorder.Snapshot();

        // Four reads, four misses, and only the first of them filled an empty
        // slot; the other three overwrote an occupied one.
        Assert.Equal(0, snapshot.AppearanceCacheHits);
        Assert.Equal(4, snapshot.AppearanceCacheMisses);
        Assert.Equal(1, snapshot.AppearanceCacheFills);
        Assert.Equal(1, cache.Fill);
    }

    /// <summary>
    /// The half of the key declaration that L2 owns end to end: leadership.
    /// Resolving the same ordinal and the same entity/weapon/shield triple
    /// first as a non-leader and then as a leader must miss — never hand back
    /// the non-leader appearance recorded a moment earlier — and the slot it
    /// overwrites was already occupied, so <see cref="PawnAppearanceCache.Fill"/>
    /// must not rise a second time. This is the scenario a leader's death and
    /// the next survivor's promotion produce mid-battle
    /// (<c>MovementRules.ScanContingentLeadersAndLivingCounts</c>).
    /// </summary>
    [Fact]
    public void PromotingTheSameOrdinalToLeaderMissesAndReturnsTheLeaderAppearance()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();
        var cache = new PawnAppearanceCache(recorder);

        var asNonLeader = cache.Resolve(
            0,
            9UL,
            WeaponId.Kalis,
            ShieldId.TallHardwood,
            isLeader: false);

        Assert.Equal(1, cache.Fill);

        var asLeader = cache.Resolve(
            0,
            9UL,
            WeaponId.Kalis,
            ShieldId.TallHardwood,
            isLeader: true);

        Assert.Equal(
            PawnAppearanceFactory.Create(
                9UL,
                WeaponId.Kalis,
                ShieldId.TallHardwood,
                isLeader: true),
            asLeader);
        Assert.NotEqual(asNonLeader, asLeader);

        var snapshot = recorder.Snapshot();

        Assert.Equal(0, snapshot.AppearanceCacheHits);
        Assert.Equal(2, snapshot.AppearanceCacheMisses);

        // The ordinal 0 slot was already occupied by the non-leader read, so
        // the leader read overwrites it rather than filling a new slot.
        Assert.Equal(1, snapshot.AppearanceCacheFills);
        Assert.Equal(1, cache.Fill);
    }

    /// <summary>
    /// What a probe run is supposed to see: a first frame that is all misses
    /// and all fills, and every frame after it all hits and nothing else. The
    /// recorder is reset between frames exactly as the client resets it, so
    /// these are per-frame figures rather than run totals.
    /// </summary>
    [Fact]
    public void TheFirstFrameIsAllMissesAndEveryFrameAfterItIsAllHits()
    {
        const int agents = 64;

        var recorder = new SpriteBatchRenderMetricsRecorder();
        var cache = new PawnAppearanceCache(recorder);

        ResolveOneFrame(cache, agents);

        var cold = recorder.Snapshot();

        Assert.Equal(0, cold.AppearanceCacheHits);
        Assert.Equal(agents, cold.AppearanceCacheMisses);
        Assert.Equal(agents, cold.AppearanceCacheFills);

        recorder.Reset();
        ResolveOneFrame(cache, agents);

        var warm = recorder.Snapshot();

        Assert.Equal(agents, warm.AppearanceCacheHits);
        Assert.Equal(0, warm.AppearanceCacheMisses);
        Assert.Equal(0, warm.AppearanceCacheFills);
    }

    /// <summary>
    /// The one-battle lifetime. After <see cref="PawnAppearanceCache.Clear"/>
    /// the cache must be indistinguishable from a new one, because the three
    /// reset points it is cleared at are exactly the points where the roster
    /// an ordinal refers to is replaced.
    /// </summary>
    [Fact]
    public void ClearEmptiesEverySlotSoTheNextBattleStartsCold()
    {
        const int agents = 32;

        var recorder = new SpriteBatchRenderMetricsRecorder();
        var cache = new PawnAppearanceCache(recorder);

        ResolveOneFrame(cache, agents);

        Assert.Equal(agents, cache.Fill);

        cache.Clear();

        Assert.Equal(0, cache.Fill);

        recorder.Reset();
        ResolveOneFrame(cache, agents);

        var afterClear = recorder.Snapshot();

        Assert.Equal(0, afterClear.AppearanceCacheHits);
        Assert.Equal(agents, afterClear.AppearanceCacheMisses);
        Assert.Equal(agents, afterClear.AppearanceCacheFills);
    }

    private static void ResolveOneFrame(PawnAppearanceCache cache, int agents)
    {
        for (var ordinal = 0; ordinal < agents; ordinal++)
        {
            var (entityId, weapon, shield) = Agent(ordinal);
            cache.Resolve(ordinal, entityId, weapon, shield, isLeader: false);
        }
    }

    /// <summary>
    /// A deterministic roster. The entity ID is injective in the ordinal, so
    /// no two ordinals ever share a key, and ordinal 0 carries entity ID 0 on
    /// purpose.
    /// </summary>
    private static (ulong EntityId, WeaponId Weapon, ShieldId Shield) Agent(
        int ordinal) =>
        ((ulong)ordinal * 7UL,
            Weapons[ordinal % Weapons.Length],
            Shields[ordinal % Shields.Length]);

    private static void AssertTypeIsDeeplyImmutable(Type type)
    {
        Assert.True(
            type.IsValueType,
            $"{type.Name} must stay a value type: the cache copies a loadout " +
            "out of the agent view and assumes nothing it copied can be " +
            "mutated behind its back.");

        Assert.True(
            IsReadOnlyStruct(type),
            $"{type.Name} must stay a readonly struct. Dropping readonly " +
            "would let a caller mutate a loadout in place, and every " +
            "PawnAppearanceCache hit would then be able to serve a stale " +
            "appearance.");

        var fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotEmpty(fields);

        foreach (var field in fields)
        {
            Assert.True(
                field.IsInitOnly,
                $"{type.Name}.{field.Name} must stay readonly. A writable " +
                "field is a loadout that can change mid-battle, which " +
                "invalidates PawnAppearanceCache outright.");
        }
    }

    private static void AssertPropertyCannotBeAssignedAfterConstruction(
        Type type,
        string propertyName)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        var setter = property.SetMethod;

        if (setter is null)
        {
            return;
        }

        Assert.True(
            IsInitOnlySetter(setter),
            $"{type.Name}.{propertyName} must have no setter beyond an " +
            "init accessor. An ordinary setter means a key input can change " +
            "mid-battle, and PawnAppearanceCache is not valid if it can.");
    }

    private static bool IsReadOnlyStruct(Type type) =>
        type.GetCustomAttributes(inherit: false)
            .Any(attribute =>
                attribute.GetType().FullName ==
                "System.Runtime.CompilerServices.IsReadOnlyAttribute");

    private static bool IsInitOnlySetter(MethodInfo setter) =>
        setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier =>
                modifier.FullName ==
                "System.Runtime.CompilerServices.IsExternalInit");

    /// <summary>
    /// A deliberately mutable stand-in for a loadout, used only as the
    /// control in
    /// <see cref="TheCacheIsOnlyValidBecauseALoadoutCannotChangeMidBattle"/>.
    /// It is the shape the real types must never take, and it exists so that
    /// the assertions against them are demonstrably able to fail.
    /// </summary>
    private struct MutableLoadoutControl
    {
        public WeaponId Weapon { get; set; }
    }
}
