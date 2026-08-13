# Auto camera modes smoke — closed 2026-08-12

**Archived: reference only.** All seven rows below are `PASS` and were moved
out of `docs/development/smoke-checklist.md` on 2026-08-12, the day they
closed. Nothing here is outstanding and nothing here is an instruction.

The family closed in full — every row that had been added by the auto-camera
hysteresis and mode setting on 2026-07-28 was run and passed on this date.
Unlike the two families closed on 2026-08-11, it left something behind: the
tester's own report on row 149 named a second, real
problem that its stated criterion does not cover — where a pan ends up when
it starts from an empty, no-fight screen. That problem is recorded below as
Finding 1 and is now tracked as a fresh row, `AC-1`, in a new section of the
live checklist titled "Auto camera centring smoke". Do not re-run any row
from this file. If a later change touches auto-camera behaviour again, write
a fresh row in the live checklist rather than reviving one of these.

| Field | Value |
| --- | --- |
| Rows | 7 |
| Source family | 1 |
| Lifted on | 2026-08-12 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-12 |
| Machine/platform | Windows 11 Pro 10.0.26200 |
| Source commit | 72e61b1 |
| Launch path (`source` or package path) | `./scripts/run.ps1 -Configuration Debug` |
| Optional screenshot paths | None recorded |

## Auto camera modes smoke

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| 149. Watch a small skirmish without being dragged away | Zoom in on two or three warriors fighting each other, well away from the main battle. The camera stays put for the whole exchange. It does not lurch toward the main battle between blows, which is the defect this change exists to fix. | The tester at the desktop reported: "yes, this is good; but usually it is not centered; and fighting usually stays at the corner of the screens; we need to fix to center, not when a battle happens, only when panning from an empty on-fight screen." The row's own stated criterion — that the camera stays put through the whole exchange and does not lurch toward the main battle between blows — was met, so this row passes on its own terms. The centring complaint names a separate, real problem with where a pan ends when it starts from a screen holding no fighting at all, and it is recorded below as Finding 1 rather than folded into this row's status. | PASS |
| 150. Confirm the camera rests between pans | Pan away from all fighting and let the assistant take over. After it settles on a melee it stays still for a couple of seconds at minimum before any further motion, rather than immediately setting off again. | The tester at the desktop reported: "passed." | PASS |
| 151. Watch it track a fight that moves | Pan far from a running battle so the assistant starts travelling, and pick a moment when the front is shifting. The camera adjusts its heading mid-journey and arrives at where the fighting is now, not at empty ground the fighting has left. | The tester at the desktop reported: "passed." | PASS |
| 152. Find the setting in the menu | Open the menu. An `AUTO CAMERA` selector sits below `MOTION INTENSITY`, reads `Assisted` on a fresh install, and cycles `Off`, `Assisted`, `Follow` with the arrows, the mouse, and Left/Right while focused. Every menu control is still fully inside the panel, above the helper line. | The tester at the desktop reported: "passed." | PASS |
| 153. Confirm `Off` means off | Set the mode to `Off`, close the menu, and pan away from every fight. The camera never moves on its own, for the rest of the match. | The tester at the desktop reported: "passed." | PASS |
| 154. Confirm `Follow` keeps up | Set the mode to `Follow` and watch a battle. The camera re-centres on fighting noticeably sooner than in `Assisted`, and keeps the melee near the middle of the screen rather than letting it drift to an edge. | The tester at the desktop reported: "passed." | PASS |
| 155. Confirm the choice survives a relaunch | Set the mode to `Follow`, exit, and relaunch. The menu still reads `Follow` and the camera behaves accordingly from the first tick, without the menu being reopened. | The tester at the desktop reported: "passed." | PASS |

## Finding 1 — a pan ended with the fight in a corner

Row 149's own criterion — that the camera holds still through a nearby
skirmish rather than lurching toward the main battle between blows — was
satisfied, and the row passes on that basis. But the tester's report carried
a second observation the row was never written to catch: "yes, this is
good; but usually it is not centered; and fighting usually stays at the
corner of the screens; we need to fix to center, not when a battle happens,
only when panning from an empty on-fight screen." Read plainly, the complaint
is not about a pan interrupting a fight already on screen — the tester is
explicit that recentring during an ongoing battle is not what is being
asked for — it is about where the assistant leaves the camera the first time
it moves in from an empty, no-fight screen to find one.

The cause was a single shared constant. `SettleFraction` was doing two jobs
at once: it defined the on-screen band `Follow` mode uses to decide whether a
melee still counts as centred, and it also defined the band a pan is allowed
to stop inside once the camera arrives at a target. Both jobs read the same
value, 0.7 of the visible half-extents, so a pan was considered finished as
soon as the melee crossed into the outer seventy percent of the screen —
which is exactly the corner the tester described, not the middle.

The fix separates the two jobs. The old constant keeps its value of 0.7 and
its first job, and is renamed `FollowOnScreenFraction` so that the name says
which job that is: it governs `Follow` mode's own decision about whether a
melee needs recentring while a battle is already in view. A new, narrower
constant, `CenteredFraction`, at 0.2 of the
visible half-extents, now governs where a pan is allowed to end: a pan does
not report itself finished until the melee it is chasing has arrived within
that tighter band around the middle of the screen, so a fresh pan now
finishes near the centre rather than wherever it first crossed into frame.

That fix reaches only the pan-end condition. The pan-start gate — the check
that decides whether the camera needs to move at all — is untouched, and it
was never the thing the tester's report was about. A fight that is already
on screen, inside whatever band `Follow` or `Assisted` considers acceptable,
is still never re-centred by this change, exactly as the tester asked: recentring
is only for a pan moving in from an empty screen, not for a fight already in
view. That is a deliberate limit rather than an oversight.

The size of the defect was measured rather than estimated. A regression test
driving the controller against the old band leaves the camera 13.78 world
units from the melee on a 20-unit half-extent — sixty-nine per cent of the way
to the edge — where the same test against the new band requires 5 or less.
That number is the corner the tester described, in the controller's own units.

Row 149 is closed and stays closed; the finding is what reopens as a fresh row.
`AC-1`, in the live checklist's new "Auto camera centring smoke" section, asks
a person to pan away from every fight until the screen holds none, let the
assistant take over, and confirm the fixed pan-end band actually lands the
melee near the middle rather than in a corner — and, in the same run, that a
fight already on screen is still left alone.
