# Sandata continuation — the column about-face, written 2026-08-15

A session prompt for whoever implements the column about-face. Paste everything
below the line into a fresh agent session started in
`C:\Users\boazs\webdev\autonomous-arena`.

Read it in full before running anything. The single most expensive mistake
available here is retrying one of the three fixes in the "do not retry" section:
each of them is a real defect, each looks like the obvious answer, and none of
them fixes the bug.

---

> **SUPERSEDED IN PART, 2026-08-19. Read this before anything below it.**
>
> The "fix, already decided" in this prompt — order the column by projected
> arclength — was implemented in full and **does not work**. It reorders the
> column correctly, with no oscillation, and it relocates the freeze from the
> retarget hairpin to the path's own 145-degree corner at `(110,110)`, costing
> two tests that passed before. Bringing `SlotTargets.ComputeTarget` into scope
> was then tried in three further variants, and none of them frees the map
> either.
>
> Seven attempts have now failed. Before writing any code, read
> `docs/plans/2026-08-19-sandata-corner-trail-placement-findings.md`, which
> records all of them and the corner geometry that rules out every
> target-placement rule. The short version: at that corner a correctly placed
> follower is 5.7 world units in front of a leader that is about to walk
> forward, and the pre-existing build clears it with half a world unit of slack.
> What is needed is a yield behaviour — the follower gives way, or the leader
> waits for it to clear the apex — and that has no design yet.
>
> Everything below is still accurate about the *bug*, the traps, and the rules.
> Treat its stated solution as one of the refuted attempts.

---

You are continuing work on **Sandata**, the second game in the `autonomous-arena`
repository (working directory `C:\Users\boazs\webdev\autonomous-arena`). Read
`CLAUDE.md` first — sections 4, 5, 6, 9, and 10 bind you.

## Where the repository is

`main` is at `60389b8`. Both canonical gates were green at `a0595f0`; a
concurrent session then landed `21e1abb` (a Hukbo sprite-atlas change) which that
gate output does not cover, so re-run the gate before trusting main wholesale.

Sandata's recorded seed-1 baseline is `stateHash DA3D1BEB99978A75` with
`eventHash 260A20BC8F578E19`. Quote that pair. Older figures
(`A644B7F8A394885D`, `13EF0685BB46CA5E`) are superseded and live in
`docs/development/measurement-history.md`.

Run the gate with PowerShell 7:

```powershell
pwsh -NoProfile -Command "./scripts/verify.ps1 -SkipBootstrap"
```

## Your task

Implement `docs/plans/2026-08-15-sandata-column-about-face-design.md`. That
design is binding and outranks this prompt; if the two disagree, the design wins
and you report the discrepancy.

**The bug:** a Sandata squad cannot reverse direction. `SquadGrouping.Compute`
assigns both the leader and every slot index in ascending entity-id order
(`src/Sandata.Core/Squads/SquadGrouping.cs:229-232` and `:246-251`), so entity 1
leads for the whole mission wherever it physically stands. When a path reverses,
the operator that was correctly trailing is now in front, and its slot target
lies behind the leader — where the leader is standing. Following the new path
requires the two to exchange positions. `CollisionBodyRadiusRaw` is 4352 raw =
4.25 world units (`src/Sandata.Core/Simulation/SandataSimulation.cs:1703`), so
they need 8.5 world units of clearance and they have 8.7. Two bodies cannot swap
in a corridor narrower than four radii. Stage 9 proposes a step every tick and
stage 10 refuses it, forever.

**The fix, already decided:** order a column by each member's projected arclength
along the group's published path, descending, with entity id as the tie-break.
The operator that was last is furthest along the new path and simply becomes
slot 0. Nobody passes anybody. No new hashed state, no remembered heading, no
reversal event, no threshold angle.

Follow the design's own staging:

- **Stage 0 — audit every reader of `SquadSlot.LeaderEntityId`.** Decision 2
  redefines it as "the entity holding slot 0" rather than "the lowest living
  entity id". Confirm each reader wants the new meaning. Read-only; changes no
  behaviour. If a reader genuinely wants lowest-id, stop and report — that
  reopens decision 2.
- **Stage 1 — order by arclength.** Decisions 1, 3, 4, and 6. `ProjectArclength`
  (`src/Sandata.Core/Simulation/SandataSimulation.cs:3386`) already computes the
  quantity for the leader; extend it to every living unassigned member rather
  than inventing a second measure of progress.
- **Stage 2 — the oscillation benchmark.** Ordering by progress can jitter if two
  members' projections cross back and forth. Measure slot-0 identity changes per
  hundred ticks and state a number. Do not add a hysteresis constant unless the
  measurement demands one.

## Acceptance criteria

`main` carries, since `1a7b97d`, three tests that **currently fail and are
supposed to**. They are the real bar:

- `SweepingTheRealMap_TheSquadReachesTheClosetRoom`
- `SweepingTheRealMap_EveryRoomIsClearedEventually`
- `SweepingTheRealMap_TheSquadNeverStopsForGood`

They drive the real `angle-house.hkmap` fixture through a real `NavBake` and
assert against real geometry, not against the broken run's coordinates. Merge
them when your change makes them pass. Their current failure text is:

```
after 20,000 ticks the leader stood at (432.52,120.52), 469.8 world units
from the closet's own floor cell at (112,464) — nowhere near arrival.
1 of 4 rooms never cleared: [18576].
the leader's sampled position never changed between tick 1000 and tick 20,000.
```

## Do not retry these — seven attempts, all failed

The first three were each a real defect and none was this bug; two were reverted
and the third shipped because it is a prerequisite. Attempts four through seven
are recorded in
`docs/plans/2026-08-19-sandata-corner-trail-placement-findings.md`:

4. **Ordering the column by projected arclength** — this prompt's own stated
   fix. Implemented on branch `sandata-about-face-attempt-4` at `4c74336`.
   Relocates the freeze and costs two passing tests.
5. **Trail offset as an exact straight-line distance between bodies.** Refuted
   by `ComputeTarget_OnRightAngleCorner_StaysInCorridor…`: it pushes the target
   through a wall.
6. **The same as a floor, applied after the lateral offset.** Refuted the same
   way.
7. **Clamping a follower so it is never ahead of the leader along its heading.**
   Passes every `SlotTargetsTests` case and is the identity on straight paths.
   Still does not free the map: it pulls the follower abeam, where the whole
   separation is the perpendicular component of about 8.5 world units, which is
   the body diameter exactly.

The original three:

1. `SlotTargets.ComputeTarget` clamps a negative slot arclength onto the path
   head, stacking every trailing slot on one point. Real defect. Replacing it
   with backwards extrapolation moved the freeze 8 world units east and fixed
   nothing. Reverted. Design section 13 leaves it deliberately open.
2. `ProjectArclength` clamps an off-path leader to arclength 0 via
   `Math.Clamp(numerator, 0, denom)`, so a leader past its own path head can
   never re-enter it. Real defect. Lifting it moved the freeze about one world
   unit. Reverted.
3. Holding unassigned operators while their group's path request is outstanding.
   **Shipped at `a0595f0` and is a hard prerequisite** (design decision 7),
   because `PathService.RequestPath` never clears `CurrentPath`, so without it a
   squad walks its stale route for the whole ten-tick latency window after every
   retarget. Worked exactly as designed and the squad still froze.

## Traps that cost real time in the previous session

- **The canonical gate cannot see any of this.** `HeadlessRunner.BuildInitialState`
  sets `Groups` empty (`src/Sandata.Headless/HeadlessRunner.cs:461`) and both
  golden replay fixtures use that same builder
  (`tests/Sandata.Core.Tests/GoldenReplayTests.cs:118`). Four consecutive changes
  moved no hash for this reason. A green gate says nothing about squad behaviour.
- **A closed `.hkmap` door is passable.** `NavBake` writes
  `NavCellFlags.Door = 2`, which is nonzero, and `_pathBlockedCells` marks only
  `Blocked = 0`. The angle-house closet is reachable. The previous session
  asserted the opposite and was wrong.
- **The JSON log's `t` field is sticky.** `SetTick` is called only where something
  interesting is logged, so a frozen `t` means nothing was logged, not that ticks
  stopped. Two sessions misread this. Trace `_nextTick` per frame instead.
- **To tell a movement bug from a navigation bug**, print stage 9's desired
  position against the settled position. A `want` that keeps changing while the
  position does not is stage 10 refusing, and no path-side change will move it.
- **This checkout is shared with other live sessions.** Files appear mid-task.
  Stage by pathspec, never `git add -A`.
- **Verify a subagent's decisive claims against the source.** Several confident
  reports in the previous session did not survive checking, in both directions.

## Rules that bind you

- The canonical gate is never delegated. Run it yourself and paste real output.
- Coding agents run on Sonnet, every time. Confirm the roster with `bo agents`.
- Every sub-agent prompt is caveman-compressed; repository documentation,
  commits, and user-facing prose stay in full English.
- Code discovery goes through `tokensave` before Grep or Glob. Never spawn an
  Explore agent for code research in this repository.
- `docs/archives/` is deprecated by definition — never execute or cite an
  archived plan, and never write a path into that folder.
- No agent may flip a manual smoke-checklist row.

## Branches on disk (updated 2026-08-19)

```
main                              the three arrival tests, failing deliberately
.claude/worktrees/sandata-about-face-acceptance   holds branch sandata-about-face-attempt-4
sandata-about-face-attempt-4      decision 1 implemented; makes the map worse, unmerged on purpose
```

Confirm before removing any of them — some may belong to another session.

## Start here

1. Read `CLAUDE.md` and the about-face design document in full.
2. Confirm state: `git log --oneline -3` and one gate run.
3. Do stage 0 — the `LeaderEntityId` reader audit — before writing any behaviour.
4. Report honestly. If a gate fails, paste the failure.
