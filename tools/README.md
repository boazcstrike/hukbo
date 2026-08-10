# Measurement tools

Eight console programs that measure things about Hukbo which are otherwise
argued about rather than known. Three exist to produce the evidence in
[`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`](../docs/research/SOUND-CAPACITY-MEASUREMENTS.md);
`Hukbo.Tools.WeaponBalance` produces the per-weapon evidence in
[`docs/development/measurement-history.md`](../docs/development/measurement-history.md)
under "T32 — weapon balance measurement"; the fifth,
`Hukbo.Tools.RenderProbe`, produces
the render-performance evidence named in R-W6.12 through R-W6.14 of the
visual-system integration design; the sixth,
`Hukbo.Tools.DeadlockProbe`, produces the collision-stall evidence in
[`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`](../docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md);
and the seventh, `Hukbo.Tools.ContingentShape`, produces the evidence behind
the two failed rows of the persistent-contingent smoke pass in that same file;
and the eighth, `Hukbo.Tools.CohesionTrace`, produces the gate-by-gate evidence
in section 9 of
[`docs/archives/2026-08-07/2026-07-28-cohesion-scan-narrowing-design.md`](../docs/archives/2026-08-07/2026-07-28-cohesion-scan-narrowing-design.md),
which is what established why contingent cohesion appeared to stop firing
partway through an advance. All eight let those numbers be reproduced later.

Two of them read `internal` members of `Hukbo.Core`.
`Hukbo.Tools.DeadlockProbe` needs exactly one — `CollisionPriority.Resolve`, the
pure function that produces the collision resolver's per-tick resolution order.
`Hukbo.Tools.CohesionTrace` needs three: `MovementRules.IsCohesionEligible`,
`MovementRules.IsCohesionWindowOpen` and
`MovementRules.ParticipatesInCrossContingentScan`, the pure predicates that
decide the six movement gates, plus `FormationPlanner.MaximumContingents` for
the slot arithmetic. `src/Hukbo.Core/Properties/AssemblyInfo.cs` grants both for
those reasons. Each observes the simulation from outside and changes nothing in
it.

## They are not part of the build

**None of these projects is listed in `Hukbo.slnx`.** `scripts/verify.ps1`
builds and tests the solution, so it does not see them: the canonical gate is
neither slowed nor put at risk by anything in this folder. They are built and
run by hand, deliberately.

They still inherit `Directory.Build.props` and `Directory.Packages.props`, so
they compile warnings-clean under `TreatWarningsAsErrors`, take package versions
from central management, and each carries its own `packages.lock.json`.

Nothing here writes to a repository file. `Hukbo.Tools.MixAnalysis` writes WAV
renders into an output directory you name; `tools/mix-output/` is gitignored for
that purpose.

## Rebuild `Hukbo.Core` first

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
```

`Hukbo.Tools.CueDemand` and `Hukbo.Tools.MixAnalysis` take a `ProjectReference`
on `Hukbo.Core`, and a stale assembly will link without complaint. That is not
hypothetical: the first revision of the research document reported demand
figures that were wrong by a factor of two because they were measured against a
`Hukbo.Core` binary from before the last-stand formation merge. Rebuild, then
measure.

## The tools

### `Hukbo.Tools.CueDemand`

How much sound a battle asks for, and how much of it the client's rate limit
throws away.

Runs a real `BattleSimulation`, counts the events that map to a sound slot,
replays the client's per-frame budget over them at 1x, 2x, and 4x, and sweeps a
range of candidate budgets.

```powershell
dotnet run --project tools/Hukbo.Tools.CueDemand -c Release -- 200 1 10000
```

Arguments: agents, seed, tick limit.

### `Hukbo.Tools.VoiceStress`

What the audio backend on this machine can actually do.

Opens no window and constructs no `Game`, so it measures the audio device
alone. Phase A finds the hard concurrent-voice ceiling with explicit
`SoundEffectInstance` objects. Phase B sustains fire-and-forget playback at
rates from 20 to 1 600 cues per second and reports refusals, CPU per frame, and
allocation. Phase C saturates and drains the device six times to prove that
voices are recycled rather than leaked.

```powershell
dotnet run --project tools/Hukbo.Tools.VoiceStress -c Release
```

Arguments: audio directory, playback volume. Both optional; the directory
defaults to the client's shipped `Content/Audio` and the volume defaults to
0.02, quiet enough that a 256-voice burst is not painful. **Raise the volume
only if you intend to listen to it.**

### `Hukbo.Tools.MixAnalysis`

What a battle actually sounds like, and whether the mix overloads.

Sums the shipped clip sample data at the trigger times a real battle produces,
under four playback policies, and reports peak level against full scale and the
count of samples beyond it. Writes each render to a WAV so the result can be
heard.

```powershell
dotnet run --project tools/Hukbo.Tools.MixAnalysis -c Release -- src/Hukbo.Client/Content/Audio tools/mix-output 200 1 1
```

Arguments: audio directory, output directory, agents, seed, speed multiplier.

> **This harness replicates client logic it cannot reference.** `SoundCueMapper`,
> `HitClassCatalog`, and `SoundVariantSelector` are `internal` to the windowed
> `Hukbo.Client` assembly, so `CueSchedule.cs` mirrors them by hand. The variant
> draw uses the same `SplitMix64` from `Hukbo.Core` the client uses, so the file
> chosen for a given tick and entity is identical rather than merely similar.
> **If the client's slot mapping, hit-class mapping, fallback chain, or variant
> selection changes, `CueSchedule.cs` must change with it** — otherwise it keeps
> reporting confident numbers about a game that no longer exists.

### `Hukbo.Tools.WeaponBalance`

Mean ticks-to-kill per weapon loadout, and per-faction win rate, for the
per-weapon damage/reach/cooldown attributes preset V2 introduced.

Runs real `BattleSimulation` instances across a fixed 5-seed sweep, at
200 and 500 agents with the default even roster, and at 500 agents with
each of the six loadouts stacked to half the faction in turn. Read-only
against `Hukbo.Core`.

```powershell
dotnet run --project tools/Hukbo.Tools.WeaponBalance -c Release -- 10000
```

Argument: tick limit per battle (optional, defaults to 10 000).

> **`Scenario.RosterCounts` applies identically to both factions.** This tool
> cannot field two different rosters against each other — only a composition
> stacked toward one loadout, mirrored on both sides. A genuine per-faction
> asymmetric matchup needs `Scenario` extended to carry a roster per faction,
> which is a separate, non-trivial change with its own design document.

### `Hukbo.Tools.RenderProbe`

How fast the client actually draws, at the camera stations and unit counts
the visual-system integration design's measurement matrix names.

Launches the real `ArenaGame` — a real window, a real `GraphicsDevice` — against
a scripted scenario, drives the three camera stations (minimum zoom, default
fit, maximum zoom) via `ArenaGame`'s render-probe opt-in
(`HUKBO_RENDER_PROBE=1`, set by this tool before construction), and records
frame-time p50/p95/p99, the peak arena sprite submission count, GC collection
deltas, and allocated-bytes deltas per station. Writes a JSON report under
`artifacts/`.

```powershell
dotnet run --project tools/Hukbo.Tools.RenderProbe -c Release -- 200 1 300 artifacts/render-baseline-2026-07-28.json
```

Arguments: agents, seed, frames sampled per station, output path. All optional.

> **Needs an interactive desktop and a GPU.** Unlike every other tool in this
> folder, `RenderProbe` opens a real window — there is no headless mode. A
> run from an automated agent session without a desktop session is BLOCKED,
> not faked.
>
> **The arena submission count is always `0` today.** VIS-034's counting
> seam (`src/Hukbo.Client/Rendering/SubmissionCount.cs`) is a pure,
> GPU-independent function over already-built layout values, exercised only
> by xunit — it is deliberately never called from the live render loop. To
> make this field meaningful, a later task calls that function from inside
> `ArenaGame.DrawArena` with the same layout values the renderer already
> computes, and threads the total through the
> `RenderProbeSample.ArenaSubmissionCount` field this tool already reads.
>
> **`packages.lock.json` is not checked in for this project yet.** Unlike its
> siblings, this tool was authored without running `dotnet restore` in this
> worktree (concurrent builds from other in-flight tasks share it). The first
> `dotnet build`/`dotnet restore` here will generate one, per the standard
> `RestorePackagesWithLockFile` behavior — that generation is expected, not
> an error, and the resulting file should be committed once produced.

### `Hukbo.Tools.ContingentShape`

What shape a contingent actually holds, and how often it is allowed to gather
at all.

This exists to settle the two rows that failed the 2026-07-28 manual smoke pass
on persistent contingents. Row 104 reported that a mid-battle gather sometimes
read as a line rather than a ragged clump; row 114 reported that gathering was
seen only near the start of the advance and never again once groups were
fighting. Both were judgements by eye, and neither had a number attached.

For every tick of every battle it runs, and for each of the sixteen contingent
slots, the tool records the contingent's `ContingentState`, the principal-axis
aspect ratio of its living members, the angle that major axis makes with the
world axes and with the contingent's own direction of advance, and — for every
tick a contingent spent in `Advance` — which of the four possible causes denied
it a cohesion destination that tick.

```powershell
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 IndependentPursuitV1
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 PersistentContingentsV2 shape.csv
```

Arguments, all optional and positional: tick limit per battle (defaults to
10 000), agent count (200), how many seeds to sweep starting from 1 (5), the
`MovementPresetId` to run (`PersistentContingentsV2`), and a path to write the
per-tick, per-contingent rows to as CSV (none, in which case only the console
summary is produced). Running it under `IndependentPursuitV1` gives the
uncohered control: every contingent stays in `ContingentState.None`, and the
shape figures describe the same nominal groups with no cohesion acting on them
at all.

> **The aspect ratio is only meaningful for a contingent that is actually
> cohering.** It is the ratio of the standard deviations along the two
> principal axes of the members' positions, so a single member left far behind
> a settled cluster moves it by more than the cluster's own shape does. Under
> `Hold` that is what makes it a fair reading of the gathered shape; under
> `Advance`, `Close`, and `None`, where members pursue independently, the
> figure is dominated by outliers and should be read as dispersion rather than
> as shape.

> **This harness reconstructs three things `Hukbo.Core` keeps internal.** The
> contingent leader, the cohesion trail base, and the hysteresis-banded
> gathering test of transition rule 5 are all recomputed here from public
> `AgentView` data, because the members that hold them are `internal`. The
> jitter radius, the trail distance, the map-edge gate, and the
> cross-contingent overlap gate are called directly on the public
> `FormationRules` and so cannot drift. **If the leader rule, the trail base,
> or rule 5 changes in `Hukbo.Core`, `Program.cs` has to change with it**, or
> its denial attribution becomes fiction. The file names the exact source
> lines it mirrors.

> **This is the only tool here that uses floating point**, in the eigenvalue
> and angle arithmetic of the shape metric. It never feeds the simulation, so
> the determinism contract in `SIMULATION-GAME-STANDARDS.md` section 4 does not
> reach it; every accumulation that could overflow is done in `long` or
> `Int128` first, and only the final ratio is taken in `double`.
