# Prompt template — Weapon-relative movement, one weapon per session

Date: 2026-07-30

This is a **template**. Copy the body below the horizontal rule into a fresh
session, fill in the `{{ }}` slots from the table underneath, add anything
extra in the notes slot, and run it. One session per weapon; the five sessions
run in parallel.

**Do not run any of these sessions until the shared foundation branch described
in [`2026-07-30-weapon-movement-foundation.md`](2026-07-30-weapon-movement-foundation.md)
has merged into `main`.** Before the foundation lands there is no movement
profile type, no registered preset, and no scenario matrix, so a weapon session
can do nothing but fail.

## Fill-in table

| Slot | Kampilan | Wasay | Kalis | Itak | Tall Hardwood Shield |
| --- | --- | --- | --- | --- | --- |
| `{{WEAPON}}` | Kampilan — Great Blade | Wasay — War Axe | Kalis — Thrusting Blade | Itak — Work Blade | Tall Hardwood Shield |
| `{{SLUG}}` | kampilan | wasay | kalis | itak | tall-hardwood-shield |
| `{{BRANCH}}` | movement-kampilan | movement-wasay | movement-kalis | movement-itak | movement-shield |
| `{{PLAN}}` | the kampilan movement plan | the wasay movement plan | the kalis movement plan | the itak movement plan | the tall-hardwood-shield movement plan |
| `{{RESEARCH}}` | `docs/research/movement/kampilan.md` | `docs/research/movement/wasay.md` | `docs/research/movement/kalis.md` | `docs/research/movement/itak.md` | `docs/research/movement/tall-hardwood-shield.md` |
| `{{LOADOUT}}` | `CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None)` | `CombatLoadout(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None)` | `CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None)` | `CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None)` | both `CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood)` and `CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood)` |
| `{{MATRIX ID}}` | `KP` | `WA` | `KA` | `IT` | `KS` and `IS` |
| `{{PROFILE FILE}}` | `KampilanMovementProfile.cs` | `WasayMovementProfile.cs` | `KalisMovementProfile.cs` | `ItakMovementProfile.cs` | `TallHardwoodMovementProfiles.cs` |
| `{{TEST FILTER}}` | `Kampilan` | `Wasay` | `Kalis` | `Itak` | `TallHardwood` |

The shield session owns both shielded rows, `KS` and `IS`, because the plan's
ownership table assigns both to one file.

---

## Task

Implement the **{{WEAPON}}** slice of Hukbo's weapon-relative movement program,
as code, in a dedicated git worktree. You own one weapon's movement profile and
the tests that prove its behaviour. Everything shared already exists; you build
on it and you do not change it.

Your governing documents, in precedence order:

1. the weapon movement workstream plan — the shared architecture, preset identity,
   hashing order, route and clearance contract, and the file-ownership table.
   **This outranks your weapon plan on anything shared.**
2. `{{PLAN}}` — your weapon's task list. This is the only plan you execute.
3. `{{RESEARCH}}` — your weapon's evidence base and approved calibration
   ranges.
4. `docs/research/movement/README.md` — evidence labels, the six-loadout
   matrix, and the numerical tuning contract.
5. The shared foundation session's handoff report, which names the allocated
   preset, the combat preset you must select, the shared symbols you may call,
   and the boundary-equality conventions that were actually implemented.

Your loadout key is {{LOADOUT}}. Your matrix identifier is {{MATRIX ID}}.

{{EXTRA NOTES — anything added for this specific weapon goes here and outranks
the defaults above.}}

## Preflight — do this before planning anything

The plan documents were written on 2026-07-29 and the repository has moved
since. Verify each of these yourself, in the worktree, and record what you
found. Where a plan document disagrees with the code, the code wins. Do not
edit the plan documents to match; they are design records, and correcting them
is a separate task.

1. **Preset identity.** Read `src/Hukbo.Core/Movement/MovementPresetId.cs`. The
   weapon plans call the new preset `EquipmentRelativeFootworkV5`. That numeric
   value belongs to `PersistentContingentsV5`, the rank-aware leader scan, and
   the foundation session renamed the new preset accordingly — expected to be
   `EquipmentRelativeFootworkV6 = 6`. Use whatever the code actually declares.
2. **Combat preset.** `Scenario.CombatPreset` no longer defaults to
   `PrecolonialPhilippinesV2`; it defaults to `PrecolonialPhilippinesV4`. Every
   scenario, test, and benchmark you write names its combat preset
   **explicitly**, using the preset the foundation handoff assigned to your
   loadout. Never rely on the default, and never assume a shielded loadout is
   available in a preset that does not field it. The plans' instruction to
   switch the combat default from V2 to V3 is obsolete and belongs to nobody.
3. **Foundation present.** Confirm these exist and match the shared contract
   before you build on them: `LoadoutMovementProfile.cs`, `Facing16.cs`,
   `FacingRules.cs`, `WeaponMovementRules.cs`, `LocalMovementContext.cs`,
   `src/Hukbo.Core/Movement/Profiles/{{PROFILE FILE}}`, the new preset's
   registry entry, and
   `tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs`. If any of them
   is missing, **stop and report** — the foundation branch has not merged and
   this session cannot proceed.
4. **Boundary conventions.** Read the boundary-equality rules the foundation
   actually implemented — entry distance, ally clearance, disengage entry,
   disengage release — and check them against the ones {{PLAN}} asserts. Where
   they differ, the shared implementation wins and your tests assert the shared
   convention. Note the difference in your report.
5. **Baseline.** Run `./scripts/verify.ps1` on the untouched worktree and
   record the result. A gate that was already red is not yours to be blamed for
   and not yours to hide.

## Worktree

Work in isolation. This checkout is shared by other live sessions, by the four
other weapon sessions, and by `dotnet test` runs that fight over `obj/`.

```powershell
git worktree add .claude/worktrees/{{BRANCH}} -b {{BRANCH}} main
```

Branch off **local** `main`, after the foundation has merged, and confirm the
worktree sits on the commit you expect:

```powershell
git -C .claude/worktrees/{{BRANCH}} log -1 --oneline
```

Other directories under `.claude/worktrees/` and untracked files appearing in
`docs/` belong to the other weapon sessions or to unrelated work. Leave them
alone. Rebase onto `main` before you run the final gate — the other four
sessions are landing while you work.

## Files you own

You may create and edit only:

- `src/Hukbo.Core/Movement/Profiles/{{PROFILE FILE}}`
- `tests/Hukbo.Core.Tests/Movement/` — the test files your plan names for
  {{WEAPON}}, and no others.

You may **not** edit, under any circumstance:

`MovementPresetId.cs`, `MovementPresetRegistry.cs`, `MovementRuleset.cs`,
`WeaponMovementRules.cs`, `FacingRules.cs`, `MovementContextQuery.cs`,
`BattleSimulation.cs`, `StateHasher.cs`, `AgentState.cs`, `AgentView.cs`,
`FormationPlanner.cs`, the inspector, `MovementScenarioMatrix.cs`, any shared
fixture or freeze test, or another weapon's profile or tests.

Four sessions are running beside you against those same files. An edit there is
a merge conflict you created deliberately.

### Handing off a shared defect

Your tests will probably find at least one. When a boundary comes out wrong for
reasons that are not your profile's values:

1. Leave the failing test in place, named and minimal.
2. Name the exact shared symbol, the input, the expected result, and the actual
   result.
3. Report it as a handoff and continue with everything else in your plan.

Do not reach into shared runtime code to make your own test go green. Do not
work around it by changing your profile values to hide it.

## Tuning authority

You may change values **only** inside your own profile row, **only** within the
approved calibration ranges in {{PLAN}} section 5, and **only** one field at a
time, re-running the same seed set after each change.

If no in-range value passes, stop and report it as a product-review item. Do
not widen a range, do not change combat statistics, do not touch another
weapon's row, and do not invent a relaxed acceptance bound. Balance here means
role viability, not equal duel win rates — never assert an equal win rate and
never tune toward one.

## How to run it

Invoke the `hukbo-orchestrate` skill and follow it. Do not restate the pipeline
from memory, and do not use the generic `/flow:*` commands — inside this
repository they do not know about worktree isolation, the `tokensave`-only
discovery rule, or the canonical gate.

**Stage 1 — research, in parallel, read-only.** Two groups in one message, at
most eight agents across the whole fan-out:

- *Requirements and evidence*: what {{PLAN}} pins exactly — every profile value,
  its approved range, every boundary-equality convention, the count-sensitive
  posture table, the matchup behaviours it requires and the failure modes it
  rejects, and the calibration acceptance and rejection criteria. Cross-check
  against {{RESEARCH}} and report which claims are **Documented**, **Documented,
  form uncertain**, or **Provisional reconstruction**. Return tables, not prose.
- *Existing code*: the shared movement surface as merged — profile type and
  bounds, resolver entry point, `WeaponMovementRules` signatures, facing rules,
  the context query, the scenario matrix API, and the metrics available to
  assert on. Return file, symbol, line, and the exact call shape.

Code discovery goes through the `tokensave` MCP tools. Never an Explore agent,
and carry that constraint into every sub-agent prompt you write.

**Stage 2 — planning, one agent.** One planner reads both research outputs and
produces one ordered task list with the columns: task, what, files, done when,
depends on, verified by. It writes no code. Audit its output yourself before
spawning anyone.

**Stage 3 — implementation.** Only this stage writes files. TDD exactly as
{{PLAN}} specifies: failing focused test first, then the minimum change, then
re-run. Your task list is small and funnels through two files, so expect to run
mostly serial; parallelism buys nothing when two agents would share a file.

## Hard constraints

**Determinism.** Load the `hukbo-determinism-change` skill before touching
simulation code. Integer and fixed-point math only in `Hukbo.Core`; no `float`,
no `double`, no trigonometry, no decimal-derived authoritative state.
`System.Random` is banned — `SplitMix64` only, and never edit its pinned
vectors. Every existing movement preset stays byte-identical in behaviour,
outcomes, ordered events, state hash, event hash, and trajectory fixtures. If
your work moves a content hash, you have edited something you do not own — stop
and report.

**Do not activate.** `Scenario.MovementPreset` stays where it is. Your row is
inert unless the new preset is explicitly selected, and that is correct.
Activation is a separate, separately approved task.

**Core boundary.** `Hukbo.Core` may not reference MonoGame, the filesystem, the
network, windowing, audio, the wall clock, or `Hukbo.Diagnostics`. No unbounded
cache, no target cache, no spatial grid, no per-tick allocation, and no derived
data in a snapshot. `Hukbo.Client` may not decide targeting, damage, retreat,
or victory.

**Scope discipline.** No change to damage, reach, cooldown, combos, clash, hit
location, or shield interception. No directional attack, defence, shield arc,
parry, or friendly damage. No velocity vector, physics, terrain, pathfinding,
morale, panic, or rout. No weapon-specific preset, no second controller, no
weapon-specific branch inside `BattleSimulation`.

**Shield sessions specifically.** Movement responds to geometry and counts, not
to which way a shield faces. A shield never grants a speed bonus and never
creates directional cover. If mirroring a shield bearing changes authoritative
movement, that is a defect to report, not a feature to keep.

**Historical accuracy.** `AGENTS.md` §7 binds. Every load-bearing claim carries
an evidence label. Every tuning number in your profile carries the *Provisional
reconstruction: gameplay tuning; no historical measurement* marker in its XML
documentation, with a link to {{RESEARCH}}. Player-facing cultural
identifications appear only in pair form — Filipino name, em dash, plain
English descriptor. Never describe a gameplay tuning value as a historical
measurement.

**Logging.** Any new instrumentation goes through
`Hukbo.Diagnostics.DiagnosticLog` with a `const` identifier on `LogEvents`.
Never `Console.Write*`. Per-tick lines are `trc`. A disabled call allocates
nothing. `Hukbo.Core` never references `Hukbo.Diagnostics`.

**Documentation.** Full, normal English. Never run a compression pass over
repository documentation. Any new document goes under `docs/plans/`, never the
repository root.

**Naming.** `Hukbo` and `Hukbo.*` only. Never reintroduce `AutonomousArena`.

## Verification

Focused tests throughout:

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~{{TEST FILTER}}"
```

Plus the shared suites your plan names, to prove you broke nothing:

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~MovementPresetFreezeTests|FullyQualifiedName~MovementPresetRegistryTests|FullyQualifiedName~DeterminismTests"
```

After integrating everything and rebasing onto `main`, run the canonical gate
**yourself, once**:

```powershell
./scripts/format.ps1 -Verify
./scripts/verify.ps1
```

The gate is never delegated to a sub-agent, and no sub-agent report substitutes
for its output. Paste the real output. A build that compiles is not a passing
test run, and a test run is not a manual smoke check. Load
`hukbo-verify-and-record` for what counts as evidence.

Where your plan calls for scale runs, name both presets explicitly and compare
against the shared baseline:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset <combat preset from handoff> -MovementPreset <new preset>
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1 -Preset <combat preset from handoff> -MovementPreset <new preset>
```

The shared plan sets the performance budget. Your weapon slice does not get to
relax it.

## Definition of done

- Preflight recorded, including the preset name in force, the combat preset
  used, and every place a plan document disagreed with the code.
- The {{WEAPON}} profile row is immutable, validates its bounds, carries the
  approved values, resolves under its exact `CombatLoadout` key, and is
  documented as provisional gameplay tuning.
- Boundary tests pass at one raw unit outside, exact equality, and one raw unit
  inside, for entry distance, ally clearance, disengage entry, and disengage
  release — asserting the shared convention, not a weapon-local one.
- Pace caps, facing step caps, acceleration and deceleration, commitment and
  recovery durations, and collision-clamped retained pace are all pinned by
  test at the approved values.
- Count behaviour is proved by test: self-inclusion, dead and out-of-radius
  exclusion, hysteresis between the two thresholds, zero-hostile behaviour, and
  integer cross-product overflow safety — with no division and no float.
- Matchup and group coverage from {{PLAN}} runs through the shared scenario
  matrix, with the rejected failure modes shown absent and no equal-win-rate
  assertion anywhere.
- Every existing preset's fixtures are byte-identical, and no content hash
  moved.
- No shared file was edited. Every shared defect found is handed off as a named
  failing test.
- `./scripts/verify.ps1` output pasted in full, by you.
- No manual smoke-checklist row flipped to `PASS` by any agent. Untested rows
  stay `PENDING`; blocked ones are reported as `BLOCKED` honestly.

## Report back in this shape

1. **Preflight** — preset in force, combat preset used, foundation present or
   missing, boundary-convention differences found, baseline gate result.
2. **What landed** — files created or changed, one line each, with commit
   hashes.
3. **Tests** — the filters you ran and their real results.
4. **Calibration** — any value you moved, its old and new setting, the approved
   range it stayed inside, and the seed set that justified it. If you moved
   nothing, say so.
5. **Gate** — pasted `verify.ps1` output.
6. **Handoffs** — shared defects found, each as symbol, input, expected,
   actual, and the failing test that proves it.
7. **Left out** — anything in {{PLAN}} you did not do, and why. Scaling this
   work down is the user's call, not yours: finish everything else in full and
   say plainly what is missing.

Commits are Conventional Commits, normal English, one logical change each,
exactly as {{PLAN}} specifies. Stage only the paths that actually changed.

## What to do if you disagree with the scope

Say so in a sentence or two, then build it anyway under stated assumptions. If
a constraint above turns out to be genuinely impossible rather than merely
inconvenient, stop on that one item, finish everything else in full, and report
exactly what you left out and why.
