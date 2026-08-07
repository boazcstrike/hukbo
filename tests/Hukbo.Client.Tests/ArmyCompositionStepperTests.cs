using Hukbo.Client.UI;

// Two distinct types share the name: the persisted settings record and the
// panel's draft struct. The default this file pins lives on the persisted one.
using SavedArmyComposition = Hukbo.Client.Settings.ArmyComposition;

namespace Hukbo.Client.Tests;

public sealed class ArmyCompositionStepperTests
{
    [Fact]
    public void ClampsAtTheLowerBoundInsteadOfWrapping()
    {
        var result = ArmyCompositionStepper.AdjustCategory(
            value: 0,
            unitsPerTeam: 200,
            direction: -1,
            isShiftHeld: false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void ClampsAtTheUpperBoundInsteadOfWrapping()
    {
        var result = ArmyCompositionStepper.AdjustCategory(
            value: 200,
            unitsPerTeam: 200,
            direction: 1,
            isShiftHeld: false);

        Assert.Equal(200, result);
    }

    [Fact]
    public void ShiftMultipliesTheCategoryStepByTen()
    {
        var unshifted = ArmyCompositionStepper.AdjustCategory(
            value: 10,
            unitsPerTeam: 200,
            direction: 1,
            isShiftHeld: false);
        var shifted = ArmyCompositionStepper.AdjustCategory(
            value: 10,
            unitsPerTeam: 200,
            direction: 1,
            isShiftHeld: true);

        Assert.Equal(11, unshifted);
        Assert.Equal(20, shifted);
    }

    [Fact]
    public void DistributeEvenlyGivesTheRemainderToTheEarliestCategories()
    {
        var result = ArmyCompositionStepper.DistributeEvenly(202);

        // 202 / 4 = 50 with a remainder of 2, so the first two of the four
        // rank categories carry the extra unit.
        Assert.Equal(
            new[] { 51, 51, 50, 50 },
            result.ToArray());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(250)]
    [InlineData(500)]
    public void DistributeEvenlyMatchesTheRoundRobinDistributionForTheSameTotal(
        int total)
    {
        var expected = new int[ArmyCompositionStepper.CategoryCount];
        for (var entityIndex = 0; entityIndex < total; entityIndex++)
        {
            expected[entityIndex % ArmyCompositionStepper.CategoryCount]++;
        }

        var result = ArmyCompositionStepper.DistributeEvenly(total);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CategoryArrowsDisableExactlyAtTheirBounds()
    {
        Assert.True(ArmyCompositionStepper.IsCategoryDecrementDisabled(0));
        Assert.False(ArmyCompositionStepper.IsCategoryDecrementDisabled(1));
        Assert.True(
            ArmyCompositionStepper.IsCategoryIncrementDisabled(
                value: 200,
                unitsPerTeam: 200));
        Assert.False(
            ArmyCompositionStepper.IsCategoryIncrementDisabled(
                value: 199,
                unitsPerTeam: 200));
    }

    [Fact]
    public void UnitsPerTeamArrowsDisableExactlyAtTheirBounds()
    {
        Assert.True(
            ArmyCompositionStepper.IsUnitsPerTeamDecrementDisabled(
                ArmyCompositionStepper.MinimumUnitsPerTeam));
        Assert.False(
            ArmyCompositionStepper.IsUnitsPerTeamDecrementDisabled(
                ArmyCompositionStepper.MinimumUnitsPerTeam + 1));
        Assert.True(
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(
                ArmyCompositionStepper.MaximumUnitsPerTeam));
        Assert.False(
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(
                ArmyCompositionStepper.MaximumUnitsPerTeam - 1));
    }

    [Fact]
    public void TheUnitsPerTeamBoundsAreFourAndFiveHundred()
    {
        // Pinned as literals on purpose. The other tests in this file are
        // written against the symbols, so they would keep passing if either
        // bound moved. The maximum is the opt-in ceiling that Phase 2 raised
        // from 250 to 500, and the minimum is the floor a battle cannot start
        // below; both are deliberate, so a change to either should fail here
        // and be argued for rather than slipped in.
        Assert.Equal(4, ArmyCompositionStepper.MinimumUnitsPerTeam);
        Assert.Equal(500, ArmyCompositionStepper.MaximumUnitsPerTeam);
    }

    [Fact]
    public void TheDefaultCompositionStaysAtTwoHundredAndFiftyPerTeam()
    {
        // Raising the ceiling must not move the default. A spectator who never
        // touches the Army Composition panel should still get the same 250 per
        // team, 500 on the field, that they got before the ceiling moved.
        Assert.Equal(250, SavedArmyComposition.Default.UnitsPerTeam);
        Assert.True(
            SavedArmyComposition.Default.UnitsPerTeam
                < ArmyCompositionStepper.MaximumUnitsPerTeam,
            "The default composition should sit strictly below the ceiling, " +
            "so the larger battle stays something a spectator opts into.");
        Assert.Equal(
            SavedArmyComposition.Default.UnitsPerTeam,
            ArmyCompositionStepper.ClampUnitsPerTeam(
                SavedArmyComposition.Default.UnitsPerTeam));
    }

    [Theory]
    [InlineData(500, 500)]
    [InlineData(501, 500)]
    [InlineData(1_000, 500)]
    [InlineData(int.MaxValue, 500)]
    [InlineData(499, 499)]
    [InlineData(251, 251)]
    public void ClampUnitsPerTeamHoldsAtTheNewMaximum(int value, int expected)
    {
        // 251 through 500 used to clamp down to 250. They are now legal values,
        // which is the whole of the behaviour change; everything above 500
        // still clamps.
        Assert.Equal(expected, ArmyCompositionStepper.ClampUnitsPerTeam(value));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(3, 4)]
    [InlineData(0, 4)]
    [InlineData(-50, 4)]
    public void ClampUnitsPerTeamStillRefusesAnythingBelowFour(
        int value,
        int expected)
    {
        Assert.Equal(expected, ArmyCompositionStepper.ClampUnitsPerTeam(value));
    }

    [Fact]
    public void IncrementingStopsAtTheNewMaximumInsteadOfWrapping()
    {
        Assert.Equal(
            500,
            ArmyCompositionStepper.AdjustUnitsPerTeam(
                value: 490,
                direction: 1,
                isShiftHeld: false));
        Assert.Equal(
            500,
            ArmyCompositionStepper.AdjustUnitsPerTeam(
                value: 500,
                direction: 1,
                isShiftHeld: false));
        Assert.Equal(
            500,
            ArmyCompositionStepper.AdjustUnitsPerTeam(
                value: 460,
                direction: 1,
                isShiftHeld: true));
    }

    [Fact]
    public void TheStepperNowClimbsPastTheOldTwoHundredAndFiftyCeiling()
    {
        Assert.Equal(
            260,
            ArmyCompositionStepper.AdjustUnitsPerTeam(
                value: 250,
                direction: 1,
                isShiftHeld: false));
        Assert.Equal(
            300,
            ArmyCompositionStepper.AdjustUnitsPerTeam(
                value: 250,
                direction: 1,
                isShiftHeld: true));
    }

    [Fact]
    public void TheIncrementArrowDisablesAtFiveHundredAndNotBefore()
    {
        Assert.True(
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(500));
        Assert.False(
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(499));
        Assert.False(
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(250));
    }

    [Fact]
    public void CategoryCountsCanReachTheNewMaximumUnitsPerTeam()
    {
        // The category ceiling is the team total, so it follows the raised cap
        // rather than the old one.
        Assert.Equal(
            500,
            ArmyCompositionStepper.ClampCategory(
                value: 600,
                unitsPerTeam: 500));
        Assert.False(
            ArmyCompositionStepper.IsCategoryIncrementDisabled(
                value: 300,
                unitsPerTeam: 500));
        Assert.True(
            ArmyCompositionStepper.IsCategoryIncrementDisabled(
                value: 500,
                unitsPerTeam: 500));
    }

    [Fact]
    public void DistributeEvenlyAtTheNewMaximumSumsToFiveHundred()
    {
        var result = ArmyCompositionStepper.DistributeEvenly(500);

        // Four rank categories, not the six weapon-and-grip categories this
        // test was written against: 500 divides by four exactly, so there is
        // no remainder to hand to the earliest categories and every rank
        // fields the same count. The sum, which is what this test is named
        // for, is unchanged.
        Assert.Equal(
            new[] { 125, 125, 125, 125 },
            result.ToArray());
        Assert.Equal(500, result.Sum());
    }
}
