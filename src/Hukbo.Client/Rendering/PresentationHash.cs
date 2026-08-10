namespace Hukbo.Client.Rendering;

/// <summary>
/// The one integer mixer every presentation-layer visual derives its stable
/// per-instance variation from.
/// </summary>
/// <remarks>
/// <para>
/// Presentation variation must be stable — the same hit must draw the same way
/// every frame it is on screen, and the same battle replayed must look the
/// same — which rules out any live random source, and <c>System.Random</c> is
/// banned outright. Mixing the identifiers an effect already holds gives
/// variation that is a pure function of them.
/// </para>
/// <para>
/// This is the finalizer <c>BloodGeometry</c> has always used for its burst
/// seeds, lifted here when a second consumer appeared rather than copied. A
/// second copy of a mixer is worse than it looks: the two drift, and nothing
/// fails when they do — the pictures merely stop agreeing for reasons no test
/// is watching.
/// </para>
/// <para>
/// Nothing here reaches the simulation. This never touches a state hash, an
/// event hash, a snapshot, or an outcome, and it is not the deterministic RNG
/// — that is <c>Hukbo.Shared.Core/Determinism/SplitMix64.cs</c>, which the
/// simulation owns and which this file must never be mistaken for.
/// </para>
/// </remarks>
internal static class PresentationHash
{
    public static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
