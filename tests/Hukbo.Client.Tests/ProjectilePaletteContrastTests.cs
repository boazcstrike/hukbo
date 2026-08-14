using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// PV-7. Pins <see cref="ProjectilePalette"/>'s three element colours
/// against <see cref="ContrastEnvelope.MinimumGroundDistance"/> for every
/// shipped ground shade, the same "arrow vanishes on some backdrop" failure
/// weapon tints already guard against (<c>WeaponVisualCatalogTests</c>).
/// </summary>
public sealed class ProjectilePaletteContrastTests
{
    // --- Reference colors mirroring production sources, matching
    // WeaponVisualCatalogTests' own "mirrors ContrastEnvelopeTests' own
    // reference set" convention rather than importing the production types
    // that own them. The six shipped themes' ArenaSurface/ArenaBorder pair,
    // lerped to PlainsBackdropGeometry.MaximumBackdropInterpolation (0.22).
    // Hex values mirror src/Hukbo.Client/Content/Themes/ui-theme-
    // standards.json (command, field-manual, signal, broadcast,
    // high-contrast, datu-court). ---

    private static readonly Color GroundShadeCommand = new(40, 54, 73);
    private static readonly Color GroundShadeFieldManual = new(201, 185, 146);
    private static readonly Color GroundShadeSignal = new(26, 42, 46);
    private static readonly Color GroundShadeBroadcast = new(212, 217, 222);
    private static readonly Color GroundShadeHighContrast = new(56, 56, 56);
    private static readonly Color GroundShadeDatuCourt = new(101, 99, 63);

    private static readonly Color[] AllGroundShades =
    [
        GroundShadeCommand,
        GroundShadeFieldManual,
        GroundShadeSignal,
        GroundShadeBroadcast,
        GroundShadeHighContrast,
        GroundShadeDatuCourt,
    ];

    public static TheoryData<Color> AllGroundShadeData()
    {
        var data = new TheoryData<Color>();
        foreach (var color in AllGroundShades)
        {
            data.Add(color);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void ProjectileShaftColor_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ProjectilePalette.ProjectileShaftColor,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void ProjectileHeadColor_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ProjectilePalette.ProjectileHeadColor,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void ProjectileFletchColor_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ProjectilePalette.ProjectileFletchColor,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }
}
