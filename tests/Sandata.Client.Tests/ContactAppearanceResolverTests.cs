using System.Collections.Immutable;
using Sandata.Client.Rendering;
using Sandata.Core.Sensing;
using Sandata.Core.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 10's pure resolver tests, design section 6's D5: "an
/// <c>Identified</c> hostile draws as it does today, a <c>Detected</c> one
/// draws as an unknown-contact marker with no facing and no weapon, and a
/// hostile nobody has any memory of is not drawn at all." Only plain
/// <see cref="OperatorState"/> values here — no <c>ArenaGame</c>, no
/// <c>GraphicsDevice</c>, no <c>SpriteBatch</c>, no window.
/// </summary>
public sealed class ContactAppearanceResolverTests
{
    private const int AssaultingFaction = 0;
    private const int DefendingFaction = 1;
    private const ulong HostileEntityId = 900UL;

    private static OperatorState MakeAssaultingOperator(
        ulong entityId, params ContactMemoryEntry[] contactMemory) =>
        new(
            EntityId: entityId,
            PositionX: default,
            PositionY: default,
            Facing: default,
            AimAngle: default,
            Health: 100,
            Faction: AssaultingFaction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 0,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0)
        {
            ContactMemory = contactMemory.ToImmutableArray(),
        };

    private static ContactMemoryEntry MakeEntry(ulong enemyEntityId, ContactTier tier) =>
        new(
            EnemyEntityId: enemyEntityId,
            LastKnownCellIndex: 0,
            ContactTier: (int)tier,
            LastSeenTick: 1);

    [Fact]
    public void NoOperatorsYieldsUnknownTier()
    {
        var tier = ContactAppearanceResolver.GetBestContactTier(
            ImmutableArray<OperatorState>.Empty, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactTier.Unknown, tier);
    }

    [Fact]
    public void NoObserverHasAnEntryForTheHostileYieldsUnknownTier()
    {
        var operators = ImmutableArray.Create(
            MakeAssaultingOperator(1, MakeEntry(enemyEntityId: 42, ContactTier.Identified)));

        var tier = ContactAppearanceResolver.GetBestContactTier(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactTier.Unknown, tier);
    }

    [Fact]
    public void QuestionMarkOnlyMemoryResolvesToUnknownAppearance()
    {
        var operators = ImmutableArray.Create(
            MakeAssaultingOperator(1, MakeEntry(HostileEntityId, ContactTier.QuestionMark)));

        var appearance = ContactAppearanceResolver.ResolveHostileAppearance(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactAppearance.Unknown, appearance);
    }

    [Fact]
    public void IdentifiedMemoryResolvesToIdentifiedAppearance()
    {
        var operators = ImmutableArray.Create(
            MakeAssaultingOperator(1, MakeEntry(HostileEntityId, ContactTier.Identified)));

        var appearance = ContactAppearanceResolver.ResolveHostileAppearance(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactAppearance.Identified, appearance);
    }

    [Fact]
    public void NoMemoryAnywhereResolvesToHiddenAppearance()
    {
        var operators = ImmutableArray.Create(MakeAssaultingOperator(1));

        var appearance = ContactAppearanceResolver.ResolveHostileAppearance(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactAppearance.Hidden, appearance);
    }

    [Fact]
    public void BestTierAcrossMultipleAssaultingOperatorsWins()
    {
        var operators = ImmutableArray.Create(
            MakeAssaultingOperator(1, MakeEntry(HostileEntityId, ContactTier.QuestionMark)),
            MakeAssaultingOperator(2, MakeEntry(HostileEntityId, ContactTier.Identified)));

        var tier = ContactAppearanceResolver.GetBestContactTier(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactTier.Identified, tier);
    }

    [Fact]
    public void ObserverOnADifferentFactionIsIgnored()
    {
        var defendingObserver = MakeAssaultingOperator(
            1, MakeEntry(HostileEntityId, ContactTier.Identified)) with
        {
            Faction = DefendingFaction,
        };
        var operators = ImmutableArray.Create(defendingObserver);

        var tier = ContactAppearanceResolver.GetBestContactTier(
            operators, AssaultingFaction, HostileEntityId);

        Assert.Equal(ContactTier.Unknown, tier);
    }

    [Theory]
    [InlineData((int)ContactTier.Unknown, "Hidden")]
    [InlineData((int)ContactTier.QuestionMark, "Unknown")]
    [InlineData((int)ContactTier.Identified, "Identified")]
    public void RawTierMapsDirectlyToAppearance(int rawTier, string expectedName)
    {
        var expected = Enum.Parse<ContactAppearance>(expectedName);

        Assert.Equal(expected, ContactAppearanceResolver.ResolveHostileAppearance(rawTier));
    }
}
