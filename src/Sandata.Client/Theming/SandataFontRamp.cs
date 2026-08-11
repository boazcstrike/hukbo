namespace Sandata.Client.Theming;

/// <summary>
/// The two named typography rungs Sandata's HUD actually draws at. Every
/// string drawn in the client is drawn at one of these roles, at a single
/// baked pixel size with no float resampling.
/// </summary>
internal enum SandataFontRole
{
    Body,
    Label,
}

/// <summary>
/// The single source of truth for Sandata's font ramp. Pure and static: no
/// MonoGame type appears in any signature here, so this class is fully
/// unit-testable without a graphics device. <see cref="SandataFontSet"/> is
/// the thin loader that turns these decisions into actual <c>SpriteFont</c>
/// instances.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a two-role ramp, not a copy of
/// <c>Hukbo.Client.Theming.UiFontRamp</c>'s six roles by four scale tiers.
/// Hukbo's ramp exists to serve a HUD with distinct caption, body, label,
/// subtitle, title, and display elements, each independently resizable for
/// accessibility. Sandata's HUD (<c>src/Sandata.Client/UI</c>) has exactly two
/// kinds of text today: fixed-height row content — <c>OperatorInspector</c>'s
/// eleven 18px lines, <c>OrderQueueView</c>'s 18px rows, <c>GoCodePanel</c>'s
/// 20px rows — and panel headers — <c>ContactList</c>'s 24px header,
/// <c>GoCodePanel</c>'s and <c>OrderQueueView</c>'s 20px headers. Every one of
/// those measured heights is either "one line of body text" or "one line of
/// header text," so <see cref="SandataFontRole.Body"/> and
/// <see cref="SandataFontRole.Label"/> are the two rungs that actually exist
/// to serve, not a speculative six. There is also no per-user scale policy in
/// Sandata yet — no <c>UiScale</c> equivalent — so unlike Hukbo's ramp this
/// one bakes exactly one physical tier per role rather than four. Adding a
/// third role or a scale axis is a real content-pipeline cost (a new
/// descriptor, a new <c>#begin</c> block, a new atlas to build every time);
/// this ramp adds neither until a HUD element actually needs one.
/// </para>
/// </remarks>
internal static class SandataFontRamp
{
    /// <summary>
    /// Every role, in ascending size order. Stable and total: safe to use as
    /// the definitive enumeration order for parity checks against the
    /// content project.
    /// </summary>
    public static IReadOnlyList<SandataFontRole> AllRoles { get; } =
    [
        SandataFontRole.Body,
        SandataFontRole.Label,
    ];

    /// <summary>
    /// The vendored TTF file every role's descriptor bakes from. Sandata
    /// carries one face — see <c>Content/Fonts/README.md</c> for why a second
    /// face is not warranted yet.
    /// </summary>
    public const string FontFileName = "Rajdhani-SemiBold.ttf";

    /// <summary>
    /// The content pipeline asset identifier for a role, matching the
    /// corresponding <c>#begin Fonts/Sandata*.spritefont</c> block in
    /// <c>Content/Content.mgcb</c>.
    /// </summary>
    public static string GetAssetId(SandataFontRole role) => role switch
    {
        SandataFontRole.Body => "Fonts/SandataBody",
        SandataFontRole.Label => "Fonts/SandataLabel",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>
    /// The pixel size the role's descriptor was baked at. Every draw of this
    /// role happens at scale 1.0 against this exact bake, so this is also the
    /// size the spectator actually sees. 14px matches
    /// <c>OperatorInspector.LineHeight</c> and <c>OrderQueueView.RowHeight</c>
    /// (both 18px, comfortable for a 14px face); 17px matches
    /// <c>ContactList.HeaderHeight</c> (24px) and
    /// <c>GoCodePanel</c>/<c>OrderQueueView</c>'s 20px header rows.
    /// </summary>
    public static int GetPixelSize(SandataFontRole role) => role switch
    {
        SandataFontRole.Body => 14,
        SandataFontRole.Label => 17,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>
    /// Parses a role name as it appears in shared configuration. The match is
    /// exact and case-sensitive against the enumeration member name, so a
    /// typo or an unknown role name is rejected rather than silently coerced.
    /// </summary>
    /// <exception cref="FormatException">
    /// <paramref name="roleName"/> does not name a member of
    /// <see cref="SandataFontRole"/>.
    /// </exception>
    public static SandataFontRole Parse(string roleName)
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
            $"'{roleName}' does not name a known {nameof(SandataFontRole)}.");
    }
}
