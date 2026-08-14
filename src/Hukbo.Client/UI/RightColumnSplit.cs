using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Splits the right-hand log column between the battle event log and the sound
/// log. Pure geometry, kept out of <c>ArenaGame</c> so it can be tested without
/// a window. Both logs carry their own visibility flag, giving four rows: with
/// both visible the column splits as usual, with only the event log visible it
/// keeps the whole column (the behaviour that existed before the sound
/// system), with only the sound log visible the sound log instead takes the
/// whole column, and with both hidden both bounds are
/// <see cref="Rectangle.Empty"/>.
/// </summary>
internal static class RightColumnSplit
{
    internal readonly record struct ColumnBounds(
        Rectangle EventBounds,
        Rectangle SoundLogBounds);

    public static ColumnBounds Split(
        Rectangle columnBounds,
        bool isEventLogVisible,
        bool isSoundLogVisible,
        int soundLogMinimumHeight,
        int soundLogHeightPercent,
        int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(soundLogMinimumHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        if (soundLogHeightPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(soundLogHeightPercent),
                soundLogHeightPercent,
                "The sound log share of the column must be a percentage.");
        }

        if (!isEventLogVisible)
        {
            return isSoundLogVisible
                ? new ColumnBounds(Rectangle.Empty, columnBounds)
                : new ColumnBounds(Rectangle.Empty, Rectangle.Empty);
        }

        if (!isSoundLogVisible)
        {
            return new ColumnBounds(columnBounds, Rectangle.Empty);
        }

        var availableHeight = Math.Max(0, columnBounds.Height);
        var soundLogHeight = Math.Min(
            availableHeight,
            Math.Max(
                Math.Min(soundLogMinimumHeight, availableHeight),
                availableHeight * soundLogHeightPercent / 100));
        var eventHeight = Math.Max(
            0,
            availableHeight - soundLogHeight - gap);

        return new ColumnBounds(
            new Rectangle(
                columnBounds.Left,
                columnBounds.Top,
                columnBounds.Width,
                eventHeight),
            new Rectangle(
                columnBounds.Left,
                columnBounds.Bottom - soundLogHeight,
                columnBounds.Width,
                soundLogHeight));
    }
}
