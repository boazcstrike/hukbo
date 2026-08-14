using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class RightColumnSplitTests
{
    private static readonly Rectangle Column = new(860, 80, 420, 640);

    [Fact]
    public void Split_GivesTheWholeColumnToTheEventLogWhenTheSoundLogIsHidden()
    {
        var split = Split(isEventLogVisible: true, isSoundLogVisible: false);

        Assert.Equal(Column, split.EventBounds);
        Assert.Equal(Rectangle.Empty, split.SoundLogBounds);
    }

    [Fact]
    public void Split_GivesTheWholeColumnToTheSoundLogWhenTheEventLogIsHidden()
    {
        var split = Split(isEventLogVisible: false, isSoundLogVisible: true);

        Assert.Equal(Rectangle.Empty, split.EventBounds);
        Assert.Equal(Column, split.SoundLogBounds);
    }

    [Fact]
    public void Split_ProducesNoBoundsWhenNeitherLogIsVisible()
    {
        var split = Split(isEventLogVisible: false, isSoundLogVisible: false);

        Assert.Equal(Rectangle.Empty, split.EventBounds);
        Assert.Equal(Rectangle.Empty, split.SoundLogBounds);
    }

    [Fact]
    public void Split_StacksTheSoundLogUnderTheEventLogWithoutOverlap()
    {
        var split = Split(isEventLogVisible: true, isSoundLogVisible: true);

        Assert.Equal(Column.Left, split.SoundLogBounds.Left);
        Assert.Equal(Column.Width, split.SoundLogBounds.Width);
        Assert.Equal(Column.Top, split.EventBounds.Top);
        Assert.Equal(Column.Bottom, split.SoundLogBounds.Bottom);
        Assert.Equal(
            10,
            split.SoundLogBounds.Top - split.EventBounds.Bottom);
        Assert.False(split.EventBounds.Intersects(split.SoundLogBounds));
    }

    [Fact]
    public void Split_UsesTheRequestedShareOfTheColumn()
    {
        var split = Split(isEventLogVisible: true, isSoundLogVisible: true);

        Assert.Equal(Column.Height * 45 / 100, split.SoundLogBounds.Height);
        Assert.Equal(
            Column.Height,
            split.EventBounds.Height + split.SoundLogBounds.Height + 10);
    }

    [Fact]
    public void Split_HonoursTheMinimumHeightOnAShortColumn()
    {
        var split = RightColumnSplit.Split(
            new Rectangle(0, 0, 420, 200),
            isEventLogVisible: true,
            isSoundLogVisible: true,
            soundLogMinimumHeight: 168,
            soundLogHeightPercent: 45,
            gap: 10);

        Assert.Equal(168, split.SoundLogBounds.Height);
        Assert.Equal(22, split.EventBounds.Height);
    }

    [Fact]
    public void Split_NeverProducesANegativeHeight()
    {
        var split = RightColumnSplit.Split(
            new Rectangle(0, 0, 420, 40),
            isEventLogVisible: true,
            isSoundLogVisible: true,
            soundLogMinimumHeight: 168,
            soundLogHeightPercent: 45,
            gap: 10);

        Assert.Equal(40, split.SoundLogBounds.Height);
        Assert.Equal(0, split.EventBounds.Height);
    }

    [Fact]
    public void Split_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RightColumnSplit.Split(Column, true, true, -1, 45, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RightColumnSplit.Split(Column, true, true, 168, 45, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RightColumnSplit.Split(Column, true, true, 168, 101, 10));
    }

    private static RightColumnSplit.ColumnBounds Split(
        bool isEventLogVisible, bool isSoundLogVisible) =>
        RightColumnSplit.Split(
            Column,
            isEventLogVisible,
            isSoundLogVisible,
            soundLogMinimumHeight: 168,
            soundLogHeightPercent: 45,
            gap: 10);
}
