# Sandata — making the client playable — 2026-08-10

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.
>
> The status line below is stale in one respect: the work was committed, as
> `5508310` and `c13b696`.

Status: P1 through P9 complete, both gates green, not yet committed. Binding
design document remains
`docs/plans/2026-08-07-sandata-scaffold-design.md`; where this plan and that
document disagree, the design document wins.

## Why this plan exists

Wave 12 closed and every row on `docs/plans/2026-08-07-sandata-scaffold.md`
through task 91 is done, but nobody had ever launched the Sandata client. This
session launched it for the first time. It opens, it draws the `angle-house`
map correctly, and it is not a game: it is a static diorama.

Three facts, each verified against `main` at `57760ea` before this plan was
written.

**The client never advances the simulation.** `grep -rn RunTick
src/Sandata.Client` returns exactly one hit, and that hit is a doc comment at
`src/Sandata.Client/SandataGame.cs:173` stating the omission outright: "This
task never calls `SandataSimulation.RunTick` — it only submits orders through
`SandataSimulation.SubmitOrder`." The simulation is constructed at `:375` and
never ticked. A thirteen-and-a-half-second run at `trc` produced four log
lines, every one carrying `"t":-1`.

**Operators are not drawn from simulation state.**
`DrawOperatorsAndFireCones` (`SandataGame.cs:928` before this plan) iterates
`_spawnRecords` — the map's own static `SPAWN` records. Even a ticking
simulation would have moved nothing on screen.

**No autonomous destination source exists.** `BuildInitialState` sets `Groups =
ImmutableArray<GroupPathState>.Empty`, and `AdvancePathService`'s own remarks
say plainly that no source populates it in this worktree. With no group path
state, stage 7 has nothing to search for and stage 9 has nothing to walk along.

Two window captures taken twelve seconds apart were byte-identical. That is the
starting point.

## Decisions taken for this plan

The four questions were put to the user on 2026-08-10 and answered. They are
recorded here in the answered form, since the first two change what the code
does.

**1. The client will run and draw the simulation.** Approved. This is not a
design question — it is missing implementation.

**2. The autonomous destination source is objective-seeking, and it lives in
the client.** Approved, with the user's stated longer-term intent recorded so
the next session does not mistake this for the final answer:

> we'd want a per area/section clearing, and eventually add noise detection,
> then attracts into the noise, but autonomous destination source is enough for
> now

So: per-area/per-section clearing with noise attraction is the intended
destination model. What this plan builds is the smallest thing that makes the
game demonstrate itself — each assaulting squad walks to a map objective — and
it is explicitly a stepping stone.

**A correction, since this plan was written believing otherwise.** The session
handoff described "what an autonomous squad wants and how a destination is
chosen" as an existing entry in design section 15. It was not there. Section 15
listed ten questions and none of them was this one — sections 7 and 8 specify
how a squad moves once it has a destination and section 8 names the field that
stores one, but nothing in the document ever said what writes that field, and
the omission went unnoticed for twelve waves. This plan adds it as question 11
and records the partial answer there.

**The source lives in `Sandata.Client`, not `Sandata.Core`, and that placement
is deliberate.** A destination source inside `Sandata.Core` would fire for the
200-operator seed-1 headless workload as well, which today runs with an empty
`Groups` array. Those operators would begin pathing, movement would change, and
`stateHash BDD56EBD06F76674` would move — which under `CLAUDE.md` section 5
requires a new preset version and new golden expectations. Building the
destination in the client keeps `Sandata.Core` untouched, keeps every recorded
Sandata digest valid, and still gives a tester something to watch. When the
real per-area clearing model is designed, that is the point at which moving it
into the simulation is worth its preset bump.

**3. Task 89's stall is accepted for this round and documented, not fixed.**
Approved. A blocked mover still stalls permanently against a static body; the
tester is told to expect it rather than discovering it.

**4. The miss model beyond range is deferred.** Approved. It only becomes
observable once decisions 1 and 2 exist, so it is a question for the session
after this one.

## The destination rule, stated exactly

`angle-house` carries two `OBJECTIVE` records. Note that
`ObjectiveRecord(LineNumber, Index, X, Y, RadiusWu)`'s second field is an
**ordinal index, not a faction** — a natural misreading, and one this plan made
before checking the record definition. The two objectives sit at (500, 120) and
(120, 520), which are exactly the two faction-1 spawn positions. The map
therefore already reads as "faction 0 assaults, faction 1 holds two rooms",
which is the room-clearing shape the product is aiming at.

The rule:

1. Derive the initial squads from the initial roster by the same union rule
   `Sandata.Core.Squads.SquadGrouping` applies — two operators of the same
   faction within `GroupCohesionRadiusWu` (96 wu under
   `SandataRuleset.ModernTacticalV1`) are unioned, and a component's identity
   is the minimum entity id it contains.
2. Every faction-0 group is an assaulting group. Rank them ascending by group
   id, rank the objectives ascending by `Index`, and give the group at rank *k*
   the objective at rank *k* modulo the objective count.
3. That group's `GroupPathState` gets `StartCellIndex` at its leader's cell
   (the minimum entity id, which at tick zero is also the minimum living entity
   id), `GoalCellIndex` at the objective's own cell, `DestinationCellIndex`
   equal to the goal, `HasOutstandingRequest` true, and `RequestTick` zero.
4. A goal cell that bakes as `NavCellFlags.Blocked` is replaced by the nearest
   non-blocked cell, searched outward in a deterministic ring order. An
   objective with no reachable cell anywhere on the grid yields no group entry
   at all rather than an invalid one.
5. Faction 1 groups get no entry. They hold, which is what
   `PathReasonCode.NoDestinationRequested` already means and what the map's own
   objective placement implies.

On `angle-house` this produces exactly one group: entities 1 and 2, 24 wu
apart and therefore one component under a 96 wu radius, group id 1, walking
from (296, 690) to the objective at (500, 120) — a genuine traverse across the
house through a door. The two faction-1 operators are 421 wu apart, form two
separate single-member groups, and hold.

## Why the client duplicates the union rule

`SquadGrouping` is `internal` to `Sandata.Core` and `InternalsVisibleTo` names
only `Sandata.Core.Tests`, so the client cannot call it. `SandataSimulation`
exposes no derived squad slots either — stage 6 recomputes them every tick and
stores nothing, by design.

The client therefore derives the initial components itself, over all
same-faction pairs rather than over a collision-grid candidate list. **That is
exact, not approximate.** `SquadGrouping`'s own remarks state that a candidate
list "can only narrow, never widen", and `SandataSimulation`'s stage 3 builds
its list from a `RebuildWithinRange` call sized to `GroupCohesionRadiusWu` —
so every pair within the cohesion radius is already a candidate, and an
all-pairs scan gated on the same radius reaches the same components. The
duplication is real and is worth naming: it is one rule written in two places,
and the client's copy carries a remark pointing at the original.

## Task list

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| P1 | `TickPacing`, a pure fixed-timestep helper: accumulate elapsed microseconds, emit whole ticks at 20,000 µs each, clamp catch-up so a stalled frame cannot spiral | `src/Sandata.Client/Simulation/TickPacing.cs` | new unit tests |
| P2 | `InitialSquadGroups`, the pure destination builder above | `src/Sandata.Client/Simulation/InitialSquadGroups.cs` | new unit tests |
| P3 | Drive the simulation from `SandataGame.Update` using P1; hold play/pause, speed, single-step, and restart state | `src/Sandata.Client/SandataGame.cs` | the game visibly moves |
| P4 | Draw operators, facings, and fire cones from `_simulation.State.Operators` instead of `_spawnRecords`; dead operators read as dead; a firing operator shows its muzzle flash | `src/Sandata.Client/SandataGame.cs` | the game visibly moves |
| P5 | Populate `Groups` from P2 in `BuildInitialState` | `src/Sandata.Client/SandataGame.cs` | one squad walks unprompted |
| P6 | Wire the control bar's four buttons and their keyboard equivalents, plus Escape to exit | `src/Sandata.Client/SandataGame.cs` | manual |
| P7 | Tests for P1 and P2 | `tests/Sandata.Client.Tests/TickPacingTests.cs`, `tests/Sandata.Client.Tests/InitialSquadGroupsTests.cs` | `./scripts/test.ps1 -Game Sandata` |
| P8 | The tester's script, the honest not-working list, and the smoke-row reality | `docs/development/testing.md` | read by a person |
| P9 | Record the answered question and its stepping-stone status | `docs/plans/2026-08-07-sandata-scaffold-design.md` section 15 | read by a person |

## Findings recorded but deliberately not fixed

**`MissionState.Tick` never advances.** Nothing in `SandataSimulation.RunTick`
or any stage it calls writes `Tick`; it stays 0 for an entire run.
`SandataStateHasher` folds `state.Tick` at line 155, so it contributes a
constant zero to every tick's state hash, and `HeadlessRunner`'s
`left.State.Tick != right.State.Tick` divergence check at line 179 compares 0
against 0 on every tick of every run. Events stamped from `state.Tick` —
`ShotFired`, `ShotHit`, `ShotMissed`, `OrderRejected` — all carry tick 0.

This is a real defect and it is not this plan's to fix: advancing the field
changes the state hash and therefore requires a new preset version and new
golden expectations under `CLAUDE.md` section 5. The client works around it by
keeping its own tick counter, which is the value `RunTick` takes as an
argument anyway.

> **Fixed on 2026-08-11, and the sentence above is wrong about the preset.**
> `SandataSimulation.RunTick` now writes `State.Tick = currentTick` before
> stage 1 — before stage 1 specifically, because stage 1 can emit an
> `OrderRejected` event and every event stamps itself from `State.Tick`. Both
> Sandata digests moved, the golden fixture and the recorded seed-1 baseline
> were re-measured, and five new tests in `TickPipelineTests` bind the
> behaviour; each was break-proofed by pinning the field back to 0.
>
> **No new preset version was created, and none was needed.** The claim above
> repeated a loose sentence in design section 15 rather than design section
> 4's actual trigger list, which is an enum's numeric value, an enum's order,
> the roster order, a weapon weight, the tick rate, the millisecond conversion
> rule, or a hash mixer. A defect in `RunTick` is none of those, the ruleset
> content is untouched, and `SandataRuleset.ContentHash` is unchanged at
> `8_955_292_433_887_190_872` — so `ModernTacticalV1 = 1` still names exactly
> the ruleset it always named. Burning a second preset value would have made
> preset 1 name a ruleset that never differed. Recorded in
> `docs/development/testing.md` under "The seed-1 headless workload,
> re-measured 2026-08-11".
>
> The client's own tick counter is left in place. It is now redundant with
> `State.Tick` rather than a workaround, and removing it is a separate change
> that no longer has a correctness reason behind it.

**`scripts/run.ps1:59` prints a control hint Sandata did not implement** —
"Press Escape for Play, Pause, and Exit Game". Task P6 makes the hint true
rather than changing the text.

**A killed Sandata client leaves a zero-byte log.** `JsonlLogSink` sets
`AutoFlush = false` and `Program` flushes in its `finally`, so terminating the
process discards the buffer. This is correct behaviour, not a defect, but it
costs a session when discovered by accident: close the window, never kill the
process.

## Result

Every task P1 through P9 is done.

### What a run now does, measured rather than asserted

The client was launched, left alone, and closed cleanly, with the debug log at
`dbg`. The whole log:

```
{"seq":4,"t":1,"ms":444,"lvl":"dbg","ch":"sim","ev":"sim.sandata.roster","assaultingAlive":2,"defendingAlive":2}
{"seq":5,"t":459,"ms":9943,"lvl":"dbg","ch":"sim","ev":"sim.sandata.roster","assaultingAlive":2,"defendingAlive":1}
```

The assaulting pair leaves the bottom wall, crosses the house through the lower
door, reaches the objective at (500, 120), and kills the defender holding it at
tick 459 — about 9.9 seconds of real time, which at 50 ticks a second is the
right number for a 605 world-unit traverse at the measured per-tick cap. Both
attackers survive. Three window captures taken two, seven, and twelve seconds
into a run are three different images; before this change, two captures twelve
seconds apart were byte-identical.

### Break-proofs

Each break was applied to a copy-backed file and undone by restoring that copy,
never by `git checkout --`.

| Break | Result |
| --- | --- |
| `TickPacing`'s surplus-discard clamp removed, so a clamped frame banks its backlog | `Advance_AFrameFarAboveTheCeiling_DiscardsTheSurplusRatherThanBankingIt` fails alone |
| `InitialSquadGroups.Union`'s merge direction inverted | **nothing fails** — see below |
| `FirstIndexOfRoot` scanning descending instead of ascending | `Build_TwoAssaultingOperatorsInsideTheCohesionRadius_ProduceOneGroupIdentifiedByTheLowerEntityId` fails alone, with a group id of 7 instead of 3 |
| `SortByIndex` replaced by the raw map order | `Build_ObjectivesOutOfOrderInTheMap_AreRankedByIndexAndNotByMapOrder` fails alone |

**The union-direction break failing nothing is a finding about the code, not a
gap in the test.** Group identity does not come from which index survives as
the union-find root; it comes from `FirstIndexOfRoot`'s ascending scan, which
reports a component's lowest member whichever index happens to be its root. The
comment in `Union` had claimed the direction was load-bearing, and that claim
was wrong and is now corrected in place rather than left standing. The test
carries a "what this test does not bind" paragraph saying the same thing, and
the break that *does* bind identity is recorded above.

### Gates

`./scripts/verify.ps1 -Game Sandata`, exit 0: `Sandata.Core.Tests` 1,113 of
1,113, `Sandata.Client.Tests` 219 of 219 (199 before this plan, plus 20 new),
`stateHash` `BDD56EBD06F76674`, `eventHash` `7C1B37876769DEC7`,
`outcome: Ongoing`, `deterministic: true`, `allocatedBytes` 6,078,230,504 —
about 6.08 GB. Every digest unchanged, which is the point: no line of
`Sandata.Core` was touched.

`./scripts/verify.ps1 -SkipBootstrap`, exit 0: `Hukbo.Core.Tests` 2,433 of
2,433, `Hukbo.Client.Tests` 3,499 of 3,499, and both headless workloads
unchanged at `1B73FC5923879AA0` / `AC55684F24D39344` and `C8023D3B5BEB005E` /
`F709A345E2F7370E`, both `deterministic: true`. Run because this change edits
`src/Hukbo.Diagnostics/LogEvents.cs` and `scripts/run.ps1`, both of which the
Hukbo client suite reads.

The two runs are two results and are never added together.

### The smoke rows

All eight stay `PENDING`. No agent may flip one and none was flipped. What
changed is which of them a person can now reach: SD-1, SD-2, SD-3, and SD-6 are
attemptable, SD-7 is half attemptable, and SD-4, SD-5, and SD-8 cannot be
attempted at all. The tester's ordered script and the full list of what is
knowingly not working are in `docs/development/testing.md` beside the table.
