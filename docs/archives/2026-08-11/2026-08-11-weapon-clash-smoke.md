# Weapon clash smoke (preset V2) — completed

**Archived: reference only.** This section was moved out of
`docs/development/smoke-checklist.md` on 2026-08-11, the day its last row
closed. All twelve rows are `PASS`; nothing here is outstanding and nothing
here is an instruction. It is kept so that a later reader can trace why the
combat cadence and the attack-animation speed ceiling look the way they do,
and what a person actually saw before and after that change.

The live checklist is `docs/development/smoke-checklist.md`. Do not re-run
these rows from this file.

---

## Weapon clash smoke (preset V2)

**This family is complete: all twelve rows `PASS`.** It took two interactive
runs and a ruleset change between them.

The first run, on 2026-08-11 at commit `0c3f7f2`, passed eight rows and failed
three — CL-1, CL-3, and CL-7. None was a logic failure. Every effect rendered;
they simply overlapped each other densely enough that a spectator could not
attribute an individual one to an individual blow. The observer's own diagnosis
was that blows arrived too often, and it was right.

The combat cadence change answered it. `PrecolonialPhilippinesV6` restates V4's
tables and retunes only cadence and damage, so blows land roughly half as often
and hurt roughly twice as much at a near-constant damage per tick — the
artefact rate is the attack rate, so halving one halves the other without
changing how long a battle lasts. CL-7 turned out to be two defects wearing one
row and was split: CL-7a is the 1x cadence, and CL-7b is the 4x animation
compression, which no simulation change could have fixed because it was applied
on top of whatever cadence the simulation produced. That half needed
`AttackAnimationSystem.MaximumAnimationSpeedMultiplier`. See
[`../plans/2026-08-11-combat-cadence-v6-design.md`](../plans/2026-08-11-combat-cadence-v6-design.md),
section 4.

The second run, later the same day, passed all four re-opened rows. Both
observations are preserved in the `Actual` column of each, because a row that
records only its final state does not explain why the code looks the way it
does.

Rows marked with a dagger (†) are the ones that decide something about the
design rather than merely confirm it — see design section 3.8 for the recorded
disposition if the void-versus-landed row returns `FAIL`. Both returned `PASS`,
so that disposition was never needed.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11, two runs |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `0c3f7f2` for the first run; the four re-runs were at or after the combat-cadence change, `main` at `982bd6f` |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CL-1 | Watch the battle event feed for one exchange of each resolution | The five lines are distinguishable: a damage line for `Landed`, "stopped by the shield" for `ShieldBlocked`, "parried" for `Parried`, "turned aside" for `Deflected`, "stepped off the line" for `Evaded` | 2026-08-11 FAIL at `0c3f7f2`: not distinguishable in practice. The five wordings exist, but too many resolutions arrived at once for an observer to match a line to the blow that produced it. The combat-cadence change roughly halves the line rate; whether that is enough is what this re-run decides Re-run 2026-08-11 after the combat-cadence change: **PASS**. The observer's words: "now it is clearer". The five wordings are distinguishable at the halved line rate | PASS |
| CL-2 | Watch a shield-blocked, parried, or deflected blow | No blood spray and no impact ring appear for any of the three | As expected | PASS |
| CL-3 | Watch the clash cross render | It appears for `ShieldBlocked`, `Parried`, and `Deflected`, and for neither `Landed` nor `Evaded` | 2026-08-11 FAIL at `0c3f7f2`: too fast to attribute. Individual clash crosses overlapped one another, so a shield block, a parry, and a deflection could not be told apart in flight. The combat-cadence change roughly halves the clash-cross rate Re-run 2026-08-11 after the combat-cadence change: **PASS**. Clash crosses no longer overlap one another at the halved attack rate, so a shield block, a parry, and a deflection can be told apart in flight | PASS |
| CL-4 † | Distinguish a void from a shield block | An `Evaded` blow (no clash cross, follow-through swing) reads differently on screen from a `ShieldBlocked` blow (clash cross, recoil) without reading the event log | As expected | PASS |
| CL-5 † | Distinguish a void from a landed blow | An `Evaded` blow (follow-through swing, no blood, no impact ring) reads differently on screen from a `Landed` blow (stops on target, blood, impact ring) without reading the event log | As expected | PASS |
| CL-6 | Watch any warrior attack | Weapons visibly swing through an arc rather than sitting static during an attack | As expected | PASS |
| CL-7a | Watch one attack at 1x | The swing reads as one countable action, with visible rest either side of it rather than running straight into the next swing | 2026-08-11 FAIL at `0c3f7f2`: the single-action reading was not visible enough to confirm. Under the old cadence an Itak swung every 200 ms while its recovery animation ran 170 ms, so consecutive swings very nearly abutted; the cadence change roughly doubles the interval Re-run 2026-08-11 after the combat-cadence change: **PASS**. The doubled attack interval leaves visible rest either side of a swing, so it reads as one countable action | PASS |
| CL-7b | Watch the same weapon at 4x | The swing is still drawn long enough to read as one action rather than smearing into a blur | 2026-08-11 FAIL at `0c3f7f2`, as part of the undivided CL-7. Attack timelines were aged by the full playback multiplier, so a 0.17-second recovery drew in 0.0425 seconds — two to three frames at 60 Hz. `AttackAnimationSystem.MaximumAnimationSpeedMultiplier` now holds the animation clock at 2x Re-run 2026-08-11 after the animation-speed ceiling: **PASS**. The swing no longer compresses past 2x and stays legible at 4x | PASS |
| CL-8 | Compare a `Parried` or `Deflected` blow, a `Landed` blow, and an `Evaded` blow | The clashed blow visibly recoils, the landed blow stops on the target, and the void follows through past it | As expected | PASS |
| CL-9 | Zoom to high detail, then to low detail, during a swing | The swing arc trail is visible at high zoom and absent at low zoom | As expected | PASS |
| CL-10 | Pan the camera so a swinging weapon crosses the arena panel edge | A weapon tip may be visibly clipped at the panel edge while panning — this is the accepted cost of the pose-blind frustum cull, not a defect | As expected | PASS |
| CL-11 | Observe the merged pawn silhouette in motion, both a shield-bearing and a solo warrior | The silhouette under D7 (main's geometry constants plus the clash branch's swing pose applied on top) reads correctly: shield block and swing pose both present, axe head distinguishable from blade, no visual corruption | As expected | PASS |

