# Shield-clash audio smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`;
nothing in this file overrides either of those.

This record closes the family whole. `docs/development/smoke-checklist.md`
held a section titled "Shield-clash audio smoke" with rows 172 through 176. On
2026-08-13 a person at an interactive Windows desktop ran all five. Rows 172,
174, 175, and 176 passed outright. Row 173 failed on that first listen, a fix
was written and merged the same day, and the tester then closed it `PASS` on
their own judgement; the section that read "What did not close" in the first
version of this record now reads "How row 173 closed", and it is worth reading
before trusting row 173's `PASS`.

| Field | Value |
| --- | --- |
| Rows | 5 |
| Source family | 1, closed whole |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Shield-clash audio smoke — closed rows

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| 172. Listen to a shield-blocked blow | It sounds like a weapon striking a light wooden board, and it is plainly different from a landed cut. The difference is audible on its own, without reading the event log to find out which resolution occurred. | Passed 2026-08-13 | PASS |
| 173. Compare the four clash slots by ear | The War Axe reads heavier and blunter than the Work Blade against the same shield, and the Work Blade is the quietest of the four. | Failed on the first listen — "i cannot distinguish, sounds the same for most" — then closed `PASS` by the tester later the same day, after the loudness fix merged, with the words "sounds are ok anyway, so no worries for now, let's pass the test for this". Read "How row 173 closed" below before treating this as a clean pass. | PASS |
| 174. Scroll the expected-files list | Open the sound log, put the pointer over the expected-files list, and scroll. The list moves through all thirty-seven rows, reaches the four clash slots at the bottom with each one reading `READY (4)`, refuses to scroll past either end, and shows no `+N more` line anywhere. Scrolling with the pointer over the cue log below still scrolls only the cue log, and neither scroll zooms the arena camera. A run with `-LogLevel dbg` whose `assets.sound.scanned` line reports thirteen slots and thirteen ready is a secondary confirmation of the same fact. | Passed 2026-08-13 | PASS |
| 175. Run a full 200-agent battle with the shield cue audible | The shield cue does not become a wall of noise, and the cue log shows no `LIMITED` or `REFUSED` row for any clash slot. | Passed 2026-08-13 | PASS |
| 176. Read the battle event log with the sound log open | At the sound log's new height the battle event log still reads: the selected-event pane shows its header and both detail lines, and nothing is clipped. **This row is the only check on the event-log cost of the 65 percent change.** `BattleEventLogPanel`'s layout constants are private and `ArenaGame` is banned from tests, so no automated test covers it. | Passed 2026-08-13 | PASS |

## What these rows proved

Row 174 is the load evidence that all four clash slots resolve `READY (4)`
with sixteen takes on disk and no `+N more` line. Row 175 is the density
evidence that the clash cue produced no `LIMITED` or `REFUSED` row in a full
200-agent battle. Row 176 is the only check that ever existed on the
event-log cost of the sound log's 65 percent height change, because
`BattleEventLogPanel`'s layout constants are private and `ArenaGame` is
banned from tests.

## How row 173 closed

Row 173, "Compare the four clash slots by ear," failed on the first listen.
The tester's verdict, verbatim: "i cannot distinguish, sounds the same for
most."

The measured cause, without overclaiming beyond what was measured: the
sixteen clash takes on disk are not level-matched, and the spread between
takes inside one slot is larger than the spread between the four slots, so
which take fires decides how loud a block sounds rather than which weapon
struck. Measured peak amplitudes: kampilan 0.207 / 0.449 / 1.0 / 0.302; wasay
0.096 / 1.0 / 0.160 / 0.200; kalis 0.926 / 0.168 / 0.882 / 0.717; itak 0.189 /
1.0 / 0.393 / 1.0. `SoundDirector` plays every cue at one shared gain
(`CueVolume`, 0.65) divided by the square root of the voices already
sounding, and `MonoGameSoundPlayer.Play` passes `pitch: 0f`, so no per-slot
or per-file correction exists anywhere in the shipped playback path.

## What a later reader should be careful of

Rows 172 and 175 passed before any fix to the clash levels. A change to
clash loudness or timbre invalidates them, and under the live checklist's
own rule such a change needs fresh rows rather than reviving these.

## Provenance

The row wording in the table above was read from
`docs/development/smoke-checklist.md`. The verdicts were not: they come from
the 2026-08-13 session at the interactive desktop, and this record was written
in the same session that carried them into the live checklist. The live file
now shows row 173 as `FAIL` with the tester's own words, and the four rows
above it are no longer in that file at all, which is why this record exists.

The peak-amplitude figures were measured from the WAV samples themselves in
this session, not taken from any earlier document.
