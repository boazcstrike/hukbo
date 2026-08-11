using Microsoft.Xna.Framework.Graphics;

namespace Sandata.Client.Theming;

/// <summary>
/// Holds one loaded <see cref="SpriteFont"/> per <see cref="SandataFontRole"/>.
/// A thin loader, not a decision-maker: every decision about which asset a
/// role resolves to and what size it was baked at lives in
/// <see cref="SandataFontRamp"/>. Storage is a fixed array indexed by the
/// enum's numeric value rather than a dictionary, so there is no hash
/// iteration order anywhere on the draw path and no allocation once
/// <see cref="Load"/> has run.
/// </summary>
internal sealed class SandataFontSet
{
    private readonly SpriteFont[] _fontsByRole;

    private SandataFontSet(SpriteFont[] fontsByRole)
    {
        _fontsByRole = fontsByRole;
    }

    /// <summary>
    /// Loads every role's font using the supplied loader delegate, called
    /// once per role. In <c>SandataGame.LoadContent</c> the delegate is
    /// <c>Content.Load&lt;SpriteFont&gt;</c>, so nothing outside this class
    /// needs to know a <c>ContentManager</c> exists, and this constructor
    /// never touches a <c>GraphicsDevice</c> itself — a stub
    /// <c>Func&lt;string, SpriteFont&gt;</c> is enough to exercise it in a
    /// test.
    /// </summary>
    public static SandataFontSet Load(Func<string, SpriteFont> load)
    {
        ArgumentNullException.ThrowIfNull(load);

        var fonts = new SpriteFont[SandataFontRamp.AllRoles.Count];
        foreach (var role in SandataFontRamp.AllRoles)
        {
            fonts[(int)role] = load(SandataFontRamp.GetAssetId(role));
        }

        return new SandataFontSet(fonts);
    }

    /// <summary>The loaded font for the given role.</summary>
    public SpriteFont Get(SandataFontRole role) => _fontsByRole[(int)role];
}
