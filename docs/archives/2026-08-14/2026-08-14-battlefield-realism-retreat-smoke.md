# Battlefield realism cohort and retreat smoke — rows BR-5 to BR-9 closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

This is a partial record. Five of this family's ten rows closed; five did not,
and those five stay in the live checklist where a reader will see them. The
family is **not** finished, and its section has **not** been deleted from the
checklist.

| Field | Value |
| --- | --- |
| Rows in the family | 10 (`BR-1` through `BR-10`) |
| Rows closed here | 5 — `BR-5`, `BR-6`, `BR-7`, `BR-8`, `BR-9` |
| Rows still open in the live checklist | 5 — `BR-1`, `BR-2`, `BR-3`, `BR-4`, `BR-10` |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Interactive Windows desktop. Not otherwise recorded |
| Source commit | Not recorded. The working tree at the time was `7036490` plus uncommitted documentation changes |
| Launch path (`source` or package path) | `./scripts/run.ps1` (source). Not otherwise recorded |
| Optional screenshot paths | None recorded |

## What these five rows were for

The family was added by the battlefield realism change, whose design document is
`docs/plans/2026-08-11-battlefield-realism-design.md`. That change did two
separable things: it grouped deployment by weapon cohort and pushed shield
bearers toward the forward-most slots of their own contingent, and it gave a
ranged warrior a retreat ladder so that a melee enemy closing inside its standoff
distance makes it back away rather than stand and shoot.

The five rows closed here are the **retreat half** of that change. Every one of
them asks about a ranged warrior's behaviour once a melee enemy is on top of it,
or about the inspector text that names the state it is in. None of them asks
about deployment shape, which is the half that did not pass.

The automated suites proved the threat-radius arithmetic, the retreat ladder's
three rungs, and a twenty-seed termination sweep. What they could not prove, and
what these five rows existed to settle, is whether a back-pedalling shooter reads
as a warrior deliberately backing away rather than as one fleeing or stuck, and
whether the battle still ends rather than stalling into a perpetual retreat.
A person watched, and it does.

## The rows, as they read when they closed

| # | Step | Expected observation | Result |
| --- | --- | --- | --- |
| BR-5 | Watch a ranged warrior (Bangkaw, Busog, or Arquebus) whose standoff distance a melee enemy closes inside | The ranged warrior visibly backs directly away from the closing melee enemy rather than holding its ground and continuing to fire. Failure is the ranged warrior standing still and shooting as the melee enemy closes to contact, indistinguishable from its behaviour before this change | PASS |
| BR-6 | Watch a ranged warrior that is backing away from a melee enemy until it is stopped by the map edge or a corner | Once cornered, the ranged warrior stops backing away and stands its ground rather than continuing to retreat in place or oscillating at the boundary. Failure is a cornered ranged warrior that appears to keep trying to back away indefinitely — visibly jittering, sliding along the edge, or kiting back and forth — instead of settling into a stationary hold | PASS |
| BR-7 | Watch the same back-pedalling ranged warrior from BR-5 with an eye specifically toward how the motion reads, as distinct from whether it happens at all. This row had no automated proxy; it was a judgement call only a person watching the game could make | The retreat reads as a warrior deliberately backing away from a threat — facing the danger, moving with evident purpose — rather than as panicked flight, and rather than as a warrior stuck sliding against terrain or another agent | PASS |
| BR-8 | Watch a full battle between two rosters that each field ranged warriors, under V10, to its conclusion | The battle reaches a terminal outcome — one side is defeated or the tick limit is reached with a clear winner — rather than a ranged side backing away for the whole of the tick limit and the battle never resolving. Failure is a battle that visibly stalls, with the ranged side perpetually retreating and no side able to close and finish the fight | PASS |
| BR-9 | Click a ranged warrior that is backing away from a melee enemy, then click one that is holding at range with no melee threat nearby, and read both inspector panels | The two intent strings — "Backing away from close fighters" and "Holding at range" — are both legible at a glance and clearly distinct from each other; a spectator reading the inspector can tell which of the two states the warrior is in without needing to also watch the battlefield. Failure is either string being hard to read at the panel's default size, or the two strings reading as similar enough to be mistaken for each other | PASS |

No separate per-row note was recorded beyond the pass itself. The tester's only
qualifications on the session concerned the other five rows, which are reproduced
below because they explain why this record is partial rather than final.

## Why five rows stayed behind

Recorded here so the next reader of this file does not mistake a partial closure
for a finished family, and so the observations that were made survive even if the
live checklist is rewritten around them. **All five remain open in
`docs/development/smoke-checklist.md`; none of them is closed by this record.**

- **`BR-1` — cohort forms up.** Observed, and not accepted. The tester's words
  were that the contingents "visibly form up but not enough, some just charged
  and fought". The weapon-cohort grouping is therefore visible but the formation
  does not hold: individuals leave their group and engage before the group has
  finished forming. The row asks whether a contingent reads as mostly one weapon;
  the answer was that the grouping is there and the cohesion is not.
- **`BR-2` — shield bearers lead.** Observed, and not accepted. The tester
  reported that "some deployments have the shield bearers at the back", which is
  the row's own stated failure — a contingent whose leading edge is
  indistinguishable from an unshielded warrior's. It is *some* deployments rather
  than all, so whatever produces it is conditional rather than a flat inversion.
- **`BR-3` — shield bearers absorb the opening blows** and **`BR-4` — the two
  deployments read as positionally equivalent.** Deliberately not attempted. Both
  are downstream of the deployment shape that `BR-1` and `BR-2` found wanting, so
  attempting them against the shape as it stands would have measured a known
  defect rather than the thing the rows are for.
- **`BR-10` — the inspector panel at the minimum window size.** Observed, and not
  accepted, but for a different fault than the row anticipated. The row was
  written to catch *vertical* clipping — a 953-pixel panel running off the bottom
  of a 1024×720 window. What the tester found instead was horizontal: the panel
  renders, but the text lines overextend the panel's width. The row's own
  expected observation covers this ("without clipping against the window edge"),
  so it fails as written; the cause is simply not the one the row's author had in
  mind.

## What this record does not say

It does not say the battlefield realism change is finished, and it does not say
the retreat ladder is correct in any case a person did not watch. Five rows
passed at one interactive desktop on one day against one working tree. That is
what a smoke row is worth and it is not worth more.

It also makes no claim about the deployment half of the change. The rows that
would have settled it are the five listed above, and four of them were never
attempted at all.
