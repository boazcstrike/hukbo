using Hukbo.Client.Settings;
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
    private readonly SpriteFont[,] _fontsByScaleAndRole;
    private UiScale _activeScale = UiScale.Percent100;

    private UiFontSet(SpriteFont[,] fontsByScaleAndRole)
    {
        _fontsByScaleAndRole = fontsByScaleAndRole;
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

        var fonts = new SpriteFont[
            UiFontRamp.AllScales.Count,
            UiFontRamp.AllRoles.Count];
        for (var scaleIndex = 0;
             scaleIndex < UiFontRamp.AllScales.Count;
             scaleIndex++)
        {
            var scale = UiFontRamp.AllScales[scaleIndex];
            foreach (var role in UiFontRamp.AllRoles)
            {
                fonts[scaleIndex, (int)role] = load(
                    UiFontRamp.GetAssetId(role, scale));
            }
        }

        return new UiFontSet(fonts);
    }

    /// <summary>
    /// The loaded font for the given role.
    /// </summary>
    public SpriteFont Get(UiFontRole role) =>
        _fontsByScaleAndRole[GetScaleIndex(_activeScale), (int)role];

    public UiScale ActiveScale => _activeScale;

    public void SelectScale(UiScale configured, int width, int height)
    {
        _activeScale = UiScalePolicy.Resolve(configured, width, height);
        UiScaleContext.Set(_activeScale);
    }

    public int GetApproximateAdvancePx(UiFontRole role) =>
        UiFontRamp.GetApproximateAdvancePx(role, _activeScale);

    private static int GetScaleIndex(UiScale scale)
    {
        for (var index = 0; index < UiFontRamp.AllScales.Count; index++)
        {
            if (UiFontRamp.AllScales[index] == scale)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(scale), scale, null);
    }
}
