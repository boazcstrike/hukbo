namespace Hukbo.Client.Presentation;

/// <summary>
/// Presentation-only procedural attack families. These names describe Hukbo's
/// provisional choreography, not documented historical techniques.
/// </summary>
internal enum AttackMotionFamily
{
    /// <summary>A broad, committed two-hand cleaving motion.</summary>
    CommittedCleaver = 0,

    /// <summary>A two-hand chop whose visible acceleration leads with the head.</summary>
    HeadWeightedChop = 1,

    /// <summary>A direct one-hand thrust with a restrained recovery cut.</summary>
    LinearThrustCut = 2,

    /// <summary>A compact one-hand chop or slash with a quick return.</summary>
    CompactChopSlash = 3,

    /// <summary>A one-hand overhand hurl that releases the weapon as a projectile.</summary>
    OverhandThrow = 4,

    /// <summary>A two-hand bow draw ending in a string release.</summary>
    DrawAndRelease = 5,

    /// <summary>A braced, near-static two-hand firearm level and discharge.</summary>
    BracedDischarge = 6,
}
