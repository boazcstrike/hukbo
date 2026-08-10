using Hukbo.Client.Settings;
using Hukbo.Client.Theming;

namespace Hukbo.Client.Tests;

public sealed class UiFontRampTests
{
    private static string ContentProjectPath =>
        Path.Combine(AppContext.BaseDirectory, "Content", "Content.mgcb");

    [Fact]
    public void EveryRoleHasADistinctAssetId()
    {
        var assetIds = UiFontRamp.AllScales
            .SelectMany(scale => UiFontRamp.AllRoles.Select(
                role => UiFontRamp.GetAssetId(role, scale)))
            .ToList();

        Assert.Equal(assetIds.Count, assetIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(1280, 720, UiScale.Percent100)]
    [InlineData(1920, 1080, UiScale.Percent125)]
    [InlineData(2560, 1440, UiScale.Percent150)]
    [InlineData(3840, 2160, UiScale.Percent200)]
    [InlineData(3440, 1080, UiScale.Percent125)]
    [InlineData(2048, 1152, UiScale.Percent125)]
    public void AutoScaleUsesBothViewportAxes(
        int width,
        int height,
        UiScale expected)
    {
        Assert.Equal(expected, UiScalePolicy.Resolve(UiScale.Auto, width, height));
    }

    /// <summary>
    /// The two viewports a 2560x1440 display at 125% Windows scaling produces,
    /// depending on whether the process declared DPI awareness.
    /// </summary>
    /// <remarks>
    /// A DPI-unaware process is told the desktop is 2048x1152, which clears
    /// this policy's 1920x1080 threshold but misses its 2560x1440 one, so it
    /// selects the 125% bake on a panel that deserves the 150% one. That is
    /// half of what smoke row UI-4 failed on, and it is a symptom of the
    /// fabricated viewport rather than a fault in the thresholds — which is
    /// why the fix is <see cref="Settings.ProcessDpiAwareness"/> and this
    /// policy is unchanged. Pinned as a pair so the connection survives any
    /// later threshold edit.
    /// </remarks>
    [Fact]
    public void TheVirtualisedViewportSelectsALowerTierThanTheRealOne()
    {
        var virtualised = UiScalePolicy.Resolve(UiScale.Auto, 2048, 1152);
        var real = UiScalePolicy.Resolve(UiScale.Auto, 2560, 1440);

        Assert.Equal(UiScale.Percent125, virtualised);
        Assert.Equal(UiScale.Percent150, real);
        Assert.True(
            UiScalePolicy.GetPercent(virtualised)
                < UiScalePolicy.GetPercent(real));
    }

    [Theory]
    [InlineData(UiScale.Percent100)]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void ExplicitScaleIsCappedByTheViewport(UiScale scale)
    {
        Assert.Equal(
            UiScale.Percent100,
            UiScalePolicy.Resolve(scale, 1280, 720));
    }

    [Fact]
    public void PixelSizesAreStrictlyIncreasingAcrossTheRamp()
    {
        var sizes = UiFontRamp.AllRoles
            .Select(UiFontRamp.GetPixelSize)
            .ToList();

        for (var index = 1; index < sizes.Count; index++)
        {
            Assert.True(
                sizes[index] > sizes[index - 1],
                $"Expected size at index {index} ({sizes[index]}) to exceed " +
                $"the previous rung ({sizes[index - 1]}).");
        }
    }

    [Fact]
    public void PixelSizeMatchesTheDesignedRampForEveryRole()
    {
        var expectedSizes = new Dictionary<UiFontRole, int>
        {
            [UiFontRole.Caption] = 12,
            [UiFontRole.Body] = 14,
            [UiFontRole.Label] = 17,
            [UiFontRole.Subtitle] = 20,
            [UiFontRole.Title] = 22,
            [UiFontRole.Display] = 38,
        };

        foreach (var (role, expectedSize) in expectedSizes)
        {
            Assert.Equal(expectedSize, UiFontRamp.GetPixelSize(role));
        }
    }

    [Fact]
    public void ParseRoundTripsEveryRoleName()
    {
        foreach (var role in UiFontRamp.AllRoles)
        {
            var parsed = UiFontRamp.Parse(role.ToString());

            Assert.Equal(role, parsed);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("caption")]
    [InlineData("Heading")]
    [InlineData("Fonts/UiCaption")]
    public void ParseRejectsAnyStringThatIsNotAnExactRoleName(string roleName)
    {
        Assert.Throws<FormatException>(() => UiFontRamp.Parse(roleName));
    }

    [Fact]
    public void ApproximateAdvanceIsPositiveAndMonotonicAcrossTheRamp()
    {
        var advances = UiFontRamp.AllRoles
            .Select(UiFontRamp.GetApproximateAdvancePx)
            .ToList();

        Assert.All(advances, advance => Assert.True(advance > 0));

        for (var index = 1; index < advances.Count; index++)
        {
            Assert.True(
                advances[index] >= advances[index - 1],
                $"Expected advance at index {index} ({advances[index]}) to be " +
                $"at least the previous rung's advance ({advances[index - 1]}).");
        }
    }

    [Fact]
    public void EveryRampAssetHasAMatchingContentProjectBuildLine()
    {
        var mgcbLines = File.ReadAllLines(ContentProjectPath);

        foreach (var scale in UiFontRamp.AllScales)
        {
            foreach (var role in UiFontRamp.AllRoles)
            {
                var expectedBuildLine =
                    $"/build:{UiFontRamp.GetAssetId(role, scale)}.spritefont";

                Assert.Contains(
                    mgcbLines,
                    line => string.Equals(
                        line.Trim(),
                        expectedBuildLine,
                        StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void NoContentProjectFontBuildLineIsOrphanedFromTheRamp()
    {
        var rampAssetIds = UiFontRamp.AllScales
            .SelectMany(scale => UiFontRamp.AllRoles.Select(
                role => UiFontRamp.GetAssetId(role, scale)))
            .ToHashSet(StringComparer.Ordinal);

        var mgcbLines = File.ReadAllLines(ContentProjectPath);
        var buildLinePrefix = "/build:Fonts/";
        var buildLineSuffix = ".spritefont";

        foreach (var line in mgcbLines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(buildLinePrefix, StringComparison.Ordinal) ||
                !trimmed.EndsWith(buildLineSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var assetId = trimmed[
                "/build:".Length..^buildLineSuffix.Length];

            Assert.Contains(assetId, rampAssetIds);
        }
    }
}
