using Hukbo.Core.Simulation;

namespace Hukbo.Core.Determinism;

internal static class StateHasher
{
    /// <summary>
    /// Folds one authoritative state into the deterministic state hash.
    /// </summary>
    /// <param name="scenario">The scenario under simulation.</param>
    /// <param name="tick">The authoritative tick.</param>
    /// <param name="outcome">The battle outcome so far.</param>
    /// <param name="eventSequence">The running event sequence number.</param>
    /// <param name="agents">The agent states, in their storage order.</param>
    /// <param name="contentHash">
    /// The content hash of the ruleset the simulation is actually running on.
    /// Passed by value rather than fetched from the registry so that a
    /// simulation given a ruleset directly folds that ruleset's hash instead of
    /// the registered one, and so that a recorded hash can be reproduced
    /// exactly after the registered value has moved on.
    /// </param>
    /// <param name="hasRankLevels">
    /// Whether the ruleset the simulation is running on declares per-rank
    /// fighter levels, mirroring <see cref="Combat.CombatRuleset.HasRankLevels"/>.
    /// Passed by value for the same reason <paramref name="contentHash"/> is:
    /// a preset that declares none (V1 through V3) folds nothing for
    /// <see cref="AgentState.Rank"/> — not even a constant — which is what
    /// keeps its state hash exactly where it was. Every warrior such a
    /// preset fields resolves to the same <c>RankId.Timawa</c> default in any
    /// case, so folding it unconditionally would only ever hash a constant.
    /// </param>
    /// <param name="movementContentHash">
    /// The <see cref="Movement.MovementRuleset.ContentHash"/> of the movement
    /// preset the simulation is running on, or <see langword="null"/> for
    /// every preset whose
    /// <see cref="Movement.MovementRuleset.UsesEquipmentRelativeFootwork"/>
    /// is <see langword="false"/> (V1 through V5). When <see langword="null"/>
    /// the fold is byte-for-byte the legacy layout — nothing new is written
    /// anywhere, following the <paramref name="hasRankLevels"/> precedent.
    /// When non-null, two additions apply: the movement content hash folds
    /// immediately after <paramref name="contentHash"/> in the scenario
    /// section, and the five equipment-relative footwork fields fold at the
    /// tail of each per-agent fold, after the conditional
    /// <see cref="AgentState.Rank"/> fold, in their <see cref="AgentState"/>
    /// declaration order: <see cref="AgentState.Facing"/> (whose
    /// <see cref="Movement.Facing16.None"/> folds as its numeric 255),
    /// <see cref="AgentState.MovementPaceRaw"/>,
    /// <see cref="AgentState.TacticalPosture"/>,
    /// <see cref="AgentState.FootworkPhase"/>, and
    /// <see cref="AgentState.FootworkTicksRemaining"/>.
    /// </param>
    /// <param name="appliesPressureInterrupt">
    /// The <see cref="Movement.MovementRuleset.AppliesPressureInterrupt"/> of
    /// the movement preset the simulation is running on,
    /// <see langword="false"/> for every preset up to and including V6. It is
    /// deliberately a gate of its own rather than a reuse of
    /// <paramref name="movementContentHash"/>: that value is already non-null
    /// under V6, so folding the pressure-interrupt fields inside its block
    /// would move V6's per-agent byte layout and break the frozen V6 digest.
    /// When <see langword="false"/> nothing new is written anywhere, following
    /// the <paramref name="hasRankLevels"/> and
    /// <paramref name="movementContentHash"/> precedent. When
    /// <see langword="true"/>, three fields fold at the tail of each per-agent
    /// fold, after the conditional footwork fields, in their
    /// <see cref="AgentState"/> declaration order:
    /// <see cref="AgentState.DamageTakenLastTick"/>,
    /// <see cref="AgentState.PriorSupportAllies"/>, and
    /// <see cref="AgentState.BrokeOffUnderPressure"/> as <c>1</c> or <c>0</c>.
    /// </param>
    internal static ulong Compute(
        Scenario scenario,
        long tick,
        BattleOutcome outcome,
        long eventSequence,
        IReadOnlyList<AgentState> agents,
        ulong contentHash,
        bool hasRankLevels,
        ulong? movementContentHash = null,
        bool appliesPressureInterrupt = false)
    {
        var hash = Fnv1a.OffsetBasis;
        Add(ref hash, scenario.Seed);
        Add(ref hash, scenario.MapWidth);
        Add(ref hash, scenario.MapHeight);
        Add(ref hash, scenario.AgentsPerFaction);
        Add(ref hash, scenario.TickRate);
        Add(ref hash, scenario.TickLimit);
        Add(ref hash, scenario.MaximumHitPoints);
        Add(ref hash, scenario.DamagePerAttack);
        Add(ref hash, scenario.AttackRangeRaw);
        Add(ref hash, scenario.PerceptionRangeRaw);
        Add(ref hash, scenario.MovementSpeedRaw);
        Add(ref hash, scenario.AttackCooldownTicks);
        Add(ref hash, scenario.BodyRadiusRaw);
        Add(ref hash, (int)scenario.CollisionPolicy);
        Add(ref hash, scenario.LastStandThresholdAgents);
        Add(ref hash, (int)scenario.CombatPreset);
        Add(ref hash, (int)scenario.MovementPreset);
        Add(ref hash, contentHash);
        if (movementContentHash is { } movementHash)
        {
            Add(ref hash, movementHash);
        }

        Add(ref hash, tick);
        Add(ref hash, (int)outcome);
        Add(ref hash, eventSequence);
        Add(ref hash, agents.Count);

        foreach (var agent in agents)
        {
            Add(ref hash, agent.EntityId);
            Add(ref hash, agent.FactionId);
            Add(ref hash, agent.XRaw);
            Add(ref hash, agent.YRaw);
            Add(ref hash, agent.HitPoints);
            Add(ref hash, agent.MaximumHitPoints);
            Add(ref hash, agent.MovementSpeedRaw);
            Add(ref hash, agent.PerceptionRangeRaw);
            Add(ref hash, agent.AttackRangeRaw);
            Add(ref hash, agent.DamagePerAttack);
            Add(ref hash, agent.AttackCooldownTicks);
            Add(ref hash, agent.AttackCooldownRemaining);
            Add(ref hash, agent.TargetEntityId ?? 0);
            Add(ref hash, (int)agent.Intent);
            Add(ref hash, (int)agent.MovementResolution);
            Add(ref hash, (int)agent.Loadout.Weapon);
            Add(ref hash, (int)agent.Loadout.Armor);
            Add(ref hash, (int)agent.Loadout.Shield);
            Add(ref hash, agent.Level);
            Add(ref hash, agent.ComboStepsRemaining);
            Add(ref hash, agent.ComboTargetEntityId ?? 0);
            Add(ref hash, agent.ContingentId);
            Add(ref hash, (int)agent.ContingentState);

            if (hasRankLevels)
            {
                Add(ref hash, (int)agent.Rank);
            }

            if (movementContentHash is not null)
            {
                Add(ref hash, (int)agent.Facing);
                Add(ref hash, agent.MovementPaceRaw);
                Add(ref hash, (int)agent.TacticalPosture);
                Add(ref hash, (int)agent.FootworkPhase);
                Add(ref hash, agent.FootworkTicksRemaining);
            }

            if (appliesPressureInterrupt)
            {
                Add(ref hash, agent.DamageTakenLastTick);
                Add(ref hash, agent.PriorSupportAllies);
                Add(ref hash, agent.BrokeOffUnderPressure ? 1 : 0);
            }
        }

        return hash;
    }

    private static void Add(ref ulong hash, int value) =>
        Fnv1a.Add(ref hash, unchecked((ulong)(uint)value));

    private static void Add(ref ulong hash, long value) =>
        Fnv1a.Add(ref hash, unchecked((ulong)value));

    private static void Add(ref ulong hash, ulong value) =>
        Fnv1a.Add(ref hash, value);
}
