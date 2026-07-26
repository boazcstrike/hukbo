using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

internal static class PawnAppearanceFactory
{
    private static readonly Color Cream = new(231, 216, 183);
    private static readonly Color Indigo = new(53, 77, 107);
    private static readonly Color TextileRed = new(143, 63, 53);
    private static readonly Color PatinaGreen = new(81, 112, 100);
    private static readonly Color Ochre = new(168, 116, 60);
    private static readonly Color Gold = new(208, 166, 74);
    private static readonly Color CharredWood = new(48, 40, 33);
    private static readonly Color LightSkin = new(186, 132, 88);
    private static readonly Color MediumSkin = new(156, 103, 66);
    private static readonly Color DarkSkin = new(119, 76, 50);

    public static PawnAppearance Create(ulong entityId)
    {
        var bodyMix = Mix(entityId ^ 0xA0761D6478BD642FUL);
        var clothingMix = Mix(entityId ^ 0xE7037ED1A0B428DBUL);
        var detailMix = Mix(entityId ^ 0x8EBC6AF09C88C6E3UL);

        return new PawnAppearance(
            (PawnWeaponRole)(entityId % 5),
            SelectStature(bodyMix),
            SelectBuild(bodyMix >> 8),
            (PawnHeadTreatment)((bodyMix >> 16) % 3),
            SelectClothingColor(clothingMix),
            SelectAccentColor(clothingMix >> 8),
            SelectSkinColor(detailMix),
            SelectHeadTreatmentColor(detailMix >> 8));
    }

    private static float SelectStature(ulong value) =>
        (value % 3) switch
        {
            0 => 0.90f,
            1 => 1.00f,
            _ => 1.10f,
        };

    private static float SelectBuild(ulong value) =>
        (value % 3) switch
        {
            0 => 0.86f,
            1 => 1.00f,
            _ => 1.18f,
        };

    private static Color SelectClothingColor(ulong value) =>
        (value % 4) switch
        {
            0 => Cream,
            1 => Indigo,
            2 => TextileRed,
            _ => PatinaGreen,
        };

    private static Color SelectAccentColor(ulong value) =>
        (value % 3) switch
        {
            0 => Ochre,
            1 => Gold,
            _ => Cream,
        };

    private static Color SelectSkinColor(ulong value) =>
        (value % 3) switch
        {
            0 => LightSkin,
            1 => MediumSkin,
            _ => DarkSkin,
        };

    private static Color SelectHeadTreatmentColor(ulong value) =>
        (value % 3) switch
        {
            0 => CharredWood,
            1 => Indigo,
            _ => TextileRed,
        };

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
