# Auto camera: stop the relentless pan, and let the spectator govern it

Date: 2026-07-28
Status: design, ready to implement

## 1. The complaint

The camera assistant pans constantly. It travels toward fighting even when
fighting is already on screen, and once it starts it rarely stays still for
long. A spectator who wants to watch one part of the field is dragged away
from it.

## 2. What the code does today

`ArenaAutoPanController` is a two-state machine. It is idle while
`ArenaAutoPan.HasFighterInside` finds at least one fighting agent inside the
full visible rectangle, and panning otherwise. `ArenaAutoPan.IsFighting` is
`agent.IsAlive && agent.Intent == AgentIntent.Attacking`.

Four properties of that design combine into the reported behaviour.

**The fighting test is a single-frame sample of a value that flickers.**
`BattleSimulation` sets `AgentIntent.Attacking` only for an agent whose target
is inside contact distance on that tick, and marks `Moving` otherwise. The
collision separation stage pushes bodies apart, so an agent in a melee
oscillates between `Attacking` and `Moving` from tick to tick. A screen holding
a small skirmish therefore drops to zero `Attacking` agents regularly even
though the fight is plainly visible. One such frame is enough to latch
`_isPanning`.

**Nothing checks whether the trip is worth taking.** Once latched, the
controller pans to the nearest melee no matter how close it already was. A
fight a few world units past the edge yanks the camera exactly as hard as one
across the map.

**The pan target goes stale.** `ContinuePan` re-resolves the target only when
`center == _target` exactly, so the camera commits to where a fight *was* at the
moment it started. Fights move. The camera arrives at empty grass, re-targets,
and sets off again — a permanent chase, and the most visible part of the
complaint.

**There is no dwell and no ceiling.** The frame after the controller settles it
may start another pan, and a pan has no maximum duration. Nothing in the design
guarantees the camera is ever still.

## 3. The fix

Keep the two-state shape and the pure-helper split. Add the missing hysteresis.

| Change | Why |
| --- | --- |
| Grace period before a pan starts | A flicker frame, or a lull between exchanges, no longer latches a pan. The screen must be genuinely empty of fighting for `IdleGraceSeconds` first. |
| Dwell after a pan settles | Guarantees stillness between pans and stops settle/re-engage chatter. |
| Minimum travel distance | A candidate closer than half a visible half-extent is already effectively on screen and is not worth moving for. |
| Periodic re-target while panning | The camera converges on a fight that is moving instead of chasing where it used to be. |
| Maximum pan duration | A hard ceiling: no pan can run forever, whatever the agents do. |

`TryResolveTarget` is unchanged. Nearest-melee-with-cluster-centroid was not
the defect, and changing target selection at the same time as the gating would
make a behaviour regression impossible to attribute.

## 4. The setting

`AutoCameraMode`, persisted in the client settings file and selected from the
menu beside gore and motion intensity.

| Mode | Behaviour |
| --- | --- |
| `Off` | The camera never moves on its own. |
| `Assisted` (default) | Holds still while any fighting is anywhere on screen; travels only after a 1.2 s empty-screen grace, then dwells 2.5 s. |
| `Follow` | Keeps the nearest melee near the middle: the on-screen test uses the settle rectangle rather than the whole screen, grace is 0.35 s and dwell 0.75 s. Frequent motion is the point of this mode. |

The mode reaches the controller as an `Update` parameter. The controller stays
free of the settings store, so tests drive every mode directly.

## 5. Answers to the nine feature questions (`SIMULATION-GAME-STANDARDS.md` §10)

1. **Simulation or presentation?** Presentation only. The controller reads
   completed-tick `AgentView` values, advances on unscaled frame time, and
   writes nothing but the camera centre. No state hash is touched.
2. **Determinism?** Not affected. Nothing here feeds the simulation.
3. **Can the spectator discover the effect without reading source?** Yes. The
   menu carries an `AUTO CAMERA` selector with three named options, and the
   difference between them is visible within seconds of a battle.
4. **New allocations per frame?** None. The added state is six floats on an
   existing object.
5. **New dependency?** None.
6. **Save format?** Settings schema rises from 4 to 5. Version 3 and 4 files
   still load; the new field defaults exactly as gore and motion intensity do.
7. **Historical claim?** None. Camera behaviour is not a claim about the 1500s.
8. **Test coverage?** `ArenaAutoPanTests` covers grace, dwell, travel
   threshold, re-target, pan ceiling, and all three modes. New manager and
   selector tests mirror the motion-intensity pair.
9. **Reversible?** Yes. `Off` restores pre-feature behaviour exactly.

## 6. Tuning values

Provisional, chosen to feel calm rather than measured against anything:

```
AssistedIdleGraceSeconds = 1.2
AssistedDwellSeconds     = 2.5
FollowIdleGraceSeconds   = 0.35
FollowDwellSeconds       = 0.75
RetargetIntervalSeconds  = 0.6
MaximumPanSeconds        = 6.0
MinimumTravelFraction    = 0.5
```

## 7. Files

| File | Change |
| --- | --- |
| `src/Hukbo.Client/Settings/AutoCameraMode.cs` | New enum |
| `src/Hukbo.Client/Settings/AutoCameraModeManager.cs` | New, mirrors `MotionIntensityManager` |
| `src/Hukbo.Client/Settings/ClientSettings.cs` | New field |
| `src/Hukbo.Client/Settings/ClientSettingsStore.cs` | Schema 5, accept 3-5, resolver, `TrySave` parameter |
| `src/Hukbo.Client/ArenaAutoPan.cs` | Tuning record, new constants, `IsWorthTravelling` |
| `src/Hukbo.Client/ArenaAutoPanController.cs` | Grace, dwell, re-target, ceiling, mode |
| `src/Hukbo.Client/UI/AutoCameraModeSelector.cs` | New, mirrors `MotionIntensitySelector` |
| `src/Hukbo.Client/MenuOverlay.cs` | Third settings selector |
| `src/Hukbo.Client/Content/Themes/ui-theme-standards.json` | Taller menu panel |
| `src/Hukbo.Client/Theming/UiThemeManager.cs` | `TrySave` call site |
| `src/Hukbo.Client/ArenaGame.cs` | Manager, persist path, menu wiring, pass mode |
| `src/Hukbo.Client/ArenaGame.Rendering.cs` | Menu draw call site |
| `src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs` | Fallback panel height, kept equal to the shipped JSON |
| `src/Hukbo.Diagnostics/DiagnosticLog.cs` | A seven-field `Write` overload; the settings-loaded line now carries seven fields |
| `tests/Hukbo.Client.Tests/*` | Updated and new tests |

## 8. Result

`./scripts/verify.ps1 -SkipBootstrap` passed on 2026-07-28: formatting, Release
build, 608 Core tests, 2383 Client tests, and the seed-1 200-agent / 10 000-tick
headless workload reporting `deterministic true` with `stateHash
A080E28DA7C79C20` and `eventHash 2B6FB3A9A9C1960D` — byte-identical to the
recorded baseline, as a presentation-only change must be. The full output is
recorded in `docs/development/testing.md`.

Seven new rows in that file's "Auto camera modes smoke" checklist are
`PENDING`. Whether the camera now feels calm is a question about motion on a
screen and no automated check can answer it.
