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

Every figure in this section comes from the mirrored starting-formation change
on 2026-07-27, taken on the `feature/starting-formations` branch. Starting
positions are now planned once per battle as a set of contingents and mirrored
across the vertical centre line, so both hashes moved. See
[docs/archives/2026-07-27-starting-formations-design.md](../archives/2026-07-27-starting-formations-design.md),
kept for traceability only.

**Everything below the next heading predates this change and is superseded.**

### Canonical gate

`./scripts/verify.ps1` passed at all five stages: prerequisite validation and
locked restore, format verification, the Release solution build with zero
warnings, the Release repository tests, and the seed-1 / 200-agent /
10,000-tick headless determinism workload.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 351 | 0 | 0 |
| `Hukbo.Client.Tests` | 513 | 0 | 0 |

The Core count is 25 higher than the 326 recorded on `main`; all 25 are the new
`FormationPlannerTests`, which cover mirror symmetry, spawn clearance, map
bounds, half-of-map containment on narrow maps, seed reproducibility, the
five-contingent structure of a default army, the eight-contingent cap, the
crowded-map fallback lattice, and the minimum-map, maximum-map, narrow-half and
single-warrior edge cases. No Client code changed and the Client count is
unchanged.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the current
recorded oracle.

| Field | Value |
| --- | --- |
| Measured ticks | 1081 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `DC7F2E7A107C885A` |
| Event hash | `6C641E90DDF0B943` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0827 ms |
| Tick p95 | 1.2937 ms |
| Tick p99 | 2.4169 ms |
| Tick maximum | 7.3589 ms |
| Allocated | 69,693,688 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 107,634 |
| `contactPairs` | 5,007 |
| `acceptedMoves` | 66,416 |
| `blockedAgentTicks` | 29,040 |
| `attackCapableAgentTicks` | 9,283 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 630,752 |
| `maximumFrontDepthRaw` | 29,114 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. Report only; not gated.

| Field | Value |
| --- | --- |
| Measured ticks | 2231 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `0C53793DEB700A53` |
| Event hash | `4F373537096F2551` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.3425 ms |
| Tick p95 | 2.6284 ms |
| Tick p99 | 4.9597 ms |
| Tick maximum | 11.6425 ms |
| Allocated | 358,456,096 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 636,262 |
| `contactPairs` | 12,746 |
| `acceptedMoves` | 346,688 |
| `blockedAgentTicks` | 92,070 |
| `attackCapableAgentTicks` | 23,207 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 639,480 |
| `maximumFrontDepthRaw` | 62,961 |
| `maximumPenetrationRaw` | 0 |

### What the deployment change moved, on the same workload

| Metric | Amended collision run | Mirrored deployment |
| --- | --- | --- |
| Terminal tick, 200 agents | 657 | 1081 |
| Faction 1 survivors, 200 agents | 10 | 3 |
| `contactPairs`, 200 agents | 5,649 | 5,007 |
| `blockedAgentTicks`, 200 agents | 14,544 | 29,040 |
| `maximumFrontDepthRaw`, 200 agents | 51,072 | 29,114 |
| `maximumPenetrationRaw`, 200 agents | 0 | 0 |

The battles now run considerably longer and end with fewer survivors on the
winning side. Front depth roughly halved and blocked agent ticks roughly
doubled, both consistent with armies that arrive as several columns and queue up
behind their own contingents instead of converging as one cloud. Penetration
stayed at exactly zero, which is the guard: the deployment change did not weaken
the solid-disc invariant.

The win distribution went the other way and that must be recorded, not glossed.
Measured directly, `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000` over seeds
1 to 20:

| Build | Faction 0 wins | Faction 1 wins |
| --- | --- | --- |
| `main` | 4 | 16 |
| This change | 1 | 19 |

Individual battles are closer; which faction wins is more predictable. The cause
is not an unfair deployment — both armies now hold identical ground. It is that
a symmetric deployment leaves the entity-ID ordering rule as the only asymmetry
in the simulation, and that rule always favours the same faction. Random spawns
used to hide it behind noise. Planning each faction from its own jitter draws
was implemented and measured as a mitigation and produced the same 1/19 split,
so it was reverted. Correcting the underlying bias is a tick-rule change that
needs its own decision record and was not attempted here.
`SeedsOneThroughTwentyProduceVictoriesForBothFactions` still passes, on one
seed.

Allocation rose from 42,568,888 to 69,693,688 bytes on the 200-agent workload.
That is **not** an efficiency regression claim in either direction: the battle
also ran 424 ticks longer, and per-tick timing is unchanged or slightly better
(p50 0.0878 ms to 0.0827 ms). The next meaningful allocation comparison is
against the 69,693,688-byte figure above, at the same agent count and seed.

### Superseded oracles

Dead values, kept so the transition can be traced. None may be used as a
regression target.

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, amended collision | `D78F0B527B7F938F` | `AC3BAAEC684854D5` | Terminal tick 657. Superseded by the mirrored deployment. |
| 500 agents, seed 1, amended collision | `C81B4F48DE54B983` | `D03F1213563DFD49` | Report-only workload. Superseded by the mirrored deployment. |

The combat preset is untouched: `CombatRuleset.ContentHash` is still
`0x59FB4CA563D87A49`, asserted by two tests in the passing suite.

### Interactive verification

**Not performed.** The opening frame is the whole visible point of this change
and no person has watched it in a live window. The rows in the deployment smoke
checklist below stay `PENDING`.

## Superseded: the amended collision run

Every figure in this section comes from one final verified run of the **amended**
collision change on 2026-07-27, taken on the `feature/collision-mechanics`
branch after the contact-closing amendment recorded in
[docs/decisions/2026-07-27-collision-policy.md](../decisions/2026-07-27-collision-policy.md).
Nothing here is estimated, rounded, or carried over from an earlier run.

**Every result recorded further down this file predates the amendment.** The
pre-amendment collision figures, the plains-backdrop run, the sound-system run,
the sound-variant run, and the blood-and-gore run were all taken before agents
closed to body contact and before the contact metric used a proximity band. They
are kept as history and must not be read as current.

Note on test counts: collision was verified on a branch taken before the
sound-variant work was committed, so this section's 437 Client tests and the
sound-variant run's 505 are each partial views. After the merge, `main` reports
**326 Core and 513 Client tests passing, 0 failed**, with the canonical gate
green at all five stages. The differing branch figures are a sequencing artefact,
not a lost test.

Environment: Windows 11 Pro 10.0.26200, .NET SDK 10.0.302 as pinned in
`global.json`. The CPU model and installed memory were not captured, so they are
not stated; a future performance comparison that depends on them has to capture
them first.

### Canonical gate

`./scripts/verify.ps1 -SkipBootstrap` passed at all five stages: format
verification, the Release solution build with zero warnings, the Release
repository tests, the seed-1 / 200-agent / 10,000-tick headless determinism
workload, and the overall gate.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 326 | 0 | 0 |
| `Hukbo.Client.Tests` | 437 | 0 | 0 |

Both counts are higher than the figures recorded for the pre-amendment collision
run because `main` was merged into this branch in the meantime, bringing the
sound, plains backdrop, blood, and army-composition suites with it. The increase
is not attributable to the collision work and must not be cited as its coverage.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the
acceptance workload named in the collision policy decision record, and these
values are the current recorded oracle.

| Field | Value |
| --- | --- |
| Measured ticks | 657 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 10 |
| State hash | `D78F0B527B7F938F` |
| Event hash | `AC3BAAEC684854D5` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0878 ms |
| Tick p95 | 1.6322 ms |
| Tick p99 | 2.1088 ms |
| Tick maximum | 9.249 ms |
| Allocated | 42,568,888 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 57,295 |
| `contactPairs` | 5,649 |
| `acceptedMoves` | 40,868 |
| `blockedAgentTicks` | 14,544 |
| `attackCapableAgentTicks` | 8,945 |
| `longestBlockedStreakTicks` | 52 |
| `maximumFrontWidthRaw` | 549,331 |
| `maximumFrontDepthRaw` | 51,072 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. This workload is **report only**. It is not
gated, and its timing and allocation figures are recorded rather than budgeted.

| Field | Value |
| --- | --- |
| Measured ticks | 978 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 17 |
| State hash | `C81B4F48DE54B983` |
| Event hash | `D03F1213563DFD49` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.3167 ms |
| Tick p95 | 1.9138 ms |
| Tick p99 | 4.1672 ms |
| Tick maximum | 12.6946 ms |
| Allocated | 157,426,736 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 280,675 |
| `contactPairs` | 14,270 |
| `acceptedMoves` | 155,460 |
| `blockedAgentTicks` | 48,573 |
| `attackCapableAgentTicks` | 22,848 |
| `longestBlockedStreakTicks` | 61 |
| `maximumFrontWidthRaw` | 695,062 |
| `maximumFrontDepthRaw` | 50,868 |
| `maximumPenetrationRaw` | 0 |

### What the amendment moved, on the same workload

Stated plainly, because these four numbers are the whole point of the amendment.
All figures are the 200-agent, seed-1 workload.

| Metric | Before the amendment | After the amendment |
| --- | --- | --- |
| `contactPairs` | 0 | 5,649 |
| `blockedAgentTicks` | 7,154 | 14,544 |
| Terminal tick | 781 | 657 |
| `maximumPenetrationRaw` | 0 | 0 |

Contact went from unobservable to observable, crowding roughly doubled, the
battle resolves sooner because the fighting ranks are closer together, and
penetration stayed at exactly zero. The last row is the guard: neither change
weakened the solid-disc invariant.

### Tactical guards inside the passing suite

Three named guards ride inside the 326 passing `Hukbo.Core.Tests` above rather
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

Both hashes moved again, and the movement is expected and was approved in
advance. The amendment changed the approach target from attack range to body
contact, which changes where agents stand and therefore changes both the state
hash and the ordered event stream. The proximity band introduced for contact
metrics moved neither hash: it was confirmed byte-identical before and after,
which is the evidence that it stayed derived rather than authoritative.

The tables above are the only recorded oracle. Two earlier pairs are
**superseded** and are listed here so the transition can be traced rather than
guessed at. They are dead values and may not be used as a regression target:

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, pre-amendment | `7EE8BF6EC0F11BB2` | `9BFC18AD06F4F572` | Terminal tick 781. Superseded by the amendment. |
| 500 agents, seed 1, pre-amendment | `7402CCC7C6EC3B50` | `619CCC872BBB2413` | Report-only workload. Superseded by the amendment. |
| 200 agents, seed 1, pre-collision | `6EBB1EA63114F6CE` | `941377BD43C556FF` | Terminal tick 235. Superseded when the collision policy first shipped. |

Allocation for the 200-agent workload is 42,568,888 bytes, against the 50,454,728
bytes recorded before the amendment. That is a same-agent-count, same-seed
comparison, but it is **not** a like-for-like efficiency claim: the amended battle
also ends 124 ticks earlier, so fewer ticks were paid for. Neither figure is
comparable to the much older 15,128,696-byte measurement, which covered a far
shorter battle under a different contact rule, and no ratio between them is
stated here. The open allocation-packing item in
[docs/plans/2026-07-27-battle-event-allocation-packing.md](../plans/2026-07-27-battle-event-allocation-packing.md)
is unaffected by the collision work and remains the place where per-event
allocation is paid down. The next meaningful allocation comparison is against the
42,568,888-byte figure above, at the same agent count and the same seed.

The collision stage itself is required to add no steady-state allocation: all
grid, pair, proposal, and resolution storage is preallocated and reused, and a
Release test asserts that a warm collision tick reuses its buffers.

### Collision metric definitions

These counters are derived observability data. They are never hashed, never
snapshotted, and never persisted, so they cannot influence an outcome. Two
same-seed runs of the same build must produce identical values in every field.

| Metric | Definition |
| --- | --- |
| `candidatePairs` | Living pairs the metrics broad phase emitted, summed over ticks: every pair whose bodies are inside the proximity band described below, allies and enemies alike. |
| `contactPairs` | The cross-faction subset of `candidatePairs`, summed over ticks. This is the fighting front rather than incidental friendly crowding. |
| `acceptedMoves` | Movement proposals that resolved to a destination other than the agent's tick-start position, summed over ticks. |
| `blockedAgentTicks` | One unit per agent per tick that resolved to `MovementResolution.Blocked`. An agent-tick count, not a count of distinct agents. |
| `attackCapableAgentTicks` | One unit per agent per tick in which that agent held a target inside attack reach at its resolved position. Also an agent-tick count. |
| `longestBlockedStreakTicks` | The longest run of consecutive ticks any single agent spent blocked. A running maximum, not a sum. |
| `maximumFrontWidthRaw` | The largest vertical span, in raw fixed-point units, of the agents holding an enemy inside attack reach in any one tick. A running maximum. |
| `maximumFrontDepthRaw` | The horizontal span of that same set, in raw fixed-point units. A running maximum. |
| `maximumPenetrationRaw` | The deepest overlap between two living bodies observed at the end of any tick, in raw fixed-point units. A guard metric, not a tuning signal: under `CollisionPolicy.Solid` a correct run reports exactly `0`, and any nonzero value is a contract violation. |

**`candidatePairs` and `contactPairs` are counted over a proximity band, not over
exact tangency.** This is the single most important thing to understand before
reading either figure. The solid resolver guarantees that every living pair ends
the tick at or beyond `(2R)^2`, so an exact-tangency test asks for a squared
distance of *precisely* `(2R)^2`. On an integer lattice that needs a Pythagorean
coincidence between the two axis deltas and the diameter, and it is unreachable
in practice. That is the mechanical reason the earlier run reported `contactPairs`
of `0`: an exact-tangency counter can essentially never fire, whatever the agents
are doing.

The band is `BodyRadiusRaw + (MovementSpeedRaw / 2)` per body, so a pair counts
as in contact when the two bodies are within one movement step of touching. At
the default values that is `5632` raw units per body, pairing bodies whose
centres are within `11264` raw units. The band is derived observability: no rule
consults it, the resolver's own legality tests still use the exact
`2 * BodyRadiusRaw` contact distance, and both hashes were confirmed
byte-identical before and after it was introduced.

**Front width and depth are measured over agents holding an enemy in reach, not
over agents in body contact.** Width and depth are named for the default
left-versus-right deployment. They are a readability signal only, and no rule
depends on them.

No penetration percentiles are reported. Under the solid contact policy,
penetration between two living bodies is identically zero at the end of every
tick, so a p50 or p95 histogram would be a column of zeros carrying no
information.

### What the collision numbers actually show

Opposing bodies meet. `contactPairs` is 5,649 at 200 agents against 57,295
candidate pairs, and 14,270 at 500 agents against 280,675. An advancing agent
closes until its body meets its target's body, so the two front ranks press
together instead of halting with air in front of them. The earlier zero was the
product of two separate problems, both now fixed: agents stopped at
twelve-world-unit attack reach while a body is only eight world units across, and
the counter itself asked for exact tangency.

Allies also still queue behind their own front line. A rear agent trying to
advance into space its own front rank already occupies is refused, holds position,
and reports `Blocked`. That shows up as 14,544 blocked agent-ticks at 200 agents
and 48,573 at 500 agents, against 8,945 and 22,848 attack-capable agent-ticks
respectively. Crowding roughly doubled at 200 agents once the front closed all the
way, which is the expected consequence rather than a regression: being blocked
does not remove an agent from combat, which is exactly why no separate anti-stall
rule was added.

`maximumPenetrationRaw` is `0` on both workloads. It was also `0` before the
amendment. Where agents choose to stop does not affect the solid-disc invariant,
and any nonzero value in this field would be a contract violation rather than a
tuning signal.

Anyone tuning contact behaviour later should start from the fact that the binding
constraint on the battle line is now the body diameter, while attack reach decides
who can strike. The two are deliberately different distances, and the four world
units between them are what let a second rank strike past a pressed first rank.

### Scope of these results

These results prove the non-interactive gate only. **The interactive
`./scripts/run.ps1` spectator check for this change has not been performed.**
Every row in the interactive smoke checklist below is therefore left `PENDING`.
Automated tests, a clean gate, a benchmark, and a zero-warning build do not
substitute for that check and do not entitle anyone to flip a row to `PASS`.

The amendment makes that outstanding check matter more, not less. It changes what
a spectator sees: front ranks now press their bodies together instead of stopping
four world units apart, roughly twice as many agents are held up behind their own
line, and `AgentIntent.Attacking` now appears only once an agent has arrived at
contact. None of that has been observed in a live window by a person. Nothing in
the automated evidence above speaks to whether the resulting battle line is
legible, and no row may be flipped on the strength of it.

### Superseded records below this line

Everything from here to the interactive smoke checklist is kept for traceability
and is **not current**. All of it predates the contact-closing amendment. Where
one of those entries says a hash is "unchanged from the values recorded above", it
means unchanged relative to the values that were current when it was written, all
of which are now superseded by the tables at the top of this section. Do not read
any hash, tick count, test count, or allocation figure below as a live baseline.

### The sound-variant run

Superseded, and kept for traceability. This run verified the hit-location sound
variant matrix, which lives entirely in `Hukbo.Client` and touches no Core code.
`./scripts/verify.ps1 -SkipBootstrap` passed every stage:

- 505/505 Client tests passed;
- 156/156 Core tests passed;
- formatting verification and the Release build passed with 0 warnings and
  0 errors;
- the seed-1 200-agent workload ended in `Faction1Victory` at tick 235 with
  state hash `6EBB1EA63114F6CE` and event hash `941377BD43C556FF`, reporting
  `deterministic: true` and `firstMismatchTick: null`;
- that workload allocated 15,122,504 bytes.

Those two hashes were unchanged relative to the baseline that was current when
this run was recorded, which was the correct expectation for a Client-only
change. **Both are now dead values**, superseded first by the pre-amendment
collision baseline and then by the amended baseline at the top of this file. The
tick-235 figure belongs to a build in which agents halted at weapon reach and is
not comparable to the current terminal tick.

Interactive variant playback remains unverified. Compiling the Client and listing
the files on disk does not establish that a single sound was ever heard.

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

### 2026-07-27 plains-backdrop gate run

A second local run on 2026-07-27, recorded after the plains battlefield
backdrop change, showed:

- `./scripts/format.ps1 -Verify` passed with 0 warnings and 0 errors;
- `./scripts/verify.ps1 -SkipBootstrap` passed all five stages;
- 141/141 Core tests passed;
- 223/223 Client tests passed, up from the 189 recorded above because of the 34
  new plains backdrop geometry test cases across 14 test methods;
- the seed-1, 200-agent, 10,000-tick headless workload ended in
  `Faction1Victory` at tick 235 with state hash `6EBB1EA63114F6CE` and event
  hash `941377BD43C556FF`, and the run reported `deterministic: true`;
- the same workload allocated 15,122,504 bytes, slightly below the previously
  recorded 15,128,696-byte baseline.

Both the state hash and the event hash are unchanged from the values recorded
above. That is the expected result for a presentation-only change: the plains
backdrop touches only `Hukbo.Client` rendering, `Hukbo.Core` was not modified,
and neither hash moving confirms the backdrop did not leak into the
deterministic simulation.

### 2026-07-27 plains-backdrop review-fix partial re-run

Code review of the change above produced two high-severity findings, both fixed:
a duplicated ground-cell formula that left the shipped render loop uncovered
while the tests constrained a method with no production caller, and incorrect
test counts in the entry above. Four medium findings were also fixed: decal
shades are now bounded by a named ceiling so the high-contrast theme does not
receive mid-grey speckle on pure black, decals are clipped to the map rectangle
so they cannot bleed past the arena border, the shade-count and decal-kind
couplings are now asserted by tests, and the renderer's positional parameter
lists are grouped into a `PlainsBackdropFrame` value.

The canonical gate could **not** be re-run in full after these fixes, and this
is recorded as a limitation rather than a pass. At the time of the re-run the
working tree also carried in-flight, unrelated work for a sound system, a
blood-and-gore layer, and army-composition settings, and several of those
untracked test files did not compile:

```
SoundCueMapperTests.cs(14,17): error CS0051: Inconsistent accessibility:
parameter type 'GameSoundId' is less accessible than method
'SoundCueMapperTests.Map_ReturnsTheWeaponSlotForAnAttack(WeaponId, GameSoundId)'
```

That failure belongs to the sound workstream, not to the backdrop. What was
verified after the review fixes:

- `./scripts/format.ps1 -Verify` passed, 0 of 148 files reformatted;
- the `Hukbo.Client` Release build succeeded with 0 warnings and 0 errors;
- all 42 plains backdrop test cases passed;
- 284/284 Client tests passed with the five non-compiling sound test files
  temporarily set aside and then restored;
- 145/145 Core tests passed.

The Core and Client totals above are higher than the 141 and 223 recorded for
the earlier run because the concurrent sound and gore workstreams have added
their own tests. Those totals are therefore not attributable to the backdrop
change alone and should not be cited as its baseline.

The headless determinism stage was not re-run after the review fixes. Every fix
is confined to `Hukbo.Client` presentation code, so no hash movement is
possible, but that remains an argument rather than recorded evidence. The full
`./scripts/verify.ps1` must be re-run once the sound workstream's test files
compile, and its output recorded here before this change is integrated.

### 2026-07-27 sound-system gate run

`./scripts/verify.ps1 -SkipBootstrap` on 2026-07-27, after the sound system
change, ended with `[PASS] Canonical repository verification completed` and
showed:

- `./scripts/format.ps1 -Verify` passed: `Formatted 0 of 150 files`;
- the Release build produced 0 warnings and 0 errors;
- 156/156 Core tests passed;
- 373/373 Client tests passed, including the 8 new sound suites — catalog,
  library, mapper, budget, cue log, director, cue formatter, and panel layout —
  plus the right-column split;
- the seed-1, 200-agent, 10,000-tick headless workload reported state hash
  `6EBB1EA63114F6CE`, event hash `941377BD43C556FF`, and
  `deterministic: true`.

Both hashes are unchanged from the values recorded above. That is the expected
result for a presentation-only change: the audio path lives entirely in
`Hukbo.Client`, reads the existing `BattleEvent` stream, and adds no Core type,
no Core file, and no simulation state.

An earlier attempt at this gate on the same day failed in the Core test stage,
and then failed to compile `Hukbo.Core` at all, because the working tree
simultaneously held an unfinished army-composition change to `Hukbo.Core`. That
failure was in Core, not in the sound system, and it cleared once the Core change
compiled again. Neither hash moved across either attempt.

### 2026-07-27 blood-and-gore gate run

`./scripts/verify.ps1 -SkipBootstrap` was run at the repository root on
2026-07-27 after the blood-and-gore feature was completed. It ended with
`[PASS] Canonical repository verification completed.` and printed:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.

Test Run Successful.
Total tests: 429
     Passed: 429
 Total time: 0.5805 Seconds
```

The headless determinism workload emitted this `RunReport`:

```json
{
  "environment": {
    "operatingSystem": "Microsoft Windows 10.0.26200",
    "framework": ".NET 10.0.10",
    "processArchitecture": "X64",
    "processorCount": 20
  },
  "seed": 1,
  "agentCount": 200,
  "requestedTicks": 10000,
  "measuredTicks": 235,
  "durationMilliseconds": 28.14780000000001,
  "tickPercentiles": {
    "p50Milliseconds": 0.0856,
    "p95Milliseconds": 0.1655,
    "p99Milliseconds": 0.2715,
    "maximumMilliseconds": 2.9543
  },
  "allocatedBytes": 15122504,
  "outcome": "Faction1Victory",
  "faction0Survivors": 0,
  "faction1Survivors": 30,
  "eventHash": "941377BD43C556FF",
  "stateHash": "6EBB1EA63114F6CE",
  "deterministic": true,
  "firstMismatchTick": null
}
```

Both the state hash (`6EBB1EA63114F6CE`) and the event hash
(`941377BD43C556FF`) are unchanged from the values recorded above, the run
reported `deterministic: true` with no first mismatch tick, and the outcome is
still `Faction1Victory` at tick 235 with 0 and 30 survivors. That is the
expected result for a presentation-only change: the blood layer lives entirely
in `Hukbo.Client`, reads the existing `BattleEvent` stream, and adds no
`Hukbo.Core` type, file, or simulation state. Neither hash moving is what
confirms `Hukbo.Core` was not modified.

Allocation for the same workload was 15,122,504 bytes, matching the figure
recorded for the plains-backdrop run above.

The reported test-run summary was `Total tests: 429` with all 429 passing. That
figure covers the whole repository test run at the time of this gate, and the
working tree also carried tests belonging to concurrent workstreams, so it is
not attributable to the blood-and-gore feature alone and should not be cited as
its baseline.

These results prove the non-interactive gate only. The blood-and-gore smoke rows
below remain `PENDING` a human at an interactive Windows desktop.

## The camera auto-pan run — 2026-07-27

Current, and later than everything above. This change adds `ArenaAutoPan` and
`ArenaAutoPanController` to `Hukbo.Client`, plus a `Center` property, a
`MoveCenterTo` method, a `GetVisibleHalfExtents` helper, and an `Update` return
value on `SpectatorCamera`. It touches no `Hukbo.Core` file.

`./scripts/verify.ps1` passed at all five stages: prerequisites and locked
restore, format verification, the Release solution build, the Release repository
tests, and the seed-1 / 200-agent / 10,000-tick headless determinism workload.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 326 | 0 | 0 |
| `Hukbo.Client.Tests` | 532 | 0 | 0 |

Core is unchanged from `main`'s 326. Client rises from `main`'s 513 by exactly
the 19 new `ArenaAutoPanTests` cases.

The gate's headless workload reported state hash `D78F0B527B7F938F` and event
hash `AC3BAAEC684854D5` at 657 measured ticks, `Faction1Victory`, 0 and 10
survivors, `deterministic: true`, `firstMismatchTick: null`, and 42,568,888
allocated bytes. Every one of those values is identical to the recorded 200-agent
acceptance oracle at the top of this file, which is the required outcome for a
Client-only change: a moved hash here would have meant the camera work had
reached simulation state.

These results prove the non-interactive gate only. **The interactive
`./scripts/run.ps1` spectator check for this change has not been performed.**
The five camera auto-pan rows in the checklist below are therefore left
`PENDING`. The unit tests prove that the controller picks the nearest melee,
engages only on an empty screen, settles inside the inner margin, and yields to
spectator input. None of them prove that the resulting camera motion reads as
helpful rather than as the view drifting on its own, which is the only thing
those rows are for.

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
| 16. Check the plains backdrop ground | The battle floor shows varied ground shading with scattered grass, dirt, and stone marks rather than one flat color. | Not run | PENDING |
| 17. Check backdrop stability at zoom extremes | Zooming fully out and fully in keeps the ground pattern locked to the same patches of map; the pattern does not crawl or shimmer, and decals neither vanish into flicker nor balloon into large blobs. | Not run | PENDING |
| 18. Check backdrop continuity while panning | Panning the camera across the map shows no seam lines, gaps, or overlapping bright edges between ground cells. | Not run | PENDING |
| 19. Check readability over the backdrop | Pawn silhouettes, faction ground rings, selection marks, and hit effects all remain clearly readable against the new backdrop. | Not run | PENDING |
| 20. Cycle every theme against the backdrop | Each theme produces a backdrop in its own palette, with the arena border still distinguishable from the ground. | Not run | PENDING |
| 21. Check backdrop reseeding on Next Round and Full Reset | Pressing `R` for a new round changes the backdrop with the new seed; pressing `Shift+R` for a full reset returns the seed-1 backdrop identical to the first launch. | Not run | PENDING |
| 22. Confirm the sound log is hidden by default | On launch, no sound panel is visible and the battle event log occupies the full height of the right column exactly as before. | Not run | PENDING |
| 23. Toggle the sound log | The `Sounds` control-bar button and `F9` both open and close the sound panel; the button shows an active state while it is open; the right column splits with battle events above and the sound log below, and nothing else on screen moves. | Not run | PENDING |
| 24. Check the expected-file list with an empty audio folder | With no files in `Content/Audio/`, the panel lists all nine expected file names, each marked `MISSING`, shows `MISSING 9/9`, and the game stays silent without errors. | Not run | PENDING |
| 25. Add one sound file | Drop a PCM WAV named `death.wav` into `Content/Audio/`, relaunch, and confirm that slot reads `READY`, the counter drops to `MISSING 8/9`, and a death audibly plays with a `PLAYED` row in the cue log. | Not run | PENDING |
| 26. Check an unusable file | Replace `death.wav` with a non-PCM file of the same name, relaunch, and confirm the slot reads `FAILED` rather than `MISSING`, and the game still runs silently for that slot. | Not run | PENDING |
| 27. Exercise mute and rate limiting | With files present, the panel's `MUTE` toggle silences playback while still logging rows; during a busy tick the cue log shows collapsed `LIMITED xN` rows rather than one row per suppressed cue. | Not run | PENDING |
| 28. Exercise sound-log scrolling and isolation | The wheel scrolls only the panel under the pointer — sound log, battle log, or arena zoom — and clicks inside the sound panel do not click through to the arena or clear the agent selection. | Not run | PENDING |
| 29. Check sound-log reset behavior | `R` and `Shift+R` clear the cue log while leaving the expected-file list and its statuses unchanged. | Not run | PENDING |
| 30. Open the Army Composition panel | Menu opens and the Army Composition button (between Next Round and Full Reset) shows the currently saved units-per-team and category counts in four steppers. | Not run | PENDING |
| 31. Adjust a category count | Left and Right arrows on a stepper adjust its value; Shift+Left and Shift+Right adjust by 10 instead of 1. The Unassigned readout updates live. | Not run | PENDING |
| 32. Check Unassigned reaches zero | Adjusting steppers such that category sum equals units-per-team displays Unassigned: 0. | Not run | PENDING |
| 33. Verify Apply gate behavior | Apply is disabled (ActionDisabled style, dimmed glyph) while Unassigned != 0 and while the draft equals the saved composition; Apply is enabled exactly when balanced and changed. | Not run | PENDING |
| 34. Check the staged banner | After pressing Apply, the panel closes, the menu shows a one-line notice stating the composition takes effect on the next Full Reset, and Apply remains disabled until a different composition is drafted and applied. | Not run | PENDING |
| 35. Verify Full Reset fields the chosen army | After applying a composition and pressing Full Reset (or `Shift+R`), the arena resets and both factions field the number and distribution of warriors specified by the staged composition, visible in the agent inspector and event log. | Not run | PENDING |
| 36. Observe blood at the default fit view | On first launch, with the default gore setting (Stylized) and the default camera fit, a landed blow shows a directional spray and a ground mark that are both plainly visible without zooming the camera in at all. | Not run | PENDING |
| 37. Check spray direction | Select an agent, watch it get struck, and confirm the spray leaves the victim along the line running from the attacker to the victim — pointing away from the attacker, never back toward it. Confirm this holds for blows arriving from several different directions. | Not run | PENDING |
| 38. Distinguish a lethal blow from a wound | A blow that kills its victim renders visibly differently from a blow that only wounds: the lethal tier is denser or longer-lived, and only the lethal blow leaves the ground mark described in row 39. A spectator can tell the two apart without reading the event log. | Not run | PENDING |
| 39. Check ground-mark persistence and fade | A ground mark stays on the battlefield after the fighters involved have moved away, then fades out gradually over time rather than vanishing in a single frame. Marks accumulate where the fighting was heaviest instead of spreading evenly. | Not run | PENDING |
| 40. Confirm gore Off draws nothing | With the gore setting on Off, no spray, spurt, or ground mark appears anywhere for any blow, including kills, at any camera zoom. The existing warm-white hit-effect ring still draws, so impacts remain readable. | Not run | PENDING |
| 41. Change gore intensity via the menu | Open Menu; the Gore Intensity control cycles Off, Stylized, Full and wraps at both ends using Left and Right and the pointer arrows. Each choice visibly changes blow rendering: Off shows nothing, Stylized shows spray and a fading mark, and Full additionally shows a sustained spurt on a kill together with denser, longer-lived marks. The change takes effect immediately, without a restart. | Not run | PENDING |
| 42. Reach the gore selector by keyboard | Inside the menu, `Tab`, `Down`, and `S` move focus from the theme selector through every button and land on the Gore Intensity selector as the final control in the order; continuing past it wraps back to the theme selector. `Up` and `W` reach it from the theme selector by wrapping backwards. While it is focused, Left and Right change the value and no button is activated. | Not run | PENDING |
| 43. Reach the gore selector by pointer | Hovering the Gore Intensity selector highlights it without changing the value; clicking its previous and next arrows changes the value; and a click on the selector does not click through to the arena or activate any menu button. | Not run | PENDING |
| 44. Check gore intensity persists across a restart | Set gore to Full, fully close the game, and relaunch it: Full is active from the first blow, without reopening the menu. Repeat with Off and confirm the same. | Not run | PENDING |
| 45. Check blood clears on Next Round and Full Reset | With sprays and ground marks visible on screen, trigger Next Round (`R`, modal, or summary); all blood clears immediately alongside the event log, inspector, and summary. Repeat separately with Full Reset (`Shift+R` and the modal command) and confirm the same. | Not run | PENDING |
| 46. Check blood readability across every theme | Cycle all five visual themes while blood is on screen. In every theme, including `high-contrast`, blood stays clearly distinguishable from the Blue faction pawns, from the Red faction pawns, and from the arena ground surface; no theme makes a spray or a ground mark disappear into a pawn or the backdrop. | Not run | PENDING |
| 47. Check speed and gore independence | At 1x, 2x, and 4x speed, switch gore between Off and Full and confirm the tick counter in the window title advances at the same visible rate for both settings at each speed. The gore setting never slows, pauses, or reorders simulation advancement. | Not run | PENDING |
| 48. Confirm variants resolve | Press `F9`. Every attack slot reports `READY` with a per-class breakdown, and the counts match the files in `Content/Audio/`: 10 for each of the four attack slots, 10 for `death`. A class with no take of its own shows its real count rather than a fallback-inflated one. | Not run | PENDING |
| 49. Hear the variation | Watch an unpaused battle for a full minute. Blows do not sound like one repeating sample: cuts to different parts of the body are audibly different, and the same weapon striking the same class does not always play the identical take. | Not run | PENDING |
| 50. Confirm no human voice | Listen through a full battle including many deaths. No cue contains a scream, grunt, groan, or breath. Pay particular attention to `death-02`, `death-06`, and `death-07`, whose prompt wording carries the highest risk of an accidental vocalisation. Any file that vocalises must be regenerated before release. | Not run | PENDING |
| 51. Check level consistency | No cue is obviously louder or quieter than its neighbours. The known-quiet takes — `attack-great-blade-ribcage-01`, `attack-great-blade-gut-01`, `attack-heavy-chopper-neck-01`, `death-02` — are audible under a busy battle rather than disappearing. Any that vanish need a re-roll. | Not run | PENDING |
| 52. Verify a partial set falls back | Move one hit class's takes for a single weapon out of `Content/Audio/` and relaunch. That weapon still makes a sound on a hit to that body part, drawn from the fallback class, and the sound log shows the class as missing rather than the whole slot going silent. | Not run | PENDING |

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

### Collision readability smoke

Added by the collision change and revised by the contact-closing amendment.
**Not performed.** Observe one collision-heavy engagement in a live window and
record what was actually seen. The automated gate, the benchmarks, and the
collision regression tests above prove the rule is enforced; none of them prove
the resulting battle line is legible to a person watching it, which is the only
thing these rows are for. The amendment changed what a spectator should expect to
see here, so these rows carry more weight than they did before and none of them
has been observed.

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
| 20. Inspect the front rank | Selecting a front-rank agent shows it moving or attacking rather than blocked, and an agent that has arrived at an enemy reads as attacking rather than still marching. | Not run | PENDING |
| 21. Confirm the ranks actually touch | Opposing front ranks close until their pawn bodies meet, rather than settling with a visible gap of open ground between the two lines. This is the amendment's whole visible effect and the pre-amendment behaviour was a persistent gap. | Not run | PENDING |

### Camera auto-pan smoke

Added by the camera auto-pan change. **Not performed.** The unit tests prove the
targeting and state-machine decisions; only a person watching a live window can
say whether the resulting camera motion is helpful rather than distracting.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 53. Confirm the camera holds still during a visible fight | Zoom in on an engagement so fighting fills the screen. The camera stays exactly where it was left for the whole engagement; it never creeps, drifts, or re-centres on its own while anyone on screen is fighting. | Not run | PENDING |
| 54. Watch the camera find a fight it lost | Zoom in, then pan away until no fighting is on screen. Within a moment the camera slides on its own toward the nearest melee, slows as it arrives, and stops with the fighting comfortably inside the view rather than pinned to an edge. | Not run | PENDING |
| 55. Confirm zoom never changes | Through several auto-pans, the zoom level is exactly what the spectator set. The camera only slides; it never zooms out to find the fight or zooms in on arrival. | Not run | PENDING |
| 56. Take control back | While the camera is auto-panning, hold a pan key. Motion stops under the spectator's hand immediately, the camera goes exactly where they steer it, and it does not resume on its own for a couple of seconds after the key is released. | Not run | PENDING |
| 57. Watch the end of a long battle | Let a match run to its final few survivors at a zoom where they leave the screen. The camera follows the fighting to the end instead of leaving the spectator on empty ground, and it stands still once the match summary appears. | Not run | PENDING |

### Starting deployment smoke

Added by the mirrored starting-formation change. **Not performed.** The
automated evidence proves the arrangement is symmetric, separated and
overlap-free in numbers; none of it proves the opening frame reads that way to a
person watching it, which is the only thing these rows are for.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, at the default camera fit and without zooming in. | Not run | PENDING |
| 59. Check the mirror | Pausing at tick 0 and comparing the two halves shows each side as the other's reflection across the centre line: same group positions, same group sizes, same ragged front. | Not run | PENDING |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups. | Not run | PENDING |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. | Not run | PENDING |

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
