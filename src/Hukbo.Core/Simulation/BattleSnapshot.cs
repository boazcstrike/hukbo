namespace Hukbo.Core.Simulation;

/// <param name="Projectiles">
/// The live projectile pool, in array order, at this tick. Authoritative
/// state rather than derived scratch or a cache, so it is snapshotted and
/// folded into <see cref="StateHash"/> exactly as <see cref="Agents"/> is —
/// see <see cref="Scenario.MaximumProjectilesInFlight"/>. Empty for any
/// combat preset that fields no ranged weapon, which never launches one.
/// </param>
public sealed record BattleSnapshot(
    long Tick,
    BattleOutcome Outcome,
    IReadOnlyList<AgentView> Agents,
    IReadOnlyList<BattleEvent> Events,
    ulong StateHash,
    IReadOnlyList<Projectile> Projectiles);
