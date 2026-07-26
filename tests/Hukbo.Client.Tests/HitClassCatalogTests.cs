using Hukbo.Client.Audio;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class HitClassCatalogTests
{
    // The expected-class parameter is an int because xunit requires public
    // test methods and HitClass is internal to Hukbo.Client.
    [Theory]
    [InlineData(BodyPart.WeaponArm, (int)HitClass.Extremity)]
    [InlineData(BodyPart.ShieldArm, (int)HitClass.Extremity)]
    [InlineData(BodyPart.Shoulder, (int)HitClass.Limb)]
    [InlineData(BodyPart.Head, (int)HitClass.Skull)]
    [InlineData(BodyPart.Neck, (int)HitClass.Neck)]
    [InlineData(BodyPart.Face, (int)HitClass.Skull)]
    [InlineData(BodyPart.Chest, (int)HitClass.Ribcage)]
    [InlineData(BodyPart.Abdomen, (int)HitClass.Gut)]
    [InlineData(BodyPart.Thigh, (int)HitClass.Limb)]
    [InlineData(BodyPart.Knee, (int)HitClass.Limb)]
    [InlineData(BodyPart.Shin, (int)HitClass.Extremity)]
    [InlineData(BodyPart.Hands, (int)HitClass.Extremity)]
    [InlineData(BodyPart.Feet, (int)HitClass.Extremity)]
    public void FromBodyPart_MapsEveryDeclaredBodyPart(
        BodyPart bodyPart,
        int expected) =>
        Assert.Equal((HitClass)expected, HitClassCatalog.FromBodyPart(bodyPart));

    [Fact]
    public void FromBodyPart_CoversEveryDeclaredBodyPartExactlyOnce()
    {
        foreach (var bodyPart in Enum.GetValues<BodyPart>())
        {
            // Every declared value must resolve without throwing.
            HitClassCatalog.FromBodyPart(bodyPart);
        }
    }

    [Fact]
    public void FromBodyPart_RejectsAnUndeclaredValue() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitClassCatalog.FromBodyPart((BodyPart)999));

    [Fact]
    public void All_ListsEveryDeclaredClassExactlyOnceInFixedOrder()
    {
        HitClass[] expectedOrder =
        [
            HitClass.Skull,
            HitClass.Neck,
            HitClass.Ribcage,
            HitClass.Gut,
            HitClass.Limb,
            HitClass.Extremity,
        ];

        Assert.Equal(expectedOrder, HitClassCatalog.All);
        Assert.Equal(
            Enum.GetValues<HitClass>().Length,
            HitClassCatalog.All.Count);
    }

    // Both parameters are ints because xunit requires public test methods and
    // HitClass is internal to Hukbo.Client.
    [Theory]
    [InlineData((int)HitClass.Skull, "skull")]
    [InlineData((int)HitClass.Neck, "neck")]
    [InlineData((int)HitClass.Ribcage, "ribcage")]
    [InlineData((int)HitClass.Gut, "gut")]
    [InlineData((int)HitClass.Limb, "limb")]
    [InlineData((int)HitClass.Extremity, "extremity")]
    public void GetToken_IsLowercaseAndMatchesTheGeneratorContract(
        int hitClass,
        string expected) =>
        Assert.Equal(expected, HitClassCatalog.GetToken((HitClass)hitClass));

    [Fact]
    public void GetToken_RejectsAnUndeclaredValue() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitClassCatalog.GetToken((HitClass)999));

    [Fact]
    public void GetFallbackChain_ExtremityFallsBackToLimbThenRibcage() =>
        Assert.Equal(
            [HitClass.Limb, HitClass.Ribcage],
            HitClassCatalog.GetFallbackChain(HitClass.Extremity));

    [Fact]
    public void GetFallbackChain_SkullFallsBackToNeckThenRibcage() =>
        Assert.Equal(
            [HitClass.Neck, HitClass.Ribcage],
            HitClassCatalog.GetFallbackChain(HitClass.Skull));

    [Theory]
    [InlineData((int)HitClass.Neck)]
    [InlineData((int)HitClass.Gut)]
    [InlineData((int)HitClass.Limb)]
    public void GetFallbackChain_FallsBackDirectlyToRibcage(int hitClass) =>
        Assert.Equal(
            [HitClass.Ribcage],
            HitClassCatalog.GetFallbackChain((HitClass)hitClass));

    [Fact]
    public void GetFallbackChain_RibcageHasNoClassFallback() =>
        Assert.Empty(HitClassCatalog.GetFallbackChain(HitClass.Ribcage));

    [Fact]
    public void GetFallbackChain_RejectsAnUndeclaredValue() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitClassCatalog.GetFallbackChain((HitClass)999));

    [Fact]
    public void GetFallbackChain_NeverEndsInACycleBackToItself()
    {
        foreach (var hitClass in HitClassCatalog.All)
        {
            Assert.DoesNotContain(hitClass, HitClassCatalog.GetFallbackChain(hitClass));
        }
    }
}
