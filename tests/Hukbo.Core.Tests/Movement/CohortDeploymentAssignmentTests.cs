using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The pure weapon-cohort deployment permutation of the battlefield realism
/// design, sections 4.2 through 4.6. Every expected value below is
/// hand-derived from the design's ordering rules — cohorts by member count
/// descending then cohort key ascending, contingents by slot count
/// descending then contingent id ascending, positional pairing, then
/// shield-bearing-first pairing against depth-ordered slots inside each
/// contingent — never by calling
/// <see cref="CohortDeploymentAssignment.AssignForFaction"/> to produce its
/// own expectation. This type is not yet wired into
/// <see cref="Hukbo.Core.Simulation.BattleSimulation"/>; these are unit
/// tests of the pure permutation alone.
/// </summary>
public sealed class CohortDeploymentAssignmentTests
{
    // Roster order of PrecolonialPhilippinesV2 (the roster order is part of
    // its content-hash contract and does not change): 0 Kampilan, 1 Wasay,
    // 2 Kalis, 3 Kalis+TallHardwood, 4 Itak, 5 Itak+TallHardwood.
    private static CombatRuleset Rules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Wasay =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Kalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout KalisShield =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ItakShield =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    // ----- Required test 1: a cohort at least as large as a contingent -----

    /// <summary>
    /// Two three-slot contingents; four Kampilan warriors (cohort size 4)
    /// against two Wasay warriors (cohort size 2). The cohort-ordered list
    /// is Kampilan(0,1,2,3), Wasay(4,5); the size-3 contingents tie and
    /// break on id ascending, so contingent 0's block is the first three of
    /// that list — warriors 0, 1, 2 — which are all Kampilan. Contingent 0
    /// therefore comes out purely one weapon, exactly what the plan row
    /// asks for; contingent 1 is not asserted pure because the arithmetic
    /// does not require it to be.
    /// </summary>
    [Fact]
    public void ACohortAtLeastAsLargeAsAContingentFillsThatContingentPurely()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 10, YRaw: 0, ContingentId: 0),
            (XRaw: 20, YRaw: 0, ContingentId: 0),
            (XRaw: 30, YRaw: 0, ContingentId: 1),
            (XRaw: 40, YRaw: 0, ContingentId: 1),
            (XRaw: 50, YRaw: 0, ContingentId: 1),
        };
        var loadouts = new[]
        {
            Kampilan, Kampilan, Kampilan, Kampilan, Wasay, Wasay,
        };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        var pureContingentMembers = 0;
        for (var index = 0; index < assigned.Length; index++)
        {
            if (assigned[index].ContingentId != 0)
            {
                continue;
            }

            pureContingentMembers++;
            Assert.Equal(WeaponId.Kampilan, loadouts[index].Weapon);
        }

        Assert.Equal(3, pureContingentMembers);
    }

    // ----- Required test 2: at most contingentCount - 1 splits army-wide -----

    /// <summary>
    /// Three contingents of sizes 3, 3, 4 (ids 0, 1, 2); cohorts Kampilan
    /// (5), Wasay (3), Kalis (2), already laid out in cohort-ordered index
    /// order (0-4, 5-7, 8-9) so no key tie-break is exercised here. Hand
    /// derivation of the contingent order (size descending, id ascending)
    /// is id 2 (size 4), then id 0, then id 1 (tied at 3, id ascending).
    /// Cutting the cohort-ordered warrior list 0..9 into blocks of 4, 3, 3
    /// for contingents 2, 0, 1 gives: contingent 2 = warriors 0-3 (all
    /// Kampilan); contingent 0 = warriors 4-6 (Kampilan, Wasay, Wasay);
    /// contingent 1 = warriors 7-9 (Wasay, Kalis, Kalis). The Kampilan
    /// cohort now spans contingents {2, 0} and the Wasay cohort spans
    /// {0, 1} — two splits — while Kalis stays entirely inside contingent 1.
    /// Two splits is exactly <c>contingentCount - 1</c> for three
    /// contingents, the tightest the design's bound allows, computed here
    /// by hand from the cut points rather than from the method's own
    /// output.
    /// </summary>
    [Fact]
    public void SplitsNumberAtMostContingentCountMinusOneArmyWide()
    {
        const int ContingentCount = 3;
        const int ExpectedSplitsByHand = 2;

        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 10, YRaw: 0, ContingentId: 0),
            (XRaw: 20, YRaw: 0, ContingentId: 0),
            (XRaw: 30, YRaw: 0, ContingentId: 1),
            (XRaw: 40, YRaw: 0, ContingentId: 1),
            (XRaw: 50, YRaw: 0, ContingentId: 1),
            (XRaw: 60, YRaw: 0, ContingentId: 2),
            (XRaw: 70, YRaw: 0, ContingentId: 2),
            (XRaw: 80, YRaw: 0, ContingentId: 2),
            (XRaw: 90, YRaw: 0, ContingentId: 2),
        };
        var loadouts = new[]
        {
            Kampilan, Kampilan, Kampilan, Kampilan, Kampilan,
            Wasay, Wasay, Wasay,
            Kalis, Kalis,
        };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        var actualSplits = CountCohortsSpanningMoreThanOneContingent(
            loadouts, assigned);

        Assert.Equal(ExpectedSplitsByHand, actualSplits);
        Assert.True(actualSplits <= ContingentCount - 1);
    }

    /// <summary>
    /// Counts, from the caller-known loadouts and the method's output
    /// alone, how many distinct weapons ended up spread across more than
    /// one contingent id — a plain tally over the already-produced result,
    /// not a second invocation of the method under test to manufacture an
    /// expectation.
    /// </summary>
    private static int CountCohortsSpanningMoreThanOneContingent(
        CombatLoadout[] loadouts,
        (int XRaw, int YRaw, int ContingentId)[] assigned)
    {
        var contingentIdsByWeapon = new Dictionary<WeaponId, HashSet<int>>();
        for (var index = 0; index < loadouts.Length; index++)
        {
            var weapon = loadouts[index].Weapon;
            if (!contingentIdsByWeapon.TryGetValue(weapon, out var set))
            {
                set = new HashSet<int>();
                contingentIdsByWeapon[weapon] = set;
            }

            set.Add(assigned[index].ContingentId);
        }

        var splitCount = 0;
        foreach (var set in contingentIdsByWeapon.Values)
        {
            if (set.Count > 1)
            {
                splitCount++;
            }
        }

        return splitCount;
    }

    // ----- Required test 3: shield bearers take the forward-most slots -----

    /// <summary>
    /// One contingent, four slots at distinct <c>XRaw</c> values, two
    /// shield-bearing loadouts (Kalis+shield, Itak+shield) and two bare
    /// ones (Kampilan, Wasay). Slots rank by <c>XRaw</c> descending: 3000,
    /// 2000, 1000, 0. Warriors rank shield-bearing first, then cohort key
    /// ascending: Kalis+shield (roster row 3) before Itak+shield (roster
    /// row 5), then Kampilan (row 0) before Wasay (row 1). Pairing puts the
    /// two shield bearers on the contingent's two forward-most slots.
    /// </summary>
    [Fact]
    public void ShieldBearersOccupyTheForwardMostSlotsOfEachContingent()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 2_000, YRaw: 0, ContingentId: 0),
            (XRaw: 3_000, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[] { Kampilan, KalisShield, Wasay, ItakShield };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        Assert.Equal((3_000, 0, 0), assigned[1]);
        Assert.Equal((2_000, 0, 0), assigned[3]);
        Assert.Equal((1_000, 0, 0), assigned[0]);
        Assert.Equal((0, 0, 0), assigned[2]);
    }

    // ----- Required test 4: the occupied-coordinate set is exactly the input -----

    [Fact]
    public void TheOccupiedCoordinateSetIsExactlyTheInputSet()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 5, ContingentId: 0),
            (XRaw: 10, YRaw: 4, ContingentId: 0),
            (XRaw: 20, YRaw: 3, ContingentId: 0),
            (XRaw: 30, YRaw: 2, ContingentId: 1),
            (XRaw: 40, YRaw: 1, ContingentId: 1),
            (XRaw: 50, YRaw: 0, ContingentId: 1),
            (XRaw: 60, YRaw: 9, ContingentId: 2),
            (XRaw: 70, YRaw: 8, ContingentId: 2),
            (XRaw: 80, YRaw: 7, ContingentId: 2),
            (XRaw: 90, YRaw: 6, ContingentId: 2),
        };
        var loadouts = new[]
        {
            Kampilan, Kampilan, Kampilan, Kampilan, Kampilan,
            Wasay, Wasay, Wasay,
            Kalis, Kalis,
        };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        Assert.Equal(
            SortedByCoordinate(deployment), SortedByCoordinate(assigned));
    }

    private static (int XRaw, int YRaw, int ContingentId)[] SortedByCoordinate(
        (int XRaw, int YRaw, int ContingentId)[] slots)
    {
        var sorted = (int[])Enumerable.Range(0, slots.Length).ToArray();
        Array.Sort(sorted, (left, right) =>
        {
            var byX = slots[left].XRaw.CompareTo(slots[right].XRaw);
            if (byX != 0)
            {
                return byX;
            }

            var byY = slots[left].YRaw.CompareTo(slots[right].YRaw);
            if (byY != 0)
            {
                return byY;
            }

            var byContingent = slots[left].ContingentId
                .CompareTo(slots[right].ContingentId);
            return byContingent != 0 ? byContingent : left.CompareTo(right);
        });

        var result = new (int XRaw, int YRaw, int ContingentId)[slots.Length];
        for (var index = 0; index < sorted.Length; index++)
        {
            result[index] = slots[sorted[index]];
        }

        return result;
    }

    // ----- Required test 5: identical inputs give identical output -----

    [Fact]
    public void IdenticalInputsGiveIdenticalOutputAcrossRepeatedCalls()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 2_000, YRaw: 0, ContingentId: 1),
            (XRaw: 3_000, YRaw: 0, ContingentId: 1),
        };
        var loadouts = new[] { Kampilan, KalisShield, Wasay, ItakShield };

        var first = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);
        var second = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        Assert.Equal(first, second);
    }

    // ----- Required test 6: a single-member contingent is the identity -----

    [Fact]
    public void ASingleMemberContingentIsTheIdentity()
    {
        var deployment = new[] { (XRaw: 555, YRaw: 777, ContingentId: 0) };
        var loadouts = new[] { Kampilan };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        Assert.Equal(deployment, assigned);
    }

    // ----- Determinism guards beyond the six required tests -----

    [Fact]
    public void TheCanonicalDeploymentArrayIsNeverMutated()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 2_000, YRaw: 0, ContingentId: 1),
            (XRaw: 3_000, YRaw: 0, ContingentId: 1),
        };
        var snapshot = (
            (int XRaw, int YRaw, int ContingentId)[])deployment.Clone();
        var loadouts = new[] { Kampilan, KalisShield, Wasay, ItakShield };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, false);

        Assert.NotSame(deployment, assigned);
        Assert.Equal(snapshot, deployment);
    }

    [Fact]
    public void ThrowsWhenTheLoadoutCountDisagreesWithTheSlotCount()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
        };

        Assert.Throws<ArgumentException>(
            () => CohortDeploymentAssignment.AssignForFaction(
                deployment, new[] { Kampilan }, Rules, false));
    }

    [Fact]
    public void ThrowsWhenALoadoutIsNotARegisteredRosterRow()
    {
        var deployment = new[] { (XRaw: 0, YRaw: 0, ContingentId: 0) };
        var offRoster = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.TallHardwood);

        Assert.Throws<InvalidOperationException>(
            () => CohortDeploymentAssignment.AssignForFaction(
                deployment, new[] { offRoster }, Rules, false));
    }
}
