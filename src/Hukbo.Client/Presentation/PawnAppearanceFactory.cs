using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Core.Combat;
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

    // Weapon and shield roles come only from the authoritative Core loadout.
    // Entity ID drives stature, build, clothing, skin, and head treatment
    // only — it must never influence equipment identity.
    /// <param name="isLeader">
    /// Whether the simulation has currently elected this entity as its
    /// contingent's leader (<c>AgentView.IsLeader</c>). Forwarded to
    /// <see cref="AppearancePresets.SelectPreset"/> only — it changes which
    /// preset pool is walked and therefore
    /// <see cref="PawnAppearance.AppearancePresetId"/> and
    /// <see cref="PawnAppearance.GarmentBaseTone"/>, never
    /// <paramref name="entityId"/>'s stature, build, skin, clothing, accent,
    /// head treatment, weapon tint, or shield skin, which are all rolled
    /// above this call and do not read it. Defaults to
    /// <see langword="false"/> so the many call sites that build an
    /// appearance for a purpose other than leadership (geometry, quad-count,
    /// and cull tests among them) keep compiling unchanged; a caller that
    /// does know the real value — <see cref="PawnAppearanceCache.Resolve"/>
    /// chief among them — must forward it rather than lean on this default,
    /// because an omitted argument here is indistinguishable from a
    /// deliberate "not a leader".
    /// </param>
    /// <param name="factionId">
    /// VIS-018: the pawn's faction, feeding the appearance-preset block-
    /// assignment stream (<see cref="AppearancePresets.SelectBlock"/>).
    /// Defaults to 0 so every call site written before this task keeps
    /// compiling unchanged; the milestone's single-entry block-assignment
    /// table makes the value inconsequential until VIS-022 grows it.
    /// </param>
    /// <param name="scenarioSeed">
    /// VIS-018: the match's <c>Scenario.Seed</c>, the other block-assignment
    /// input. Defaults to 0 for the same reason as
    /// <paramref name="factionId"/>. A future integration task threads the
    /// real values through the existing render and inspector call sites once
    /// more than one block ships.
    /// </param>
    public static PawnAppearance Create(
        ulong entityId,
        WeaponId weapon,
        ShieldId shield,
        bool isLeader = false,
        int factionId = 0,
        ulong scenarioSeed = 0)
    {
        var bodyMix = Mix(entityId ^ 0xA0761D6478BD642FUL);
        var clothingMix = Mix(entityId ^ 0xE7037ED1A0B428DBUL);
        var detailMix = Mix(entityId ^ 0x8EBC6AF09C88C6E3UL);

        var weaponRole = ToWeaponRole(weapon);
        var shieldRole = ToShieldRole(shield);

        // VIS-010: the weapon-tint stream is independent of, and mixed with
        // a different salt than, every stream above — WeaponTintSalt, not
        // PawnBodySalt/PawnClothingSalt/PawnDetailSalt — and it never
        // influences weaponRole itself. WeaponVisualCatalog.SelectTint only
        // ever chooses among weaponRole's own tints (or falls back to the
        // model-category default), so equipment identity stays loadout-only.
        var tint = WeaponVisualCatalog.SelectTint(entityId, weaponRole);

        // VIS-013: same discipline as the weapon tint above, with its own
        // salt (ShieldSkinSalt) — ShieldVisualCatalog.SelectSkin only ever
        // chooses among shieldRole's own skins (or falls back to the
        // model-category default), never influencing shieldRole itself, so
        // shield *presence* stays loadout-only. Called unconditionally,
        // exactly like the weapon tint above; the result is simply never
        // drawn when shieldRole is PawnShieldRole.None.
        var shieldSkin = ShieldVisualCatalog.SelectSkin(entityId, shieldRole);

        // VIS-018: the appearance-preset selection streams, independent of
        // and additive to every roll above — their own salts
        // (AppearanceBlockAssignmentSalt, AppearancePresetSelectionSalt)
        // never touch PawnBodySalt/PawnClothingSalt/PawnDetailSalt, so
        // bodyMix/clothingMix/detailMix and everything derived from them
        // above are unaffected by this block. isLeader chooses which of
        // SelectPreset's two pools is walked (leader-character-design.md
        // section 4.1); it introduces no new salt of its own.
        var block = AppearancePresets.SelectBlock(scenarioSeed, factionId);
        var preset = AppearancePresets.SelectPreset(entityId, block, weaponRole, isLeader);
        var skinColor = SelectSkinColor(detailMix);

        // The garment base tone folded into the torso fill at Low tier
        // (warrior-appearance-design.md zoom table). Every preset shipped
        // this milestone recipes D1 — Bare-Chested — whose own catalog note
        // says it "renders as the base skin-tone torso fill"
        // (AppearanceComponentCatalog.TorsoD1BareChested), so the tone is
        // simply this pawn's own skin tone. VIS-020+ must extend this
        // resolution once a preset with a dyed torso garment (D2/D3/D4)
        // ships; there is no dye color to resolve yet because no shipped
        // recipe carries one.
        var garmentBaseTone = skinColor;

        return new PawnAppearance(
            weaponRole,
            shieldRole,
            SelectStature(bodyMix),
            SelectBuild(bodyMix >> 8),
            (PawnHeadTreatment)((bodyMix >> 16) % 3),
            SelectClothingColor(clothingMix),
            SelectAccentColor(clothingMix >> 8),
            skinColor,
            SelectHeadTreatmentColor(detailMix >> 8),
            tint.Catalog.Id,
            tint.BladeColor,
            tint.GripColor,
            tint.LashingBandColor,
            shieldSkin.Catalog.Id,
            shieldSkin.FaceColor,
            preset.Catalog.Id,
            garmentBaseTone);
    }

    /// <summary>
    /// Visible beyond this factory because <c>BattleEventFormatter</c> needs
    /// the same weapon-to-role mapping to reach
    /// <see cref="PawnAppearance.GetWeaponLabel"/>, and a second copy of this
    /// switch is exactly what produced the crash that fix repairs.
    /// </summary>
    internal static PawnWeaponRole ToWeaponRole(WeaponId weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => PawnWeaponRole.Kampilan,
            WeaponId.Wasay => PawnWeaponRole.Wasay,
            WeaponId.Kalis => PawnWeaponRole.Kalis,
            WeaponId.Itak => PawnWeaponRole.Itak,
            WeaponId.Bangkaw => PawnWeaponRole.Bangkaw,
            WeaponId.Busog => PawnWeaponRole.Busog,
            WeaponId.Arquebus => PawnWeaponRole.Arquebus,
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };

    private static PawnShieldRole ToShieldRole(ShieldId shield) =>
        shield switch
        {
            ShieldId.None => PawnShieldRole.None,
            ShieldId.TallHardwood => PawnShieldRole.TallHardwood,
            // T4 (shield-projectile-block-design.md): the only place
            // ShieldId.NarrowBreastHigh maps to its PawnShieldRole.
            // KNOWN GAP, not this task's file: ShieldVisualCatalog.GetSkins
            // (Presentation/Catalogs/ShieldVisualCatalog.cs) has no case for
            // PawnShieldRole.NarrowBreastHigh yet, so SelectSkin below throws
            // ArgumentOutOfRangeException for this role until that catalog is
            // extended — see this task's report for the affected tests.
            ShieldId.NarrowBreastHigh => PawnShieldRole.NarrowBreastHigh,
            _ => throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                null),
        };

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
