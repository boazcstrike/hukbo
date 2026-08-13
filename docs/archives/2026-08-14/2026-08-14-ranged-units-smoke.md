# Ranged units smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file overrides either of those.

This record lifts the whole of the "Ranged units smoke (ranged-units package)"
family, eleven rows numbered `RG-1` through `RG-11`, out of
`docs/development/smoke-checklist.md`. The family was added by the
ranged-units package, which adds three ranged weapons — the Bangkaw
(`Bangkaw — Long Spear`, thrown), the Busog (`Busog — War Bow`), and the
Imported Arquebus (a matchlock, carrying the `IMPORTED` badge rather than a
Filipino pair-form label because no source ties the weapon to a Philippine
name) — together with a hitscan projectile that carries a flight time, a
five-phase draw/load/release/recover cycle, a movement rule that holds a
ranged warrior at its preferred distance instead of closing to melee, and
thirteen new sound slots split across the three weapons.

All eleven rows ran and passed on 2026-08-14, by a person at an interactive
Windows desktop, launched from source through `./scripts/run.ps1`. The family
closed 11 of 11 and the whole section was deleted from the live checklist the
same day.

| Field | Value |
| --- | --- |
| Rows | 11 |
| Source family | 1 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

## Evidence — 2026-08-14 closing run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

No per-row written observation was captured for any of the eleven rows. Every
row below closed on the person's judgement at the desktop alone.

## What the automated evidence already established, before this run

The automated suites proved the countdown resolves on the correct tick, that
the state and event hashes move only for a ruleset that fields a ranged
weapon, that `AgentIntent.Holding` and a rejected route are written by
independent code paths, and that the pose geometry and the inspector strings
are wired and tested in isolation. None of that proved any of it reads
correctly to a person watching the screen, which is what the eleven rows below
were for.

A separate measurement pass on 2026-08-13, ahead of this smoke run, closed two
things that had previously stood between these rows and a person attempting
them. First, whether a battle with ranged warriors on the field ever advanced
at all: the `Hukbo.Tools.RenderProbe` harness was run against a clean worktree
of `main` at `653d3fa`, seed 1, three camera stations, 150 sampled frames
each, and the battle advanced to tick 3,584 with 27 and 23 warriors still
alive and falling, the debug log carried zero `err` lines, and all 16,794
audio cues in it report `Played`. Every one of the thirteen ranged sound
slots fired in that run. Second, whether the sixty ranged sound files existed
on disk: they did, and `./scripts/sfx.ps1 -List` reported zero missing slots.
Neither of those facts is itself a smoke result — a measurement harness
driving playback is not a spectator, and proves only that the code path
executes and that a cue reached the mixer — but both facts are what made the
eleven rows below attemptable rather than blocked.

The shipped client pairs `CombatPresetId.PrecolonialPhilippinesV5` with
`MovementPresetId.LastStandEngagementV11`, not with `RangedStandoffV8` as the
ranged-units plan describes and not with `BattlefieldRealismV10` as two of the
rows below were amended to say. `LastStandEngagementV11` carries
`BattlefieldRealismV10`'s holding and backing-away rules forward unchanged, so
the two amended rows were attempted against a later movement preset than the
one named in their own text, and read the same either way.

## Ranged units smoke

The rows below are reproduced as they stood in the live checklist. The
`Actual` column was empty there for every row and stays empty here, because no
per-row written observation was captured when the family closed.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| RG-1 | Watch a Bangkaw, Busog, or Arquebus warrior fire at a target several world units away, at default zoom | A projectile is visibly drawn traveling from the launcher toward the target and exists on screen for multiple ticks before impact, rather than the target reacting the instant the release plays. Failure is a shot that resolves with no visible projectile at all, or one that appears to teleport instantly from launcher to target | | PASS |
| RG-2 | Listen to one ranged shot from release to impact, at default zoom and 1x speed | A release cue plays at the launcher, then a separate impact or miss cue plays at the target after a perceptible gap, and that gap reads as the shot's flight time rather than as two disconnected sounds. Failure is the two cues sounding simultaneous, the gap sounding random rather than distance-related, or only one of the two cues playing | | PASS |
| RG-3 | Watch one Bangkaw warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a spear being thrown: the shaft draws back past the shoulder during Draw, then releases forward and returns to a neutral carry during Recover. Failure is a Bangkaw sequence that reads as a generic swing, or one that shows no visible change in weapon angle across the five phases | | PASS |
| RG-4 | Watch one Busog warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a bow being drawn: the bow stave holds out from the body while the string hand draws back toward the cheek during Draw, then both return toward Ready after Release. Failure is a Busog sequence indistinguishable from the Bangkaw's throwing motion, or one that shows no build-up of draw tension before Release | | PASS |
| RG-5 | Watch one Imported Arquebus warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a matchlock being fired: the weapon is shouldered and levelled, held on target through Release rather than swept quickly, with a long barrel plainly visible out in front of the warrior. Failure is an Arquebus sequence that reads as a spear or a bow, or one indistinguishable from the other two ranged weapons at a glance | | PASS |
| RG-6 | Amended by the battlefield-realism change (`BattlefieldRealismV10`). Watch a ranged warrior (Bangkaw, Busog, or Arquebus) approach its standoff distance from a target during an advance, alongside melee warriors closing on the same line, and separately watch one that has a melee enemy close on it | While no melee enemy is inside its threat radius, the ranged warrior visibly halts and holds its position once it reaches range, while melee warriors on the same approach keep walking forward and pass it — this is now only the unthreatened case. Once a melee enemy closes inside the threat radius, the ranged warrior instead backs directly away from that enemy rather than holding still, continuing until it is clear of the threat again or is stopped by the map edge. Failure is the ranged warrior continuing to close all the way to melee range like its comrades, halting at a point indistinguishable from where a melee warrior would stop on its own, or standing still once a melee enemy is inside the threat radius instead of backing away | | PASS |
| RG-7 | Amended by the battlefield-realism change. Click a ranged warrior that has halted at its standoff distance with no melee enemy nearby, and separately click one that is currently backing away from a melee enemy | The unthreatened warrior's inspector reads "Intent: Holding at range". The threatened, backing-away warrior's inspector instead reads "Intent: Backing away from close fighters" — a second, distinct intent string that did not exist before this change — and switches back to reading "Intent: Holding at range" once the warrior is cornered by the map edge and can no longer retreat. Neither warrior's inspector ever reads "Blocked" or any other movement-refusal wording. Failure is either warrior's inspector showing "Blocked" — the movement row's own wording for a warrior whose route was rejected — or a cornered, retreat-blocked warrior continuing to read "Backing away from close fighters" instead of falling back to "Holding at range" | | PASS |
| RG-8 | Watch and listen to a ranged shot that resolves as a miss rather than a landed hit | A miss cue plays instead of the ordinary flesh-impact cue used for a landed blow. Failure is a missed shot playing the same body-hit sound as a hit would, or playing no sound where a miss cue exists for that weapon | | PASS |
| RG-9 | Compare a Bangkaw, a Busog, and an Arquebus warrior side by side at the High, Medium, and Low detail tiers, from a close-up zoom down to fully zoomed out | At every tier the three ranged silhouettes are distinguishable from each other and from the four existing melee silhouettes — the Bangkaw reads as spear-armed, the Busog as bow-armed, the Arquebus as carrying a long firearm. Failure is any two of the three collapsing into the same silhouette at the Low tier, or a ranged warrior being mistaken for a melee warrior at any tier | | PASS |
| RG-10 | Watch and listen to a battle fielding all three ranged weapons for several minutes | The Arquebus fires far less often than the Bangkaw or the Busog, matching its much longer authored shot interval, and each Arquebus shot is audibly louder and more distinctive than a Busog release or a Bangkaw throw — a spectator should be able to tell an Arquebus has fired without seeing which warrior fired it. Failure is the Arquebus firing at a cadence similar to the other two ranged weapons, or its report sounding unremarkable next to theirs. The firing-cadence half of this row does not depend on sound and can be attempted once RG-1 is attemptable | | PASS |
| RG-11 | Watch a Bangkaw or Busog shot whose flight path passes through or near a friendly warrior standing between the launcher and the target | **This row has no pass/fail criterion; it is an open question, not a check.** Phase 1 deliberately implements no friendly fire and no line of sight — a projectile resolves as a pure distance-and-timer hitscan against its chosen target, with nothing checked about who or what stands between launcher and target — and that gap is deferred to Phase 2 by design, not an oversight to correct here. Record in `Actual` whatever was actually observed: does the projectile visibly passing through the friendly warrior look wrong to a spectator, or does it go unnoticed at the pace and scale of a real battle? This is the one Phase 1 effect a spectator cannot discover for themselves through any other row above, which is why it needs a person to look at it deliberately rather than being inferred from the others | | PASS |

## RG-11 closed without answering its own question

Ten of these eleven rows are ordinary pass/fail checks, and closing them
`PASS` records that the expected observation held. `RG-11` is not that kind of
row. Its own text says so directly: it carries no pass/fail criterion at all,
and it exists to make a person deliberately watch one specific Phase 1 gap —
a projectile that resolves as a pure distance-and-timer hitscan, checking
nothing about who or what stands between the launcher and the target — and
write down whether a friendly warrior standing in a shot's path looks wrong to
a spectator or goes unnoticed at the pace of a real battle. That is a
question, not a threshold to clear.

The row was nonetheless closed `PASS`, alongside the other ten, with no
written observation recorded in its `Actual` column. The question the row was
written to answer therefore was not answered in this record. A "pass" on a row
with no pass/fail criterion says only that a person looked at it and moved on;
it does not say the projectile passed through a friendly warrior cleanly, and
it does not say it looked wrong either. Phase 1 deliberately implements no
friendly fire and no line of sight, and that gap is deferred to Phase 2 by
design — this closing does not change that gap or shrink it. A later session
that actually needs the answer to RG-11's question — whether a friendly
warrior standing in a projectile's path reads as a visible defect to a
spectator — has to write a fresh row and watch for it deliberately; nothing in
this closed record answers it.

## What this pass does and does not prove

The verdict recorded on 2026-08-14 is a pass on eleven rows, and nothing more
than that. No machine identification, source commit, screenshot, or written
per-row description of what was seen was captured with the run; those fields
are left as "Not recorded" above rather than reconstructed after the fact.
Each row's own criterion was judged satisfied by the person watching and
listening at the desktop, and that judgement is the entire evidence this file
carries for `RG-1` through `RG-10`. For `RG-11`, as the section above states,
even that much is missing: the row closed without the observation it exists to
capture.

This pass says nothing about behaviour under any movement preset other than
`MovementPresetId.LastStandEngagementV11`, the one the client selects by
default and the one `RG-6` and `RG-7` were actually attempted against. It says
nothing about friendly fire or line of sight, which Phase 1 does not
implement. And it says nothing beyond what a person watching a single session
can attest to — it is not a substitute for the automated suites that prove the
countdown timing, the hash boundaries, and the wiring in isolation, and it is
not proof against a regression in a build that ships after this one.

## Where the plan and the design live

The documents behind this work stay in `docs/plans/` rather than joining this
archive batch: `2026-08-07-ranged-units.md`, the task plan;
`2026-08-07-ranged-units-design.md`, the design; and
`2026-08-09-ranged-units-handoff.md`, the mid-package handoff record. They stay
live because source and test files cite them by path, and because
`docs/plans/README.md`'s own rule keeps a source-cited document in that folder
however finished the smoke rows against it are.
