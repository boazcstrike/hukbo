using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Determinism;

internal static class StateHasher
{
    internal static ulong Compute(
        Scenario scenario,
        long tick,
        BattleOutcome outcome,
        long eventSequence,
        IReadOnlyList<AgentState> agents)
    {
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
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
        Add(ref hash, (int)scenario.CombatPreset);
        Add(ref hash, rules.ContentHash);
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
