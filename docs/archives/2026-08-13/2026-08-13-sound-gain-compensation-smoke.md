# Sound gain compensation smoke — closed 2026-08-13

**Archived: reference only.** All eight rows below are `PASS` and were moved out
of `docs/development/smoke-checklist.md` on 2026-08-13, the day they closed.
Nothing here is outstanding and nothing here is an instruction.

The family closed in full. Every row added by the sound gain compensation
change was attempted by a person at an interactive Windows desktop on this date
and every one passed, with no finding left behind and no row reopened. Do not
re-run any row from this file. If a later change touches voice allocation,
gain compensation, or the sound log, write a fresh row in the live checklist
rather than reviving one of these.

| Field | Value |
| --- | --- |
| Rows | 8 |
| Source family | 1 |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not captured by the tester. The reporting machine for this repository's recent runs is Windows 11 Pro 10.0.26200 x64 with an NVIDIA GeForce RTX 4070 SUPER on a 2560x1440 display at 125% Windows scaling |
| Source commit | Not captured by the tester. `main` was at `8da5d92` when these results were transcribed |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## What these rows were for

The measured, non-interactive half of the sound gain compensation change is in
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`, and the superseded runs are in
`docs/development/measurement-history.md`. Those numbers prove the voice budget
and the gain curve behave arithmetically. They prove nothing about whether a
busy melee actually sounds clean to a person with working speakers, which is
the only thing the eight rows below were ever for. That gap is now closed by
observation rather than by measurement.

## Sound gain compensation smoke

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 82. Hear a busy melee without distortion | Let a 200-agent battle reach its densest fighting at normal speed. Blows stay individually distinguishable. There is no continuous rasp, crackle, or buzz underneath the fighting, and no moment where the sound seems to break up or drop out. | The tester at the desktop reported: "passed". | PASS |
| 83. Compare a duel with a melee | The final one-on-one survivors sound clearly louder per blow than the same weapon does in the middle of the melee. The change is gradual as the fight thins out, not a sudden jump. | The tester at the desktop reported: "passed". | PASS |
| 84. Watch the voice count and gain react | Open the sound log with `F9`. During heavy fighting `VOICES` climbs into the tens and `GAIN` falls well below 0.65; as the battle thins both recover, and `GAIN` returns to `0.65` once nothing is sounding. | The tester at the desktop reported: "passed". | PASS |
| 85. Confirm nothing is being limited | Through a full 200-agent battle at normal speed, the sound log shows no `LIMITED` row and no `REFUSED` row. | The tester at the desktop reported: "passed". | PASS |
| 86. Check 4x speed | At 4x the audio stays clean and undistorted, `VOICES` climbs higher than at 1x, and `GAIN` falls further. Still no `LIMITED` or `REFUSED` rows. | The tester at the desktop reported: "passed". | PASS |
| 87. Confirm mute still works | Toggling `MUTE` silences everything immediately and unmuting resumes without a burst of backed-up sound. | The tester at the desktop reported: "passed". | PASS |
| 88. Confirm a new round starts at full gain | After a match ends and a new one starts, the first blow of the new battle is at full volume rather than carrying the previous battle's reduction. | The tester at the desktop reported: "passed". | PASS |
| 89. Confirm the header stays readable | The `VOICES n GAIN 0.nn` text in the sound log header does not overflow its panel, overlap the `MUTE` button, or clip at any of the six themes. | The tester at the desktop reported: "passed". | PASS |

## What a later reader should be careful of

Row 83 is the one row here whose result depends on the endgame reaching a
genuine one-on-one exchange. It did on this run, and the tester heard the per
blow gain rise as the fight thinned. If a later change alters how a battle ends
— how survivors converge, or how many warriors are still standing at the end —
that change alters the conditions row 83 passed under, and the honest response
is a fresh row rather than a citation of this one.
