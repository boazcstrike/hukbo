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
    Rectangle ScrollbarTrackBounds);

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
