# Interactive smoke checklist

The manual rows for both games, and **the only place a manual `PASS` may be
recorded.** Run `./scripts/run.ps1` on an interactive Windows desktop. This
repository uses local-only verification: there is no hosted-CI substitute for
this direct interaction pass.

**Only a person at an interactive desktop may flip a row.** No agent may, for
any reason, including a passing automated test. Compilation, unit tests, a
window-opening probe, and synthetic input do not make a row pass. A row nobody
attempted stays `PENDING`; a row that cannot be attempted is `BLOCKED` with the
reason recorded.

The gate, the current gate results, and the recorded baselines live in
`docs/development/testing.md`. Superseded measurement runs live in
`docs/development/measurement-history.md`.

## Where the checklist stands, 2026-08-15

**14 rows in 3 subsections, every one `PENDING`.** One is `GR-5`; twelve are
`WS-1` through `WS-12`, added on 2026-08-15 by the weapon and shield sprite
atlas package; and `CC-1` was added the same day by the contingent cohesion
package, for the opt-in movement preset V15. No `PASS`, `FAIL`, `BLOCKED`, or
`DECLINED` row is left.
Twenty-nine rows closed on 2026-08-15 and were lifted
out with the six families that closed in full: calibrated army composition,
death collapse, UI chrome nine-slice, pawn visual fidelity, pawn sprite body,
and the hidden battle event log. Each has a dated archive record carrying its
family's name, and every one of those records says the same thing about its
evidence — the tester returned a pass verdict and wrote down no separate
observation, though several rows had asked for one.

**Recount before trusting that figure.** Count the status column itself, and
count every status: a row that is neither `PENDING` nor a result is still a row.
Every count in this file that was ever taken on faith turned out to be wrong.

## Rules for editing this file

**This file holds open work only.** It is not a record of what has been tested.
A family every one of whose rows is `PASS` is deleted outright, prose and all,
and its history leaves with it in one piece. A family is deleted only when it is
entirely `PASS` — an open `FAIL` or `BLOCKED` row is unfinished work and stays
here where a reader will see it.

**A single passing row is lifted out the same way, without its section.** The
section's own preamble then names which of its rows closed and what to be
careful of when reading the archived result.

**There is no `PASS` column any more, and that is deliberate.** If a `PASS` ever
appears here it is a row that has just closed and has not yet been lifted.

**A fixed row goes back to `PENDING`, never straight to `PASS`.** A row keeps
its `FAIL` observation in `Actual` when it reopens, so the re-run is judged
against what was actually seen. An agent may write the fix; only a person may
close the row.

**No file in this repository may link to `docs/archives/`.** That folder is
pruned periodically, so a link into it is a link that breaks. Name a closed
row's record in prose, by title, and find it by that title:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'gpu-render-smoke'
```

**Controls, so no row has to be attempted by guesswork.** `Space` plays and
pauses; `1`, `2`, and `4` set playback speed; `R` starts the next round and
`Shift+R` is a full reset; `W`/`A`/`S`/`D` or the arrow keys pan; the mouse
wheel zooms; a left click selects a warrior and opens the agent inspector; `F8`
toggles the battle event log, which is hidden on launch; `F9` toggles the sound
log; `B` switches sprite bodies on and off.

**A row that names no preset is read against the shipped default.** That default
is `ClientSettingsStore.DefaultMovementPreset`, currently
`MovementPresetId.CohortLateralSpreadV13`. A preset chosen in the Army
Composition panel is staged for the next Full Reset rather than applied to the
battle in progress.

## GPU render smoke (gpu-render Phases 1 and 2)

**Four of this family's five rows have closed** — `GR-1`, `GR-2` and `GR-4` on
2026-08-14, `GR-3` on 2026-08-15 — and their records are the archives titled
"GPU render smoke — PARTIAL 2026-08-14", "GPU render smoke — `GR-4`", and "GPU
render smoke — `GR-3`". The one row below is what is left.

**Read this before deciding `GR-5` cannot be run.** The tester who ran this
family on 2026-08-14 stopped at it for one stated reason: the team size cannot
be raised above 500. That is correct and it is not a blockage, because the
ceiling is **per team, not per battle**.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is `500`, the panel's row is
labelled `Units Per Team`, and `ArenaGame` builds the scenario with
`composition.UnitsPerTeam * 2`, so the maximum gives exactly the 1,000-unit
battle this row wants. `GR-3` was run that way on 2026-08-15 and passed. `GR-5`
watches hit pulses inside that same battle, from the same launch; the panel
offers no bigger one to reach.

`HitEffectSystemTests` proves the per-frame pulse lookup returns what the
per-pawn scan returned. It cannot prove that the pulses read the same on screen,
which is the whole of this row's question.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip this row. Compilation, unit tests, and a window-opening probe run do not.
Leave it `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-5 | In the same 1,000-unit battle `GR-3` uses — `Units Per Team` at its maximum of 500, which is per team and gives 1,000 warriors on the field — wait for the two sides to close and watch hit pulses in the dense melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | Attempted 2026-08-14 and not run, because the tester read the 500 ceiling as a per-battle one. It is per team, and `GR-3` passed at that setting on 2026-08-15, so there is no larger battle to wait for | PENDING |

Phase 3's rows GR-6 through GR-10 are deliberately absent. They covered the
instanced backend, which the NO-GO verdict closed and which does not exist.

## Weapon and shield sprite atlas (weapon-sprite)

Eighty authored cells — ten variants for each of the seven weapon roles and ten
for the tall hardwood shield — drawn in place of the procedural weapon line and
the procedural shield block. The mode is off by default and the `V` key flips it
live.

Everything below is a question about what appears on a screen while a battle is
running, which is exactly the class of thing this repository's automated gate
cannot answer. The gate proves the atlas has the right shape, that every cell
sits inside its content box, that variant selection is stable and spread, that
the rotation constant is right for a worked example, and that the submission
counts fall. It draws nothing and looks at nothing.

The art itself was reviewed by rendering the atlas and looking at it, and that
review rejected and re-authored three of the eight rows; that record is in
`docs/plans/2026-08-15-weapon-sprite.md`. Reviewing a PNG is still not the same
as watching a warrior swing, which is what these rows are for.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any of these rows. Compilation, unit tests, and a window-opening probe run
do not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| WS-1 | Press `V` during a live battle, then press it again | Every armed warrior's weapon and shield changes on the next frame, and changes back | | PENDING |
| WS-2 | Press `V`, quit, and relaunch | The chosen style survives the restart | | PENDING |
| WS-3 | In sprite mode, watch one warrior swing through a full attack | The weapon tracks the swing arc and stays anchored in the hand, neither sliding along its own length nor drifting off the fist | | PENDING |
| WS-4 | In sprite mode, watch a warrior die | The weapon rotates with the collapse rather than staying upright | | PENDING |
| WS-5 | In sprite mode, look along a full battle line | Warriors of the same role visibly do not all carry the same weapon | | PENDING |
| WS-6 | In sprite mode, at normal gameplay zoom, compare the seven roles | The roles are tellable apart, and more easily than in procedural mode | | PENDING |
| WS-7 | In sprite mode, watch an archer draw and loose | The bowstring still pulls back, still meets both stave tips, and does not float off the drawn stave. The stave cells are authored with no string precisely so the procedural one is the only string on screen | | PENDING |
| WS-8 | In sprite mode, watch a shield bearer | The sprite shield occupies exactly the block the procedural shield occupied — no overlap onto the ground ring, the head, or the weapon | | PENDING |
| WS-9 | In sprite mode, zoom out far enough to cross into the Low detail tier, then back in | The weapon and shield fall back to the procedural drawing without flicker or a jump at the boundary | | PENDING |
| WS-10 | In sprite mode, watch a warrior take a hit and then die | Faction colour still reads on the sprite weapon and shield, and the hit pulse and dead-state fade still land on them | | PENDING |
| WS-11 | In sprite mode, watch a warrior swing leftward specifically, and compare against a rightward swing | Judge whether the single-edged blades read wrongly when the swing carries them through a half turn. This is a known, unfixed consequence of rotating one authored cell, recorded in design section 15; the row exists to decide whether it is acceptable, not to confirm it is absent | | PENDING |
| WS-12 | Switch the theme to the light one, whose arena surface is `#E5D4AA`, and look at sprite shields and pale hafts | They keep a readable edge. The three-pixel inner outline exists only because they dissolved into that background without it | | PENDING |

## Contingent cohesion before contact (movement preset V15)

`MovementPresetId.ContingentCohesionBeforeContactV15` is registered, selectable,
and **not** the shipped default. `ClientSettingsStore.DefaultMovementPreset`
stays `CohortLateralSpreadV13`, so a tester has to choose V15 deliberately from
the Army Composition panel, and a preset chosen there is staged for the **next
Full Reset** rather than applied to the battle already running. A round started
before the reset is still running V13.

The row below is the question this whole package exists to answer, and it is the
one no test can answer. The automated work proves that an advancing member
between one third and three quarters of the cohesion radius is now granted a
cohesion destination, that a contingent claims a smaller square, that member
spacing is byte-identical to V13, that all twenty seeds still decide before the
cap with a median of 2058 ticks, and that the worst blocked streak is 49 ticks
against a bound of 125. None of that says a contingent *looks* like a body
crossing the field. That is what `BR-1` originally reported as missing, in the
words "they visibly form up but not enough, some just charged and fought".

The preset is a provisional reconstruction for gameplay purposes. The band and
the margin were chosen from a calibration sweep, not from any source: no account
describes how close a warrior stood to the man leading his contingent.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip this row. Compilation, unit tests, and a window-opening probe run do not.
Leave it `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CC-1 | Open the Army Composition panel, select `V15 Contingent Cohesion Before Contact`, press `Shift+R` for a full reset, and watch one full battle from the default camera station. Then do the same with `V13 Cohort Lateral Spread` and compare | Under V15 a contingent visibly crosses the field as a body rather than dissolving into individual pursuit, and the difference from V13 is discernible without being told which preset is running. Record whether the two are tellable apart at all, because a change nobody can see on screen is the failure this row exists to catch. Spacing must still look irregular — the warriors close up, they do not dress into ranks or files, and if they look tidy that is a defect against the design's section 3 | | PENDING |
