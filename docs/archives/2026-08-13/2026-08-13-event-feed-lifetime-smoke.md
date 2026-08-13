# Event feed lifetime smoke (T17) — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`;
nothing in this file overrides either of those.

This record exists because `docs/development/smoke-checklist.md` holds open
work only, not a history of what has already been tested. Once a family has
no open row left, that file's own rule is to delete the family from it
outright rather than let it accumulate closed rows. The "## Event feed
lifetime smoke (T17)" family closed in full on this date, and this file is
where its three rows, 99 through 101, went when they were lifted out.

## What these rows were for

Covers the change recorded under T7 of the Arch-informed performance
hardening plan: `LastEvents` now returns one of two permanent
double-buffered collections instead of a fresh one created each tick. The
automated tests — the seed-1 hash equality above,
`LastEventsRemainsACompletedTickSnapshot`,
`RetainedLastEventsReferenceIsNotValidPastTheProducingTick`, and
`BattleEventFeedTests.Ingest_CopiesEventValuesRatherThanRetainingTheSourceBuffer`
— prove the buffer contract and the copy-out behavior in isolation; none of
them prove that a spectator watching the live feed on screen ever sees the
effect of the changed lifetime. These three rows were the only rows this
workstream added to the checklist. They existed because T7 changed the
lifetime of the collection `LastEvents` returns, and only a person at an
interactive Windows desktop could flip one of them to `PASS`. All three were
run and passed on 2026-08-13.

## Evidence

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 11 Pro 10.0.26200 x64 |
| Source commit | `8da5d92`, repository head at the time of the run. The working tree also carried uncommitted changes from a parallel session, so the run was head plus those. |
| Launch path (`source` or package path) | `source`. `./scripts/run.ps1` is the only supported source launch; the tester did not separately record it. |
| Optional screenshot paths | None recorded |

## Rows

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 99. Watch the battle event feed during a live run | Events appear correctly and in the correct order for the whole run; nothing is missing, duplicated, or out of sequence. | 2026-08-13, tester at the desktop: PASS. | PASS |
| 100. Pause, resume, and change speed repeatedly during a run | The feed survives every pause and every speed change without losing or duplicating a single entry. | 2026-08-13, tester at the desktop: PASS. | PASS |
| 101. Let a battle run to its end | Once the battle ends, the feed shows nothing stale left over from the last live tick. | 2026-08-13, tester at the desktop: PASS. | PASS |

## If the event feed's buffer lifetime changes again

A later change to the lifetime, ownership, or buffering strategy of
`LastEvents` should not revive these three rows. It should add fresh rows to
the live `## Event feed lifetime smoke` section (or a newly named
equivalent) in `docs/development/smoke-checklist.md`, get them run by a
person at an interactive Windows desktop, and only then let this archive
stay untouched as history.
