namespace Sandata.Core.Sensing;

/// <summary>
/// The alert-level transition rule design section 5's 2026-08-07 amendment
/// ("the alert level had no transition rule") states and plan task 68 wires
/// into a testable, dependency-free type: "<c>Calm</c> becomes <c>Raised</c>
/// when any living operator of that faction gains an identified contact, or
/// hears a sound of a kind that carries intent — gunfire, breaking glass, or
/// a death scream — whose radius reaches it. <c>Raised</c> becomes
/// <c>Breach</c> when any operator of that faction dies, or a breachable wall
/// on that faction's side is breached. The level never decreases within a
/// mission."
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here is a pure function of its parameters.</b> Task 35
/// declared <see cref="AlertLevel"/> and implemented no transition on
/// purpose, because no task row and no acceptance criterion named one at the
/// time; the design amendment supplies that rule now, and this type is the
/// implementation, not a second invention of the rule. <see cref="AlertLevel"/>
/// and <c>Sandata.Core.Simulation.MissionState</c> are both read-only inputs
/// to the task that commissioned this file — the raw <c>int</c>
/// <c>FactionAlertState.AlertLevel</c> field and its per-faction storage are
/// unchanged, and no new hashed field is declared anywhere in this file.
/// </para>
/// <para>
/// <b>The frozen-view seam, modelled as parameters.</b> Design section 5's
/// tick table places this evaluation in stage 5 ("sensing... tick-start
/// view"), against the tick-start view, but says the result is
/// <em>committed</em> only after that view is released at the end of stage 9
/// — so stage 8's intent selection reads the previous tick's level, not the
/// one this tick just computed. The type that name — <c>TickStartView</c> —
/// would belong to does not exist yet; it is task 49's, a later wave, and the
/// task that commissioned this file was explicit that inventing it here would
/// be reaching outside this task's remit. The seam is modelled the same way
/// every other sensing primitive in this codebase already models a view it
/// cannot yet reference — <see cref="ContactObservation"/> takes "is this
/// visible right now" as a plain <see langword="bool"/> parameter rather than
/// a view type, and <see cref="ContactMemory.ObserveOrRemember{T}"/> takes
/// "is this observed this tick" the same way. <see cref="Evaluate"/> and
/// <see cref="EvaluateFaction"/> both take the previous tick's committed level
/// as an explicit <see langword="in"/>-by-value parameter and this tick's
/// frozen-view-derived facts as further explicit parameters, and return the
/// level to commit — never a level they read from or write to any shared,
/// mutable, or ambient location. Nothing in this type stores a "current"
/// level for a caller to read back mid-tick: the only way a computed level
/// could re-enter the same evaluation is if this type held mutable state
/// linking one call to the next, and it holds none —
/// <see cref="AlertRulesTests"/> asserts that absence directly, by reflection,
/// rather than merely by convention.
/// </para>
/// <para>
/// <b>Which target each trigger raises to, and why it is not "one step
/// up" from wherever the level happens to be.</b> The design's own prose
/// narrates the ladder in two adjacent steps — Calm to Raised, then Raised to
/// Breach — because that is the order the two rules are listed in, not
/// because a friendly death is defined to matter only once a faction has
/// already noticed something else. A friendly death or a breached wall is,
/// on its own terms, a <see cref="AlertLevel.Breach"/>-tier event: design
/// section 4 already treats <see cref="AlertLevel.Breach"/> as "open
/// engagement... an unambiguous sign of combat," and a faction that loses an
/// operator without ever separately identifying a contact or hearing a
/// covered sound has still had an unambiguous sign of combat happen to it.
/// Reading the design's two rules as "the death/breach trigger only fires
/// once a faction is already <see cref="AlertLevel.Raised"/>" would leave a
/// faction that suffers a silent, unheralded loss stuck at
/// <see cref="AlertLevel.Calm"/> forever — never decreasing is the documented
/// invariant, but never rising past a level a genuinely Breach-tier event
/// should have cleared is not the same property, and nothing in the
/// amendment asks for that gap. <see cref="Evaluate"/> therefore treats each
/// trigger family as naming its own fixed ceiling — the low-severity family
/// (identified contact, a covered heard sound) raises the level to at most
/// <see cref="AlertLevel.Raised"/>, the high-severity family (friendly death,
/// wall breach) raises the level to at most <see cref="AlertLevel.Breach"/> —
/// and folds every ceiling with the previous level under <see cref="Max"/>,
/// which is what the acceptance criterion's "each trigger raises exactly one
/// level and no more" describes: no trigger ever pushes the level past its
/// own named ceiling, regardless of where the level started. This is a
/// decision the design and the plan did not spell out; it is recorded here,
/// in the task report, and nowhere else, exactly so it reads as a decision
/// rather than an accident.
/// </para>
/// <para>
/// <b>No decay timer.</b> Design section 5 is explicit that this is
/// deliberate: "a monotonic level needs no duration constant, no per-faction
/// timer in the hash, and no answer to the question of what a squad
/// 'forgetting' a breach would mean while the breached door is still open in
/// front of it." <see cref="Max"/> is the only combinator this type offers,
/// and it has no path back down.
/// </para>
/// </remarks>
public static class AlertRules
{
    /// <summary>
    /// Whether a heard sound of <paramref name="kind"/> is one design
    /// section 5's amendment names as "carrying intent" — the only three
    /// <see cref="SoundKind"/> members that can raise <see cref="AlertLevel"/>
    /// on their own: gunfire, breaking glass, and a death scream. The other
    /// four named sounds — a bolt cutter, smoke, a hammer or crowbar, and a
    /// breacher shotgun round — are load-bearing for
    /// <see cref="ContactMemory"/> and future breach mechanics but are not
    /// named in the amendment's alert-raising list, so they do not raise the
    /// level through this path.
    /// </summary>
    public static bool CarriesIntent(SoundKind kind) =>
        kind is SoundKind.Gunfire or SoundKind.BreakingGlass or SoundKind.DeathScream;

    /// <summary>
    /// Whether a sound of <paramref name="kind"/>, made at the origin, both
    /// carries intent per <see cref="CarriesIntent"/> and is heard at a point
    /// <paramref name="dx"/>, <paramref name="dy"/> world units away per
    /// <see cref="HearingRules.IsHeard"/>. A convenience composition of the
    /// two checks the amendment's "hears a sound of a kind that carries
    /// intent... whose radius reaches it" names together; the underlying
    /// radii remain <see cref="HearingRules"/>'s, provisional where that
    /// type's own remarks say so, and this method neither restates nor
    /// changes them.
    /// </summary>
    public static bool IsTriggeringSound(SoundKind kind, long dx, long dy) =>
        CarriesIntent(kind) && HearingRules.IsHeard(kind, dx, dy);

    /// <summary>
    /// The higher of two <see cref="AlertLevel"/> values, by declaration
    /// order (<see cref="AlertLevel.Calm"/> &lt; <see cref="AlertLevel.Raised"/>
    /// &lt; <see cref="AlertLevel.Breach"/>). The only combinator this type
    /// offers, and the reason the level can never decrease: every result
    /// <see cref="Evaluate"/> or <see cref="EvaluateFaction"/> produces is the
    /// maximum of the previous level and zero or more trigger ceilings, never
    /// a value chosen independently of the previous level.
    /// </summary>
    public static AlertLevel Max(AlertLevel left, AlertLevel right) =>
        left >= right ? left : right;

    /// <summary>
    /// The alert level a faction commits for this tick, given
    /// <paramref name="previousLevel"/> — the level committed at the end of
    /// the previous tick, which is what stage 8's intent selection reads
    /// while this method's result is still being computed — and this tick's
    /// frozen-tick-start-view facts for that faction, already reduced to
    /// booleans by the caller. Never returns a value lower than
    /// <paramref name="previousLevel"/>; see this type's remarks for why a
    /// high-severity trigger can reach <see cref="AlertLevel.Breach"/>
    /// directly from <see cref="AlertLevel.Calm"/> rather than stopping at
    /// <see cref="AlertLevel.Raised"/>.
    /// </summary>
    /// <param name="previousLevel">
    /// The faction's alert level as committed at the end of the previous
    /// tick. This method never mutates or reads back a value it or any other
    /// call previously returned; the caller supplies it explicitly every
    /// time.
    /// </param>
    /// <param name="hasIdentifiedContact">
    /// Whether any living operator of this faction gained an
    /// <see cref="ContactTier.Identified"/> contact against the frozen
    /// tick-start view this tick.
    /// </param>
    /// <param name="hasTriggeringSound">
    /// Whether any living operator of this faction heard a sound satisfying
    /// <see cref="IsTriggeringSound"/> this tick.
    /// </param>
    /// <param name="hasFriendlyDeath">
    /// Whether any operator of this faction died this tick — design section
    /// 5 stage 13's <c>DamageResolution.ResolveDeaths</c> output, reduced to
    /// "at least one entry belonging to this faction" by the caller.
    /// </param>
    /// <param name="hasWallBreach">
    /// Whether a breachable wall on this faction's side was breached this
    /// tick. No wall-breach type exists in this worktree yet — the caller
    /// that eventually detects a breach supplies this as a plain
    /// <see langword="bool"/>, the same pattern <paramref name="hasFriendlyDeath"/>
    /// and every other parameter here follow.
    /// </param>
    public static AlertLevel Evaluate(
        AlertLevel previousLevel,
        bool hasIdentifiedContact,
        bool hasTriggeringSound,
        bool hasFriendlyDeath,
        bool hasWallBreach)
    {
        var level = previousLevel;

        if (hasIdentifiedContact || hasTriggeringSound)
        {
            level = Max(level, AlertLevel.Raised);
        }

        if (hasFriendlyDeath || hasWallBreach)
        {
            level = Max(level, AlertLevel.Breach);
        }

        return level;
    }

    /// <summary>
    /// <see cref="Evaluate"/>, folded over every operator's individual
    /// per-tick observations rather than pre-reduced booleans. The design
    /// amendment's triggers are all "any living operator of that faction" —
    /// a logical OR across the faction's roster — and OR is commutative and
    /// associative, so folding <paramref name="operatorObservations"/> in any
    /// order produces the same four reduced booleans and therefore the same
    /// result; nothing here sorts or otherwise depends on array order, unlike
    /// <c>DamageResolution.Accumulate</c>'s sort-by-key, because OR needs no
    /// such normalisation to be order-independent. <see cref="AlertRulesTests"/>
    /// asserts this by permuting <paramref name="operatorObservations"/> and
    /// checking the result does not change.
    /// </summary>
    public static AlertLevel EvaluateFaction(
        AlertLevel previousLevel,
        ReadOnlySpan<AlertTriggerObservation> operatorObservations)
    {
        var hasIdentifiedContact = false;
        var hasTriggeringSound = false;
        var hasFriendlyDeath = false;
        var hasWallBreach = false;

        foreach (var observation in operatorObservations)
        {
            hasIdentifiedContact |= observation.HasIdentifiedContact;
            hasTriggeringSound |= observation.HasTriggeringSound;
            hasFriendlyDeath |= observation.HasFriendlyDeath;
            hasWallBreach |= observation.HasWallBreach;
        }

        return Evaluate(previousLevel, hasIdentifiedContact, hasTriggeringSound, hasFriendlyDeath, hasWallBreach);
    }
}

/// <summary>
/// One living operator's contribution to <see cref="AlertRules.EvaluateFaction"/>
/// for this tick — the four facts the amendment's triggers name, already
/// resolved against the frozen tick-start view for this one operator. Not
/// itself authoritative or hashed state, exactly like
/// <see cref="ContactObservation"/>: an ephemeral per-tick input a caller
/// assembles from data <see cref="AlertRules"/> is deliberately not given
/// direct access to (the tick-start view itself, and the wall-breach source
/// of truth, neither of which exists in this worktree yet).
/// </summary>
/// <param name="HasIdentifiedContact">
/// Whether this operator gained an <see cref="ContactTier.Identified"/>
/// contact this tick.
/// </param>
/// <param name="HasTriggeringSound">
/// Whether this operator heard a sound satisfying
/// <see cref="AlertRules.IsTriggeringSound"/> this tick.
/// </param>
/// <param name="HasFriendlyDeath">
/// Whether this operator is the one who died this tick. A dead operator
/// contributes this fact exactly once, on the tick it dies; a caller folding
/// <see cref="DamageResolution.ResolveDeaths"/> output sets this
/// <see langword="true"/> only for entity ids that appear there.
/// </param>
/// <param name="HasWallBreach">
/// Whether this operator witnessed a breachable wall on their faction's side
/// breached this tick. See <see cref="AlertRules.Evaluate"/>'s remarks on
/// <c>hasWallBreach</c> for why this is a plain <see langword="bool"/>.
/// </param>
public readonly record struct AlertTriggerObservation(
    bool HasIdentifiedContact,
    bool HasTriggeringSound,
    bool HasFriendlyDeath,
    bool HasWallBreach);
