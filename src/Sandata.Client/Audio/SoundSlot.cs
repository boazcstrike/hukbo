namespace Sandata.Client.Audio;

/// <summary>
/// One declared row of the Sandata sound catalog. Design section 10:
/// <c>SandataSoundCatalog</c> holds a <c>static readonly SoundSlot[]</c> as
/// the whole catalog, resolved through a flat lookup index rather than a
/// dictionary, because the four key fields below
/// (<see cref="Family"/>, <see cref="FamilyKey"/>, <see cref="Mode"/>,
/// <see cref="Environment"/>) are all small bounded ordinals.
/// </summary>
/// <param name="Family">Which of the eight sound families this row belongs to.</param>
/// <param name="FamilyKey">
/// The ordinal of the axis <paramref name="Family"/> uses to select a report
/// or surface sample: <c>Sandata.Core.Weapons.CaliberFamily</c> for
/// <see cref="SoundFamily.GunReport"/>, <see cref="SoundFamily.GunLoop"/>,
/// <see cref="SoundFamily.GunTail"/>, and <see cref="SoundFamily.Dry"/>;
/// <c>Sandata.Core.Weapons.MechanismGroup</c> for
/// <see cref="SoundFamily.Mechanism"/>; <see cref="ImpactSurface"/> for
/// <see cref="SoundFamily.Impact"/>; <c>Sandata.Core.Weapons.WeaponClass</c>
/// for <see cref="SoundFamily.Casing"/>.
/// </param>
/// <param name="Mode">
/// The generic third naming axis. On a gun-report, gun-loop, or gun-tail row
/// this is the actual firing mode. On a mechanism row it is the mechanism
/// action (<see cref="FireMode.Selector"/>, <see cref="FireMode.MagazineOut"/>,
/// <see cref="FireMode.MagazineIn"/>, <see cref="FireMode.BoltRack"/>). On a
/// casing row it is the landing surface (<see cref="FireMode.GroundConcrete"/>,
/// <see cref="FireMode.GroundDirt"/>). On a dry-fire or impact row it is
/// <see cref="FireMode.None"/>.
/// </param>
/// <param name="Environment">The acoustic environment, or <see cref="SoundEnvironment.None"/> when not applicable.</param>
/// <param name="VariantCount">How many numbered takes this row has, 1 to 99.</param>
/// <param name="TailTicks">
/// How long a played instance of this row should hold its pool reservation,
/// in ticks. Provisional: design section 10 requires this measured against a
/// real hardware run, through a hand-run harness under <c>tools/</c>, before
/// it stops being provisional. Every value declared for it in
/// <c>SandataSoundCatalog</c> is a placeholder proportional to the family's
/// expected sound length, not a measurement.
/// </param>
internal readonly record struct SoundSlot(
    SoundFamily Family,
    int FamilyKey,
    FireMode Mode,
    SoundEnvironment Environment,
    byte VariantCount,
    int TailTicks);

/// <summary>
/// The generic third positional axis in the
/// <c>&lt;family&gt;-&lt;key&gt;-&lt;mode&gt;-&lt;environment&gt;-&lt;NN&gt;.wav</c>
/// name design section 10 specifies. The name is taken directly from that
/// section's own <c>SoundSlot</c> record.
/// </summary>
/// <remarks>
/// <b>Disagreement worth recording:</b> design section 10 comments this field
/// "<c>FireMode Mode, // None for non-gun families</c>", but its own worked
/// examples do not hold to that comment — <c>mech-ar-selector-none-01.wav</c>
/// and <c>casing-rifle-concrete-none-03.wav</c> both put a real, non-"none"
/// token in the mode position for a non-gun family (a mechanism action and a
/// casing landing surface, respectively). This enum is implemented to match
/// the nine worked examples exactly, which is the task's literal test bar,
/// rather than the inline comment; it therefore carries members beyond
/// literal firing modes. A future task that revisits the catalog shape may
/// want to split this into per-family types instead.
/// </remarks>
internal enum FireMode
{
    /// <summary>Not applicable; renders as the literal token <c>none</c>.</summary>
    None = 0,

    /// <summary>Semi-automatic fire: one round per trigger pull.</summary>
    Single = 1,

    /// <summary>A mechanically baked two-round burst.</summary>
    Burst2 = 2,

    /// <summary>A mechanically baked three-round burst.</summary>
    Burst3 = 3,

    /// <summary>Fully automatic fire.</summary>
    Auto = 4,

    /// <summary>A mechanism's selector or safety detent.</summary>
    Selector = 5,

    /// <summary>A magazine leaving the weapon.</summary>
    MagazineOut = 6,

    /// <summary>A magazine seating into the weapon.</summary>
    MagazineIn = 7,

    /// <summary>A bolt or charging handle racking.</summary>
    BoltRack = 8,

    /// <summary>A spent casing landing on concrete.</summary>
    GroundConcrete = 9,

    /// <summary>A spent casing landing on dirt.</summary>
    GroundDirt = 10,
}

/// <summary>
/// The bullet-impact surface axis, used as <see cref="SoundSlot.FamilyKey"/>
/// for <see cref="SoundFamily.Impact"/> rows. Five surfaces, matching
/// docs/research/2026-08-07-sandata-research-consolidated.md section 6's
/// impact row.
/// </summary>
internal enum ImpactSurface
{
    /// <summary>A concrete or masonry surface.</summary>
    Concrete = 0,

    /// <summary>A metal surface.</summary>
    Metal = 1,

    /// <summary>A wood surface.</summary>
    Wood = 2,

    /// <summary>Flesh.</summary>
    Flesh = 3,

    /// <summary>A ricochet off a hard surface.</summary>
    Ricochet = 4,
}
