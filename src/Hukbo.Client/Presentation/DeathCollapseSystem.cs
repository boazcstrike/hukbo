using Hukbo.Client.Rendering;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One fallen warrior's collapse: when its fall began and which way it went.
/// </summary>
/// <param name="EntityId">
/// The warrior this entry describes. Stored beside the entry and compared on
/// every read, because the entry is addressed by roster ordinal rather than by
/// identity — the same validity rule <see cref="PawnAppearanceCache"/> uses,
/// for the same reason.
/// </param>
/// <param name="FinalRotationRadians">
/// The angle the body comes to rest at, resolved once when the collapse
/// registered and never recomputed. A corpse that changed which way it was
/// lying between frames would be worse than one that never fell.
/// </param>
/// <param name="AgeSeconds">
/// Seconds of advanced presentation time since the collapse began.
/// </param>
internal readonly record struct DeathCollapse(
    ulong EntityId,
    float FinalRotationRadians,
    float AgeSeconds)
{
    /// <summary>
    /// The rotation this body is drawn at right now.
    /// </summary>
    public float RotationRadians =>
        CollapsePose.Resolve(AgeSeconds, FinalRotationRadians);
}

/// <summary>
/// The per-warrior death-collapse clocks (the 2026-08-14 death-collapse design,
/// section 7). Presentation only: it never changes a position, a health value,
/// an intent, or anything that reaches a hash, and it never reads the wall
/// clock.
/// </summary>
/// <remarks>
/// <para>
/// Storage is an array indexed by the agent's ordinal position in
/// <c>BattleSimulation.Agents</c>, not a dictionary and not a scanned list.
/// That roster is a fixed-size array refilled element for element every tick;
/// death clears <c>IsAlive</c> in place and never removes, compacts, or
/// reorders an entry, so ordinal <c>i</c> names the same warrior for the whole
/// battle. The alternative — a linear scan per pawn per frame — is a million
/// comparisons a frame at a thousand units, which is the same argument that
/// produced <c>HitEffectSystem.BuildPulseLookup</c>.
/// </para>
/// <para>
/// Unlike every ageing system beside it, nothing here is ever evicted. A
/// corpse persists for the rest of the battle by design, so the store's size is
/// the roster's size and its only clearing point is
/// <see cref="PresentationCoordinator.ResetFor"/>.
/// </para>
/// </remarks>
internal sealed class DeathCollapseSystem
{
    private DeathCollapse[] _collapses;
    private bool[] _registered;

    /// <param name="capacity">
    /// The largest roster this store will be asked about. Growth is handled
    /// rather than thrown on, because the roster size is a scenario input and
    /// the coordinator is constructed before a scenario exists.
    /// </param>
    public DeathCollapseSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _collapses = new DeathCollapse[capacity];
        _registered = new bool[capacity];
    }

    /// <summary>
    /// Registers a collapse for every agent that has just become a corpse —
    /// not alive, no longer inside its lethal hold, and not registered
    /// already. Idempotent per agent: a second call never restarts a fall.
    /// </summary>
    /// <remarks>
    /// The lethal-hold test is what makes the collapse start at the right
    /// moment. An agent's <c>IsAlive</c> goes false on the tick the simulation
    /// killed it, which is up to a third of a second before the kill has
    /// finished reading on screen; starting the fall there would consume the
    /// window the lethal-blow legibility work bought.
    /// <c>PawnVisualStateResolver</c> already draws exactly this line, and this
    /// method deliberately draws the same one rather than a second one that
    /// could drift from it.
    /// </remarks>
    /// <param name="agents">
    /// The roster, indexed by the same ordinal the store is indexed by.
    /// </param>
    /// <param name="defenderReactions">
    /// Where the lethal hold and the killing blow's screen direction are read
    /// from. A lethal reaction outlives its own hold — 0.50s against 0.34s —
    /// so the direction is still present at the exact frame a collapse
    /// registers.
    /// </param>
    public void Observe(
        IReadOnlyList<AgentView> agents,
        DefenderReactionSystem defenderReactions)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(defenderReactions);

        EnsureCapacity(agents.Count);

        for (var ordinal = 0; ordinal < agents.Count; ordinal++)
        {
            var agent = agents[ordinal];
            if (_registered[ordinal] &&
                _collapses[ordinal].EntityId == agent.EntityId)
            {
                continue;
            }

            if (agent.IsAlive ||
                defenderReactions.IsLethalHoldActive(agent.EntityId))
            {
                continue;
            }

            _registered[ordinal] = true;
            _collapses[ordinal] = new DeathCollapse(
                agent.EntityId,
                CollapsePose.ResolveFinalRotation(
                    ResolveFallsRight(agent, defenderReactions),
                    agent.EntityId),
                AgeSeconds: 0f);
        }
    }

    /// <summary>
    /// Ages every registered collapse. Called from the same
    /// <c>advanceContacts</c> group the lethal hold is advanced in, so a
    /// spectator who pauses mid-fall sees a body held mid-fall.
    /// </summary>
    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        for (var ordinal = 0; ordinal < _registered.Length; ordinal++)
        {
            if (!_registered[ordinal])
            {
                continue;
            }

            var collapse = _collapses[ordinal];

            // Stops accumulating once the curve has nothing left to say. The
            // pose is already exactly the final angle past this point, so the
            // only thing a growing age would buy is a float that eventually
            // loses precision over a long battle.
            if (collapse.AgeSeconds >= CollapsePose.CollapseSeconds)
            {
                continue;
            }

            _collapses[ordinal] = collapse with
            {
                AgeSeconds = collapse.AgeSeconds + elapsedSeconds,
            };
        }
    }

    /// <summary>
    /// The collapse registered for one agent, or <c>false</c> when it has none
    /// — which is every living agent, and every dead one still inside its
    /// lethal hold.
    /// </summary>
    /// <param name="ordinal">The agent's position in the roster.</param>
    /// <param name="entityId">
    /// The agent's identity, compared against the stored one. A mismatch is a
    /// miss rather than a wrong answer, so a store carried across a roster it
    /// no longer describes can only fail to find a body, never invent one.
    /// </param>
    public bool TryGetCollapse(
        int ordinal,
        ulong entityId,
        out DeathCollapse collapse)
    {
        if (ordinal < 0 ||
            ordinal >= _registered.Length ||
            !_registered[ordinal] ||
            _collapses[ordinal].EntityId != entityId)
        {
            collapse = default;
            return false;
        }

        collapse = _collapses[ordinal];
        return true;
    }

    /// <summary>
    /// The rotation one agent's body is drawn at, or zero when it has no
    /// collapse. The one call the pawn draw loop makes.
    /// </summary>
    public float ResolveRotationRadians(int ordinal, ulong entityId) =>
        TryGetCollapse(ordinal, entityId, out var collapse)
            ? collapse.RotationRadians
            : 0f;

    public void Clear()
    {
        Array.Clear(_collapses);
        Array.Clear(_registered);
    }

    /// <summary>
    /// Which way the killing blow pushed this body across the screen. A warrior
    /// struck from its left falls to its right, because
    /// <c>DefenderReaction.DirectionX</c> points from the attacker toward the
    /// defender.
    /// </summary>
    /// <remarks>
    /// The fallback covers two cases that are not errors: a death with no
    /// surviving reaction at all, and a blow that arrived straight up or down
    /// the screen, where the horizontal component says nothing. Both fall back
    /// to the entity id's low bit, which splits the field evenly and is stable
    /// for one warrior across runs.
    /// </remarks>
    private static bool ResolveFallsRight(
        AgentView agent,
        DefenderReactionSystem defenderReactions)
    {
        if (defenderReactions.TryGetReaction(agent.EntityId, out var reaction) &&
            reaction.DirectionX != 0f)
        {
            return reaction.DirectionX > 0f;
        }

        return (agent.EntityId & 1UL) == 0UL;
    }

    private void EnsureCapacity(int count)
    {
        if (count <= _registered.Length)
        {
            return;
        }

        Array.Resize(ref _collapses, count);
        Array.Resize(ref _registered, count);
    }
}
