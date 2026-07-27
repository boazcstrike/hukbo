using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Theming;

/// <summary>
/// Holds one loaded <see cref="SpriteFont"/> per <see cref="UiFontRole"/>. A
/// thin loader, not a decision-maker: every decision about which asset a role
/// resolves to and what size it was baked at lives in <see cref="UiFontRamp"/>.
/// Storage is a fixed array indexed by the enum's numeric value rather than a
/// dictionary, so there is no hash iteration order anywhere on the draw path
/// and no allocation once <see cref="Load"/> has run.
/// </summary>
internal sealed class UiFontSet
{
    private readonly SpriteFont[] _fontsByRole;

    private UiFontSet(SpriteFont[] fontsByRole)
    {
        _fontsByRole = fontsByRole;
    }

    /// <summary>
    /// Loads every role's font using the supplied loader delegate, called
    /// once per role. In <c>ArenaGame.LoadContent</c> the delegate is
    /// <c>Content.Load&lt;SpriteFont&gt;</c>, so nothing outside this class
    /// needs to know a <c>ContentManager</c> exists.
    /// </summary>
    public static UiFontSet Load(Func<string, SpriteFont> load)
    {
        ArgumentNullException.ThrowIfNull(load);

        var fontsByRole = new SpriteFont[UiFontRamp.AllRoles.Count];
        foreach (var role in UiFontRamp.AllRoles)
        {
            fontsByRole[(int)role] = load(UiFontRamp.GetAssetId(role));
        }

        return new UiFontSet(fontsByRole);
    }

    /// <summary>
    /// The loaded font for the given role.
    /// </summary>
    public SpriteFont Get(UiFontRole role) => _fontsByRole[(int)role];
}
