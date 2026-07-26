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
