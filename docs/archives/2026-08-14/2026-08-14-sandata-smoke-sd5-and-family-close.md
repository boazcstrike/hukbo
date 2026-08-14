# Sandata smoke — `SD-5`, and the family closing in full — 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**The Sandata family is closed.** All nine rows pass, so the section was deleted
from the live checklist whole, under that file's own rule that a family every one
of whose rows is `PASS` is a record rather than a checklist. This document holds
the last row's evidence and the section's still-useful prose, which is why it is
longer than a single-row record.

| Field | Value |
| --- | --- |
| Rows in the family | 9 — `SD-1` through `SD-8` with `SD-7b` |
| Closed on 2026-08-11 and 2026-08-12 | 6, recorded separately |
| Closed on 2026-08-14 | 3 — `SD-7b`, then `SD-4`, then `SD-5` |
| Rows still open | None |
| Prior interactive runs of `SD-5` | Four, all failed: 2026-08-11, twice on 2026-08-12, and once on 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Windows 11 desktop, interactive |
| Source commit | **`7db52fa` on branch `sandata-engage-raise`, not `main`** |
| Launch path | `./scripts/run.ps1 -Game Sandata -Configuration Debug`, from the `sandata-engage-raise` worktree |
| Gate at the time | `./scripts/verify.ps1 -Game Sandata` and `./scripts/verify.ps1` both `[PASS]`; Sandata `stateHash A644B7F8A394885D`, `eventHash AEDE4D16B5E6FAAF`, `deterministic true`, matching the recorded baseline exactly |

## The row that closed

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-5 | Hold sustained automatic fire from the maximum operator count | Automatic fire sounds continuous rather than machine-gun-stuttered, and no audio drops out | 2026-08-14, fifth attempt, tester at the desktop. Passed | PASS |

## Read this before trusting the `PASS`

**The build that passed is not on `main`.** Branch `sandata-engage-raise` was
unmerged when this row closed, because another session held uncommitted work
across the main checkout. `main` is a build in which the rifle cannot fire
indoors at all.

## Why it took five attempts, and what each one was actually wrong about

This row is the clearest case in the project of a green test suite proving
nothing. Four separate fixes shipped for it, each correct, each with passing
tests, and none of them audible.

- **2026-08-11.** Sandata shipped no sound files and no playback path. Both
  landed that day under a narrow authorisation.
- **2026-08-12, first attempt.** The sounds were audible but did not sound like
  the weapon. Sixteen further takes were generated from prompts naming the
  weapon rather than only its cartridge.
- **2026-08-12, second attempt.** "No auto heard; it sounds just single shots."
  Accurate, and the cause was in the simulation: `FireModeSelection` and the
  cyclic-fire accumulator both had no production caller and the client hardcoded
  `FireMode.Single`, so no weapon in the roster had ever fired automatically.
  Both were wired.
- **2026-08-14, third attempt.** Still single shots. Two independent causes were
  found, either sufficient on its own. The audio latch played **one report per
  burst**: `SoundAutomaticFireStops` ran on every tick, and on the four quiet
  ticks between rounds `HandleAutomaticFireStopped` cleared the fallback flag,
  while the live 60-tick `GunLoop` reservation renewed rather than re-arming it.
  Separately, 100 health against 25 damage a round meant the fourth round killed,
  so the longest burst the game could physically produce was 0.30 seconds. The
  2026-08-12 tests had missed both, using a `health: 100_000` fixture and a
  `NoWalls(grid)` grid — the two conditions that make sustained fire possible.
- **2026-08-14, fourth attempt.** Still no burst. This is the one worth
  remembering, because the previous four fixes were all downstream of a gun that
  never went off.

### What the fourth attempt found, and how

Rather than send the tester back a fifth time, the game was driven directly: a
`Debug` run with `HUKBO_LOG_LEVEL=trc` and `HUKBO_LOG_CHANNELS=audio,sim`,
launched with `keybd_event` to press Space, left to run, and closed with
`WM_CLOSE` so the log flushed.

The whole run produced **seven shot cues, and every one of them was the
defending pistol firing `Single`. Neither attacker fired once.** The line at the
same tick read `{"ev":"sim.sandata.weaponState","entityId":1,"lowered":true}`.

`LoweredWallDistanceWu` is 24 world units and `angle-house`'s corridors are about
32 wide, so a rifleman is inside the threshold for his entire approach and is
forced lowered at the moment of contact. **In a room-clearing game the rifle
could not shoot indoors at all**, and no automatic round had ever been produced
on this map in the project's history.

Two changes closed it, both recorded as decisions D6 and D7 of that day's design:

- An operator engaging a hostile it has identified is no longer forced lowered.
  A moving operator with no identified target still lowers at a wall and in a
  doorway, which is what `SD-4` checks, so that row was not disturbed.
- The placeholder roster's health went from 100 to 300, making a burst long
  enough to hear.

The same driven run afterwards measured eleven reports from the AK attacker at
about 100-millisecond spacing across 1.03 seconds — the AK's 600 rounds per
minute, sustained for a full second.

### What was deliberately not done

`LoweredWallDistanceWu` was **not** reduced. The doorway aperture is 40 world
units, so its centre is 20 from each jamb, and any threshold below 20 stops a
doorway lowering the weapon and would have silently un-passed `SD-4`, which had
been closed hours earlier. That constant also folds into
`SandataRuleset.ContentHash`, so moving it costs a new preset version. Neither
change made here moved a hash.

## The section's prose, kept because it is still true

### Controls

| Input | Effect |
| --- | --- |
| Space, or the first control-bar button | Play / pause |
| Period (`.`), or the second control-bar button | Advance exactly one tick, pausing first |
| Tab, or the third control-bar button | Cycle speed: half, normal, double, quadruple |
| F5, or the fourth control-bar button | Restart the mission from tick zero |
| F6 | Cycle the theme. Not saved: the next launch starts on `night-ops` again |
| Escape | Exit |
| Mouse wheel | Zoom |
| Left-click on an operator | Select it, and open the operator inspector |
| Left-drag on the map | Marquee-select friendly operators |
| Right-click on the map | Add a node to a hand-drawn path |
| Enter | Submit the drawn path to the selected operators |
| Any letter key, released | Submit a go-code release order for the selection |

**Close the window to end a run. Never kill the process.** `JsonlLogSink` sets
`AutoFlush = false` and the log is flushed when `Program` exits normally, so a
terminated process leaves a zero-byte log file and the whole run's record is
gone.

### Drawing a path, and why one gets refused

Right-click three or four points, then press Enter with operators selected. The
squad abandons its objective route, walks the polyline node by node, and returns
to its own route at the last node.

A polyline that crosses a wall is refused by design. Design section 16 validates
an authored path at submission against four rules — node count, map bounds,
blocked cells, and wall crossings — and never silently re-routes one. A refusal
names its reason in the order queue panel and writes an `input.sandata.order`
`warn` line with `accepted: false`. A submission that produces neither a queue
row nor a log line submitted nothing at all, most likely from an empty selection.

### What was knowingly not working as of 2026-08-14

Some of this was fixed by the work above; the rest was still true when the
section was archived.

- There is no menu. The client opens straight into the mission.
- Almost no text. The contact list, mission clock, roster strip, and go-code
  panel are still blank rectangles, and there is no tick counter, score, or
  victory banner. The operator inspector and order queue do draw rows, and the
  inspector gained a firearm row and a lowered-state row on 2026-08-14.
- The mission never ends. Nothing checks an outcome; a run stops at the
  36,000-tick limit, about twelve minutes at normal speed.
- **No `GunLoop` or `GunTail` file exists**, so sustained fire is carried by one
  report per round. That fallback is marked in the code as the degradation it is,
  and it stops firing on its own the day real loop and tail files exist.
  Generating them is unauthorised ElevenLabs spend.
- Three of the five acoustic environments — `outdoor`, `distant`, `suppressed` —
  have no files, so a shot resolving one of them plays silence. In practice a
  shot inside 200 world units uses the `close` files and everything further out
  is silent.
- Accuracy is effectively range-only. This is a deferred design question, not a
  defect.
- Nothing consumes a magazine. `MagazineRounds` is stored and hashed and no stage
  decrements it, so automatic fire never runs a weapon dry.

## What a later reader should be careful of

- **Do not reduce `LoweredWallDistanceWu`.** It would un-pass `SD-4` silently and
  costs a preset version. See above.
- **The health value is a placeholder, not a measurement.** It lives in the
  client's scenario builder, reaches no hash, and its doc comment says why it is
  300. Treat it as tuning, and note that every engagement on the placeholder map
  now takes proportionally longer to resolve.
- **`SD-5` passed on a one-second rattle of discrete reports, not on a smooth
  continuous roar.** The row's wording asks for fire that "sounds continuous
  rather than machine-gun-stuttered", and the tester accepted the rattle. If loop
  and tail files are ever generated, this row's `PASS` predates them.
- **The row's wording drifted from the build and was never rewritten.** It asks
  for fire "from the maximum operator count"; the shipped mission has four
  operators and there is no scenario selector.
