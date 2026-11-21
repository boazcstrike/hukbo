using Hukbo.Core.Simulation;

namespace Hukbo.Client.UI;

/// <summary>
/// Which agents the damage card's hover spotlight rings. One predicate, shared
/// by the draw pass and the render probe's quad count, so the probe counts the
/// rings the renderer draws.
/// </summary>
internal static class DamageTallySpotlight
{
    /// <summary>
    /// True when <paramref name="hover"/> names a row and
    /// <paramref name="agent"/> is a living warrior of that row's faction,
    /// weapon, and shield. A fallen warrior is never ringed: the ring answers
    /// "where are they?" and a corpse is not still fighting.
    /// </summary>
    public static bool Matches(DamageTallyHover? hover, AgentView agent) =>
        hover is { } row &&
        agent.IsAlive &&
        agent.FactionId == row.FactionId &&
        agent.Loadout.Weapon == row.Weapon &&
        agent.Loadout.Shield == row.Shield;
}
