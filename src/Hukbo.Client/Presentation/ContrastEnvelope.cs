using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Shared legibility machinery: a pure channel-distance metric over
/// <see cref="Color"/> values and the named contrast-envelope bounds every
/// catalog validation checks against. Consumed by weapon tints (R-W1.7),
/// shield tones (R-W2.8), and the dye-palette faction-distance rule
/// (R-W3.8, R-W6.10). This class is machinery only — it does not itself
/// validate any shipped palette; that is the catalog validation pass
/// (VIS-006) and the workstream tasks that follow it. Pure and allocation
/// free, so it is fully unit tested with no graphics device.
/// </summary>
internal static class ContrastEnvelope
{
    /// <summary>
    /// PROVISIONAL. Minimum channel distance an equipment tone (weapon tint,
    /// shield tone) must keep from every ground shade so the silhouette
    /// stays legible against the backdrop, whose interpolation toward
    /// <c>ArenaBorder</c> is itself bounded at the 0.22 ceiling
    /// (<c>PlainsBackdropGeometry.MaximumBackdropInterpolation</c>)
    /// (R-W1.7, R-W2.8).
    /// </summary>
    internal const float MinimumGroundDistance = 60f;

    /// <summary>
    /// PROVISIONAL. Minimum channel distance an equipment tone must keep
    /// from every pawn clothing color, so a weapon or shield tone never
    /// merges into the torso it is drawn beside (R-W1.7, R-W2.8).
    /// </summary>
    internal const float MinimumClothingDistance = 60f;

    /// <summary>
    /// PROVISIONAL. Minimum channel distance a garment dye constant must
    /// keep from every fixed faction constant, so no garment can read as a
    /// competing faction signal (R-W3.8, R-W6.10).
    /// </summary>
    internal const float MinimumFactionDyeDistance = 80f;

    /// <summary>
    /// Channel-distance metric over two colors: plain Euclidean distance
    /// across the red, green, and blue channels. Alpha is excluded — every
    /// consumer of this helper is fully opaque. Pure and allocation free.
    /// </summary>
    internal static float ColorDistance(Color a, Color b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;
        return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    /// <summary>
    /// True when <paramref name="candidate"/> keeps at least
    /// <paramref name="minimumDistance"/> of channel distance from every
    /// color in <paramref name="reference"/>. The shape catalog validation
    /// (VIS-006) calls this once per candidate tone against a ground-shade
    /// set, a clothing-color set, or the fixed faction constants, using the
    /// matching named bound above. Pure and allocation free.
    /// </summary>
    internal static bool IsWithinEnvelope(
        Color candidate,
        ReadOnlySpan<Color> reference,
        float minimumDistance)
    {
        foreach (var color in reference)
        {
            if (ColorDistance(candidate, color) < minimumDistance)
            {
                return false;
            }
        }

        return true;
    }
}
