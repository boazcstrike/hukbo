using Hukbo.Core.Simulation;

namespace Hukbo.Core.Determinism;

internal static class StateHasher
{
    private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
    private const ulong Prime = 1_099_511_628_211UL;

    internal static ulong Compute(
        Scenario scenario,
        long tick,
        BattleOutcome outcome,
        long eventSequence,
        IReadOnlyList<AgentState> agents)
    {
        var hash = OffsetBasis;
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
        }

        return hash;
    }

    private static void Add(ref ulong hash, int value) =>
        Add(ref hash, unchecked((ulong)(uint)value));

    private static void Add(ref ulong hash, long value) =>
        Add(ref hash, unchecked((ulong)value));

    private static void Add(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash = unchecked(hash * Prime);
        }
    }
}
