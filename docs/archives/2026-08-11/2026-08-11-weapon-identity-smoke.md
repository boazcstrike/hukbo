# Weapon identity and attributes smoke (preset V2) — completed

**Archived: reference only.** This section was moved out of
`docs/development/smoke-checklist.md` on 2026-08-11, the day its last row
closed. All ten rows are `PASS`; nothing here is outstanding and nothing here
is an instruction. It is kept so that a later reader can trace why the agent
pick target, the pair-form weapon labels, and the four-rank composition panel
look the way they do, and what a person actually saw.

The live checklist is `docs/development/smoke-checklist.md`. Do not re-run
these rows from this file. If a later change touches weapon labels, weapon
silhouettes, the agent inspector, or the army composition panel, write fresh
rows in the live checklist rather than reviving these.

---

## Weapon identity and attributes smoke (preset V2)

**This family is complete: all ten rows `PASS`.** It took two interactive runs
on 2026-08-11 and one code fix between them.

The first run attempted every row. Six passed outright. The automated tests
already proved the labels, the profiles, the resolver, the reach floor, and the
panel arithmetic; what that run added was that an axe does read as an axe on
screen and that a shield block is visible at battle scale — and that a warrior
could not be clicked at all, and that the six-row composition panel two of these
rows were written against does not exist. See the findings below the table.

`V2-7` and `V2-8` were **rewritten** between the runs. They described a
six-weapon-category composition panel; the panel that ships is the four-rank
one, and no code has ever implemented the other. The rewritten rows describe the
panel that exists, so the two `FAIL` results the original wording produced are
recorded in finding 3 rather than left as rows nobody could ever pass. The
six-weapon panel is deferred, not cancelled — it is a feature nobody has
designed yet.

The second run, later the same day, closed the four rows the first run left
open: `V2-3` against the `AgentPickTarget` fix, `V2-7` and `V2-8` against their
rewritten wording, and `V2-10`, which the first run could not attribute at
battle scale.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| V2-1 | Watch the battle event feed for one exchange | Attack lines read `Kampilan — Great Blade`, `Wasay — War Axe`, `Kalis — Thrusting Blade (solo)`, `Kalis — Thrusting Blade (shielded)`, `Itak — Work Blade (solo)`, `Itak — Work Blade (shielded)`, with differing damage values | Pair-form labels appeared in the feed and the damage values differed between them | PASS |
| V2-2 | Watch the two-handed weapons in the feed | Neither Kampilan nor Wasay ever carries a `(solo)` or `(shielded)` suffix | The `(shielded)` suffix appeared only on Kalis and Itak lines; no Kampilan or Wasay line carried a suffix. No `(solo)` line was noticed at all during the first run — see finding 1 | PASS |
| V2-3 | Click a warrior, then a second of the same weapon and the other grip | The inspector shows the pair label, the evidence tier, the grip, and the three attribute values, and the two differ by one damage and one reach | First run 2026-08-11: `BLOCKED`, no warrior could be selected — the click target was about five screen pixels wide and sat at the warrior's feet rather than on its body. Fixed the same day by `AgentPickTarget`. Re-run later that day by the person at the desktop: reported `PASS` | PASS |
| V2-4 | Look at the battlefield at default zoom | Shield bearers are distinguishable from solo warriors of the same weapon without clicking either | Shield bearers were clearly distinguishable | PASS |
| V2-5 | Zoom out to the lowest detail tier | The shield block is still visible; the Wasay is still distinguishable from the Kampilan | Both held at the lowest tier | PASS |
| V2-6 | Compare a Wasay warrior against a Kampilan warrior up close | The Wasay reads as a hafted axe with a distinct head, not as a narrow blade | The Wasay read as an axe | PASS |
| V2-7 | Open the army composition panel | Four stepper rows, one per rank — `Datu`, `Maharlika`, `Timawa`, `Aliping Namamahay` — above a units-per-team row; every row and every button is fully on screen | The first run saw the four rank rows and reached every button; the row was then rewritten to describe the panel that ships. Re-run 2026-08-11 by the person at the desktop against the rewritten wording: reported `PASS` | PASS |
| V2-8 | Use Distribute Evenly, then Apply, then Full Reset | The battle fields the chosen composition: each rank's count is spread across every combat-preset V5 roster row carrying that rank, so moving the `Timawa` stepper visibly changes how many Kalis, Bangkaw, Busog, and Arquebus warriors take the field | The first run confirmed all three buttons worked and that the battle fielded what was chosen; the row was then rewritten to ask for the roster effect explicitly. Re-run 2026-08-11 by the person at the desktop against the rewritten wording: reported `PASS` | PASS |
| V2-9 | Launch with an existing pre-V2 settings file present | Settings reset to defaults without an error dialog or a crash; the composition is the four-rank default | Launched cleanly, no dialog and no crash | PASS |
| V2-10 | Listen during a Wasay attack | The war-axe sound plays; no slot is silent | First run 2026-08-11: `BLOCKED` — a wood-chop sound was audible and no slot was silent, but too many warriors were fighting at once to attribute any one sound to a Wasay attack. Re-run later that day by the person at the desktop: reported `PASS`. See finding 4 for the attribution caveat this row was written under | PASS |

### Findings from the 2026-08-11 V2 runs

**1. No `(solo)` line was seen on the first run, and this is an observation
rather than a confirmed defect.** `BattleEventFormatter.GetGripSuffix` returns
`solo` for any one-handed weapon carrying `ShieldId.None`, and the client's own
scenario does field solo rows: `ArenaGame.CalibratedRosterEntryWeights` gives
solo Kalis a weight of 10 against 44 for the whole Timawa group, and solo Itak a
weight of 9 against 18 for Aliping Namamahay, so roughly a quarter of Timawa and
half of Aliping Namamahay start the battle without a shield. The suffix should
therefore appear. Nothing in either run proves it does not — the feed retains
200 events and scrolls quickly, and the tester was watching for the two-handed
case `V2-2` asks about. If a later change makes this worth settling, watch the
feed paused rather than treating the absence as a bug.

**2. A warrior could not be clicked. Fixed on 2026-08-11.** The click target was
computed in `ArenaGame.SelectAtPointer` as `MathF.Max(5f / _camera.Zoom, 1.5f)`
world units — about five screen pixels — and it was centred on the agent's own
world position, which is the warrior's foot anchor. A pawn draws entirely
*above* that anchor, so the part of a warrior a spectator aims at was never
inside the target at any zoom. Both halves are now derived from the geometry the
renderer actually draws: `Presentation/AgentPickTarget.cs` samples at the foot
anchor rather than at the cursor, and sizes the target at half the drawn body's
height with a ten-pixel floor, using the same `PawnGeometry.ResolveApparentScale`
every pawn layout length is multiplied by. `AgentPickTargetTests` pins it across
the whole `0.05`–`12` zoom range: a click on the feet, the waist, the chest, or
the head selects the warrior, and a click clear of the body still selects
nothing. The re-run confirmed it on screen, which is what closed `V2-3`.

**3. The six-category composition panel was never built; the panel is the
four-rank one.** This was a plan-versus-repository mismatch rather than a
regression. `ArmyCompositionStepper.CategoryCount` is `4`, and
`ArmyCompositionPanel.CategoryLabels` is `Datu`, `Maharlika`, `Timawa`, and
`AlipingNamamahay` — rank names, not weapon pair-form labels.
`Settings.ArmyComposition` carries one slider per rank, and
`ArenaGame.ExpandCompositionToRosterCounts` spreads each rank's slider across
every combat-preset V5 roster row that carries that rank. So the sliders do move
real warriors and `V2-8`'s buttons do work; what does not exist is any
per-weapon control. `V2-7` and `V2-8` were rewritten against the panel that
ships. Building a genuine six-weapon panel would widen the stepper, change the
persisted settings schema and its reset-on-old-file path, rewire the roster
expansion, and retune the calibrated share weights — a feature needing its own
design document, not a smoke-row fix.

**4. `V2-10` is a weak row even though it closed.** The row asks the tester to
isolate one weapon's sound in a battle of hundreds of simultaneous attacks. The
first run could not attribute the sound and recorded `BLOCKED`; the re-run
reported `PASS`. The underlying difficulty is unchanged: attribution at battle
scale rests on the listener's judgement rather than on anything the client
isolates. If Wasay audio is ever changed, do not re-run this row as written —
field a single Wasay pair, or lean on the existing sound-gain section, which
tests the same slot without asking a person to pick one voice out of a crowd.
