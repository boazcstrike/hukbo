using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

public sealed class DetailTierGateTests
{
    [Theory]
    [InlineData(0.9499999f)]
    [InlineData(0.72f)]
    [InlineData(0f)]
    public void ShouldDraw_BelowMediumThreshold_OnlyLowMinimumDraws(float apparentScale)
    {
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Low));
        Assert.False(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium));
        Assert.False(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.High));
    }

    [Fact]
    public void ShouldDraw_AtExactly0Point95_ClassifiesAsMediumNotLow()
    {
        const float apparentScale = 0.95f;

        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Low));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium));
        Assert.False(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.High));
    }

    [Theory]
    [InlineData(0.95f)]
    [InlineData(1.2f)]
    [InlineData(1.79f)]
    public void ShouldDraw_BetweenThresholds_LowAndMediumMinimumsDrawHighDoesNot(float apparentScale)
    {
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Low));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium));
        Assert.False(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.High));
    }

    [Fact]
    public void ShouldDraw_AtExactly1Point80_ClassifiesAsHighNotMedium()
    {
        const float apparentScale = 1.80f;

        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Low));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.High));
    }

    [Theory]
    [InlineData(1.80f)]
    [InlineData(2.0f)]
    [InlineData(2.40f)]
    public void ShouldDraw_AtOrAboveHighThreshold_EveryMinimumDraws(float apparentScale)
    {
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Low));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium));
        Assert.True(DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.High));
    }
}
