namespace Hukbo.Core.Movement;

/// <summary>
/// One warrior's bounded view of its neighbourhood, derived once per tick
/// during the tick-start observation stage under a movement preset whose
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
/// <see langword="true"/> (weapon-relative movement design, section 7).
/// Derived scratch: never hashed, never snapshotted, never persisted, and
/// overwritten every tick.
/// </summary>
/// <remarks>
/// Membership in either ring is inclusive at the exact radius. The immediate
/// counts exclude the acting warrior; <see cref="SupportAllies"/> and
/// <see cref="AlliedComposition"/> include it. Dead agents count nowhere.
/// Enemies are observed through the same perception test target selection
/// applies, so a warrior's context never reacts to an enemy it could not
/// have targeted; allies carry no perception test, matching the observation
/// stage, which never perceives allies at all. Squared-distance ties break
/// on lower <c>EntityId</c>; absence is <see langword="null"/>, the same
/// no-entity sentinel <c>AgentState.TargetEntityId</c> uses.
/// </remarks>
/// <param name="ImmediateAllies">
/// Living allies, excluding the actor, within the immediate radius
/// inclusive.
/// </param>
/// <param name="ImmediateEnemies">
/// Living perceived enemies within the immediate radius inclusive.
/// </param>
/// <param name="SupportAllies">
/// Living allies, including the actor itself, within the support radius
/// inclusive.
/// </param>
/// <param name="SupportEnemies">
/// Living perceived enemies within the support radius inclusive.
/// </param>
/// <param name="AlliedComposition">
/// Per-loadout counts over <paramref name="SupportAllies"/>, including the
/// actor's own bucket.
/// </param>
/// <param name="EnemyComposition">
/// Per-loadout counts over <paramref name="SupportEnemies"/>.
/// </param>
/// <param name="NearestAllyEntityId">
/// The nearest living ally inside the support radius, or
/// <see langword="null"/> when none exists. Ties break on lower
/// <c>EntityId</c>.
/// </param>
/// <param name="SecondThreatEntityId">
/// The nearest living immediate enemy other than the tick's selected target,
/// or <see langword="null"/> when none exists. Ties break on lower
/// <c>EntityId</c>.
/// </param>
public readonly record struct LocalMovementContext(
    int ImmediateAllies,
    int ImmediateEnemies,
    int SupportAllies,
    int SupportEnemies,
    LoadoutCompositionCounts AlliedComposition,
    LoadoutCompositionCounts EnemyComposition,
    ulong? NearestAllyEntityId,
    ulong? SecondThreatEntityId);
