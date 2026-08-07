using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Client.UI;
using Sandata.Core.Navigation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 69's own done-when bar for <see cref="HudComposer"/>: the composed
/// rectangles do not overlap at three window sizes, every element from design
/// section 11's HUD list is present exactly once, and the composer degrades
/// sanely at the smallest supported window rather than producing negative or
/// inverted rectangles. No member of this class constructs a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a <c>SandataGame</c>.
/// </summary>
public sealed class HudComposerTests
{
    // Placeholder inputs this test hands to HudComposer.Compose. None of
    // these are pinned by design or plan; they are this test's own stand-ins
    // for values a running battle would supply — see SandataGame.cs's own
    // PLACEHOLDERS section for the equivalent values it uses at runtime.
    private const int PlaceholderOperatorCount = 4;
    private const int PlaceholderContactCount = 5;

    private static readonly NavGrid PlaceholderMinimapGrid = new(width: 20, height: 15);

    // Design section 11's HUD element list, transcribed verbatim from that
    // table's own "Element" column, in the table's own row order. A future
    // design edit that adds, removes, or renames a row must edit this array
    // to match — until it does, this test is pinned against today's design,
    // not merely against whatever HudComposer.Compose happens to return.
    private static readonly string[] DesignSection11HudElements =
    [
        "Roster strip",
        "Contact list",
        "Alert indicator",
        "Mission clock and tick counter",
        "Event log",
        "Operator inspector",
        "Spectator control bar",
        "Fire cone overlay",
        "Order path overlay",
        "Breach-point marker",
        "Minimap",
        "Multi-select marquee",
        "Undo stack",
    ];

    private static HudComposer.Layout ComposeAt(int windowWidth, int windowHeight) =>
        HudComposer.Compose(
            new Rectangle(0, 0, windowWidth, windowHeight),
            PlaceholderOperatorCount,
            PlaceholderContactCount,
            PlaceholderMinimapGrid);

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void Compose_ProducesEveryDesignSection11ElementExactlyOnce(int windowWidth, int windowHeight)
    {
        var layout = ComposeAt(windowWidth, windowHeight);
        var namedElements = HudComposer.ToNamedElements(layout);

        var producedNames = namedElements.Select(e => e.Element).ToList();

        Assert.Equal(DesignSection11HudElements.Length, namedElements.Count);
        Assert.Equal(
            DesignSection11HudElements.OrderBy(n => n, System.StringComparer.Ordinal),
            producedNames.OrderBy(n => n, System.StringComparer.Ordinal));
        // "Exactly once": no duplicate element names in the composed set.
        Assert.Equal(producedNames.Count, producedNames.Distinct().Count());
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void Compose_RectanglesDoNotOverlapAtThreeWindowSizes(int windowWidth, int windowHeight)
    {
        var layout = ComposeAt(windowWidth, windowHeight);
        var rectangles = HudComposer.ToNamedElements(layout).ToList();

        for (var i = 0; i < rectangles.Count; i++)
        {
            for (var j = i + 1; j < rectangles.Count; j++)
            {
                var (nameA, boundsA) = rectangles[i];
                var (nameB, boundsB) = rectangles[j];

                Assert.False(
                    boundsA.Intersects(boundsB),
                    $"'{nameA}' {boundsA} overlaps '{nameB}' {boundsB} at {windowWidth}x{windowHeight}.");
            }
        }
    }

    [Fact]
    public void Compose_DegradesSanelyAtTheSmallestSupportedWindow()
    {
        // This repository defines no named "smallest supported window"
        // constant anywhere in Sandata.Client or the design/plan docs. 320x180
        // is this test's own placeholder floor, deliberately far below every
        // panel's preferred size so every clamp path in every task-38/46
        // helper this composer calls actually executes.
        const int smallestSupportedWidth = 320;
        const int smallestSupportedHeight = 180;

        var layout = ComposeAt(smallestSupportedWidth, smallestSupportedHeight);
        var rectangles = HudComposer.ToNamedElements(layout);

        foreach (var (name, bounds) in rectangles)
        {
            Assert.True(bounds.Width >= 0, $"'{name}' produced a negative width: {bounds}.");
            Assert.True(bounds.Height >= 0, $"'{name}' produced a negative height: {bounds}.");
        }
    }

    [Fact]
    public void ToNamedElements_MatchesDesignSection11HudElementListExactly()
    {
        var layout = ComposeAt(1280, 720);
        var producedNames = HudComposer.ToNamedElements(layout).Select(e => e.Element).ToList();

        // Order-sensitive: catches a row silently dropped or renamed even if
        // the count still matches.
        Assert.Equal((IReadOnlyList<string>)DesignSection11HudElements, producedNames);
    }
}
