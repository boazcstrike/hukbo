namespace Sandata.Client.Audio;

/// <summary>
/// The acoustic environment a gunshot or mechanism sound was recorded for.
/// Design section 10 names five real environments; <see cref="None"/> is the
/// sentinel added for a row where environment does not apply at all — a
/// dry-fire click, an impact, a casing landing, or a mechanism action carry
/// no environment tail and render the literal token <c>none</c> in that
/// filename position, exactly as design section 10's worked examples for
/// those families show.
/// </summary>
/// <remarks>
/// Append-only, matching the numbering convention every other Sandata enum
/// in this design follows.
/// </remarks>
internal enum SoundEnvironment
{
    /// <summary>Not applicable to this family; renders as the literal token <c>none</c>.</summary>
    None = 0,

    /// <summary>Close range, no reverberant tail.</summary>
    CloseDry = 1,

    /// <summary>Indoors, with a short reflective tail.</summary>
    IndoorTail = 2,

    /// <summary>Outdoors, with a longer open-air tail.</summary>
    OutdoorTail = 3,

    /// <summary>Heard from a distance, attenuated.</summary>
    Distant = 4,

    /// <summary>Fired through a suppressor.</summary>
    Suppressed = 5,
}
