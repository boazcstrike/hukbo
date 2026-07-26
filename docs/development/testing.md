# Testing and Verification

## Canonical gate

```powershell
./scripts/verify.ps1
```

The gate performs, in order:

1. prerequisite validation and locked restore;
2. formatting verification;
3. Release solution build;
4. Core and GPU-independent Client tests without rebuilding;
5. a 200-agent, 10,000-tick, seed-1 headless determinism workload.

It does not launch a window or alter authoritative game state. It never runs a
destructive Git or filesystem cleanup.

This repository intentionally uses local-only verification. There is no GitHub
Actions workflow or hosted-CI completion gate. Run the canonical gate on the
integration workstation and record its exact result.

## Focused commands

```powershell
./scripts/test.ps1 -Configuration Release
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
dotnet test tests/Hukbo.Core.Tests -c Release `
  --filter FullyQualifiedName~DeterminismTests
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
```

Client presentation tests must not create an `ArenaGame`, graphics device,
sprite batch, or window. Tests must remain independent from GPU, audio
hardware, window focus, network, wall clock, `System.Random`, and platform input
types. Performance output is evidence, not a universal frame-time guarantee.

## Latest non-interactive result

Every figure in this section comes from one final verified run of the collision
change on 2026-07-27. Nothing here is estimated, rounded, or carried over from an
earlier run.

Environment: Windows 11 Pro 10.0.26200, .NET SDK 10.0.302 as pinned in
`global.json`. The CPU model and installed memory were not captured, so they are
not stated; a future performance comparison that depends on them has to capture
them first.

### Canonical gate

`./scripts/verify.ps1 -SkipBootstrap` passed at every stage: format
verification, the Release solution build with zero warnings, the Release
repository tests, and the seed-1 / 200-agent / 10,000-tick headless determinism
workload.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 311 | 0 | 0 |
| `Hukbo.Client.Tests` | 197 | 0 | 0 |

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the
acceptance workload named in the collision policy decision record.

| Field | Value |
| --- | --- |
| Measured ticks | 781 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 4 |
| State hash | `7EE8BF6EC0F11BB2` |
| Event hash | `9BFC18AD06F4F572` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0488 ms |
| Tick p95 | 1.1663 ms |
| Tick p99 | 1.5145 ms |
| Tick maximum | 8.0294 ms |
| Allocated | 50,454,728 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 37 |
| `contactPairs` | 0 |
| `acceptedMoves` | 42,510 |
| `blockedAgentTicks` | 7,154 |
| `attackCapableAgentTicks` | 9,042 |
| `longestBlockedStreakTicks` | 57 |
| `maximumFrontWidthRaw` | 560,099 |
| `maximumFrontDepthRaw` | 33,731 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. This workload is **report only**. It is not
gated, and its timing and allocation figures are recorded rather than budgeted.

| Field | Value |
| --- | --- |
| Outcome | `Faction1Victory` |
| Faction 1 survivors | 22 |
| State hash | `7402CCC7C6EC3B50` |
| Event hash | `619CCC872BBB2413` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p95 | 1.8977 ms |
| Tick p99 | 4.2038 ms |
| Tick maximum | 11.5472 ms |
| Allocated | 145,882,872 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 75 |
| `contactPairs` | 0 |
| `acceptedMoves` | 157,404 |
| `blockedAgentTicks` | 32,473 |
| `attackCapableAgentTicks` | 22,402 |
| `longestBlockedStreakTicks` | 63 |
| `maximumFrontWidthRaw` | 695,154 |
| `maximumFrontDepthRaw` | 53,498 |
| `maximumPenetrationRaw` | 0 |

The terminal tick of the 500-agent run was not captured, so it is deliberately
absent from the table above rather than guessed. The previously recorded tick 309
belongs to a different contact rule and is not a valid comparison.

### Tactical guards inside the passing suite

Three named guards ride inside the 311 passing `Hukbo.Core.Tests` above rather
than in a separate report, because they are ordinary deterministic tests:

- `SeedsOneThroughTwentyProduceVictoriesForBothFactions` keeps the seed
  distribution honest, so solid contact did not turn every seed into a draw or
  hand every seed to one faction.
- `PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage` proves a
  packed line stays inside the approved attack geometry and deals damage instead
  of deadlocking.
- `PackedFront_DenseLinesThatMarchIntoReachStillDealDamage` proves agents that
  have to march into reach through their own crowd still get there and still deal
  damage.

### Reading the hashes and the allocation figure

Both hashes moved from the previously recorded state hash `6EBB1EA63114F6CE` and
event hash `941377BD43C556FF`. The movement is expected and was approved in
advance: the collision change put `Scenario.BodyRadiusRaw`,
`Scenario.CollisionPolicy`, and each agent's `MovementResolution` into the state
hash, and constraining movement changes where agents stand, which changes the
ordered event stream as well. The values in the tables above are the new oracle,
and they are the only recorded oracle; the earlier pair is superseded.

Unlike the previous transition, this one is behavioural rather than additive. The
seed-1 battle now ends at tick 781 instead of tick 235, because agents that
cannot walk through each other take longer to close and to finish each other off.
The allocation figures are therefore **not comparable to the earlier
15,128,696-byte measurement**, which covered a much shorter battle. Comparing
them directly would be misleading, so no ratio between them is stated here. The
open allocation-packing item in
[docs/plans/2026-07-27-battle-event-allocation-packing.md](../plans/2026-07-27-battle-event-allocation-packing.md)
is unaffected by the collision change and remains the place where per-event
allocation is paid down. The next meaningful allocation comparison is against the
50,454,728-byte figure above, at the same agent count and the same seed.

The collision stage itself is required to add no steady-state allocation: all
grid, pair, proposal, and resolution storage is preallocated and reused, and a
Release test asserts that a warm collision tick reuses its buffers.

### Collision metric definitions

These counters are derived observability data. They are never hashed, never
snapshotted, and never persisted, so they cannot influence an outcome. Two
same-seed runs of the same build must produce identical values in every field.

| Metric | Definition |
| --- | --- |
| `candidatePairs` | Living pairs the broad phase emitted, summed over ticks: every pair inside the inclusive contact distance, allies and enemies alike. |
| `contactPairs` | The cross-faction subset of `candidatePairs`, summed over ticks. |
| `acceptedMoves` | Movement proposals that resolved to a destination other than the agent's tick-start position, summed over ticks. |
| `blockedAgentTicks` | One unit per agent per tick that resolved to `MovementResolution.Blocked`. An agent-tick count, not a count of distinct agents. |
| `attackCapableAgentTicks` | One unit per agent per tick in which that agent held a target inside attack reach at its resolved position. Also an agent-tick count. |
| `longestBlockedStreakTicks` | The longest run of consecutive ticks any single agent spent blocked. A running maximum, not a sum. |
| `maximumFrontWidthRaw` | The largest vertical span, in raw fixed-point units, of the agents holding an enemy inside attack reach in any one tick. A running maximum. |
| `maximumFrontDepthRaw` | The horizontal span of that same set, in raw fixed-point units. A running maximum. |
| `maximumPenetrationRaw` | The deepest overlap between two living bodies observed at the end of any tick, in raw fixed-point units. A guard metric, not a tuning signal: under `CollisionPolicy.Solid` a correct run reports exactly `0`, and any nonzero value is a contract violation. |

**Front width and depth are measured over agents holding an enemy in reach, not
over agents in body contact.** That choice is deliberate and it matters for
anyone reading the figure. A body is eight world units across while attack reach
is twelve, so a line that halts at reach never touches; a contact-based span
would read zero for an entire battle and would tell a spectator nothing. Width
and depth are named for the default left-versus-right deployment. They are a
readability signal only, and no rule depends on them.

No penetration percentiles are reported. Under the solid contact policy,
penetration between two living bodies is identically zero at the end of every
tick, so a p50 or p95 histogram would be a column of zeros carrying no
information.

### What the collision numbers actually show

`contactPairs` is **0 across both entire battles**. Opposing bodies never touch.
An agent stops advancing once its target is inside the twelve-world-unit attack
reach, while a body is only eight world units across, so roughly four world units
of air remain between the two front ranks for the whole engagement. This is the
shipped behaviour, not a measurement error and not a defect.

The observable effect of collision is therefore allies queueing behind their own
front line rather than shield-to-shield contact between factions. A rear agent
trying to advance into space its own front rank already occupies is refused,
holds position, and reports `Blocked`. That shows up as 7,154 blocked agent-ticks
at 200 agents and 32,473 at 500 agents, against 9,042 and 22,402 attack-capable
agent-ticks respectively. Being blocked does not remove an agent from combat,
which is exactly why no separate anti-stall rule was added.

Anyone tuning contact behaviour later should start from the fact that the binding
constraint on the battle line is attack reach, not body radius.

### Scope of these results

These results prove the non-interactive gate only. **The interactive
`./scripts/run.ps1` spectator check for this change has not been performed.**
Every row in the interactive smoke checklist below is therefore left `PENDING`.
Automated tests, a clean gate, a benchmark, and a zero-warning build do not
substitute for that check and do not entitle anyone to flip a row to `PASS`.

### Retained evidence from the earlier spectator-clarity work

Kept so it is not lost when the section above is next replaced. These
observations belong to the earlier spectator-clarity package run, not to the
collision change:

- the package run produced
  `artifacts/packages/client-win-x64/Hukbo.Client.exe`;
- that packaged Client opened visibly, remained responsive, showed
  `Hukbo — A 0 : 0 B — Seed 1 — Tick 0 — 1x — Paused — Ongoing`, and returned
  exit code 0 after a normal window-close request;
- the spectator-clarity independent review reported no Critical, High, Medium, or
  Low findings.

None of that was re-observed after the collision change.

## Interactive smoke checklist

Run `./scripts/run.ps1` on an interactive Windows desktop. This repository uses
local-only verification: there is no hosted-CI substitute for this direct
interaction pass. Compilation, automated tests, a window-opening probe, or
synthetic input do not make a manual row pass.

### Spectator clarity smoke

Record the observed value in `Actual` and change `Status` only after performing
the interaction. Use `PASS`, `FAIL`, or `BLOCKED`; leave untouched rows
`PENDING`.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 1. Launch the game | The window opens, agents render, and the match starts paused with tick unchanged. | Not run | PENDING |
| 2. Activate Play | The always-visible Play button advances ticks; Space provides the same toggle while the modal is closed. | Not run | PENDING |
| 3. Activate Pause | The always-visible Pause button stops tick advancement and visibly indicates the paused state. | Not run | PENDING |
| 4. Open Menu | The always-visible Menu button pauses the match and opens the modal; Escape toggles that same menu behavior. | Not run | PENDING |
| 5. Exercise modal commands | Modal Play resumes and closes; modal Pause remains open and paused; Escape closes without resuming; Exit Game, which is available only in the modal, requests one clean shutdown. | Not run | PENDING |
| 6. Select an agent | A primary click on a living agent pins the inspector with ID, faction, alive/dead state, health, intent, target, and position. | Not run | PENDING |
| 7. Move away and observe death | Moving the pointer away does not clear selection; if the selected agent dies, the inspector remains pinned and shows its final `DEAD` state. | Not run | PENDING |
| 8. Check observational behavior | Selecting or inspecting an agent does not alter tick progression or the deterministic battle result; an empty-arena click clears selection and UI clicks do not click through. | Not run | PENDING |
| 9. Exercise event-log scrolling | At 1x and 4x, events remain ordered without duplicates and retain at most 200 rows. The wheel scrolls only the log while the pointer is over it and does not zoom the arena; new events do not steal an upward scroll position; returning to the bottom reveals the newest events; over the arena, the wheel zooms. | Not run | PENDING |
| 10. Reach a terminal outcome | The match pauses and the summary winner, both survivor counts, terminal tick, simulated duration, and seed match the final status and visible arena state; the summary offers Next Round. | Not run | PENDING |
| 11. Check score timing and team mapping | Team A is Blue/faction 0 and Team B is Red/faction 1. Reaching a victory does not change the score immediately; choosing Next Round adds exactly one win to that completed round's winner. Starting the next round after a draw or while the current round is ongoing adds no win. | Not run | PENDING |
| 12. Exercise ordinary Next Round | `R`, modal Next Round, and summary Next Round each preserve the score, speed, and camera; clear selection, event history, scroll state, and summary; and leave the fresh round paused. | Not run | PENDING |
| 13. Check seed progression | Each Next Round changes the seed to a distinct deterministic value. After Full Reset, repeating the same Next Round sequence produces the same seed sequence. | Not run | PENDING |
| 14. Exercise Full Reset | After changing the score, speed, and camera, press `Shift+R`; both win totals become 0, seed returns to 1, speed returns to 1x, the camera fits the arena, disposable UI state clears, and the fresh round is paused. Change state again and confirm modal Full Reset has the same result. | Not run | PENDING |
| 15. Close the window | The operating-system close button exits the process once with exit code 0. | Not run | PENDING |

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

### Collision readability smoke

Added by the collision change. **Not performed.** Observe one collision-heavy
engagement in a live window and record what was actually seen. The automated
gate, the benchmarks, and the collision regression tests above prove the rule is
enforced; none of them prove the resulting battle line is legible to a person
watching it, which is the only thing these rows are for.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 16. Read the battle line | Agents form a visible front instead of a shapeless blob, and the shape reads as a consequence of crowding rather than as a snapped grid. | Not run | PENDING |
| 17. Look for stacking and jitter | No two living pawns visually occupy the same spot, and a pressed front settles instead of vibrating between positions tick after tick. | Not run | PENDING |
| 18. Confirm combat continues | A packed front keeps dealing damage; the match does not stall into a standoff and reaches a terminal outcome inside its tick limit. | Not run | PENDING |
| 19. Inspect a blocked agent | Selecting an agent in the second rank shows a movement label explaining why it is not advancing, and that label changes as the situation changes. | Not run | PENDING |
| 20. Inspect the front rank | Selecting a front-rank agent shows it moving or attacking rather than blocked, matching the recorded finding that opposing bodies stop at reach and never touch. | Not run | PENDING |

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
