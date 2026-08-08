namespace Sandata.Core.Orders;

/// <summary>
/// Pure evaluation of design section 16's <see cref="OrderKind.GoCodeRelease"/>
/// primitive: "A go-code assigns a letter to waypoints across several
/// operators, and releasing that letter is itself an order — a
/// <c>GoCodeRelease</c> with its own <c>TargetTick</c> and
/// <c>OrderSequence</c>. A keypress therefore enters the same queue as
/// everything else and gets the same determinism guarantee for free. No
/// separate input path reaches the simulation."
/// </summary>
/// <remarks>
/// <para>
/// <b>The letter itself is not this file's concern.</b> <see cref="OrderKind"/>'s
/// own remarks on <see cref="OrderKind.GoCodeRelease"/> record why: "no
/// separate letter payload is required for the order to function, so none is
/// declared here" — <see cref="Order.Addressees"/> already names exactly the
/// operators tied to the released code. Mapping a player-facing letter to an
/// addressee set is authoring-time work (a future client-side task, not
/// listed among tasks 57 through 63), so it never crosses into
/// <c>Sandata.Core</c> at all. What this file provides is the one thing that
/// does cross the boundary once a <see cref="OrderKind.GoCodeRelease"/> order
/// has been applied: whether a given operator is one of the ones it
/// releases.
/// </para>
/// <para>
/// <b>No frozen tick-start view type exists yet.</b> Exactly as
/// <see cref="SyncRules"/> states in its own remarks, task 49's tick
/// pipeline has not been built in this worktree, so <see cref="ReleasesOperator"/>
/// is a pure function of caller-supplied parameters and is expected to have
/// no production caller yet.
/// </para>
/// <para>
/// <b>Indistinguishability at the queue boundary is not this file's job to
/// enforce — it already holds structurally.</b> <see cref="OrderQueue.SubmitValidated"/>
/// is "the only public entry into <c>OrderQueue</c>" (per this task's brief;
/// <see cref="OrderQueue"/>'s own unvalidated storage primitive is
/// <see langword="private"/>), it applies the same validation and the
/// same dense counter assignment regardless of <see cref="OrderKind"/>, and
/// <see cref="OrderQueue.InApplicationOrder"/> sorts purely on
/// <c>(TargetTick, OrderSequence)</c> — a comparison that never reads
/// <see cref="Order.Kind"/> at all (see <see cref="OrderQueue.CompareApplicationOrder"/>).
/// A <see cref="OrderKind.GoCodeRelease"/> order therefore already sorts,
/// stores, and applies identically to a <see cref="OrderKind.Hold"/> order
/// with the same schedule, with no code in this file needed to make that
/// true. This file does not duplicate that proof; <c>GoCodeRulesTests</c>
/// asserts it directly against <see cref="OrderQueue"/> and <see cref="Order"/>,
/// the same types both of them already are.
/// </para>
/// </remarks>
public static class GoCodeRules
{
    /// <summary>
    /// Whether <paramref name="order"/>, once applied, releases
    /// <paramref name="entityId"/> — i.e., whether that operator is one of
    /// <paramref name="order"/>'s <see cref="Order.Addressees"/>. A future
    /// mover (task 49's tick pipeline) calls this once per operator whose
    /// go-code-held state it is updating this tick, after stage 1 has
    /// applied the order.
    /// </summary>
    /// <param name="order">
    /// A <see cref="OrderKind.GoCodeRelease"/> order that has been applied
    /// this tick.
    /// </param>
    /// <param name="entityId">The operator being tested.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="entityId"/> appears in
    /// <paramref name="order"/>'s <see cref="Order.Addressees"/>;
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="order"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="order"/>'s <see cref="Order.Kind"/> is not
    /// <see cref="OrderKind.GoCodeRelease"/>. Named narrowly to this one
    /// kind rather than accepting any <see cref="Order"/> so a caller that
    /// passes the wrong order by mistake fails loudly at the call site
    /// instead of silently answering a question about the wrong kind of
    /// order.
    /// </exception>
    /// <remarks>
    /// Binary search over <see cref="Order.Addressees"/>, which
    /// <see cref="OrderQueue"/> always stores ascending — "so the set has one written form" (design
    /// section 16) — rather than a linear scan or a
    /// <c>Dictionary&lt;</c>/<c>HashSet&lt;</c> lookup, both banned from
    /// <c>Sandata.Core</c> by <c>SandataSourceHygieneTests</c>. The same
    /// technique <see cref="MovementSource.SlotTargetingRoster"/>'s private
    /// <c>IsAssigned</c> helper already uses for the equivalent ascending-array
    /// membership test.
    /// </remarks>
    public static bool ReleasesOperator(Order order, ulong entityId)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Kind != OrderKind.GoCodeRelease)
        {
            throw new ArgumentException(
                $"Expected an {nameof(OrderKind.GoCodeRelease)} order, got {order.Kind}.", nameof(order));
        }

        var addressees = order.Addressees;
        var low = 0;
        var high = addressees.Length - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidate = addressees[mid];

            if (candidate == entityId)
            {
                return true;
            }

            if (candidate < entityId)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return false;
    }
}
