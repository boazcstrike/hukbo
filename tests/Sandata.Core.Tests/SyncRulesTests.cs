using Sandata.Core.Orders;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 60 of docs/plans/2026-08-07-sandata-scaffold.md: <c>SyncRules</c>
/// against design section 16, "Sync sets and go-codes" — "Sync pace-matches
/// a set of operators... When every living member of the set is holding, all
/// of them release on the same tick," keyed by the lowest entity id.
/// </summary>
public sealed class SyncRulesTests
{
    /// <summary>
    /// "A sync set releases on exactly the tick its last living member
    /// arrives." Three members; two are already holding, the third —
    /// entity 30, deliberately not the lowest id, so this fixture does not
    /// accidentally also prove something about <see cref="SyncRules.SyncEvaluation.SetKey"/>
    /// ordering — arrives one tick later than the other two. Both sides of
    /// the transition are asserted: not-released on the tick before, and
    /// released on the tick it arrives.
    /// </summary>
    [Fact]
    public void Evaluate_ReleasesOnExactlyTheTickTheLastLivingMemberArrives()
    {
        var tickBeforeLastArrival = new[]
        {
            new SyncRules.MemberFacts(EntityId: 10, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 20, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 30, IsAlive: true, IsHolding: false),
        };

        var tickOfLastArrival = new[]
        {
            new SyncRules.MemberFacts(EntityId: 10, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 20, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 30, IsAlive: true, IsHolding: true),
        };

        var before = SyncRules.Evaluate(tickBeforeLastArrival);
        var after = SyncRules.Evaluate(tickOfLastArrival);

        Assert.False(before.Releases);
        Assert.True(after.Releases);
    }

    /// <summary>
    /// "A dead member does not deadlock the set." Entity 30 is killed before
    /// it ever reaches its final node (<c>IsHolding: false</c>) — under a
    /// naive "every member must hold" rule this set would never release.
    /// Once the two living members are both holding, the set releases
    /// regardless of the dead member's own <c>IsHolding</c> value.
    /// </summary>
    [Fact]
    public void Evaluate_ADeadMemberThatNeverArrived_DoesNotDeadlockTheSet()
    {
        var members = new[]
        {
            new SyncRules.MemberFacts(EntityId: 10, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 20, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 30, IsAlive: false, IsHolding: false),
        };

        var result = SyncRules.Evaluate(members);

        Assert.True(result.Releases);
    }

    /// <summary>
    /// The stated, decided behaviour for an all-dead set (documented on
    /// <see cref="SyncRules.Evaluate"/> itself): it never releases. Vacuous
    /// truth over the empty "living members" condition was rejected in
    /// favour of this because a release with nobody left to act on it is not
    /// a fact worth reporting <see langword="true"/> on every evaluation.
    /// </summary>
    [Fact]
    public void Evaluate_EverySetMemberDead_NeverReleases()
    {
        var members = new[]
        {
            new SyncRules.MemberFacts(EntityId: 10, IsAlive: false, IsHolding: false),
            new SyncRules.MemberFacts(EntityId: 20, IsAlive: false, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 30, IsAlive: false, IsHolding: false),
        };

        var result = SyncRules.Evaluate(members);

        Assert.False(result.Releases);
    }

    /// <summary>
    /// <see cref="SyncRules.Evaluate"/> requires at least one member — a
    /// sync set naming nobody has no lowest entity id to key itself with.
    /// </summary>
    [Fact]
    public void Evaluate_NoMembers_Throws()
    {
        Assert.Throws<ArgumentException>(() => SyncRules.Evaluate(ReadOnlySpan<SyncRules.MemberFacts>.Empty));
    }

    /// <summary>
    /// "Permuting the evaluation order of the members changes nothing." Four
    /// distinct orderings of the same five-member set — including one where
    /// the lowest-id member (which decides
    /// <see cref="SyncRules.SyncEvaluation.SetKey"/>) sits last and one where
    /// it sits first — all produce the identical <see cref="SyncRules.SyncEvaluation"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(PermutationsOfFiveMembers))]
    public void Evaluate_PermutingMemberOrder_ProducesTheSameResult(SyncRules.MemberFacts[] permutation)
    {
        var result = SyncRules.Evaluate(permutation);

        Assert.Equal(10ul, result.SetKey);
        Assert.True(result.Releases);
    }

    public static IEnumerable<object[]> PermutationsOfFiveMembers()
    {
        // The canonical set: entity 10 is the lowest id and, along with
        // every other living member, is holding; entity 40 is dead and not
        // holding, which must not block the release.
        var byLowestIdFirst = new[]
        {
            new SyncRules.MemberFacts(EntityId: 10, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 20, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 30, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 40, IsAlive: false, IsHolding: false),
            new SyncRules.MemberFacts(EntityId: 50, IsAlive: true, IsHolding: true),
        };

        yield return new object[] { byLowestIdFirst };

        yield return new object[]
        {
            new[] { byLowestIdFirst[4], byLowestIdFirst[3], byLowestIdFirst[2], byLowestIdFirst[1], byLowestIdFirst[0] },
        };

        yield return new object[]
        {
            new[] { byLowestIdFirst[2], byLowestIdFirst[0], byLowestIdFirst[4], byLowestIdFirst[1], byLowestIdFirst[3] },
        };

        yield return new object[]
        {
            new[] { byLowestIdFirst[3], byLowestIdFirst[1], byLowestIdFirst[0], byLowestIdFirst[4], byLowestIdFirst[2] },
        };
    }

    /// <summary>
    /// "Two sets releasing on the same tick resolve in a total order" —
    /// keyed by the lowest entity id, per <see cref="SyncRules.OrderReleasingSets"/>.
    /// Set B's lowest id (2) is below set A's (5), so B must always precede
    /// A regardless of which order the two evaluations are handed in.
    /// </summary>
    [Fact]
    public void OrderReleasingSets_TwoSetsReleaseOnTheSameTick_ResolveInATotalOrderKeyedByLowestEntityId()
    {
        var setA = SyncRules.Evaluate(
        [
            new SyncRules.MemberFacts(EntityId: 5, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 6, IsAlive: true, IsHolding: true),
        ]);
        var setB = SyncRules.Evaluate(
        [
            new SyncRules.MemberFacts(EntityId: 2, IsAlive: true, IsHolding: true),
            new SyncRules.MemberFacts(EntityId: 3, IsAlive: true, IsHolding: true),
        ]);

        Assert.True(setA.Releases);
        Assert.True(setB.Releases);

        var aThenB = SyncRules.OrderReleasingSets([setA, setB]);
        var bThenA = SyncRules.OrderReleasingSets([setB, setA]);

        Assert.Equal(new[] { setB, setA }, aThenB);
        Assert.Equal(new[] { setB, setA }, bThenA);
    }

    /// <summary>
    /// A non-releasing set contributes nothing to the ordering — the same
    /// tick can carry sets that are still waiting alongside sets that just
    /// released, and <see cref="SyncRules.OrderReleasingSets"/> reports only
    /// the latter.
    /// </summary>
    [Fact]
    public void OrderReleasingSets_ExcludesSetsThatDidNotReleaseThisTick()
    {
        var releasing = SyncRules.Evaluate(
        [
            new SyncRules.MemberFacts(EntityId: 1, IsAlive: true, IsHolding: true),
        ]);
        var stillWaiting = SyncRules.Evaluate(
        [
            new SyncRules.MemberFacts(EntityId: 8, IsAlive: true, IsHolding: false),
        ]);

        var ordered = SyncRules.OrderReleasingSets([releasing, stillWaiting]);

        Assert.Equal(new[] { releasing }, ordered);
    }

    /// <summary>
    /// Structural proof that this file never bypasses
    /// <c>OrderQueue.SubmitValidated</c> to fabricate an applied order or
    /// order-like state: <c>SyncRules.cs</c> constructs no <c>Order</c>
    /// value and never assigns <c>OrderQueue.Orders</c> directly. The only
    /// state this file produces is <see cref="SyncRules.SyncEvaluation"/>, a
    /// value type with no relationship to <c>OrderQueue</c> at all.
    /// </summary>
    [Fact]
    public void SyncRulesSource_NeverConstructsAnOrderOrTouchesTheQueueDirectly()
    {
        var sourcePath = FindSourceFile("SyncRules.cs");
        var text = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("new Order(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Orders =", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".Submit(", text, StringComparison.Ordinal);
    }

    private static string FindSourceFile(string fileName)
    {
        var root = Hukbo.Diagnostics.LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(root is not null, "Could not locate the repository root to find " + fileName + ".");

        var path = Path.Combine(root!, "src", "Sandata.Core", "Orders", fileName);
        Assert.True(File.Exists(path), path + " does not exist.");

        return path;
    }
}
