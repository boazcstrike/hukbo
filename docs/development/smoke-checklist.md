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

## Where the checklist stands, 2026-11-21

**66 table rows in 9 subsections, every one `PENDING`, plus the nine `SPB` rows
described below.** Counted from the status column on 2026-11-21, not carried
forward from any previous figure. Thirty-three of the table rows were added on
2026-11-21 by the We March as One optimizations package: six are `GOV-1`
through `GOV-6` for the play-rate governor, eight are `INT-1` through `INT-8`
for render interpolation, eleven are `TAL-1` through `TAL-11` for the weapon
damage tally, and eight are `SEP-1` through `SEP-8` for the opt-in movement
preset V17. Of the thirty-three older table rows, twelve are
`WS-1` through `WS-12` from the weapon and shield sprite atlas package; `CC-1`
came from the contingent cohesion package, for the opt-in movement preset V15;
five are `HEL-1` through `HEL-5` from the hide-event-log change; eight are
`SB-1` through `SB-8` from the shield size against projectile size package; and
seven are `AR-1` through `AR-7` from the armor identity package.

Nine more rows, `SPB-1` through `SPB-9`, are stored as a single-line JSON array
in the shield size section rather than as a table, so a count of table rows
misses them. All nine are `PENDING`, which makes the true total 75.

`GR-5` closed `PASS` on 2026-08-16 and took the last of the GPU render smoke
family with it, so that section is gone from this file and its archive record
is dated 2026-08-16.

The armor rows are unlike most of the others in that they cover a preset that
**is** the shipped default, so a tester needs to choose nothing to reach them.
Two of them, `AR-6` and `AR-7`, are expected to be hard or impossible to pass
as written and say so in their own rows; recording "cannot tell" or "no visible
difference" against those two is a real result, not a failure to test properly.

No `PASS`, `FAIL`, `BLOCKED`, or `DECLINED` row is left. Twenty-nine rows
closed on 2026-08-15 and were lifted out with the six families that closed in
full: calibrated army composition, death collapse, UI chrome nine-slice, pawn
visual fidelity, pawn sprite body, and the hidden battle event log. Each has a
dated archive record carrying its family's name, and every one of those records
says the same thing about its evidence — the tester returned a pass verdict and
wrote down no separate observation, though several rows had asked for one.

**Recount before trusting that figure.** Count the status column itself, and
count every status: a row that is neither `PENDING` nor a result is still a
row. Every count in this file that was ever taken on faith turned out to be
wrong.

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
log; `B` switches sprite bodies on and off; `I` switches render interpolation
on and off, and it comes back on at every launch; `T` shows and hides the
damage tally card, which is hidden on launch. While the machine cannot hold the
speed you asked for, the status line shows both, for example
`4x (holding 2.6x)`.

**A row that names no preset is read against the shipped default.** That default
is `ClientSettingsStore.DefaultMovementPreset`, currently
`MovementPresetId.EvasiveFootworkV14`. A preset chosen in the Army
Composition panel is staged for the next Full Reset rather than applied to the
battle in progress.

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
| SB-1 | Start a battle, let it run, press `B`, then press `B` again | Every warrior's head and torso changes to drawn art on the very next frame, and changes back on the second press. No stall, no flicker, and the battle does not pause or restart | | PENDING |
| SB-2 | Press `B` to enable the mode, quit the game, relaunch | The game comes back up still drawing sprite bodies, because the choice was persisted | | PENDING |
| SB-3 | With sprite bodies on, watch a full engagement at the default camera fit and say whether the two sides stay tellable apart | Team A still reads blue and Team B still reads red at a glance, without relying on the selection boxes. The faction wash is a provisional tuning value at 0.32; if the sides blur together, record that and the mode fails its own acceptance | | PENDING |
| SB-4 | Zoom in on one contingent and compare warriors side by side | Warriors visibly differ from one another — skin tone, headband colour and presence, hair, facial hair, tattoos, build. They do not all share one body. Record roughly how many distinct bodies you can pick out | | PENDING |
| SB-5 | Watch a warrior die with sprite bodies on | The drawn body rotates and falls with the collapse, staying attached to the legs. It does not stay standing upright while the rest of the pawn falls | | PENDING |
| SB-6 | Watch a warrior walk and run with sprite bodies on | The legs still animate underneath the drawn torso. Gait is unaffected by the mode | | PENDING |
| SB-7 | Watch a warrior fight with sprite bodies on | The weapon arm still swings and still points at the target, drawn over the body rather than under it. The shield still sits in front of the torso | | PENDING |
| SB-8 | Zoom all the way out until pawns resolve the `Low` detail tier | The body falls back to the procedural quads with no flicker and no gap at the changeover. This is expected behaviour, not a defect | | PENDING |

## Battle event log hidden by default (the 2026-08-15 hide-event-log change)

The battle event log no longer draws on launch. The right-hand column it used
to occupy is given back to the arena, and the log returns either through the
new `Events` button on the control bar or through F8. The sound log keeps F9
and its own button, and the two are independent: either, both, or neither may
be shown.

None of these rows may be closed by a passing test, by a build, or by a
screenshot probe. Only a person at an interactive Windows desktop, watching a
live battle, may flip one of them. Leave untouched rows `PENDING`; report
`BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| HEL-1 | Launch the client and look at the right-hand side of the window before pressing anything | No event log is drawn, and the arena extends to the right margin of the window rather than stopping short of a log column. The control bar carries a new `Events` button, shown inactive | | PENDING |
| HEL-2 | Press F8, then press F8 again | The event log appears in the right-hand column and the arena narrows to make room for it; the `Events` button reads as active while it is shown. The second press hides it again and the arena returns to full width | | PENDING |
| HEL-3 | Click the `Events` button on the control bar twice | The button toggles the same visibility F8 toggles, and its active state tracks whether the log is shown | | PENDING |
| HEL-4 | With the event log hidden, press F9 to show the sound log | The sound log occupies the whole right-hand column on its own rather than only its usual lower share, and the arena narrows by exactly the column width | | PENDING |
| HEL-5 | With the event log hidden, click and scroll where the log used to be, then press Escape | Nothing in the hidden log reacts: the click reaches the arena beneath it, the wheel drives the camera rather than a log scroll, and Escape is handled by whatever would handle it with no log present | | PENDING |

## Shield size against projectile size (the 2026-08-15 shield-projectile-block design)

Warriors now carry one of three shield states rather than two: no shield, a
narrow breast-high board, or the body-length tall hardwood shield. A larger
shield intercepts more and stops small projectiles reliably, a larger
projectile is harder for any shield to stop and punishes the small shield
worst, a larger shield is slower to carry, and a warrior who has just taken a
blow on the shield has their pace clamped for a few ticks before recovering.
The shipped build runs combat preset V7 and movement preset V14, which are the
only presets that carry any of this.

None of these rows may be closed by a passing test, by a build, or by a
screenshot probe. Only a person at an interactive Windows desktop, watching a
live battle, may flip one of them. Leave untouched rows `PENDING`; report
`BLOCKED` honestly.

[{"#":"SPB-1","Step":"Launch the client and look at the shielded warriors on both sides before pressing play","Expected":"Two visibly different shields are drawn: a tall body-length board and a clearly narrower, shorter breast-high board. The difference is legible without zooming in","Actual":"","Status":"PENDING"},{"#":"SPB-2","Step":"Click a warrior carrying the narrow shield and read the agent inspector","Expected":"The `Shield:` row reads `Narrow Breast-High`, not `None` and not `Tall Hardwood`","Actual":"","Status":"PENDING"},{"#":"SPB-3","Step":"Run a battle and watch an archer or arquebusier shoot at a tall-shield bearer, then at a narrow-shield bearer","Expected":"The tall shield stops noticeably more of what is shot at it than the narrow shield does","Actual":"","Status":"PENDING"},{"#":"SPB-4","Step":"Watch arquebus fire against a tall-shield bearer specifically","Expected":"Shot gets through the shield far more often than arrows do — the arquebus is the least blockable projectile in the game despite being the smallest","Actual":"","Status":"PENDING"},{"#":"SPB-5","Step":"Watch a tall-shield bearer and a narrow-shield bearer advance across open ground alongside an unshielded warrior of the same weapon","Expected":"The unshielded warrior is fastest, the narrow-shield bearer is next, and the tall-shield bearer is slowest. The ordering is visible over a sustained advance","Actual":"","Status":"PENDING"},{"#":"SPB-6","Step":"Select a shield bearer, keep the inspector open, and watch it through a melee exchange in which the shield takes a blow","Expected":"A `Block:  recovering Nt` line appears in the inspector for a few ticks immediately after a blocked blow and then disappears on its own. It is absent at every other moment","Actual":"","Status":"PENDING"},{"#":"SPB-7","Step":"Watch the same warrior's movement at the moment that line is showing","Expected":"The warrior's pace visibly checks for the short window and then recovers. The effect reads as a brief stumble, not as a stun","Actual":"","Status":"PENDING"},{"#":"SPB-8","Step":"Show the battle event log and find a line for a one-handed attacker carrying each shield","Expected":"The suffix names the shield size — `tall shield` or `narrow shield` — rather than the bare word `shielded`, and an unshielded attacker still reads `solo`","Actual":"","Status":"PENDING"},{"#":"SPB-9","Step":"Open the army composition panel and cycle the movement preset selector to the end of the list","Expected":"`V16 Shield Encumbrance` is present in the selector. It is NOT the default — the evasive footwork preset holds that, so selecting V16 and pressing Shift+R for a full reset is what SPB-5 and SPB-7 require before they can be run at all","Actual":"","Status":"PENDING"}]

## Armor identity (combat preset V8)

`CombatPresetId.PrecolonialPhilippinesV8` **is** the shipped default:
`ArenaGame.BuildScenario` selects it, so a spectator who changes nothing is
already watching armored warriors. That is deliberate — the design's whole point
is that armor reaches players rather than only the gate. Combat presets V1
through V7 stay registered and unmodified, and their content hashes are
unchanged, so any recorded replay naming one of them still reproduces.

Every mitigation value, every pace penalty, and every distribution share in this
package is invented gameplay tuning. No source in the sixteenth-century record
states what any armor stopped, what it weighed, or what proportion of a force
wore it — `docs/research/HISTORICAL_1500s_ARMOR.md` open question 11 says so
outright. None of these numbers is a historical measurement and none may be
presented as one.

Only *baluti* clears the attestation bar for a Filipino label, on Pigafetta's
own 1521 Visayan vocabulary. Every other armor term the research graded OPEN or
EXCLUDE, so the other variants carry English descriptors only. A row below
checks that the one permitted pair-form label renders correctly.

Two rows below are expected to be hard or impossible to pass as written, and
they are deliberately not softened. `AR-6` asks about a movement difference of
three to six per cent, which may simply not be perceptible; recording "cannot
tell" is a real and useful result, not a failure to test properly. `AR-7` asks
whether the drawn pawn matches its armor identity, and the honest expectation is
**no**: `PawnRenderer.Draw` takes an armor width factor that defaults to `1f`
with no call site passing anything else, and the armor material tone is a fixed
`DyePalette.BarkBrown` constant, so armor is not visibly drawn at all today. The
appearance *resolution* was made authoritative by this package; the *renderer*
was not, and that gap is recorded rather than hidden.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any row below. Compilation, unit tests, and a window-opening probe run do
not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| AR-1 | Watch one full battle from the default camera station. Select an agent whose inspector shows `Armor: Rigid Cuirass` or `Armor: Imported Iron`, then one showing `Armor: None`, and compare how long each survives sustained contact | The armored warrior visibly absorbs blows that kill the unarmored one. The difference is discoverable by watching, without reading source. If the two are indistinguishable, armor is not legible to a spectator and the feature is incomplete under `SIMULATION-GAME-STANDARDS.md` section 10 question 8 | | PENDING |
| AR-2 | Select any agent and read the inspector's armor rows | The armor identity row appears, followed by an armor variant label line, an armor evidence line, and a note line. The evidence line reads one of `Documented`, `Documented, form uncertain`, or `Provisional reconstruction` — never blank and never absent. This closes a live gap: the armor row had no evidence tier before this package, which `CLAUDE.md` section 7 requires | | PENDING |
| AR-3 | Cycle through agents until one shows the quilted-cotton baseline, and read its variant label character by character | The label renders exactly as `Baluti — Quilted Armor`, with a real em dash rather than a hyphen, and its evidence line reads `Documented`. No other variant label anywhere carries a Filipino term. A bare unqualified cultural name, or a Filipino name on any other variant, is a policy violation under `CLAUDE.md` section 7 | | PENDING |
| AR-4 | Watch the battle event feed through a sustained melee | A line reporting that armor absorbed damage and held off a lethal blow appears at least once, is readable as plain English, and is attributable to a blow the observer can see. It must be uncommon — it fires only when armor turned a killing blow into a survivable one. If it floods the feed, the emission condition has regressed from lethal-only to per-hit | | PENDING |
| AR-5 | Open the Army Composition panel, change the armor composition away from its default, press `Shift+R` for a full reset, and inspect several agents on each side | The fielded mix of armor identities visibly follows the setting, and both factions field the same composition as each other. A visible asymmetry between the two sides is a defect: mirroring is what makes an outcome difference attributable to the simulation rather than to the draw | | PENDING |
| AR-6 | Compare the movement of a warrior showing `Armor: Rigid Cuirass` against one showing `Armor: None`, both advancing across open ground | The rigid-armored warrior moves slightly slower. The modelled difference is six per cent, which is deliberately small, so **record honestly whether it is perceptible at all**. "Cannot tell them apart" is a legitimate and expected result here and should be written in the Actual column rather than treated as a failed test | | PENDING |
| AR-7 | Compare the drawn pawn of a warrior showing `Armor: Rigid Cuirass` against one showing `Armor: None` | Expected today: **no visible difference**, because the renderer ignores armor entirely. Record what is actually seen. If the two do differ, the renderer has changed since this row was written and the row needs rewriting. If they do not, this row documents the known gap and stays open until a separate package wires the armor width factor and material tone through to `PawnRenderer.Draw` | | PENDING |

## Play-rate governor (the 2026-11-21 play-rate-governor design)

The client now measures what a simulation tick costs and, when the machine
cannot run the requested speed inside half a frame, plays at the fastest speed
it can hold instead of stalling and bursting. The held speed is clamped between
0.25x and 4x and never exceeds the speed that was asked for. The status line
and the window title show both figures while the governor is holding, for
example `4x (holding 2.6x)`. The governor is presentation only: it decides how
many ticks a frame runs and never changes what a tick computes.

Every threshold in the governor is provisional tuning, chosen on one machine.
Whether a 1,000-unit battle holds at all depends on the machine, which is why
`GOV-1` asks for the machine to be written down.

Log evidence. Run `./scripts/run.ps1 -Configuration Debug -LogLevel dbg
-LogChannels render,input`. The event `render.playRateGoverned` is written on
a transition only, never once per frame, with the fields `requested`,
`effective`, and `costPerTickMicroseconds`.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any row below. Compilation, unit tests, and a window-opening probe run do
not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GOV-1 | Press `Esc`, open `Army Composition`, raise both teams to the 500-unit maximum, press `Shift+R` for a full reset, press `Space` to play, then press `4` and watch the status line for about ten seconds | Either the status line stays `4x` because the machine holds it, or it reads `4x (holding N.Nx)` with a number between 0.25 and 4. Record which one, the number it settles on, and the machine. While it is holding, pawns move at a steady slower pace with no stutter, no jumping forward, and no burst followed by a stall | | PENDING |
| GOV-2 | On a 1,000-unit battle that showed `holding` in `GOV-1`, press `1`, then `2`, then `4` again, watching the status line on each press | The line changes on the very frame of the key press. `1x` and `2x` show no `holding` suffix if the machine can sustain them, and `4x` returns to its own value. The window title carries the same text as the status line | | PENDING |
| GOV-3 | Press `Shift+R` after setting both teams back to the default composition (use `Reset to Default` in the panel), play, and press `2`, then `1` | A cheap battle shows plain `2x` and plain `1x`, never `holding`. If `2x` shows `holding` on a default-size battle, record the number, because that means the half-frame budget is too tight | | PENDING |
| GOV-4 | On a battle that is holding, press `Space` to pause, then press `1` while still paused | The status line drops the stale `holding` suffix immediately, without waiting for play to resume. A paused battle does not change its held speed by itself | | PENDING |
| GOV-5 | On a battle that was holding at `4x`, press `Shift+R` and play the new battle at `4x`. Then press `R` for the next round | After the full reset the status line shows no leftover `holding` value at the first frame. It re-measures and settles. After `R` the held speed carries over, with no stutter at the round start. In the log, `render.playRateGoverned` appears once per transition and never once per frame | | PENDING |
| GOV-6 | Let a battle that is holding at `4x` run to its end | Once the battle is decided the status line shows plain `4x`, with no `holding` suffix | | PENDING |

## Render interpolation (the 2026-11-21 play-rate-governor design, section 4)

Pawns are now drawn between their previous and current tick positions, so
motion is smooth on every frame rather than stepping twenty times a second.
The `I` key switches interpolation on and off live. The choice is not
persisted, so every launch starts with it on. Interpolation is presentation
only and reads positions the simulation already produced.

Several rows below ask for a judgement rather than a confirmation. A small
backward step at unpause, a slight jitter against a moving camera, and blood
that appears up to one step ahead of the pawn are known consequences recorded
in the design, and the rows exist to decide whether each is acceptable.

Log evidence. A key press is written as `input.key` with `key` equal to `I`
and the command `ToggleInterpolation`. No log line records whether
interpolation is on, so the screen is the only evidence of the mode.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any row below. Compilation, unit tests, and a window-opening probe run do
not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| INT-1 | Play at `1` with `I` on, zoom in on an advancing line, then press `I` and compare the same line for a few seconds, then press `I` again | With interpolation on, each pawn glides on every frame. With it off, pawns advance in visible small steps. The difference is discernible without being told which mode is running. Record whether it is | | PENDING |
| INT-2 | Press `I` ten times quickly while watching a battle | The mode flips on every press with no stall, no flicker, and no restart or pause of the battle. The pawns never jump to a wrong place when the mode flips | | PENDING |
| INT-3 | Press `I` to turn interpolation off, quit the game, relaunch, and start a battle | The game comes back with interpolation on, because the choice is not persisted | | PENDING |
| INT-4 | Zoom in on a moving pawn at `1`, then press `Space` and `Space` again about six times, one second apart | Judge whether a single small step backward is visible at each unpause. It may be one third of a tick of motion. Record "not noticeable", "noticeable but harmless", or "distracting". This is a known consequence of the design, so the row decides whether it is acceptable | | PENDING |
| INT-5 | Set `AUTO CAMERA` to `Follow` in the menu settings, play at `1`, let the camera follow a fight, and watch the pawns at the screen edges. Then set it to `Assisted` and repeat | Judge whether the pawns visibly shake against the screen edge as the camera moves. The camera follows tick positions, so a slight jitter is possible. Record whether it appears, in which mode, and whether pressing `I` removes it | | PENDING |
| INT-6 | Press `Space` to pause in the middle of an advance, click on a moving pawn at the centre of the screen, then do the same with the game playing at `1` | The inspector opens on the pawn that was under the pointer, and the selection mark sits on that pawn. A click lands on the pawn the spectator sees, not on the position it held one tick earlier | | PENDING |
| INT-7 | Press `Shift+R` and press `R` with interpolation on, and watch the first second of each new battle | No pawn slides in from the corner of the arena or from its last battle's position. Pawns appear at their starting places and move from there | | PENDING |
| INT-8 | Watch a melee exchange at `1`, first with interpolation on and then with it off, looking at where the blood appears relative to the pawn that is struck | Judge whether blood that appears up to one step ahead of the pawn with interpolation on reads as wrong compared with interpolation off. This is the cost recorded in design section 4.4. Record the preference, because it decides the default | | PENDING |

## Weapon damage tally (the 2026-11-21 weapon-damage-tally design)

A card titled `DAMAGE TALLY` lists, for each faction, every fielded weapon row
with the damage it has dealt, its share of the faction's total, the blows it
has landed, and how many of its warriors are alive. A section header reads, for
example, `Blue — 1,240 dealt`; the columns are `DAMAGE`, `SHARE`, `BLOWS`,
and `ALIVE`, the last written as `12 / 40`; and the footer reads
`Damage after armour, includes overkill.` The `T` key shows and hides the
card, which is hidden on launch and whose state is not persisted. Hovering a
row rings every living warrior of that faction and weapon in the arena.

The card is built from events and state the simulation already publishes. It
adds nothing to the simulation and decides no outcome.

Log evidence. A key press is written as `input.key` with `key` equal to `T`
and the command `ToggleDamageTally`. No log line records whether the card is
shown or which row is hovered.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any row below. Compilation, unit tests, and a window-opening probe run do
not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| TAL-1 | Launch the client and look at the arena before pressing anything, then press `T`, then press `T` again | No card is drawn on launch. After the first press a card titled `DAMAGE TALLY` appears at the top-right of the arena with a Blue section and a Red section. The second press hides it. The arena does not resize either way | | PENDING |
| TAL-2 | Press `T` to show the card, quit the game, and relaunch | The card is hidden again, because the choice is not persisted | | PENDING |
| TAL-3 | With the card shown, play at `2` for about a minute and watch the order of the rows in each section | Rows are sorted by damage dealt, highest first, and reorder as the battle unfolds. Two rows with equal damage keep a steady order rather than swapping every refresh. Numbers update about four times a second and stay readable at `4` | | PENDING |
| TAL-4 | Read every row label in both sections, and find a shielded variant | Every label is a Filipino name, an em dash, and a plain English descriptor, such as `Kampilan — Great Blade`. None is a bare name or a raw enum spelling such as `ArquebusRanged`. A shielded variant has its own row, labelled by the same wording the event log uses | | PENDING |
| TAL-5 | Hover each row in turn and look at the arena, zoomed out far enough to see the whole faction | A ring is drawn around every living agent of that faction and weapon and around no other. The number of rings matches the first number of that row's `ALIVE` column, for example `12` of `12 / 40`. Dead warriors are not ringed. The ring colour reads clearly against both team colours | | PENDING |
| TAL-6 | Hover a row so rings show, then move the pointer off the card. Hover again and press `T`. Hover again and press `Esc` to open the menu. Hover again and slide the pointer straight onto the inspector | The rings and the row's hover fill vanish together on the same frame in all four cases. No ring and no highlighted row is left behind | | PENDING |
| TAL-7 | With the card shown, click a pawn in the arena well clear of the card. Then click inside the card, on a row and on its empty margin. Then scroll the mouse wheel over the card | A click outside the card selects the pawn and opens the inspector as usual. A click inside the card does not select the pawn behind it. The wheel over the card does not zoom the arena. The arena keeps responding everywhere outside the card's rectangle | | PENDING |
| TAL-8 | Let damage accumulate, press `R` for the next round, then press `Shift+R` for a full reset, with the card shown each time | Every `DAMAGE`, `SHARE`, and `BLOWS` value returns to zero and every `ALIVE` reads full strength, such as `40 / 40`, after each reset. Section totals read `0 dealt` | | PENDING |
| TAL-9 | Launch and press `T` before pressing `Space`. Then let a battle run for ten seconds with the card hidden and press `T` | Before the first tick the card already lists each faction's weapon rows at zero damage and full strength. On a later show the numbers are current on the first frame, not blank and not ten seconds old | | PENDING |
| TAL-10 | Resize the window to a small size, then enlarge it, with the card shown | The card stays inside the arena at every size, stays anchored at the top-right, and no row text overlaps another. Long labels end in `...` rather than spilling out of the card. In a very short window the card drops its footer, then its headers, then its title, rather than drawing past its border | | PENDING |
| TAL-11 | With the card shown and a battle playing, rest the pointer on the inspector, then on the event log (`F8`), for several seconds each | The card's numbers keep refreshing while another panel holds the pointer | | PENDING |

## Separating contact (movement preset V17)

`MovementPresetId.SeparatingContactV17` is registered, selectable, and **not**
the shipped default. `ClientSettingsStore.DefaultMovementPreset` stays
`EvasiveFootworkV14`, so a tester has to choose V17 deliberately from the
movement preset selector in the Army Composition panel, where it is labelled
`V17 Separating Contact`, and then press `Shift+R`, because a preset chosen in
the panel is staged for the next Full Reset.

V17 is V14 plus soft separation and nothing else. Bodies may press into each
other slightly and a separation pass pushes them apart again, with one hard
guarantee: a living pair is never closer than 1.6 body radii. Every number
behind that behaviour is provisional gameplay tuning and none is a historical
or physical measurement.

The log does not record the active movement preset, so the selector in the
panel is the only evidence of which preset a battle is running.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip any row below. Compilation, unit tests, and a window-opening probe run do
not. Leave a row `PENDING` if untouched; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SEP-1 | Press `Esc`, open `Army Composition`, open the movement preset selector, and look at the last entry | `V17 Separating Contact` is present, directly after `V16 Shield Encumbrance`. Select it and the staged-composition notice appears. Pressing `Shift+R` applies it | | PENDING |
| SEP-2 | With V17 running, set both teams to the 500-unit maximum, play at `1`, and watch two armies meet from the default camera station. Then do the same with `V14 Evasive Footwork` and compare the packed front | Under V17 the packed front still reads as a line of individual warriors with irregular gaps, not as one dense blob of overlapping discs. Record whether the two presets are tellable apart at all. A front that reads as a single smear under V17 fails the design's own acceptance | | PENDING |
| SEP-3 | Under V17, zoom in on a warrior standing still in a dense press, then watch at `1` and again at `4` for ten seconds | A warrior that is not moving does not vibrate, shimmer, or crawl. Positions settle. Record any visible buzzing and whether pressing `I` to turn interpolation off removes it, since that separates a render cause from a simulation cause | | PENDING |
| SEP-4 | Under V17, zoom all the way in on the densest contact and look at neighbouring warriors | Bodies may touch and overlap slightly, by up to a fifth of a diameter, but two bodies never sit on top of each other and one never hides another completely | | PENDING |
| SEP-5 | Under V17, zoom in on the legs of warriors being pressed by neighbours in a crowd | The legs animate smoothly. A warrior shoved sideways does not twitch its feet, flicker between walking and standing, or flip a foot from frame to frame. A slow steady shuffle is acceptable and a rapid flicker is not | | PENDING |
| SEP-6 | Under V17, watch a melee pair at the front while others crowd in behind them | A warrior locked in a melee stays in contact with its opponent and is not shoved apart by bystanders | | PENDING |
| SEP-7 | Under V17, find two warriors that meet head-on on a narrow gap and watch for a few seconds | They pass or settle around each other. Two bodies do not press forever with no progress. Record whether any pair stays frozen | | PENDING |
| SEP-8 | Move `%LOCALAPPDATA%\Hukbo\settings.json` aside (rename it, do not delete it), launch the client, open `Army Composition`, and look at the selected movement preset. Then put the file back | A launch with no settings file shows `V14 Evasive Footwork` selected, not V17. V17 is opt-in | | PENDING |
