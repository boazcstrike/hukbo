using System.Collections.Immutable;
using Sandata.Core.Simulation;

namespace Sandata.Core.Combat;

/// <summary>
/// Design section 5 stage 13 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md: "resolve death and
/// mission outcome" — the mission-outcome half of that stage. A mission ends
/// as soon as one faction has no living operator left, mirroring
/// <c>Hukbo.Core.Simulation.BattleSimulation.ResolveOutcome</c>'s own
/// elimination rule for the tactical battle simulation.
/// </summary>
/// <remarks>
/// <b>Call this only after every death for the tick has been resolved.</b>
/// <see cref="Resolve"/> must be given the operator roster after
/// <see cref="DamageResolution.ApplyDamage"/> has finished — never an
/// intermediate array reached partway through a tick's damage accumulation.
/// Evaluating it earlier could observe one faction's kill before another
/// faction's mutual kill has landed, which would decide a mission's winner
/// from incidental processing order rather than from the tick's true, final
/// state. This is exactly the property design section 5 stage 13 requires
/// when it lists "resolve death and mission outcome" as one stage, in that
/// order.
/// </remarks>
public static class OutcomeRules
{
    /// <summary>
    /// Determines the mission outcome from a tick's fully damage-resolved
    /// operator roster. An operator counts toward its faction's living
    /// presence when <see cref="DamageResolution.IsAlive"/> is true for its
    /// <see cref="OperatorState.Health"/>; every operator's
    /// <see cref="OperatorState.Faction"/> is <c>0</c> or <c>1</c> per
    /// <c>Simulation.MissionState</c>'s own constraint.
    /// </summary>
    /// <param name="operators">
    /// The tick's operator roster after damage has been applied — the array
    /// <see cref="DamageResolution.ApplyDamage"/> returned, not an
    /// intermediate value. An empty or default roster resolves to
    /// <see cref="MissionOutcome.Draw"/>: neither faction has a living
    /// operator, because neither has any operator at all.
    /// </param>
    public static MissionOutcome Resolve(ImmutableArray<OperatorState> operators)
    {
        var faction0Alive = false;
        var faction1Alive = false;

        if (!operators.IsDefaultOrEmpty)
        {
            foreach (var operatorState in operators)
            {
                if (!DamageResolution.IsAlive(operatorState.Health))
                {
                    continue;
                }

                if (operatorState.Faction == 0)
                {
                    faction0Alive = true;
                }
                else
                {
                    faction1Alive = true;
                }
            }
        }

        return (faction0Alive, faction1Alive) switch
        {
            (true, true) => MissionOutcome.Ongoing,
            (true, false) => MissionOutcome.Faction0Victory,
            (false, true) => MissionOutcome.Faction1Victory,
            (false, false) => MissionOutcome.Draw,
        };
    }
}
