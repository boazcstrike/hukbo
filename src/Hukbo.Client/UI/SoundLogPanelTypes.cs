using Hukbo.Client.Audio;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Every region of the sound log panel, computed by
/// <c>SoundLogPanel.CalculateLayout</c>.
/// </summary>
internal readonly record struct SoundLogPanelLayout(
    Rectangle HeaderBounds,
    Rectangle MuteBounds,
    Rectangle PathBounds,
    Rectangle BindingsBounds,
    Rectangle BindingRowsBounds,
    Rectangle CueListBounds,
    Rectangle CueRowsBounds,
    Rectangle ScrollbarTrackBounds,
    Rectangle BindingScrollbarTrackBounds);

/// <summary>
/// Which of the sound log's two scrollable lists a wheel notch moves. The panel
/// routes the wheel to the list under the pointer, and falls back to the cue
/// log everywhere else inside the panel so a notch is never swallowed. Cues is
/// first so that the default value is the fallback.
/// </summary>
internal enum SoundLogScrollTarget
{
    /// <summary>
    /// The cue log at the bottom of the panel. Also the fallback for the
    /// header, the path line, and the mute button.
    /// </summary>
    Cues,

    /// <summary>
    /// The expected-files list in the middle of the panel.
    /// </summary>
    Bindings,
}

/// <summary>
/// One flattened row of the "EXPECTED FILES" section: either a slot's own
/// header row or, for a hit-location-driven slot, one indented sub-row per
/// acoustic class. Built by <c>SoundLogPanel.BuildBindingRows</c> so
/// <c>DrawBindings</c> only has to paint what has already been decided.
/// </summary>
internal readonly record struct SoundBindingRow(
    string Label,
    string StatusText,
    SoundBindingStatus Status);
