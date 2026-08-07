using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Sandata.Client.UI;
using Sandata.Core.Navigation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 46's done-when bar, verbatim: the marquee predicate includes and
/// excludes at its exact boundary and never selects a hostile; drag capture
/// consumes a drag that begins on a panel without producing a marquee; the
/// undo stack discards the oldest at its depth limit; and the minimap's pixel
/// rectangle for a known cell is pinned. No member of this class constructs a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a <c>SandataGame</c>.
/// </summary>
public sealed class InteractionScaffoldTests
{
    // ---- MultiSelectState: marquee inclusion predicate --------------------

    [Fact]
    public void Marquee_IncludesTheLastPixelInsideItsBounds()
    {
        var marqueeBounds = new Rectangle(0, 0, 10, 10);
        var lastIncludedPixel = new MarqueeCandidate(EntityId: 1, ScreenPosition: new Point(9, 9), IsHostile: false);

        Assert.True(MultiSelectState.IsIncluded(marqueeBounds, lastIncludedPixel));
    }

    [Fact]
    public void Marquee_ExcludesTheFirstPixelPastItsBounds()
    {
        var marqueeBounds = new Rectangle(0, 0, 10, 10);
        var firstExcludedPixel = new MarqueeCandidate(EntityId: 1, ScreenPosition: new Point(10, 10), IsHostile: false);

        Assert.False(MultiSelectState.IsIncluded(marqueeBounds, firstExcludedPixel));
    }

    [Fact]
    public void Marquee_NeverIncludesAHostileEvenWellInsideItsBounds()
    {
        var marqueeBounds = new Rectangle(0, 0, 10, 10);
        var hostileAtCenter = new MarqueeCandidate(EntityId: 1, ScreenPosition: new Point(5, 5), IsHostile: true);

        Assert.False(MultiSelectState.IsIncluded(marqueeBounds, hostileAtCenter));
    }

    [Fact]
    public void Marquee_FromMarqueeSelectsOnlyFriendliesInsideBounds()
    {
        var marqueeBounds = new Rectangle(0, 0, 10, 10);
        var candidates = new List<MarqueeCandidate>
        {
            new(EntityId: 1, ScreenPosition: new Point(9, 9), IsHostile: false), // last included pixel
            new(EntityId: 2, ScreenPosition: new Point(10, 10), IsHostile: false), // first excluded pixel
            new(EntityId: 3, ScreenPosition: new Point(5, 5), IsHostile: true), // hostile, inside bounds
        };

        var state = MultiSelectState.FromMarquee(marqueeBounds, candidates);

        Assert.Single(state.SelectedEntityIds);
        Assert.Equal(1, state.SelectedEntityIds[0]);
    }

    // ---- DragCapture: pointer priority -------------------------------------

    [Fact]
    public void DragCapture_ConsumesADragStartingOnAPanelWithoutProducingAMarquee()
    {
        var panelBounds = new List<Rectangle> { new(0, 0, 200, 100) };
        var startInsidePanel = new Point(50, 50);

        var capture = DragCapture.Begin(startInsidePanel, panelBounds);

        Assert.False(capture.IsActive);
        Assert.Null(capture.MarqueeBounds);
    }

    [Fact]
    public void DragCapture_BeginsAndProducesAMarqueeWhenStartingOutsideEveryPanel()
    {
        var panelBounds = new List<Rectangle> { new(0, 0, 200, 100) };
        var startOutsidePanels = new Point(300, 300);

        var capture = DragCapture.Begin(startOutsidePanels, panelBounds)
            .WithCurrentPosition(new Point(340, 330));

        Assert.True(capture.IsActive);
        Assert.Equal(new Rectangle(300, 300, 40, 30), capture.MarqueeBounds);
    }

    // ---- UndoStack: depth limit --------------------------------------------

    [Fact]
    public void UndoStack_DiscardsTheOldestEntryOnceItGrowsPastTheDepthLimit()
    {
        var stack = new UndoStack<int>(depthLimit: 3);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4); // pushes the stack past its limit of 3

        Assert.Equal(3, stack.Depth);
        Assert.Equal([2, 3, 4], stack.Entries);
        Assert.DoesNotContain(1, stack.Entries);
    }

    [Fact]
    public void UndoStack_PopReturnsTheMostRecentlyPushedEntry()
    {
        var stack = new UndoStack<int>(depthLimit: 3);
        stack.Push(1);
        stack.Push(2);

        var popped = stack.TryPop(out var entry);

        Assert.True(popped);
        Assert.Equal(2, entry);
        Assert.Equal([1], stack.Entries);
    }

    [Fact]
    public void UndoStack_PopOnAnEmptyStackFails()
    {
        var stack = new UndoStack<int>(depthLimit: 3);

        var popped = stack.TryPop(out _);

        Assert.False(popped);
    }

    // ---- Minimap: pinned pixel rectangle -----------------------------------

    [Fact]
    public void Minimap_BoundsArePinnedForAKnownGridAndWindow()
    {
        var grid = new NavGrid(width: 20, height: 15);
        var windowBounds = new Rectangle(0, 0, 1920, 1080);

        var panelBounds = Minimap.CalculateBounds(windowBounds, grid);

        Assert.Equal(new Rectangle(12, 12, 22, 17), panelBounds);
    }

    [Fact]
    public void Minimap_PixelRectangleForAKnownCellIsPinned()
    {
        var grid = new NavGrid(width: 20, height: 15);
        var windowBounds = new Rectangle(0, 0, 1920, 1080);
        var panelBounds = Minimap.CalculateBounds(windowBounds, grid);

        var cellPixelBounds = Minimap.CalculateCellPixelBounds(panelBounds, grid, cellX: 5, cellY: 3);

        Assert.Equal(new Rectangle(18, 16, 1, 1), cellPixelBounds);
    }

    [Fact]
    public void Minimap_CellPixelBoundsThrowsForACellOutsideTheGrid()
    {
        var grid = new NavGrid(width: 20, height: 15);
        var windowBounds = new Rectangle(0, 0, 1920, 1080);
        var panelBounds = Minimap.CalculateBounds(windowBounds, grid);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Minimap.CalculateCellPixelBounds(panelBounds, grid, cellX: 20, cellY: 0));
    }
}
