namespace Sandata.Core.Sensing;

/// <summary>
/// The sound kinds design section 4 and the research consolidation name for
/// Sandata's hearing model: "sound is a parallel sense with published radii".
/// </summary>
public enum SoundKind
{
    /// <summary>Cutting a padlock or chain open. Published radius 4.5 m.</summary>
    BoltCutter = 0,

    /// <summary>A deployed smoke canister. Published radius 10 m.</summary>
    Smoke = 1,

    /// <summary>A hammer or crowbar forcing a door or lock. Published radius 12 m.</summary>
    HammerOrCrowbar = 2,

    /// <summary>A shotgun round used to breach a door. Published radius 25 m.</summary>
    BreacherShotgun = 3,

    /// <summary>A firearm discharged at a target, outside the breaching context above.</summary>
    Gunfire = 4,

    /// <summary>A window or other glass pane broken. Louder than <see cref="Gunfire"/>.</summary>
    BreakingGlass = 5,

    /// <summary>An operator's death cry, which "propagates and pulls investigators".</summary>
    DeathScream = 6,
}

/// <summary>
/// Sound as Sandata's second sense, per design section 4 and the research
/// consolidation's "Vision, contact tiers, and sound as a second sense":
/// "sound is a parallel sense with published radii: bolt cutters 4.5 m, smoke
/// 10 m, hammer and crowbar 12 m, breacher shotgun 25 m. Breaking glass is
/// louder than gunfire. Death screams propagate and pull investigators."
/// </summary>
/// <remarks>
/// <para>
/// <b>Which radii are documented and which are provisional.</b>
/// <see cref="BoltCutterRadiusWu"/>, <see cref="SmokeRadiusWu"/>,
/// <see cref="HammerOrCrowbarRadiusWu"/>, and
/// <see cref="BreacherShotgunRadiusWu"/> are the four figures the design and
/// research documents publish by name, converted at the design's own pinned
/// 16 wu-per-metre scale (design section 4's metre-to-world-unit table
/// confirms two of these conversions directly: "the 4.5 m bolt-cutter sound
/// radius is 72 wu, the 25 m breacher-shotgun radius is 400 wu"). Neither
/// document gives a numeric radius for <see cref="GunfireRadiusWu"/>,
/// <see cref="BreakingGlassRadiusWu"/>, or <see cref="DeathScreamRadiusWu"/> —
/// only the qualitative rules "breaking glass is louder than gunfire" and
/// "death screams propagate". Those three are <b>provisional reconstructions</b>,
/// following the same convention <c>SandataRuleset</c> uses for its own
/// unmeasured tuning constants: round metre figures on the same 16 wu-per-metre
/// scale, chosen only to satisfy the ordering the design states
/// (<see cref="BreakingGlassRadiusWu"/> &gt; <see cref="GunfireRadiusWu"/>,
/// and <see cref="DeathScreamRadiusWu"/> loudest of the three since a death
/// scream is the sound explicitly tasked with pulling in investigators from
/// the widest area) rather than measured from any published source. Whichever
/// future task wires sound into the tick pipeline and can observe actual
/// firefights is expected to confirm or revise these three.
/// </para>
/// <para>
/// <b>No square root, ever.</b> Every comparison here is a squared-distance
/// comparison against a squared radius, per <c>CLAUDE.md</c> section 4 and
/// design section 4's ban on <c>Math.Sqrt</c> in <c>Sandata.Core</c>.
/// </para>
/// </remarks>
public static class HearingRules
{
    /// <summary>4.5 m. Design section 4: "the 4.5 m bolt-cutter sound radius is 72 wu." Documented.</summary>
    public const int BoltCutterRadiusWu = 72;

    /// <summary>10 m, at the design's pinned 16 wu-per-metre scale. Documented (research consolidation).</summary>
    public const int SmokeRadiusWu = 160;

    /// <summary>12 m, at the design's pinned 16 wu-per-metre scale. Documented (research consolidation).</summary>
    public const int HammerOrCrowbarRadiusWu = 192;

    /// <summary>25 m. Design section 4: "the 25 m breacher-shotgun radius is 400 wu." Documented.</summary>
    public const int BreacherShotgunRadiusWu = 400;

    /// <summary>
    /// 22 m. <b>Provisional reconstruction</b> — neither the design nor the
    /// research consolidation publishes a gunfire radius; this type's remarks
    /// explain the choice. Deliberately distinct from
    /// <see cref="BreacherShotgunRadiusWu"/> even though a breacher shotgun
    /// round is itself a gunshot, so the two can be tuned independently once
    /// a future task measures either.
    /// </summary>
    public const int GunfireRadiusWu = 352;

    /// <summary>
    /// 26 m. <b>Provisional reconstruction</b>, chosen only to satisfy design
    /// section 4's explicit "breaking glass is louder than gunfire" —
    /// see <see cref="GunfireRadiusWu"/>. This type's remarks explain the choice.
    /// </summary>
    public const int BreakingGlassRadiusWu = 416;

    /// <summary>
    /// 30 m. <b>Provisional reconstruction</b> — the loudest sound this model
    /// recognises, matching design section 4's "death screams propagate and
    /// pull investigators" from the widest area of the sounds named. This
    /// type's remarks explain the choice.
    /// </summary>
    public const int DeathScreamRadiusWu = 480;

    /// <summary>
    /// The published or provisional radius, in world units, a sound of
    /// <paramref name="kind"/> can be heard from.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is not one of the named <see cref="SoundKind"/> members.
    /// </exception>
    public static int RadiusWu(SoundKind kind) => kind switch
    {
        SoundKind.BoltCutter => BoltCutterRadiusWu,
        SoundKind.Smoke => SmokeRadiusWu,
        SoundKind.HammerOrCrowbar => HammerOrCrowbarRadiusWu,
        SoundKind.BreacherShotgun => BreacherShotgunRadiusWu,
        SoundKind.Gunfire => GunfireRadiusWu,
        SoundKind.BreakingGlass => BreakingGlassRadiusWu,
        SoundKind.DeathScream => DeathScreamRadiusWu,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognised sound kind."),
    };

    /// <summary>
    /// <see cref="RadiusWu"/> squared, as a <see langword="long"/> so the
    /// comparison in <see cref="IsHeard"/> never risks overflowing an
    /// <see langword="int"/> at the largest published radius.
    /// </summary>
    public static long RadiusSquaredWu(SoundKind kind)
    {
        var radius = RadiusWu(kind);
        return checked((long)radius * radius);
    }

    /// <summary>
    /// Whether a sound of <paramref name="kind"/>, made at the origin, is
    /// heard at a point <paramref name="dx"/>, <paramref name="dy"/> world
    /// units away. The boundary is inclusive: a listener at exactly the
    /// published radius hears it.
    /// </summary>
    public static bool IsHeard(SoundKind kind, long dx, long dy)
    {
        checked
        {
            var rangeSquared = (dx * dx) + (dy * dy);
            return rangeSquared <= RadiusSquaredWu(kind);
        }
    }
}
