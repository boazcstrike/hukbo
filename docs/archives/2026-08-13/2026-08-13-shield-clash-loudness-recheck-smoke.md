# Shield-clash loudness re-check smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file is outstanding and nothing in it is an instruction.

This record closes the family whole. `docs/development/smoke-checklist.md` held
a section titled "Shield-clash loudness re-check (2026-08-13)" with two rows,
`SCL-1` and `SCL-2`. Both passed on 2026-08-13 and the section was deleted the
same day.

| Field | Value |
| --- | --- |
| Rows | 2 |
| Source family | 1, closed whole |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## Why these two rows existed at all

They were not a new feature's rows. They were owed by a fix.

The shield-clash audio family — rows 172 through 176 — closed whole earlier the
same day, and its record is the 2026-08-13 archive titled **"Shield-clash audio
smoke — closed 2026-08-13"**. Row 173 of that family failed on its first listen
with the tester's verdict "i cannot distinguish, sounds the same for most". The
measured cause was that the sixteen melee clash takes on disk were never
level-matched: the spread between takes inside one slot was wider than the
spread between the four slots, so which take was drawn decided how loud a block
sounded rather than which weapon struck.

The fix normalises every melee clash take in the sample domain at load, toward a
reference peak of `0.85`, and gives the four melee clash slots their own
relative level and pitch offset on top of that.

Two of the rows that had already closed, 172 and 175, had passed *against the
loudness that fix replaced*. Under the live checklist's own rule, a change that
touches what a closed row tested owes fresh rows rather than a revival of the
lifted ones. `SCL-1` and `SCL-2` are those two fresh rows. They are not a re-run
of 172 and 175 and must not be read as one.

## Shield-clash loudness re-check — closed rows

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SCL-1 | Listen to a shield-blocked blow after the loudness normalisation | It still sounds like a weapon striking a light wooden board, and it is still plainly different from a landed cut, without reading the event log to find out which resolution occurred. Failure is a normalised block now reading as a landed hit, or as a click, a thump, or clipped noise rather than as wood | Passed 2026-08-13 | PASS |
| SCL-2 | Run a full 200-agent battle with the shield cue audible | The clash cue does not become a wall of noise now that the previously inaudible takes play at full reference level, and the cue log shows no `LIMITED` or `REFUSED` row for any clash slot. Failure is a continuous clatter in which individual blocks stop being separable, or a clash slot appearing as `LIMITED` or `REFUSED` where it did not before | Passed 2026-08-13 | PASS |

## What these rows proved

`SCL-1` is the evidence that lifting the quiet takes did not change what a
blocked blow *is* to a listener. The concern the row existed to catch was that
normalisation would push a take into clipping or flatten its attack, so that a
block stopped reading as wood and started reading as a landed cut, a click, or
noise. It did not.

`SCL-2` is the density evidence at the new levels. This is the row the design
document flagged as the real risk, and it is worth being precise about why. The
change lowers the loudest possible clash cue — `0.85 × 1.00 × 0.65 = 0.5525`
against the `1.0 × 0.65 = 0.65` a full-scale take could reach before — but it
raises the quiet end a great deal, because the quietest take on disk peaked at
0.096 and now reaches the same reference as every other take. Blocks a listener
previously did not register as blocks at all are now audible, so many
simultaneous clashes are louder in aggregate than they were even though any one
of them is capped lower. `SCL-2` is the check that this did not turn a
200-agent battle into a continuous clatter, and it passed.

## What this record does not prove

It does not prove that a listener can tell the four melee clash slots apart by
ear. That was row 173 of the earlier family, and row 173 closed `PASS` on the
tester's own judgement that the sounds are acceptable rather than on a
demonstration that the four read as four weapons — the earlier record's section
"How row 173 closed" says so plainly and is the section to read before citing
that pass. `SCL-1` and `SCL-2` were never asked that question. They were asked
whether the fix broke anything, and the answer to that is no.

Regenerating the sixteen clash takes with consistent generation parameters
remains the better long-term answer to the timbral half of row 173. It spends
ElevenLabs credits and remains unauthorised.

## Provenance

The row wording in the table above was read from
`docs/development/smoke-checklist.md` before the section was deleted. The
verdicts come from the person at the interactive desktop on 2026-08-13, who
reported both rows as `PASS`. No agent flipped either row, and no automated
test was treated as evidence for either: the canonical gate never opens an audio
device.

The build these rows were run against is the one recorded in
`docs/development/testing.md` as "Canonical gate result — Hukbo, 2026-08-13
(shield-clash audio legibility)", whose four seed-1 workloads are
byte-identical to the baselines they had before the audio change — the required
result for a presentation-only change.

The design and plan documents behind the fix are archived under the titles
**"Shield-clash audio legibility — design"** and **"Shield-clash audio
legibility — plan"**. The plan's section "How this actually closed, 2026-08-13"
records where the work diverged from what was planned.
