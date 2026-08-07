using System.Collections.Immutable;
using Sandata.Core.Simulation;

namespace Sandata.Core.Combat;

/// <summary>
/// One source-to-target damage contribution proposed during a tick's hitscan
/// resolution (design section 5 stage 12 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md, "create hitscan fire
/// proposals; resolve line of sight, cover arc, and accuracy"), before any
/// accumulation or death resolution. Pure data — this type carries no
/// behaviour of its own, and nothing in <see cref="DamageResolution"/> ever
/// mutates one; every instance is consumed by value.
/// </summary>
/// <param name="SourceEntityId">
/// The operator whose shot produced this contribution. Carried for
/// provenance — a future event-emission stage can attribute a kill — but
/// <see cref="DamageResolution"/> itself never reads it; accumulation and
/// death only ever depend on <paramref name="TargetEntityId"/> and
/// <paramref name="Amount"/>.
/// </param>
/// <param name="TargetEntityId">
/// The operator this contribution is addressed to, matching
/// <see cref="OperatorState.EntityId"/>.
/// </param>
/// <param name="Amount">The raw damage this contribution deals. Never negative.</param>
public readonly record struct DamageInstance(ulong SourceEntityId, ulong TargetEntityId, int Amount);

/// <summary>
/// One target's total damage for the tick, after every
/// <see cref="DamageInstance"/> addressed to it has been summed —
/// <see cref="DamageResolution.Accumulate"/>'s output element.
/// </summary>
public readonly record struct AccumulatedDamage(ulong TargetEntityId, int TotalDamage);

/// <summary>
/// Design section 5 stage 13 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md: "apply accumulated
/// damage simultaneously; resolve death and mission outcome" (plan task 36).
/// Every member here is a pure function of its parameters — none mutates an
/// argument, and none reads or writes any wall-clock, filesystem, or shared
/// state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Simultaneity, and why it makes mutual kills land.</b> A tick's hitscan
/// stage can address several <see cref="DamageInstance"/> values at the same
/// target, from different shooters, and two operators can each address a
/// lethal contribution at the other in the same tick. <see cref="Accumulate"/>
/// sums every contribution addressed to a target before
/// <see cref="ApplyDamage"/> touches that target's health, and
/// <see cref="ApplyDamage"/> computes every operator's new health from the
/// tick's starting health, never from another operator's already-updated
/// value. Neither step ever asks whether a target is already dead before
/// finishing the sum or the subtraction, so a mutual kill is not a special
/// case: both operators simply end the tick with non-positive health, exactly
/// as a one-sided kill would.
/// </para>
/// <para>
/// <b>Order-independence is a property of addition, made structural rather
/// than incidental.</b> <see cref="Accumulate"/> does not fold
/// <paramref name="damageInstances"/> in the order the caller supplies them.
/// It sorts a working copy by <see cref="DamageInstance.TargetEntityId"/>
/// first — <c>Array.Sort</c>, never a <c>Dictionary</c> or a <c>HashSet</c>,
/// both banned from this project — and only then walks the sorted run
/// summing each contiguous same-target group. Sorting by key before summing
/// means the result cannot depend on the caller's array order: any
/// permutation of <paramref name="damageInstances"/> sorts to the same
/// grouping, and integer addition within a group is itself commutative and
/// associative, so the per-target total is identical either way. This is
/// deliberately stronger than "addition happens to commute" — a future
/// change that folded contributions without first normalising the order
/// would still be correct arithmetically, but this shape is what makes the
/// order-independence a property a test can pin, not an accident of how
/// the loop happened to be written.
/// </para>
/// <para>
/// <b>No downed state, no bleeding, no revive.</b> An operator's only
/// health-derived states are alive (<see cref="IsAlive"/> true) and dead
/// (<see cref="IsAlive"/> false); nothing in this type computes a third
/// value from health, and nothing here ever raises a dead operator's health
/// back above zero. Death is therefore instant and final the moment a
/// tick's accumulated damage takes an operator's health to zero or below —
/// there is no tick in between where a lethally wounded operator is anything
/// other than dead.
/// </para>
/// <para>
/// <b>Deaths resolve in ascending entity id, after every death is known.</b>
/// <see cref="ResolveDeaths"/> is called only once <see cref="ApplyDamage"/>
/// has produced the tick's final, fully accumulated health for every
/// operator — never against an intermediate value reached partway through
/// summing. It then reports every operator that crossed from alive to dead
/// this tick sorted ascending by entity id, which is the order any later
/// stage (event emission, mission-outcome resolution) processes them in, so
/// which death happens to be discovered first while scanning never decides
/// an outcome.
/// </para>
/// </remarks>
public static class DamageResolution
{
    /// <summary>
    /// Whether a health value represents a living operator. The only two
    /// health-derived states Sandata's combat model recognises are alive
    /// (<paramref name="health"/> strictly greater than zero) and dead (zero
    /// or less) — design section 5 stage 13's "instant death with no downed
    /// state" leaves nothing in between, and every other member of this type
    /// classifies health through this one method rather than repeating the
    /// comparison.
    /// </summary>
    public static bool IsAlive(int health) => health > 0;

    /// <summary>
    /// Sums every <see cref="DamageInstance"/> addressed to the same target
    /// into one <see cref="AccumulatedDamage"/> entry, independent of the
    /// order <paramref name="damageInstances"/> arrives in — see this type's
    /// remarks for why sorting by target before summing makes that a
    /// structural property rather than an incidental one. A target with no
    /// addressed damage produces no entry. Returned ascending by
    /// <see cref="AccumulatedDamage.TargetEntityId"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any <see cref="DamageInstance.Amount"/> is negative — this stage only
    /// ever removes health, never restores it.
    /// </exception>
    public static ImmutableArray<AccumulatedDamage> Accumulate(
        ImmutableArray<DamageInstance> damageInstances)
    {
        if (damageInstances.IsDefaultOrEmpty)
        {
            return ImmutableArray<AccumulatedDamage>.Empty;
        }

        var sorted = damageInstances.ToArray();
        Array.Sort(sorted, static (left, right) => left.TargetEntityId.CompareTo(right.TargetEntityId));

        var builder = ImmutableArray.CreateBuilder<AccumulatedDamage>();
        var index = 0;
        while (index < sorted.Length)
        {
            var targetEntityId = sorted[index].TargetEntityId;
            checked
            {
                long total = 0;
                while (index < sorted.Length && sorted[index].TargetEntityId == targetEntityId)
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(sorted[index].Amount);
                    total += sorted[index].Amount;
                    index++;
                }

                var clampedTotal = (int)Math.Clamp(total, int.MinValue, int.MaxValue);
                builder.Add(new AccumulatedDamage(targetEntityId, clampedTotal));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Applies every accumulated per-target damage total to the matching
    /// operator's <see cref="OperatorState.Health"/>, returning a new array
    /// in the same order as <paramref name="operators"/> — the input array
    /// and every element in it are left untouched. An operator with no
    /// addressed damage passes through unchanged (the same instance, not a
    /// copy). Health is allowed to fall below zero; nothing beyond
    /// <see cref="IsAlive"/>'s zero boundary treats a more negative value
    /// differently from a less negative one.
    /// </summary>
    public static ImmutableArray<OperatorState> ApplyDamage(
        ImmutableArray<OperatorState> operators,
        ImmutableArray<DamageInstance> damageInstances)
    {
        if (operators.IsDefaultOrEmpty)
        {
            return operators;
        }

        var accumulated = Accumulate(damageInstances);
        if (accumulated.IsEmpty)
        {
            return operators;
        }

        var builder = ImmutableArray.CreateBuilder<OperatorState>(operators.Length);
        foreach (var operatorState in operators)
        {
            var totalDamage = 0;
            foreach (var entry in accumulated)
            {
                if (entry.TargetEntityId == operatorState.EntityId)
                {
                    totalDamage = entry.TotalDamage;
                    break;
                }
            }

            if (totalDamage == 0)
            {
                builder.Add(operatorState);
                continue;
            }

            checked
            {
                var newHealth = Math.Clamp((long)operatorState.Health - totalDamage, int.MinValue, int.MaxValue);
                builder.Add(operatorState with { Health = (int)newHealth });
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// The entity ids that crossed from alive to dead between
    /// <paramref name="beforeDamage"/> and <paramref name="afterDamage"/>,
    /// sorted ascending. Call this only once <paramref name="afterDamage"/>
    /// is the tick's fully accumulated result — the output of
    /// <see cref="ApplyDamage"/> — not an intermediate value: two operators
    /// that mutually kill each other on the same tick were both alive in
    /// <paramref name="beforeDamage"/> and are both dead in
    /// <paramref name="afterDamage"/>, so both appear here, regardless of
    /// which one this method happens to examine first while scanning.
    /// </summary>
    /// <param name="beforeDamage">
    /// Every operator's state at the start of the tick, before
    /// <see cref="ApplyDamage"/> ran.
    /// </param>
    /// <param name="afterDamage">
    /// The same operators after <see cref="ApplyDamage"/> ran — the array it
    /// returned.
    /// </param>
    public static ImmutableArray<ulong> ResolveDeaths(
        ImmutableArray<OperatorState> beforeDamage,
        ImmutableArray<OperatorState> afterDamage)
    {
        if (afterDamage.IsDefaultOrEmpty)
        {
            return ImmutableArray<ulong>.Empty;
        }

        var newlyDead = new List<ulong>();
        foreach (var after in afterDamage)
        {
            if (IsAlive(after.Health))
            {
                continue;
            }

            foreach (var before in beforeDamage)
            {
                if (before.EntityId != after.EntityId)
                {
                    continue;
                }

                if (IsAlive(before.Health))
                {
                    newlyDead.Add(after.EntityId);
                }

                break;
            }
        }

        newlyDead.Sort();
        return newlyDead.ToImmutableArray();
    }
}
