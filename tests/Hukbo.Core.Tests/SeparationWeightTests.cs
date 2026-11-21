using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Pins the weights the separating-contact pass reads, as literals. The pass
/// is part of the state hash under movement V17, so a weight that moved with
/// the numeric value of <see cref="ArmorWeightClass"/> would silently change
/// that preset's trajectory.
/// </summary>
public sealed class SeparationWeightTests
{
    [Theory]
    [InlineData(ArmorWeightClass.Light, 1)]
    [InlineData(ArmorWeightClass.Medium, 2)]
    [InlineData(ArmorWeightClass.Rigid, 3)]
    public void EachWeightClass_HasItsPinnedSeparationWeight(
        ArmorWeightClass weightClass,
        int expectedWeight)
    {
        Assert.Equal(expectedWeight, BattleSimulation.SeparationWeightFor(weightClass));
    }

    [Fact]
    public void EveryDeclaredWeightClass_HasASeparationWeight()
    {
        foreach (var weightClass in Enum.GetValues<ArmorWeightClass>())
        {
            Assert.True(
                BattleSimulation.SeparationWeightFor(weightClass) > 0,
                $"{weightClass} has no positive separation weight.");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void AnUndeclaredWeightClass_Throws(int undeclared)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BattleSimulation.SeparationWeightFor((ArmorWeightClass)undeclared));
    }
}
