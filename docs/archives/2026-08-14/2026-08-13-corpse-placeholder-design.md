# Corpse placeholder — design

**Archived: reference only.** Shipped at `4b9253d`; the corpse branch lives in
`PawnVisualStateResolver`, and smoke row 131 closed `PASS` on 2026-08-13. Every
path citation to it under `src/`, `tests/`, `scripts/`, and `docs/` was
rewritten on the day it was archived to name this document in prose, which is
what the rule against paths into `docs/archives/` requires. Never execute it,
never treat it as a live task list, and never cite it as the reason to make a
change. The live contract for this project remains `CLAUDE.md` and
`docs/development/testing.md`; nothing in this file overrides either of those.
Archived 2026-08-14.

Date: 2026-08-13. Scope: `Hukbo.Client` presentation only. No simulation type,
no tick stage, and no hash is touched by anything in this document.

## 1. Why this exists

Smoke row 131, "Trampled areas visibly thin where fighting happened", asks a
tester to observe the grass around a cluster of `Death` events. It was attempted
on 2026-08-13 and abandoned. The tester's whole report was three words: **"no
visible casualty"**.

That is literally true of the build rather than a testing mistake, and the cause
was measured afterwards:

- `ArenaGame.Rendering.cs` skips any agent whose `IsAlive` is false unless
  `DefenderReactions.IsLethalHoldActive` is true for it, in both the counting
  pass and the drawing pass.
- `GetPawnVisualState` returns only `Selected`, `Hovered`, or `Normal`. It never
  returns `PawnVisualState.Dead`.
- `PawnRenderer.DrawDeadMark` and the whole `isDead` desaturation path are
  reachable only from `AgentInspectorPanel`, which draws a single portrait of the
  selected agent.

So a fallen warrior is drawn for its death animation and then stops being drawn
at all. There is no minimap, and an event-feed entry carries no position, so a
spectator has no way to find where deaths happened. Row 131's precondition
cannot be met, and the row was marked `BLOCKED` for that reason rather than
`FAIL` — the trample work it tests may well be correct, and nobody has been able
to look at it.

This document specifies the smallest change that makes the row runnable.

## 2. What this is not

**This is a placeholder, and the name is meant literally.** It is not a corpse
system, it does not model bodies, it adds no new art, and it introduces no new
visual concept. Everything it draws was already built and simply had no caller:
`PawnVisualState.Dead`, the `ApplyState(colour, isDead)` desaturation every pawn
layer already runs through, `DrawDeadMark`'s X over the torso and head, and
`SubmissionCount.CountStateMark`'s `DeadMarkQuadCount` of 2. The change wires
existing code to an existing state.

A real casualty layer — bodies that fall in a direction, that decay, that pool,
that thin out under a cap, that read differently for a leader — is a separate
piece of work with its own design document and its own smoke rows. Nothing here
should be treated as that work having been done.

## 3. The change

Four rules, all in `ArenaGame.Rendering.cs` plus one new pure helper.

**A dead agent stays drawn for the rest of the battle.** The two `continue`
skips go. A body persisting is the entire point: row 131 needs a cluster of them
to survive long enough for a person to look at the ground underneath.

**The death animation is untouched.** While `IsLethalHoldActive` is true the pawn
still draws as `Normal`. A parallel workstream is lengthening exactly that window
so that a kill reads as a kill, and turning the pawn grey early would undo it.
Only once the hold has expired does the agent draw as `Dead`.

**A corpse does not animate.** No attack pose and no gait pose is passed for a
dead agent. `GaitAnimationSystem` already gates on `IsAlive`, so this is a matter
of not reintroducing motion rather than of suppressing it.

**Corpses draw beneath the living.** The pawn loop becomes two passes over the
same roster in the same order, dead first and living second, so a body never
occludes a fight in progress. Neither pass may renumber or compact ordinals: the
appearance cache addresses its slots by an agent's ordinal position in the full
roster, and a loop counter that skipped the dead would shift every later agent's
ordinal the moment somebody fell. The existing comment in that file explains this
at length and it survives unchanged.

Selection and hover are left alone. `AgentSelection` already skips dead agents,
so a corpse is neither hoverable nor selectable, which is the behaviour we want
and costs nothing to keep.

## 4. Where the decision lives

`ArenaGame.Rendering.cs` cannot be unit tested — Client presentation tests may
never construct `ArenaGame`, a `GraphicsDevice`, or a `SpriteBatch`. The state
decision therefore moves into a pure helper, `PawnVisualStateResolver`, taking
the entity id, the selected and hovered ids, `isAlive`, and `isLethalHoldActive`,
and returning a `PawnVisualState`. The renderer delegates to it.

Precedence is decided explicitly rather than left to `if`/`else` fallthrough:

| Agent | Lethal hold | Selected or hovered | Resolved state |
| --- | --- | --- | --- |
| Alive | — | selected | `Selected` |
| Alive | — | hovered | `Hovered` |
| Alive | — | neither | `Normal` |
| Dead | active | cannot arise | `Normal` |
| Dead | expired | cannot arise | `Dead` |

The two "cannot arise" cells are not an omission. `AgentSelection` refuses to
select or hover a dead agent, so no dead agent can carry either id.

## 5. Cost

A corpse costs what a living pawn costs plus `DeadMarkQuadCount`, which is 2.

The drawn-pawn count no longer falls as a battle progresses, so the worst case
moves from "every agent alive at tick 0" to "every agent dead at the end". Since
the roster size is fixed at scenario creation and never grows, the pawn count
itself is unchanged at the worst case — what rises is 2 quads per agent for the
dead marks. `RenderBudgetEstimate` and its tests pin arena batch quad totals at
200 and 500 units, and that arithmetic has to be checked against those pinned
numbers rather than assumed. A budget assertion that goes red is reported with
its numbers, not weakened, skipped, or re-pinned, and detail is not silently
dropped from the corpse draw to make it fit.

## 6. How this is proven

Not by the gate. The automated tests can show that the resolver returns the right
state for every combination of alive, hold, and selection, and that is worth
having, but it is not the row.

**Row 131 closes only when a person at an interactive Windows desktop runs a
battle to the point where casualties have accumulated, finds a cluster of bodies,
and reports whether the ground under them reads as visibly worn compared to
untouched field.** Until this change that person had nothing to look for. The row
moved from `BLOCKED` back to `PENDING` when the change landed — not to `PASS`,
because landing this change only made the row runnable.

It was then run, and it passed. On 2026-08-13 a person at an interactive Windows
desktop re-ran row 131 with the corpse placeholder in the build and reported
`PASS`: a cluster of bodies gave them somewhere to look, and the ground beneath
it read as visibly worn against untouched field. The caution above still stands
as written — a landing change is not a passing row, and the row went to `PENDING`
first — it simply did not have to wait long for the run that closed it. With row
131 closed the improve-visuals smoke family finished at thirty-two rows of
thirty-two and was deleted from `docs/development/smoke-checklist.md`, which by
its own rule holds open work only. The history of the family, including why this
row was blocked rather than failed, is preserved in the archived record titled
"Visual improvement smoke (VIS) — closed 2026-08-13"; find it by title rather
than by path.
