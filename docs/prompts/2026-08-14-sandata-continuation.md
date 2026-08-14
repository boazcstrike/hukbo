# Sandata continuation — written 2026-08-14

A session prompt for whoever picks Sandata up next. It is written to be read in
full before anything is run, because the single most expensive mistake available
here is building a feature that passes its tests and does nothing on screen.
That happened six times on 2026-08-14 alone.

## 1. Where the repository actually is

`main` is at `0e04364`. Both canonical gates are green on it.

**The gate now runs both games when you pass no `-Game`**, as of 2026-08-14. It
runs the five Hukbo workloads, prints a banner, then runs Sandata's suite and its
seed-1 benchmark. An explicit `-Game` still runs exactly one game. Use
PowerShell 7:

```powershell
pwsh -NoProfile -Command "./scripts/verify.ps1"
```

Running `./scripts/verify.ps1` from Windows PowerShell 5.1 fails in `doctor.ps1`
with a version error. That is the shell, not the repository.

**Sandata's recorded seed-1 baseline moved on 2026-08-14** to
`stateHash 13EF0685BB46CA5E` with `eventHash AEDE4D16B5E6FAAF`, when a
concurrent session's mission-outcome fix landed. `A644B7F8A394885D` is the
superseded figure and appears in older records; do not quote it as current.
`docs/development/testing.md` is the authority.

**The Sandata smoke family is closed.** All nine rows passed on 2026-08-14 and
the section was deleted from `docs/development/smoke-checklist.md` whole. The
rows still open in that file belong to Hukbo families.

## 2. The one branch that is not merged, and must not be

`sandata-rooms` at `e16aec9`. It is stage 0 of the clear-the-map design, and it
is committed as **evidence, not as a fix**. Its own commit message says do not
merge.

What it does: room derivation by flood fill, a hashed `RoomClearState`, phase-A
ordering, retargeting, and the engage freeze. 1,147 Core tests pass and no pinned
digest moved.

What is wrong with it, in two stages:

- As built entirely inside `Sandata.Core`, it was **completely inert in the
  shipped game**. `SandataGame` constructs `SandataSimulation` with six
  arguments; `roomLayout` is a seventh with a default of `null`. No layout meant
  no seeded `RoomClearStates`, so `TrySelectNextRoom` never found a room and
  nothing ever retargeted. Every test passed because every test constructed the
  simulation itself and passed a layout.
- Once wired, it **regressed the mission**. A driven eighty-second run produced
  one log line, zero audio cues, and no roster change, where the same probe on
  `main` produces thirty lines, seventeen cues, and a casualty. The squad stops
  fighting.

The diagnosis, not yet confirmed by instrumentation: the sweep retargets to the
nearest uncleared room by octile distance, which from the spawn is the hall the
squad is already standing in, so it never travels to the objective where the
defender is. Correct as a sweep, useless as an assault.

## 3. The decision that blocks it

The design's ordering rule needs correcting before the code does. Three options
were put to the user and none was chosen before the session ended:

- **A.** The objective seeds the first target; the sweep takes over once that is
  cleared. Assault first, then mop up.
- **B.** Rooms holding a known hostile outrank distance. Contact-driven.
- **C.** Order by distance to the objective rather than to the squad.

**A was recommended**, because it preserves the behaviour the mission has today
and adds sweeping after it, so it cannot regress what already works. Get an
answer before touching `sandata-rooms` again. This is a design correction, not an
implementation bug, and patching it by instinct is how it becomes both.

## 4. The lesson that matters more than any item below

**Six features on 2026-08-14 shipped with green suites and did nothing, or the
wrong thing, when the game was actually run.** Automatic fire, the weapon-lowered
transition, the indoor audio, the pathfinder, room sweeping inert, room sweeping
regressed. The unit suites caught none of them. A driven run caught all six.

The technique, which works and is cheap:

```powershell
$env:HUKBO_LOG_LEVEL = 'trc'
$env:HUKBO_LOG_CHANNELS = 'audio,sim'
# start the built exe, SetForegroundWindow, keybd_event Space to play,
# sleep, then PostMessage WM_CLOSE so the log flushes.
```

`SendKeys` does not work — MonoGame polls raw device state rather than the
message queue, so only `keybd_event` is seen. **Never kill the process**:
`JsonlLogSink` sets `AutoFlush = false` and a terminated run leaves a zero-byte
log. Then read `artifacts/logs/sandata-*.jsonl` and count what actually
happened — shot cues by environment and fire mode, weapon-state transitions,
roster changes.

**The recommended next piece of work is turning that into `scripts/probe.ps1`**
with a documented set of assertions, roughly "a run must produce at least one
shot cue and at least one roster change". Perhaps an hour. It cannot flip a smoke
row and it does not replace a person at a desk; it is a smoke alarm. Today the
gate cannot tell a working game from one where nobody moves.

## 5. The queue, in the order recommended

| Order | Work | State |
| --- | --- | --- |
| 1 | `scripts/probe.ps1` | Not started. Recommended before any feature |
| 2 | Room sweep | Designed, built, regressed. Blocked on section 3's decision |
| 3 | Scenario roster | Designed, not built. `docs/plans/2026-08-14-sandata-scenario-and-roster-design.md` |
| 4 | Blocked mover re-plan | Design needs a second pass — see below |
| 5 | Magazine and reload | Designed and **authorised**. `docs/plans/2026-08-14-sandata-magazine-and-reload-design.md` |
| 6 | The HUD | Last. Wants the others to exist first |

**On the scenario roster.** It keeps per-operator data in a file beside the map
rather than extending the `SPAWN` grammar, so no `HKMAP` version is spent and
`angle-house.hkmap`'s pinned content hash is untouched. It also fixes a drift
already in the tree: `SandataGame`'s `PlaceholderOperatorHealth` is `300` and
`HeadlessRunner.cs`'s inline literal is still `100` — two placeholders for one
concept, already diverged.

**On the blocked mover.** The user first chose "re-request the path" and then
reversed it the same day, because re-requesting cannot work: a blocked mover has
zero displacement by definition, so its start cell, its goal, and the blocked
span are all unchanged, and `NavSearch` contains no randomness — the search
returns the identical route. Dynamic bodies now enter the nav search's blocked
span. The design's closing section names five questions that reversal opened and
deliberately does not answer them: which bodies get marked, when the span is
written and cleared without reintroducing a per-tick allocation, whether a body
blocks its own squad, whether the seed-1 insulation holds, and what happens to a
published detour once the blocker moves away. Answer those before building.

**On magazine and reload.** Authorised on 2026-08-14 in a narrow form recorded
in `CLAUDE.md` and `AGENTS.md`: a round consumed per shot, a reload costing the
firearm's authored `ReloadMs`, and **infinite spare magazines**. A finite spare
count is the stock-and-consumption economy that do-not bullet exists to stop and
stays unauthorised. This one moves both hashes, so it costs recaptured golden
expectations.

## 6. Decisions already taken, so nobody reopens them

- The product name is **`Sandata`**. Settled 2026-08-14; the question is closed.
- Sandata is **a different product from Hukbo**, not downloadable content. What
  the two share is the engine core. `CLAUDE.md` section 1 stands unchanged. The
  shared assemblies keep the `Hukbo.` prefix.
- **Real weapon names** in shipped display strings, taken with the design's own
  trademark analysis on the record and the choice still behind one field.
- **2D weapon geometry**, not meshes.
- Sandata **stays in this repository** long-term.
- Map authoring **stays hand-written**; no editor. The grammar is documented in
  `docs/development/map-format.md`.
- An autonomous squad wants to **clear the map**, checking all corners, rooms
  before the map as a whole. That is what the clear-the-map design specifies.
- A **wall-bearing golden fixture** is wanted, owned by the concurrent
  mission-outcome work. This overrides an earlier "no real map for now" answer.

## 7. Traps, each of which cost real time

- **A green Sandata suite is weak evidence about anything interactive.** See
  section 4.
- **No Sandata determinism fixture runs against a real map.** Both golden
  replays and the gate's own workload build a wall-free grid through
  `HeadlessRunner.BuildOpenGrid`. That is why a pathfinder ignoring every wall on
  every map survived the gate for the project's whole life. The gate cannot
  detect a pathfinding change that only shows up around geometry.
- **Do not shrink `LoweredWallDistanceWu`.** The doorway aperture is 40 world
  units, so its centre is 20 from each jamb; any threshold below 20 stops a
  doorway lowering the weapon and silently un-passes a closed smoke row. It also
  folds into `SandataRuleset.ContentHash`, so moving it costs a preset version.
- **`docs/plans/2026-08-07-sandata-scaffold-design.md` is Sandata's binding
  contract** and outranks every summary of it, including `CLAUDE.md`'s. Its
  plan document was archived on 2026-08-14; the design was not, and must not be
  while Sandata has no v0.1.
- **No file outside `docs/archives/` may carry a path into it.** That folder is
  pruned. Name an archived document in prose. Sixty-nine such citations were
  rewritten on 2026-08-14 to make one archive move possible.
- **This checkout is shared with other live sessions.** Files appear and vanish
  mid-task, and `docs/development/smoke-checklist.md` in particular is rewritten
  underneath you. Recount its status column at write time, stage by pathspec, and
  never `git add -A` in the root checkout.
- **Verify a subagent's decisive claims against the source.** Several confident
  reports on 2026-08-14 did not survive checking, in both directions — one agent
  predicted red tests that were green, another reported a directory empty that
  was not.
