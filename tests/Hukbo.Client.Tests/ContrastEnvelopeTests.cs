using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class ContrastEnvelopeTests
{
    [Fact]
    public void ColorDistance_IsZeroForIdenticalColors()
    {
        var color = new Color(56, 66, 73);

        var distance = ContrastEnvelope.ColorDistance(color, color);

        Assert.Equal(0f, distance);
    }

    [Fact]
    public void ColorDistance_PinnedOnBlackToWhite()
    {
        var black = new Color(0, 0, 0);
        var white = new Color(255, 255, 255);

        var distance = ContrastEnvelope.ColorDistance(black, white);

        Assert.Equal(441.6729f, distance, precision: 4);
    }

    [Fact]
    public void ColorDistance_PinnedOnAKnownThreeFourFiveOffset()
    {
        var a = new Color(10, 20, 30);
        var b = new Color(13, 24, 35);

        var distance = ContrastEnvelope.ColorDistance(a, b);

        Assert.Equal(7.0710678f, distance, precision: 4);
    }

    [Fact]
    public void ColorDistance_IsSymmetric()
    {
        var a = new Color(64, 164, 255);
        var b = new Color(231, 199, 84);

        Assert.Equal(
            ContrastEnvelope.ColorDistance(a, b),
            ContrastEnvelope.ColorDistance(b, a),
            precision: 4);
    }

    [Fact]
    public void EnvelopeConstants_ArePinned()
    {
        Assert.Equal(60f, ContrastEnvelope.MinimumGroundDistance);
        Assert.Equal(60f, ContrastEnvelope.MinimumClothingDistance);
        Assert.Equal(80f, ContrastEnvelope.MinimumFactionDyeDistance);
    }

    [Fact]
    public void IsWithinEnvelope_FlagsAColorTooCloseToAnyReferenceEntry()
    {
        Color[] reference = [new Color(0, 0, 0), new Color(180, 180, 180)];
        var tooCloseToBlack = new Color(5, 5, 5);

        var result = ContrastEnvelope.IsWithinEnvelope(
            tooCloseToBlack,
            reference,
            ContrastEnvelope.MinimumGroundDistance);

        Assert.False(result);
    }

    [Fact]
    public void IsWithinEnvelope_AcceptsAColorFarFromEveryReferenceEntry()
    {
        Color[] reference = [new Color(0, 0, 0), new Color(180, 180, 180)];
        var farEnough = new Color(255, 255, 255);

        var result = ContrastEnvelope.IsWithinEnvelope(
            farEnough,
            reference,
            ContrastEnvelope.MinimumGroundDistance);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinEnvelope_FlagsADyeIdenticalToAFactionConstant()
    {
        // Faction pawn colors are fixed constants (blue, red, gold) per
        // visual-system-integration-design.md section 3.
        Color[] factionConstants =
        [
            new Color(64, 164, 255),
            new Color(255, 91, 105),
            new Color(231, 199, 84),
        ];
        var illegalDye = new Color(64, 164, 255);

        var result = ContrastEnvelope.IsWithinEnvelope(
            illegalDye,
            factionConstants,
            ContrastEnvelope.MinimumFactionDyeDistance);

        Assert.False(result);
    }

    [Fact]
    public void IsWithinEnvelope_TrueForAnEmptyReferenceSet()
    {
        var candidate = new Color(56, 66, 73);

        var result = ContrastEnvelope.IsWithinEnvelope(
            candidate,
            [],
            ContrastEnvelope.MinimumGroundDistance);

        Assert.True(result);
    }
}
