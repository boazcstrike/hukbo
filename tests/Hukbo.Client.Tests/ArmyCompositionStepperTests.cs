using Hukbo.Client.UI;

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

        Assert.Equal(
            new[] { 34, 34, 34, 34, 33, 33 },
            result.ToArray());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(250)]
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
}
