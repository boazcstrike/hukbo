using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <c>PawnRenderer.DrawsSpriteBody</c> — the gate <c>DrawLayout</c> uses
/// to choose the authored body sprite over the procedural torso and head
/// (the 2026-08-15 pawn sprite body design, section 3).
/// </summary>
/// <remarks>
/// <para>
/// Nothing here constructs an <c>ArenaGame</c>, a <c>GraphicsDevice</c>, a
/// <c>SpriteBatch</c>, or a window. <c>DrawsSpriteBody</c> is a pure function
/// of its four value-type inputs and <c>DrawLayout</c> holds no second copy of
/// the condition, so the truth table asserted below is the truth table that
/// decides what actually draws.
/// </para>
/// <para>
/// Written as separate facts rather than as theories over
/// <c>PawnDetailTier</c> and <c>VisualFallbackStep</c>. Both of those enums are
/// <c>internal</c>, and a public xunit method cannot take an internal parameter
/// type, so an <c>InlineData</c> theory over either one fails to compile with
/// CS0051. Every sibling suite in this project names those enums inside a test
/// body for the same reason.
/// </para>
/// </remarks>
public sealed class PawnSpriteBodyGateTests
{
    [Fact]
    public void SpriteBodyDrawsAtMediumDetail()
    {
        Assert.True(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.Medium,
            VisualFallbackStep.ModelCategoryDefault));
    }

    [Fact]
    public void SpriteBodyDrawsAtHighDetail()
    {
        Assert.True(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.ModelCategoryDefault));
    }

    /// <summary>
    /// The procedural style is the shipped default, so it must refuse the
    /// sprite at every detail tier even with an atlas loaded. This is what
    /// guarantees a player who never presses the key sees exactly what the
    /// game drew before the mode existed.
    /// </summary>
    [Fact]
    public void TheProceduralStyleNeverDrawsTheSpriteAtAnyDetailTier()
    {
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.Procedural,
            hasBodyAtlas: true,
            PawnDetailTier.Low,
            VisualFallbackStep.ModelCategoryDefault));
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.Procedural,
            hasBodyAtlas: true,
            PawnDetailTier.Medium,
            VisualFallbackStep.ModelCategoryDefault));
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.Procedural,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.ModelCategoryDefault));
    }

    /// <summary>
    /// A client whose content failed to load must fall back rather than draw
    /// nothing where a warrior's body should be.
    /// </summary>
    [Fact]
    public void NoAtlasFallsBackToTheProceduralBody()
    {
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: false,
            PawnDetailTier.High,
            VisualFallbackStep.ModelCategoryDefault));
    }

    /// <summary>
    /// Design section 6: at the low tier a drawn face is a few pixels tall and
    /// buys nothing over the torso quad, so the sprite is skipped there.
    /// </summary>
    [Fact]
    public void TheLowDetailTierKeepsTheProceduralBody()
    {
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.Low,
            VisualFallbackStep.ModelCategoryDefault));
    }

    /// <summary>
    /// The diagnostic placeholder exists to make a failed catalog resolution
    /// visible on screen. Covering it with an authored body would hide the
    /// very thing it is drawn to report.
    /// </summary>
    [Fact]
    public void TheDiagnosticPlaceholderIsNeverCoveredByASprite()
    {
        Assert.False(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.DiagnosticPlaceholder));
    }

    /// <summary>
    /// Only the placeholder step blocks the sprite. Every other fallback step
    /// resolved a real appearance, so the body draws normally under all of
    /// them.
    /// </summary>
    [Fact]
    public void EveryNonPlaceholderFallbackStepStillDrawsTheSprite()
    {
        Assert.True(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.SpecificVariant));
        Assert.True(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.FamilyDefault));
        Assert.True(PawnRenderer.DrawsSpriteBody(
            PawnVisualStyle.SpriteBody,
            hasBodyAtlas: true,
            PawnDetailTier.High,
            VisualFallbackStep.ModelCategoryDefault));
    }
}
