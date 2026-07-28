namespace Hukbo.Core.Simulation;

/// <summary>
/// One tick's attack-resolution counters split by the faction of the
/// <b>attacker</b>, so a caller can report what each side did rather than only
/// what happened overall.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="CombatMetrics"/> itself, these counters are <b>derived</b>
/// observability data. They are never hashed, never snapshotted, and never
/// persisted, so they cannot influence a simulation outcome. Adding this type
/// moved no recorded state hash or event hash, and
/// <c>CombatMetrics_ReachesNeitherHash</c> is the standing guard for that.
/// </para>
/// <para>
/// Each side is a full <see cref="CombatMetrics"/> rather than a bespoke
/// counter set, because every field of that type — including
/// <see cref="CombatMetrics.DefenceAttributableShare"/> — is meaningful at any
/// level of aggregation, which is exactly what its own documentation says.
/// A faction's share therefore reads correctly without any further arithmetic.
/// </para>
/// <para>
/// This type carries <b>one tick</b>. It is deliberately not accumulated inside
/// the simulation: a run total is the caller's business, and holding one here
/// would add mutable run-scoped state to <see cref="BattleSimulation"/> whose
/// only purpose is observability. A per-tick value the caller sums is not
/// simulation state; a running total living in the simulation would be.
/// </para>
/// <para>
/// Two same-seed runs of the same build must produce identical values in every
/// field.
/// </para>
/// </remarks>
/// <param name="Faction0">
/// Counters for attacks made by agents of faction 0.
/// </param>
/// <param name="Faction1">
/// Counters for attacks made by agents of faction 1.
/// </param>
public readonly record struct FactionCombatMetrics(
    CombatMetrics Faction0,
    CombatMetrics Faction1)
{
    /// <summary>
    /// The two sides added together, which must equal the undivided
    /// <see cref="BattleSimulation.LastTickCombat"/> for the same tick.
    /// </summary>
    /// <remarks>
    /// Every accepted attack has exactly one attacker and therefore exactly one
    /// attacking faction, so the split is a partition and the sum is total. A
    /// test asserts this against the simulation on every tick of a seeded run;
    /// that is what makes the split trustworthy rather than merely plausible.
    /// The arithmetic is <c>checked</c> so a total wide enough to overflow fails
    /// loudly instead of silently wrapping.
    /// </remarks>
    public CombatMetrics Total
    {
        get
        {
            checked
            {
                return new CombatMetrics(
                    Faction0.AcceptedAttacks + Faction1.AcceptedAttacks,
                    Faction0.LandedAttacks + Faction1.LandedAttacks,
                    Faction0.ShieldBlockedAttacks + Faction1.ShieldBlockedAttacks,
                    Faction0.ParriedAttacks + Faction1.ParriedAttacks,
                    Faction0.DeflectedAttacks + Faction1.DeflectedAttacks,
                    Faction0.EvadedAttacks + Faction1.EvadedAttacks);
            }
        }
    }

    /// <summary>
    /// The counters for one faction by its numeric identifier.
    /// </summary>
    /// <param name="factionId">0 or 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factionId"/> is neither 0 nor 1. The simulation has
    /// exactly two factions, so any other value is a caller defect rather than
    /// an empty result.
    /// </exception>
    public CombatMetrics ForFaction(int factionId) => factionId switch
    {
        0 => Faction0,
        1 => Faction1,
        _ => throw new ArgumentOutOfRangeException(nameof(factionId)),
    };
}
