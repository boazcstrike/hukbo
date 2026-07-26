using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

internal enum PawnWeaponRole
{
    LongSpear,
    HardenedJavelin,
    WarBow,
    BroadDagger,
    GreatBlade,
}

internal enum PawnHeadTreatment
{
    CroppedHair,
    Headcloth,
    WrappedCloth,
}

internal readonly record struct PawnAppearance(
    PawnWeaponRole WeaponRole,
    float StatureMultiplier,
    float BuildMultiplier,
    PawnHeadTreatment HeadTreatment,
    Color ClothingColor,
    Color AccentColor,
    Color SkinColor,
    Color HeadTreatmentColor)
{
    public string WeaponLabel =>
        WeaponRole switch
        {
            PawnWeaponRole.LongSpear => "Bangkaw - Long Spear",
            PawnWeaponRole.HardenedJavelin => "Hardened Javelin",
            PawnWeaponRole.WarBow => "Busog - War Bow",
            PawnWeaponRole.BroadDagger => "Broad Dagger",
            PawnWeaponRole.GreatBlade => "Great Blade",
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };
}
