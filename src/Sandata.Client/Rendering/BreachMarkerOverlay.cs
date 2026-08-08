using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Sandata.Core.Maps;

namespace Sandata.Client.Rendering;

/// <summary>
/// Pure geometry for breach-point markers on map-declared breachable walls
/// (design section 12: "<see cref="WallRecord.Material"/> is 0 glass, 1
/// solid, 2 partition, 3 breachable"; section 11's HUD element list:
/// "Breach-point marker... map-declared breachable wall faces", scaffolded —
/// "drawn, not interactive"). Same pure-helper shape as
/// <see cref="WorldRenderer"/>: no <c>GraphicsDevice</c>, no
/// <c>SpriteBatch</c>, no window.
/// </summary>
/// <remarks>
/// Like <see cref="FireConeOverlay"/> and <see cref="OrderPathOverlay"/>,
/// this is tactical decision geometry and takes no detail-tier parameter: a
/// breach point renders at every detail tier, never fading with zoom the way
/// a decorative operator layer does.
/// </remarks>
internal static class BreachMarkerOverlay
{
    /// <summary><see cref="WallRecord.Material"/>'s breachable value.</summary>
    internal const int BreachableMaterial = 3;

    /// <summary>
    /// World-unit half-width of the square <see cref="WorldRenderer.DrawShape"/>
    /// each marker becomes, centered on the wall segment's midpoint. Larger
    /// than <see cref="WorldRenderer.WallThicknessWu"/> so the marker reads
    /// as its own icon rather than disappearing inside the wall's own drawn
    /// thickness.
    /// </summary>
    internal const float BreachMarkerRadiusWu = 12f;

    /// <summary>
    /// Filters <paramref name="records"/> to every <see cref="WallRecord"/>
    /// whose <see cref="WallRecord.Material"/> equals
    /// <see cref="BreachableMaterial"/> and returns one square marker shape
    /// per wall, centered on that wall segment's midpoint. Order matches the
    /// walls' order in <paramref name="records"/>. A map with no breachable
    /// wall produces no markers.
    /// </summary>
    internal static ImmutableArray<WorldRenderer.DrawShape> CreateWorldShapes(ImmutableArray<MapRecord> records)
    {
        var shapes = ImmutableArray.CreateBuilder<WorldRenderer.DrawShape>();
        foreach (var wall in records.OfType<WallRecord>().Where(wall => wall.Material == BreachableMaterial))
        {
            var midpointX = (wall.X1 + wall.X2) / 2f;
            var midpointY = (wall.Y1 + wall.Y2) / 2f;
            shapes.Add(WorldRenderer.DrawShape.FromAxisAligned(new Rectangle(
                (int)MathF.Round(midpointX - BreachMarkerRadiusWu),
                (int)MathF.Round(midpointY - BreachMarkerRadiusWu),
                (int)MathF.Round(BreachMarkerRadiusWu * 2f),
                (int)MathF.Round(BreachMarkerRadiusWu * 2f))));
        }

        return shapes.ToImmutable();
    }
}
