using Hukbo.Client.Settings;

namespace Hukbo.Client.Theming;

/// <summary>
/// The six named typography rungs. Every string drawn in the client is drawn
/// at one of these roles, at scale 1.0 against a bake taken at that role's own
/// pixel size. There is no float resampling anywhere in the presentation
/// layer once every panel has migrated onto this enumeration.
/// </summary>
internal enum UiFontRole
{
    Caption,
    Body,
    Label,
    Subtitle,
    Title,
    Display,
}

/// <summary>
/// The single source of truth for the size ramp described in
/// <c>docs/plans/2026-07-27-font-text-quality-design.md</c>, section 4. Pure
/// and static: no MonoGame type appears in any signature here, so this class
/// is fully unit-testable without a graphics device. <see cref="UiFontSet"/>
/// is the thin loader that turns these decisions into actual
/// <c>SpriteFont</c> instances.
/// </summary>
internal static class UiFontRamp
{
    /// <summary>
    /// A conservative per-pixel multiplier used to derive
    /// <see cref="GetApproximateAdvancePx"/>. Rajdhani and Bebas Neue are both
    /// condensed faces, so the true average advance width is narrower than
    /// this; the multiplier is deliberately generous so that a caller using
    /// the estimate to decide how many characters fit in a budget never
    /// under-clips a string that would in fact have fit.
    /// </summary>
    private const float ConservativeAdvanceFactor = 0.65f;

    /// <summary>
    /// Every role, in ascending size order. Stable and total: safe to use as
    /// the definitive enumeration order for parity checks against the
    /// content project.
    /// </summary>
    public static IReadOnlyList<UiFontRole> AllRoles { get; } =
    [
        UiFontRole.Caption,
        UiFontRole.Body,
        UiFontRole.Label,
        UiFontRole.Subtitle,
        UiFontRole.Title,
        UiFontRole.Display,
    ];

    /// <summary>
    /// Every physical font tier compiled into the content project. Auto is a
    /// policy value and therefore never appears here.
    /// </summary>
    public static IReadOnlyList<UiScale> AllScales { get; } =
    [
        UiScale.Percent100,
        UiScale.Percent125,
        UiScale.Percent150,
        UiScale.Percent200,
    ];

    /// <summary>
    /// The content pipeline asset identifier for a role, matching the
    /// corresponding <c>#begin Fonts/Ui*.spritefont</c> block in
    /// <c>Content/Content.mgcb</c>.
    /// </summary>
    public static string GetAssetId(UiFontRole role) => role switch
    {
        UiFontRole.Caption => "Fonts/UiCaption",
        UiFontRole.Body => "Fonts/UiBody",
        UiFontRole.Label => "Fonts/UiLabel",
        UiFontRole.Subtitle => "Fonts/UiSubtitle",
        UiFontRole.Title => "Fonts/UiTitle",
        UiFontRole.Display => "Fonts/UiDisplay",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>
    /// Returns the content id for a role at a resolved physical tier. The
    /// baseline ids stay unchanged for backward compatibility; larger tiers
    /// use a numeric suffix and are independently rasterized by MGCB.
    /// </summary>
    public static string GetAssetId(UiFontRole role, UiScale scale)
    {
        var baseline = GetAssetId(role);
        return scale == UiScale.Percent100
            ? baseline
            : $"{baseline}{UiScalePolicy.GetPercent(scale)}";
    }

    /// <summary>
    /// The pixel size the role's descriptor was baked at. Every draw of this
    /// role happens at scale 1.0 against this exact bake, so this is also the
    /// size the spectator actually sees.
    /// </summary>
    public static int GetPixelSize(UiFontRole role) => role switch
    {
        UiFontRole.Caption => 12,
        UiFontRole.Body => 14,
        UiFontRole.Label => 17,
        UiFontRole.Subtitle => 20,
        UiFontRole.Title => 22,
        UiFontRole.Display => 38,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static int GetPixelSize(UiFontRole role, UiScale scale) =>
        (int)Math.Round(
            GetPixelSize(role) * (UiScalePolicy.GetPercent(scale) / 100d),
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// A conservative, over-estimated per-character advance width in pixels
    /// for the role. Callers that need to guess how many characters fit in a
    /// horizontal budget without measuring the actual string should use this
    /// value rather than a hardcoded divisor, so a future size change moves
    /// every budget at once instead of silently clipping. Deliberately biased
    /// wide: undershooting the available width is a wasted pixel, but
    /// overshooting it is a dropped or truncated line of evidence.
    /// </summary>
    public static int GetApproximateAdvancePx(UiFontRole role) =>
        (int)MathF.Ceiling(GetPixelSize(role) * ConservativeAdvanceFactor);

    public static int GetApproximateAdvancePx(
        UiFontRole role,
        UiScale scale) =>
        (int)MathF.Ceiling(
            GetPixelSize(role, scale) * ConservativeAdvanceFactor);

    /// <summary>
    /// Parses a role name as it appears in shared configuration (for example
    /// the theme JSON's slot-to-role map). The match is exact and
    /// case-sensitive against the enumeration member name, so a typo or an
    /// unknown role name is rejected rather than silently coerced.
    /// </summary>
    /// <exception cref="FormatException">
    /// <paramref name="roleName"/> does not name a member of
    /// <see cref="UiFontRole"/>.
    /// </exception>
    public static UiFontRole Parse(string roleName)
    {
        ArgumentNullException.ThrowIfNull(roleName);

        foreach (var role in AllRoles)
        {
            if (string.Equals(role.ToString(), roleName, StringComparison.Ordinal))
            {
                return role;
            }
        }

        throw new FormatException(
            $"'{roleName}' does not name a known {nameof(UiFontRole)}.");
    }
}
