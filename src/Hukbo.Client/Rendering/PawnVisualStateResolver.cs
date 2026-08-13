namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from one agent's identity, aliveness, lethal-hold status, and
/// the spectator's current selection and hover to the <see cref="PawnVisualState"/>
/// <c>PawnRenderer</c> draws it in. Extracted so the decision can be unit
/// tested without constructing <c>ArenaGame</c>, a <c>GraphicsDevice</c>, or a
/// <c>SpriteBatch</c> — this file has no MonoGame dependency at all.
/// </summary>
/// <remarks>
/// <para>
/// The corpse layer (docs/plans/2026-08-13-corpse-placeholder-design.md;
/// smoke row 131) is what gave a
/// dead agent a second state to resolve to. Before it, <c>GetPawnVisualState</c>
/// only ever chose among <see cref="PawnVisualState.Selected"/>,
/// <see cref="PawnVisualState.Hovered"/>, and <see cref="PawnVisualState.Normal"/>,
/// because a dead agent outside its lethal hold was never drawn at all — the
/// caller's own <c>continue</c> skipped it before this method was reached.
/// Now every agent is drawn for the rest of the battle, so this method has to
/// say what a dead one looks like too.
/// </para>
/// </remarks>
internal static class PawnVisualStateResolver
{
    /// <summary>
    /// Resolves the visual state for one agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Precedence, decided explicitly rather than left to <c>if</c>/<c>else</c>
    /// fallthrough (the reasoning ArenaGame.Rendering.cs already works through
    /// at its own selection/hover call site applies here too):
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <b>Not alive, lethal hold active</b> → <see cref="PawnVisualState.Normal"/>.
    /// The kill has not finished reading yet — a parallel change is lengthening
    /// this exact window so a kill reads clearly — so the pawn keeps drawing
    /// exactly as it did before it died, undesaturated and unmarked.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Not alive, lethal hold expired</b> → <see cref="PawnVisualState.Dead"/>.
    /// The hold has finished; this is the first frame, and every frame after,
    /// in which the agent is a corpse rather than a warrior mid-death.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Alive, selected</b> → <see cref="PawnVisualState.Selected"/>.
    /// <b>Alive, hovered (and not selected)</b> → <see cref="PawnVisualState.Hovered"/>.
    /// <b>Alive, neither</b> → <see cref="PawnVisualState.Normal"/>.
    /// Selection and hover are decided only once aliveness has already routed
    /// to this branch — a corpse can never be selected or hovered in the first
    /// place, because <c>AgentSelection</c> and the hover-selection helper both
    /// skip a dead agent upstream of this call, so those two states structurally
    /// cannot arise for one. This method does not re-derive that exclusion; it
    /// only reflects the order the two families of state are allowed to compete
    /// in, for the case where a caller ever did pass a dead agent's id as
    /// <paramref name="selectedEntityId"/> or <paramref name="hoveredEntityId"/>.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="entityId">The agent being resolved.</param>
    /// <param name="selectedEntityId">
    /// The spectator's current selection, or <c>null</c> if nothing is selected.
    /// </param>
    /// <param name="hoveredEntityId">
    /// The agent under the pointer this frame, or <c>null</c> if none.
    /// </param>
    /// <param name="isAlive">Whether the agent is alive this tick.</param>
    /// <param name="isLethalHoldActive">
    /// Whether the agent is within its lethal-hold window — meaningful only
    /// when <paramref name="isAlive"/> is <c>false</c>; ignored otherwise.
    /// </param>
    internal static PawnVisualState Resolve(
        ulong entityId,
        ulong? selectedEntityId,
        ulong? hoveredEntityId,
        bool isAlive,
        bool isLethalHoldActive)
    {
        if (!isAlive)
        {
            return isLethalHoldActive ? PawnVisualState.Normal : PawnVisualState.Dead;
        }

        if (entityId == selectedEntityId)
        {
            return PawnVisualState.Selected;
        }

        return entityId == hoveredEntityId
            ? PawnVisualState.Hovered
            : PawnVisualState.Normal;
    }
}
