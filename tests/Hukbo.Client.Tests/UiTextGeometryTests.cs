using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class UiTextGeometryTests
{
    [Fact]
    public void SnapToPixel_RoundsFractionalCoordinatesToWholePixels()
    {
        var snapped = UiTextGeometry.SnapToPixel(new Vector2(10.4f, 20.6f));

        Assert.Equal(10f, snapped.X);
        Assert.Equal(21f, snapped.Y);
        Assert.Equal(MathF.Round(snapped.X), snapped.X);
        Assert.Equal(MathF.Round(snapped.Y), snapped.Y);
    }

    [Fact]
    public void SnapToPixel_LeavesAlreadyWholeCoordinatesUnchanged()
    {
        var snapped = UiTextGeometry.SnapToPixel(new Vector2(42f, -8f));

        Assert.Equal(new Vector2(42f, -8f), snapped);
    }

    [Fact]
    public void GetCenteredTopLeft_ReturnsWholePixelOriginForOddMeasuredWidth()
    {
        var topLeft = UiTextGeometry.GetCenteredTopLeft(
            measuredSize: new Vector2(7f, 10f),
            center: new Vector2(100f, 50f));

        Assert.Equal(MathF.Round(topLeft.X), topLeft.X);
        Assert.Equal(MathF.Round(topLeft.Y), topLeft.Y);
    }

    [Fact]
    public void GetCenteredTopLeft_ReturnsWholePixelOriginForEvenMeasuredWidth()
    {
        var topLeft = UiTextGeometry.GetCenteredTopLeft(
            measuredSize: new Vector2(8f, 10f),
            center: new Vector2(100f, 50f));

        Assert.Equal(MathF.Round(topLeft.X), topLeft.X);
        Assert.Equal(MathF.Round(topLeft.Y), topLeft.Y);
        Assert.Equal(96f, topLeft.X);
        Assert.Equal(45f, topLeft.Y);
    }

    [Fact]
    public void GetCenteredTopLeft_ReturnsWholePixelOriginForFractionalCenter()
    {
        var topLeft = UiTextGeometry.GetCenteredTopLeft(
            measuredSize: new Vector2(7f, 9f),
            center: new Vector2(100.3f, 50.7f));

        Assert.Equal(MathF.Round(topLeft.X), topLeft.X);
        Assert.Equal(MathF.Round(topLeft.Y), topLeft.Y);
    }
}
