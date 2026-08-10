# Camera auto-pan smoke — completed

**Archived: reference only.** This section was removed from
`docs/development/smoke-checklist.md` on 2026-08-11, the day its last row
closed. All five rows are `PASS`; nothing here is outstanding and nothing here
is an instruction.

Nothing in the repository links to this file. The live checklist holds open
work only and does not point at `docs/archives/`, which is deleted
periodically. Do not add a link back to it, and do not re-run these rows from
this file.

---

## Camera auto-pan smoke

Added by the camera auto-pan change. The unit tests proved the targeting and
state-machine decisions; only a person watching a live window could say whether
the resulting camera motion was helpful rather than distracting. That is what
these five rows were for, and the answer was yes.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `777ac13` |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 53. Confirm the camera holds still during a visible fight | Zoom in on an engagement so fighting fills the screen. The camera stays exactly where it was left for the whole engagement; it never creeps, drifts, or re-centres on its own while anyone on screen is fighting. | 2026-08-11, tester at the desktop: passed. | PASS |
| 54. Watch the camera find a fight it lost | Zoom in, then pan away until no fighting is on screen. Within a moment the camera slides on its own toward the nearest melee, slows as it arrives, and stops with the fighting comfortably inside the view rather than pinned to an edge. | 2026-08-11, tester at the desktop: passed. | PASS |
| 55. Confirm zoom never changes | Through several auto-pans, the zoom level is exactly what the spectator set. The camera only slides; it never zooms out to find the fight or zooms in on arrival. | 2026-08-11, tester at the desktop: passed. | PASS |
| 56. Take control back | While the camera is auto-panning, hold a pan key. Motion stops under the spectator's hand immediately, the camera goes exactly where they steer it, and it does not resume on its own for a couple of seconds after the key is released. | 2026-08-11, tester at the desktop: passed. | PASS |
| 57. Watch the end of a long battle | Let a match run to its final few survivors at a zoom where they leave the screen. The camera follows the fighting to the end instead of leaving the spectator on empty ground, and it stands still once the match summary appears. | 2026-08-11, tester at the desktop: passed. | PASS |

**What these rows do not cover.** The later auto-camera hysteresis and mode
setting — the grace period, the dwell, mid-journey re-targeting, and the
`Off` / `Assisted` / `Follow` selector — are rows 149 to 155, a separate family
that is still open in the live checklist. These five rows are the baseline
motion behaviour underneath that setting and say nothing about the modes built
on top of it.

If a later change touches auto-pan targeting, camera easing, or the
take-control-back grace, write fresh rows in the live checklist rather than
re-running the rows above.
