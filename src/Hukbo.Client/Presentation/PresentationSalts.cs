namespace Hukbo.Client.Presentation;

/// <summary>
/// The single registry of every presentation salt used to seed a
/// deterministic variant-selection stream (visual-system-integration-design.md
/// section 3, R-W6.2). A "salt" is a constant XORed with a stable identity
/// input — <c>EntityId</c> for per-warrior traits, <c>Scenario.Seed</c> for
/// per-match features — before mixing, so distinct trait streams never
/// correlate with each other by accident.
///
/// This class does not replace the declaration sites: the four pre-existing
/// salts stay declared and consumed exactly where they already are
/// (<see cref="PawnAppearanceFactory"/>,
/// <see cref="Hukbo.Client.Rendering.PlainsBackdropGeometry"/>). They are
/// listed here, at their existing values, purely so the distinctness test
/// below can see them alongside every new salt this package mints. A salt
/// value, once shipped, must never change — changing it reshuffles every
/// selection the stream already made.
///
/// Every new trait stream this package adds gets its own named salt here
/// before any code reads it (R-W6.2, RF-05 sibling rule). Never reuse an
/// existing salt for a new stream, and never let one call site XOR more than
/// one salt into the same stream.
/// </summary>
internal static class PresentationSalts
{
    /// <summary>
    /// One registered presentation salt: a stable name, its XOR mixing
    /// constant, and the trait stream it seeds.
    /// </summary>
    internal readonly record struct Entry(string Name, ulong Value, string Purpose);

    // --- Existing salts (declaration sites unchanged; listed for distinctness only) ---

    /// <summary>
    /// Seeds <see cref="PawnAppearanceFactory"/>'s stature, build, and
    /// head-treatment selection. Declared and consumed in
    /// <see cref="PawnAppearanceFactory"/>; unchanged by this package.
    /// </summary>
    public const ulong PawnBodySalt = 0xA0761D6478BD642FUL;

    /// <summary>
    /// Seeds <see cref="PawnAppearanceFactory"/>'s clothing and accent color
    /// selection. Declared and consumed in <see cref="PawnAppearanceFactory"/>;
    /// unchanged by this package.
    /// </summary>
    public const ulong PawnClothingSalt = 0xE7037ED1A0B428DBUL;

    /// <summary>
    /// Seeds <see cref="PawnAppearanceFactory"/>'s skin and head-treatment
    /// color selection. Declared and consumed in
    /// <see cref="PawnAppearanceFactory"/>; unchanged by this package.
    /// </summary>
    public const ulong PawnDetailSalt = 0x8EBC6AF09C88C6E3UL;

    /// <summary>
    /// Seeds the plains backdrop's ground decal placement, scoped by
    /// <c>Scenario.Seed</c>. Declared and consumed in
    /// <see cref="Hukbo.Client.Rendering.PlainsBackdropGeometry"/>; unchanged
    /// by this package.
    /// </summary>
    public const ulong PlainsBackdropSalt = 0x504C41494E530001UL;

    // --- New salts reserved for the visual improvement package ---

    /// <summary>
    /// Weapon tint variant selection stream (weapon-visuals-design.md), scoped
    /// by <c>EntityId</c>. Chooses a tint within the weapon's variant family;
    /// never chooses the weapon identity itself, which comes only from
    /// <c>CombatLoadout</c>.
    /// </summary>
    public const ulong WeaponTintSalt = 0xB4A2C1F3D8E5A907UL;

    /// <summary>
    /// Weapon silhouette variant selection stream (weapon-visuals-design.md),
    /// scoped by <c>EntityId</c>. Chooses among the documented silhouette
    /// variants for the loadout's weapon role.
    /// </summary>
    public const ulong WeaponSilhouetteSalt = 0xC9F1E4A7B2D6503CUL;

    /// <summary>
    /// Shield skin variant selection stream (shield-visuals-design.md), scoped
    /// by <c>EntityId</c>. Chooses among the documented skins for the
    /// loadout's shield role.
    /// </summary>
    public const ulong ShieldSkinSalt = 0x6F3D8A1C4E9B7025UL;

    /// <summary>
    /// Warrior-appearance preset block assignment stream
    /// (warrior-appearance-design.md), scoped by <c>EntityId</c> and, where the
    /// roster design scopes preset blocks by faction, <c>FactionId</c>. Chooses
    /// which preset block a warrior draws from.
    /// </summary>
    public const ulong AppearanceBlockAssignmentSalt = 0x2A7E5C9B3F1D6480UL;

    /// <summary>
    /// Warrior-appearance preset selection stream
    /// (warrior-appearance-design.md), scoped by <c>EntityId</c>. Chooses the
    /// specific preset within the block assigned by
    /// <see cref="AppearanceBlockAssignmentSalt"/>.
    /// </summary>
    public const ulong AppearancePresetSelectionSalt = 0x91D4B6F2A8C3E057UL;

    /// <summary>
    /// Battlefield grass cluster placement and generation stream
    /// (battlefield-environment-design.md), scoped by <c>Scenario.Seed</c> and
    /// consumed sequentially rather than by modulo selection.
    /// </summary>
    public const ulong GrassGenerationSalt = 0x47B2F8D1A6C93E05UL;

    /// <summary>
    /// Ground corner-lattice shading stream (battlefield-environment-design.md),
    /// scoped by <c>Scenario.Seed</c>. A new salt per the planner's resolution
    /// of inconsistency 3 (implementation-plan-draft.md): corner-averaged
    /// shading changes the ground shades regardless, so reusing
    /// <see cref="PlainsBackdropSalt"/> would buy nothing and would break the
    /// one-salt-per-trait-stream rule. The existing decal placement stays
    /// pinned under <see cref="PlainsBackdropSalt"/> either way.
    /// </summary>
    public const ulong GroundCornerLatticeSalt = 0x5E8C1A4F9D3B7602UL;

    /// <summary>
    /// Warrior personal-name region assignment stream
    /// (2026-07-29-warrior-personal-names-design.md), scoped by
    /// <c>Scenario.Seed</c> and <c>FactionId</c>. Chooses which regional name
    /// corpus a whole faction draws from, so every warrior in one faction
    /// shares one regional grammar (the naming research's rule 1). Deliberately
    /// its own stream rather than a reuse of
    /// <see cref="AppearanceBlockAssignmentSalt"/>: the appearance roster has a
    /// Cagayan block for which the name research clears no corpus, and the name
    /// corpus has a Mindanao River region for which the appearance roster has no
    /// block, so the two assignments cannot be one shared draw.
    /// </summary>
    public const ulong WarriorNameRegionSalt = 0x3C7A9E15D2B84F60UL;

    /// <summary>
    /// Warrior personal-name selection stream
    /// (2026-07-29-warrior-personal-names-design.md), scoped by
    /// <c>EntityId</c>. Chooses the specific recorded form within the region
    /// assigned by <see cref="WarriorNameRegionSalt"/>.
    /// </summary>
    public const ulong WarriorNameSelectionSalt = 0xD6B03F82A5C1E794UL;

    /// <summary>
    /// Every registered salt, existing and new, in declaration order. The
    /// pairwise-distinctness test walks this list; nothing else should need to.
    /// </summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new(nameof(PawnBodySalt), PawnBodySalt,
            "Pawn stature, build, and head-treatment selection."),
        new(nameof(PawnClothingSalt), PawnClothingSalt,
            "Pawn clothing and accent color selection."),
        new(nameof(PawnDetailSalt), PawnDetailSalt,
            "Pawn skin and head-treatment color selection."),
        new(nameof(PlainsBackdropSalt), PlainsBackdropSalt,
            "Plains backdrop ground decal placement."),
        new(nameof(WeaponTintSalt), WeaponTintSalt,
            "Weapon tint variant selection."),
        new(nameof(WeaponSilhouetteSalt), WeaponSilhouetteSalt,
            "Weapon silhouette variant selection."),
        new(nameof(ShieldSkinSalt), ShieldSkinSalt,
            "Shield skin variant selection."),
        new(nameof(AppearanceBlockAssignmentSalt), AppearanceBlockAssignmentSalt,
            "Warrior-appearance preset block assignment."),
        new(nameof(AppearancePresetSelectionSalt), AppearancePresetSelectionSalt,
            "Warrior-appearance preset selection within the assigned block."),
        new(nameof(GrassGenerationSalt), GrassGenerationSalt,
            "Battlefield grass cluster placement and generation."),
        new(nameof(GroundCornerLatticeSalt), GroundCornerLatticeSalt,
            "Ground corner-lattice shading."),
        new(nameof(WarriorNameRegionSalt), WarriorNameRegionSalt,
            "Warrior personal-name regional corpus assignment."),
        new(nameof(WarriorNameSelectionSalt), WarriorNameSelectionSalt,
            "Warrior personal-name selection within the assigned region."),
    ];
}
