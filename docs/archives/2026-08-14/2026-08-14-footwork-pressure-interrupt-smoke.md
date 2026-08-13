# Footwork pressure interrupt smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

All eleven rows in this section were run by a person at an interactive Windows
desktop on 2026-08-14 and all eleven passed. This was the family's first and only
interactive run: not one of these rows had ever been executed before that day.
The section has been deleted from the live checklist.

Ten of the eleven rows are the movement V7 pressure interrupt rows, `P-1`
through `P-10`. The eleventh, `L-7`, is not a pressure interrupt row at all —
it belongs to the leader marker family and was moved into this section on
2026-08-13 because it was blocked by exactly the same missing preset selector.
Its pass here closes the leader marker family as well, whose other six rows were
run, passed, and lifted out on 2026-08-13 into the record titled **"Leader
marker and inspector smoke"**, named in prose rather than linked.

| Field | Value |
| --- | --- |
| Rows in the section | 11 — `P-1` through `P-10`, plus `L-7` |
| Rows closed here | 11 |
| Rows still open anywhere | 0 |
| Prior interactive runs | None. Every row below was executed for the first time on 2026-08-14 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `7036490` plus uncommitted documentation changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## Why these rows stood blocked for a week, and what unblocked them

Nine of the `P` rows, plus `L-7`, stood `BLOCKED` from the day they were written
until 2026-08-13. The obstacle was never a gap in V7's implementation — the
three spectator channels were built and unit-tested from the start. It was a gap
between the feature and the player: `ArenaGame.BuildScenario` overrode the
movement preset and the client exposed no way to choose another one, so a tester
had no supported route to a `EquipmentRelativeFootworkV7` battle or to an
`IndependentPursuitV1` one.

The staged movement-preset selector built on 2026-08-13 closed that gap. Its
design and plan are the two documents titled **"Pressure interrupt observability
— design"** and **"Pressure interrupt observability — plan"**, both archived on
2026-08-14 and named here in prose. That plan states plainly that it closes none
of these rows itself; its deliverable was that a person could finally attempt
them. The rows became runnable on 2026-08-13, and they became true on
2026-08-14.

The route the tester took is the one the checklist described: open the Army
Composition panel, choose the preset in the selector, apply it, and then perform
a **Full Reset**. The selector stages a preset for the next full reset rather
than changing the battle in progress, and a round started before the reset is
still running the previous preset.

## What the automated tests already proved, and what they did not

`FootworkPressureInterruptTests` covers the `ShouldPressureInterrupt` predicate
in isolation — the transition-only guard, each signal alone, saturation, and
threshold equality. `MovementStateHashTests` proves the version gate rather than
the field is what moves the two hashes. `ComboChainPressureInterruptTests` proves
an interrupted warrior's combination chain is cleared and its cooldown is
`AttackCooldownTicks`. `MovementViewProjectionTests` proves a V7 view carries
live pressure values and a V6 view carries the defaults.
`AgentInspectorContentTests` proves both new inspector strings and the panel
height arithmetic, and `PawnRendererTests` proves the break-off mark's placement
geometry against the leader mark and the selection ring.

None of that proved that a spectator watching a real battlefield at default zoom
can see a warrior peel out of a losing knot, that the break-off mark reads as
distinct from the leader mark and the dead mark at 1× speed rather than only in
placement arithmetic, or that the two inspector rows are legible at their shipped
colour and position. That is what the eleven passes below are.

## The rows that closed

The tester reported the section as a whole rather than row by row: all eleven
were run and all eleven passed, with no separate observation recorded for any
individual row. The `Actual` column below says exactly that and no more. Nothing
here should be read as a detailed finding that was never made.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| P-1 | Watch a V7 battle at default zoom and 1× speed | A warrior that breaks off under pressure shows the break-off mark above its head, and the mark is noticeable at 1× without pausing or zooming | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-2 | Watch a warrior that is losing a local fight — outnumbered, taking hits, allies dying around it | It visibly peels out of the knot, and a spectator can tell that it chose to disengage rather than that it died or was pushed. **This is the section 10 discoverability row: the effect must be readable without reading source code.** | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-3 | Find a warrior showing both the break-off mark and the leader mark | Both are visible at once and neither is hidden by the other | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-4 | Select a warrior showing the break-off mark | The selection ring, the leader mark where present, and the break-off mark are all legible together, none fighting for the same screen space | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-5 | Watch a warrior carrying the break-off mark as it is killed | The dead mark and the break-off mark do not merge into an unreadable smear on that warrior. The break-off mark is a short horizontal orange bar above the head, roughly two-thirds of the head's width and a sixth of its height, sitting one slot above the leader band; the failure to watch for is the two bands reading as one thick smudge, not their absence | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-6 | Click a warrior that has just broken off | The footwork row reads `Footwork: Disengaging (broke off under pressure)`, distinct from an ordinary `Footwork: Disengaging` | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-7 | Click any warrior in a V7 battle | The pressure row reads `Pressure: {value} of {threshold} basis points to break off`, and the value visibly moves as the warrior's local situation changes | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-8 | Click warriors carrying each of the six weapon rows | The ordering matches the shipped values — Kampilan and Wasay highest, Itak lowest. Two of the six thresholds are ties and a tie is the expected result: Kampilan 10 000 = Wasay 10 000, then shielded Kalis 8 750, then Kalis 7 500 = shielded Itak 7 500, then Itak 6 250 | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-9 | Compare an ordinary `Disengaging` warrior with a broken-off one | The two footwork rows are distinguishable at a glance, not only by careful reading | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| P-10 | Legacy regression: launch under a preset that does not apply the pressure interrupt | No warrior ever shows the break-off mark, and no inspector line ever carries the pressure row. This is the gating row: it proves the feature is off wherever it is meant to be off | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. | PASS |
| L-7 | Launch under `IndependentPursuitV1` | No warrior ever shows the leader mark, and no inspector contingent line ever carries `(leading)` | 2026-08-14, tester at the desktop. Run as part of the whole section; passed, with no separate note recorded. It is the leader marker family's gating row — the one that proves the mark is absent when it should be | PASS |

## The stale preset name in `P-10`, recorded rather than hidden

`P-10` was written naming `PersistentContingentsV4` and calling it "the one row
here that **is** runnable today, because V4 is the shipped default". That was
true when the row was written and had not been true for several presets by the
time the row was run: the client's shipped default is `LastStandEngagementV11`
with combat preset `PrecolonialPhilippinesV5`, both set explicitly in
`ArenaGame.BuildScenario`. The checklist's preamble already carried a correction
saying so.

What `P-10` actually tests is preset gating:
`EquipmentRelativeFootworkV7` is the only movement preset with
`AppliesPressureInterrupt = true`, and under any of the other ten all three
pressure-related `AgentView` members stay at their defaults, no break-off mark
is drawn, and no pressure row renders. The row is restated above in those terms.
It is worth knowing that the exact preset the tester selected for this row was
not recorded, only that the row passed.

## What a later reader should be careful of

- **The `Actual` column is deliberately thin.** The tester gave one verdict for
  the whole section. No agent may enrich these cells later; an invented
  observation is worse than a thin one. In particular, nobody wrote down which
  warrior broke off, what the pressure values read, or which six weapons were
  clicked for `P-8`.
- **These passes describe the build of 2026-08-14.** They are about the shipped
  colour, size, and position of the break-off mark and about two inspector
  strings. Retuning any of those invalidates them.
- **V7 is still not the shipped default, and these passes do not make it one.**
  Decision D6 keeps the default off V7 because V7 never passes the termination
  bar: a V7 battle is expected to run to its tick cap as a draw rather than
  resolve, and ending one manually is the normal way to finish a row here. The
  rows prove the spectator channels are legible, not that the preset is fit to
  ship.
- **The interrupt is rare by measurement.** The calibration record measures it
  firing on well under one per cent of agent-ticks, so a future re-run may have
  to watch for some time before seeing a break-off at all. A re-run that sees
  nothing is evidence about frequency, not automatically a failure of the mark.
