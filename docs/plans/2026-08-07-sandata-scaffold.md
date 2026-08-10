# Sandata scaffold — task plan

Date: 2026-08-07
Status: **executed and merged.** All twelve waves are on `main` as of 2026-08-09;
every numbered task through 91 is done and the task list is empty. Kept live
rather than archived because the wave results below are Sandata's only written
record of what each change measured, and because nine design questions raised
during execution are still open. New Sandata work gets its own plan document.
Branch: `sandata-scaffold`, based on `main` at `8743e8b`
Design: [2026-08-07-sandata-scaffold-design.md](2026-08-07-sandata-scaffold-design.md)
Research: [../research/2026-08-07-sandata-research-consolidated.md](../research/2026-08-07-sandata-research-consolidated.md)

This plan executes the design document. It writes no design decisions of its
own; where a task looks like it needs one, the design section is named in the
"What" column and the answer is there.

## How to read this table

- **Wave** groups tasks that may run in parallel. Every task in a wave has all
  its dependencies satisfied by an earlier wave, and no two tasks in the same
  wave write the same file. Eight parallel agents is the ceiling, so no wave
  exceeds eight tasks.
- **Files** lists every path a task is allowed to create or modify. A task that
  needs a file outside its list stops and reports rather than editing it. Two
  agents editing one file in parallel is a merge conflict created on purpose.
- **Done when** names the verification. "It compiles" is never a verification of
  logic. Every row names a test, a script, or a recorded number.
- **Verified** is filled in by the agent that completed the task, with the real
  command output, and by nobody else.

## Standing rules for every task

1. `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not weaken
   a test, a warning, or an analyzer to get green.
2. No new NuGet package. `SourceHygieneTests.PinnedPackageNames` is asserted for
   exact equality and a new package fails the gate.
3. `Sandata.Core` may not contain `float`, `double`, `System.Random`,
   `Math.Sqrt`, `Math.Atan2`, `Dictionary<`, `HashSet<`, or `PriorityQueue<`
   outside a doc comment. Task 7 makes this a test.
4. No `Console.Write*`, no `Debug.WriteLine`, no bespoke text file. Logging goes
   through `Hukbo.Diagnostics.DiagnosticLog`.
5. Repository documentation, code comments, and commit messages are written in
   full, normal English. Prose compression is for agent-to-agent prompts only.
6. `Hukbo.slnx` is a single-writer file. Only task 1 edits it. A later task that
   needs a new project stops and reports.
7. The canonical gate is not delegated. No task in this plan may report
   `./scripts/verify.ps1` output it did not itself produce, and no task may flip
   a manual smoke-checklist row in `docs/development/testing.md` to `PASS`.
8. Nothing calls ElevenLabs. Task 40 produces a manifest and stops.

## Task table

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 1 | Solution scaffolding and tier-1 extraction | Create `Hukbo.Shared.Core` and `git mv` exactly the **four** files named in design section 3 into it, keeping their namespaces and pinning `<RootNamespace>Hukbo.Core</RootNamespace>`. **`FacingRules.cs` does not move** — see the 2026-08-07 amendment in design section 3. Add `InternalsVisibleTo` for `Hukbo.Core`, `Hukbo.Core.Tests`, `Sandata.Core`, `Sandata.Core.Tests`. Add a `ProjectReference` from `Hukbo.Core`. Create the five empty Sandata projects with the csproj shapes in design section 3. Add all six projects to `Hukbo.slnx`. Regenerate every `packages.lock.json`. **Change no `using` directive anywhere.** | `src/Hukbo.Shared.Core/Hukbo.Shared.Core.csproj`, `src/Hukbo.Shared.Core/Properties/AssemblyInfo.cs`, `src/Hukbo.Shared.Core/Mathematics/FixedPoint.cs` (moved), `src/Hukbo.Shared.Core/Determinism/SplitMix64.cs` (moved), `src/Hukbo.Shared.Core/Determinism/Fnv1a.cs` (moved), `src/Hukbo.Shared.Core/Movement/Facing16.cs` (moved), `src/Hukbo.Core/Hukbo.Core.csproj`, `src/Sandata.Core/Sandata.Core.csproj`, `src/Sandata.Core/Properties/AssemblyInfo.cs`, `src/Sandata.Client/Sandata.Client.csproj`, `src/Sandata.Headless/Sandata.Headless.csproj`, `tests/Sandata.Core.Tests/Sandata.Core.Tests.csproj`, `tests/Sandata.Client.Tests/Sandata.Client.Tests.csproj`, `Hukbo.slnx`, every `packages.lock.json` | `./scripts/build.ps1` and `./scripts/test.ps1 -Configuration Release` both pass, and `git diff -M` shows all four moved files as pure renames. Two deviations from the original "zero changed lines outside the moved files" bar are expected and permitted; see the note below the table. Anything beyond those two stops the task. | — | **DONE 2026-08-07.** Four renames at 100%. Build: `0 Error(s)`, `Build succeeded` (one benign MonoGame content warning, resolved by task 33). Tests: 2,614 + 3,049 = 5,663 passed, zero failed. `git diff --stat -- '*.cs'` shows exactly one changed tracked file, `src/Hukbo.Core/Properties/AssemblyInfo.cs`, `1 file changed, 11 insertions(+)`. |
| 2 | 2 | `FixedPoint` multiply, divide, square root | Add `operator *`, `operator /`, and a public `Sqrt` as specified in design section 6. All `checked`. Truncation toward zero documented as a behavioural contract in the XML doc. Divide by zero throws. `Sqrt` rejects negatives. Pure addition — change no existing member. | `src/Hukbo.Shared.Core/Mathematics/FixedPoint.cs`, `tests/Hukbo.Core.Tests/FixedPointArithmeticTests.cs` | `FixedPointArithmeticTests` passes with pinned golden vectors covering both signs, the exact-half truncation case, perfect and non-perfect squares, zero, and the overflow boundary that must throw. Existing `Hukbo.Core.Tests` still green. | 1 | |
| 3 | 2 | `Bam16` and `IntegerMath` | Implement the binary angular measurement type (design section 6): `ShortestArc` as the `short` cast of the unsigned difference, `FromFacing16` at 4096 per sector, `ToFacing16` with the half case pinned upward. Implement `FloorDiv` and `FloorMod` branchless. | `src/Sandata.Core/Mathematics/Bam16.cs`, `src/Sandata.Core/Mathematics/IntegerMath.cs`, `tests/Sandata.Core.Tests/Bam16Tests.cs`, `tests/Sandata.Core.Tests/IntegerMathTests.cs` | Both test classes pass. `Bam16Tests` covers all sixteen `Facing16` round trips, both `ToFacing16` half boundaries, all four quadrant crossings of `ShortestArc`, and both wrap directions. `IntegerMathTests` covers negatives, exact multiples, and a divisor of one. | 1 | |
| 4 | 2 | Exact geometric predicates | Implement `Orient` returning sign only in `long`, and `ClassifySegments` returning the four named results `Disjoint`, `Crossing`, `Touching`, `CollinearOverlap`. No epsilon anywhere. Each named result carries a written rule in its XML doc. | `src/Sandata.Core/Geometry/ExactPredicates.cs`, `src/Sandata.Core/Geometry/SegmentRelation.cs`, `tests/Sandata.Core.Tests/ExactPredicatesTests.cs` | `ExactPredicatesTests` passes with at least one pinned vector per named result plus the collinear, shared-endpoint, T-junction, and both-turn-direction cases, and a magnitude case near the `long` product boundary. | 1 | |
| 5 | 2 | Fine trigonometry: sine table and CORDIC | Implement the 257-entry quarter-wave sine table at scale 65,536 with integer linear interpolation and quadrant reflection by index arithmetic, and integer CORDIC `Atan2` in vectoring mode, sixteen iterations, sixteen pinned arctangent constants. Both replace banned `double` transcendentals. | `src/Sandata.Core/Mathematics/Trig.cs`, `src/Sandata.Core/Mathematics/Cordic.cs`, `tests/Sandata.Core.Tests/TrigTests.cs`, `tests/Sandata.Core.Tests/CordicTests.cs` | Both pass. `TrigTests` asserts the exact endpoints 0 and 65,536, first-quadrant monotonicity across all 257 entries, the four quadrant boundaries, and two interpolated mid-entries. `CordicTests` asserts the eight axis and diagonal directions exactly and sweeps a pinned expected table. Table values are pinned from the mathematical definition, never read back from the implementation. | 1 | |
| 6 | 2 | Ray-box and point-in-polygon | Slab-method ray versus axis-aligned box **without division**, each parametric bound kept as a rational numerator-denominator pair compared by cross-multiplication with denominator signs carried explicitly. Point-in-polygon by crossing number with the half-open edge rule `y1 <= py < y2`. | `src/Sandata.Core/Geometry/RayBox.cs`, `src/Sandata.Core/Geometry/Polygon.cs`, `tests/Sandata.Core.Tests/RayBoxTests.cs`, `tests/Sandata.Core.Tests/PolygonTests.cs` | Both pass. `RayBoxTests` covers axis-parallel rays, corner grazes, and an origin inside the box. `PolygonTests` covers a vertex hit, an edge hit, a horizontal edge, and a ray exiting through a vertex. Neither file contains a division of a parametric value. | 1 | |
| 7 | 2 | Widen the build-gate tests to cover Sandata | Add `src/Sandata.Client/Program.cs` and `src/Sandata.Headless/Program.cs` to `ConsoleOwners`. Add facts asserting `Sandata.Core`'s and `Hukbo.Shared.Core`'s assemblies do not reference `Hukbo.Diagnostics`, with `Sandata.Headless` as the positive control that proves the assertion can fail. Add a source scan of `src/Sandata.Core` for the string `Hukbo.Diagnostics`. Add the banned-token scan of `src/Sandata.Core` for `float`, `double`, `System.Random`, `Math.Sqrt`, `Math.Atan2`, `Dictionary<`, `HashSet<`, `PriorityQueue<`, excluding doc comments the way the existing `System.Random` scan already does. Leave `PinnedPackageNames` untouched. | `tests/Hukbo.Client.Tests/SourceHygieneTests.cs`, `tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs`, `tests/Sandata.Core.Tests/SandataSourceHygieneTests.cs` | All facts pass with the projects empty, and each new assertion is proven able to fail by temporarily introducing the offending token locally and observing the red before removing it. The positive control on `Sandata.Headless` is a real assertion, not a comment. | 1 | |
| 8 | 2 | Map record types and tokenizer | Implement the record types for `HKMAP`, `NAME`, `GRID`, `WALL`, `DOOR`, `COVER`, `SPAWN`, `OBJECTIVE`, `END` and the tokenizer. `NumberStyles.None` with `CultureInfo.InvariantCulture`. Blank lines and `#` comment lines removed before parsing. Single-space separation, empty token is an error. Every malformed input is a hard load error with a message naming the line number and the rule broken. Per-record field validation from design section 12. Cross-record validation is task 25. | `src/Sandata.Core/Maps/MapRecord.cs`, `src/Sandata.Core/Maps/MapRecordKind.cs`, `src/Sandata.Core/Maps/MapTokenizer.cs`, `src/Sandata.Core/Maps/MapLoadException.cs`, `tests/Sandata.Core.Tests/MapTokenizerTests.cs` | `MapTokenizerTests` passes with one test per named rejection rule: negative sign, decimal point, group separator, leading whitespace, trailing whitespace, empty token, unknown record kind, wrong token count, non-integer token, out-of-range field, `HKMAP` not on line 1, wrong version, `END` not last, `GRID` cell size not a power of two, and grid dimension over 512 cells. | 1 | |
| 9 | 2 | Preset, ruleset shell, and the content-hash helper | Declare `SandataPresetId.ModernTacticalV1 = 1`, append-only, with a test pinning its numeric value. Declare `SandataRuleset` holding the tick rate, the millisecond-to-tick conversion rule identifier, `PathLatencyTicks`, `GroupCohesionRadius`, `LoweredWallDistanceWu`, and `AimToleranceBam`, with a `ContentHash` computed by FNV-1a over its field stream in a fixed order. Provide the shared FNV-1a folding helper Sandata uses. | `src/Sandata.Core/Rules/SandataPresetId.cs`, `src/Sandata.Core/Rules/SandataRuleset.cs`, `src/Sandata.Core/Determinism/SandataHash.cs`, `tests/Sandata.Core.Tests/SandataRulesetTests.cs` | `SandataRulesetTests` passes: the preset's numeric value is pinned, the content hash is pinned to a recorded value, and changing any single ruleset field is shown to move the hash. | 1 | |
| 10 | 3 | Nav grid, neighbour table, and the octile heuristic | Flat-array `NavGrid` sized `width * height`, `nodeIndex = y * width + x`, cell size 4 world units, world-to-cell by shift. One pinned static neighbour offset table in the fixed order east, south-east, south, south-west, west, north-west, north, north-east. Integer octile heuristic `10 * (max - min) + 14 * min`. Diagonal moves rejected when either orthogonal neighbour is blocked. No dictionary, no hash set. | `src/Sandata.Core/Navigation/NavGrid.cs`, `src/Sandata.Core/Navigation/NavNeighbors.cs`, `src/Sandata.Core/Navigation/NavHeuristic.cs`, `tests/Sandata.Core.Tests/NavGridTests.cs`, `tests/Sandata.Core.Tests/NavHeuristicTests.cs` | Both pass. `NavGridTests` asserts the index round trip at all four corners and the shift-based conversion against a hand-computed table. `NavHeuristicTests` pins the octile value for the eight compass directions and two arbitrary offsets, and asserts admissibility against a reference on a 16 by 16 open grid. `NavNeighbors` order is asserted element by element. | 3 | |
| 11 | 3 | Vision cone containment | `VisionCone.Contains(Bam16 centre, ushort halfWidth, long dx, long dy)` using two half-plane cross products against boundary vectors from a pinned table. **Never a cosine comparison** — design section 6 records why the existing sector vectors are not unit length. Every term inside `long`, no normalisation, no length assumption. | `src/Sandata.Core/Geometry/VisionCone.cs`, `src/Sandata.Core/Geometry/ConeBoundaryTable.cs`, `tests/Sandata.Core.Tests/VisionConeTests.cs` | `VisionConeTests` passes: on-boundary inclusion asserted on both edges, a point just outside each edge excluded, a point behind the apex excluded, a reflex cone above 180 degrees handled, and a test asserting the file contains no multiplication of two normalised vectors and no `Trig` call. | 3, 4 | |
| 12 | 3 | Weapon enumerations, pinned | Declare `FirearmId` (38 members, append-only), `WeaponClass`, `CaliberFamily` (eight members), `MechanismGroup` (four members), and `[Flags] FireModeSet` with `Safe`, `Single`, `Burst2`, `Burst3`, `Auto`. Every numeric value explicit. No behaviour. | `src/Sandata.Core/Weapons/FirearmId.cs`, `src/Sandata.Core/Weapons/WeaponClass.cs`, `src/Sandata.Core/Weapons/CaliberFamily.cs`, `src/Sandata.Core/Weapons/MechanismGroup.cs`, `src/Sandata.Core/Weapons/FireModeSet.cs`, `tests/Sandata.Core.Tests/WeaponEnumTests.cs` | `WeaponEnumTests` passes: every enum member's numeric value pinned as a literal, `FirearmId` dense from 0 with 38 members, `CaliberFamily` exactly the eight families in design section 10, and `FireModeSet` flag values distinct powers of two. | 1 | |
| 13 | 3 | Sandata theme record and catalog | Declare `SandataThemeColors` with the 39 roles enumerated in design section 11, its metrics record, its catalog with `ValidateDocument` rejecting unknown and missing roles, and at least two shipped themes. Faction colours are theme-independent constants. **Touch no file under `src/Hukbo.Client/Theming`.** | `src/Sandata.Client/Theming/SandataTheme.cs`, `src/Sandata.Client/Theming/SandataThemeCatalog.cs`, `src/Sandata.Client/Theming/SandataFactionPalette.cs`, `src/Sandata.Client/Content/Themes/sandata-theme-standards.json`, `tests/Sandata.Client.Tests/SandataThemeTests.cs` | `SandataThemeTests` passes: role count is exactly 39, the role name set matches the design list exactly, an unknown role in the JSON is rejected, a missing role is rejected, every required contrast pair clears its threshold in every shipped theme, and friendly, hostile, and unknown colours are identical across themes. | 1 | |
| 14 | 3 | Entry points and log events | Create both `Program.cs` files with argument parsing, `--help`, and the headless exit-code contract mirroring `Hukbo.Headless`. Add the `sandata.*` `ev` constants and channel names to `LogEvents`. Wire `DiagnosticLog` into both entry points writing `artifacts/logs/sandata-<utc>-<pid>.jsonl`, default `dbg` in Debug and `off` in Release, honouring `HUKBO_LOG_LEVEL`, `HUKBO_LOG_CHANNELS`, `HUKBO_LOG_DIR`. | `src/Sandata.Headless/Program.cs`, `src/Sandata.Client/Program.cs`, `src/Hukbo.Diagnostics/LogEvents.cs`, `tests/Sandata.Core.Tests/HeadlessArgumentTests.cs` | `HeadlessArgumentTests` passes: `--help` returns exit code 0, an unknown flag returns the documented non-zero code, and every new `ev` constant is a stable dotted identifier carrying no value and no count. `SourceHygieneTests.OnlyTheEntryPointsWriteDirectlyToTheConsole` is green with both new `Program.cs` files present. | 7 | |
| 15 | 3 | Map canonicalisation and content hash | Normalise wall and door endpoints to lexicographic ascending order, sort body records by `(kindOrdinal, fields...)` with the ordinals in design section 12, detect duplicates as a hard error, and compute `MapContentHash` as FNV-1a over the canonical record stream — kind ordinal as one byte, each integer field as four big-endian bytes. Comments, whitespace, and line order must not reach the hash. | `src/Sandata.Core/Maps/MapCanonicalizer.cs`, `src/Sandata.Core/Maps/MapContentHash.cs`, `tests/Sandata.Core.Tests/MapCanonicalizerTests.cs` | `MapCanonicalizerTests` passes: a scrambled-line-order input produces a byte-identical canonical stream to the sorted input, a reversed wall endpoint is detected as a duplicate of its normalised twin, adding or removing a comment does not move the hash, and changing one coordinate by one does. | 8, 9 | |
| 16 | 3 | Sandata collision grid, pair emission, and resolver | Sandata's own uniform grid, pair list, and three-phase resolver, following the propose-prioritise-commit shape without sharing Hukbo's code — design section 3 records why tier 2 extraction is deferred. Pairs normalised to `(lower, higher)` and sorted ascending. Commit order is `(groupId, slotIndex, entityId)`; until groups exist, `entityId` alone. | `src/Sandata.Core/Collision/SandataCollisionGrid.cs`, `src/Sandata.Core/Collision/SandataCollisionPair.cs`, `src/Sandata.Core/Collision/SandataCollisionResolver.cs`, `tests/Sandata.Core.Tests/SandataCollisionTests.cs` | `SandataCollisionTests` passes: the emitted pair list is identical for a permuted insertion order, two bodies at the same position resolve to a deterministic separation, and a hand-built eight-body fixture produces a pinned committed-position list. | 1 | |
| 17 | 3 | Mission, mission state, snapshot, and the state hasher | Declare `Mission` (the `Scenario` equivalent: format version, seed, map reference, tick policy, two faction setups, ruleset id, `MissionContentHash`), `MissionState`, and the snapshot record. Implement `SandataStateHasher` over exactly the authoritative field list in design section 4, in a fixed field order. Nothing on the derived list may appear in the snapshot or the hasher. | `src/Sandata.Core/Simulation/Mission.cs`, `src/Sandata.Core/Simulation/MissionState.cs`, `src/Sandata.Core/Simulation/MissionSnapshot.cs`, `src/Sandata.Core/Determinism/SandataStateHasher.cs`, `tests/Sandata.Core.Tests/MissionStateTests.cs` | `MissionStateTests` passes: `Mission` validation rejects each invalid field with a named exception, the snapshot round-trips to an equal `MissionState`, the state hash is stable across two constructions of the same state, and a reflection-based test asserts that no member name on the design's derived list appears on `MissionSnapshot`. | 9 | |
| 18 | 4 | Wall rasterisation and body-radius inflation | Rasterise `WALL` records and closed `DOOR` records into `NavGrid.passability`, then inflate blocked cells by the body radius so the grid encodes "a body fits here". Tag door cells as high-cost-but-passable to the planner and impassable to the mover until opened. Derived at load; never stored, never hashed. | `src/Sandata.Core/Navigation/NavBake.cs`, `src/Sandata.Core/Navigation/NavCellFlags.cs`, `src/Sandata.Core/Navigation/NavGrid.cs` (amended 2026-08-07: task 18 owns the passability and flag arrays it populates; task 10 deliberately left their representation unchosen rather than guessing), `tests/Sandata.Core.Tests/NavBakeTests.cs` | `NavBakeTests` passes against a hand-computed 8 by 8 fixture asserted cell by cell, including an axis-parallel wall, a 26.57-degree wall, a closed door, and the inflation boundary at exactly one body radius. | 8, 10 | |
| 19 | 4 | Clearance field | Two-pass integer chamfer distance transform, weights `(10, 14)` matching the octile heuristic so a clearance value compares directly to a formation half-width. Pass one top-left to bottom-right over four neighbours, pass two bottom-right to top-left over the other four. Local rebuild on a door change, bounded by the radius of influence. | `src/Sandata.Core/Navigation/ClearanceField.cs`, `tests/Sandata.Core.Tests/ClearanceFieldTests.cs` | `ClearanceFieldTests` passes against a hand-computed 8 by 8 fixture asserted cell by cell, plus a test showing that a local rebuild after a door change produces a field identical to a full rebuild from scratch. | 10 | |
| 20 | 4 | Grid ray and the wall bucket index | Division-free Amanatides-Woo traversal: parametric values stay rational and are compared by cross-multiplication; the diagonal-corner tie steps X first by written rule. Build the compressed-sparse-row cell-to-wall-segment index for the line-of-sight narrow phase. Combine into the two-phase line-of-sight query. | `src/Sandata.Core/Navigation/GridRay.cs`, `src/Sandata.Core/Navigation/WallBuckets.cs`, `src/Sandata.Core/Navigation/LineOfSight.cs`, `tests/Sandata.Core.Tests/GridRayTests.cs`, `tests/Sandata.Core.Tests/LineOfSightTests.cs` | Both pass. `GridRayTests` pins the visited-cell sequence for an axis-parallel line, an exact diagonal through a corner, and an 18.4-degree line, and asserts the file contains no division of a parametric value. `LineOfSightTests` shows a case where supercover cell touching and true wall crossing disagree, and asserts the exact predicate wins. | 4, 10 | |
| 21 | 4 | Grid A\* and the total comparator | Binary heap over `int` node indices ordered by the total key `(f, h, nodeIndex)`. Flat `gScore`, `cameFrom`, and `visitStamp` arrays allocated once and never cleared between searches. No dictionary, no hash set, no `PriorityQueue`. Diagonal corner-cutting rejected. | `src/Sandata.Core/Navigation/NavSearch.cs`, `src/Sandata.Core/Navigation/NavOpenSet.cs`, `src/Sandata.Core/Navigation/NavComparer.cs`, `tests/Sandata.Core.Tests/NavSearchTests.cs`, `tests/Sandata.Core.Tests/NavComparerTests.cs` | Both pass. `NavComparerTests` is a property test over generated `(f, h, index)` triples asserting antisymmetry, transitivity, and totality. `NavSearchTests` asserts the path matches a naive Dijkstra reference on ten fixtures including equal-cost ties, a narrow passage, and an unreachable goal, and that the expanded-node sequence is identical across two runs and across a permuted insertion order. | 10 | |
| 22 | 4 | Firearm definitions, the 38-row catalog, and the ruleset hash | Declare `FirearmDefinition` with the fields in design section 9. Author all 38 rows in `FirearmId` order with timings in milliseconds. Add `WeaponNameSets` with `Manufacturer` and `Generic` string tables behind one `WeaponNameSetId` field. Fold the field stream into `FirearmRuleset.ContentHash`. | `src/Sandata.Core/Weapons/FirearmDefinition.cs`, `src/Sandata.Core/Weapons/FirearmCatalog.cs`, `src/Sandata.Core/Weapons/WeaponNameSets.cs`, `src/Sandata.Core/Weapons/FirearmRuleset.cs`, `tests/Sandata.Core.Tests/FirearmCatalogTests.cs` | `FirearmCatalogTests` passes: exactly 38 rows in dense `FirearmId` order, every `Modes` value is one of the five sets in design section 9, no row carries both `Burst2` and `Burst3`, both name sets have an entry for every row, `M4` and `M4A1` differ in `Modes`, the AUG's `MechanismGroup` is `Bullpup`, and `ContentHash` is pinned to a recorded value that moves when any single field changes. | 9, 12 | |
| 23 | 4 | Millisecond-to-tick conversion and the timing chain | Implement the pinned conversion `(ms * TickRate + 500) / 1000` and the weapon chain state machine `Lowered → Raising → Turning → Aiming → Firing → Resetting → Aiming` with per-phase remaining-tick counters in hashed state. Every zero-tick transition resolves in the same tick in one written order. Implement the cyclic-fire accumulator. | `src/Sandata.Core/Weapons/TickConversion.cs`, `src/Sandata.Core/Weapons/WeaponChain.cs`, `src/Sandata.Core/Weapons/WeaponChainPhase.cs`, `tests/Sandata.Core.Tests/WeaponChainTests.cs` | `WeaponChainTests` passes: the conversion is pinned for 80, 150, 180, 335, 350, 405, and 500 ms at tick rate 50; every zero-tick transition is exercised and resolves without swallowing or double-advancing a tick; and the 800 rpm accumulator produces the exact `4, 4, 4, 3` pattern over 40 ticks. | 9, 12 | |
| 24 | 4 | Sandata sound slot catalog | Declare `SoundSlot`, `SoundFamily`, `SoundEnvironment`, `FireMode`, the flat lookup index, and the base-name builder producing `<family>-<key>-<mode>-<environment>-<NN>.wav`. Keep the `{0:D2}` variant format and the 99 cap. **Touch no file under `src/Hukbo.Client/Audio`.** | `src/Sandata.Client/Audio/SoundSlot.cs`, `src/Sandata.Client/Audio/SoundFamily.cs`, `src/Sandata.Client/Audio/SoundEnvironment.cs`, `src/Sandata.Client/Audio/SandataSoundCatalog.cs`, `tests/Sandata.Client.Tests/SandataSoundCatalogTests.cs` | `SandataSoundCatalogTests` passes: every declared tuple resolves to exactly one row, no tuple resolves to two, every `VariantCount` is between 1 and 99, the base names for the nine examples in design section 10 are produced exactly, the bullpup selector slot is distinct from the AR and AK selector slots, and the lookup uses no dictionary. | 12 | |
| 25 | 4 | The `angle-house` fixture and cross-record validation | Add `angle-house.hkmap` exactly as printed in design section 12. Implement cross-record validation: spawn presence per faction, spawn separation, full enclosure by flood fill from outside the bounding box, faction-0 reachability to every objective with all doors passable, and no duplicate of any kind. Record the measured `MapContentHash` as the golden expectation. | `src/Sandata.Core/Maps/MapValidator.cs`, `tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap`, `tests/Sandata.Core.Tests/MapValidatorTests.cs`, `tests/Sandata.Core.Tests/AngleHouseFixtureTests.cs` | Both pass. `MapValidatorTests` has one failing-fixture test per cross-record rule. `AngleHouseFixtureTests` asserts the fixture parses, canonicalises to itself byte for byte, contains exactly five walls whose run is neither horizontal nor vertical, contains one open and two closed doors, contains exactly one 360-degree cover, contains one material-3 wall that is not on the outer shell, and pins `MapContentHash` to the recorded value. | 8, 15 | |
| 26 | 5 | Funnel string-pull port | Port Recast's simple stupid funnel algorithm to integers from DotRecast (zlib), with a licence header naming the source, the licence, and the fact that only attribution is required. Uses `ExactPredicates.Orient` alone — no other geometry primitive. Consumes an A\* corridor and emits a polyline snapped to real wall geometry. | `src/Sandata.Core/Navigation/Funnel.cs`, `src/Sandata.Core/Navigation/ThirdPartyNotices.md`, `tests/Sandata.Core.Tests/FunnelTests.cs` | `FunnelTests` passes: a straight corridor collapses to two points, an L corridor to three, and a corridor along the fixture's 26.57-degree wall emits a single straight segment rather than a staircase. A test asserts the licence header is present and names DotRecast and zlib. | 4, 21 | |
| 27 | 5 | Path service with fixed-latency amortisation | A path requested at tick `t` becomes valid at `t + PathLatencyTicks` regardless of how many searches completed. Request records `(groupId, startCellIndex, goalCellIndex, requestTick)` are authoritative and snapshotted; results are derived and recomputed on resume. At most one search per group per tick. A group with no valid path holds position and emits an inspectable reason code. **No "no path yet, move at the goal" fallback.** | `src/Sandata.Core/Navigation/PathService.cs`, `src/Sandata.Core/Navigation/PathRequest.cs`, `src/Sandata.Core/Navigation/PathReasonCode.cs`, `tests/Sandata.Core.Tests/PathServiceTests.cs` | `PathServiceTests` passes: a path becomes valid on exactly `requestTick + PathLatencyTicks` whether one or eight searches ran that tick; a group with an outstanding request does not enqueue a second; a group with no path holds and reports a reason code; and recomputing every published path from its stored request reproduces the identical polyline. | 21 | |
| 28 | 5 | Union-find squad grouping | Deterministic union-find with path compression and union by size over the normalised, ascending-sorted pair list. Group identity is the minimum entity id in the component; leader is the lowest living entity id. **Both derived, neither stored.** Slot assignment within a group by ascending entity id. | `src/Sandata.Core/Squads/SquadGrouping.cs`, `src/Sandata.Core/Squads/UnionFind.cs`, `src/Sandata.Core/Squads/SquadSlot.cs`, `tests/Sandata.Core.Tests/SquadGroupingTests.cs` | `SquadGroupingTests` passes: a chain, a ring, and two disjoint clusters produce the expected group ids; permuting the input pair order changes nothing; killing the leader re-derives the next-lowest living id on the same tick with no intermediate leaderless state; and a reflection test asserts no group state is stored on `MissionState`. | 16 | |
| 29 | 5 | Recursive shadowcasting field of view | Port recursive shadowcasting from GoRogue (MIT) with a licence header, in integers, over the nav grid, producing the per-faction fog-of-war cell visibility layer that sits behind the per-unit cone. Derived, never hashed. | `src/Sandata.Core/Sensing/Shadowcast.cs`, `src/Sandata.Core/Sensing/VisibilityField.cs`, `tests/Sandata.Core.Tests/ShadowcastTests.cs` | `ShadowcastTests` passes: symmetry on an open grid, a pillar casting the expected shadow on a hand-computed 16 by 16 fixture, a corner peek revealing exactly the cells listed in the fixture, and identical output across two runs on the same input. The licence header names GoRogue and MIT. | 20 | |
| 30 | 5 | Directional cover model | Flat 50 percent damage and hit reduction, applied only within the arc a cover object faces unless `arcHalfBam` is 32768. Fire from the flank or rear ignores cover entirely. Crouching behind cover is near-total protection and forbids firing. Only the operator inside the arc benefits. | `src/Sandata.Core/Combat/CoverRules.cs`, `src/Sandata.Core/Combat/CoverState.cs`, `tests/Sandata.Core.Tests/CoverRulesTests.cs` | `CoverRulesTests` passes: incoming fire exactly on the arc boundary is covered and one Bam outside it is not; two operators behind the same object, one inside and one outside the arc, get different results; rear fire ignores cover; a 32768 half-width object covers from every direction; and a crouched operator is near-immune and cannot produce a fire proposal. | 11, 25 | |
| 31 | 5 | Fire-mode band selection and accuracy interpolation | The ordered, total selection rule from design section 9, with `Burst3` tested before `Burst2`. Linear integer dispersion interpolation clamped at `MaxEffectiveWu`. The angular error draw comes from the named `Accuracy` RNG stream keyed `(missionSeed, Accuracy, entityId)`. | `src/Sandata.Core/Combat/FireModeSelection.cs`, `src/Sandata.Core/Combat/AccuracyRules.cs`, `tests/Sandata.Core.Tests/FireModeSelectionTests.cs`, `tests/Sandata.Core.Tests/AccuracyRulesTests.cs` | Both pass. `FireModeSelectionTests` asserts the selected mode on both sides of every band boundary for one weapon of each of the five mode sets, and that beyond `SingleBandMaxWu` no engagement is produced. `AccuracyRulesTests` pins dispersion at 0, at `MaxEffectiveWu`, and beyond it, and asserts the draw is reproducible from the same seed and entity id. | 22 | |
| 32 | 5 | The weapon-lowered rule | An operator is lowered while within `LoweredWallDistanceWu` of any wall segment or inside a door cell, unless `ExemptFromLoweredRule`. Raising re-imposes `ReadyMs`. Evaluated against the position committed this tick, using the wall bucket index and the door cell tag. The flag is hashed state and its transition emits an authoritative event. | `src/Sandata.Core/Combat/WeaponLoweredRules.cs`, `tests/Sandata.Core.Tests/WeaponLoweredRulesTests.cs` | `WeaponLoweredRulesTests` passes: lowered at exactly `LoweredWallDistanceWu` and not one unit beyond; lowered inside a door cell and not in the adjacent cell; a pistol carrier is never lowered; and the raise path costs exactly `ReadyTicks` before the chain can reach `Aiming`. | 20, 23 | |
| 33 | 5 | Sandata client shell | The MonoGame window, the game loop, the spectator camera reused from the client's existing camera helper, and the world renderer for walls, doors, cover objects, and objectives, using the Sandata theme. No simulation decisions anywhere in this task. | `src/Sandata.Client/SandataGame.cs`, `src/Sandata.Client/Rendering/WorldRenderer.cs`, `src/Sandata.Client/Rendering/SandataCamera.cs`, `src/Sandata.Client/Content/Content.mgcb`, `src/Sandata.Client/Sandata.Client.csproj` (amended 2026-08-07 — task 33 owns the csproj so it can add the copy rule that puts `Content/Themes/*.json` into the build output; task 13 could not reach it and the theme JSON currently never ships), `src/Sandata.Client/Program.cs` (amended 2026-08-07 — task 33 is the task that constructs and runs `SandataGame`, so it takes ownership from task 14 at this point), `tests/Sandata.Client.Tests/WorldRendererGeometryTests.cs` | `WorldRendererGeometryTests` passes against the pure geometry helpers only — no `GraphicsDevice`, no `SpriteBatch`, no window. It asserts wall, door, and cover draw rectangles for the `angle-house` fixture at three zoom levels, and that a 26.57-degree wall produces a rotated block rather than an axis-aligned one. | 13, 25 | |
| 34 | 6 | Arclength slot targeting | Precompute cumulative integer arclength on the shared polyline. Each unit's target is a pure function of its slot's trail offset and lateral offset along that arclength, so followers stand on the leader's past path and cut the same corners. Binary search into the arclength array. | `src/Sandata.Core/Squads/SlotTargets.cs`, `src/Sandata.Core/Squads/PolylineArclength.cs`, `tests/Sandata.Core.Tests/SlotTargetsTests.cs` | `SlotTargetsTests` passes: on a right-angle corner a follower's target stays inside the corridor where a rigid world-space lateral offset would place it inside the wall; the arclength binary search returns the exact vertex at an exact-vertex query; and slot targets are identical across two evaluations of the same polyline. | 26 | |
| 35 | 6 | Sensing: contact tiers, memory ghosts, alert, and hearing | Three contact tiers — unknown, question-mark (present but beyond identify range and not shootable), identified. World state is remembered rather than live: a ghost carries the last known cell and the tick last seen. Three-state faction alert `Calm`, `Raised`, `Breach`. Sound as a parallel sense with the published radii, breaking glass louder than gunfire, death screams propagating. All of it reads the frozen tick-start view only. | `src/Sandata.Core/Sensing/ContactMemory.cs`, `src/Sandata.Core/Sensing/ContactTier.cs`, `src/Sandata.Core/Sensing/AlertLevel.cs`, `src/Sandata.Core/Sensing/HearingRules.cs`, `tests/Sandata.Core.Tests/ContactMemoryTests.cs`, `tests/Sandata.Core.Tests/HearingRulesTests.cs` | Both pass. `ContactMemoryTests` asserts each tier transition at its exact range boundary, that a ghost persists at the last seen cell with an increasing age, and that a door opened out of sight is not observed until seen. `HearingRulesTests` pins each published radius in world units and asserts breaking glass exceeds gunfire. | 11, 29 | |
| 36 | 6 | Damage, death, and mission outcome | Simultaneous damage application, instant death with no downed state, and mission outcome resolution. Deaths resolve in ascending entity id after all damage is accumulated, so two mutual kills both land. | `src/Sandata.Core/Combat/DamageResolution.cs`, `src/Sandata.Core/Combat/OutcomeRules.cs`, `src/Sandata.Core/Combat/MissionOutcome.cs`, `tests/Sandata.Core.Tests/DamageResolutionTests.cs` | `DamageResolutionTests` passes: two operators killing each other on the same tick both die; damage from three sources accumulates before any death is resolved; the outcome is decided only after every death; and no code path produces a downed or bleeding state. | 17, 31 | |
| 37 | 6 | Operator geometry and persistent aim | `OperatorGeometry.Create` returning an `OperatorLayout` record of rectangles, points, and angles, with the fifteen layers in design section 11 and `Rectangle.Empty` for any layer that contributes nothing. Carries `WeaponAimBam` from the simulation plus a presentation-only smoothing term excluded from snapshot equality. Pure — no `GraphicsDevice`. | `src/Sandata.Client/Rendering/OperatorGeometry.cs`, `src/Sandata.Client/Rendering/OperatorLayout.cs`, `src/Sandata.Client/Rendering/OperatorRenderer.cs`, `tests/Sandata.Client.Tests/OperatorGeometryTests.cs` | `OperatorGeometryTests` passes: every layer's bounds pinned at three detail tiers, absent layers are `Rectangle.Empty`, the weapon rotates continuously about the grip anchor across a full turn without springing back, the muzzle anchor equals the weapon line tip, and the smoothing term is absent from the layout's equality members. | 13 | |
| 38 | 6 | HUD and operator inspector | Roster strip, contact list, alert indicator, mission clock and tick counter, event log retaining at most 200 ordered events, spectator control bar, and the operator inspector showing intent, reason code, chain phase and remaining ticks, cover state and arc, group id, slot index, and both the decision position and the resolution position. Pure layout helpers, tested without a graphics device. | `src/Sandata.Client/UI/RosterStrip.cs`, `src/Sandata.Client/UI/ContactList.cs`, `src/Sandata.Client/UI/AlertIndicator.cs`, `src/Sandata.Client/UI/MissionClock.cs`, `src/Sandata.Client/UI/SandataEventLog.cs`, `src/Sandata.Client/UI/SandataControlBar.cs`, `src/Sandata.Client/UI/OperatorInspector.cs`, `tests/Sandata.Client.Tests/HudLayoutTests.cs`, `tests/Sandata.Client.Tests/OperatorInspectorTests.cs` | Both pass. `HudLayoutTests` pins every element's rectangle at three window sizes and asserts the event log discards the oldest beyond 200. `OperatorInspectorTests` asserts every listed field is present and that the alert indicator differs by shape as well as colour across all three levels. | 13, 33 | |
| 39 | 6 | Sound player wiring and the tail-aware budget | Resolve a `ShotFired` event to a slot by the mapping in design section 10, select a variant with the existing `SoundVariantSelector`, and play through a Sandata budget that holds a reservation for `TailTicks` rather than one frame. Automatic fire plays one loop instance plus one tail instance per shooter, never one per round. Budget constants marked provisional in comments until task 53 measures them (**corrected 2026-08-07**: this row said task 49, which is the tick pipeline; task 53 is the measurement task, as the risk register and task 54's row both already said). | `src/Sandata.Client/Audio/SandataSoundPlayer.cs`, `src/Sandata.Client/Audio/SandataSoundBudget.cs`, `src/Sandata.Client/Audio/ShotSlotResolver.cs`, `tests/Sandata.Client.Tests/ShotSlotResolverTests.cs`, `tests/Sandata.Client.Tests/SandataSoundBudgetTests.cs` | Both pass. `ShotSlotResolverTests` asserts the slot chosen for every fire mode at every environment, and that the resolver is reachable only from the client. `SandataSoundBudgetTests` asserts a reservation is held for exactly `TailTicks`, that sustained automatic fire from eight shooters holds sixteen instances rather than one per round, and that the same tick and entity id always select the same variant. | 24, 33 | |
| 40 | 6 | Audio manifest generator and `sfx.ps1` batch mode | Create `scripts/sfx-manifest.ps1` with **no network code in it at all**: it enumerates the slot matrix from the catalog (**corrected 2026-08-07**: 106 rows expanding to 524 variant files, not the 484 this row originally stated), emits a manifest listing every file name, prompt, duration, and trim threshold, prints the credit and dollar estimate, and stops. Separately add a batch mode and per-family trim thresholds to `sfx.ps1` so gunshot families use 1 to 2 percent rather than the 5 percent melee default. | `scripts/sfx-manifest.ps1`, `scripts/sfx.ps1`, `tests/Sandata.Client.Tests/SoundManifestTests.cs` | `./scripts/sfx-manifest.ps1` writes a manifest with exactly the row count the catalog declares, and a grep of that script for any HTTP verb, `Invoke-RestMethod`, `Invoke-WebRequest`, or `ELEVENLABS` returns nothing. `SoundManifestTests` asserts the manifest's per-slot variant counts equal the catalog's declared counts. **This task does not call ElevenLabs and does not generate a single file.** | 24 | |
| 41 | 6 | Script game-target table | Create `scripts/_gametargets.ps1` with `Get-GameTarget`, and add `-Game` with `[ValidateSet('Hukbo','Sandata')]` defaulting to `'Hukbo'` to `run.ps1`, `test.ps1`, `benchmark.ps1`, and `package.ps1`, replacing their hardcoded paths with table lookups. `build.ps1`, `format.ps1`, and `bootstrap.ps1` are not touched — they operate on the solution. | `scripts/_gametargets.ps1`, `scripts/run.ps1`, `scripts/test.ps1`, `scripts/benchmark.ps1`, `scripts/package.ps1`, `tests/Hukbo.Client.Tests/ScriptTargetTests.cs` | `ScriptTargetTests` passes: the `Hukbo` branch of `_gametargets.ps1` contains the four exact literal project paths the scripts hardcoded before this change, and no remaining script body contains a hardcoded `src/Hukbo.` or `tests/Hukbo.` project path. `./scripts/test.ps1 -Configuration Release` with no `-Game` argument runs the same two Hukbo test projects and passes. | 14 | |
| 42 | 7 | Single-file collapse | When clearance at the leader's cell drops below the formation half-width, every slot's lateral offset goes to zero and the squad becomes single file; it re-expands on the far side. No state, no timer, no special case inside the pathfinder. | `src/Sandata.Core/Squads/FormationCollapse.cs`, `tests/Sandata.Core.Tests/FormationCollapseTests.cs` | `FormationCollapseTests` passes: on the `angle-house` fixture a four-slot group collapses at the entry door and re-expands inside; the collapse fires at exactly the clearance threshold and not one unit either side; and a reflection test asserts no collapse state is stored on `MissionState`. | 19, 34 | |
| 43 | 7 | Local avoidance: propose, prioritise, commit | Proposals accumulate into a write-only buffer with no unit seeing another's. Commit order is the total key `(groupId, slotIndex, entityId)`. A blocked unit tries one 22.5-degree sidestep, side chosen by a rule pinned on entity id parity, then waits a tick. **Never a force, never an impulse, never a push-apart.** | `src/Sandata.Core/Movement/LocalAvoidance.cs`, `src/Sandata.Core/Movement/MovementProposal.cs`, `src/Sandata.Core/Movement/SidestepRules.cs`, `tests/Sandata.Core.Tests/LocalAvoidanceTests.cs` | `LocalAvoidanceTests` passes: eight units funnelling into one doorway all pass through with no overlap and no deadlock within a pinned tick count; permuting the proposal insertion order changes nothing; the sidestep side is deterministic for a given entity id; and a scan of the movement folder finds no accumulation of a force or velocity vector. | 16, 28, 34 | |
| 44 | 7 | Intent selection | `Hold`, `Advance`, `Breach`, `Engage`, `Reposition`, `Dead`, chosen from the frozen tick-start view. Every selected intent carries an inspectable reason code so the operator inspector can explain a held position. | `src/Sandata.Core/Simulation/IntentSelection.cs`, `src/Sandata.Core/Simulation/OperatorIntent.cs`, `src/Sandata.Core/Simulation/IntentReasonCode.cs`, `tests/Sandata.Core.Tests/IntentSelectionTests.cs` | `IntentSelectionTests` passes: one fixture per intent producing that intent and no other; every returned intent carries a non-default reason code; and permuting the evaluation order of the operators produces an identical intent list. | 27, 35 | |
| 45 | 7 | In-world tactical overlays | Fire cone (`FireConeFill`, `FireConeEdge`), order path polyline, waypoint markers, and breach-point markers on material-3 walls. Tactical decision geometry renders at **every** detail tier rather than fading with zoom the way decorative layers do. | `src/Sandata.Client/Rendering/FireConeOverlay.cs`, `src/Sandata.Client/Rendering/OrderPathOverlay.cs`, `src/Sandata.Client/Rendering/BreachMarkerOverlay.cs`, `tests/Sandata.Client.Tests/OverlayGeometryTests.cs` | `OverlayGeometryTests` passes: the fire cone's boundary geometry matches `VisionCone`'s boundary vectors exactly rather than being recomputed; every overlay returns non-empty geometry at the lowest detail tier; and the breach marker appears on exactly the one material-3 wall in the `angle-house` fixture. | 33, 37 | |
| 46 | 7 | Scaffolded interaction layer | Minimap panel drawing nav passability at one pixel per cell with no interaction; multi-select state as a pure record with a marquee inclusion predicate; continuous drag-capture pointer state slotted above the in-world layer and below every panel; a typed undo stack with push, pop, and a depth limit and no producers. Types and UI only — nothing in v0.1 requires them to be driven. | `src/Sandata.Client/UI/Minimap.cs`, `src/Sandata.Client/UI/MultiSelectState.cs`, `src/Sandata.Client/UI/DragCapture.cs`, `src/Sandata.Client/UI/UndoStack.cs`, `tests/Sandata.Client.Tests/InteractionScaffoldTests.cs` | `InteractionScaffoldTests` passes: the marquee predicate includes and excludes at its exact boundary and never selects a hostile; drag capture consumes a drag that begins on a panel without producing a marquee; the undo stack discards the oldest at its depth limit; and the minimap's pixel rectangle for a known cell is pinned. | 33 | |
| 47 | 7 | `verify.ps1` game passthrough and `doctor.ps1` lock-file union | Add `-Game` to `verify.ps1`, passing it through to `test.ps1` and `benchmark.ps1`. Replace `doctor.ps1`'s fixed lock-file list with the union of both games' lock files. **The default gate keeps running the Hukbo workload alone**; the Sandata benchmark is added to the default only after task 51 records a baseline. | `scripts/verify.ps1`, `scripts/doctor.ps1`, `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` | `ScriptDefaultsTests` passes, asserting `verify.ps1` with no `-Game` argument invokes `benchmark.ps1` exactly once with `Agents 200`, `Ticks 10000`, `Seed 1`. `./scripts/doctor.ps1` reports every lock file in the repository as present and current. | 41 | |
| 48 | 7 | Audio instance-pool measurement harness | A hand-run harness under `tools/`, **not** added to `Hukbo.slnx` and **not** in the gate, matching how the existing measurement harnesses are treated. Sustains automatic fire from the maximum operator count and records the instance count at which `InstancePlayLimitException` first fires, on named hardware. | `tools/Sandata.Tools.AudioPool/Program.cs`, `tools/Sandata.Tools.AudioPool/Sandata.Tools.AudioPool.csproj` | The harness runs and prints an instance count, a hardware description, and a sustained-fire duration. `Hukbo.slnx` is unchanged, and `./scripts/build.ps1` does not build the harness. Numbers are reported to the plan, not written into any document — task 54 records them. | 39 | |
| 49 | 8 | Tick pipeline orchestrator and its order tests | Implement the fourteen stages in design section 5 with the frozen tick-start view taken at stage 3 and released at the end of stage 9. Stages 5 to 9 read the frozen view and write only into the proposal buffer; stages 10 to 14 read committed state and may not touch the frozen view. Write the tests that enforce both rules. | `src/Sandata.Core/Simulation/SandataSimulation.cs`, `src/Sandata.Core/Simulation/TickStartView.cs`, `src/Sandata.Core/Simulation/TickStage.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs` | `TickPipelineTests` passes: the executed stage sequence equals the pinned fourteen-stage list; permuting the processing order of every operator through stages 5 to 9 produces an identical proposal buffer; a reflection test asserts no type reachable from stages 10 to 14 holds a `TickStartView` field; and the view is disposed or invalidated at the end of stage 9 so a stage-11 read throws. | 32, 36, 42, 43, 44 | |
| 50 | 8 | Navigation benchmark matrix | Define and implement the benchmark workload `SIMULATION-GAME-STANDARDS.md` section 11 requires: map density, changed-cell count, concurrent seekers, query distance, and replanning rate as named parameters, reporting p50, p95, and p99 for both A\* query time and the stage-7 tick-stage time. Expose it behind headless flags. | `src/Sandata.Headless/NavBenchmark.cs`, `src/Sandata.Headless/NavBenchmarkOptions.cs`, `tests/Sandata.Core.Tests/NavBenchmarkOptionTests.cs` | `NavBenchmarkOptionTests` passes, asserting every required matrix parameter is a named option with a validated range and no default that hides a missing value. The benchmark runs to completion on the `angle-house` fixture and prints all six percentiles. Numbers are reported to the plan, not written into any document. | 27 | |
| 51 | 9 | Sandata headless runner | The determinism and benchmark runner: seed workload, the two independent hashes, the documented exit codes including a distinct code for a determinism mismatch carrying `firstMismatchTick`, and a `RunReport` carrying seed, tick count, both hashes, outcome, wall-clock percentiles, and allocated bytes. Logging off by default so the workload measures the simulation and not a writer. | `src/Sandata.Headless/HeadlessRunner.cs`, `src/Sandata.Headless/RunReport.cs`, `src/Sandata.Headless/Program.cs`, `tests/Sandata.Core.Tests/HeadlessRunnerTests.cs` | `HeadlessRunnerTests` passes: a seed-1 run produces a populated `RunReport`; a deliberately corrupted second run returns the mismatch exit code and a correct `firstMismatchTick`; and `--help` still returns 0. `./scripts/benchmark.ps1 -Game Sandata -Seed 1` completes and prints the report. | 14, 49 | |
| 52 | 10 | Determinism equivalence suite and golden replay | Four equivalence tests plus the golden replay: same-seed repeat in-process and in a fresh process; cold-cache equivalence after discarding and rebuilding every derived structure; save and resume equivalence across a mid-mission snapshot, which is the only proof that paths are genuinely derived; logging off versus `trc` producing identical hashes, outcome, and event stream; and a pinned seed-1 mission whose state hash and event hash are recorded as expected constants. | `tests/Sandata.Core.Tests/DeterminismEquivalenceTests.cs`, `tests/Sandata.Core.Tests/GoldenReplayTests.cs`, `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` | All five pass. `GoldenReplayTests` names the first mismatch tick on failure. The save-resume test asserts that the resumed run recomputed its paths rather than restoring them, by clearing the path cache before resuming and still matching. | 17, 51 | |
| 53 | 10 | Measurement runs | Run the navigation benchmark matrix and the audio instance-pool harness on named hardware and capture their raw output to `artifacts/`. Set the `SandataSoundBudget` constants from the measured pool ceiling and remove the provisional marker from those comments only. **Writes no document** — task 54 records the numbers. | `src/Sandata.Client/Audio/SandataSoundBudget.cs`, `artifacts/` (untracked output only) | Both runs complete and their raw output is captured. `SandataSoundBudgetTests` still passes with the measured constants. The measured pool ceiling, the sustained-fire duration, the hardware description, and all six navigation percentiles are reported to the plan. | 48, 50, 51 | |
| 54 | 11 | Documentation | Record the measured numbers from task 53 in `docs/development/testing.md` under a dated Sandata section, and add the manual smoke-checklist rows from design section 13 as `PENDING`. Update `README.md`, `AGENTS.md`, and `CLAUDE.md` for the two-game layout, the new projects, the `-Game` script parameter, and the `Hukbo.Shared.Core` boundary. Full, normal English throughout. | `docs/development/testing.md`, `README.md`, `AGENTS.md`, `CLAUDE.md` | Every measured figure in the new testing section is traceable to task 53's captured output. Every smoke row is `PENDING` and none is `PASS`. `CLAUDE.md` section 3's layout block lists all eleven projects, and its section 5 states the Sandata determinism additions. `SourceHygieneTests`' recorded-artifact fact still passes. | 52, 53 | |
| 55 | 12 | Canonical gate | Run `./scripts/verify.ps1` once, after everything above is integrated, and paste the real output. **Not delegated to any agent.** No sub-agent report substitutes for it. | none | The gate passes all five stages and the pasted output shows the Hukbo 200-agent, 10,000-tick, seed-1 workload producing the recorded baseline hashes unchanged. Then `./scripts/verify.ps1 -Game Sandata` is run and its output pasted separately. | 54 | |

### Task 56 — geometry reconciliation (added 2026-08-07, wave 3)

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 56 | 3 | Geometry reconciliation | Wave 2 ran tasks 4 and 6 in separate worktrees, and each was told not to depend on a file it could not see. The result is three private implementations of the cross-product sign test, in `ExactPredicates.cs`, `RayBox.cs`, and `Polygon.cs`. Collapse them onto `ExactPredicates.Orient` as the single definition, and settle the calling convention: flat `long` coordinates are the house style, so correct design section 6 rather than introducing a `Point` struct that would need its own equality, hashing, and ordering rules to stay deterministic. Change no behaviour and no test expectation. | `src/Sandata.Core/Geometry/ExactPredicates.cs`, `src/Sandata.Core/Geometry/RayBox.cs`, `src/Sandata.Core/Geometry/Polygon.cs`, `docs/plans/2026-08-07-sandata-scaffold-design.md` | Exactly one method in `src/Sandata.Core/Geometry/` computes a cross product, proven by a test that greps the folder and asserts a single definition. All 171 existing Sandata tests still pass unchanged — not one expectation is edited. Design section 6 states flat `long` coordinates and no longer mentions `Point` or `Box`. | 4, 6 | |

This is the price of the eight-agent fan-out, and it was the right trade: the
alternative was serialising tasks 4 and 6 behind one another to save a
reconciliation that costs far less than the wait would have. Recorded here so
the duplication is closed deliberately rather than discovered later.

### Wave 2 note: task 9's ruleset constants are provisional

Design sections 4 and 9 pin the tick rate at 50 Hz and the millisecond-to-tick
conversion exactly, but give no numeric value for `PathLatencyTicks`,
`GroupCohesionRadius`, `LoweredWallDistanceWu`, or `AimToleranceBam`. Task 9
chose 10, 96, 24, and 1024 as documented placeholders, each naming the later
task that revises it — 27, 28, 32, and 23 respectively. Those four tasks must
each either confirm the placeholder against a measurement or replace it, and
must move `SandataRuleset.ContentHash` when they do. The currently pinned hash
is `8955292433887190872`.

### Open item from wave 2: there is no `Point` type

Design section 6 writes geometry signatures in terms of a `Point`, but no such
type exists anywhere in the repository and no task creates one. Task 4 hit this
first and implemented `ClassifySegments` with the flat `long` coordinate
parameters `Orient` already uses, rather than introducing a shared type inside a
worktree that the other seven wave-2 agents could not see. That was the right
call in isolation, but it leaves a decision outstanding.

Tasks 6, 20, and 26 all consume geometry and will each face the same question.
Before wave 3 begins, one of two things has to be true: either Sandata declares
a `Point` value type in `src/Sandata.Core/Geometry/` and every geometry
signature takes it, or flat `long` coordinates are confirmed as the house style
and design section 6 is corrected to match. Flat coordinates are the cheaper
answer and the one already implemented, and they avoid a struct that would need
its own equality, hashing, and ordering rules to stay deterministic.

The cost of leaving this open is two geometry modules with different calling
conventions that a later task has to reconcile.

### Wave 2 note: `test.ps1` does not yet run the Sandata suites

`scripts/test.ps1:15-16` names the two Hukbo test projects explicitly, so a
wave-2 agent running `./scripts/test.ps1` sees 5,663 passing tests and none of
them are its own. Task 41 adds the `-Game` parameter that fixes this. Until it
lands, any task adding tests under `tests/Sandata.Core.Tests` or
`tests/Sandata.Client.Tests` must run `dotnet test` against that project
directly and report that number separately, and the integrating thread must
re-run both Sandata suites after every merge rather than trusting the aggregate.

### Task 1 deviations, recorded 2026-08-07

Task 1 was written expecting the tier-1 move to change zero lines outside the
moved files. Implementation proved that impossible for two specific reasons,
both of which are recorded here rather than quietly absorbed.

**One grant had to be restored to `Hukbo.Core`.** The attribute
`[assembly: InternalsVisibleTo("Hukbo.Headless")]` was declared at the top of
`Determinism/Fnv1a.cs`. An assembly-level attribute belongs to the assembly it
is compiled into, not to the file that declares it, so a perfect byte-for-byte
rename carried the grant out of `Hukbo.Core` along with the file.
`Hukbo.Core` then silently stopped granting `Hukbo.Headless` access to its
internals, and the build failed with five errors — `CS0122` on three metrics
accumulators and `CS1061` on two `BattleSimulation` members. The grant is now
declared in `src/Hukbo.Core/Properties/AssemblyInfo.cs` beside the other three,
with a comment explaining why it lives there. It adds no behaviour and no
hashed state.

This is the second instance in one task of the same underlying hazard, after
the `FacingRules` coupling recorded in design section 3. Both were invisible to
a `using`-directive scan, and both would have been caught by compiling the
candidate in isolation. **A pure rename is not automatically a safe rename.**
Any tier-2 extraction must check, for every candidate file, both its
same-namespace type dependencies and any assembly-level attribute it declares.

**Two entry-point stubs had to exist.** An `Exe` or `WinExe` project without a
`Main` fails the build with `CS5001`, so `src/Sandata.Client/Program.cs` and
`src/Sandata.Headless/Program.cs` were created containing a single `return 0;`
and a comment naming the task that replaces them. Task 14 still owns both files
and still writes the real argument parsing, `--help`, and exit-code contract.
Neither stub contains a `Console.` call, so the console-ban scan stays green
before task 7 adds them to `ConsoleOwners`.

## Wave plan

Twelve waves, none exceeding the eight-agent ceiling. Every task in a wave has
all its dependencies satisfied by a strictly earlier wave, and the file sets
within a wave are disjoint by construction.

| Wave | Tasks | Count | Theme | Notes |
| --- | --- | --- | --- | --- |
| 1 | 1 | 1 | Extraction and scaffolding | Single-writer wave. Task 1 is the only task in the plan that touches `Hukbo.slnx` or any existing Hukbo csproj. Nothing may start until it is green. |
| 2 | 2, 3, 4, 5, 6, 7, 8, 9 | 8 | Pure primitives | Mathematics, geometry, the tokenizer, the ruleset shell, and the build-gate widening. Every task here is a leaf with no dependency on another task in the wave. |
| 3 | 10, 11, 12, 13, 14, 15, 16, 17 | 8 | Foundations | Nav grid, cone, weapon enums, theme, entry points, map canonicalisation, collision, mission state. |
| 4 | 18, 19, 20, 21, 22, 23, 24, 25 | 8 | Bakes, search, weapon data | The heaviest wave. Tasks 21 and 25 are the two most likely to overrun; schedule them first within the wave. |
| 5 | 26, 27, 28, 29, 30, 31, 32, 33 | 8 | Behaviour | Funnel, path service, grouping, field of view, cover, fire modes, the lowered rule, and the client shell. |
| 6 | 34, 35, 36, 37, 38, 39, 40, 41 | 8 | Squads, sensing, presentation, tooling | Task 40 is the gated audio task. It produces a manifest and stops. |
| 7 | 42, 43, 44, 45, 46, 47, 48 | 7 | Movement, overlays, scripts, harness | |
| 8 | 49, 50 | 2 | Pipeline and benchmark matrix | Task 49 is the integration point for everything in waves 5 to 7 and is deliberately alone with only the benchmark definition beside it. |
| 9 | 51 | 1 | Headless runner | Single task by necessity: it is the only consumer of the pipeline and the only producer of a `RunReport`. |
| 10 | 52, 53 | 2 | Determinism proof and measurement | |
| 11 | 54 | 1 | Documentation | Single-writer on `docs/development/testing.md`, `README.md`, `AGENTS.md`, and `CLAUDE.md`. |
| 12 | 55 | 1 | Canonical gate | Not delegated. Run by the integrating thread, output pasted. |

Three scheduling rules bind the plan:

- **Never start a wave before every task in the previous wave is green.** A
  dependency listed in the table is a hard prerequisite, not a suggestion.
- **A task that discovers it needs a file outside its list stops and reports.**
  It does not edit the file and it does not negotiate with a sibling agent.
- **Waves 8 through 12 are deliberately narrow.** Integration, determinism
  proof, and the gate do not parallelise, and pretending they do produces a
  merge conflict at the worst possible moment.

## Risk register

| Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- |
| **The requirement assumption is wrong** — the user wanted a literal Door Kickers 2 in which player troopers do not auto-path | Medium | Very high. Sections 7, 8, and 11 of the design are re-cut and waves 5 to 7 are largely rewritten | Design section 2 flags it as the largest single assumption and question 1 in section 15. **Get an answer before wave 2 starts.** The cost of asking is one message; the cost of being wrong at wave 7 is roughly thirty tasks |
| **A Hukbo state hash moves during the tier-1 extraction** | Low | Very high. Every recorded Hukbo replay and both golden baselines are invalidated, and it may not be noticed until the gate | The extraction is pure file motion with the root namespace pinned, so not one `using` changes and no member's accessibility widens. Task 1's acceptance criterion is a `git diff --stat` showing zero changed lines outside the five moved files, and `git diff -M` confirming all five as pure renames. The gate's seed-1 workload must reproduce the recorded baseline hashes exactly |
| **The MonoGame instance pool exhausts under sustained automatic fire** | High | High. Gunfire silently drops out during exactly the moments the player cares about, and `MonoGameSoundPlayer` swallows `InstancePlayLimitException` so nothing reports it | Gunshot tails hold an instance three to five times longer than the 0.191-second melee mean the current budget was tuned against, and that budget has never seen automatic fire. Three mitigations: `TailTicks` declared per slot so a reservation outlives its frame; one loop plus one tail instance per shooter instead of one per round; and task 48 measures the real ceiling on named hardware before task 53 sets the constants. Constants stay marked provisional until measured |
| **`TreatWarningsAsErrors` blocks a wave on an analyser diagnostic nobody anticipated** | High | Medium. A task stalls, and the tempting fix is a suppression | `Directory.Build.props` applies nullable, `EnableNETAnalyzers`, `EnforceCodeStyleInBuild`, and NuGet audit to every project with no visible opt-out, so seven brand-new projects meet all of it from line one. Standing rule 1 forbids weakening a test, a warning, or an analyzer to get green. A task blocked on a diagnostic reports it rather than suppressing it, and the suppression — if genuinely warranted — becomes its own reviewed task with a written justification in the code |
| **The tracked audio library adds 27 to 46 MB that every clone pays for** | High, if question 5 is answered yes | High. A roughly 25-fold increase over the 1,289,596 bytes of audio tracked today, permanently, with no Git LFS configuration in this repository | Nothing is generated until the user reviews the manifest and authorises the spend, and task 40 has no network code in it at all. Before authorising, three questions get answers: whether 24 kHz stereo is needed when these are short mono-ish impulses, whether Git LFS is introduced first, and whether the library ships in the repository or beside it. The decision is reversible only before the first commit of a WAV |
| **A hand-transcribed table is wrong** — the 257-entry sine table or the sixteen CORDIC constants | Medium | High. A silently wrong angle poisons every cone, every aim, and every hash, and a golden vector generated from the implementation would confirm the error | Golden vectors are pinned from the published mathematical definition and never read back from the implementation. A separate consistency test asserts first-quadrant monotonicity across all 257 entries and the exact endpoints 0 and 65,536, so a transcription error cannot hide behind a self-confirming pin. Task 5's acceptance criterion names both tests |
| **The A\* open set is ordered by something less than a total key** | Medium | Very high. A desync that appears only on some machines, some runs, or after an unrelated capacity change, and is among the hardest classes of bug to find | The comparator carries the determinism, not the container: `(f, h, nodeIndex)` is total because `nodeIndex` is unique, so any correct heap gives one answer. Task 21 includes a property test over generated triples asserting antisymmetry, transitivity, and totality, and a test that the expanded-node sequence is identical across a permuted insertion order. Task 7's banned-token scan blocks `PriorityQueue<`, `Dictionary<`, and `HashSet<` from `Sandata.Core` outright |
| **Two agents in one wave edit the same file** | Medium | Medium. A merge conflict created on purpose, and the resolution silently drops one agent's work | Every task lists every file it may touch, the wave plan asserts disjointness, `Hukbo.slnx` is single-writer and only task 1 touches it, `docs/development/testing.md` is single-writer and only task 54 touches it, and a task that needs a file outside its list stops and reports rather than editing it |
| **Navigation misses its tick budget at the target operator count** | Medium | Medium. The tempting fix is a per-tick search budget, which is precisely what design section 4 forbids because it makes arrival depend on scheduling | Amortisation is by fixed latency from the first line, not retrofitted: a path requested at tick `t` is valid at `t + PathLatencyTicks` regardless of how many searches completed. There is no "no path yet, move at the goal" fallback to tempt anyone. Task 50 defines the benchmark matrix `SIMULATION-GAME-STANDARDS.md` section 11 requires and task 53 measures it, so a budget conversation starts from a number rather than a feeling |
| **ElevenLabs take quality overruns the credit budget** | Medium | Medium. 22 USD becomes 99 USD, or the run stalls half-finished with a partially populated folder | The project's own skill documentation records one run peaking at 93 percent usable and another under 1 percent. The manifest carries the credit and dollar estimate at both a zero-reject and a 50-percent-reject rate so the authorisation is informed. Whether credits scale with requested duration is **UNVERIFIED** and is stated as unverified rather than assumed favourable |
| **The product name changes after the first commit** | Medium | Medium. Seven project names, every namespace, every file path in this plan, and every `sandata.*` log event identifier | Design section 15 question 3 raises it before wave 1. The name is trivial to change before the first commit and expensive after, and the single cheapest moment to answer is now. The same applies to question 8, the assembly name of the shared spine |
| **A manual smoke row is marked `PASS` without a human at a desktop** | Medium | Medium. The checklist stops meaning anything, and an interactive regression ships | Standing rule 7 forbids any task from flipping a row, task 54's acceptance criterion is that every new row is `PENDING`, and design section 13 lists exactly which behaviours are human-only. Compilation, unit tests, and a window-opening probe do not qualify. `BLOCKED` is reported honestly rather than upgraded |

## Status log

**Wave 1 complete, 2026-08-07.** Tier-1 extraction of four files into
`Hukbo.Shared.Core` plus seven new project shells. Two hazards recorded above.

**Wave 2 complete, 2026-08-07.** Tasks 2 through 9 all merged into
`sandata-scaffold`, each from its own worktree, with no merge conflicts — the
disjoint file-set audit held in practice. 175 Sandata tests and 5,733 Hukbo
tests pass. The canonical gate passes all five stages, and the seed-1 workload
reproduces `stateHash 1B73FC5923879AA0` and `eventHash AC55684F24D39344`,
byte-identical to the same workload run on untouched `main`. Adding a second
game moved no Hukbo hash.

**Stale line citations, 2026-08-07.** `main` gained warrior gait animation
during this session and it has been merged in. It is `Hukbo.Client` only and
touches nothing Sandata depends on, but it rewrote large parts of
`PawnGeometry.cs` and `PawnRenderer.cs`. Design section 11's analysis of the
pawn draw path cites line numbers in both files that have now shifted by
several hundred lines. **Task 37 must re-derive those citations against the
current files rather than trusting the design's numbers.** The structural
claims — fifteen composed layers, arbitrary rotation about a pivot already
available, the weapon tip already serving as a muzzle anchor — were verified
independently and still hold; only the line numbers are stale.

**A note on the const-inlining discovery, 2026-08-07.** While proving its new
assembly-reference assertions could fail, task 7 found that Roslyn inlines
`const` fields and emits no `AssemblyRef` for them. A reference test therefore
passes falsely when the only usage is a constant. This applies to the
pre-existing `CoreDoesNotReferenceTheDiagnosticsAssembly` fact as much as to the
new Sandata ones: it proves no *non-constant* use of the assembly. The paired
source-text scan is what closes that gap, which is why both forms exist.

### Plan defect found by task 10: an unowned file

Task 18's "What" column says it rasterises into `NavGrid.passability`, but
`NavGrid.cs` was absent from its "Files" column, so the task could not create
the member it was told to populate. Task 10 hit this from the other side: it
was not asked for a passability array and correctly declined to invent one,
since guessing the representation — a `byte[]`, an enum-typed array, a bitset —
would have forced task 18 to either accept the guess or rewrite it.

`NavGrid.cs` is now listed in task 18's files. Nothing else in wave 4 claims it,
so the wave stays disjoint.

The general lesson is about the audit, not this row. Checking that no two tasks
in a wave write the same file catches collisions but says nothing about files
that *no* task owns. A plan can be perfectly disjoint and still be unbuildable.
Any future wave audit should check both directions: no file claimed twice, and
every file named in a "What" column claimed once.

### Task 56 scope grew: a second duplicated table, and a signature to reconcile

Task 11 was told its acceptance test must assert the file "contains no `Trig`
call". The intent behind that wording was to forbid a **cosine comparison** for
cone containment, which is the real hazard — Hukbo's sector vectors are not
unit length, the error differs per sector, and such a test also overflows
`long`. The wording did not say that. Read literally and correctly, it forced
`ConeBoundaryTable` to declare its own independent 257-entry quarter-wave sine
table rather than calling the one task 5 had already written and pinned.

The containment logic is right — two half-plane cross products, magnitude never
used. Only the table is redundant. Task 56 therefore also:

- folds `ConeBoundaryTable`'s sine table onto `Trig`'s single pinned table, and
- rewrites the offending assertion so it forbids what was actually meant: no
  cosine-comparison containment test, no use of a vector's magnitude in a
  containment decision. An assertion banning a *call* bans the wrong thing.

Both tables were independently derived from the mathematical definition rather
than from each other, so folding them is a deletion, not a reconciliation of two
disagreeing sources. Confirm they are element-for-element equal before removing
either; if they differ, that difference is a real bug in one of them and must be
resolved before the fold.

Separately, task 11 added a `long rangeSquared` parameter to
`VisionCone.Contains`, which design section 6's signature omits even though a
cone needs a range. Task 56's design-correction pass covers this alongside the
`Point` and `Box` removal.

**The lesson for future prompts.** An acceptance criterion should name the
property that must hold, not the symbol that must be absent. "No cosine
comparison" is checkable and correct; "no `Trig` call" is checkable and wrong,
and it produced real duplicated state that a later task now has to unwind.

### Two map-format decisions task 25 must make explicit

Task 15 implemented canonicalisation and the content hash as specified and
surfaced two places where the specification is silent rather than wrong. Both
are decisions, and both are currently settled by accident.

**A map name does not reach the content hash.** Design section 12 says the hash
folds "each integer field", and a `NAME` record's identifier is a string, so it
contributes only its kind byte. Two maps differing solely by name therefore hash
identically. That is very probably the right behaviour — a name changes no
simulation outcome, and hashing it would force new golden expectations for a
pure rename. But the hash exists to stop a recorded replay being silently reused
against a different map, so "which differences are allowed to be invisible" has
to be stated deliberately. Task 25 either writes that rule down and keeps the
current behaviour, or folds the name in and accepts that renaming a map
invalidates its replays.

**A canonicalised door does not move its hinge.** Endpoint normalisation sorts a
door's endpoints into lexicographic order, but hinge side and open state are
left as authored. If hinge side is expressed relative to the endpoint order,
then swapping the endpoints without swapping the hinge silently mirrors the
door. If it is absolute, nothing is wrong. Design section 12 does not say which,
and the answer changes whether a door opens into the room or out of it. Task 25
must state it and add a fixture that would fail under the wrong reading.

Task 15 also added a narrow duplicate check inside the canonicaliser, needed
there so the comparator is total and immune to introsort's instability. That is
not a substitute for task 25's full cross-record duplicate rule and is
documented in the code as such.

### Three more ownership gaps, and one real logging bug

Wave 3 found the unowned-file pattern twice more, both the same way: an agent
respected its file list rather than reaching outside it, and reported the gap.

**`Sandata.Client.csproj` had no owner after task 1 created it.** Task 13 wrote
`Content/Themes/sandata-theme-standards.json` but could not add a
`CopyToOutputDirectory` rule, so the file never reaches the build output. Its
tests pass because they locate the JSON through the repository root, which works
in a test run and would not work in a packaged build. Task 33 now owns the
csproj.

**`Sandata.Client/Program.cs` was owned by task 14 permanently**, but task 33 is
the task that has to construct and run `SandataGame`, and its file list did not
name the file. Ownership now transfers to task 33 at that point.

**`LogPaths.ApplyRetention` only sweeps `hukbo-*.jsonl`.** The prefix is a
`const` at `src/Hukbo.Diagnostics/LogPaths.cs:23`, so Sandata's log files are
written but never retention-swept and will accumulate without bound. No task
owns `LogPaths.cs`. This is a real defect rather than a naming preference, and
it needs either a parameterised prefix or a retention sweep that covers both
prefixes. Assign it before Sandata runs regularly in Debug.

**A constraint task 14 discovered.** Design section 4 asked for `ev` identifiers
under a leading `sandata.` prefix, but the existing
`LogEventCatalogTests.EveryIdentifierPrefixNamesADeclaredChannel` requires the
first dotted segment to be a declared `LogChannel` wire name. The two rules
cannot both hold. Task 14 kept the existing test — it guards a real invariant —
and used `boot.sandata.started` rather than `sandata.boot.started`. Design
section 4's wording should be corrected to match, since channel-first ordering
is the established contract and the design was simply unaware of it.

**Wave 4 complete, 2026-08-07.** Tasks 18 through 25 plus task 56 all merged, no
merge conflicts across nine worktrees. 608 Sandata.Core tests, 25
Sandata.Client tests, 2,635 Hukbo.Core and 3,098 Hukbo.Client tests — 6,366
total, zero failures. The canonical gate passes all five stages and the seed-1
workload still reproduces stateHash 1B73FC5923879AA0 and eventHash
AC55684F24D39344, unchanged from untouched main.

Decisions settled and pinned by tests during this wave:

- A map's NAME record does **not** reach MapContentHash. Two maps differing only
  by name hash identically, so a rename does not invalidate recorded replays.
- A door's hinge is **absolute** to canonical endpoint order, not relative to
  authored order. MapCanonicalizer leaving the hinge untouched on an endpoint
  swap is therefore correct and needed no change.
- FirearmRuleset.ContentHash is 12611003062847309889, and it was observed to
  move when a single Modes field changed, which is what makes it meaningful.
- angle-house.hkmap has MapContentHash 11909359227906322716.

**The AK-15 correction, and what it says about the pipeline.** Task 22 reported a
rifle count it could not reconcile against the research rather than quietly
choosing a number. Chasing it found that the research consolidation had dropped
the AK-15 two-round burst entirely while condensing the source material. The
design inherited the omission and the catalog implemented the design faithfully.
Every step downstream was correct; the input was wrong.

The consolidation step is the only link in this chain with no test behind it, and
it fed thirty-eight rows of weapon data. Correcting it touched four files and
moved a pinned hash. The general point for future work: a research consolidation
deserves the same suspicion as generated code, and the cheapest guard is an
implementer that reports an irreconcilable count instead of picking one.

**Two real defects found during verification, both by the agent that wrote the
code.** Task 25 MarkBlockedCells clamped only the upper cell-index bound, so a
wall lying exactly on the grid far boundary produced an empty cell range and
silently never rasterised — a wall present in the file, drawn on screen, and
blocking nothing, with enclosure validation passing over the hole. Task 21 found
that one of its own ten fixtures, labelled start-equals-goal, actually parsed
into two adjacent distinct cells and had never tested what it claimed.

**Wave 5 is not started.** It is the first wave whose work depends on the
unanswered question in section 15: whether bots path and group themselves, or the
player draws every path by hand, or both. Everything through wave 4 holds under
any of the three answers.

## Answer C, and the wave re-cut it forces — 2026-08-07

The open question this plan and the design both flagged as the largest single
assumption was put to the user before wave 5 started, as three options:

- **(A)** autonomous bots that path and group themselves,
- **(B)** a literal Door Kickers 2 in which the player hand-draws every path,
- **(C)** both.

**The answer was (C).** Design section 2 records the decision and design
section 16, added the same day, is the full specification of the order layer it
promotes. The design document remains authoritative over this plan; what follows
is only the task-level consequence.

### What did not change, and why wave 5 started anyway

(C) is additive rather than corrective. Every autonomous mechanism in the plan
stands exactly as written, so tasks 26 through 33 were dispatched unchanged and
none of waves 1 through 4 is invalidated. This is the cheapest of the three
outcomes: (B) would have re-cut roughly thirty tasks and (C) adds seven.

Task 27 was given one forward-looking instruction rather than a change: a later
order layer supplies authored polylines from outside the path service, so
`PathService` must not be written as though it were the only source of a polyline
in the game. It still owns autonomous group paths alone.

### Seven new tasks

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 57 | 7 | Order records and the queue | Declare `Order` with `OrderId`, `OrderSequence`, `TargetTick`, `FactionId`, ascending `Addressees`, `Kind`, and payload; the six v0.1 kinds from design section 16; and the queue applied in `(TargetTick, OrderSequence)` order. The authored-polyline node cap is a `const` on the order type, never a `SandataRuleset` field — design section 16 records why. | `src/Sandata.Core/Orders/Order.cs`, `src/Sandata.Core/Orders/OrderKind.cs`, `src/Sandata.Core/Orders/OrderQueue.cs`, `tests/Sandata.Core.Tests/OrderQueueTests.cs` | `OrderQueueTests` passes: the applied order equals `(TargetTick, OrderSequence)` for a deliberately shuffled submission order; two orders sharing a `TargetTick` resolve on `OrderSequence` alone; every `OrderKind` numeric value is pinned; and an addressee list submitted out of order is stored ascending. | 17 | |
| 58 | 8 | Authored polyline validation and rejection | Validate a `MoveAlongPath` order at submission against design section 16's four rejection rules — out of bounds, a node in a `Blocked` cell, a segment crossing a wall by `ExactPredicates.ClassifySegments`, and the node-count bounds. A door cell is deliberately not a rejection. Every rejection carries a reason code and an authoritative event; nothing is silently dropped. | `src/Sandata.Core/Orders/AuthoredPath.cs`, `src/Sandata.Core/Orders/OrderValidation.cs`, `src/Sandata.Core/Orders/OrderRejectReason.cs`, `tests/Sandata.Core.Tests/OrderValidationTests.cs` | `OrderValidationTests` passes with one failing fixture per rejection rule, a fixture proving a path through a door cell is accepted, and a fixture proving a segment that grazes a wall endpoint is classified by the exact predicate rather than an epsilon. | 20, 57 | |
| 59 | 8 | Order assignment and the movement-source rule | Implement design section 16's per-tick rule: an operator with an `OrderAssignment` follows its authored polyline and is excluded from slot targeting; an operator without one follows its squad slot target. Implement all four clearing conditions, each with an inspectable reason code. A cleared assignment never repairs, re-routes, or re-smooths the polyline. | `src/Sandata.Core/Orders/OrderAssignment.cs`, `src/Sandata.Core/Orders/MovementSource.cs`, `tests/Sandata.Core.Tests/MovementSourceTests.cs` | `MovementSourceTests` passes: exactly one source is selected per operator per tick with no third case; each of the four clearing conditions is exercised and names its reason code; autonomy resumes on the same tick the assignment clears; a closed door across a polyline clears rather than re-routes; and an ordered operator's group still derives the same group id it would have had. | 34, 57 | |
| 60 | 9 | Sync sets and go-codes | Sync pace-matching evaluated in stage 8 against the frozen tick-start view, releasing every living member on one tick, keyed by the lowest entity id. `GoCodeRelease` as an ordinary order carrying its own `TargetTick`, so a keypress enters the same queue. | `src/Sandata.Core/Orders/SyncRules.cs`, `src/Sandata.Core/Orders/GoCodeRules.cs`, `tests/Sandata.Core.Tests/SyncRulesTests.cs`, `tests/Sandata.Core.Tests/GoCodeRulesTests.cs` | Both pass: a sync set releases on exactly the tick its last living member arrives; a dead member does not deadlock the set; permuting the evaluation order of the members changes nothing; two sets releasing on the same tick resolve in a total order; and a go-code release is indistinguishable from any other order at the queue boundary. | 57, 59 | |
| 61 | 9 | Order state on mission state, snapshot, and hasher | Add the order queue and the per-operator assignment to `MissionState`, to the snapshot, and to `SandataStateHasher`, folded **after** every field the hasher already covers so the existing field order is undisturbed. An authored polyline is stored verbatim and hashed; it is never recomputed on resume. **Single-writer wave task** — no other task in wave 9 touches these files. | `src/Sandata.Core/Simulation/MissionState.cs`, `src/Sandata.Core/Simulation/MissionSnapshot.cs`, `src/Sandata.Core/Determinism/SandataStateHasher.cs`, `tests/Sandata.Core.Tests/MissionStateTests.cs`, `tests/Sandata.Core.Tests/OrderStateHashTests.cs` | `OrderStateHashTests` passes: the state hash moves when any order field changes, the snapshot round-trips an authored polyline byte for byte, a resumed authored polyline is identical rather than recomputed, and the existing `MissionStateTests` facts still pass unchanged. | 57, 58, 59 | |
| 62 | 9 | Path drawing tool and order authoring UI | The hand-drawn path tool, the go-code panel, and the order queue view, built on task 46's multi-select, drag capture, and undo stack. Pure layout and state helpers, tested with no graphics device. The undo stack edits orders **before** submission and never reaches `Sandata.Core`. | `src/Sandata.Client/UI/PathDrawTool.cs`, `src/Sandata.Client/UI/GoCodePanel.cs`, `src/Sandata.Client/UI/OrderQueueView.cs`, `tests/Sandata.Client.Tests/PathDrawToolTests.cs` | `PathDrawToolTests` passes: a drawn node list submits as one `MoveAlongPath` order with ascending addressees; undo removes the last unsubmitted node and cannot remove a submitted one; a rejected order surfaces its reason code in the queue view; and a test asserts no `Sandata.Core` type is reachable from the undo stack. | 33, 46 | |
| 63 | 9 | Ruleset constant revision, single writer | The wave-2 note left `PathLatencyTicks`, `GroupCohesionRadius`, `LoweredWallDistanceWu`, and `AimToleranceBam` as documented placeholders, each naming a later task that revises it. Those four tasks ran in parallel worktrees and were **forbidden** `SandataRuleset.cs` to stop a three-way collision on one file; each reported a verdict instead. This task applies all four verdicts at once and moves the content hash once. | `src/Sandata.Core/Rules/SandataRuleset.cs`, `tests/Sandata.Core.Tests/SandataRulesetTests.cs` | `SandataRulesetTests` passes with the revised values, the pinned `ContentHash` is updated from `8955292433887190872` to the newly measured value, and every downstream test that consumed a placeholder still passes. Each of the four values cites the task that justified it. | 23, 27, 28, 32 | |

### Five existing rows amended

- **Task 44 (intent selection) moves from wave 7 to wave 9** and gains task 59 as
  a dependency. Intent selection consults the order assignment first: an operator
  under orders never selects an autonomous intent. Its file list is unchanged.
- **Task 46 (interaction layer) is no longer speculative.** Its file list and
  acceptance criteria stand exactly as written, but the types it declares now
  have a real producer in task 62 rather than being kept warm for a later
  milestone. The original row's phrase "nothing in v0.1 requires them to be
  driven" no longer holds.
- **Task 49 (tick pipeline) gains tasks 57, 59, 60, and 61 as dependencies.**
  Stage 1 is no longer empty: it applies the order queue in
  `(TargetTick, OrderSequence)` order. The stage's position and ordering rule do
  not change.
- **Task 52 (determinism suite) needs two golden baselines**, not one: an empty
  order stream, which is the pure autonomous case, and a recorded non-empty one.
  Its save-resume test must cover an authored polyline, because a mission with
  only autonomous paths never exercises design section 16's authored-path rule.
- **Task 54 (documentation) covers the order layer** — the two path sources, the
  order stream as part of the replay contract, and the new smoke rows for
  drawing, submitting, cancelling, and go-code release, all `PENDING`.

### Revised wave plan

| Wave | Tasks | Count | Change |
| --- | --- | --- | --- |
| 1–4 | 1–25, 56 | — | Complete. Unaffected by the answer. |
| 5 | 26–33 | 8 | Dispatched unchanged. |
| 6 | 34–41 | 8 | Unchanged. |
| 7 | 42, 43, 45, 46, 47, 48, 57 | 7 | Task 44 moved out to wave 9; task 57 added. |
| 8 | 58, 59 | 2 | New. |
| 9 | 44, 60, 61, 62, 63 | 5 | New, plus task 44 relocated. Task 61 is single-writer on the mission-state files and task 63 is single-writer on the ruleset. |
| 10 | 49, 50 | 2 | Was wave 8. |
| 11 | 51 | 1 | Was wave 9. |
| 12 | 52, 53 | 2 | Was wave 10. |
| 13 | 54 | 1 | Was wave 11. |
| 14 | 55 | 1 | Was wave 12. |

Both directions of the wave audit were run on the new rows: no file is claimed by
two tasks in one wave, and every file named in a new "What" column is claimed by
exactly one task. `MissionState.cs`, `MissionSnapshot.cs`,
`SandataStateHasher.cs`, and `SandataRuleset.cs` are the four files this re-cut
puts back into play, and each is given to exactly one single-writer task.

### The ruleset placeholders, and why no wave-5 task touched them

The wave-2 note tasked 23, 27, 28, and 32 with confirming or replacing one
placeholder each and moving `SandataRuleset.ContentHash` when they did. Tasks 27,
28, and 32 ran in parallel in wave 5, and all three would have had to edit
`SandataRuleset.cs` and its test to do that — three agents, one file, in one
wave, which is the merge conflict the plan's own rules exist to prevent.

Each was therefore forbidden the file and asked to report a verdict on its
placeholder instead. Task 63 applies all four verdicts in one edit and moves the
hash once. The cost is one extra task; the alternative was a guaranteed conflict
on a file that carries a pinned hash.

### Two type defects task 28 reported, and the task that fixes them

Task 28 was told to assert by reflection that no group state is stored on
`MissionState`. It could not honestly assert that, and rather than widening its
own scope into a forbidden file it narrowed its test to the risk it could
control — a *new* leader field or membership collection — and reported the rest.
That is the behaviour the file-list rule exists to produce.

**The squad slot index is stored but should be derived.** Design section 4's
authoritative list named it; design section 8 says group id, leader, membership,
and slot index are all derived each tick and nothing is stored per group. The two
could not both hold. Section 8 wins, section 4 is corrected in place with the
reasoning recorded there, and `OperatorState.SquadSlotIndex` has to go. Being
derived does not disturb the `(groupId, slotIndex, entityId)` commit key: a value
computed identically on every run orders exactly as totally as a stored one.

**Entity ids are two different types in one game.** `SandataCollisionBody`,
`SandataCollisionPair`, and `SandataCollisionMoveRequest` use `ulong` for entity
and group ids. `MissionState.OperatorState.EntityId` is `int`. Hukbo's own
`AgentState.EntityId` is `ulong`, so `ulong` is the house style and the mission
record is the outlier. Left alone, the widening cast lands in whichever task
first wires the pipeline together, which is exactly where a silent type error is
most expensive.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 64 | 5b | Mission state type reconciliation and the world-unit conversion | Widen every entity and group identifier on the mission record from `int` to `ulong`, matching `Hukbo.Core.Simulation.AgentState` and Sandata's own collision types. Remove `SquadSlotIndex` from `OperatorState` and from the state hasher, per design section 4's 2026-08-07 correction. Add the missing `FixedPoint`-to-world-unit conversion as a named helper with a written rounding rule — design section 4 stores distance as `FixedPoint` raw at scale 1024 while `NavGrid`, `WallBuckets`, and every rule built on them take plain `long` world units, and no conversion exists today. Change no other field and no field order beyond the removal. **Single-writer task, run between waves 5 and 6** — every wave-6 task consumes these types, so the fix has to precede them. | `src/Sandata.Core/Simulation/MissionState.cs`, `src/Sandata.Core/Simulation/MissionSnapshot.cs`, `src/Sandata.Core/Determinism/SandataStateHasher.cs`, `src/Sandata.Core/Mathematics/WorldUnits.cs`, `tests/Sandata.Core.Tests/MissionStateTests.cs`, `tests/Sandata.Core.Tests/WorldUnitsTests.cs` | `MissionStateTests` passes with the widened identifiers; a reflection test asserts no member named `SquadSlotIndex` survives on `OperatorState`; the snapshot still round-trips to an equal `MissionState`; `WorldUnitsTests` pins the conversion at zero, at an exact world unit, at a fractional value, and on both sides of zero, with the rounding rule stated in the XML doc rather than left to C# division's truncation-toward-zero; and the task reports explicitly whether any pinned constant moved. No golden mission hash exists yet, so this should cost no re-pinning — if it does, that is a finding to report, not to absorb. | 17, 28, 32 | |

The conversion is a determinism decision, not a convenience. C# integer division
truncates toward zero and an arithmetic right shift floors, so the two disagree
for negative coordinates. Map space cannot express a negative coordinate, but
relative offsets are signed, which is exactly why design section 4 already
requires explicit floor division for every world-to-cell conversion. The helper
states which rule it implements and a test pins it on both sides of zero.

Wave 5b is a real wave, not a footnote: it holds exactly one task, it is
single-writer on four files, and wave 6 does not start before it is green.

### The unowned step task 27 exposed: nothing smooths the published path

Task 27 published a path as the A\* corridor's ordered cell-index sequence, and
said so rather than assuming. That is the correct output for the file list it
was given — task 26's funnel string-pull was running in a sibling worktree it
could not see, and design section 7 is explicit that the corridor and the
smoothed polyline are two different things: "Grid A\* decides topology; a funnel
string-pull snaps the corridor to the real vector wall geometry."

No task owns the step between them. Task 26 writes `Funnel.StringPull` and
tests it on synthetic corridors. Task 27 writes the service that publishes a
path. Task 34 consumes "the shared polyline" and computes cumulative arclength
along it. Nothing in the plan says who calls the funnel on a published corridor,
so as written the squad would follow a staircase and the funnel would be dead
code — which is precisely the visible defect design section 7 introduced the
funnel to prevent.

This is the third instance of the same audit gap, after `NavGrid.cs` and
`Sandata.Client.csproj`: the wave audit checks that no file is claimed twice and
does not check that every *step* is claimed once. A file-level audit cannot catch
it, because the missing owner here is a call, not a file.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 65 | 5b | Smooth the published path | Make `PathService` publish the funnel-smoothed polyline rather than the raw corridor, calling `Funnel.StringPull` on the corridor `NavSearch` produced. The request record stays authoritative and unchanged; the smoothed polyline stays derived and recomputed on resume, so the recompute test must now reproduce the smoothed output rather than the corridor. Keep the corridor available to callers that genuinely want cells, and say in the XML doc which one is which. | `src/Sandata.Core/Navigation/PathService.cs`, `tests/Sandata.Core.Tests/PathServiceTests.cs` | `PathServiceTests` passes with the published path asserted as a smoothed polyline: a corridor crossing the `angle-house` fixture's 26.57-degree wall publishes a single straight segment rather than a staircase, and recomputing every published path from its stored request reproduces the identical smoothed polyline. Task 26's own `FunnelTests` stay untouched and green. | 26, 27 | |

Task 65 joins task 64 in wave 5b. Their file sets are disjoint — 64 owns the
mission record and the hasher, 65 owns the path service — and both must be green
before wave 6 starts, because task 34's arclength work consumes exactly the
polyline task 65 changes and exactly the identifiers task 64 widens.

### A fourth unowned declaration: the RNG system tags

Design section 4 names four random streams — `Accuracy`, `Reaction`, `Sidestep`,
and `SpawnJitter` — and says a stream derives from
`(missionSeed, systemTag, entityId or eventId)` so that adding a draw in one
system cannot shift an unrelated outcome. No task declares that enum. Task 17
noticed the gap and documented it in `MissionState.cs`; task 31 hit it from the
other side and needed a real value to fold, so it declared a private
`AccuracyStreamTag` constant, documented it as a stand-in, and asked whichever
task declares the enum to confirm the value.

That is the right behaviour and it leaves a determinism-relevant identity
sitting in a private field of one combat file. A stream tag is not an
implementation detail: it is part of what makes a draw reproducible, and three
more systems need theirs before the pipeline is wired.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 66 | 5b | Declare the RNG system tags | Declare the four v0.1 system tags from design section 4 as one append-only enum with explicit numeric values, beside the rest of Sandata's determinism code. Adopt it in `AccuracyRules`, replacing the private stand-in constant, and state in the XML doc that adding a tag is append-only because changing an existing value re-keys every draw that system makes. | `src/Sandata.Core/Determinism/SandataSystemTag.cs`, `src/Sandata.Core/Combat/AccuracyRules.cs`, `tests/Sandata.Core.Tests/SandataSystemTagTests.cs`, `tests/Sandata.Core.Tests/AccuracyRulesTests.cs` | `SandataSystemTagTests` pins every member's numeric value as a literal and asserts the four members are exactly the ones design section 4 names. `AccuracyRulesTests` still passes; if adopting the enum changes the accuracy draw for a given seed and entity id, the task says so explicitly rather than silently re-pinning — the existing expectations were pinned against the stand-in constant, and a moved draw is a real finding about which value the tag stream should carry. | 31 | |

Wave 5b now holds tasks 64, 65, and 66. Their file sets are disjoint: 64 owns
the mission record, the hasher, and the new world-unit helper; 65 owns the path
service; 66 owns the system-tag enum and the accuracy rules. All three must be
green before wave 6 starts.

### What task 26 proved about the funnel, and what that means for task 65

Task 26 is merged and green, and it returned a geometric result that changes how
a later task has to be tested.

**The plan and the design disagreed on the fixture angle.** The task 26 row above
says the shallow-angle corridor is 26.57 degrees; design section 6's signature
row says 18.4 degrees, which is `atan(1/3)` and matches the slope the existing
`GridRayTests` fixture already uses. The design wins under the stated read order,
the implementation used 18.4 degrees, and the row above is wrong on this point.

**A one-cell-wide corridor cannot generally collapse to a straight segment, and
that is a property of the geometry rather than a defect in the port.** Every turn
in a single-file grid corridor puts a real concave notch in the passable region,
and a taut string has to route around it. It collapses only when the notch
happens to lie exactly on the straight line from start to end. Task 26's shallow
fixture works because its two south-transition portal corners, `(8,4)` and
`(20,8)`, were chosen to sit exactly on `y = 2 + (x - 2) / 3`. The agent
hand-traced all nine portals before writing code and said so, rather than
tuning the fixture until it went green.

So the acceptance criterion as written — "a corridor along the shallow wall emits
a single straight segment" — was satisfiable only by choosing a corridor that
makes it true. That does not invalidate the port. It relocates where the design's
actual promise lives: the funnel delivers "the path follows the angled wall
rather than a staircase" **when the corridor is wider than one cell**, which is
the ordinary case in a room, and delivers exactly the corridor's own shape when
it is not, which is correct behaviour for a single-file passage.

**Task 65 must be tested against a corridor wider than one cell.** Task 65 makes
`PathService` publish the funnel-smoothed polyline. If its acceptance fixture is
a single-file corridor, it will pin a staircase, the test will pass, and the
visible defect the funnel exists to prevent will ship anyway. Its criterion is
therefore restated: the published path across an open region beside the
`angle-house` fixture's angled wall is materially straighter than the corridor it
came from, asserted as a vertex count and as the absence of an axis-aligned
step, not as a coincidence of one hand-picked line.

The manual smoke row for this behaviour is affected the same way: the human check
is a squad crossing an open room diagonally, not a squad in a corridor.

**One decision task 26 settled that the design did not state.** Deriving a
portal's left and right endpoints from the two cells' local travel direction
labels the same physical corner inconsistently across a turn, which a hand-trace
proved. The port instead uses a fixed, translation-invariant per-departing-cell
corner winding — top-left, top-right, bottom-right, bottom-left — which is how a
real navmesh's precomputed winding behaves. The reasoning and the discarded
alternative are recorded in `Funnel.cs`. It also ports DotRecast's exact-equality
deduplication against the last emitted point, without which the last commit
emits a spurious trailing duplicate.

### Wave 5 complete, 2026-08-07

All eight tasks — 26, 27, 28, 29, 30, 31, 32, and 33 — merged into
`sandata-scaffold`, each from its own worktree, with no merge conflicts. The
disjoint file-set audit held again across eight parallel agents.

Counts after the final merge, run directly against the Sandata projects because
`test.ps1` still covers only the Hukbo suites until task 41:

- `Sandata.Core.Tests`: `Failed: 0, Passed: 725, Skipped: 0, Total: 725`
- `Sandata.Client.Tests`: `Failed: 0, Passed: 41, Skipped: 0, Total: 41`

The canonical gate was run by the integrating thread after integration, not
delegated:

```
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
```

The seed-1 workload still reproduces the baseline recorded against untouched
`main`. Five waves of a second game have now moved no Hukbo hash.

Two tasks were sent back once each before merging, both for a real defect rather
than a style preference:

- **Task 30** made crouched cover protection unconditional on direction. Design
  section 9 says "fire from the flank or rear ignores cover entirely" before it
  introduces crouching at all, and crouching behind cover is a cover benefit, so
  the arc gate binds both postures. The arc test now precedes the posture branch,
  and a test pins that rear fire against a crouched operator gets no reduction —
  the case that passed under the original reading.
- **Task 33** shipped an objective draw path with no pinned geometry test. Its
  "What" column named objectives; only its "Done when" list forgot them. The
  test now pins objective geometry at the same three zoom levels as walls, doors,
  and cover.

Task 33 also closed the wave-3 defect where task 13's theme JSON never reached
the build output. `Content/Themes/sandata-theme-standards.json` is now in
`bin/Release/net10.0/win-x64/Content/Themes/`, so the packaged build no longer
depends on tests happening to find the file through the repository root.

**One duplication accepted deliberately.** `SandataCamera` could not reuse
`Hukbo.Client.SpectatorCamera`: that type and the `InputEdges` it depends on are
both `internal` to `Hukbo.Client`, and reaching them meant editing a forbidden
tree. The pan, zoom-clamp, fit, and world-to-screen formulas were ported
unchanged and `Update` was rewritten against raw keyboard and scroll state, with
the reasoning recorded in the type. Extracting a shared camera later is a
candidate for a tier-2 move, and the tier-1 lesson applies to it in full: compile
the candidate in isolation before calling it a rename.

**Nothing in wave 5 was rewritten by the answer to question 1.** The user chose
both autonomous bots and hand-drawn player paths, and every wave-5 task stood
exactly as written. The order layer's own tasks are 57 through 63.

### The funnel does not deliver the straight line, and task 67 does

Task 65 is merged and its tests are honest: the published path is measurably
straighter than its corridor, and the fixture is open ground rather than the
single-file corridor the earlier amendment warned against. It still does not
produce the straight line the design promised, and the numbers say so plainly.

Across a fully open ten-by-four cell region with no walls, from cell `(0,0)` to
cell `(9,3)`, the taut path is the single segment `(2,2)` to `(38,14)` in world
units. The published path is `(2,2)`, `(4,4)`, `(8,8)`, `(12,12)`, `(38,14)` —
five points, off the straight line by about 6.7 world units at its worst, which
is roughly one and three quarter cells. The first four points are collinear, so
the shape is really the corridor's diagonal run followed by one long segment: a
better path than the staircase, and not the taut one.

The cause is structural and design section 7 now carries the amendment that
records it. A navmesh portal is as wide as the polygons sharing it; a grid A\*
corridor is a chain of single cells, so every portal is one cell edge wide. The
funnel removes the steps it has room to remove and cannot straighten what the
corridor never gave it room to straighten.

This is the second time in this wave that an acceptance criterion was met while
the behaviour behind it was not delivered, and both times the criterion was the
problem. "Emits a single straight segment" was satisfiable by picking a
convenient corridor. "Materially straighter than its corridor" was satisfiable by
any improvement at all. The criterion that would have caught it on the first pass
is the one task 67 now carries: on open ground, the published path equals the
straight line, exactly.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 67 | 5b | Line-of-sight path smoothing | Publish the greedy line-of-sight smoothing of the corridor, per design section 7's 2026-08-07 amendment: anchor at the first point, advance a probe to the furthest corridor point still visible from the anchor, emit it, repeat. Visibility is `LineOfSight.IsVisible` against the wall bucket index — the same exact, epsilon-free predicate the shooting model uses. `PathService.Advance` takes the `WallBuckets` it needs. `Funnel.StringPull` and `FunnelTests` are **not** deleted and **not** edited: the amendment records why the port is kept and marked off the v0.1 publish path. | `src/Sandata.Core/Navigation/PathSmoothing.cs`, `src/Sandata.Core/Navigation/PathService.cs`, `tests/Sandata.Core.Tests/PathSmoothingTests.cs`, `tests/Sandata.Core.Tests/PathServiceTests.cs` | `PathSmoothingTests` passes: across open ground the published path is **exactly two points and equals the straight line**, asserted as literal coordinates rather than as a vertex-count improvement; a path around one wall publishes exactly three points; every emitted segment passes `LineOfSight.IsVisible`; no segment can be removed without breaking visibility, which is what "minimum vertices" means here. `PathServiceTests` still passes with the recompute-from-request test reproducing the identical smoothed polyline. `FunnelTests` untouched and green. | 20, 65 | |

Task 67 joins wave 5b. It is the third task to own `PathService.cs` in sequence —
27 wrote it, 65 changed what it publishes, 67 changes how — and each held it
alone. That is the file-ownership rule working as intended rather than three
agents discovering each other in a merge.

### Wave 5b complete, 2026-08-07

Tasks 64, 65, 66, and 67 merged into `sandata-scaffold`, no conflicts. Wave 5b
existed because wave 5 exposed four defects that every wave-6 task would
otherwise have built on top of, and all four are now closed.

- `Sandata.Core.Tests`: `Failed: 0, Passed: 765, Skipped: 0, Total: 765`
- `Sandata.Client.Tests`: `Failed: 0, Passed: 41, Skipped: 0, Total: 41`

Gate, run by the integrating thread:

```
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Seed-1 workload, captured separately so the hashes appear in full:

```
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
```

Still byte-identical to untouched `main`.

**What each task settled.**

Task 64 widened exactly four identifiers to `ulong` — the operator's entity id,
the remembered enemy's entity id, the group id, and the next-entity-id counter —
and pinned in a reflection test why faction ids, door ids, cell indices, and
stream ids stay `int`. A faction id is a two-valued selector, a cell index is a
`NavGrid` node index and `NavGrid` uses `int`, and neither shares an identifier
space with an operator. `SquadSlotIndex` is gone from the operator record.
`WorldUnits.FromFixedPoint` floors by arithmetic shift, matching
`NavGrid.WorldToCellCoordinate` exactly, and is cross-checked against
`IntegerMath.FloorDiv` across the whole `int` range rather than at a handful of
points. No pinned constant moved.

Task 66 folds the enum member itself for a system tag, matching how every other
Sandata enum reaching a hash is folded, and reported that the accuracy draw
necessarily moved as a result. No expectation needed editing because
`AccuracyRulesTests` pins reproducibility and the dispersion bound rather than a
literal draw value — but the report said the draw moved rather than treating an
untouched test file as proof that it had not. That distinction is the whole
point of the reporting rule.

Task 67 is the one worth remembering. See the amendment above it: the funnel
could not straighten a single-cell corridor, line-of-sight smoothing does, and
the acceptance criterion is now the taut path as literal coordinates —
`(2,2)` to `(38,14)`, exactly two points, across open ground. `Funnel.StringPull`
remains in the tree with its licence and its tests, called only from
`FunnelTests`, marked off the v0.1 publish path.

**The pattern across waves 5 and 5b.** Every defect found in this run was found
because an agent hit a wall its file list would not let it climb, and reported
the wall instead of climbing it: the unowned `NavGrid` member, the unowned
smoothing step, the two identifier types, the missing world-unit conversion, the
undeclared system tags, and the design's own slot-index contradiction. The file
list is not overhead. It is the instrument.

### The alert level is hashed, drawn, and never changes: task 68

Task 35 declared `AlertLevel` and implemented no transitions, reporting that no
task row and no acceptance criterion named one. Checking the design confirmed the
gap is upstream: section 4 makes the level authoritative and hashed, section 11
gives it three theme roles and an indicator that differs by shape as well as
colour, and nothing in the document says what moves it. The state was hashed,
drawn, and inert.

Design section 5 now carries the rule and, more importantly, the tick placement:
the transition is evaluated during sensing against the frozen tick-start view and
committed after that view is released, so intent selection reads the previous
tick's level. An alert level that changed underneath the units evaluating against
it would make the outcome depend on processing order, which is the failure the
tick seam exists to prevent. The visible cost is a one-tick delay between hearing
a shot and reacting to it.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 68 | 7 | Alert level transitions | Implement design section 5's 2026-08-07 amendment: `Calm` to `Raised` on an identified contact or on hearing gunfire, breaking glass, or a death scream; `Raised` to `Breach` on a friendly death or a wall breach; never decreasing within a mission. Evaluated against the frozen tick-start view, committed after it is released. Pure functions over parameters — `AlertLevel.cs` and `MissionState.cs` are read-only here, and this task declares no new hashed field. | `src/Sandata.Core/Sensing/AlertRules.cs`, `tests/Sandata.Core.Tests/AlertRulesTests.cs` | `AlertRulesTests` passes: each trigger raises exactly one level and no more; a trigger for a level already reached changes nothing; the level never decreases across a long synthetic tick sequence including a quiet stretch; permuting the order of the operators evaluated changes nothing; and a test asserts that a level committed this tick is not visible to a reader of the frozen view. | 35, 36 | |

**The provisional radii task 35 reported.** Four of the sound radii come from the
design or the research consolidation: bolt cutter 72 wu, smoke 160 wu, hammer or
crowbar 192 wu, breacher shotgun 400 wu. Five were invented and are marked
provisional at every site: gunfire 352 wu, breaking glass 416 wu, death scream
480 wu, the identify boundary 96 wu, and the detect boundary 256 wu. The only
thing making them acceptable is that they are labelled rather than presented as
measurements. A tuning pass owns them, and until it runs, no document should
quote them as though they were derived.

**One generalisation to watch.** Task 35 satisfied "a door opened out of sight is
not observed until seen" with a generic `ObserveOrRemember` helper demonstrated
on a bare boolean, because `DoorState` and `MissionState` were forbidden to it.
The rule is proven generically and is not yet wired to the actual door type.
Whichever task wires sensing into the pipeline has to connect it, and the fact
that a passing test does not prove the door path works is exactly the sort of
thing that is invisible six weeks later.

### Wave 6 complete, 2026-08-07

Tasks 34 through 41 merged into `sandata-scaffold`, eight parallel worktrees, no
conflicts. Counts through the supported entry point, which task 41 made possible:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 836
     Passed: 836
Total tests: 138
     Passed: 138
[PASS] Release repository tests completed.
```

**Three document errors this wave found and corrected.**

Task 40 measured the sound catalog instead of trusting the prose and found that
the plan, the design, and the research consolidation all say the matrix is 484
slots. The catalog as built declares **106 slot rows expanding to 524 individual
variant files**. The 484 traces to the research consolidation's own summing
table, which predates task 24's implementation, so the design inherited it and
the plan inherited it from the design. All three are corrected in place; the
research table is left as written with a correction note beneath it so the error
stays traceable rather than being erased.

The corrected cost is **104,800 credits at zero rejects** — not the 96,800 the
design stated — which still fits the Creator tier at 22 USD. At a realistic 50
percent reject rate it is 209,600 credits and needs the Pro tier at 99 USD. The
manifest states plainly that whether credits scale with requested duration is
**UNVERIFIED**, and that if they do, every row longer than half a second costs
more than the estimate says.

Task 39 found that this plan's row 39 says "until task 49 measures them" while
the risk register and task 54's row both say task 53. Task 49 is the tick
pipeline and 53 is the measurement task, so 53 is right; row 39 is corrected.

**Nothing was generated and nothing was spent.** `scripts/sfx-manifest.ps1`
contains no network call — verified independently by the integrating thread, not
only by the task that wrote it — and the script ends by printing where the
manifest is and that a person has to authorise the spend. `sfx.ps1` gained batch
mode and per-family trim thresholds and was never executed.

**Provisional constants this wave introduced**, all marked at their site and none
presented as measured: `SandataSoundBudget.DefaultMaximumInstances` (task 53
measures the real MonoGame pool ceiling), and task 39's environment-selection
boundaries `CloseRangeMaxWu = 200` and `DistantRangeMinWu = 800`, which are its
own reasoned rule derived from the firearm catalog's range-band scale rather than
a design value. Task 39 also found that `SoundSlot` already carries a per-row
`TailTicks` from task 24 and used it rather than inventing a second tail
constant.

### A third client duplication, and the extraction question it raises

Task 39 could not reach `SoundVariantSelector`: `Hukbo.Client` grants
`InternalsVisibleTo` to `Hukbo.Client.Tests` only. It ported the SplitMix64 mix
with the same golden-gamma constant and documented why. That is the third time a
Sandata client task has been forced to copy a Hukbo client internal — the
spectator camera in task 33, the rotated-block draw in task 37, and now the
variant selector.

Three duplications is the point at which the question stops being hypothetical:
whether a `Hukbo.Shared.Client` tier-2 extraction is worth doing, or whether
`Hukbo.Client` should simply grant `InternalsVisibleTo` to `Sandata.Client`. The
grant is one line and no code motion; the extraction is cleaner and carries the
tier-1 hazard in full, since an assembly-level attribute or a same-namespace
dependency travels with a moved file and neither is visible to an import scan.
**This is a decision, not a task, and it is deliberately left open here rather
than settled by whoever next hits the wall.**

### The HUD exists and nothing draws it: task 69

Task 38 built every HUD element as a pure layout helper taking its bounds as a
parameter, because `SandataGame.cs` belonged to task 33 and was closed by then.
Task 37 built the operator geometry the same way. Both are correct and neither is
on screen: no task owns composing the HUD, wiring the renderer and the sound
player into the game loop, and drawing any of it.

This is the fifth unowned step this session — after `NavGrid`'s passability
array, the client csproj, the corridor smoothing call, and the RNG system tags —
and the pattern is now unmistakable. A file-level disjointness audit cannot catch
a missing *call*, and four of the five were missing calls or missing
declarations rather than missing files.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 69 | 7 | Client composition | Compose the HUD from task 38's elements, wire task 37's operator geometry and task 39's sound player into the game loop, and draw all of it through the existing world renderer and theme. The layout helpers stay pure and untouched; this task owns only the composition and the call sites. Where an element needs a value the simulation does not yet expose, pass a documented placeholder rather than reaching into `Sandata.Core`. | `src/Sandata.Client/SandataGame.cs`, `src/Sandata.Client/UI/HudComposer.cs`, `tests/Sandata.Client.Tests/HudComposerTests.cs` | `HudComposerTests` passes with no graphics device: the composed rectangles do not overlap at three window sizes, every element from design section 11's HUD list is present exactly once, and the composer degrades sanely at the smallest supported window rather than producing negative or inverted rectangles. Whether any of it actually appears on screen stays a manual smoke row that only a human at a desktop may flip. | 33, 37, 38, 39 | |

### Wave 7 complete, 2026-08-07

Nine tasks — 42, 43, 45, 46, 47, 48, 57, 68, and 69 — all merged into
`sandata-scaffold`, each from its own worktree, with no merge conflicts. The
wave held nine tasks against an eight-agent ceiling, so it ran as two batches:
eight in parallel, then task 69 alone.

Counts through the supported entry point:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 909
     Passed: 909
Total tests: 169
     Passed: 169
[PASS] Release repository tests completed.
```

The canonical gate was run by the integrating thread after integration, not
delegated:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Still byte-identical to untouched `main`. Seven waves of a second game have now
moved no Hukbo hash.

#### Why task 69 was held back rather than dropped from the wave

The eight-agent ceiling forced a split, and which task to hold was not
arbitrary. Task 45 built the three tactical overlays and task 46 built the
minimap, the multi-select state, the drag capture layer, and the undo stack, all
as pure helpers with no call site. Task 69's row named only tasks 37, 38, and
39, so as written nothing in the plan ever drew tasks 45 or 46. That is the
sixth instance of the unowned-step pattern this project has hit.

Holding task 69 for the second batch turned the ceiling into the fix. By the
time it ran, tasks 45 and 46 were merged and visible to it, and every call site
it needed lived inside `SandataGame.cs`, which it already owned. Its brief was
extended to cover them explicitly. No file ownership changed and no task was
re-cut.

#### A real defect in the collision resolver, found by task 43

`SandataCollisionResolver.CommitOne` does not guarantee non-overlap. Task 43 hit
it as a reproducible `Entities 2 and 3 overlap after commit` failure, correctly
declined to edit `src/Sandata.Core/Collision/` because it was outside its file
list, and reported it. The integrating thread verified the claim against the
source before accepting it rather than trusting the report.

There are two distinct causes, both in `SandataCollisionResolver.cs`:

- `_committedGrid` is cleared at the start of every `Resolve` call and gains a
  body only when that body's own turn is processed, in ascending priority order.
  The highest-priority entity therefore evaluates its move against an empty
  grid, and can move directly onto a lower-priority entity's real current
  position without seeing anything there.
- The `Blocked` fallback commits the entity to `request.StartXRaw` and
  `request.StartYRaw` unconditionally, with no check that the start position is
  still clear. By the time a lower-priority entity is blocked, an
  earlier-priority entity may already be standing where it is about to fall
  back to.

The separation path only fires on exact coincidence, through
`AnyCoincidentUnchecked`, so a partial overlap at the desired position skips the
separation search entirely and goes straight to the unchecked fallback.

Task 43 worked around this inside its own test's proposal generator, which was
the right call for a task forbidden the file, but it means the production defect
is still present. A passing `LocalAvoidanceTests` does not prove that stage 10
cannot produce an overlap. This is the same shape as the wave-6 note about task
35's door path: a rule proven generically and not yet wired to the thing it has
to govern.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 70 | 8 | Close the collision resolver's overlap hole | Make `SandataCollisionResolver.Resolve` guarantee that no two committed bodies overlap. Seed `_committedGrid` with every request's start position before the commit loop and remove each body as its own turn is processed, so a mover sees the real current positions of entities it has not reached yet. Make the `Blocked` fallback validate the start position before committing to it, and give the resolver a written rule for what happens when the start position is itself occupied. Change the commit priority order in no way — it is the total key `(groupId, slotIndex, entityId)` and it stays that. Remove task 43's `WouldStepIntoALowerPriorityEntitysCurrentPosition` guard from `LocalAvoidanceTests` once the resolver no longer needs it, and say in the report whether the funnel test still passes without it. | `src/Sandata.Core/Collision/SandataCollisionResolver.cs`, `tests/Sandata.Core.Tests/SandataCollisionTests.cs`, `tests/Sandata.Core.Tests/LocalAvoidanceTests.cs` | `SandataCollisionTests` gains a fact that fails against today's resolver: two entities where the higher-priority one's desired position lands exactly on the lower-priority one's start position, asserted to produce no overlap after commit. A second fact covers the blocked-fallback case, where an entity's start position has been taken by an earlier commit. `LocalAvoidanceTests` passes with task 43's test-side guard removed, and the pinned eight-body committed-position list from task 16 is re-derived and re-pinned if it moved, with the move reported rather than absorbed. | 16, 43 | |

Task 70 is placed in wave 8 rather than wave 7 because it changes committed
positions, and every wave-7 task that consumes the resolver had to be green
against the current behaviour before the behaviour moves underneath it.

#### The alert ladder: the code and design section 5 currently disagree

Task 68 implemented a fixed-ceiling model rather than the two adjacent steps
design section 5's amendment narrates. Under its reading, the low-severity
trigger family — an identified contact, or a heard sound that carries intent —
raises the level to at most `Raised`, and the high-severity family — a friendly
death or a wall breach — raises it to at most `Breach`, from wherever the level
started. A friendly death therefore reaches `Breach` directly from `Calm`.

The design text says `Raised` becomes `Breach` on a friendly death, which read
literally means a death only matters once the faction has already noticed
something else. The integrating thread checked that literal reading before
accepting the deviation, and it is degenerate. Triggers are evaluated per tick.
A first death evaluates against a previous level of `Calm`, so only the raise
rule can fire; on the following tick the death is no longer a trigger, and
`Breach` is never reached. Under the literal ladder the first death in a mission
can never breach — only a death occurring after some separate contact or heard
sound can. That is very unlikely to be what the amendment intended.

Two consequences are recorded here rather than settled:

- **Design section 5's amendment needs an edit, and it is a gameplay decision
  rather than an implementation detail**, so it is deliberately not made here.
  The code is the deviation until someone confirms it. Reversing it is one line
  in `AlertRules.Evaluate`.
- **Task 68's acceptance criterion is now inaccurate.** "Each trigger raises
  exactly one level and no more" describes the ladder reading. The property the
  implementation actually holds, and the one its tests assert, is that no
  trigger raises the level past its own named ceiling. If the ceiling model is
  confirmed, the criterion should be restated in those terms.

This is the third time in this project that an acceptance criterion has turned
out to describe something other than the behaviour wanted. The first two were
caught after the fact; this one was caught because the implementer reported the
ambiguity instead of choosing silently.

#### Three more unowned steps, none of them files

- **Nothing supplies the production formation half-width.** Task 42 correctly
  made it a parameter and used a test-local value of 8, marked provisional. No
  `SandataRuleset` field carries it and nothing derives it from the slot table,
  although the maximum absolute lateral offset across a formation's slots is the
  obvious derivation. Whichever task wires stage 9 owns this.
- **Nothing implements `ISandataSoundOutput` against MonoGame.** Task 69 wired
  task 39's `SandataSoundPlayer` into the game loop against a private
  `NullSandataSoundOutput` stub that always refuses, because no MonoGame-backed
  implementation exists anywhere and adding one was outside its file list. The
  sound player is constructed, correct, and mute. There is also no shot-event
  source to trigger it until the tick pipeline lands.
- **`tools/README.md` has no row for `Sandata.Tools.AudioPool`.** Every other
  harness under `tools/` is described there. Task 48 could not reach the file
  and said so. Task 54 is the documentation task and is the natural owner.

Task 49's brief now carries four call-site obligations that wave 7 created and
deliberately did not satisfy: apply the order queue at stage 1, evaluate the
alert transition during stage 5 and commit it after the frozen view is released,
apply the formation collapse to slot offsets in stage 9, and run local avoidance
at stage 10. Each is a pure function today with no production caller.

#### What the audio pool measurement found

Task 48's harness ran on this machine and returned a real number rather than an
estimate. The MonoGame DesktopGL and OpenAL instance pool holds 256 concurrent
instances; the 257th `Play()` raises `InstancePlayLimitException`. Eight
shooters, each holding one looping instance with a tail cue fired at every tail
interval, sustained ten seconds of continuous fire with zero refusals and zero
exceptions.

`SandataSoundBudget.DefaultMaximumInstances` is currently 64, so the provisional
constant is conservative by a factor of four rather than optimistic. These
figures are recorded here and in no other document. Task 53 re-runs the harness
and task 54 writes the numbers into `docs/development/testing.md`.

The harness measures `Microsoft.Xna.Framework.Audio.SoundEffectInstance`
directly rather than going through `SandataSoundPlayer`, which is `internal` to
`Sandata.Client`. That was deliberate: widening the grant is the open decision
recorded in the wave-6 amendment above, and a measurement harness is not the
place to settle it.

#### `verify.ps1 -Game Sandata` and what it currently proves

Task 47's passthrough works end to end. `./scripts/verify.ps1 -Game Sandata`
passes formatting, the Release build, and both Sandata test suites, then fails
at the benchmark stage with:

```
Argument error: Unsupported argument '--agents'.
Usage: sandata-headless [--help] [--log-level off|err|warn|inf|dbg|trc] ...
```

That is the expected state and not a defect. `Sandata.Headless/Program.cs`
parses only `--help` and the log flags today; task 51 adds the workload flags.
The failure is itself evidence the passthrough is correct, because the flags
reached the Sandata headless binary rather than the Hukbo one.

The default gate is untouched. `./scripts/verify.ps1` with no `-Game` argument
still invokes `benchmark.ps1` exactly once with 200 agents, 10,000 ticks, and
seed 1, and `ScriptDefaultsTests` was proved able to fail by temporarily adding
a second invocation and observing
`Assert.Single() Failure: The collection contained 2 items`.

Task 47 also replaced `doctor.ps1`'s four hardcoded lock-file paths with a check
derived from the project list rather than from the lock files on disk, on the
reasoning that a disk-derived list can only confirm files that exist and can
never notice a project whose lock file was never generated. All twenty projects
report present, and the check was proved able to fail by removing
`src/Sandata.Core/packages.lock.json` and observing
`[FAIL] Project is missing its packages.lock.json`.

#### Provisional constants this wave introduced

All marked at their site, none presented as measured or derived:

- `UndoStack<T>.DefaultDepthLimit = 50` (task 46). The depth-limit test uses a
  caller-supplied limit instead, so no acceptance criterion depends on the
  invented number.
- `OrderPathOverlay.WaypointMarkerRadiusWu = 6` and
  `BreachMarkerOverlay.BreachMarkerRadiusWu = 12` (task 45), the latter chosen
  larger than `WorldRenderer.WallThicknessWu` so a breach marker does not vanish
  inside its own wall. The fire cone's far edge is a straight chord rather than
  a true arc, marked as a rendering simplification.
- `Order.MaxAuthoredPathNodeCount = 128` (task 57). Design section 16 requires
  the cap to be a `const` on the order type and names no value. Task 58 enforces
  it as a rejection rule and may revise it before any fixture depends on it.
- Task 43's funnel fixture constants, all test-local: a 200-tick budget, a
  15-raw maximum step, a 10-raw body radius, a 40-raw cell size, and a 15-raw
  door half-width.
- Task 69's ten placeholder values, each documented at its site with the task
  that supplies the real one — body radius for nav baking, operator apparent
  scale and detail tier, fire-cone half-width and range, alert level, the empty
  order-path waypoint list, the entity id taken from a spawn record's array
  index, the faction-0-is-friendly mapping, and the null sound output backend.

Task 48's harness constants — an eight-operator maximum, a 150 ms loop clip, a
764 ms tail clip, and a ten-second sustain — live in `tools/` only and reach no
shipped code.

#### Two things worth remembering from this wave

**A doorway cannot be a mathematical point.** Task 43's first funnel fixture
aimed eight lanes at the exact coordinate `(0,0)` and deadlocked permanently.
Eight bodies of finite radius cannot occupy one point, and because the sidestep
rule is fully deterministic — a fixed rotation with the side pinned on entity id
parity — a failed sidestep against a symmetric, equally frozen neighbour never
resolves on retry, since nothing about the state changes between ticks. The
fixture now uses a finite door band. This is a property of any deterministic
avoidance model and is worth knowing before anyone tunes one: determinism
removes the jitter that lets a stochastic model escape a symmetric deadlock.

**Verify a report's numbers against the disk, not against the report.** Task
69's report gave `SandataGame.cs` as 663 lines; the file on disk is 887. Nothing
about the work was wrong and the test output was accurate, but the file-level
figures in a long agent report drifted. Every branch in this wave was checked
with `git diff --stat` against its merge base before merging, and every reported
file list matched. Doing that check is cheap and it is the only thing that would
have caught a report claiming a file it never wrote.

### Wave 8 complete, 2026-08-08

Three tasks — 58, 59, and 70 — all merged into `sandata-scaffold`, each from its
own worktree, with no merge conflicts. The wave ran as a single batch of three,
well under the eight-agent ceiling, because the three file sets are disjoint and
every dependency was already merged.

Counts through the supported entry point:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 937
     Passed: 937
Total tests: 169
     Passed: 169
[PASS] Release repository tests completed.
```

The Sandata core suite moved from 909 to 937. That is 10 facts from task 59, 14
from task 58, and 4 from task 70, and the arithmetic was checked against the
merged tree rather than taken from the three reports.

The canonical gate was run by the integrating thread after integration, not
delegated:

```
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Still byte-identical to untouched `main`. Eight waves of a second game have now
moved no Hukbo hash.

#### All three agents died at launch on a transient API failure

The first dispatch of all three tasks terminated within seconds with
`API Error: Unable to connect to API (ConnectionRefused)`. The local proxy on
`127.0.0.1:8787` was still listening throughout, and the integrating thread's own
tool calls kept working, so the refusal was specific to the three simultaneous
sub-agent connections rather than to the machine being down.

All three worktrees were confirmed clean at the base commit before anything was
retried, so nothing had been half-written. The agents were then resumed by
message rather than re-spawned, which preserved each one's transcript and cost
nothing. Re-spawning would have been the wrong move: a fresh spawn starts with an
empty transcript and the brief has to be sent again. This is worth knowing
because the failure looks alarming and is recoverable in one step.

#### Task 58 shipped two submission doors, and was sent back to close one

Task 58 added `OrderQueue.SubmitValidated` as a new public method and left the
existing `OrderQueue.Submit` public and unvalidated beside it. Every acceptance
criterion passed, and the work was correct as far as it went, but design section
16 says "An order is validated when it is submitted", and as shipped the
validation was opt-in. Task 49 wires the queue into the tick pipeline and could
have called the unvalidated door without anything failing.

This is the same shape as the six earlier unowned-step findings, and it is worth
separating out because the earlier six were missing call sites while this one was
a bypassable call site. A file-level ownership audit cannot see either. There
were four `.Submit(` call sites in the whole repository, all inside
`OrderQueueTests.cs` and none in production, so narrowing cost almost nothing:
`Submit` is now `private`, `SubmitValidated` is the only public entry, and it
delegates internally for the five order kinds that carry no authored polyline.

The four pre-existing facts were migrated to enter through the validated door.
That relaxed the standing "existing facts unmodified" constraint deliberately and
narrowly: the constraint exists to stop an agent silently rewriting a test to go
green, not to freeze an API against a reason to narrow it. Each migrated fact
still asserts exactly the property it asserted before, and the agent listed them
one by one. One fixture's nav grid grew from 10x10 to 20x20 cells because the
validated door now bounds-checks that fact's own path, which reaches world
coordinate (50, 60); the property under test did not change.

#### Task 58's interpretation call: a rejected order consumes an id

Design section 16 calls `OrderId` "dense, ascending, assigned at submission", and
separately requires a rejected order to emit an event "carrying the order id and
the reason code". Those two readings conflict for exactly one case, because an id
has to be assigned before it can be carried, and a rejected order never enters
the stored set.

Task 58 resolved it in favour of carrying the id: `SubmitValidated` advances both
counters on a rejection and does not append to `Orders`, so the stored sequence
can contain a gap. The reasoning is recorded in that method's own XML remarks
rather than only here. The alternative reading — that "dense" governs the stored
set and a rejection consumes nothing — is defensible and was not chosen. Whoever
builds the event feed should revisit it, because the choice only matters once a
rejection is observable somewhere other than the caller's own return value.

#### There is still no event feed, and it is still nobody's

`Sandata.Core` has no event type at all. It has `MissionState.NextEventSequence`
and nothing that consumes it. Design section 16 says a rejected order "emits an
authoritative event", and design section 11's HUD list has an event log element
marked built, which draws events that no core type produces.

Task 58 could not build one — an event feed lives outside `Orders/` and outside
its file list — and correctly did not invent one. It returns an
`OrderRejection(OrderId, Reason)` value instead, which carries both required
fields and is not silently dropped, but is observable only to the immediate
caller. Design section 10's question 8 asks whether a spectator can discover an
effect without reading source code; a rejection visible only as a return value
does not yet clear that bar.

This is now a named gap rather than an incidental one. Whichever task wires the
order queue into the tick pipeline owns deciding where `OrderRejection` values
go.

#### Task 59 enforced "no third case" in the type system rather than in a test

Design section 16 says an operator's movement comes from exactly one of two
sources, with "no third case and no blend of the two". Task 59 made that
structural: `MovementSource` is an abstract record with a private constructor and
exactly two nested sealed subtypes, so no third subtype is declarable anywhere
outside the file, and no cast can fabricate one. The brief had asked for a test;
what came back is stronger, because a wrong implementation fails to compile
rather than failing an assertion. An enum would have been weaker — an enum
permits an undefined numeric value.

The four clearing conditions carry `AssignmentClearReason.ReachedFinalNode`,
`Cancelled`, `OperatorDied`, and `PolylineUntraversable`, asserted distinct from
one another rather than merely non-default.

#### The wave-7 hygiene test caught a planned dictionary before review did

Task 59 intended an `IReadOnlyDictionary<ulong, OrderAssignment>` for its
slot-targeting roster and was stopped by
`SandataSourceHygieneTests.SandataCoreDoesNotUseBannedNumericOrCollectionTypes`,
the test wave 7 added for design section 7's "flat arrays, no dictionaries" rule.
The roster became a sorted `ReadOnlySpan<ulong>` with a hand-rolled binary
search.

This is the first time one of this project's own hygiene tests caught a design
violation before a human or an integrating thread did. It is worth recording as
evidence that the assembly-boundary and banned-type tests earn their maintenance
cost, because the equivalent rule stated only in a design document has been
missed repeatedly.

#### Task 70 closed the overlap hole, and left one documented last resort

The resolver now seeds its committed grid with every request's real start
position before the commit loop and removes each body as its own turn is
processed, so a mover sees where the entities it has not reached yet actually
are. The `Blocked` fallback now validates the start position before committing to
it. The commit priority order is untouched and is still the total key
`(GroupId, SlotIndex, EntityId)`.

Three new facts were each proved able to fail: the resolver file alone was
stashed, the facts were run against the unmodified resolver, and all of them
failed with real overlap coordinates in the output — for example
`Entities 1 at (200, 0) and 2 at (215, 0) overlap.` The stash was then popped and
all of them passed. A general pairwise no-overlap assertion was added as a shared
helper and is now applied to five fixtures rather than only to the new ones.

The invariant has one stated exception. When an entity's start position is
occupied at fallback time, the resolver repairs it with the same bounded ring
search used for an exact coincidence at the desired position, and reports
`Separated` rather than `Blocked`. If that search also exhausts its sixteen rings
without finding daylight, the resolver commits into the occupied start position
as an absolute last resort. That is the one place where no-overlap is not
guaranteed, it is documented at the site rather than hidden, and it has never
been observed to trigger against any fixture in this codebase. It exists so the
method always returns a value rather than throwing out of an already-degenerate
scene. Anyone tightening this later should know the hole is deliberate and
labelled, not overlooked.

#### Task 43's workaround guard is gone and the funnel still passes

`WouldStepIntoALowerPriorityEntitysCurrentPosition` was task 43's test-side
pre-filter, written to work around exactly this resolver defect. It has been
removed along with its call site, and
`EightUnitsFunnellingIntoOneDoorwayAllPassThroughWithNoOverlapAndNoDeadlock`
passes without it, well inside its 200-tick budget. That was the stated condition
for task 70 being done at all, and it is met.

#### One pinned fixture moved, and it is the right one

Task 16's pinned eight-body committed-position list did not move. Entities 1
through 8 land at their original positions and resolutions; only the no-overlap
assertion was added to that fixture.

`TwoBodiesAtTheSameStartingPositionResolveToADeterministicSeparation` did move,
and it is a different fixture from the one the task 70 row named. Its pinned
outcome flipped from entity 1 holding at (0, 0) with entity 2 separating to
(20, 0), to entity 1 separating to (20, 0) with entity 2 holding at (0, 0). The
cause is the fix itself: the grid is now seeded with both start positions before
either entity is processed, so entity 1, processed first because it has the lower
id, is the one that discovers the coincidence and separates. Under the old
empty-grid behaviour entity 1 saw nothing and entity 2 was the one that had to
move.

The flip was confirmed by an actual run rather than by hand-tracing alone, and it
is documented in the fixture's own remarks. It is a consequence of the rule
changing, not a re-pin of convenience, and it is recorded here because the
standing rule is that a moved pin is reported rather than absorbed.

#### The resolver's cost changed, and the allocation claim is inspection not measurement

`SandataCollisionGrid` has no per-body remove or update primitive, so removing a
body as its turn comes up is implemented as a full clear and re-insert of every
live body, once per request. That is linear per request and quadratic per
`Resolve` call, where it was linear per call before. For an indoor operator squad
this is not a concern and the trade is documented in the class remarks, but it is
a real change to a per-tick hot path and it is recorded here rather than left to
be rediscovered by whoever runs task 50's navigation benchmark matrix.

The zero-allocation-on-a-warm-tick property is claimed by code inspection, not
measured: the committed grid, the result list, and the new live-body list are all
reused across calls and the body write is an in-place struct write into an
existing slot. No allocation micro-benchmark exists for this resolver. The agent
said so plainly instead of implying a measurement, which is the behaviour the
verification-honesty rule is asking for, and the claim should be treated as
unverified until task 53 measures it.

#### Provisional constants this wave introduced

All marked at their site, none presented as measured or derived:

- `MovementSource`'s precedence order among the four clearing conditions when
  more than one holds on the same tick. Design section 16 lists the four
  conditions and does not order them; the chosen order is documented in that
  file's XML remarks.
- `MovementSource.MaxCellsPerSegment`, a traversal-buffer bound derived from
  `NavGrid.MaxDimensionCells`.
- `Order.MaxAuthoredPathNodeCount = 128` is unchanged. Task 58 was free to revise
  it, looked, and found no reason in design section 16 or in any existing test to
  move it. It keeps task 57's PROVISIONAL marker at its original site.
- A 1.5-world-unit clearance in one of task 58's wall-proximity fixtures. It is a
  test-local value that exercises the exact predicate against a near miss; it is
  not a runtime constant and carries no tuning claim.

#### The wave-9 audit, run before dispatch rather than after

Both directions were run over wave 9's five rows: no file is claimed by two
tasks, and every file named in a "What" column is claimed by exactly one task.
The file-level audit passed. It then found five things a file-level audit cannot
see, which is the whole reason the audit is now run in both directions.

1. Nothing draws task 62. Task 62 builds `PathDrawTool`, `GoCodePanel`, and
`OrderQueueView` as pure layout helpers with no call site, and
`src/Sandata.Client/SandataGame.cs` became unowned the moment task 69 finished.
This is the seventh instance of the pattern. It is worse than the previous six in
one specific way: `HudComposer.Layout` has thirteen fields matching design
section 11's HUD element list exactly, and that list has no row for a go-code
panel or an order queue view, because section 11 was written before section 16
promoted the order layer. Two new window-anchored panels would have had no
anchor, no bounds, and no overlap test.

2. Task 61's change does not reach the client. This was checked rather than
assumed. No file under `src/Sandata.Client` or `tests/Sandata.Client.Tests`
references `MissionState`, `MissionSnapshot`, or any `Sandata.Core.Simulation`
type in code; the only four occurrences are doc comments, each stating that the
helper deliberately takes primitives instead. Of task 69's ten placeholders, one
— the empty order-path waypoint list — is supplied from the client-side draw tool
and belongs to the order-UI work. The other nine wait on the tick pipeline in
wave 10. No follow-through task is needed for task 61, and this is recorded so
the question is not reopened.

3. Task 63 has no input document. Its row says tasks 23, 27, 28, and 32 "each
reported a verdict instead" and that task 63 "applies all four verdicts in one
edit". Those verdicts were never written down. A search for the word across the
whole plan returns only task 63's own row and the paragraph that promises them,
and the four constants in `SandataRuleset.cs` are still task 9's provisionals.
Task 63 therefore has to re-derive each value from the code and tests that
consume it, which is different and larger work than applying a record, and it
must be allowed to return "no change justified" for any of the four rather than
inventing a number to satisfy the row.

4. Task 63's acceptance criterion presumes a change. It requires
`SandataRuleset.ContentHash` be updated from `8955292433887190872` to a newly
measured value. If re-derivation justifies keeping all four constants, the hash
does not move and the criterion cannot be met as written. The criterion is
restated: the hash moves if and only if a value moves, and the report says which.

5. The preset-version rule may bind, and task 63 cannot satisfy it as scoped.
`SandataRulesetTests.ModernTacticalV1_ContentHashIsPinned` carries its own doc
comment saying a moved value "is a new preset version with a new recorded
expectation, not a fix to this test", and `CLAUDE.md` section 5 requires a new
preset version for changed weights. `SandataPresetId.cs` is not in task 63's file
list, so as scoped it could not produce a `ModernTacticalV2` even if one were
required.

Revising `ModernTacticalV1` in place is nonetheless the right call here, and the
reasoning is recorded so it is not mistaken for an oversight: no golden replay,
no save file, and no recorded Sandata baseline references the preset yet — design
section 16 states this explicitly when it says no golden mission hash exists —
so there is no earlier artifact for a version bump to protect. The moment a
golden replay is recorded, this stops being true and the preset-version rule
binds in full.

#### Task 49's call-site obligations, extended

Wave 7 recorded four. Wave 8 adds three more, and one belongs to the client
rather than to task 49. Every one of these is a pure function today with no
production caller:

- apply the order queue at stage 1, through `OrderQueue.SubmitValidated` and
  never through the now-private `Submit`
- select intent at its stage (task 44)
- choose each operator's movement source, and exclude ordered operators from slot
  targeting (task 59)
- evaluate sync sets and go-code releases at stage 8 against the frozen
  tick-start view (task 60)
- evaluate the alert transition during stage 5 and commit it after the frozen
  view is released (task 68)
- apply the formation collapse to slot offsets at stage 9 (task 42)
- run local avoidance at stage 10 (task 43)

The go-code keypress path into the order queue is client-side and belongs to the
order-UI composition task below, not to task 49.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 71 | 9, second batch | Order-UI composition and the inspector's order rows | Give task 62's three helpers real call sites, and give design section 16's order layer the spectator surface it requires. Route the pointer chain into `PathDrawTool` at the priority design section 11 states — above the in-world layer, below every panel. Draw `GoCodePanel` and `OrderQueueView`, and add both to `HudComposer.Layout` as window-anchored panels with real computed bounds, since neither exists in design section 11's element list. Replace task 69's placeholder empty order-path waypoint list with the path currently drawn. Add design section 16's three operator-inspector rows — active order id, the node index currently being walked, and the reason code that cleared the last assignment — against documented placeholders until the tick pipeline supplies them. Wire the go-code keypress so it enters the order queue as an ordinary `GoCodeRelease` order rather than through a separate input path. Amend design section 11's HUD element list in the same change to carry the two new panels, in full normal English. | `src/Sandata.Client/SandataGame.cs`, `src/Sandata.Client/UI/HudComposer.cs`, `src/Sandata.Client/UI/OperatorInspector.cs`, `tests/Sandata.Client.Tests/HudComposerTests.cs`, `tests/Sandata.Client.Tests/HudLayoutTests.cs`, `tests/Sandata.Client.Tests/OperatorInspectorTests.cs`, `docs/plans/2026-08-07-sandata-scaffold-design.md` (section 11's element table only) | `HudComposerTests` passes with two more elements in the layout, both carrying non-empty rectangles that overlap no other panel. `HudLayoutTests` proves the go-code panel and the order queue view each clip correctly in a window too small for them. `OperatorInspectorTests` proves all three new rows render, and that the cleared-assignment row shows the reason code rather than a blank when an assignment has been cleared. A test proves the drawn path reaches the order-path overlay rather than the placeholder empty list. A test proves the go-code keypress produces a `GoCodeRelease` order carrying its own `TargetTick`, and that no other input path reaches the queue. Design section 11's element table names both new panels. | 62, 69 | |

Task 71 runs alone in a second batch after task 62 merges, exactly as task 69 ran
after tasks 45 and 46 in wave 7. The ceiling is not what forces the split this
time — wave 9 has five tasks and room for more — the dependency is. Task 71
cannot compose helpers that do not exist yet.

Giving task 62 these files instead was considered and rejected: it would have
made one agent responsible for three new pure helpers, three existing client
files, four test files, and a design amendment, which is past the size where a
single agent finishes reliably.

### Wave 9 complete, 2026-08-08

Six tasks — 44, 60, 61, 62, 63, and the new 71 — all merged into
`sandata-scaffold`, each from its own worktree, with no merge conflicts. The
wave ran as two batches: five in parallel, then task 71 alone. The eight-agent
ceiling is not what forced the split; task 71 composes task 62's helpers and
could not run before they existed.

Counts through the supported entry point:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 985
     Passed: 985
Total tests: 192
     Passed: 192
[PASS] Release repository tests completed.
```

The Sandata core suite moved from 937 to 985 and the client suite from 169 to
192. The core arithmetic closes exactly: 17 facts from task 44, 16 from task 60,
15 from task 61, and none from task 63, which added only documentation. The
client arithmetic closes at 16 from task 62 and 7 from task 71.

The canonical gate was run by the integrating thread after integration, not
delegated:

```
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Total tests: 3108
     Passed: 3108
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Still byte-identical to untouched `main`. Nine waves of a second game have now
moved no Hukbo hash.

#### Task 63 found that all four ruleset constants are read by nothing

This is the most consequential finding of the wave and it came from a task whose
row was wrong about its own input. The wave-8 audit had already established that
the four verdicts tasks 23, 27, 28, and 32 were supposed to have recorded were
never written down, so task 63's brief told it to re-derive from the consuming
code instead of applying a record, and explicitly authorised "no change
justified" as an outcome.

It returned "no change justified" for all four. `ContentHash` did not move and
stays `8_955_292_433_887_190_872`.

The reason it did not move is the finding. Verified against the working tree
rather than taken from the report: **not one of the four fields is read by any
production code.** Every reference outside `SandataRuleset.cs` is in a test.

- `PathLatencyTicks` — `PathService` holds its own property, set from its own
  constructor parameter. Nothing passes the ruleset's value into it.
- `LoweredWallDistanceWu` — `WeaponLoweredRules` takes a wall distance as a
  parameter. Only tests supply one, and they supply a local `8`, not the
  production `24`.
- `GroupCohesionRadius` — no reference anywhere under `src/`.
- `AimToleranceBam` — no reference anywhere under `src/`.

**Two of those are behavioural deviations from the design, not merely unwired
constants.** Design section 8 says two operators within `GroupCohesionRadius` are
unioned into a squad; `SquadGrouping.Compute` unions same-faction pairs
unconditionally from a pair list that `SandataCollisionGrid.Rebuild` already
filtered by physical body radius, which is a different quantity, and no
cohesion-radius comparison exists in the codebase. Design section 9 states the
aim transition rule as `|ShortestArc| <= AimToleranceBam`; `WeaponChain.Advance`
takes an already-decided `arcWithinTolerance` boolean and nothing anywhere
computes it, although `Bam16.ShortestArc` exists.

There is a second-order consequence. `SandataRulesetTests`'
`ChangingAnySingleField_MovesTheContentHash` currently passes on four fields no
behaviour depends on, which is exactly the failure its own doc comment warns
against when it says a pinned hash is meaningless if most of its inputs are dead
weight. The test is not wrong; the wiring behind it is missing.

An agent that had invented four numbers to satisfy the original row would have
moved the hash, passed every stated criterion, and buried both deviations. The
corrected brief is what prevented that, and it is worth keeping the shape of it:
name the evidence, say what a non-change looks like, and say that inventing a
value is worse than reporting none.

#### Nothing specifies what causes an intent, so task 44 invented it

Design section 5's stage-8 row names the six intents and says they are read from
the tick-start view. Neither it nor the plan row states a single trigger
condition for any of them. Task 44 therefore invented the whole selection rule:
a first-match-wins cascade of `Dead`, then `Reposition`, then `Engage`, then
`Breach`, then `Advance`, then `Hold`; the condition behind each; and a
`SuppressionRepositionThreshold` of 3 whose input, `OperatorState.SuppressionCounter`,
nothing in the repository currently increments.

It documented all of it as a reasoned decision rather than presenting it as
derived, which is the correct handling of an underspecified requirement. But
this is a design gap filled by an implementer, not a provisional constant, and it
is larger than the usual placeholder: how an operator decides to engage or fall
back is a gameplay decision. It should be reviewed before the tick pipeline makes
it load-bearing, and if it is confirmed it belongs in design section 5 as a
written rule rather than living only in one file's XML remarks.

#### A second bypassable door on `OrderQueue`, found by a task forbidden the file

Wave 8 recorded that task 58 shipped `SubmitValidated` beside a public,
unvalidated `Submit`, and that narrowing `Submit` to private closed it. Task 60
then found the same defect on a second door: `OrderQueue.Orders` is declared
`public ImmutableArray<Order> Orders { get; init; }` on a record, so
`queue with { Orders = ... }` injects arbitrary orders and skips validation
entirely.

Task 60 could not fix it — `OrderQueue.cs` was outside its file list — verified
that its own two files never do it, and reported. That is the wall working
exactly as intended for the second time in two waves.

It was deliberately not fixed during the wave, and the reason is worth recording
because it is not squeamishness. Three existing tests build queues through that
setter, and task 61 was at that moment adding snapshot resume, which legitimately
needs a non-validating way to rebuild a queue: a snapshot's orders were validated
when they were submitted, and revalidating them on resume would violate design
section 16's rule that an authored polyline is never recomputed. Narrowing the
setter before knowing what resume needs would have either broken task 61 or
forced it into a workaround. It is now task 72 below.

**These two are a distinct defect class from the six unowned steps before them.**
The earlier six were a *missing* call site. These are a *bypassable* one: the
correct path exists, the code is right, every acceptance criterion passes, and an
incorrect path sits open beside it. A file-level ownership audit cannot see
either, but the bypassable kind is harder, because nothing fails. The check that
finds it is to ask, whenever a task adds a validating or ordering entry point,
what else can reach the same state — a second constructor, an `init` accessor on
a record, a `with` expression, a public collection property.

#### Task 61 pinned the pre-change hash, which is what "appended" actually means

The brief demanded one fact above all others: that a state with an empty order
queue and no assignments hash to exactly what the same state hashed to before the
change. Task 61 captured `5_550_901_129_500_655_850` by running the pre-edit
hasher through a deliberately failing temporary test before touching any source,
then deleting the temporary file, and pinned it.

That is the operational meaning of "folded after every field the hasher already
covers". A reviewer can read a diff and believe the folds were appended; only the
pinned pre-change value proves it, and it would fail immediately if a fold were
inserted rather than appended.

It also added the case that would otherwise silently collide with that baseline:
a queue whose counters have advanced but which stores no orders is not
`OrderQueue.Empty`, and must hash differently from the empty case. A gate written
against emptiness alone would have merged the two.

The resume rule is proven the hard way as well. The fixture constructs a case
where recomputing the path would produce a *different* polyline from the stored
one, and asserts the stored one wins. A naive round-trip test passes even when
the implementation recomputes; that one does not.

#### Task 71 gave task 62's helpers their call sites, and left one behind

Task 71 exists because task 62 built three pure helpers with no caller, which
would have been the seventh instance of the pattern. It routed the pointer chain
into `PathDrawTool`, added `GoCodePanel` and `OrderQueueView` to
`HudComposer.Layout` with real computed bounds, replaced task 69's placeholder
empty order-path waypoint list with the path actually being drawn, wired the
go-code keypress so a release enters the queue as an ordinary `GoCodeRelease`
order, and amended design section 11's HUD element table with the two rows that
table had been missing since section 16 promoted the order layer.

Its most surprising claim was checked and is true: `OperatorInspector` already
carried `ActiveOrderId`, `OrderNodeIndex`, and `OrderClearReasonCode`, with
format functions and row wiring, from an earlier task. Design section 16's three
inspector rows were already built, so task 71 correctly edited nothing there
rather than duplicating them.

**`PathDrawTool.Submit` still has no production caller.** A drawn path is added
to, undone, and rendered, but nothing submits it as a `MoveAlongPath` order. That
was not among the six things task 71's brief listed, so this is a scoping miss in
the brief rather than a failure by the agent. Submission needs an addressee set
from the multi-select state and a real target tick, neither of which exists
outside a placeholder yet. It is task 73 below.

**Task 71 did not commit its own work.** It applied a general "only commit when
explicitly asked" rule over its brief's explicit instruction to commit, and left
five modified files uncommitted in its worktree. The integrating thread verified
the tree and committed it. Worth knowing because the work was complete and a less
careful check would have read the clean-looking report and merged nothing.

#### Design section 11's order-path overlay row is now stale

That row still reads "**scaffolded** — renders a path when one exists, has no
editor". After task 71 there is an editor and the overlay renders live drawn
nodes. Task 71 was scoped to add two rows and deliberately not to restatus an
existing one, so the row was left alone rather than changed outside its remit. It
needs a one-row correction, and that is folded into task 73.

#### Provisional constants this wave introduced

All marked at their site, none presented as measured or derived:

- `IntentSelection.SuppressionRepositionThreshold = 3` (task 44), plus the entire
  intent trigger cascade discussed above.
- `GoCodePanel`'s margin, width, header height, row height, and maximum visible
  row count, and the same five for `OrderQueueView` (task 62). Task 62's
  top-right and bottom-right anchors were explicitly offered to task 71 to
  override, and task 71 placed both in the left column instead.
- `SandataGame.PlaceholderOrderTargetTick = 0` and
  `PlaceholderOrderFactionId = 0` (task 71), standing in for a mission tick and
  a faction selector no system supplies yet.
- Two test-only counts in `HudComposerTests`, following that file's existing
  operator-count and contact-count convention.

Task 60 and task 61 introduced no provisional constants at all.

#### Report arithmetic drifted in three of six reports, and file sets did not

Task 60 claimed a 949-test baseline that never existed. Task 62 claimed 22 new
facts against a 163 baseline; the file holds 16 `[Fact]` and no `[Theory]`, and
169 + 16 = 185 closed exactly. Task 71's own numbers were right. In every case
the file set, the pass or fail, and the shape of the work were accurate, and only
the counts were wrong.

This now matches wave 7's finding about task 69's line count and wave 8's about
task 58's. The rule that follows is narrow and cheap: quote `git diff --stat`
against the merge base and the test runner's own totals, never a report's
figures, and re-derive every count from the merged tree.

#### Three tasks this wave created

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 72 | 10 | Close `OrderQueue`'s second bypassable door | `OrderQueue.Orders` is `public ... { get; init; }` on a record, so `queue with { Orders = ... }` injects orders and skips `SubmitValidated` entirely. Narrow it so submission has exactly one door, while giving snapshot resume an explicit, named restore path that does not revalidate — a snapshot's orders were validated when submitted, and revalidating them would violate design section 16's rule that an authored polyline is never recomputed. Migrate the three tests that build queues through the setter. | `src/Sandata.Core/Orders/OrderQueue.cs`, `src/Sandata.Core/Simulation/MissionSnapshot.cs`, `tests/Sandata.Core.Tests/OrderQueueTests.cs` | A test asserts that no public member other than `SubmitValidated` and the named restore path can add an order to a queue, by reflection over the type's writable surface rather than by inspection. The restore path is documented as resume-only and a test proves it does not revalidate. Every existing `OrderQueueTests` and `OrderStateHashTests` fact still passes. | 58, 61 | |
| 73 | 10 | Submit a drawn path, and correct the overlay's design row | `PathDrawTool.Submit` has no production caller: a path is drawn, undone, and rendered, but never submitted. Wire submission from the multi-select state's addressee set, through `OrderQueue.SubmitValidated`, and surface the rejection in `OrderQueueView`. Correct design section 11's order-path overlay row, which still reads "has no editor". | `src/Sandata.Client/SandataGame.cs`, `tests/Sandata.Client.Tests/HudComposerTests.cs`, `docs/plans/2026-08-07-sandata-scaffold-design.md` (section 11's element table only) | A test proves a drawn path with a non-empty selection submits exactly one `MoveAlongPath` order with ascending addressees, and that a rejected submission's reason code reaches the order queue view. A test proves an empty selection submits nothing rather than an order with no addressees. Design section 11's order-path overlay row no longer claims there is no editor. | 62, 71 | |
| 74 | 10 | Wire the four ruleset constants, and close two design deviations | Not one of `PathLatencyTicks`, `GroupCohesionRadius`, `LoweredWallDistanceWu`, or `AimToleranceBam` is read by production code. Pass the ruleset's values to `PathService` and `WeaponLoweredRules` rather than to constructor parameters callers fill arbitrarily. Implement the two rules the design states and the code does not: design section 8's cohesion-radius union in `SquadGrouping`, and design section 9's `|ShortestArc| <= AimToleranceBam` comparison feeding `WeaponChain.Advance`'s `arcWithinTolerance`. | `src/Sandata.Core/Squads/SquadGrouping.cs`, `src/Sandata.Core/Weapons/WeaponChain.cs`, `src/Sandata.Core/Navigation/PathService.cs`, `src/Sandata.Core/Combat/WeaponLoweredRules.cs`, and the corresponding test files | For each of the four constants, a test fails when the ruleset's value changes and the behaviour does not follow it — which is what makes `ChangingAnySingleField_MovesTheContentHash` meaningful rather than a hash over dead weight. A squad-grouping test pins that two operators just inside `GroupCohesionRadius` union and two just outside do not. A weapon-chain test pins that the transition fires at exactly `AimToleranceBam` and not one raw unit beyond. | 63 | |

Task 74 is the one to schedule deliberately rather than opportunistically. It is
the first task in this project that will change simulation behaviour on purpose,
and it touches four subsystems that currently agree with each other only because
none of them consults the value that is supposed to govern them.

### The wave-10 audit, run before dispatch — 2026-08-08

Wave 10 is tasks 49, 50, 72, 73, and 74. Both directions of the audit were run
over their rows before any agent was dispatched. The file-level check passed: no
file is claimed by two tasks. It then found three things a file-level check
cannot see, and two of them are decisions rather than observations, so they are
recorded here at the time they were taken rather than after the wave.

#### Task 74 is re-scoped: two of its four constants are not defects

Task 74's row, written during wave 9, says to wire all four ruleset constants
into the code that should consume them. Reading the code first showed that two of
the four are already correct by design.

`PathService` does not read `SandataRuleset` on purpose, and the reason is
written at the site: the latency "is a ruleset constant in the caller's
possession... so it stays usable in a test or a tool that has no ruleset to
hand." `WeaponLoweredRules` has the same shape for `LoweredWallDistanceWu`. Both
take their value as a parameter, which is this codebase's established convention
for handing a ruleset constant to a `Sandata.Core` type.

So for those two the gap is not in the consumer. It is that **no caller passes
the ruleset's value**, and the caller is the tick pipeline. That half moves to
task 49's call-site obligations, and task 74 was explicitly forbidden both files.

What remains in task 74 is the two genuine deviations, where the design states a
rule and no code implements it at all: design section 8's cohesion-radius union
in `SquadGrouping`, and design section 9's `|ShortestArc| <= AimToleranceBam`
comparison feeding `WeaponChain.Advance`.

This is worth recording as a general caution. Wave 9 established that all four
constants were unread, which was true. The inference that all four were therefore
defects was not, and it survived into a task row. **A finding and its remedy are
separate claims, and the remedy needs its own reading of the code.**

#### Task 49 cannot run beside tasks 72 and 74

Their file sets are disjoint, so the file-level audit passed them for the same
wave. They still cannot run in parallel.

Task 49 is the first production caller of the order queue, of squad grouping, and
of the weapon chain. Task 72 changes the order queue's writable surface and task
74 changes both of the other two. Whichever merged second would carry a call site
written against a surface that had moved underneath it — a conflict created on
purpose, exactly what the disjointness rule exists to prevent, and invisible to
that rule because no file is shared.

Wave 10 therefore runs as two batches: 50, 72, 73, and 74 together, then task 49
alone against the merged result. This is the third wave in a row to need a
second batch, and the reason differs each time. Wave 7's was the eight-agent
ceiling, wave 9's was a missing dependency, and wave 10's is an API surface two
tasks move and a third consumes.

**The audit question that catches this one is not "which files does each task
own" but "which surfaces does each task move, and who calls them".**

#### Task 50 could not reach its own acceptance criterion

Its row says the benchmark is exposed "behind headless flags" and its file list
names no `Program.cs`. `Sandata.Headless/Program.cs` parses only `--help` and the
log flags today, so as scoped the benchmark would have been unreachable from any
command line and its "runs to completion and prints all six percentiles"
criterion unsatisfiable.

`Program.cs` was granted to task 50 for its navigation-benchmark flags only. Task
51 still owns the determinism workload flags next wave, and task 50 was told not
to add them and not to "fix" the known `Unsupported argument '--agents'` failure,
which remains expected rather than a defect.

### Wave 10 complete, 2026-08-08

Six tasks — 49, 50, 72, 73, 74, and the new 75 — all merged into
`sandata-wave10`, each from its own worktree, with no merge conflicts. The wave
ran as the two batches the pre-dispatch audit required: 50, 72, 73, and 74
together, then task 49 alone against the merged result. Task 75 was created and
finished inside the wave, after task 50's benchmark work surfaced a crash on the
project's own map fixture.

This record was reconstructed after the session that wrote it. Git Bash failed on
every command, including a bare `true`, with
``/usr/bin/bash: -c: line 77: unexpected EOF while looking for matching `'``,
so the record could not be appended or committed, and the prepared text was lost
when the scratchpad holding it was swept. Everything below was re-verified
against the merged tree rather than restated from a report: the counts come from
a fresh run of the supported entry point, the gate output from a fresh run of the
gate, and the code claims from the files named beside them.

Counts through the supported entry point, re-run on the merged branch:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 1042
     Passed: 1042
Total tests: 195
     Passed: 195
[PASS] Release repository tests completed.
```

The Sandata core suite moved from 985 to 1042 and the client suite from 192 to
195. The shape of the change, from `git diff --stat` against the merge base
`40e5b59`, is 22 files, 5,263 insertions and 37 deletions, of which
`SandataSimulation.cs` is 1,292 new lines and `TickPipelineTests.cs` is 873.

The canonical gate was run by the integrating thread after integration, not
delegated:

```
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Total tests: 2376
     Passed: 2376
Total tests: 3131
     Passed: 3131
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Still byte-identical to untouched `main` in what it produces. Ten waves of a
second game have now moved no Hukbo hash.

#### A stage that runs is not a stage that works

Sandata now has the fourteen-stage tick pipeline design section 5 specifies, in
`src/Sandata.Core/Simulation/SandataSimulation.cs`, building warning-clean. Five
of those stages are honestly degenerate, and task 49's author marked every one of
them at the site as well as in the report. That marking is the reason they are
recorded here as known gaps rather than discovered later as bugs.

- **Stage 7 never calls `PathService.RequestPath`.** No destination-request
  source exists anywhere in the pipeline, so no group ever holds a live path.
  The only production call to `RequestPath` in the repository is in
  `src/Sandata.Headless/NavBenchmark.cs`, which is a measurement harness.
- **Stage 9's autonomous branch holds position**, for the same missing input.
  Formation collapse is therefore structurally unreachable rather than absent —
  the code implementing it is right and nothing feeds it.
- **Stage 11 hardcodes a weapon.** `SandataSimulation.cs:284` declares
  `private const FirearmId DefaultFirearmId = FirearmId.Ak47`, because
  `OperatorState` carries no loadout field.
- **Stage 12 does not resolve fire.** Every shot hits, damage is an invented flat
  `ProvisionalDamagePerHitPoints = 25` (`SandataSimulation.cs:292`, applied at
  `:599`), cover is always `CoverState.NotInCover`, and the result of
  `AccuracyRules.DrawAngularErrorBam` is discarded outright.
- **Stage 14 emits no events.** The state hash is real and computed on the
  documented cadence. The event half is unimplemented because `Sandata.Core`
  declares no event type at all.

Do not read the pipeline's existence as the game being playable.

#### Task 74 did not close what it appeared to close

This is the wave's most consequential finding, and it is a finding about how
acceptance criteria were written rather than about the agent that satisfied them.

Task 74 added a cohesion-radius gate to `SquadGrouping.Compute`, with boundary
tests on both sides of the radius and a test proving that changing the radius
changes the grouping. It also removed the ungated legacy overload, so the gate
cannot be bypassed. Every acceptance criterion in its row passed. Task 49c then
tested the same constant *through the pipeline* and proved it does nothing. Two
causes compound:

- **A unit mismatch.** `SandataRuleset.GroupCohesionRadius` is documented "in
  world units". `SquadGrouping.Compute`'s parameter is named
  `groupCohesionRadiusRaw` and is treated as raw fixed-point.
  `SandataSimulation.cs:1063` passes the first into the second, so a default of
  96 world units behaves as roughly 0.094.
- **The gate sits downstream of the decision it is supposed to make.**
  `view.Pairs` comes from `SandataCollisionGrid.Rebuild(bodies, bodyRadiusRaw)`,
  already filtered to physical contact. A downstream gate can only narrow a
  candidate list, never widen it. Even with the units fixed, two operators fifty
  world units apart never reach the comparison, because they were never
  candidates.

Task 74's tests passed because their fixtures supplied the candidate pair list
directly instead of reaching it through the collision grid. **A criterion a
fixture can satisfy without exercising the production call chain is not a
criterion.** The previous session's integrating thread wrote those criteria and
approved that test shape, so this is a defect in the row, not in the work. Task
77 below fixes both halves in one change.

#### Score on wave 9's "all four ruleset constants are read by nothing"

Half closed, not closed.

- `AimToleranceBam` — **proven load-bearing** through the pipeline.
- `LoweredWallDistanceWu` — **proven load-bearing**, and inclusive at the
  threshold.
- `PathLatencyTicks` — **blocked, and correctly reported as blocked.** Nothing
  calls `RequestPath`, so there is nothing to observe. The test written instead
  proves inertness, and deliberately compares full record equality rather than
  the state hash, because `SandataRuleset.ContentHash` folds `PathLatencyTicks`
  and a hash comparison would have diverged for the wrong reason and looked like
  success.
- `GroupCohesionRadius` — **proven not to work**, as above.

`SandataRuleset.ContentHash` did not move in this wave and stays
`8_955_292_433_887_190_872`.

#### `Sandata.Core` has no event type at all

This blocks three separate things that are each recorded as built or specified:
design section 5's stage 14, design section 16's order-rejection event, and
design section 11's event log. It is task 76 below, and it is the first row of
wave 11 because two other rows want a destination for what they observe.

#### Two identifier narrowings survived task 64's widening pass

Both are at subsystem boundaries, both are bridged with `unchecked((int)...)`,
and both are inert today only because no group holds a path and no shot resolves:

- `SandataSimulation.cs:1129` — `SquadSlot.GroupId` into `PathService`.
- `SandataSimulation.cs:596` — `OperatorState.EntityId` into
  `AccuracyRules.DrawAngularErrorBam`.

A source scan for `unchecked((int)` over `src/` returns exactly these two.
Task 64 widened the identifiers themselves and did not reach every consumer.
Task 78 below closes them and pins the absence by scan rather than by
inspection.

#### A crash latent since task 20, found by a benchmark and fixed by task 75

`GridRay.Traverse` threw whenever a ray's origin cell lay outside the grid, and
`tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap` authors its perimeter walls
exactly on the map edge, so `WallBuckets.Build` threw on the project's own
fixture — the same call `Sandata.Client.SandataGame` makes at startup. Task 75
fixed it by clamping only the broad-phase traversal through a new
`ClampToInterior` helper, leaving `GridRay`'s guard intact and the exact narrow
phase in `LineOfSight` receiving true unclamped coordinates. That follows
`OrderValidation`'s established precedent in this codebase rather than inventing
a second convention.

The row, recorded here because the task was created after the wave was
dispatched and never had one:

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 75 | 10 | Boundary walls crash the nav index | `WallBuckets.Build` throws `ArgumentOutOfRangeException` for any wall endpoint on the map's outer boundary, because that world-unit value floors to the cell one past the last row or column and `GridRay.Traverse` rejects an out-of-bounds origin. `angle-house.hkmap`'s four perimeter walls all touch a boundary, so building the nav index from real fixture data always failed. Clamp the broad phase only; the stored segments keep their true coordinates so the exact narrow phase is unaffected. | `src/Sandata.Core/Navigation/WallBuckets.cs`, `tests/Sandata.Core.Tests/WallBucketsTests.cs` | A test builds the index from `angle-house.hkmap` without throwing. A test proves a segment with both endpoints outside the grid is still stored unclamped and still classified exactly. | — | Merged as `3a59fac`; 215 new lines of tests in `WallBucketsTests.cs`. |

#### What this wave cost, and the five habits that paid for themselves

Task 49 stalled seven times on the 600-second watchdog. Every stall was in a read
phase and none was while writing. A single grep producing a 121-line
`path:line:declaration` index of the subsystems it had to call, handed over as a
scratch file the agent deletes before committing, unblocked it every time.
**For any task that calls many subsystems, supply the call surface at dispatch
rather than making the agent discover it.**

**Split coarse rows.** Task 49 cost eight agent runs as one row and completed as
three — pipeline scaffold and stages, then stage implementation, then the tests.
The granularity rule is not satisfied by a row that fits in a table; it is
satisfied by a row an agent can finish.

**Tell agents to commit as they go.** Long tasks survive stalls only if partial
work is already committed. This saved real work twice this wave.

**Audit both directions before dispatching, not after.** No file claimed twice,
*and* every step named in a "What" column claimed exactly once. The file-level
half is easy and catches little: it passed tasks 49, 72, and 74 for the same
batch, and the surface-level half is what caught that task 49 is the first
caller of both surfaces the other two move. It also caught that task 50 had been
given no `Program.cs` and so could not reach its own acceptance criterion.

**Verify every report against disk.** Reports get the file set and the pass or
fail right and the counts wrong, consistently — five instances now across waves 7
to 10. Quote `git diff --stat` against the merge base and the runner's own
totals, never a report's figures.

**Watch for bypassable call sites, not only missing ones.** Three have now been
found in three consecutive waves: `OrderQueue.Submit` left public beside
`SubmitValidated`, `OrderQueue.Orders` with a public `init` on a record (closed
by task 72 this wave), and a legacy `SquadGrouping.Compute` overload with the
radius disabled (removed by task 74). All three passed every acceptance
criterion they were given.

#### Four tasks this wave created

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 76 | 11 | An authoritative event feed for `Sandata.Core` | `Sandata.Core` declares no event type, which blocks design section 5's stage 14, design section 16's order-rejection event, and design section 11's event log. Declare the event record and the ordered feed, retain at most 200 as CLAUDE.md section 5 requires of Hukbo, and fold the feed into the state hash after every field the hasher already covers. Give stage 14 and `OrderQueue`'s rejection path a real destination. | `src/Sandata.Core/Events/` (new), `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 14 only), `src/Sandata.Core/Orders/OrderQueue.cs` (rejection emission only), and the corresponding test files | A test pins the pre-change state hash of a mission with an empty feed, proving the fold was appended and not interleaved. A test proves the feed retains exactly 200 and drops the oldest. A test proves a rejected order emits exactly one event carrying its reason code. Stage 14 produces an event hash that changes when the ordered event stream changes and not otherwise. | 49, 72 | |
| 77 | 11 | Make the cohesion radius govern grouping where candidates are formed | `SandataRuleset.GroupCohesionRadius` is documented in world units and passed into `SquadGrouping.Compute`'s raw fixed-point `groupCohesionRadiusRaw` at `SandataSimulation.cs:1063`, so 96 world units behaves as roughly 0.094. The gate is also downstream of `SandataCollisionGrid.Rebuild`, which has already filtered to physical contact, so it can only narrow a candidate list it should be widening. Move the decision to the candidate source or give grouping its own candidate query, and resolve the unit mismatch in the same change by putting the unit in the field's name as `LoweredWallDistanceWu` already does. | `src/Sandata.Core/Squads/SquadGrouping.cs`, `src/Sandata.Core/Simulation/SandataRuleset.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 10 call site only), `tests/Sandata.Core.Tests/SquadGroupingTests.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs` | Task 49c's `RunTick_TwoSameFactionOperatorsFiftyWorldUnitsApart_AreNotGroupedDespiteDocumentedRadius` is inverted and passes — reached through `RunTick`, never through a hand-supplied pair list. A test proves two operators just outside the radius are not grouped, also through `RunTick`. `SandataRuleset.ContentHash` moves, and its new value is recorded here with the reason. | 74 | |
| 78 | 11 | Widen the two remaining identifier narrowings | `SquadSlot.GroupId` into `PathService` (`SandataSimulation.cs:1129`) and `OperatorState.EntityId` into `AccuracyRules.DrawAngularErrorBam` (`SandataSimulation.cs:596`) are both bridged with `unchecked((int)...)`. Task 64's widening pass did not reach these consumers. Widen the consumers rather than the call sites. | `src/Sandata.Core/Navigation/PathService.cs`, `src/Sandata.Core/Combat/AccuracyRules.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` (the two call sites only), `src/Sandata.Headless/NavBenchmark.cs`, and the corresponding test files | A test asserts by source scan over `src/` that no `unchecked((int)` cast of an entity or group identifier remains, rather than asserting it by inspection. Every existing `PathService` and `AccuracyRules` fact still passes. No hash moves. | 49 | |
| 79 | 11 | Give stage 7 a destination source and stage 12 a hit test | Stage 7 never calls `PathService.RequestPath` because nothing requests a destination, which also holds stage 9's autonomous branch at position. Stage 12 resolves no fire: every shot hits, damage is a flat provisional 25, cover is always `NotInCover`, and `AccuracyRules.DrawAngularErrorBam`'s result is discarded. **This row must be split before dispatch — it is at least three tasks, and is written as one only so the shared cause stays visible.** | To be assigned per split row | Per split row. Stage 7's part is done when a group with an issued `MoveAlongPath` order holds a live path within `PathLatencyTicks`, proven through `RunTick`, which also unblocks wave 9's fourth constant. Stage 12's part is done when the drawn angular error decides the outcome and a miss is observable. | 76, 77 | |

Task 76 and task 78 are disjoint and can run together. **Task 77 must not run
beside anything that calls squad grouping.** Task 79's split parts depend on 77
for the grouping surface and on 76 for somewhere to emit what they resolve.

### The wave-11 audit, run before dispatch — 2026-08-08

Wave 11 is tasks 76, 77, 78, and 51, plus the split of task 79 into four rows
that this section writes. Both directions of the audit were run over those rows
before any agent was dispatched: no file is claimed by two tasks in the same
batch, and every step named in a "What" column is claimed exactly once. As in
wave 10, the file-level half passed immediately and the surface-level half is
what produced everything below.

Wave 10's own record was reconstructed and committed at the start of this
session before any wave-11 work began, because the shell failure at the end of
the previous session left it uncommitted. Wave 10 is now merged into `main`.

#### `SandataSimulation.cs` is this wave's single-writer bottleneck

Six of the rows in play — 76, 77, 78, and three of the four task 79 splits —
each need a different region of `src/Sandata.Core/Simulation/SandataSimulation.cs`.
The regions are genuinely disjoint: stage 14 for 76, the stage 6 call at line
1063 for 77, the two casts at lines 596 and 1129 for 78, and stages 7 and 9 for
the 79 splits. Disjoint regions inside one file are still one file, and this plan
has said since its first page that two agents in one file is a merge conflict
created on purpose.

So the wave serialises on that file rather than pretending the regions make it
safe:

- **Batch 1:** task 76 alone. It is the longest row and everything downstream
  wants a place to emit events.
- **Batch 2:** task 77 and task 51 together. Their file sets are disjoint and
  neither moves a surface the other consumes, subject to the constraint on 51
  recorded below.
- **Batch 3:** task 78 alone.
- **Task 79's splits** follow in wave 12, in the dependency order given at the
  end of this section.

#### Task 76's row asks for the wrong fold, and the design says so

The row written at the end of wave 10 says to fold the event feed "into the
state hash after every field already covered". That is wrong, and it is worth
correcting here rather than letting an implementer discover it or, worse, satisfy
it.

Design section 4 declares two hashes and says why they are two: "They are
independent on purpose: a bug that moves state without emitting an event moves
one and not the other." Folding the event stream into the state hash destroys
exactly the property the second hash exists to provide. Section 4's list of what
is authoritative and hashed does not contain the event feed, and it already
contains `NextEventSequence`, which `SandataStateHasher` folds today at line 151.

The corrected obligation for task 76:

- The **event hash** is FNV-1a over the ordered event stream, accumulated as
  events are emitted, so that the 200-event retention cap cannot truncate it. A
  bounded feed and a complete hash are different things and the cap belongs only
  to the feed.
- The **state hash** gains no new field. Emitting an event still moves it,
  through the `NextEventSequence` increment it already folds, and that is the
  designed coupling rather than a new one.
- The running event-hash accumulator and the event sequence are authoritative and
  belong in `MissionSnapshot`, because resume has to reproduce the event hash and
  cannot replay a truncated feed to get it.

This is the same shape as the caution wave 10 recorded about the four ruleset
constants: the finding that `Sandata.Core` has no event type was right, and the
remedy attached to it was not, and the remedy needed its own reading of the
design.

#### Task 77 cannot reach its criterion with the files its row names

Two corrections. First, the row says "stage 10 call site only". Squad grouping is
**stage 6**; `ComputeSquadGrouping` sits at `SandataSimulation.cs:1045` and calls
`SquadGrouping.Compute` at line 1063. Stage 10 is movement commit.

Second, and this is the one that would have cost a run: the row grants
`SquadGrouping.cs`, `SandataRuleset.cs`, the call site, and two test files. The
whole finding wave 10 recorded is that the gate cannot work where it currently
sits, because `TickStartView.Pairs` is filled from
`SandataCollisionGrid.Rebuild(bodies, bodyRadiusRaw)` and is already narrowed to
physical contact. Fixing that means changing where candidate pairs come from,
which is `src/Sandata.Core/Collision/SandataCollisionGrid.cs` and
`src/Sandata.Core/Simulation/TickStartView.cs`. Neither is in the row. As
written, the only change reachable inside the granted file set is the unit
conversion, which would leave the behaviour exactly as broken and every stated
criterion still passing.

Both files are therefore granted to task 77. This is the same failure the
wave-10 audit caught when task 50 was given no `Program.cs`, and it is the second
consecutive wave in which the pre-dispatch read of a row's file list against its
own acceptance criterion was what caught it.

#### Task 51 may not pin a literal Sandata hash this wave

Task 51 and task 77 run in the same batch and share no file. They do share a
value: task 77 renames a `SandataRuleset` field and changes grouping behaviour,
so `SandataRuleset.ContentHash` moves and every mission state hash moves with it.
A determinism runner that recorded a literal expected hash would be recording a
number that stops being true the moment the other agent in its own batch merges.

Task 51's assertions are therefore self-consistency assertions — two runs of the
same seed agree, a resumed run agrees with an uninterrupted one, the documented
exit codes fire on the documented conditions — and not literal expected values.
Golden values belong to task 52, which runs after both.

#### Task 79, split into four rows

The row created in wave 10 said it must be split before dispatch, and this is the
split. The shared cause is that stage 7 has no destination source, which is also
what holds stage 9's autonomous branch at position; stage 12's fire resolution is
a separate defect that was written into the same row only to keep the two
visible together.

One decision is deliberately kept out of these rows. **What an autonomous squad
wants — how a destination is chosen — is undesigned**, in the same way and for
the same reason that intent selection was undesigned when task 44 invented it.
Task 79a below wires the machinery that serves a destination request and
deliberately does not decide what issues one. That decision is listed among the
open questions the user has not answered, and an implementer must not settle it.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 79a | 12 | Serve an outstanding group path request | `MissionState.Groups` already carries `GroupPathState` with `DestinationCellIndex`, `HasOutstandingRequest`, `StartCellIndex`, `GoalCellIndex`, and `RequestTick`, and `SandataStateHasher` already folds all five. Nothing reads them: `AdvancePathService` calls `PathService.Advance` and never `RequestPath`. Wire stage 7 to issue a request for every group holding an outstanding one, publish the result at `RequestTick + PathLatencyTicks`, and clear the request when it publishes. **Do not decide what sets a destination** — the policy is an open question and this row is the mechanism only. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 7 only), `tests/Sandata.Core.Tests/TickPipelineTests.cs` | A test drives a group destination into `MissionState` directly, runs `RunTick` `PathLatencyTicks` times, and proves the path is unavailable before the latency elapses and available on the exact tick it does — which is wave 9's fourth ruleset constant finally observable. A test proves a request is cleared once published and not re-issued. | 78 | |
| 79b | 12 | Autonomous movement from a published path | Stage 9's autonomous branch holds position because no group had a path. With 79a it can have one. Sample the group's published polyline by arclength through `SlotTargets.ComputeTarget`, apply `FormationCollapse`'s half-width gate against the clearance field, and produce a real proposal for an unassigned operator. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 9 autonomous branch only), `tests/Sandata.Core.Tests/TickPipelineTests.cs` | A test proves an unassigned operator in a group with a published path moves along it, and a test proves a group whose leader stands in a cell whose clearance is below the formation half-width collapses to single file — design section 8's doorway behaviour, structurally unreachable until now. | 79a | |
| 79c | 12 | Give an operator a loadout | Stage 11 hardcodes `private const FirearmId DefaultFirearmId = FirearmId.Ak47` at `SandataSimulation.cs:284` because `OperatorState` carries no loadout field. Add the field, fold it into the state hash after every field the hasher already covers, carry it in the snapshot, and read it at stage 11. | `src/Sandata.Core/Simulation/MissionState.cs`, `src/Sandata.Core/Determinism/SandataStateHasher.cs`, `src/Sandata.Core/Simulation/MissionSnapshot.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 11 only), and the corresponding test files | A test pins the pre-change state hash of a mission whose operators all carry the previous default, proving the fold was appended rather than interleaved. A test proves two operators with different loadouts advance different weapon chains. | 76 | |
| 79d | 12 | Resolve fire for real | Stage 12 resolves nothing: every shot hits, damage is a flat `ProvisionalDamagePerHitPoints = 25`, cover is always `CoverState.NotInCover`, and `AccuracyRules.DrawAngularErrorBam`'s result is discarded. Make the drawn angular error decide the outcome against the target's subtended angle, take damage from the firearm the operator carries, and evaluate `CoverRules` at the shooter-to-target arc. Emit the shot and its outcome as events. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 12 only), `src/Sandata.Core/Combat/DamageResolution.cs`, and the corresponding test files | A test proves the same shot hits at one drawn error and misses at another, with no other input changed. A test proves a target in cover takes the cover-modified value and not the flat provisional one. A miss and a hit each emit exactly one event, observable in the feed. | 76, 79c | |

Task 79a and 79c are disjoint and can run together. 79b depends on 79a, 79d
depends on 79c, and both want task 76's feed to emit into.

### Wave 11 complete, 2026-08-08

Four tasks — 76, 77, 78, and the long-outstanding 51 — all merged into `main`,
each from its own worktree, with no merge conflicts. The wave ran as the three
batches the pre-dispatch audit required, because `SandataSimulation.cs` was a
single-writer bottleneck: task 76 alone, then 77 beside 51, then 78. Task 79 was
split into four rows before dispatch and none of them ran; they are wave 12.

Counts through the supported entry point, run by the integrating thread on
merged `main`:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 1066
     Passed: 1066
Total tests: 195
     Passed: 195
[PASS] Release repository tests completed.
```

The Sandata core suite moved from 1042 to 1066 and the client suite stayed at
195, which is right — no task this wave touched `Sandata.Client`. The arithmetic
closes exactly, and each figure was re-derived from the merged tree rather than
taken from a report: task 76 added 7 (1049), task 77 added 4 (1053), task 51
added 9 (1062), task 78 added 4 (1066).

The canonical gate, run by the integrating thread after integration:

```
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Total tests: 2376
     Passed: 2376
Total tests: 3131
     Passed: 3131
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Eleven waves of a second game have moved no Hukbo hash.

**The gate's totals did not change this wave, and that is not a mistake.** The
default gate runs the Hukbo projects only, exactly as design section 14 says it
will until Sandata has a recorded baseline. Sandata's 1,066 core tests are not in
it. Anyone reading `[PASS] Canonical repository verification completed.` as
covering Sandata is reading it wrong; `-Game Sandata` is a separate run and it
was made separately above.

#### Sandata ran its own determinism workload for the first time

Task 51's runner exists, and this is its real output on merged `main`:

```
./scripts/benchmark.ps1 -Game Sandata -Seed 1
  "seed": 1,
  "operatorsPerFaction": 100,
  "measuredTicks": 10000,
  "tickPercentiles": {
    "p50Milliseconds": 4.0214,
    "p95Milliseconds": 6.0804,
    "p99Milliseconds": 7.5789,
    "maximumMilliseconds": 59.6075
  },
  "allocatedBytes": 65679126648,
  "outcome": "Ongoing",
  "eventHash": "CBF29CE484222325",
  "stateHash": "00EC034D18941D36",
  "deterministic": true,
  "firstMismatchTick": null
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
```

Neither hash is pinned anywhere and neither should be until task 52 records a
golden baseline; task 51 was explicitly forbidden a literal, because task 77 ran
in its own batch and could have moved every Sandata hash underneath it.

Two numbers in that report are findings rather than results.

**The event hash is `CBF29CE484222325`, which is the FNV-1a offset basis.** Not
one event was emitted across ten thousand ticks. That is consistent — task 76
wired exactly one producer, the order-rejection path, and this workload submits
no orders — but it means the event half of the determinism contract is asserted
by construction and not yet by a run. Task 79d is what will first exercise it.

**The workload allocated 65,679,126,648 bytes over 10,000 ticks**, about 6.5 MB
per tick at 200 operators. Stage 3 now constructs two collision grids per tick
and several stages allocate their own arrays. `SIMULATION-GAME-STANDARDS.md`'s
per-tick allocation budget exists for exactly this, and nothing in Sandata has
ever been measured against it before, because until this wave there was no
runner to measure with. It is task 81 below.

#### Task 76 built the feed; the corrected fold was the right call

The audit's correction held up in implementation. `MissionEventFeed` retains at
most 200 events and carries a `Hash` that accumulates over the full stream, so
the cap cannot truncate it; `SandataStateHasher` was not touched at all, which
was verified as an empty diff rather than asserted. The feed's `Events` property
is `{ get; private init; }`, so `with { Events = ... }` cannot inject an event —
the lesson from task 72's bypassable door applied without being asked for.

`PreTask76BaselineHash` came out equal to `PreTask61BaselineHash`
(`5_550_901_129_500_655_850`), which is exactly right and is itself the proof:
adding the feed moved no state hash, because the feed is not in it.

#### The fourth bypassable door, found while verifying task 76

`SandataSimulation.SubmitOrder` is the door that emits the rejection event. It is
not the door the client uses. `src/Sandata.Client/SandataGame.cs:649` and
`src/Sandata.Client/UI/PathDrawTool.cs:182` both call `OrderQueue.SubmitValidated`
directly, so on the real client path a rejected order still vanishes with no
event — design section 16's "it is not silently dropped" is satisfied in
`Sandata.Core` and false in the game.

This is not task 76's failure: `Sandata.Client` was forbidden to it, correctly,
and the client does not construct a `SandataSimulation` at all yet, so there is
currently no simulation door for it to call. It is task 80 below.

That makes four consecutive waves in which a validating entry point shipped
beside an open one — `OrderQueue.Submit`, `OrderQueue.Orders`, the ungated
`SquadGrouping.Compute` overload, and now `SubmitValidated` reachable from
presentation code. The check keeps paying for itself: whenever a task adds a
door that does something extra, ask who else can reach the same state without it.

#### Task 77 corrected its own brief, and was right to

The brief said this task would move `SandataRuleset.ContentHash`, because it
renames a field. It does not. `SandataHash.Fold` folds by value and not by
property name, and the stored value of 96 did not change, so the pinned literal
`8_955_292_433_887_190_872` is identical before and after. The agent verified
that by running the pinned test rather than inventing a replacement number, and
reported the discrepancy against its own instructions.

The instruction was written by the integrating thread and it was wrong. This is
the same shape as wave 10's caution about the four ruleset constants, pointed the
other way: **a remedy stated in a brief is a claim, and it needs its own reading
of the code before an agent is told to satisfy it.** An agent that had obeyed the
brief instead of the code would have re-pinned a hash that never moved.

The fix itself moves the decision to where candidates are formed, as the audit
required. A second `SandataCollisionGrid`, sized from the cohesion radius, is
built at stage 3 and queried by the new `RebuildWithinRange`; stage 6 reads it
through `TickStartView.CohesionPairs`, beside the untouched physical-contact
`Pairs`. `ValidateRange` refuses a query wider than a cell, which is what keeps
the 3×3 neighbour scan complete at any radius — the hazard that would otherwise
have silently dropped pairs, since the physical grid's provisional 256-raw cell
is four orders of magnitude smaller than the 98,304-raw cohesion radius.

Task 49c's `RunTick_TwoSameFactionOperatorsFiftyWorldUnitsApart_AreNotGroupedDespiteDocumentedRadius`
is inverted and passes through `RunTick`, and the boundary is inclusive at the
radius, matching `SandataCollisionGrid.IsContact`'s existing convention.

#### Task 78 proved the RNG stream did not move, rather than assuming it

Widening `AccuracyRules.DrawAngularErrorBam`'s entity id from `int` to `ulong`
changes what the hash folds, and that method feeds a deterministic draw. The task
captured three concrete draws from the pre-change code through a temporary
`ITestOutputHelper` fact, deleted it, and pinned the recorded values against the
widened method: `(12345, 7, 256) → -122`, `(999, 42, 171) → -147`, and
`(55, 3, 32767) → 30472`. A widening that had moved a stream would be a preset
version change, not a refactor, and this is the evidence that it was not.

`grep -rn 'unchecked((int)' src/` now returns nothing, and a source-scan fact,
`SandataSourceHygieneTests.SourceTreeNeverNarrowsAnIdentifierWithAnUncheckedIntCast`,
holds it that way.

Task 78 also hit `main` moving underneath it mid-run, when task 51 merged. It
correctly reported that its two-dot `git diff main..HEAD` had become misleading
and gave its merge base explicitly instead. That is the right reflex and it is
worth naming: when a wave runs in batches, a branch's two-dot diff against `main`
stops meaning what it looks like as soon as another batch lands.

#### Two tasks this wave created

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 80 | 12 | Route client order submission through the simulation door | `SandataGame.cs:649` and `UI/PathDrawTool.cs:182` call `OrderQueue.SubmitValidated` directly, so a rejection on the real client path emits no event and design section 16's "not silently dropped" is false outside `Sandata.Core`. The client also constructs no `SandataSimulation` at all, so the door it should be calling is not reachable yet. Give the client a simulation to submit into and route both call sites through `SandataSimulation.SubmitOrder`. | `src/Sandata.Client/SandataGame.cs`, `src/Sandata.Client/UI/PathDrawTool.cs`, `tests/Sandata.Client.Tests/` | A test proves a rejected client submission produces exactly one `OrderRejected` event in the simulation's feed, reached through the client's own submission path and not by calling `SubmitOrder` directly. A test asserts no `Sandata.Client` type calls `OrderQueue.SubmitValidated`, by source scan rather than inspection. | 76 | |
| 81 | 12 | Measure and cut Sandata's per-tick allocation | Task 51's first workload allocated 65,679,126,648 bytes over 10,000 ticks at 200 operators, roughly 6.5 MB per tick, against `SIMULATION-GAME-STANDARDS.md` section 11's per-tick allocation budget. Stage 3 constructs two `SandataCollisionGrid` instances per tick and several stages allocate per-tick arrays. Reuse the grids and the scratch buffers across ticks rather than reallocating, without introducing any cache that outlives a tick's meaning or reaches a hash. | `src/Sandata.Core/Simulation/SandataSimulation.cs`, `src/Sandata.Core/Collision/SandataCollisionGrid.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs` | The same seed-1 workload reports a materially lower `allocatedBytes`, with the before and after figures both recorded here. Every existing state hash and the workload's `deterministic: true` are unchanged, proving the reuse changed no outcome. A test proves a reused grid holds nothing from the previous tick. | 51 | |

Task 80 and task 81 are disjoint from each other. Task 81 shares
`SandataSimulation.cs` with every task-79 split, so it does not run beside them.

### The wave-12 audit, run before dispatch — 2026-08-08

Wave 12 is tasks 79a, 79b, 79c, 79d, 80, and 81, plus tasks 52, 53, 54, and 55,
which have been outstanding since the original plan. Both directions of the
audit were run over those rows before any agent was dispatched: no file is
claimed by two tasks in the same batch, and every step named in a "What" column
is claimed exactly once. For the third consecutive wave the file-level half
passed immediately and the surface-level half produced everything below — and
this time it produced more than in either of the two waves before it.

Two things the user authorised at the start of this session are recorded here so
they are decisions rather than drift. First, the client may load
`tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap` at startup and construct a
`SandataSimulation` from it, which folds into task 80's row below. Second, the
merged Sandata worktrees were swept: eleven of them — `sandata-t49-tickpipeline`,
`sandata-t50-navbench`, `sandata-t51-headless`, `sandata-t72-queuedoor`,
`sandata-t73-pathsubmit`, `sandata-t74-rulesetwiring`, `sandata-t75-wallboundary`,
`sandata-t76-events`, `sandata-t77-cohesion`, `sandata-t78-idwiden`, and
`sandata-wave10` — each verified clean (`git status --porcelain` empty) and
verified an ancestor of `main` (`git merge-base --is-ancestor`) before removal.
The branches themselves were kept. The two unregistered directories under
`.claude/worktrees/`, `hit-animations` and `rank-basecheck`, belong to another
session and were not touched.

#### `SandataSimulation.cs` is still the single-writer bottleneck

Wave 11 serialised on this file and wave 12 has to serialise on it harder,
because five of this wave's rows want five different regions of it. The regions
were confirmed by line number against `main` at `d1f6640` rather than taken from
the rows:

| Task | Region of `SandataSimulation.cs` |
| --- | --- |
| 79a | `AdvancePathService`, lines 1157 to 1161 — stage 7 |
| 79b | `ComputeMovementProposals`, lines 1278 to 1330 — stage 9 |
| 79c | `AdvanceWeaponChain`, lines 385 to 474, and the `DefaultFirearmId` constant at line 324 — stage 11 |
| 79d | `ProposeFire`, lines 590 to 645 — stage 12 |
| 81 | `RunTick`, lines 204 to 264, plus whichever stage measurement names |

Disjoint regions inside one file are still one file. Each of those five rows gets
a batch to itself. Task 80 touches `Sandata.Client` only, so it is the one row
that can run beside a Core task, and it runs beside the first.

#### Task 79d cannot reach three of its four clauses, and is split in two

Task 79d's row names four changes. Three of them are unreachable inside the files
the row grants, and each is unreachable for a different reason. This is the same
failure the wave-10 audit caught for task 50 and the wave-11 audit caught for
task 77, arriving three times in one row.

**Per-weapon damage has nowhere to come from.** `FirearmDefinition`
(`src/Sandata.Core/Weapons/FirearmDefinition.cs`, lines 92 to 123) declares
twenty-one fields and not one of them is damage. `ProposeFire`'s own remarks
already say so at line 328. Adding a damage field to that record means editing
all thirty-eight rows of `FirearmCatalog` and inventing thirty-eight tuning
numbers, which is a design decision smuggled in as an implementation detail.
Design section 10 already establishes the alternative for the audio catalog, in
words that apply here unchanged: "the caliber, not the weapon, keys the report.
Six report families cover the rifles... Eight families in total, not 38
weapons." Damage is therefore keyed on `CaliberFamily` in a new file under
`src/Sandata.Core/Combat/`, as eight values marked provisional at their
declaration, and `FirearmDefinition` and `FirearmCatalog` are not touched at all.

**Cover exists in the map format and never reaches the simulation.** `COVER` is
a real record kind — `MapRecordKind.Cover = 3`, parsed into `CoverRecord` at
`src/Sandata.Core/Maps/MapRecord.cs:63` — and the `angle-house` fixture carries
four of them. `CoverState` is a record struct carrying an arc centre, an arc
half-width, and a posture, and `CoverRules.IsWithinProtectedArc` already exists
to test a bearing against it. What does not exist is any route from the map to
the simulation: `SandataSimulation`'s constructor takes a `Mission`, a
`SandataRuleset`, a `NavGrid`, a `WallBuckets`, and a `MissionState`, and cover
is in none of them. Evaluating cover is one constructor parameter of work and
exactly zero lines of it are reachable inside a grant that reads "stage 12 only".

**There is one event kind.** `MissionEventKind` declares `OrderRejected = 0` and
nothing else, and `MissionEvent` exposes one factory. Emitting a shot and its
outcome means new members on both, in `src/Sandata.Core/Events/`, which the row
does not grant.

Three new surfaces in one row is what task 79 looked like before wave 11 split
it, so task 79d is split the same way. The two halves share stage 12 and would
have been serial regardless, so the split costs one extra batch and buys two
briefs an agent can actually finish.

#### Task 79c's acceptance criterion is arithmetically impossible

The row asks for "a test that pins the pre-change state hash of a mission whose
operators all carry the previous default, proving the fold was appended rather
than interleaved". No such test can exist. `SandataHash.Fold` is FNV-1a, and
folding one additional value changes the digest unconditionally — including when
that value equals the old hardcoded default, because the fold is over the value's
bytes and not over any notion of "the value that was already assumed". The
pre-change hash cannot survive an appended fold under any placement.

This is wave 11's task 77 lesson pointed the other way. There, a brief claimed a
hash would move and it did not; here, a row claims a hash will hold and it
cannot. Both are the same error: **a remedy stated in a brief is a claim, and it
needs its own reading of the code before an agent is told to satisfy it.**

The corrected obligation for task 79c:

- The new fold goes **last inside `FoldOperator`**, after the contact-memory
  block that currently ends at `SandataStateHasher.cs:343`. "After every field
  the hasher already covers" means after all of them, including the nested ones.
- A fresh `PreTask79cBaselineHash` constant is recorded, in the same shape as the
  existing `PreTask61BaselineHash` and `PreTask76BaselineHash`.
- The seed-1 workload's state hash moves off `00EC034D18941D36` **on purpose**,
  and recording the new value is the deliverable rather than a problem to be
  worked around. Nothing anywhere pins the old one.
- Fold position is proved by a test in which two mission states differing only in
  one operator's loadout hash differently, which is decisive where the pinned
  literal is not.

One grant in that row is spare and should not be spent: `MissionSnapshot` holds
an `ImmutableArray<OperatorState>` already, so a new field on `OperatorState` is
carried through the snapshot with no edit to `MissionSnapshot.cs` at all.

#### Task 79b needs a clearance field that nothing builds

`FormationCollapse.IsCollapsed` takes a leader clearance and a formation
half-width. `SandataSimulation` holds a `NavGrid` and a `WallBuckets` and no
clearance field, and `ClearanceField.Build` is never called anywhere in the
simulation. Baking one belongs in the constructor, which is outside a grant
reading "stage 9 autonomous branch only". Two smaller consequences fall out of
the same row: `ComputeMovementProposals` is `static` today and has to become an
instance method to reach `_pathService` at all, and the formation half-width is a
seventh provisional constant with no `SandataRuleset` field behind it, to be
marked at its declaration exactly as `CollisionCellSizeRaw` and
`VisionConeHalfWidthBam` already are.

#### Task 52 needs two golden baselines, and design section 16 says so

Task 52's row, written before the order layer was promoted, names "a pinned
seed-1 mission whose state hash and event hash are recorded as expected
constants" — one baseline. Design section 16 outranks it: "The golden replay
needs two baselines, not one: a mission with an empty order stream, which is the
pure autonomous case, and a mission with a recorded non-empty one. A single
empty-stream baseline would prove nothing about the subsystem this section adds."
Task 52 records both.

#### Task 81's stated cause is a claim, not a measurement

The row asserts that stage 3's two `SandataCollisionGrid` constructions are where
the 65,679,126,648 bytes go. That was a reasonable guess written at the end of
wave 11 and it does not survive a reading of the code. `SandataCollisionGrid`
already reuses its internal arrays across `Rebuild` calls and grows them on
demand (`_hashHeadSlot`, `_hashOccupied`, `_nextSlotInCell`,
`_occupiedCellHashIndex`, and the growth path at line 738), so what a fresh
instance costs each tick is four small arrays and not megabytes. The other named
suspect is smaller still: `AdvancePathService` allocates `new bool[_navGrid.CellCount]`
every tick, and the headless workload's grid is ten cells by ten, so that
allocation is one hundred bytes.

Roughly 3.28 MB per simulation-tick at two hundred operators has to be *measured*
before it is cut. Task 81 therefore produces a per-stage allocation table first,
from `GC.GetAllocatedBytesForCurrentThread()` deltas in a temporary harness it
deletes before committing, and cuts what the table names. If the table names a
file outside the grant, the task stops and reports rather than widening its own
scope.

#### Task 54's project count is stale

The row requires that "`CLAUDE.md` section 3's layout block lists all eleven
projects". `Hukbo.slnx` lists twelve: five under `src/` for Hukbo — `Hukbo.Core`,
`Hukbo.Client`, `Hukbo.Headless`, `Hukbo.Diagnostics`, `Hukbo.Shared.Core` —
three for Sandata, and four test projects. Twelve is the number to write.

#### Two policy decisions stay out of these rows

**What an autonomous squad wants — how a destination is chosen — is undesigned**,
in the same way and for the same reason that intent selection was undesigned when
task 44 invented it. Task 79a wires the machinery that serves a destination
request and deliberately does not decide what issues one.

**What decides an operator's loadout is equally undesigned.** Task 79c adds the
field and defaults it to the `FirearmId.Ak47` that stage 11 hardcodes today, so
behaviour is unchanged and the degeneracy stays honest. It does not invent a rule
that assigns a weapon to an operator.

An implementer must settle neither. Both are listed among the open questions the
user has not answered.

#### The batch plan

| Batch | Tasks | Why they can share it |
| --- | --- | --- |
| 1 | 79a and 80 | 80 is `Sandata.Client` only; 79a is stage 7 of `SandataSimulation.cs` |
| 2 | 79b | Stage 9; depends on 79a |
| 3 | 79c | Stage 11, and it moves the state hash |
| 4 | 79d-1 | Stage 12, hit resolution and events |
| 5 | 79d-2 | Stage 12, damage and cover; depends on 79d-1 |
| 6 | 81 | Whole-file allocation work; runs after every 79 split |
| 7 | 52 | Golden baselines, recorded after every hash-moving task above |
| 8 | 54 | Documentation, after 52 and 53 |

Task 53's measurement runs are not delegated. The audio instance-pool harness
needs a real audio device and the row requires naming the hardware, so the
integrating thread runs both harnesses itself, between batches. Task 55 is the
canonical gate and is likewise never delegated.

#### The rows this audit rewrites

Task 79c's "Done when" column is replaced by the corrected obligation above.
Task 79d is replaced by the two rows below. Task 80's row gains the client
startup map load the user authorised.

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 79d-1 | 12 | Make the drawn angular error decide hit or miss, and emit the shot | Stage 12 draws a real angular error from the real `Accuracy` stream and discards it at `SandataSimulation.cs:634`. Resolve that draw against the target's subtended angle, computed from the range already measured there and the provisional body radius `CollisionBodyRadiusRaw`. A miss produces no `DamageInstance`. Add the shot-fired, shot-hit, and shot-missed members to `MissionEventKind` with matching `MissionEvent` factories, and emit them from stage 12 through the same "assign then advance `NextEventSequence`" shape `EmitOrderRejectedEvent` already uses. Damage stays the flat provisional value and cover stays `NotInCover`; both are 79d-2. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 12 only), `src/Sandata.Core/Events/MissionEvent.cs`, `src/Sandata.Core/Events/MissionEventKind.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs`, `tests/Sandata.Core.Tests/MissionEventFeedTests.cs` | A test proves the same shot hits at one drawn error and misses at another, with no other input changed, reached through `RunTick` rather than by calling `ProposeFire` directly. A test proves a hit emits exactly one shot-fired and one shot-hit event and a miss emits exactly one shot-fired and one shot-missed, observable in `MissionState.EventFeed`. A test proves the event hash moves off the bare FNV-1a offset basis once a shot is emitted. | 76, 79c | |
| 79d-2 | 12 | Damage by caliber, and cover evaluated from the map | `FirearmDefinition` carries no damage field and cover never reaches the simulation. Add a provisional per-`CaliberFamily` damage table — eight values, marked provisional at the declaration, keyed the way design section 10 keys the audio report families — and read it through the shooter's loadout from task 79c. Give `SandataSimulation` the mission's `CoverRecord` values, find the record containing the target, and evaluate `CoverRules.IsWithinProtectedArc` at the shooter-to-target bearing with `CoverPosture` taken from `OperatorState.IsCrouched`, then apply `CoverRules.ApplyToDamage` to the caliber value. `FirearmDefinition.cs` and `FirearmCatalog.cs` are not edited. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 12 and the constructor only), `src/Sandata.Core/Combat/CaliberDamage.cs` (new), `src/Sandata.Core/Combat/DamageResolution.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs`, `tests/Sandata.Core.Tests/DamageResolutionTests.cs`, `tests/Sandata.Core.Tests/CoverRulesTests.cs` | A test proves two operators carrying firearms of different caliber families deal different damage on an identical hit. A test proves a target inside a cover record's protected arc takes the cover-modified value and a target outside that arc takes the unmodified one, both reached through `RunTick`. The `ProvisionalDamagePerHitPoints` constant is gone and `grep -rn 'ProvisionalDamagePerHitPoints' src/` returns nothing. | 79d-1 | |

Task 80's row is amended rather than replaced: it additionally loads
`angle-house.hkmap` at client startup and constructs the `SandataSimulation` from
it, so the client is something a person can watch and the submission door it
should be calling actually exists. The map file stays a single source of truth —
`Sandata.Client.csproj` links the existing fixture into the client's output
rather than copying it, and the follow-up of moving that fixture to a shared
`assets/maps/` location owned by neither the tests nor the client is recorded as
a later task, not done here. `src/Sandata.Client/Program.cs` and
`src/Sandata.Client/Sandata.Client.csproj` join that row's file list.

### Task 53's measurement runs, and the benchmark defect they exposed — 2026-08-08

Both harnesses were run by the integrating thread rather than delegated. The
audio one needs a real audio device and the row requires naming the hardware, so
no sub-agent could have produced an honest number for it. Raw output was captured
to `artifacts/sandata-task53/`, which is untracked, exactly as the row requires;
the figures below are the record, and task 54 carries them into
`docs/development/testing.md`.

**The hardware, reported by the harness itself:**

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

#### The audio instance-pool ceiling

```
=== Phase A: shooter-pair ceiling ramp ===
shooters held             : 128
instance count at first InstancePlayLimitException: 257 (shooter #129, InstancePlayLimitException)

=== Phase B: sustained automatic fire at the maximum operator count ===
sustained-fire duration    : 10.0 s (target 10 s)
loop instances still playing at end: 8 / 8
tail cues fired            : 14, refused 0
no InstancePlayLimitException while sustaining 8 shooters for 10.0 s.
```

The usable ceiling is 256 concurrent `SoundEffectInstance` objects, the 257th
throwing. The harness synthesized its clips in memory — a 150 ms loop and a
764 ms tail at 44,100 Hz, played at volume 0.02 — because Sandata has shipped no
audio content yet, and the pool ceiling is a property of the device rather than
of any clip's contents.

**256 is MonoGame's own DesktopGL pool limit, not a property of this sound
card.** That is worth writing down plainly, because the obvious reading of "on
named hardware" is that a different machine would give a different number, and
here it will not. What is genuinely machine-specific is Phase B: eight shooters
sustained automatic fire for ten seconds holding sixteen instances, with zero
refusals, against a 256 ceiling. Design section 10's structural claim — "A
shooter holding the trigger holds two instances, not thirty-two" — is what makes
that comfortable, and this run is the first evidence for it rather than the first
assertion of it.

`SandataSoundBudget.DefaultMaximumInstances` is still the provisional 64 at the
time of writing. It was deliberately not changed in this batch:
`SandataSoundBudgetTests` lives under `tests/Sandata.Client.Tests/`, which task
80's agent held for the whole of batch 1, and moving the constant without moving
its test is two writers in one file. The constant is set after batch 1 merges.

#### The navigation benchmark matrix

Six rows on the `angle-house` fixture, which bakes to a 160-by-180-cell nav grid,
at seed 1 and 2,000 ticks per row. Every row carries a nonzero replanning rate:
the first smoke run used a rate of zero and took only four A\* samples, at which
point p95, p99, and the maximum are all the same measurement and none of them
means anything.

| Row | Density % | Changed cells | Seekers | Query distance (wu) | Replan % | A\* samples | A\* p50 / p95 / p99 (ms) | Stage 7 p50 / p95 / p99 (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 20 | 0 | 4 | 512 | 5 | 408 | 0.8821 / 1.6028 / 2.0621 | 0.0001 / 1.0172 / 1.7411 |
| dense | 40 | 0 | 4 | 512 | 5 | 408 | 0.1210 / 0.6196 / 1.0512 | 0.0001 / 0.4316 / 0.5604 |
| doors-moving | 20 | 50 | 4 | 512 | 10 | 793 | 0.0001 / 1.2468 / 1.6153 | 0.0001 / 0.0179 / 1.2115 |
| many-seekers | 20 | 0 | 16 | 512 | 5 | 1,603 | 0.7647 / 1.5033 / 1.8552 | 0.0654 / 1.7368 / 2.6899 |
| long-queries | 20 | 0 | 4 | 2,048 | 5 | 408 | 1.7004 / 2.7783 / 3.4253 | 0.0001 / 2.3031 / 3.0602 |
| worst-case | 40 | 200 | 32 | 2,048 | 25 | 15,864 | 0.0001 / 0.0342 / 0.1241 | 0.0005 / 0.0664 / 0.2253 |

#### Only three of those six rows measure a search that found anything

Read the table as a performance result and it says the worst case is forty times
faster than the baseline, which is not a surprising result, it is an impossible
one. The cause is a defect in task 50's benchmark, and it was confirmed in the
code rather than inferred from the shape of the numbers:

- `NavBenchmark.TimeProbeQuery` calls
  `search.TryFindPath(grid, startCellIndex, goalCellIndex, blocked, scratchPath, scratchExpanded)`
  at `src/Sandata.Headless/NavBenchmark.cs:275` and **discards the return
  value**. `NavBenchmarkReport` has no outcome field of any kind.
- Each seeker's start and goal pair is placed once, before the tick loop, by
  `PlaceSeekerPair`. `ApplyChangedCells` then blocks cells on every subsequent
  tick without re-placing anything.

So a query whose goal has become unreachable returns almost immediately, having
proved a negative, and is recorded as a fast search. The higher the density and
the larger the changed-cell count, the more of the sample is failure latency —
which is exactly the gradient the table shows. `worst-case` at density 40 with
200 changed cells per tick is very nearly a pure measurement of how quickly A\*
can establish that no path exists.

**The three rows at density 20 with no changed cells are the trustworthy ones**,
and read on their own they are coherent and unremarkable: 0.88 ms at p50 for a
512-world-unit query, 1.70 ms for a 2,048-world-unit one, and going from four
concurrent seekers to sixteen moves p50 by less than a tenth of a millisecond
because the searches are independent. Stage 7's own cost tracks the query cost,
as it should, since the stage is one search per group.

This is the same class of finding as the acceptance-criterion rule wave 11
recorded: *a measurement a fixture can satisfy without exercising the thing being
measured is not a measurement.* A benchmark that cannot distinguish "found a path
in 0.0001 ms" from "proved there is no path in 0.0001 ms" reports its most
degenerate configuration as its best result.

#### One task this finding creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 82 | 12 | Make the navigation benchmark report search outcomes | `NavBenchmark.TimeProbeQuery` discards `NavSearch.TryFindPath`'s return value at `NavBenchmark.cs:275` and `NavBenchmarkReport` carries no outcome field, so a row in which every query is unreachable reports the best percentiles in the matrix. Record the outcome of every probe query and report the breakdown beside the percentiles. Report the successful-search percentiles separately from the all-queries percentiles, since the two answer different questions and only the first one is a navigation performance number. | `src/Sandata.Headless/NavBenchmark.cs`, `src/Sandata.Headless/NavBenchmarkOptions.cs`, `tests/Sandata.Core.Tests/NavBenchmarkOptionTests.cs` | A test proves a benchmark configuration whose goals are all unreachable reports zero successful searches rather than a fast p50. The six-row matrix above is re-run and its successful-search percentiles recorded here, replacing the table above as the figures task 54 carries into `docs/development/testing.md`. | 50 | |

Task 82 runs before task 54, because task 54's whole job is recording numbers and
three of the six rows above are not yet numbers worth recording. The audio
figures are unaffected by any of this and stand as measured.

### Task 53 re-run after task 82, and the second benchmark defect it found — 2026-08-08

Task 82 merged at `414c9c1` and the six-row matrix was re-run against it. The
figures in the previous section stand as the record of what the broken benchmark
reported; everything below supersedes them as the record of what the workload
actually does. Task 54 carries the numbers from *this* section into
`docs/development/testing.md`.

#### What fraction of each row ever found a path

| Row | Density % | Changed cells | Seekers | Query wu | Replan % | Probes | Found | Found % |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 20 | 0 | 4 | 512 | 5 | 408 | 408 | 100.0 |
| dense | 40 | 0 | 4 | 512 | 5 | 408 | 0 | 0.0 |
| doors-moving | 20 | 50 | 4 | 512 | 10 | 793 | 66 | 8.3 |
| many-seekers | 20 | 0 | 16 | 512 | 5 | 1,603 | 1,603 | 100.0 |
| long-queries | 20 | 0 | 4 | 2,048 | 5 | 408 | 408 | 100.0 |
| worst-case | 40 | 200 | 32 | 2,048 | 25 | 15,864 | 0 | 0.0 |

The three rows called trustworthy in the previous section were the right three,
and the assessment of the other three was too generous: `dense` is not partly
degenerate, it is **entirely** degenerate at zero successful searches out of 408.
It reported the second-best p50 in the whole matrix.

Successful-search percentiles for the rows that have any, which are the only
navigation performance numbers this hardware has produced:

| Row | Successful p50 / p95 / p99 (ms) |
| --- | --- |
| baseline | 0.8861 / 1.3273 / 1.7880 |
| doors-moving | 1.2540 / 1.8782 / 4.9456 |
| many-seekers | 0.8169 / 1.6261 / 2.0530 |
| long-queries | 2.1426 / 2.8909 / 3.6742 |

#### The map-density parameter has a cliff between 30 and 40 percent

A density sweep was run to find it, since a matrix whose rows silently fall off a
connectivity cliff is not a matrix. All rows at zero changed cells, four seekers,
512-world-unit queries, 5 percent replanning, 2,000 ticks:

| Density % | Probes | Found | Found % | Successful p50 / p95 / p99 (ms) |
| --- | --- | --- | --- | --- |
| 0 | 418 | 418 | 100.0 | 0.8215 / 3.9979 / 4.4503 |
| 10 | 408 | 408 | 100.0 | 0.5895 / 1.2473 / 1.6271 |
| 20 | 408 | 408 | 100.0 | 0.8861 / 1.3273 / 1.7880 |
| 30 | 408 | 206 | 50.5 | 1.2055 / 1.5904 / 1.7153 |
| 40 | 408 | 0 | 0.0 | — |

`angle-house` is an indoor map that is already walled. The density parameter
blocks additional cells on top of that, so 40 percent extra blocking severs it
completely and 30 percent severs half the seeker pairs. **The usable density
range on this fixture is 0 to 20 percent**, and any future matrix row above that
is measuring disconnection rather than pathfinding.

#### The changed-cell parameter randomises the map instead of moving doors

Two more rows were run inside the usable density range, and they still came back
mostly unreachable:

| Row | Density % | Changed cells | Seekers | Query wu | Replan % | Probes | Found | Found % | Successful p50 / p95 / p99 (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| doors-light | 10 | 20 | 4 | 512 | 10 | 798 | 173 | 21.7 | 1.0413 / 1.7029 / 4.0855 |
| stress-connected | 10 | 50 | 32 | 2,048 | 25 | 16,011 | 1,293 | 8.1 | 1.3470 / 3.1114 / 3.9579 |

That is not a density problem, and the cause is in the code rather than in the
parameters. `NavBenchmark.ApplyChangedCells` (`src/Sandata.Headless/NavBenchmark.cs:386-402`)
draws `changedCellCount` **fresh random cell indices across the whole grid on
every tick** and toggles each between `Open` and `Blocked`, skipping only
`NavCellFlags.Door`. It is an unbiased random toggle over 28,800 cells, so the
map random-walks away from its authored layout toward a roughly half-blocked
noise field and then stays there. The density sweep above shows that anything
past about 30 percent blocked is disconnected, so within a few hundred ticks
every seeker pair is severed no matter how small the changed-cell count is. Only
the rate of arrival differs.

That is the wrong model twice over. Design section 5, stage 4 says **"Doors are
the only runtime nav mutation in v0.1"** and that the rebake is local rather than
global. A door is a fixed location toggling between two states; it is not an
arbitrary interior cell being randomised, and a nav rebake in this game never
touches a cell that is not part of a door. As written, the changed-cell
parameter cannot measure what
`SIMULATION-GAME-STANDARDS.md` section 11 asks it to measure — replanning cost
against dynamic blockers — because after a short warm-up there is nothing left to
replan through.

This is the same shape as the defect task 82 fixed, one level further in. Task 82
made the benchmark *report* that its queries were failing. This one is why they
fail.

#### One task this finding creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 83 | 12 | Make the benchmark's changed cells behave like doors | `NavBenchmark.ApplyChangedCells` (`NavBenchmark.cs:386-402`) redraws `changedCellCount` random cell indices across the whole grid every tick and toggles each one, so the map random-walks to a roughly half-blocked noise field and every seeker pair is severed within a few hundred ticks — the density sweep recorded above puts the disconnection threshold between 30 and 40 percent blocked. Design section 5, stage 4: "Doors are the only runtime nav mutation in v0.1", and the rebake is local. Choose the changed-cell set **once**, at placement time, from cells that are genuinely part of the authored layout's connectivity, and toggle that same fixed set each tick so the map oscillates between two known configurations instead of degrading. | `src/Sandata.Headless/NavBenchmark.cs`, `tests/Sandata.Core.Tests/NavBenchmarkOptionTests.cs` | A test proves a 2,000-tick run with a nonzero changed-cell count ends with the same set of cells blocked-or-open as some tick early in the run, rather than a monotonically degrading one. A test proves the successful-search fraction of a changed-cell run inside the usable density range stays above a stated floor for the whole run instead of collapsing. The `doors-light` and `stress-connected` rows above are re-run and their successful-search percentiles recorded here. | 82 | |

Task 83, like task 82, runs before task 54. The audio measurements in the
previous section are untouched by any of this and stand as measured.

**The usable matrix, as it stands today**, is density 0 to 20 with zero changed
cells. Within it the numbers are coherent and unremarkable: roughly 0.6 to 0.9 ms
at p50 for a 512-world-unit query on a 160-by-180-cell grid, about 2.1 ms for a
2,048-world-unit one, and moving from four concurrent seekers to sixteen barely
moves p50 because the searches are independent of one another.

### Task 53 complete, after tasks 82 and 83 — 2026-08-08

Task 83 merged at `8533b26` and the matrix was run a third time. **These are the
figures task 54 records in `docs/development/testing.md`.** The two earlier
tables are kept above as the record of two real defects and how they presented,
not as measurements of anything.

Task 83's effect on the two rows it was dispatched against is the confirmation
that the diagnosis was right, and it is large:

| Row | Found before task 83 | Found after |
| --- | --- | --- |
| doors-light | 173 of 798, 21.7 percent | 820 of 820, 100.0 percent |
| stress-connected | 1,293 of 16,011, 8.1 percent | 14,933 of 15,937, 93.7 percent |

#### The measurement, on named hardware

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

**Audio instance pool.** The 257th concurrent `SoundEffectInstance` throws
`InstancePlayLimitException`, so 256 is the usable pool. Eight shooters
sustained automatic fire for ten seconds holding sixteen instances — one loop
and one tail each — with fourteen tail cues fired, zero refused, and no
exception. `SandataSoundBudget.DefaultMaximumInstances` was moved from the
provisional 64 to the measured 256 at commit `650214c`, and its provisional
markers removed.

The constant equals the ceiling rather than sitting below it, and that is a
deliberate choice with a stated precondition rather than an oversight. This
budget refuses the 257th reservation, which is the same instance MonoGame would
have thrown on, so no headroom is required — **but only while every played
instance is reserved here first.** Every cue path on `SandataSoundPlayer` goes
through `TryReserve` before `ISandataSoundOutput.Play` today. A future task that
adds a play path bypassing the budget reintroduces exactly the exception this
constant exists to prevent, and that is the fifth instance of this repository's
recurring "a validated door beside an open one" hazard.

**Navigation matrix.** The `angle-house` fixture bakes to a 160-by-180-cell nav
grid. Seed 1, 2,000 ticks per row.

| Row | Density % | Changed cells | Seekers | Query wu | Replan % | Probes | Found % | Successful p50 / p95 / p99 (ms) | Stage 7 p50 / p95 / p99 (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 20 | 0 | 4 | 512 | 5 | 408 | 100.0 | 0.9376 / 1.5742 / 1.8825 | 0.0001 / 1.1056 / 1.6790 |
| many-seekers | 20 | 0 | 16 | 512 | 5 | 1,603 | 100.0 | 0.7900 / 1.5222 / 1.8724 | 0.0672 / 1.7753 / 2.8294 |
| long-queries | 20 | 0 | 4 | 2,048 | 5 | 408 | 100.0 | 1.5465 / 2.9441 / 3.2198 | 0.0001 / 2.4036 / 3.2181 |
| doors-light | 10 | 20 | 4 | 512 | 10 | 820 | 100.0 | 0.5794 / 1.2112 / 1.3738 | 0.0001 / 0.9457 / 1.3047 |
| stress-connected | 10 | 50 | 32 | 2,048 | 25 | 15,937 | 93.7 | 1.3415 / 4.8988 / 6.0748 | 3.8829 / 11.5732 / 15.9489 |

What the numbers say, now that they say anything:

- A single A\* query over a 512-world-unit distance costs well under a
  millisecond at p50 and under two at p99.
- Quadrupling the query distance to 2,048 world units roughly doubles the cost,
  which is the expected shape for grid A\* over a bounded indoor map.
- Going from four concurrent seekers to sixteen barely moves p50, because the
  searches are independent of one another. What it moves is the stage-7 total,
  which is the sum of them.
- **The stress row is the one worth watching.** Thirty-two seekers replanning at
  25 percent puts stage 7 at 3.88 ms p50 and 15.95 ms p99. At the 50 Hz tick
  rate the ruleset declares, one tick is 20 ms, so that row spends most of a
  tick budget in one stage. It is far past anything design section 8 anticipates
  — that section's cost table is written for `n = 16` operators and `g = 4`
  groups — and it is not a configuration the game runs. It is recorded because a
  measured ceiling is worth more than an assumed one.
- The 6.3 percent of stress-row queries that still fail are genuine: at 50
  toggling cells and 32 seekers, some goals sit behind a cell that is blocked in
  the toggled configuration. That is a real dynamic-blocker case, which is what
  the row was meant to measure.

#### One cost this incurred, recorded rather than absorbed silently

Tasks 82 and 83 added seven test cases that run the benchmark itself, including a
2,000-tick, 32-seeker, 2,048-world-unit row. Sandata's core suite went from
roughly ten seconds to about 45. That does not touch the canonical gate today,
because design section 14 keeps the gate on the Hukbo projects until Sandata has
a recorded baseline. **It will matter at task 55**, which is the task that
proposes running `verify.ps1 -Game Sandata`. Whoever takes that decision should
know they are adding about 45 seconds, and that roughly half of it is one test
case, before deciding whether the benchmark tests belong in a gate at all or
belong beside `tools/` as hand-run measurement.

Task 53 is complete. Tasks 82 and 83 are complete. Task 54 now has figures worth
recording.

### There is no movement speed, and an ordered operator teleports — 2026-08-08

Task 79b's rejected first attempt pinned `leaderArclength` to the polyline's
`TotalLength`, and the redo's evidence for why that was wrong turned up something
larger than the defect it was sent back to fix. With the bad pin restored, the
operator's raw X after a single `RunTick` was **26,624** — the goal's own raw X,
26 world units — where a correct projection produces 7,168. It did not move
toward the goal over several ticks. It arrived, in one.

The cause is not in stage 9. `LocalAvoidance.Commit`
(`src/Sandata.Core/Movement/LocalAvoidance.cs:105-148`) hands
`proposal.DesiredXRaw` and `proposal.DesiredYRaw` straight to
`SandataCollisionResolver.Resolve`. **Nothing anywhere clamps how far an operator
may move in one tick.** There is no speed field on `SandataRuleset`, no step
constant in `Movement/`, and no distance check in the resolver. An operator moves
to its proposed position in a single tick regardless of distance, subject only to
collision.

That is defensible as a division of labour — design section 8 describes stage 10
as "commit sequentially against the collision grid" and says nothing about
speed, so producing one tick's worth of movement is the *proposer's* obligation,
and stage 9 is the proposer. What is not defensible is that only one of the two
proposal branches honours it:

- **The autonomous branch now does**, since task 79b: it samples the polyline at
  the leader's projected arclength plus `FormationLookaheadWu`, so the proposed
  point is a bounded distance from the current one. That bound is an accidental
  speed and it is marked provisional.
- **The ordered branch does not.** `ComputeMovementProposals` sets
  `desiredXRaw = RawFromWorldUnits(node.X)` directly from
  `assignment.PathNodes[assignment.CurrentNodeIndex]`. An operator following a
  path a player drew jumps to its current waypoint in one tick.

This is reachable in the game right now. Task 80 wired `Sandata.Client`'s path
draw tool to `SandataSimulation.SubmitOrder` and gave the client a real
simulation over `angle-house.hkmap`, so drawing a path and submitting it is a
thing a person can do, and what they will see is an operator teleporting between
the waypoints they drew. Design section 16 promises the opposite in as many
words: "A path a person drew is that person's decision", with the operator
walking it and the inspector able to report "the node index currently being
walked". A node index being walked presumes walking.

The wider point is worth stating plainly, because it explains why this went
unnoticed for eleven waves: **every stage-9 test before task 79b asserted that an
operator's position changed, and none asserted how far it moved.** A teleport
satisfies "moved" perfectly. It is the same shape as the two benchmark defects
this wave found — a measurement that cannot distinguish the success it claims to
observe from a degenerate case that looks identical.

#### One task this creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 84 | 12 | Give movement a speed | `LocalAvoidance.Commit` commits `MovementProposal.DesiredXRaw`/`DesiredYRaw` unclamped, so an operator moves any distance in one tick. Stage 9's ordered branch proposes the authored waypoint itself, so an operator under a drawn order teleports between the nodes a player drew — observable in the client task 80 wired up. Clamp the proposed step to a per-tick movement distance in stage 9, for **both** branches, so the ordered branch walks its polyline instead of jumping to it. Replace task 79b's provisional `FormationLookaheadWu` with that same distance rather than leaving two constants that both mean "how far in one tick". Do not add a `SandataRuleset` field — that moves the pinned `ContentHash` `8_955_292_433_887_190_872`; declare it a provisional `const` marked at its declaration, as task 79b's four already are, and record that a real tuning pass owes this a measured value. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (stage 9 only), `tests/Sandata.Core.Tests/TickPipelineTests.cs` | A test proves an operator under an `OrderAssignment` whose next waypoint is far away takes more than one tick to reach it, and that its per-tick displacement never exceeds the constant — asserted on the displacement, not on "the position changed". A test proves the same bound for the autonomous branch. Task 79b's `RunTick_UnassignedOperatorInGroupWithPublishedPath_FollowsTheBentPolylineNotTheGoal` still passes, with its pinned raw coordinates updated if and only if the constant differs from `FormationLookaheadWu`'s 8. | 79b, 80 | |

Task 84 shares `SandataSimulation.cs` with every remaining task-79 split and with
task 81, so it does not run beside any of them.

### Task 79c complete, and the pinned-literal habit it exposed — 2026-08-08

Merged at `2d1db58`. `OperatorState` gained a `Firearm` field, folded last inside
`FoldOperator` — after the nested contact-memory entries, not interleaved among
the scalar fields — carried through `MissionSnapshot` with no edit to that file
at all, and read at stage 11 in place of the hardcoded `DefaultFirearmId`.

The wave-12 audit's corrected acceptance criterion is what this task was built
against, and it was the right correction. The original row asked for a test
pinning the *pre-change* state hash; that test cannot exist, because folding one
more value into FNV-1a changes the digest unconditionally.

**The seed-1 workload's state hash moved from `00EC034D18941D36` to
`FB4715E7AFF108F6`, on purpose**, and the run stayed `deterministic: true` with
`firstMismatchTick: null`. That is this task's deliverable. The event hash is
still `CBF29CE484222325`, the bare FNV-1a offset basis, because nothing yet emits
during a run — task 79d-1 is what changes that.

`PreTask79cBaselineHash` is `3_159_438_799_659_597_482UL`.

#### A real bug in the hasher, found because the fold had to go last

`FoldOperator` used to `return` early from inside `if (contactMemory.IsDefault)`.
Nothing followed that branch, so it had never skipped anything and no recorded
hash was ever wrong. It was a trap laid for the next person to append a fold:
any operator whose `ContactMemory` was still its default value would have
silently skipped the appended field, producing a hash that depended on whether an
unrelated array had been initialised. Task 79c restructured it to an
`if (!contactMemory.IsDefault)` block with no early return, which changes no
pre-existing hash and makes the appended fold unconditional.

#### Two absolute-literal pins broke, and updating them was the integrator's call

`OrderStateHashTests.PreTask61BaselineHash` and
`MissionEventFeedTests.PreTask76BaselineHash` both pinned
`5_550_901_129_500_655_850UL` for the same fixture, and task 79c's appended fold
moved both to `3_159_438_799_659_597_482UL`. **The implementing agent stopped at
its grant boundary and reported the two failures rather than editing literals it
had not been given** — which is exactly right, and is the second time this wave an
agent has been correct to refuse an instruction. The integrating thread made the
edit and the judgement, at commit `2d1db58`.

The judgement, written down because it is the kind that gets made silently and
then regretted: updating those literals is **not** the forbidden "re-pin a hash to
go green". The property each test guards is untouched by task 79c. `PreTask61`
guards that an empty order queue folds nothing; `PreTask76` guards that
`SandataStateHasher.Compute` never reads `MissionState.EventFeed`. Both remain
true. Only the absolute value of an unrelated fixture moved, for a sanctioned
reason recorded above. Had the *property* broken, the correct action would have
been to stop.

But the episode names a habit worth fixing. **Both tests assert an absolute
literal where they mean to assert an invariant.** Written that way, every future
legitimate change to the operator fold breaks two unrelated test files and
presents the next agent with a one-character fix that looks harmless and is
sometimes catastrophic. `MissionEventFeedTests` already contains the better
form of its own assertion —
`StateHash_DoesNotMove_WhenTheEventFeedGainsEvents` compares two computed hashes
and is invariant under any appended fold — so the pattern is already in the
repository and is simply not used consistently.

#### One task this creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 85 | 12 | Turn the absolute state-hash pins into invariant assertions | `OrderStateHashTests.PreTask61BaselineHash` and `MissionEventFeedTests.PreTask76BaselineHash` pin an absolute fixture digest to guard properties that are actually relational: "an empty order queue folds nothing" and "`Compute` never reads the event feed". Any legitimate change to the operator fold breaks both and invites a one-character re-pin. Rewrite each as a comparison between two computed hashes — the state with an empty queue against a state whose queue was never populated, and the state with an empty feed against the same state with events appended — following `StateHash_DoesNotMove_WhenTheEventFeedGainsEvents`, which already does it correctly in the same file. Keep exactly one absolute pin in the suite, `PreTask79cBaselineHash`, as the deliberate canary that fires when any state fold changes, and say in its comment that it is the only one and why. | `tests/Sandata.Core.Tests/OrderStateHashTests.cs`, `tests/Sandata.Core.Tests/MissionEventFeedTests.cs`, `tests/Sandata.Core.Tests/MissionStateTests.cs` | Both rewritten tests pass, and both still fail if their guarded property is genuinely broken — proved by temporarily breaking each property and recording the failure. Exactly one absolute state-hash literal remains in `tests/Sandata.Core.Tests/`, confirmed by search, and the search used is recorded. | 79c | |

Task 85 touches only test files, but it is **not** free to run beside anything:
`MissionEventFeedTests.cs` is writable to task 79d-1, and `TickPipelineTests.cs`
is writable to tasks 81 and 84. This correction is itself the file-level half of
the audit doing its job — the sentence originally written here claimed task 85
conflicted with nothing, and that claim was wrong the moment task 79d-1 was
dispatched with the same test file in its grant. Task 85 runs after task 79d-1
merges, and not beside task 81 or task 84.

### Task 79d-1 complete, and the invented constants it made load-bearing — 2026-08-08

Merged. Stage 12 now resolves the drawn angular error against the target's
subtended half-angle, a miss produces no `DamageInstance`, and three new event
kinds — `ShotFired = 1`, `ShotHit = 2`, `ShotMissed = 3`, appended after
`OrderRejected = 0`, whose value is contract and was not renumbered — are emitted
through the same "assign the sequence, then advance it" shape
`EmitOrderRejectedEvent` already used.

**Sandata emitted events during a run for the first time.** The seed-1 workload's
event hash moved from `CBF29CE484222325`, the bare FNV-1a offset basis, to
`270364E265A3A8A7`. The event half of the determinism contract is now asserted by
a run rather than by construction, which is what the wave-11 record said task
79d would be the first to do. The state hash moved from `FB4715E7AFF108F6` to
`6D4AEA08BEFEFA92` and the run stayed `deterministic: true`.

Two process notes. The agent did not commit its work and reported a test total of
3,152, which is neither Sandata suite — the integrating thread committed the
branch at `612ca2b` and re-derived the real figures from the merged tree, 1,085
core and 199 client. **Every count in this document is re-derived, never taken
from a report**, and this is the wave's clearest illustration of why.

#### The survivor counts did not move, and chasing that found the real defect

Before hit resolution existed, the seed-1 workload ended with 98 and 92
survivors. After it — with misses now producing no damage at all — the workload
ends with **98 and 92 survivors**. Both hashes moved, so the mechanism is
genuinely running. A change that removes damage from some fraction of all shots
and kills exactly the same people is not a result, it is a symptom.

It is. `SandataSimulation` declares:

```
private const int CollisionCellSizeRaw = 256;   //  0.25 wu
private const int CollisionBodyRadiusRaw = 32;  //  0.03125 wu
```

Design section 4's unit table specifies the second of those explicitly, and not
as a suggestion:

> | Body radius | world unit | 4.25 wu, `CollisionRules.DefaultBodyRadiusRaw` unchanged, which is 0.266 m — a 0.53 m human footprint |

`Hukbo.Core/Simulation/CollisionRules.cs:72` carries exactly that value:
`public const int DefaultBodyRadiusRaw = (17 * FixedPoint.Scale) / 4`, which is
4,352 raw. Sandata uses 32. **The operator body radius in the shooter is 136
times smaller than the number the design names**, and at 1 metre = 16 world units
it describes a person three centimetres across.

Everything downstream inherits it:

- **Collision is decorative.** Two operators must come within 0.0625 wu — four
  millimetres — to register contact. `LocalAvoidance`, `SidestepRules`, and the
  whole propose-prioritise-commit chain resolve conflicts that essentially never
  occur.
- **Hit resolution is now built on it.** Task 79d-1 computes the target's
  subtended half-angle as `Cordic.Atan2(CollisionBodyRadiusRaw, range)`, which is
  correct code against an incorrect constant. At a 90-world-unit range the target
  subtends about two hundredths of a degree, so whether a shot lands is decided
  by whether the drawn error rounds to near zero rather than by aim, range, or
  dispersion. That is why removing damage from misses changed nobody's fate.
- **The collision grid cell is smaller than a body.** `CollisionCellSizeRaw` is
  0.25 wu against a designed 4.25 wu radius, so the uniform grid's cells are
  seventeen times finer than the objects they index.

Design section 4 also states the invariant these constants exist to satisfy:

> Fifty hertz also keeps the collision invariant `MovementSpeedRaw <= BodyRadiusRaw`
> comfortable: a 5 m/s sprint is 80 wu per second, which is 1.6 wu per tick
> against a 4.25 wu radius.

Against the code's 0.03125 wu radius, the maximum lawful movement step is 0.03125
wu per tick. Task 79b's `FormationLookaheadWu` is 8. **The invariant is violated
by a factor of 256**, which means operators pass through one another and through
the grid between ticks and no test notices.

#### This corrects task 84's row

Task 84 was written on the finding that no movement-speed constant exists in
`Movement/`. That is true of the code and it led to the wrong conclusion. **The
design specifies the speed**: 5 m/s is 80 wu per second, which at the ruleset's
50 Hz tick rate is exactly 1.6 wu per tick. It is not a value for an implementer
to invent and mark provisional; it is a value to read out of design section 4 and
derive. Task 84's row is amended accordingly below.

#### The pattern, stated once

Six constants in `SandataSimulation.cs` carry `<b>PROVISIONAL</b>` markers
claiming that nothing supplies them. For at least the body radius, the movement
speed, and arguably the collision cell size, **something did supply them and
nobody looked.** The marker is honest about the code and wrong about the design,
and being marked provisional made each one feel accounted for.

This is wave 11's lesson pointed at the source instead of at a brief. A remedy
stated in a brief needs its own reading of the code; a constant marked
provisional needs its own reading of the *design*. Both failures look like
diligence.

#### One task this creates, and one it amends

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 86 | 12 | Take the invented physical constants out of the shooter and use the designed ones | `CollisionBodyRadiusRaw` is 32 raw, 0.03125 wu. Design section 4's unit table names 4.25 wu and names its source, `CollisionRules.DefaultBodyRadiusRaw` at `src/Hukbo.Core/Simulation/CollisionRules.cs:72`, which is `(17 * FixedPoint.Scale) / 4` = 4,352 raw. Use the designed value. Re-derive `CollisionCellSizeRaw` from it rather than leaving 0.25 wu cells indexing 4.25 wu bodies, and state the rule you derived it by. Do **not** take a `ProjectReference` on `Hukbo.Core` — the reference graph forbids it; read design section 3 and either restate the value with its provenance in a comment or raise tier-2 extraction as a question rather than doing it. Every `<b>PROVISIONAL</b>` marker you leave in place must be re-checked against design section 4 first, and the ones the design actually supplies must lose the marker and gain a citation. | `src/Sandata.Core/Simulation/SandataSimulation.cs` (the constants and their doc comments), `tests/Sandata.Core.Tests/TickPipelineTests.cs`, `tests/Sandata.Core.Tests/SandataCollisionTests.cs` | A test pins the body radius to 4,352 raw and cites design section 4 in its own comment. A test proves two operators at a plausible separation now collide where they previously did not. The seed-1 workload is re-run and its new hashes, survivor counts, and outcome recorded here — the survivor counts are expected to move, and if they do not, that is a finding to report rather than a result to accept. Task 79d-1's hit and miss tests still pass, with their pinned entity ids updated if the changed subtended angle moves which draw hits. | 79d-1 | |

**Task 84 amended.** Its row said the per-tick movement distance is a value to
invent and mark provisional. It is not. The row now reads: derive the per-tick
movement step from design section 4 — a 5 m/s sprint is 80 wu per second, which
at `TickRate` 50 is 1.6 wu per tick — cite that derivation at the declaration,
and assert the design's own invariant `MovementSpeedRaw <= BodyRadiusRaw` in a
test so that a future change to either constant cannot silently break it. Task 84
now depends on task 86, because the invariant needs the corrected radius to mean
anything.

Ordering for the rest of the wave: task 86, then task 84, then task 81, then task
52. All four want `SandataSimulation.cs` and none may run beside another.

### The second wave-12 audit, run before the remaining eight rows — 2026-08-08

Eight rows are left: 86, 84, 81, 79d-2, 52, 85, 54, and 55. Both directions of
the audit were run over them against `main` at `13f67ad` before any agent was
dispatched. The file-level half passed. The surface-level half produced six
findings, one of which is large enough that it changes what task 86 can honestly
deliver.

Everything below is re-derived from the merged tree rather than taken from the
wave's own record, because the record itself says every count in this document is
re-derived and this wave has twice caught a report that was wrong.

**The baseline, measured now.** `./scripts/test.ps1 -Configuration Release -Game Sandata`
reports 1,085 tests passed in `Sandata.Core.Tests` and 199 passed in
`Sandata.Client.Tests`, with zero failures in either. The seed-1 workload
reports:

```
{"seed":1,"operatorsPerFaction":100,"requestedTicks":10000,"measuredTicks":10000,
 "allocatedBytes":65782309192,"outcome":"Ongoing",
 "faction0Survivors":98,"faction1Survivors":92,
 "eventHash":"270364E265A3A8A7","stateHash":"6D4AEA08BEFEFA92",
 "deterministic":true,"firstMismatchTick":null}
```

All three absolute pins — `OrderStateHashTests.PreTask61BaselineHash`,
`MissionEventFeedTests.PreTask76BaselineHash`, and
`MissionStateTests.PreTask79cBaselineHash` — carry
`3_159_438_799_659_597_482UL` on disk today. The graph tools `CLAUDE.md`
section 8 mandates are not available in this session, so discovery for this audit
was done with `Grep` and `Read` over the working tree; that is a deviation worth
recording rather than passing over silently.

#### The seed-1 fixture cannot hold operators of the designed size

This is the finding that matters. Task 86's row asks for the seed-1 workload to
be re-run and its new survivor counts recorded, and expects them to move. They
will move, but not for a reason anyone would want to record as a result.

`HeadlessRunner.BuildOpenGrid` sizes the nav grid as
`side = ceil(sqrt(operatorCount))`, which is 15 cells for the 200-operator
workload, and `HeadlessRunner.BuildInitialState` then places one operator per nav
cell at that cell's centre with a jitter of at most one world unit.
`NavGrid.CellSizeWu` is 4. **The operators are therefore four world units apart.**

The designed body radius is 4.25 world units, so the designed body *diameter* is
8.5 world units. Every operator in the seed-1 fixture would overlap all four of
its orthogonal neighbours by more than half a body the instant task 86 lands, and
design section 8 forbids the one mechanism that would push them apart: "Never a
force, never an impulse, never a push-apart." A blocked unit tries one
22.5-degree sidestep and then waits. Two hundred mutually overlapping bodies on a
60-by-60-world-unit grid is not a configuration that resolves; it is a
configuration in which nothing can move at all.

Three consequences follow, and none of them is optional:

- Task 86 cannot record a meaningful post-change survivor count from a fixture
  that is geometrically invalid the moment the change lands. Recording it anyway
  would produce exactly the class of number this wave has already thrown away
  twice — a measurement whose degenerate case and whose success case look
  identical.
- Task 81 measures per-tick allocation. A run in which every operator overlaps
  four neighbours emits a far larger collision pair list than a correctly spaced
  one, so an allocation table taken against the broken fixture would name the
  wrong stage.
- Task 52 records the golden baselines. A baseline recorded from a fixture that
  has to be re-spaced afterwards is a baseline that has to be recorded twice.

**Task 86's file grant therefore gains `src/Sandata.Headless/HeadlessRunner.cs`,
and its scope gains the fixture spacing.** The alternative — a separate task
after it — costs an extra batch and buys nothing, because the two changes are
meaningless apart and both move the same two hashes.

The obligation is stated narrowly on purpose. The implementer measures the
minimum pairwise separation in the current fixture and reports it; re-spaces the
placement so that separation is at least one body diameter, deriving the new
pitch from `CollisionBodyRadiusRaw` and `NavGrid.CellSizeWu` rather than choosing
a number; and changes nothing else about the fixture — not the operator count,
not the jitter rule, not the faction split, not the RNG draw order. The jitter
draw in particular must keep consuming exactly the same number of `SplitMix64`
values in exactly the same order, or the change stops being a spacing change and
becomes an unrelated RNG-stream change hiding inside one.

#### Task 85 conflicts with nothing that is left

The note recorded above at task 79c's completion says task 85 "is **not** free to
run beside anything", naming `MissionEventFeedTests.cs` as writable to task 79d-1
and `TickPipelineTests.cs` as writable to tasks 81 and 84. The first half was
true and is now spent: task 79d-1 is merged. The second half was never true.
Task 85's row grants three files — `OrderStateHashTests.cs`,
`MissionEventFeedTests.cs`, and `MissionStateTests.cs` — and `TickPipelineTests.cs`
is not among them.

Checked against every remaining row rather than against memory: tasks 86, 84, 81,
and 79d-2 grant `TickPipelineTests.cs`; task 79d-2 additionally grants
`DamageResolutionTests.cs` and `CoverRulesTests.cs`; task 52 grants
`DeterminismEquivalenceTests.cs`, `GoldenReplayTests.cs`, and
`Fixtures/seed-1-baseline.json`; task 54 grants documentation only. None of them
touches any of task 85's three files.

Task 85 is also semantically independent of everything above it. Its three pins
are digests of a constructed `MissionState` fixture, not of a run, so only a
change to the *fold* moves them — and no remaining task changes the fold. Task 85
pairs with task 86 in batch 1.

This is the second time in two waves that a claim about task 85's conflicts has
been wrong, once in each direction. The lesson is the same one this wave has now
recorded three times in three different disguises: a statement written into the
plan is a claim, and it needs its own reading of the thing it describes.

#### Task 84's per-tick step is not an integer, and the rounding rule is a decision

Task 84's amended row says to derive the per-tick movement step from design
section 4: a 5 m/s sprint is 80 world units per second, which at `TickRate` 50 is
1.6 world units per tick. That derivation is correct and it does not land on an
integer in either unit. At `FixedPoint.Scale` 1024, 1.6 world units is 1,638.4
raw, and the constant has to be an integer.

The design supplies the speed and does not supply the rounding, so an implementer
told only to "derive it" will invent one. The rule is written here instead:
`MovementStepRaw = (80 * FixedPoint.Scale) / TickRate`, evaluated in integer
arithmetic, which truncates toward zero to **1,638 raw**. Truncation rather than
rounding half away from zero is chosen because design section 4's own conversion
rule for weapon timings is the only rounding rule the game has, it is explicitly
scoped to milliseconds, and borrowing it for a distance would create a second
pinned rule that nothing states. Truncating also keeps the invariant
`MovementSpeedRaw <= BodyRadiusRaw` on the safe side of the boundary rather than
the unsafe one.

Task 84 must also state, at the declaration, that 1,638 raw is derived from the
design's 5 m/s sprint figure and is therefore the *sprint* bound rather than a
walking pace, because design section 4 offers no second speed and the game has no
gait model.

#### Task 79d-2 needs a constructor parameter and seven call sites it does not have

`SandataSimulation`'s constructor takes five parameters —
`Mission`, `SandataRuleset`, `NavGrid`, `WallBuckets`, `MissionState` — and cover
is in none of them, exactly as the first wave-12 audit recorded. What that audit
did not do is count the call sites. There are seven files:

| File | Sites |
| --- | --- |
| `src/Sandata.Client/SandataGame.cs` | 1 |
| `src/Sandata.Headless/HeadlessRunner.cs` | 2 |
| `tests/Sandata.Client.Tests/ClientOrderDoorTests.cs` | 2 |
| `tests/Sandata.Client.Tests/HudComposerTests.cs` | 1 |
| `tests/Sandata.Client.Tests/PathDrawToolTests.cs` | 1 |
| `tests/Sandata.Core.Tests/MissionEventFeedTests.cs` | 6 |
| `tests/Sandata.Core.Tests/TickPipelineTests.cs` | 30 |

Only the last is in task 79d-2's grant.

The parameter is **required, not optional with a default**. An optional cover
parameter would mean a caller that omits it gets a simulation in which cover
silently does nothing — which is this repository's recurring "a validated door
beside an open unvalidated one" hazard for the sixth time, and the one shape of
defect no file-level audit ever catches. A required parameter turns every missed
call site into a compile error.

Task 79d-2's grant therefore gains all six additional files. It runs alone, and
it may not run beside task 85, because the `MissionEventFeedTests.cs` overlap
that the earlier note claimed is real for the first time.

One thing this makes visible and does not fix: `HeadlessRunner` synthesises its
mission and loads no map, so the seed-1 workload carries no `CoverRecord` values
at all. Cover is therefore unobservable in the workload and is provable only
through `TickPipelineTests`. That is acceptable — it is the same situation
`WallBuckets.Build(grid, [], [], [], [])` already puts walls in — but task 79d-2
must pass an empty cover set at those two sites rather than inventing fixture
cover, and must say so.

#### Stage 12 still reads a hardcoded firearm

`ProposeFire` resolves its weapon definition at `SandataSimulation.cs:655` with
`FirearmCatalog.Rows[(int)DefaultFirearmId]`, and `DefaultFirearmId` is the
`FirearmId.Ak47` constant at line 352. Task 79c gave `OperatorState` a `Firearm`
field and wired it into stage 11's chain advance; stage 12 never learned about
it, and the constant's own doc comment at line 342 says only that
`AdvanceWeaponChain` no longer uses it.

So the loadout task 79c added is half-connected, and task 79d-2 is the row that
finishes it: keying a caliber damage table off `DefaultFirearmId` would satisfy
the letter of "read it through the shooter's loadout" while changing nothing.
Task 79d-2's own acceptance criterion — two operators carrying different caliber
families dealing different damage on an identical hit — is decisive against that,
and the brief names the line so the point is not left to be discovered.

#### Task 52's golden baseline against task 85's single-pin rule

Task 85 requires that exactly one absolute state-hash literal remain in
`tests/Sandata.Core.Tests/`. Task 52 records two golden baselines, each carrying
a state hash and an event hash. Read literally, task 52 breaks task 85's
guarantee four times over the moment it lands, and task 85 runs first.

The two rows are not actually in conflict; the rule needs its scope written
down. Task 85's canary is about the *operator fold* — a digest of a constructed
fixture that fires when any state field changes shape. Task 52's baselines are
about a *run* — a seed, a build, and an ordered order stream reproducing an
outcome. They guard different properties and the second is the entire point of a
golden replay.

The scoping rule, decided here so neither implementer has to guess:

- Task 85's criterion reads: exactly one absolute state-hash literal remains **in
  a `.cs` file** under `tests/Sandata.Core.Tests/`, and that one is
  `PreTask79cBaselineHash`. The search it records must be scoped to `.cs`.
- Task 52's baselines live in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`,
  which its own row already names, and `GoldenReplayTests` reads them from that
  file rather than declaring constants.
- `PreTask79cBaselineHash`'s comment says it is the only absolute pin in C# and
  that the golden replay's baselines live in the fixture JSON, so the next person
  to append a fold finds both halves of the picture from either end.

#### Task 81's stated "before" figure is stale

Task 81's row cites 65,679,126,648 bytes from task 51's first workload. The same
workload on the merged tree reports **65,782,309,192**. Task 81 records the
figure it measures on the tree it starts from — which will be the post-task-84
tree, not this one — as its "before", and cites this section only as evidence
that the row's number had already drifted.

The arithmetic in the first wave-12 audit is right and worth restating so nobody
re-derives it wrong: `HeadlessRunner.Execute` constructs and ticks **two**
simulations per benchmark tick, `left` and `right`, so 65.78 GB over 10,000 ticks
is 6.58 MB per benchmark tick and 3.29 MB per simulation-tick at 200 operators.

#### Task 54's project count, re-derived

`Hukbo.slnx` lists twelve projects: `Hukbo.Client`, `Hukbo.Core`,
`Hukbo.Diagnostics`, `Hukbo.Headless`, `Hukbo.Shared.Core`, `Sandata.Client`,
`Sandata.Core`, `Sandata.Headless`, `Hukbo.Client.Tests`, `Hukbo.Core.Tests`,
`Sandata.Client.Tests`, and `Sandata.Core.Tests`. The first wave-12 audit's
correction of task 54's "eleven" to twelve is confirmed independently.

#### Task 86's provisional-marker sweep overlaps two other rows

Task 86 is told to re-check every `<b>PROVISIONAL</b>` marker in
`SandataSimulation.cs` against design section 4. There are nine marked constants
and four marked prose passages, and two of the constants belong to other rows:
`FormationLookaheadWu` is task 84's to replace, and `ProvisionalDamagePerHitPoints`
is task 79d-2's to delete. Task 86 may correct their *comments* and may not
change their *values* or remove them.

Checked ahead of time so no implementer has to decide it: design section 4
supplies the body radius and, through the sprint figure, the movement step. It
supplies nothing for `VisionConeHalfWidthBam` — design section 6's row for
`VisionCone.Contains` specifies the predicate's shape and never a half-width —
and nothing for `FormationHalfWidthWu`, `FormationTrailStepWu`, or
`FormationLateralStepWu`, which design section 8 describes structurally without
numbers. Those four markers are correct as they stand and task 86 leaves them,
which is a finding to state rather than an omission to notice later.

#### The batch plan for the rest of the wave

| Batch | Tasks | Why |
| --- | --- | --- |
| 1 | 86 and 85 | Disjoint file sets, verified above; 85 is independent of every hash 86 moves |
| 2 | 84 | Stage 9; needs 86's radius for its invariant to mean anything |
| 3 | 81 | Whole-file allocation work; measures against the post-84 tree |
| 4 | 79d-2 | Stage 12, the constructor, and six call-site files |
| 5 | 52 | Golden baselines, recorded after every hash-moving task above |
| 6 | 54 | Documentation, after every number it records exists |

Task 55 is the canonical gate and is not delegated.

#### What is still nobody's to settle

Unchanged from the first wave-12 audit and restated because two of these rows sit
next to them: **what an autonomous squad wants** and **what decides an operator's
loadout** are both undesigned, and no implementer may fill either in. Task 79d-2
sits directly beside the second of those and adds damage keyed on the loadout
field task 79c defaulted; it does not gain the right to decide what sets that
field.

One new question joins the open list. Task 86 restates
`CollisionRules.DefaultBodyRadiusRaw`'s value in `Sandata.Core` with its
provenance in a comment, because design section 3 forbids `Sandata.Core` a
`ProjectReference` on `Hukbo.Core` and the constant lives in
`Hukbo.Core/Simulation/`, not in `Hukbo.Shared.Core`. That leaves the two games
carrying the same physical constant in two places. Whether that constant should
join the tier-1 shared assembly is a tier-2 extraction question, and design
section 3 is explicit that tier 2 becomes its own design document once Sandata's
collision has run a full gate. It has not. The duplication is recorded here as
the price of that, not resolved.

### Task 85 complete, and the redundant rewrite it refused to write — 2026-08-08

Merged at `fe233f1`. `OrderStateHashTests.PreTask61BaselineHash` and
`MissionEventFeedTests.PreTask76BaselineHash` are gone, and both properties they
guarded are now asserted by comparing two computed hashes.
`MissionStateTests.PreTask79cBaselineHash` remains as the single deliberate
canary, with a comment saying it is the only absolute state-hash literal in C#
and that task 52's golden-replay baselines belong in
`tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` rather than beside it.

The single-pin claim was re-checked by the integrating thread with a broader
search than the one the implementer ran — every `[0-9][0-9_]{6,}UL` literal in
`tests/Sandata.Core.Tests/**/*.cs`, rather than the two known values. Four
literals survive. One is `PreTask79cBaselineHash`. The other three are content
hashes, not state hashes: `AngleHouseFixtureTests.cs:136`'s `MapContentHash`,
`FirearmCatalogTests.cs:120`'s `FirearmRuleset.ModernTacticalV1.ContentHash`, and
`SandataRulesetTests.cs:61`'s `SandataRuleset.ContentHash`. Those three are
supposed to be absolute — a content hash pinned to a recorded value is the
mechanism by which a preset change becomes a new preset version, and it is the
opposite of the habit this task removed.

Test method counts per file are unchanged at 15, 7, and 26, confirmed by
counting `[Fact]` and `[Theory]` attributes on both sides of the merge. Nothing
was dropped in the rewrite. Both suites are green at 1,085 core and 199 client.

#### The one deviation, and why it was the right call

Task 85's row asked for `PreTask76BaselineHash`'s test to be rewritten as "the
state with an empty feed against the same state with events appended". The
implementer reported that writing exactly that would produce a tautological
duplicate of `StateHash_DoesNotMove_WhenTheEventFeedGainsEvents`, which already
sits in the same file and already does precisely that comparison — and the row's
own text names that test as the pattern to follow, so the row was asking for a
copy of the thing it was pointing at.

It replaced the pinned test with
`StateHash_DoesNotMove_AfterTheEventFeedEvictsPastItsRetentionCap` instead, which
exercises a path no existing test reached: the feed's 200-event retention cap
evicting older entries. That is a stricter statement of the same property — the
hasher does not read the feed, not even when the feed mutates by discarding.

This is the fourth time in this wave an agent has contradicted its brief with
evidence and been right. The implementer flagged it for review rather than
deviating quietly, which is the behaviour the brief asked for.

One claim in that report is worth stating more carefully than the report did. The
implementer described the new test as "independently breakable", but its own
break-proof shows the sibling test failing under the same injected break. The new
test is not independent of the sibling in that sense; what it adds is a distinct
code path through the retention cap, not a distinct failure mode. The coverage is
genuinely wider and the wording was stronger than the evidence.

#### Break-proofs, which are the actual acceptance criterion

A rewrite that cannot fail is worth nothing, so each property was broken in
`src/` on purpose, the failure recorded, and the break reverted:

| Property | The break | Failure |
| --- | --- | --- |
| An empty order queue folds nothing | `FoldOrderQueue`'s gate changed from `queue.Equals(OrderQueue.Empty)` to `ReferenceEquals(queue, OrderQueue.Empty)` | `Assert.Equal() Failure: Values differ / Expected: 3159438799659597482 / Actual: 16164427518677148266` |
| A queue with advanced counters is not the empty queue | the same gate changed to `queue.Orders.IsEmpty` | `Assert.NotEqual() Failure: Values are equal / Expected: Not 3159438799659597482 / Actual: 3159438799659597482` |
| `Compute` never reads the event feed | an unconditional `SandataHash.Fold(ref hash, state.EventFeed.Events.Length)` added to `Compute` | `Assert.Equal() Failure: Values differ / Expected: 15784416212425839146 / Actual: 13828696414860078466` |

`git diff --name-only main..HEAD -- src/` returned zero files on the branch
before the integrator's own edit, so every break was genuinely reverted.

#### A stale cross-reference the task exposed, fixed by the integrator

`SandataStateHasher.cs`'s remarks on the order-fold gate named
`OrderStateHashTests.StateHash_WithEmptyOrderQueueAndNoAssignments_MatchesThePreTask61Hasher`
and said it "pins that value directly". The implementer found it, correctly
refused to edit a production file outside its grant, and reported it. The
integrating thread made the edit at `e8eee11`: the remarks now name both
rewritten tests, say they compare two computed hashes rather than pinning a
literal, and cite this task.

That the comment survived at all is a small instance of the same pattern this
wave keeps finding. The guarantee the hasher documents was true; the artifact it
named as the proof of that guarantee had been the wrong kind of proof since task
61 wrote it.

### Task 86 complete, and the two reasons a shot can no longer miss — 2026-08-08

Merged at `272722c`. `CollisionBodyRadiusRaw` is the designed 4,352 raw — 4.25
world units, restated from `Hukbo.Core/Simulation/CollisionRules.cs:72`'s
`DefaultBodyRadiusRaw` with its provenance in the doc comment, because design
section 3 forbids `Sandata.Core` the `ProjectReference` that would let it share
the constant. `CollisionCellSizeRaw` is now `2 * CollisionBodyRadiusRaw` rather
than a second invented number, derived by the rule
`SandataCollisionGrid.ValidateBodyRadius` already enforces: a uniform grid's cell
must be at least one body diameter, or the three-by-three neighbour scan is
incomplete.

The seed-1 workload, re-run by the integrating thread rather than taken from the
implementer's report:

| | before (`4d42bc7`) | after |
| --- | --- | --- |
| state hash | `6D4AEA08BEFEFA92` | `BDD56EBD06F76674` |
| event hash | `270364E265A3A8A7` | `7C1B37876769DEC7` |
| survivors | 98 / 92 | 70 / 64 |
| allocated bytes | 65,782,309,192 | 48,636,057,432 |
| deterministic | true | true |

The survivor counts moved, which is what the row said to check for, and they
moved in the direction a real body radius predicts: larger bodies mean denser
contact and more shots that land. The allocation figure fell by 26 percent
without anyone touching allocation, which is worth knowing before task 81 opens —
the correctly sized collision cell emits a far smaller pair list than a cell
seventeen times finer than the objects it indexed.

`SandataRuleset.ContentHash` did not move and could not have: none of these
values is a ruleset field.

#### The headless fixture had to move with the constant

Recorded in this wave's second audit and confirmed by measurement here. The
fixture placed one operator per nav cell at a four-world-unit pitch, and the
implementer measured the true worst case as **2.0 world units** once the
`NextInt(3) - 1` jitter is allowed to pull two neighbours together — worse than
the audit's estimate, because the audit only counted the pitch.

`OperatorSpacingPitchCells` is now 3, derived rather than chosen: the 8.5
world-unit diameter rounded up to 9, plus 2 world units of worst-case jitter
shrink, is an 11 world-unit minimum pitch, which at `NavGrid.CellSizeWu` 4 is 3
cells and 12 world units actual. The grid grows from 15 cells square to 43 so the
same 200 operators still fit inside `GridRay.Traverse`'s bounds. Minimum pairwise
separation is now **10.0 world units**, pinned by
`HeadlessFixture_MinimumPairwiseSeparation_ClearsOneBodyDiameter` on the
magnitude rather than on "it changed".

The `SplitMix64` draw sequence is unchanged: two `NextInt` calls per operator, in
the same order, for the same operator count. Only the placement arithmetic moved,
which is what keeps this a spacing change rather than an RNG-stream change
wearing a spacing change's clothes.

`BuildOpenGrid` and `BuildInitialState` were promoted from `private` to
`internal` so the separation test calls the real placement code instead of
reimplementing it. No new `InternalsVisibleTo` grant was needed; the one
`Sandata.Headless` already carries for `Sandata.Core.Tests` covers it.

#### A shot can no longer miss, and the geometry is only half the reason

Task 79d-1's two miss tests were removed. That was the right call and the
implementer's stated reason was incomplete, in a way that matters for who has to
fix it.

**The reason it gave.** At the designed radius the target's subtended half-angle
grows roughly 136-fold. Solving the rifle's dispersion against that half-angle
puts the crossover at about 345 world units, and `ContactMemory.DetectRangeWu` is
256, so no geometry the sensing pipeline can reach draws a miss. The implementer
proved this exhaustively rather than analytically —
`SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange` walks every
whole range from 1 to 256 and asserts the maximum drawn magnitude never exceeds
the half-angle. The integrating thread re-derived the crossover independently
from `FirearmCatalog`'s constants and got the same 345.

**The reason it did not give, and the one that binds.**
`SandataSimulation.ProposeFire` resolves its `FirearmDefinition` at line 655 from
the private `DefaultFirearmId`, hoisted once outside the per-shooter loop, rather
than from the shooter's `OperatorState.Firearm`. Stage 11 reads the loadout at
line 446; stage 12 never learned about it. **Every shot in the game uses AK-47
dispersion regardless of what any operator carries.** So the impossibility is not
a property of the weapon model, it is a property of the rifle plus a wiring gap.

That distinction is load-bearing because the pistol's curve is far wider —
`PistolDispersionAtZeroWu` 64 and `PistolDispersionAtMaxWu` 512 over a
`PistolMaxEffectiveWu` of 320 put its crossover at roughly 157 world units, well
inside both `DetectRangeWu` and `PistolSingleBandMaxWu` 320. A pistol shooter
between about 160 and 256 world units misses readily. The miss path is not
unreachable in this game; it is unreachable in this build.

The integrating thread's first instruction to the implementer was to restore the
miss test with a pistol loadout, and that instruction was wrong for exactly the
reason above: setting `OperatorState.Firearm` cannot change stage 12's dispersion
while line 655 ignores it. The correction is recorded here rather than quietly
dropped, because it is the same error this wave has now made four times in four
directions — **a remedy stated in a brief is a claim, including when the
integrator is the one stating it.**

`EmitShotMissedEvent` and its branch at line 707 are now executed by no test.
Only negative assertions survive. That is a production path gone dark and it is
recorded as such rather than absorbed.

#### This amends task 79d-2

Task 79d-2 already owned replacing line 655's `DefaultFirearmId` with the
shooter's loadout — the audit named the line. It now also owns the coverage that
change restores:

> When stage 12 reads `OperatorState.Firearm`, restore a `RunTick`-level miss
> test with a pistol shooter at a range between roughly 160 and 256 world units,
> and restore task 79d-1's second event criterion for the miss half: a miss emits
> exactly one `ShotFired` and exactly one `ShotMissed`, observable in
> `MissionState.EventFeed`. `SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange`
> stays as the rifle's regression lock and is not weakened to accommodate the
> pistol.

Both obligations are written into the doc comment on that test, so the next
person to read it finds the outstanding work from the code rather than from this
document.

#### The rifle finding is a real gameplay result and needs its own decision

Set aside the wiring gap and the rifle result survives on its own: with the
designed body radius, a rifleman inside sensing range **cannot miss**. The
accuracy draw at stage 12 is decorative for every rifle in the catalog, and
design section 9's whole accuracy-interpolation apparatus — dispersion at zero,
dispersion at maximum, the effective-range clamp — has no observable effect on a
rifle engagement.

That is not obviously wrong as physics. A 0.53-metre target at twenty metres
genuinely does subtend more than a service rifle's dispersion cone. It is wrong
as *game design*, because nothing else currently modulates a shot: there is no
suppression penalty, no movement penalty, no stance term, and cover does not
reach the simulation until task 79d-2. A hit resolution with exactly one input
that never matters is the same degeneracy this wave found twice in the navigation
benchmark, arriving a third time in a different subsystem.

**This is a design decision and is not settled here.** It is added to the open
questions: what, besides range, should decide whether a shot lands. Nobody
implements an answer without one.

#### Two process notes

The implementer's constants table reported `CollisionCellSizeRaw`'s previous
value as 64 raw. It was 256, at `SandataSimulation.cs:919` on the base commit.
The derivation and the new value were right and the old value was not, which is
the third figure this wave that a report got wrong and a reading of the tree got
right.

Sent back once, the implementer did not do the redo. It re-read its own
transcript, concluded the task was already finished, and wrote a completion
section into `docs/plans/2026-08-07-sandata-scaffold.md` — a file its brief
explicitly withheld, and one the integrating thread was concurrently writing.
That commit was reverted at `cc98530` and the remaining work was done by the
integrating thread directly. Worth recording as a failure mode: **a resumed agent
that believes it is finished will find something to do rather than nothing, and
what it finds may be outside its grant.** A redo instruction to a resumed agent
should restate the grant as though the agent were cold.

### The third wave-12 audit, run before task 84 — 2026-08-09

Five rows are left: 84, 81, 79d-2, 52, and 54, with task 55 after them. Both
directions of the audit were run over those rows against `main` at `8fcd103`
before any agent was dispatched. The file-level half passed and is trivial this
time, because every remaining batch holds exactly one task: batches 2 through 6
of the previous audit's plan are serialised on `SandataSimulation.cs` and nothing
runs beside anything. The surface-level half produced six findings, two of which
change what task 84 can honestly deliver and one of which changes how its
constant is declared.

Every figure below was re-derived from the merged tree rather than carried
forward from the previous session's record. The graph tools `CLAUDE.md` section 8
mandates are again unavailable in this session — `tokensave` did not register —
so discovery ran through `Grep` and `Read` over the working tree. That is the
second consecutive session in which this has been true and it is recorded rather
than passed over.

**The baseline, measured now.** `./scripts/benchmark.ps1 -Game Sandata -Seed 1`
on `main` at `8fcd103` reports `stateHash` `BDD56EBD06F76674`, `eventHash`
`7C1B37876769DEC7`, 70 and 64 survivors, `outcome: Ongoing`,
`deterministic: true`, and `firstMismatchTick: null`. Every one of those matches
the figures recorded when task 86 merged.

`allocatedBytes` does not. This run reports **48,636,051,624** against the
48,636,057,432 recorded at task 86, a difference of 5,808 bytes over ten thousand
ticks. That is worth knowing before task 81 opens: the allocation figure is not
part of the determinism contract and is not bit-reproducible across runs, so
task 81's "before" and "after" are only meaningful as a magnitude, and an
acceptance criterion phrased as an exact byte count would be unsatisfiable.

#### The seed-1 workload contains no moving operators at all

This is the finding that matters most, because it decides what task 84 is
allowed to claim.

`HeadlessRunner.BuildInitialState` returns a `MissionState` whose `Groups` array
is `ImmutableArray<GroupPathState>.Empty` (`src/Sandata.Headless/HeadlessRunner.cs:411`),
and nothing anywhere else populates it: `AdvancePathService` drains that array
into `PathService.RequestPath` and its own remarks say plainly that no autonomous
destination-request source exists. The fixture also carries no `OrderAssignment`
values. So for all ten thousand ticks, every operator takes stage 9's autonomous
branch, finds `_pathService.GetCurrentPath` empty, and proposes its own current
position. **Nobody in the seed-1 workload has ever moved.**

That explains a fact recorded earlier in this wave without an explanation: task
86's survivor counts moved because the *fixture spacing* changed and therefore
every pairwise range changed, not because larger bodies collided during a run.
Nothing collides during that run, because nothing moves.

Three consequences, none optional:

- **Task 84 cannot move the seed-1 hashes and must not be asked to.** A step
  clamp applied to a proposal that already equals the operator's own position is
  a no-op on every tick of that workload. The correct acceptance criterion is
  that the hashes are *unchanged*, and an implementer who reports them moved has
  found a defect rather than a deliverable.
- Task 84 is provable only through `TickPipelineTests`, which is the same
  situation the previous audit recorded for cover in task 79d-2 and for the same
  underlying reason: the headless fixture exercises a narrow slice of the
  pipeline.
- Task 81's allocation table is unaffected by task 84's ordering. The two stay
  serial because they want the same file, not because one measures the other's
  output.

#### The per-tick step cannot be a `const`, because the tick rate is not one

Task 84's row says to "declare it a provisional `const`". It cannot be one.
`TickRate` is an instance property on `SandataRuleset`
(`src/Sandata.Core/Rules/SandataRuleset.cs:145`), not a compile-time constant, and
the derivation the previous audit fixed — `(80 * FixedPoint.Scale) / TickRate` —
reads it.

The correct shape, decided here so no implementer invents one, is the shape this
file already uses for every weapon timing. `SandataSimulation` converts
milliseconds to ticks through `_ruleset.TickRate` at lines 447, 448, and 485. The
movement step follows: a `const int` carrying design section 4's sprint figure of
80 world units per second, and a per-instance value derived from it and
`_ruleset.TickRate`, which at the shipped tick rate of 50 evaluates to **1,638
raw** exactly as the previous audit computed. No `SandataRuleset` field is added,
so `SandataRuleset.ContentHash` `8_955_292_433_887_190_872` does not move — the
constraint the row was protecting is satisfied by this shape as well as by a
`const`, and this shape additionally keeps the physical speed correct if the tick
rate ever changes.

#### The autonomous branch is unbounded for every operator that is not the leader

The record written on 2026-08-08 says task 79b made the autonomous branch honour
a per-tick bound. That is true of slot 0 and of nothing else, and the difference
decides how task 84's clamp has to be written.

`SlotTargets.ComputeTarget(arclength, leaderArclength, trailOffsetWu, gatedLateralOffsetWu)`
at `src/Sandata.Core/Simulation/SandataSimulation.cs:1625` is a pure function of
the group's polyline, the *leader's* projected arclength, and the proposing
operator's slot offsets. The proposing operator's own position is not an input.
The leader's target is bounded relative to the leader's own position because the
leader is the thing being projected; a trailing operator's target is bounded
relative to the leader, and an operator standing far from its formation slot
jumps into that slot in a single tick exactly as an ordered operator jumps to its
waypoint.

So the clamp is one clamp, of the vector from `(startXRaw, startYRaw)` to
`(desiredXRaw, desiredYRaw)`, applied once after both branches have chosen a
desired point and before the `MovementProposal` is constructed at line 1633. It
is not two per-branch clamps, and it is not reachable by tuning the lookahead.

`SandataSimulation.IntegerSqrt` at line 808 already provides the exact integer
square root this needs, in the same file, so no new mathematics is introduced and
design section 4's ban on `Math.Sqrt` inside `Sandata.Core` is untouched.

#### What becomes of `FormationLookaheadWu`, decided rather than left open

Task 84's row says to replace `FormationLookaheadWu` with the per-tick step
"rather than leaving two constants that both mean 'how far in one tick'". Once
the clamp exists, the two constants no longer mean the same thing: the clamp
means how far an operator may move in one tick, and the lookahead means how far
ahead of its own projection the leader *aims*. They also live in different
domains — the clamp is raw, the lookahead is an arclength in whole world units,
and 1.6 world units is not expressible there.

The decision, so that an implementer does not invent a rounding rule the way the
previous audit found one waiting to be invented: keep a lookahead, and derive it
as the per-tick step rounded **up** to the next whole world unit, which is 2. Any
value at or above the step is fully absorbed by the clamp and cannot make an
operator move further than the step allows; a value below the step would throttle
the leader beneath the designed sprint speed. Rounding up is therefore the only
direction that is safe in both senses, and the declaration must say that the
clamp and not this value decides distance travelled.

#### Task 79b's pinned polyline test needs rewriting, not re-pinning

`RunTick_UnassignedOperatorInGroupWithPublishedPath_FollowsTheBentPolylineNotTheGoal`
(`tests/Sandata.Core.Tests/TickPipelineTests.cs:1231`) spawns its operator at
world units (2, 2), asserts raw (7,168, 7,168) after the publish tick — a
displacement of about 7.07 world units in one tick — and asserts raw (19,456,
14,336) two ticks after that. At 1.6 world units per tick none of those three
assertions is reachable at anything near the current tick counts; walking the
bent polyline from x = 2 to x = 19 is on the order of twenty ticks.

The row's phrasing, "with its pinned raw coordinates updated if and only if the
constant differs from `FormationLookaheadWu`'s 8", understates the work. The tick
counts change as well as the coordinates. What must survive the rewrite is the
pair of properties the test exists to prove, both of which are stated in its own
remarks: the first published tick moves along the first segment toward (10, 10)
rather than to the goal at (26, 14), and a later tick finds the operator on the
corridor's own Y of 14 rather than on the spawn-to-goal beeline's Y of 10. The
new expected values are re-derived from the polyline's geometry, never copied out
of a failing run's actual output.

#### Task 79d-2's call-site count is stale in one cell

The previous audit's table of `SandataSimulation` construction sites is still
right about the seven files and is now wrong about one count.
`tests/Sandata.Core.Tests/TickPipelineTests.cs` holds **26** sites today, not 30;
task 86 removed the two miss tests. The totals on the merged tree are 39 sites
across seven files: `SandataGame.cs` 1, `HeadlessRunner.cs` 2,
`ClientOrderDoorTests.cs` 2, `HudComposerTests.cs` 1, `PathDrawToolTests.cs` 1,
`MissionEventFeedTests.cs` 6, `TickPipelineTests.cs` 26. The file list task 79d-2
was granted is unchanged and remains correct.

#### The surface-level half, in both directions

Every step named in a remaining "What" column is claimed exactly once: the
two-branch step clamp, the lookahead replacement, and the
`MovementSpeedRaw <= BodyRadiusRaw` invariant assertion belong to task 84; the
per-stage allocation table and the cuts it names belong to task 81; the caliber
damage table, the cover constructor parameter across seven files, the
`DefaultFirearmId` replacement at line 655, and the restored miss coverage belong
to task 79d-2; the two golden baselines belong to task 52; the measured figures
and the twelve-project layout belong to task 54; the gate belongs to task 55. No
step is claimed twice and none is unclaimed.

`scripts/verify.ps1` does carry a `-Game` parameter, defaulting to `Hukbo`
(`scripts/verify.ps1:14`), so task 55's second half is a command that exists
rather than one that has to be written first.

### Task 84 complete, and the integrator's own remedy that froze a leader — 2026-08-09

Merged at `5e1ed19`. Stage 9 now converts design section 4's sprint figure into a
per-tick raw step and clamps every operator's proposed movement to it, once,
after both the ordered and the autonomous branch have chosen a desired point.

`SprintSpeedWuPerSecond` is a `const int` carrying the design's 80 world units
per second; the per-tick cap is derived per instance as
`(SprintSpeedWuPerSecond * FixedPoint.Scale) / _ruleset.TickRate`, which is
**1,638 raw** at the shipped tick rate of 50. That shape was chosen by this
wave's third audit because `TickRate` is an instance property rather than a
compile-time constant, and it matches how the same file already converts every
weapon timing. No `SandataRuleset` field was added, and
`SandataRuleset.ContentHash` `8_955_292_433_887_190_872` did not move.

**The seed-1 workload's hashes did not move, and that is the deliverable rather
than a disappointment.** The audit predicted it: nothing in that fixture ever
moves, so a movement clamp is a no-op across all ten thousand ticks. The run
re-verified on `main` after the merge reports `stateHash` `BDD56EBD06F76674`,
`eventHash` `7C1B37876769DEC7`, 70 and 64 survivors, `outcome: Ongoing`,
`deterministic: true` — every value unchanged.

Counts re-derived from the merged tree rather than from any report:
`Sandata.Core.Tests` 1,088 to **1,092**, four tests added;
`Sandata.Client.Tests` 199, unchanged. `TickPipelineTests.cs` went from 25 to 29
`[Fact]`/`[Theory]` attributes and from 26 to 29 `new SandataSimulation(`
construction sites — which task 79d-2 should note, since its call-site table now
reads 29 for that file rather than the 26 this wave's third audit recorded or the
30 its second one did.

#### The lookahead decision in the brief was wrong, and it deadlocked a leader

The third audit decided that `FormationLookaheadWu` should become the per-tick
step rounded up to the next whole world unit, which is 2, reasoning that any
lookahead at or above the step is absorbed by the new clamp anyway. The
implementer built exactly that, discovered it stalls, reported the mechanism
precisely, and was right.

`PolylineArclength.Build` stores each segment's length as a truncated integer
square root — an (8, 8) segment measures 11, not 8·√2 ≈ 11.31 — and
`ProjectArclength` and `PolylineArclength.SampleAt` then divide by that truncated
length in opposite directions. The round trip from a world position to an
arclength and back therefore loses up to about two world units on a diagonal
segment. At a lookahead of 2 that loss consumes the entire lookahead: a leader
standing at (4, 4) projects to arclength 2, samples arclength 4, and lands back
on (4, 4) — its own position. The clamp has nothing to move toward and the
leader is frozen there for the rest of the mission. The fixed point existed
before task 84 and was invisible, because an unclamped commit stepped straight
over it in a single stride.

The lookahead is back at task 79b's 8, still marked provisional, now with the
floor it has to clear stated at its declaration rather than left to be
rediscovered.

**This is the fifth time in this wave that a remedy stated in a brief turned out
to be a claim, and the second time the integrating thread was the one stating
it.** The first was task 86's "restore the miss test with a pistol loadout",
which stage 12's hardcoded firearm made impossible. This one is worse in one
respect: it was written into the plan document as a decision taken so that no
implementer would have to invent a rounding rule, and the reasoning it carried —
"any value at or above the step is absorbed by the clamp" — reads as though it
had been checked. It had not. It was checked against the clamp and not against
the arclength arithmetic the value actually feeds.

#### Two further defects, found while correcting the first

**The clamp could exceed its own cap.** `ClampToMovementSpeed` divided by
`IntegerSqrt(distanceSq)`, which truncates, so the scale factor came out slightly
too large and a step could land just past the bound the method exists to enforce
— measured at 1,638.06 raw against a cap of 1,638, on a (-1,554, -518) step. The
divisor now rounds up when the square is inexact, which puts the error on the
undershoot side: a tick occasionally travels one raw unit less than it could, and
the bound is never broken. Both the implementer's own non-leader test and the
rewritten polyline test caught this the moment the lookahead changed, which is
the strongest argument for the audit's insistence on asserting the magnitude
rather than the difference.

**The invariant test could not fail.** `MovementSpeedRaw_NeverExceedsTheCollisionBodyRadius`
re-derived both constants locally — `(80L * FixedPoint.Scale) / TickRate` and
`(17L * FixedPoint.Scale) / 4` — so it would have passed no matter what
`SandataSimulation` actually used, which is the precise opposite of an invariant
test's purpose. It now reads `SprintSpeedWuPerSecond` and `CollisionBodyRadiusRaw`
out of the production type by reflection, the way this file's task 86 constants
test already did.

#### A test that pinned the deadlock as expected behaviour

The implementer's rewrite of task 79b's polyline test was named
`RunTick_UnassignedOperatorInGroupWithPublishedPath_WalksTheFirstSegmentThenHitsAQuantizationDeadlock`,
and its final assertion required the operator to still be at (4, 4) ten ticks
later. The analysis in its remarks was correct and genuinely useful. The test was
not: it asserted a degenerate outcome as the expected one, and in doing so it
discarded both properties the test had existed since task 79b to prove — that the
opening move lands on the first segment rather than on the goal, and that the
operator later sits on the corridor's own Y rather than on the spawn-to-goal
beeline's.

**This is the third time this wave that a measurement or an assertion could not
distinguish success from its own degenerate case**, after the navigation
benchmark's discarded search outcome and its randomised changed cells. It arrives
here in the sharpest form yet, because this one was not an oversight — the
degeneracy was understood, described accurately, and then written into a test
name.

The replacement, `RunTick_UnassignedOperatorInGroupWithPublishedPath_WalksTheBentPolylineAtTheDesignedSpeed`,
keeps both original properties, adds the per-tick displacement bound, and adds
the assertion a stall cannot satisfy: the operator must arrive at the goal. Its
first-move pin of raw 3,206 on both axes was derived from the polyline's own
arithmetic before the test was run — sample arclength 8 on a segment stored as
11 gives (7, 7), a displacement of 5,120 raw per axis whose magnitude is 7,240,
scaled to 2,048 + 5,120·1,638/7,240 = 3,206 — and the run then agreed with the
derivation.

#### Break-proofs

Each new assertion was broken on purpose in `src/`, the failure recorded, and the
break reverted. `git status --porcelain` was empty of `src/` changes afterwards
and the suite returned to 1,092 passing.

| Break | Result |
| --- | --- |
| `movementSpeedRaw` replaced with `int.MaxValue / 4`, disabling the clamp | 4 failures: the ordered, autonomous-leader, non-leader-slot, and rewritten polyline tests |
| `SprintSpeedWuPerSecond` changed from 80 to 213, the largest value the invariant still admits plus one | `MovementSpeedRaw_NeverExceedsTheCollisionBodyRadius` fails on the constant it reads by reflection, plus the three displacement tests |

An earlier attempt to break the clamp by returning early from
`ClampToMovementSpeed` could not even compile: `TreatWarningsAsErrors` turns the
resulting unreachable code into `error CS0162`. Worth knowing for the next
break-proof — the break has to change behaviour without leaving dead code.

#### One task this creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 87 | 12 | Make an arclength survive a diagonal round trip | `PolylineArclength.Build` stores each segment's length as `FixedPoint.IntegerSquareRoot`'s truncated result (`src/Sandata.Core/Squads/PolylineArclength.cs:134`), and `PolylineArclength.SampleAt` (line 208) and `SandataSimulation.ProjectArclength` then divide by that truncated length in opposite directions. Position to arclength to position loses up to about two world units on a diagonal segment, which is why `FormationLookaheadWu` cannot be reduced to the per-tick step without freezing a leader in place. Carry enough precision through the round trip that it is exact to within one raw unit — for example by storing cumulative lengths in raw fixed-point units rather than whole world units, which is the same representation every other distance in `Sandata.Core` already uses. No floating point, no `Math.Sqrt`, and the segment-length comparator's total order must not change. | `src/Sandata.Core/Squads/PolylineArclength.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` (`ProjectArclength` and `FormationLookaheadWu` only), `tests/Sandata.Core.Tests/SlotTargetsTests.cs`, `tests/Sandata.Core.Tests/TickPipelineTests.cs` | A test proves that for a polyline with axis-aligned, diagonal, and oblique segments, every vertex and every interior point sampled from an arclength projects back to that same arclength within one raw unit. A test proves a leader walks the bent polyline without stalling at a lookahead equal to the per-tick step, which is the reduction task 84 could not make. The seed-1 workload's hashes are unchanged, since nothing in that fixture has a path. | 84 | |

Task 87 wants `SandataSimulation.cs` and therefore cannot run beside task 81 or
task 79d-2. It is not on the critical path for either, so it is placed after task
54 rather than inserted into the remaining batch order, and the wave's ordering
stands: task 81, then 79d-2, then 52, then 54, then 55, with 87 after them.

### Task 81 complete, and the 81 percent it was not allowed to touch — 2026-08-09

Merged at `5d0e1f8`. The row's stated cause was measured before anything was cut,
exactly as this wave's first audit required, and the measurement changed what got
cut.

**The row was half right and half wrong, and both halves matter.** It blamed
stage 3's two `SandataCollisionGrid` constructions. The first audit rebutted that
on the grounds that the class already reuses its internal arrays across `Rebuild`
calls — which is true of the class and irrelevant to the call site, because
`RunTick` constructed two *fresh instances* every tick and so never reached the
reuse the class offers. Stage 3 was in fact the largest allocator inside the
grant, at 382,452 bytes per simulation-tick. The rebuttal was right about
`SandataCollisionGrid` and wrong about the consequence, and only a measurement
could have separated the two.

#### What was measured

`GC.GetAllocatedBytesForCurrentThread()` deltas around each of `RunTick`'s
fourteen stages, at 200 operators and seed 1, over 300 measured ticks after 50
warm-up ticks. The instrumentation was temporary and is gone: `git diff` on the
merged branch contains no `GetAllocatedBytesForCurrentThread` and no measurement
scaffolding, confirmed by searching the whole source tree rather than by reading
the report.

| Stage | Bytes per simulation-tick, before | After |
| --- | --- | --- |
| 3 — collision grids and the tick-start view | 382,452 | ~21,595 |
| 7 — path service | one `bool[CellCount]` per tick | 0 |
| 9 — movement proposals | 26,033 | 13,777 |

#### What was cut

- `SandataSimulation` now holds `_contactGrid` and `_cohesionGrid` as readonly
  fields built once in the constructor, and stage 3 rebuilds them in place. The
  cohesion grid's cell size depends only on `SandataRuleset.GroupCohesionRadiusWu`,
  which is fixed for the lifetime of a simulation, so sizing it once is sound.
- `AdvancePathService` reuses a `_pathBlockedCells` array instead of allocating
  `new bool[_navGrid.CellCount]` every tick. This one carries no stale-state
  hazard and does not merely claim not to: `PathService.Advance` takes its
  blocked cells as a `ReadOnlySpan<bool>`, so nothing downstream can write into
  the buffer and the compiler is what enforces it rather than a comment.
- `ComputeMovementProposals` sizes its `ImmutableArray.CreateBuilder` to
  `view.Count`, which is the exact upper bound since the loop adds at most one
  proposal per operator, so the builder no longer regrows by doubling.

`SandataCollisionGrid.cs` was in the grant and ended with a zero net diff — it
was edited only to break-proof the new test, and the break was reverted.

#### The result, and the honest shape of it

The seed-1 workload's `allocatedBytes` went from roughly **48.64 GB** to
**42.18 GB** over ten thousand ticks, re-run by the integrating thread on `main`
after the merge at 42,184,446,424 bytes. That is about a 13 percent cut, and
per simulation-tick at 200 operators it is 2.43 MB down to 2.11 MB.

Both hashes are unchanged — `stateHash` `BDD56EBD06F76674`, `eventHash`
`7C1B37876769DEC7`, 70 and 64 survivors, `deterministic: true` — which is the
proof that a pure allocation change changed no outcome.

Neither figure is quoted as an exact byte count anywhere, and the reason is
recorded in this wave's third audit: two runs of an identical tree differ by
thousands of bytes. The implementer's own report said 42,184,447,672 and the
integrator's re-run said 42,184,446,424, a difference of 1,248 bytes on the same
commit.

#### The two largest allocators are outside the grant, and the task stopped

This is the finding, and the row's three-file grant is why it is a finding rather
than a fix. The per-stage table named two allocations that dwarf everything task
81 was allowed to touch:

| Site | Bytes per simulation-tick | Shape |
| --- | --- | --- |
| `src/Sandata.Core/Navigation/LineOfSight.cs:59` | 1,761,332 | `new int[grid.Width + grid.Height + 1]` per call, at roughly 4,684 calls per tick |
| `src/Sandata.Core/Sensing/ContactMemory.cs:207` | 456,130 | `new ContactMemoryEntry[maxCount]` per operator per tick |

Both line numbers were confirmed against the merged tree rather than taken from
the report. The implementer stopped and reported instead of widening its own
scope, which is what the brief asked for and the second time in this wave an
agent has been right to refuse.

One caution about the arithmetic, stated rather than smoothed over. Those two
figures sum to 2.22 MB, which is more than the 2.11 MB per simulation-tick the
benchmark reports after the cuts, so the harness's denominator and the
benchmark's are not the same measurement — the harness instrumented one
simulation directly while the benchmark's figure covers the two `HeadlessRunner`
constructs and ticks per benchmark tick, plus everything outside `RunTick`. The
**ranking** is what this table is good for, and the ranking is unambiguous: line
of sight is the dominant allocator in the tick by a wide margin and contact
memory is second. The exact fractions are not, and no percentage from this
measurement should be quoted as a result.

#### One task this creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 88 | 12 | Cut the two allocators task 81 was not allowed to reach | `LineOfSight` allocates `new int[grid.Width + grid.Height + 1]` on every call at `src/Sandata.Core/Navigation/LineOfSight.cs:59`, roughly 4,684 times per tick, and `ContactMemory` allocates `new ContactMemoryEntry[maxCount]` per operator per tick at `src/Sandata.Core/Sensing/ContactMemory.cs:207`. Task 81 measured both and stopped at its grant boundary. Give each a caller-supplied scratch buffer rather than an internal cache: the ray buffer's bound is a pure function of the grid it is handed, and the contact-memory buffer's bound is the same `maxCount` the method already computes, so both can be passed in by the one stage that calls them. **No cache that outlives a tick, and no buffer stored on a type that reaches a snapshot or a hash** — `SIMULATION-GAME-STANDARDS.md` section 11 and `CLAUDE.md` section 9 both bind here. Re-measure per stage before and after with the same temporary harness discipline task 81 used, and delete the harness before committing. | `src/Sandata.Core/Navigation/LineOfSight.cs`, `src/Sandata.Core/Sensing/ContactMemory.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` (stages 3 and 5 only), `tests/Sandata.Core.Tests/LineOfSightTests.cs`, `tests/Sandata.Core.Tests/ContactMemoryTests.cs` | The seed-1 workload's `allocatedBytes` falls materially again, with the before and after both recorded as magnitudes rather than exact counts. Both hashes, both survivor counts, and `deterministic: true` are unchanged. A test proves a reused scratch buffer carries nothing from its previous use into the next result, asserted on the result's content rather than on the buffer's identity. | 81 | |

Task 88 wants `SandataSimulation.cs` and so cannot run beside task 79d-2 or task
87. It goes after task 54 alongside task 87, since neither is on the path to the
gate.

Remaining order for the wave: task 79d-2, then 52, then 54, then 55, with 87 and
88 after them.

### Task 79d-2 is split in two before dispatch — 2026-08-09

The second wave-12 audit widened task 79d-2's grant from one file to seven,
because the cover parameter is required rather than optional and there are
thirty-nine construction sites to update. Counting the row's own files as well,
the grant is twelve. That is too large for one brief, and the reason is recorded
rather than assumed: an implementer in an earlier session was killed by the
600-second watchdog while still exploring a large file, and this wave's two
completed rows each held two or three files and each still needed integrator
correction.

The split is by *reachability*, not by file count, so that neither half is a
parameter that does nothing:

| # | Task | Files |
| --- | --- | --- |
| 79d-2a | Stage 12 reads `OperatorState.Firearm` instead of the hoisted `DefaultFirearmId` at `SandataSimulation.cs:698`, and the miss coverage task 86 removed is restored on the pistol curve that change makes reachable | `SandataSimulation.cs` (stage 12 only), `TickPipelineTests.cs`, `MissionEventFeedTests.cs` |
| 79d-2b | The per-`CaliberFamily` damage table, cover as a required constructor parameter across all seven construction-site files, and the deletion of `ProvisionalDamagePerHitPoints` | `SandataSimulation.cs` (stage 12 and the constructor), `Combat/CaliberDamage.cs` (new), `Combat/DamageResolution.cs`, `TickPipelineTests.cs`, `DamageResolutionTests.cs`, `CoverRulesTests.cs`, plus the six call-site files the audit named |

They share stage 12 and would have been serial in either arrangement, so the
split costs one batch and buys two briefs an agent can finish. 79d-2a runs first
because 79d-2b's own acceptance criterion — two operators carrying different
caliber families dealing different damage on an identical hit — is unreachable
while stage 12 ignores the loadout, which is exactly the trap the second audit
warned about when it said keying a damage table off `DefaultFirearmId` would
satisfy the letter of the row while changing nothing.

Three facts confirmed against the merged tree before the split, since the tree
has moved twice since the second audit:

- The thirty-nine construction sites are still spread over seven files, and
  `TickPipelineTests.cs` now holds **29** of them, not the 26 the third audit
  recorded or the 30 the second one did. Tasks 84 and 81 added the difference.
- `FirearmDefinition` already carries a `Caliber` field of type `CaliberFamily`
  (`src/Sandata.Core/Weapons/FirearmDefinition.cs:95`), and `CaliberFamily`
  declares exactly eight members, `Cal762X39 = 0` through `Cal58X21 = 7`
  (`src/Sandata.Core/Weapons/CaliberFamily.cs`). So 79d-2b's "eight values, keyed
  the way design section 10 keys the audio report families" needs no new
  classification work and no edit to `FirearmDefinition` or `FirearmCatalog`.
- `CoverRules.ApplyToDamage` and `CoverRules.IsWithinProtectedArc` both take
  bare shooter and defender coordinates rather than an operator
  (`src/Sandata.Core/Combat/CoverRules.cs:155` and `:243`), and stage 12 already
  calls `ApplyToDamage` with `CoverState.NotInCover`. The work in 79d-2b is
  building a real `CoverState` from the map's `CoverRecord` values, not
  rewriting the call.

### Task 79d-2a complete, and the miss path is lit again — 2026-08-09

Merged at `fde1e85`. `ProposeFire` resolved one `FirearmDefinition` from the
private `DefaultFirearmId`, hoisted above the per-shot loop; it now resolves
`FirearmCatalog.Rows[(int)shooter.Firearm]` inside the loop, mirroring the shape
stage 11 has used since task 79c. `DefaultFirearmId` had no remaining consumer
and is deleted.

**`EmitShotMissedEvent` is executed by a test again.** Task 86 removed both of
task 79d-1's miss tests and recorded the branch as a production path gone dark;
it is dark no longer. Two tests replace them:

- `RunTick_PistolMissesAndRifleHitsAtTheSameTwoHundredWorldUnitRange` builds the
  identical fixture twice — same 200-world-unit range, same shooter entity id and
  therefore the same `Accuracy` draw, same target — and changes exactly one
  thing, the shooter's `Firearm`. The pistol misses and the rifle hits. That the
  only variable is the loadout is what makes it a test of this task rather than
  of the geometry.
- `RunTick_Miss_EmitsExactlyOneShotFiredAndOneShotMissedEvent` restores the miss
  half of task 79d-1's event criterion, as the exact-count mirror of the hit test
  that already existed in the same file.

Both are reached through `RunTick` and assert event counts and the target's
health, not that something differed. The fixture is honest: real positions 200
world units apart on a widened grid, no walls, and a genuine `Identified` contact
memory entry so the shooter engages. `ProposeFire` computes its range from the
operators' real positions, so nothing about the geometry is faked to reach the
branch.

`SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange` stands
unweakened as the rifle's regression lock, and its doc comment — which carried
both of these obligations so the next reader would find them from the code rather
than from this document — now records them as discharged and names the tests that
discharged them.

Counts re-derived from the merged tree: `Sandata.Core.Tests` 1,093 to **1,095**;
`Sandata.Client.Tests` 199, unchanged. `TickPipelineTests.cs` went from 30 to 32
`[Fact]`/`[Theory]` attributes.

The seed-1 workload is unchanged — `stateHash` `BDD56EBD06F76674`, `eventHash`
`7C1B37876769DEC7`, 70 and 64 survivors, `deterministic: true` — which is the
expected result and was reasoned in advance: `HeadlessRunner` leaves every
operator's `Firearm` at its default, so reading the field resolves to the same
definition the constant named. Had a hash moved it would have meant the workload
carries a loadout nobody set.

The break-proof was a whole-file swap to the pre-fix version rather than an
injected edit, which sidesteps the `error CS0162` problem task 84 hit: both new
tests failed with the pistol fixture recording a hit, and both passed again on
restore.

#### What this does not change

The rifle still cannot miss inside sensing range. Task 86's finding survives this
task intact: with the designed body radius the crossover sits near 345 world units
against a `DetectRangeWu` of 256, so for every rifle in the catalog the accuracy
draw remains decorative. What 79d-2a changed is that the *game* can now produce a
miss at all, on the pistol curve, where before no weapon could. **What besides
range should decide whether a shot lands remains an open design question and is
still nobody's to settle here.**

#### One process note

The implementer's report said `grep` showed zero remaining `DefaultFirearmId`
references in `src/`. Two remained: `src/Sandata.Core/Simulation/MissionState.cs:181`
and `tests/Sandata.Core.Tests/MissionStateTests.cs:589`, both describing
`OperatorState.Firearm` by reference to the constant that had just been deleted.
Neither is a `<see cref/>`, so nothing failed to compile and no test failed —
which is exactly why a search is the only thing that finds them. The integrating
thread made both edits, since both files were outside the row's grant.

That is the fourth figure this wave that a report got wrong and a reading of the
tree got right, after a constant's previous value, a test total, and an
allocation figure. The pattern is stable enough to plan around: **re-derive every
count and every "grep returns nothing" claim from the tree, in every case.**

### Task 79d-2b complete, and the hash that did not move for an honest reason — 2026-08-09

Merged. Cover reaches the simulation and damage is keyed on the shooter's
caliber, which closes the last two clauses of the original task 79d row.

`SandataSimulation`'s constructor takes `ImmutableArray<CoverRecord>` as a
required final parameter — required rather than optional with a default, for the
reason the second wave-12 audit gave and which has now bitten six times: an
omitted optional parameter would hand a caller a simulation in which cover
silently does nothing, and a required one turns every missed call site into a
compile error. All forty-three construction sites across seven files are updated.
`HeadlessRunner`'s two sites pass an empty set, because it synthesises its
mission and loads no map.

Stage 12 resolves the target's `CoverState` from those records —
`ResolveCoverState` finds the record whose rectangle contains the target,
breaking a tie on the lowest `CoverRecord.LineNumber` so the lookup carries the
total order design section 4 requires, and takes the posture from the target's
own `IsCrouched` flag. `CaliberDamage.RawDamage`, a new eight-value table in
`src/Sandata.Core/Combat/`, replaces the flat `ProvisionalDamagePerHitPoints`
that every hit dealt regardless of loadout. `grep -rn 'ProvisionalDamagePerHitPoints' src/`
returns nothing, including the prose references, and `FirearmDefinition` and
`FirearmCatalog` were not touched — the table keys on the `CaliberFamily` those
types already carried.

All eight values are marked `PROVISIONAL` at their own declaration and say that
no source supplies them. What the declarations do justify is the **relation**:
the two pistol calibers below every rifle caliber, the smaller-bore
intermediates below 7.62x39, and the two full-power rounds above it. A relation
can be defended from public documentation where an absolute number cannot, and
the comments claim only the former.

#### The seed-1 workload cannot observe this task, and its unchanged hash is not evidence

`stateHash` `BDD56EBD06F76674` and `eventHash` `7C1B37876769DEC7` are unchanged,
with 70 and 64 survivors. The brief told the implementer this task was expected
to move both, and that expectation was wrong for two compounding reasons:

- The workload carries no cover at all, since `HeadlessRunner` loads no map.
- Every operator in it carries the default `FirearmId.Ak47`, whose caliber is
  `Cal762X39`, and the table's value for that caliber is 25 — the same number the
  deleted flat constant carried.

The second is worth stating carefully, because the brief explicitly forbade
choosing a value in order to keep the digest still. That is not what happened:
25 was kept as the anchor the rest of the scale was built around, and it sits
mid-table among values from 10 to 30. But the effect is the same, and the honest
conclusion is the one that matters: **the unchanged hash says nothing about
whether this task works.** The three `RunTick` tests are the entire evidence, and
that is the situation the second audit predicted when it recorded that cover
would be provable only through `TickPipelineTests`.

#### The three tests, and why each holds only one thing variable

- `RunTick_TwoShootersOfDifferentCaliberFamilies_DealDifferentDamageOnAnIdenticalHit`
  runs the identical fixture twice — same 100-world-unit range, same shooter
  entity id and therefore the same `Accuracy` draw, same target — and changes only
  the firearm, an AK-47 against an M4. This is the criterion that catches a
  damage table keyed off a constant instead of off the loadout.
- `RunTick_TargetInsideACoverArc_TakesReducedDamageWhileAFlankingShotIgnoresTheCover`
  places the same rectangle over the target twice and differs only in the arc.
  Neither half encodes a bearing convention: the protecting record uses the
  `ArcHalfBam` of 32,768 that `CoverRecord`'s own documentation defines as
  covering "from every direction", and the bypassing record uses a one-BAM arc
  centred a quarter turn from either bearing a shooter due east can occupy under
  any convention.
- `RunTick_CrouchedTargetInCover_TakesTheCrouchedReductionRatherThanTheStandingOne`
  exists because without it the posture half of the lookup could be hardcoded to
  standing and every other cover assertion in the file would still pass.

Every expected value is computed from `CaliberDamage.RawDamage` and from
`CoverRules`' own published percentages rather than written as a literal, and
each target's full health is read from the fixture before the tick rather than
assumed, so none of the three can drift into passing vacuously.

Counts re-derived from the merged tree: `Sandata.Core.Tests` 1,095 to **1,098**;
`Sandata.Client.Tests` 199, unchanged.

#### Break-proofs

| Property | The break | Result |
| --- | --- | --- |
| Damage is keyed on the shooter's caliber | stage 12's `CaliberDamage.RawDamage(definition.Caliber)` replaced by the literal 25 | the caliber test fails, alone |
| Cover reaches stage 12 from the map | `ResolveCoverState(...)` replaced by `CoverState.NotInCover` | both cover tests fail |
| Posture comes from the target's own flag | `Posture:` hardcoded to `CoverPosture.Standing` | the crouched test fails, alone |

Each break was reverted and the suite returned to 1,098 passing.

#### The implementer stalled, and what that cost

The agent hit the six-hundred-second watchdog with **nothing committed** —
production work spread uncommitted across nine files, in an intermediate state
where `CaliberDamage.cs` existed and compiled but nothing consumed it, and stage
12 still read the flat constant it was meant to delete. The suite was green at
1,095 at that moment, which is exactly the trap: a green suite on a half-finished
task looks identical to a green suite on a finished one, because the tests that
would have failed had not been written yet.

The integrating thread finished it directly rather than resuming the agent,
following this repository's own record that a resumed agent may not do the redo.
What the integrator wrote: the caliber wiring at stage 12, the deletion of the
constant and of the two prose references to it that survived inside
`CaliberDamage.cs`'s own documentation, the fixture's cover and posture
parameters, all three tests, and the break-proofs.

Two process lessons, both already in this document in other forms and both
sharper here:

- **"Commit as you go" was in the brief and was ignored for the third time this
  wave.** The instruction is not enough on its own. A brief that depends on
  incremental commits should say what the first commit is and require it before
  anything else begins — this one did say "do the call sites first, as their own
  commit", and the agent did the call sites first and committed nothing.
- **A green suite is not a finished task.** The only reliable check is the one
  the wave has been running all along: re-derive the counts. 1,095 was the base
  count, and a task that adds no test has added no evidence.

### Task 52b complete, and the event hash both baselines share — 2026-08-09

Merged at `ab57dc0`. `GoldenReplayTests` pins two seed-1 baselines, and every hash
literal lives in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` rather
than in the `.cs` file, so task 85's guarantee — exactly one absolute state-hash
literal in C# under `tests/Sandata.Core.Tests/` — survives intact. A search of the
test file for a sixteen-character hex literal returns nothing.

Both missions are the same fixture: seed 1, eight operators in a four-against-four
packing built through `HeadlessRunner.BuildOpenGrid` and
`HeadlessRunner.BuildInitialState`, ticked forty times. Forty ticks rather than
the workload's ten thousand, because Sandata's core suite already runs about
forty-five seconds and a second ten-thousand-tick run would have doubled it. The
two tests cost about ten seconds; the suite now runs about fifty-six.

The second mission submits two real orders through `SandataSimulation.SubmitOrder`
at tick 0 — a `MoveAlongPath` for one faction's operator and a `Hold` for the
other's — both accepted. Submitting them through the door rather than injecting
them into state is what makes the baseline a statement about the order layer at
all.

**The failure message names the first mismatch tick**, which the row required and
which is the difference between a golden test that costs a minute to diagnose and
one that costs a day. A real failure, captured during the break-proof:

```
Golden replay diverged: state hash at tick 0 was 2EE5D7F78DBCAB16,
expected D81CEB0CBE66B3D8 (first mismatch tick = 0).
39 further ticks were not compared.
```

Neither mission is degenerate, and this was asserted rather than assumed: each
run emits sixteen events including eight `ShotFired`, and three operators end
below full health. The order baseline additionally re-runs an empty-order
companion and asserts the two state hashes differ, which is the direct proof that
the orders changed the run rather than being accepted and ignored.

Break-proofs, both reverted with an empty `src/` diff afterwards: folding an extra
value into `SandataStateHasher.Compute` fails both baselines at tick 0, and
folding one into `MissionEventFeed.FoldEvent` fails the event-hash assertion while
every per-tick state hash still matches — which is the two-independent-hashes
property demonstrating itself.

#### Both baselines carry the same event hash, and that is a finding

`FinalEventHashHex` is `74E008E940AB05A5` for the empty-order mission and for the
order mission alike, while their state hashes differ from tick 0 onward. The
implementer noticed this and reported it rather than passing over it.

It is not a bug in the test, and the cause is structural rather than
coincidental. `MissionEventKind` declares exactly four members — `OrderRejected`,
`ShotFired`, `ShotHit`, `ShotMissed`. **An order that is accepted emits no event
at all**, and movement emits none either, so the only way an order can reach the
event stream is by changing which shots resolve. Over forty ticks, with the
per-tick movement clamp at 1.6 world units, one operator walking a short polyline
does not change any shot's outcome — every rifle inside sensing range hits
regardless.

Two consequences, stated rather than filed away:

- **The order baseline's event half proves nothing that the empty one does not.**
  Its state half does the work. Design section 16's requirement for two baselines
  is met, but it is met on one of the two hashes, and a future reader comparing
  the fixture's two `FinalEventHashHex` values should know why they are equal
  rather than assume the file is wrong.
- **A player's accepted order leaves no trace in the authoritative event stream.**
  Design section 16 promises that rejection is observable and says nothing about
  acceptance. Whether an accepted order should emit an event is a design question
  — the event feed is what a replay and a spectator read, and an order layer whose
  successful commands are invisible to it cannot be reconstructed from the stream
  alone. **This is added to the open questions and is not settled here.**

One deviation, correctly reported: `HeadlessRunner.BuildMission` is `private
static`, unlike `BuildOpenGrid` and `BuildInitialState` which task 86 promoted to
`internal`. Rather than widen a production type's visibility from outside its
grant, the implementer wrote a local equivalent in the test file and said so. That
is the right call at a grant boundary and it leaves a small duplication worth
folding away if a third test project ever needs the same mission.

`Sandata.Core.Tests` is at **1,100** and `Sandata.Client.Tests` at 199, both
re-derived from the merged tree.

### Task 52a complete, and the clause it cannot prove — 2026-08-09

Merged. `DeterminismEquivalenceTests` adds four relational tests, none of which
pins an absolute hash, so task 85's single-C#-literal guarantee is untouched and
the golden replay's fixture JSON remains the only place a Sandata run's digest is
written down.

| Test | Held constant | Varied | Asserted |
| --- | --- | --- | --- |
| Same-seed repeat, in process | seed, mission, fixture, 60 ticks | nothing — two independently constructed simulations | per-tick state hash, event hash, the whole ordered event stream by sequence, outcome |
| Cold cache | seed, mission | one simulation runs 0–59 continuously; a second, freshly constructed one — new nav grid, new clearance field, new collision grids — runs only 30–59 | the two agree tick for tick from the midpoint |
| Save and resume | seed, mission, one authored `MoveAlongPath` order | reference never stops; the other snapshots at the midpoint and resumes in a brand-new simulation | tick-for-tick agreement, plus the order's `PathNodes` and `CurrentNodeIndex` surviving the round trip |
| Logging off versus `trc` | seed, mission, fixture | `DiagnosticLog.Disabled` against `LogLevel.Trace` writing to an in-memory writer | tick-for-tick agreement, plus the writer actually produced output — so the logged run genuinely logged |

Every one of them calls `AssertRunWasActive`, which requires events emitted and
total operator health below its starting value, so none can pass by both sides
doing nothing. That check exists because this wave has now thrown away three
measurements whose degenerate case and success case were indistinguishable.

`Sandata.Core.Tests` is at **1,104** and `Sandata.Client.Tests` at 199, both
re-derived from the merged tree. The Sandata core suite now runs about a minute.

#### The fresh-process clause, reported rather than invented

The row's fifth clause asks for a same-seed repeat "in a fresh process". The
implementer confirmed what this wave's audit suspected: nothing in either test
project spawns a process, and `tests/Hukbo.Core.Tests/DeterminismTests.cs` does
not either. It declined to introduce one and said so. That is correct — a process
launch drags the wall clock, the filesystem, and a build layout into a unit
suite, and the clause is already discharged by
`./scripts/benchmark.ps1 -Game Sandata -Seed 1`, which is a fresh process by
construction and is task 55's evidence.

#### Two break-proofs failed to break, and that is the useful part of the report

The implementer tried three breaks and reported all three honestly:

| Break attempted | Result |
| --- | --- |
| A construction-time bias baked into the clearance field | did not fail — no fixture in the file populates `MissionState.Groups`, so the clearance field's squad-leader consumer is never reached |
| Removing `SandataCollisionGrid`'s clear-before-insert | did not fail — `GeneratePairs` clears `_pairs` itself and the hash-occupancy dedupe masks the rest |
| XOR-ing wall-clock entropy into `ComputeStateHash` | all four tests failed with exact hash mismatches |

The second is a genuine finding about the production code and matches what task
81 found from the other direction: `SandataCollisionGrid` has **two** independent
clears, so neither one alone is load-bearing. The third is a coarse break — it
fails everything, including the trivial repeat test — so it establishes that the
suite detects nondeterminism in general and not that any individual test binds its
own specific property.

#### The save-and-resume test does not prove what design section 4 asks it to

This is the finding, and it was reached by the integrating thread writing a fifth
test, discovering that the test passed vacuously, and deleting it rather than
merging it.

The row calls save-and-resume "the only proof that paths are genuinely derived".
Design section 4 states the rule precisely: published path polylines are derived
and excluded from the snapshot, "the *request* is authoritative and snapshotted;
the *result* is not. On resume, every outstanding and every published path is
recomputed from its stored request record before the first tick executes."

The merged test snapshots an **authored** order. An authored polyline is stored
state — design section 16 is explicit that it is authoritative, not derived — so
it round-trips as data and the test proves the round trip works. It says nothing
about a recomputed path, because **no test in the file populates
`MissionState.Groups` at all**, so no autonomous path is ever requested and none
is ever published.

The attempted fifth test populated `Groups`, ran past `PathLatencyTicks`,
asserted the request had published, snapshotted, and resumed in a fresh
simulation. It passed. It was still worthless, and a probe is what showed why:
entity 1 moved from raw (3,072, 2,048) to (4,095, 3,327) over the first thirty
ticks and then **did not move at all** for the remaining thirty. Tick-for-tick
agreement after the snapshot was agreement between two runs that were both
standing still. The probe that settled it compared the operator's position at the
snapshot tick against its position at the end, and returned `moved=False`.

That is the same shape as everything else this wave has caught, arriving one
level deeper: not a test that asserts "it changed", but a test whose *scenario*
looks active and is inert in exactly the window the assertion covers.

Two things follow, and the second is the more interesting one.

- **The derived-path resume rule is unproven.** It is not disproven; nothing
  suggests the recomputation is wrong. It simply has no test, and task 52 should
  not be recorded as having supplied one.
- **An operator with a freshly published group path walks about one world unit
  and stops.** That is a second finding, independent of the test, and it was not
  looked for. Whether the cause is the arclength quantization task 87 already
  names, or derived squad grouping moving the operator out of the group whose
  path was published, or the clearance gate collapsing the formation, is not
  known. It is worth knowing before anyone tunes movement.

#### The tasks these create

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 89 | 12 | Find out why an operator with a published group path stops after one world unit | A fixture that populates `MissionState.Groups` with an outstanding request, runs past `SandataRuleset.PathLatencyTicks`, and ticks on, moves its operator roughly one world unit in the first thirty ticks and then holds position for the next thirty — measured, not inferred. Candidate causes, none confirmed: the diagonal arclength quantization task 87 names; `SquadGrouping` deriving a different `GroupId` once the operator has moved, so `PathService.GetCurrentPath(slot.GroupId)` no longer finds the published path; `FormationCollapse` gating the slot to single file; or the leader projection reaching the end of a short polyline. **Diagnose before changing anything**, and report the cause rather than patching the symptom. | `tests/Sandata.Core.Tests/TickPipelineTests.cs`, and whichever single production file the diagnosis names, reported before it is edited | The cause is named with file and line and reproduced by a test. If it is a defect, the fix lands with a test asserting sustained movement over a stated number of ticks, on the displacement rather than on "the position changed". If it is correct behaviour, the reason is written at the code that produces it. | 84, 52a | |
| 90 | 12 | Prove a published path is recomputed on resume, not restored | Task 52a's save-and-resume test snapshots an authored order, whose polyline is stored state; design section 4's rule is about the *derived* polyline of an autonomous group path, and no test exercises it. Once task 89 has an operator that keeps walking a published path, snapshot mid-walk and resume into a fresh `SandataSimulation`, which begins with an empty `PathService`. The resumed run must match the never-stopped one tick for tick **through a window in which the operator is demonstrably still moving** — assert that movement inside the compared window, not merely at some point in the run, or the test repeats this one's mistake. | `tests/Sandata.Core.Tests/DeterminismEquivalenceTests.cs` | The comparison window contains real movement, proved by an assertion inside the test rather than by a probe outside it, and the resumed run matches. Breaking the recomputation makes it fail, with the failure recorded. | 89 | |

### Task 54 complete, and the four documents that pointed at files which no longer exist — 2026-08-09

`docs/development/testing.md`, `README.md`, `AGENTS.md`, and `CLAUDE.md` now
describe a repository that builds two games. Every figure below was re-derived
from the merged tree during this session rather than carried forward, and doing
that is what caught most of what follows.

The audit was run in both directions before anything was written. Task 54's four
documents are claimed by nothing else; task 55 claims no file at all; tasks 87,
88, 89, and 90 all want `SandataSimulation.cs` or its test file and are serial
with one another and with nothing here. Every step in task 54's "What" column is
claimed exactly once, and the row's own count was the first thing the audit
found wrong.

#### The row says eleven projects and there are twelve

`Hukbo.slnx` lists twelve, a search for `.csproj` under `src` and `tests`
returns twelve, and the layout blocks now list twelve: `Hukbo.Shared.Core`,
`Hukbo.Core`, `Hukbo.Client`, `Hukbo.Headless`, `Hukbo.Diagnostics`,
`Sandata.Core`, `Sandata.Client`, `Sandata.Headless`, and the four test
projects.

#### Two documents pointed at source files that do not exist

`CLAUDE.md` section 5 and `AGENTS.md`'s non-negotiables both told the reader to
use `Hukbo.Core/Determinism/SplitMix64.cs` and
`Hukbo.Core/Mathematics/FixedPoint.cs`. Neither path exists.
`src/Hukbo.Core/Mathematics/` is not a directory at all, and
`src/Hukbo.Core/Determinism/` holds only `StateHasher.cs`; both files moved to
`Hukbo.Shared.Core` when the tier-1 extraction landed in an early Sandata wave.

Nothing failed to compile and no test went red, because a path inside a Markdown
bullet is not checked by anything. This is the same shape as task 79d-2a's two
surviving prose references to a deleted constant, and it is one more figure in
this wave that a reading of the tree corrected. Both now read
`src/Hukbo.Shared.Core/...` and name the project the rule actually lives in.

#### A test cited a sentence in `CLAUDE.md` that had never been written

`GoldenReplayTests`' class remarks justified its eight-operator, forty-tick
fixture by quoting `CLAUDE.md` section 4: "Sandata's core suite already takes
about 45 seconds". `CLAUDE.md` contained no such sentence and, before this task,
did not mention Sandata anywhere at all. The quotation was of a document that
task 54 had not yet written.

The figure was also stale twice over — the suite has been recorded at 45, then
56, then about 60 seconds by three different sessions, and measures 37.77
seconds warm today. The remarks now point at section 4 without quoting a number,
so the citation is true and cannot rot the same way again. That edit is one doc
comment in a test file outside the row's four-document grant, made by the
integrating thread and recorded here rather than left to be found later.

#### `main` moved underneath this session, and a test count moved with it

The session began at `de687b4`. Partway through, another session merged
`codex/attack-animation-v2`, and `main` became `9e28a65` while this work was in
progress. The uncommitted documentation edits survived intact: `git diff HEAD`
on `docs/development/testing.md` reports insertions and zero deletions, so the
other session's sixty-one lines of attack-animation smoke observations are all
present underneath this task's additions.

What did not survive was a figure. `Hukbo.Client.Tests` was measured at **3,152**
before the merge and is **3,270** after it, and the first figure had already been
written into two of these documents. The 118 new tests are the attack-animation
work's, not this task's.

The false hypothesis was worth eliminating rather than assuming: a documentation
edit that changed test *discovery* would have been a far more interesting
problem. It was ruled out by listing discovered tests, then stashing the four
documents, listing again, and comparing — 3,105 discovered entries both ways,
identical. Discovery is unaffected by these documents; the count moved because
the tree did.

**Re-derive a count immediately before writing it down, not once at the start of
a session.** A figure measured an hour earlier in a repository with concurrent
sessions is a figure from a different commit.

#### Sandata's suite is slow in one place, not seven, and it is not half

Three sessions have carried the sentence "roughly half of it is one test case"
alongside "tasks 82 and 83 added seven test cases that run the benchmark". The
first half understates it and the second half is wrong about which tests those
are.

Per-test durations from a full Release run:

| Test | Duration |
| --- | --- |
| `NavBenchmarkOptionTests.ChangedCellRunStaysAboveTheSuccessfulSearchFloorThroughoutTheRun(tickCount: 2000)` | **36 s** |
| the same theory at `tickCount: 200` | 3 s |
| the same theory at `tickCount: 1` | 0.152 s |
| `DeterminismEquivalenceTests.AFreshlyConstructedSimulation_MatchesAnAlreadyRunningOne...` | 0.121 s |
| every other test in the suite | under 0.09 s |

The whole suite is 37.77 seconds warm. **One `InlineData` value on one theory is
thirty-six of them.** Nothing else in the file is expensive: the golden replay
tests task 52b was careful to keep cheap cost 44 and 39 milliseconds, and the
four determinism equivalence tests together cost about a fifth of a second.

#### The decision task 55 carried, taken

**The benchmark test cases stay in the suite. They do not move to `tools/`.**

The reasoning that made this look like an architecture question does not survive
the measurement. There is no cluster of seven expensive cases trading off
against gate speed; there is one assertion endpoint. It is a regression lock on
a defect this same wave found and fixed — under the old per-tick fresh-draw
behaviour the successful-search fraction decayed as the tick count grew, and
`tickCount: 2000` is the point at which that decay was unmistakable. Thirty-six
seconds on a locally run gate that already spends over a minute elsewhere is
affordable, and moving a regression lock out of the suite to buy it back would
trade a guard that runs for a hand-run one nobody runs.

**What is deliberately not decided here** is whether `InlineData(2000)` could be
reduced to a cheaper endpoint without weakening the lock. It probably could —
two hundred ticks is already two hundred mutation steps against a 160-by-180
grid — but "probably" is exactly the kind of claim this wave has been wrong
about repeatedly. Proving it means reverting the defect and confirming the
smaller endpoint still fails, which is a break-proof, which is a task. It is
filed as task 91.

#### What the documents now say, and one thing they deliberately do not

`CLAUDE.md` gained a Sandata subsection in section 1, Sandata naming in section
2, a twelve-project layout with the reference graph and the tier-1 rule in
section 3, the `-Game` parameter in section 4, Sandata's determinism additions
in section 5, and four Sandata entries in section 9. `AGENTS.md` gained the same
material in its standalone form, because it must be complete for tools that
never read `CLAUDE.md`. `README.md` gained a section on the second game, the
`-Game` commands, a twelve-project architecture diagram, four enforced
boundaries rather than two, the Sandata documentation links, and a corrected
test badge.

`docs/development/testing.md` gained a Sandata section carrying the seed-1
baseline, the suite counts warm and inside the gate, task 53's audio-pool and
navigation-matrix figures, the two largest remaining allocators, and the eight
smoke rows.

**Task 53's raw output is cited to this plan document and not to a file.** Both
measurement runs wrote under `artifacts/`, which `.gitignore` excludes, so those
files exist on one workstation and in no clone. `SourceHygieneTests` carries a
fact written for exactly this mistake in Hukbo's render baselines; rather than
repeat the mistake in a form that fact does not scan for, the new section names
the section titled "Task 53 complete, after tasks 82 and 83" as the transcript
of record and says plainly that no such file travels with the repository.

**Every one of the eight Sandata smoke rows is `PENDING` and none may be flipped
by an agent.** SD-5, the sustained-automatic-fire audio row, cannot be attempted
at all: Sandata ships no sound files, its catalog is 106 slots expanding to 524
variants at roughly 104,800 ElevenLabs credits, and that spend is not
authorised. It stays `PENDING` with the reason stated rather than becoming
`BLOCKED`, because the blocker is upstream of the smoke run and the row must not
be quietly forgotten once the audio question is answered.

The allocation figure is recorded as a magnitude. Two further runs this session
reported 42,184,440,712 and 42,184,446,456 bytes against the previously recorded
42,184,446,424 and 42,184,447,672. All four mean "about 42.18 GB".

#### One task this creates

| # | Wave | Task | What | Files (explicit paths) | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 91 | 12 | Find the cheapest endpoint that still locks task 83's connectivity defect | `NavBenchmarkOptionTests.ChangedCellRunStaysAboveTheSuccessfulSearchFloorThroughoutTheRun` costs thirty-six seconds at `InlineData(2000)` and is, on its own, the overwhelming majority of Sandata's core suite runtime; the same theory costs three seconds at 200 and 0.15 seconds at 1. Task 55 decided the case stays in the suite and explicitly did not decide whether its endpoint can shrink. Restore task 83's per-tick fresh-random-draw behaviour behind a temporary local edit, run the theory at descending endpoints, and find the smallest tick count at which the successful-search fraction still falls below the floor. If a materially cheaper endpoint fails under the reverted defect, replace 2000 with it and record both the failing output at the new endpoint and the passing output after the revert. If nothing below 2000 fails, say so and leave the case exactly as it is — **a cheaper endpoint that cannot detect the defect is worse than a slow one that can**, and reporting "no cheaper endpoint works" is a successful outcome for this task. | `tests/Sandata.Core.Tests/NavBenchmarkOptionTests.cs`, and `src/Sandata.Headless/NavBenchmark.cs` for the temporary revert only, which must not appear in any commit | The smallest defect-detecting endpoint is named with its measured failure output, or the absence of one is stated with the endpoints tried. The merged branch contains no trace of the reverted defect. The suite's total runtime before and after is recorded. | 55 | |

Task 91 wants neither `SandataSimulation.cs` nor its test file, so it is the one
remaining row that could run beside task 87, 88, 89, or 90.

### Task 55 complete — both gates run, both green — 2026-08-09

Run by the integrating thread on `main` at `9e28a65`, not delegated, both
outputs pasted in full into `docs/development/testing.md`.

`./scripts/verify.ps1` with no flags, exit code 0: prerequisites and locked
restore, formatting, a Release solution build with zero warnings and zero
errors, `Hukbo.Core.Tests` 2,376 of 2,376 passing in 29.49 seconds,
`Hukbo.Client.Tests` 3,270 of 3,270 passing in 2.07 seconds, and the 200-agent,
10,000-tick, seed-1 workload reporting `stateHash` `1B73FC5923879AA0`,
`eventHash` `AC55684F24D39344`, `outcome` `Faction1Victory`, survivors 0 and 6,
and `deterministic: true`. **Both recorded Hukbo baseline hashes are
unchanged**, which is the first time they have been confirmed in four sessions.

`./scripts/verify.ps1 -Game Sandata`, exit code 0, the same five stages against
Sandata's two suites and Sandata's workload: `Sandata.Core.Tests` 1,104 of 1,104
passing in 1.0803 minutes, `Sandata.Client.Tests` 199 of 199 passing in 0.5005
seconds, and the seed-1 workload reporting `stateHash` `BDD56EBD06F76674`,
`eventHash` `7C1B37876769DEC7`, `outcome` `Ongoing`, survivors 70 and 64, and
`deterministic: true`. **This is the first recorded run of Sandata's gate.**

The two runs are recorded as two results and are never added together. A green
default gate is not evidence about Sandata, which is now stated in all four
documents and in this plan.

One figure worth carrying: Sandata's core suite costs 1.08 minutes inside the
gate against 37.77 seconds warm. Both are real, and the gate figure is the one
to quote, because it is what the suite costs immediately after a full Release
build.

`Hukbo.Client.Tests` was re-run after the two gate-result sections were appended
to `docs/development/testing.md`, since `SourceHygieneTests` reads that file:
3,270 of 3,270 passing.

### Task 87 complete, and the row's own precision figure was unreachable — 2026-08-09

Merged. Every length `PolylineArclength` produces is now raw fixed point. The
vertices `Build` is handed are still whole world units, because that is what
`PathService` publishes; everything derived from them is scaled by
`FixedPoint.Scale` **before** the square root rather than after it. That single
change moves the truncation from one part in eleven to one part in forty-eight
thousand.

`ArclengthSample`'s coordinates, direction and length, `SlotTargets.ComputeTarget`'s
offsets and result, and `ProjectArclength`'s query position and return value all
moved with it. The published polyline itself is untouched.

#### The grant was too small for the row's own acceptance criterion

The row granted `PolylineArclength.cs`, `SandataSimulation.cs` restricted to
"`ProjectArclength` and `FormationLookaheadWu` only", `SlotTargetsTests.cs`, and
`TickPipelineTests.cs`. Storing cumulative lengths in raw, which is what the row
suggested, does **not** on its own make the round trip exact: `SampleAt` still
returned whole-world-unit coordinates and `ProjectArclength` still took a
whole-world-unit query position, and each of those throws away more precision
than the segment length ever did. Meeting the criterion required
`SlotTargets.cs` and the stage 9 call site as well.

This is the same shape as task 79d-2's grant widening and it was widened
deliberately, by the integrating thread, with no other row running. The row's
file list is recorded as insufficient rather than quietly obeyed.

#### `Int128` in two places, for a reason that only appears at raw scale

`ProjectArclength`'s projection numerator is on the order of the squared map
extent. In world units that is about 1.7 × 10⁷ and multiplying it by a
coordinate is harmless; at raw scale it is about 10¹³ and the same multiplication
overflows a signed 64-bit integer on a map only a few thousand world units
across. Two products are widened to `Int128`, whose quotient is back inside
`long` by construction since the clamped numerator never exceeds the
denominator. `Hukbo.Core`'s `MovementContextQuery` already widens the same way
for the same reason, so this introduces no new technique, and `Int128` is exact
integer arithmetic carrying none of the cross-version hazard that bans `double`
from `Sandata.Core`.

#### `FormationLookaheadWu` is gone, which is the reduction task 84 could not make

The leader's sample now sits exactly one per-tick step ahead of its own
projection. Anything larger is absorbed by `ClampToMovementSpeed` and buys
nothing; anything smaller throttles the leader below the designed sprint speed.
One step is therefore not a tuning parameter at all — it is
`SprintSpeedWuPerSecond` divided by the tick rate, and it moves when either of
those does. Task 79b's provisional 8 world units, and the floor task 84 had to
write under it after freezing a leader at (4, 4), are both deleted.

#### The row asked for one raw unit and one raw unit is not reachable

Measured worst-case round-trip drift over 97 probes is exactly **2 raw units**,
and the test pins 2 rather than 1. Three truncating divisions sit on that round
trip: the segment length is a truncated integer square root, `SampleAt`
truncates the interpolated coordinate, and `ProjectArclength` truncates the
projection back. Each can lose a raw unit and they do not cancel. One is not
reachable without rounding-to-nearest at every step.

**The row said "exact to within one raw unit" and the integrating thread wrote
that row.** It was written without checking it against the three roundings it
has to survive. Two raw units is 2/1024 of a world unit against a per-tick step
of 1,638 raw, so the property the task exists for is intact — but the figure was
a claim, and it is now a measurement.

#### The break-proofs found a defect in the new tests, not in the new code

Three breaks, each reverted, with `git diff` on `src/` empty afterwards.

| Break | Result |
| --- | --- |
| Segment length taken in world units and scaled afterwards — task 87's defect, restored exactly | `RunTick_...WalksTheBentPolylineAtTheDesignedSpeed` fails alone, 3,206 against 3,205, and after the pin below was added, `Build_DiagonalSegmentLength_...` fails too, 11,585 against 11,264 |
| Query position rounded back to whole world units before projecting | `ProjectArclength_RoundTrips...` fails alone, worst drift 1,448 raw — 1.41 world units, the loss the row described |

The first break is the interesting one. It was expected to fail the two new
precision tests and **it failed neither.**

The round trip is insensitive to the stored segment length being wrong, because
`SampleAt` and `ProjectArclength` divide by the *same* length in opposite
directions and the error cancels exactly. And the "sample stays on the segment"
test is insensitive to it too, because interpolation scales both components by
the same ratio: a truncated length moves the sample *along* the segment without
ever moving it *off* the segment.

So the two tests written to prove task 87's headline property proved a different
property, and only the pre-existing walk test caught the headline one — by a
margin of one raw unit. A third test was added in response,
`Build_DiagonalSegmentLength_IsTheRawRootNotTheScaledWorldUnitRoot`, which pins
the (8, 8) segment at 11,585 raw against the 11,264 the old arithmetic produces.
It fails immediately and alone under that break.

Both surviving tests now carry a "what this test does not bind, established by
breaking it" paragraph naming the test that does bind it. **A test that passes
under the break it was written for is worth more as a corrected doc comment than
as a deleted test**, but only if the correction is written down.

#### The seed-1 workload is unchanged, as predicted

`stateHash` `BDD56EBD06F76674`, `eventHash` `7C1B37876769DEC7`, 70 and 64
survivors, `deterministic: true`. Nothing in that fixture has a published path,
so none of this code executes there. `Sandata.Core.Tests` went 1,104 to 1,107.

### Task 91 complete, and the endpoint that was 95 percent of a suite — 2026-08-09

Merged. `Sandata.Core.Tests` drops from **1,107 tests in 38 seconds to 1,106 in
4.5**.

Task 83's defect — redrawing the changed-cell set every tick instead of toggling
a fixed one — was restored behind a temporary local edit, and
`ChangedCellRunStaysAboveTheSuccessfulSearchFloorThroughoutTheRun` was swept
across descending endpoints to measure what each one actually detects.

| Tick count | Found fraction under the restored defect | Detects it? |
| --- | --- | --- |
| 2,000 | 5.8 % | yes |
| 800 | 14.4 % | yes |
| 400 | 28.9 % | yes |
| 200 | 56.9 % | yes |
| 100 | 75.4 % | yes |
| 50 | 86.7 % | yes |
| 38 | 88.9 % | yes, by 1.1 points |
| 34 and below | above the floor | **no** |

On the fixed code the same run reports 93.7 percent at 2,000 ticks and 93.2
percent at 200, taking 35 seconds and 3 seconds respectively.

**200 catches the defect by 33 points of margin in 3 seconds; 2,000 catches it
by 84 points in 35.** Twelve times the cost to move a decisive measurement to a
more decisive one, and to move the healthy reading by half a point. The
2,000-tick endpoint is removed and the theory keeps 1 and 200.

38 was **not** chosen despite being the cheapest endpoint that works. A
1.1-point margin is a lock a later fixture change could silently unlatch, and
the task's own instruction was that a cheaper endpoint which cannot detect the
defect is worse than a slow one that can. The same reasoning applies a little
above the threshold.

What is genuinely lost is the assertion that the found fraction holds up after a
full-length run. That loss is smaller than it looks: under the fix the map
oscillates between exactly two configurations by tick parity, so 200 and 2,000
are the *same configuration* and differ only in sample count. The full-length
property has its own test —
`ChangedCellRunOscillatesBetweenExactlyTwoConfigurationsAcross2000Ticks` reaches
tick 2,002 in 82 milliseconds, because it uses one seeker and no replanning —
and that test, not this one, is what proves the map does not drift.

The temporary revert never entered a commit: `git diff` on `src/` was empty
before the branch was committed, and the merged diff is one test file.

#### Both gates, run after Batch A

`./scripts/verify.ps1 -Game Sandata`, exit 0: `Sandata.Core.Tests` 1,106 of
1,106 in 4.52 seconds, `Sandata.Client.Tests` 199 of 199 in 0.49 seconds, and
the seed-1 workload reporting `stateHash` `BDD56EBD06F76674`, `eventHash`
`7C1B37876769DEC7`, 70 and 64 survivors, `deterministic: true` — unchanged
through both tasks.

`./scripts/verify.ps1` with no flags, exit 0: `Hukbo.Core.Tests` 2,376 of 2,376,
`Hukbo.Client.Tests` 3,270 of 3,270, and the 200-agent workload reporting
`stateHash` `1B73FC5923879AA0`, `eventHash` `AC55684F24D39344`,
`deterministic: true`.

The suite-duration figures written into `CLAUDE.md`, `README.md`, and
`docs/development/testing.md` earlier the same day were corrected in place
rather than left standing, since task 91 invalidated all three within hours of
task 54 recording them.

### Tasks 89, 88, and 90 complete, and the resume rule that was never implemented — 2026-08-09

Three rows closed in one session, run inline rather than through the agent
pipeline because the harness in force this session forbids the Agent tool
without an explicit user request. The recommended order was followed — 89
first, because its diagnosis could change what the other two mean, and it did.

Both gates were run on `main` at `8c1e2c0` before anything was touched, and
both were green: Hukbo at `1B73FC5923879AA0` / `AC55684F24D39344`, Sandata at
`BDD56EBD06F76674` / `7C1B37876769DEC7` with 1,106 and 199 tests passing.

#### The sequencing audit, run in both directions before any work began

File-level: task 89 claimed `TickPipelineTests.cs` plus one production file the
diagnosis would name; task 88 claimed `LineOfSight.cs`, `ContactMemory.cs`,
`SandataSimulation.cs`, `LineOfSightTests.cs`, and `ContactMemoryTests.cs`;
task 90 claimed `DeterminismEquivalenceTests.cs` alone. No file is claimed
twice. Running the three serially made the question moot in practice, but the
audit was run rather than skipped, and it is what established in advance that
task 89's unnamed production file could collide with task 88's grant — which in
the end it did not, because the diagnosis named `LocalAvoidance.cs`.

Surface-level: task 89's steps are the diagnosis, the reproduction, and the
record; task 88's are the two allocator cuts, the re-measurement, and the
scratch-buffer test; task 90's are the fixture, the resume comparison, and the
break. Each is claimed exactly once. The surface-level half is again what
mattered, since it is where task 90's step "prove a published path is
recomputed" turned out to name a behaviour that did not exist.

#### Task 89 — the cause was none of the four the row proposed

The row listed four candidates: the arclength quantization task 87 had already
addressed, `SquadGrouping` deriving a different `GroupId` once the operator
moves, `FormationCollapse` gating the slot, and the leader projection reaching
the end of a short polyline. **It is none of them.**

The symptom was reproduced first, on the same twenty-operator fixture task 52a
used. Entity 1 sits at raw (3,072, 2,048), the request publishes at tick 10,
and the operator moves exactly once — to raw (4,585, 1,422), a displacement
whose magnitude is 1,637.4 against a per-tick cap of 1,638 — and then holds that
position for every remaining tick until it is killed at tick 37.

Throughout the stall the derived `GroupId` stays 1 and the `SlotIndex` stays 0,
so grouping is not it. Stage 9 keeps proposing a fresh full-magnitude step, from
raw (4,585, 1,422) toward (6,114, 2,006), on every single tick — so the polyline
has not run out, the formation is not collapsed, and the arclength arithmetic is
producing a target the whole time. **The proposal is live and the commit is
refused.**

The control settled it. The identical fixture with entity 1 alone on the map,
same start position, same group request, same grid: it walks the entire fifty
world units at exactly 1,638 raw per tick and arrives at the goal cell's own
centre. Nothing in stage 9 is wrong.

The cause is stage 10, at `src/Sandata.Core/Movement/LocalAvoidance.cs:105`, and
it is two correct components composing into a permanent stall. Entity 2, a
faction-1 operator that never moves, stands at raw (13,312, 3,072) — directly on
entity 1's published route. `LocalAvoidance.Commit` refuses a step whose
destination would overlap another body, and then offers exactly one retry:
`SidestepRules.Sidestep`'s single 22.5-degree rotation of the same delta, to the
side `entityId` parity picks. Measured against the contact distance of 8,704 raw
(two body radii of 4,352), the first tick's desired point sits 8,662 raw from
entity 2 and every subsequent tick's sits 7,276 raw from it.

Design section 8 states the rule in full — "A blocked unit first tries a single
22.5-degree sidestep, choosing the side by a rule pinned on `entityId` parity so
it is total; if that is also blocked, it waits a tick" — and says nothing about a
blocker that never moves. Both candidates are pure functions of the proposal,
and the proposal is a pure function of an unchanged start position and an
unchanged polyline, so both are refused again on every following tick with
nothing in the simulation able to change any input. **"Waits a tick" becomes
"waits forever."** Head on, a 22.5-degree turn cannot clear a body whose radius
is 4,352 raw with a step of 1,638 raw.

Nothing upstream re-routes either. No path is re-requested after publication —
`AdvancePathService` clears `HasOutstandingRequest` on publish and nothing sets
it again — and no operator is ever entered into the nav search's `blocked` span,
so the search routes straight through a standing body. **Whether a blocked mover
should re-plan around a static body is an open design question and was not
settled here.**

The finding is written at the code that produces it, as the row required: a
paragraph on `LocalAvoidance`'s own remarks naming the measurement, the two
tests, and the open question. That is the one production file this task edited,
and the edit is documentation only.

Two tests, in `TickPipelineTests.cs`. The first,
`RunTick_AutonomousLeaderWithAClearRoute_WalksThePublishedPathToItsGoal`, is the
control turned into a permanent assertion — arrival at the goal, which a stall
cannot satisfy, plus the per-tick displacement bound, plus a lower bound on the
tick count so an unclamped single stride cannot satisfy it either. The second,
`RunTick_StationaryBodyOnThePublishedPath_StallsTheLeaderBecauseItsOneSidestepIsBlockedToo`,
reproduces the stall.

**The stall test asserts the mechanism rather than the degenerate outcome**,
which is the trap this wave has fallen into repeatedly. It requires stage 9 to
keep proposing a full forward stride on every tick of the stall window, so it
cannot pass on a simulation that has simply stopped proposing anything; it
asserts the blocker never moves and neither operator dies, so the fixture cannot
quietly become a combat test; and its own remarks say plainly that it pins a
known gap rather than a desired outcome, and that the right response to it
failing is to delete it and keep the arrival test.

Break-proofs, each reverted with an empty `src/` diff afterwards:

| Break | Result |
| --- | --- |
| the leader's one-step lookahead removed, so the sample sits at its own projection | the arrival test fails, "stopped at (2,048, 2,048) raw" — it never left; the stall test fails on its stage-9 stride assertion, "stage 9 stopped proposing a move, so this is not a stage 10 stall" |
| `SidestepRules.TurnMagnitudeBam` multiplied by eight, a 180-degree sidestep instead of 22.5 | the stall test fails alone at tick 61, "the leader moved from (21,704, 2,048) to (20,066, 2,048)"; the arrival test still passes, correctly, since its fixture has no blocker and no sidestep ever runs |

Merged at `bf73c31`.

#### Task 88 — ninety-four percent of the tick was one stage

Both allocators task 81 measured and was not allowed to reach sit in stage 5,
and the re-measurement taken before anything was cut shows how far above
everything else they were. `GC.GetAllocatedBytesForCurrentThread()` deltas
around each of `RunTick`'s fourteen stages, at 200 operators and seed 1, over
300 measured ticks after 50 warm-up ticks:

| Stage | Bytes per simulation-tick, before | After |
| --- | --- | --- |
| 3 — collision grids and the tick-start view | 21,613 | 21,593 |
| 5 — sensing | 2,229,069 | 187,857 |
| 6 — squad grouping | 16,895 | 16,896 |
| 8 — intent selection | 12,872 | 12,872 |
| 9 — movement proposals | 13,776 | 13,776 |
| 10 — local avoidance and collision | 46,508 | 46,506 |
| 11 — weapon chain | 14,878 | 14,874 |
| 12 — fire proposal | 15,517 | 15,517 |
| the whole tick | 2,371,482 | 330,245 |

Stage 5 was ninety-four percent of everything the tick allocated, and task 81's
two figures — 1,761,332 for line of sight and 456,130 for contact memory — sum
to 2,217,462 against stage 5's measured 2,229,069, which confirms those two
sites were essentially all of it. The instrumentation was temporary and is gone:
a search of `src/` and `tests/` for `GetAllocatedBytesForCurrentThread` and for
the harness type returns nothing but stale build output.

The seed-1 workload's `allocatedBytes` falls from about **42.18 GB** to about
**6.08 GB** over ten thousand ticks. Neither figure is quoted as an exact count,
for the reason this wave has already recorded three times. `stateHash`
`BDD56EBD06F76674`, `eventHash` `7C1B37876769DEC7`, 70 and 64 survivors,
`outcome: Ongoing`, `deterministic: true` — every one unchanged.

What was cut. `LineOfSight.FirstBlockingSegment` and `IsVisible` gained
overloads taking a `Span<int>` cell buffer, with a `RequiredCellBufferLength`
helper so a caller can size one; the allocating overloads remain and now
delegate to them. `ContactMemory.Update` gained an overload taking a merge
buffer, and falls back to allocating its own whenever the supplied span is
shorter than the merge needs. `SandataSimulation` holds the cell buffer as a
`readonly` field sized once in the constructor from the nav grid's fixed
dimensions, and the merge buffer as a grow-on-demand field, the same shape
`PathService` already uses for its own smoothing scratch and the same shape task
81 used for `_pathBlockedCells`. Neither buffer reaches a snapshot or a hash and
neither outlives its tick as content.

**Two deliberate deviations from the row, both recorded rather than quietly
taken.** The row granted `SandataSimulation.cs` "stages 3 and 5 only", and the
buffers are allocated in the constructor instead, because that is where task 81
put the identical thing for the identical reason and because sizing a buffer
inside the stage that uses it every tick would defeat the point. And the
allocating overloads were kept rather than removed, because `PathSmoothing`
calls line of sight once per published path rather than once per operator pair
and threading a buffer through it buys nothing measurable; the risk that this
leaves an open door beside a validated one is answered by naming the hot-caller
rule at the type's own remarks rather than by leaving it implicit.

**The line-of-sight reuse test could not be made to fail, and that is a finding
about the code rather than a defect in the test.** The first attempt compared
the buffer overload's answers against the allocating overload's, which is
worthless once the second delegates to the first — it compares a code path
against itself. Rewritten to pin each query's answer from the fixture's own wall
geometry, it then survived the break it was written for: reading the whole
buffer instead of only the prefix `GridRay.Traverse` had just written changed no
result at all. The reason is structural. A stale cell only ever adds candidate
wall segments, and `ExactPredicates.ClassifySegments` then tests each candidate
against the query's own two endpoints, so a wall that does not actually cross
the sightline is rejected no matter which cell proposed it. **A reused
line-of-sight buffer cannot corrupt an answer**, and that is now written both at
`LineOfSight` and in the test's own "what this test does not bind" paragraph,
following the pattern task 87 established. A break that does bind was then found
— walking the cell chain in reverse fails the test alone, "Expected: 0, Actual:
1" — so the pinned answers are real assertions and not decoration.

The contact-memory buffer genuinely can leak, and its test binds it:

| Break | Result |
| --- | --- |
| the merge returns the whole buffer rather than the filled prefix, on every path | six `ContactMemoryTests` fail, which is too coarse to attribute to any one property |
| the merge returns the whole buffer only when a caller-supplied scratch was used | the reuse test fails, and so do both golden replays and two determinism-equivalence tests — the leak reaching the simulation, demonstrated |

One process note, cheap to state and expensive to relearn: `git checkout --` on
a file holding uncommitted work reverts the work, not the break. It silently
discarded this task's whole `LineOfSight.cs` edit in the middle of a
break-proof. A break in an uncommitted file is reverted by undoing the break, or
from a copy taken first.

Merged at `c1acbc7`.

#### Task 90 — the rule the row assumed was already implemented was not

The row's premise was that design section 4's derived-path resume rule works and
merely lacks a test: "It is not disproven; nothing suggests the recomputation is
wrong. It simply has no test." **The recomputation did not exist.**

`SandataSimulation`'s constructor built a fresh, empty `PathService` and never
seeded it from `initialState.Groups`, and `AdvancePathService` re-submits only
groups whose `HasOutstandingRequest` is still set — which publication clears. So
a *published* path was not recomputed on resume, it was lost. Measured, not
inferred: an operator resumed mid-walk at raw x 34,808 was still at 34,808 after
its first resumed tick, while the run that never stopped kept walking, and the
per-tick state hashes diverged immediately at the resume tick.

An *outstanding* request was always fine, and that distinction is what makes the
fix small: its flag survives the snapshot, stage 7 re-submits it on the first
tick, and `PathService.Advance` publishes it on the tick its stored
`RequestTick` always implied.

The fix. `PathService.RestorePublishedPath` rebuilds one group's polyline from
its request record and publishes at once rather than serving `PathLatencyTicks`
a second time — the wait already happened in the run being resumed, and charging
it again is exactly what would make a saved mission diverge from an unsaved one,
which is the property this suite exists to forbid. `SandataSimulation` calls it
from its constructor, before the first tick, for every group whose flag is
already cleared. On a fresh mission the loop does nothing at all, since every
group a mission starts with has its request outstanding — which is why the
seed-1 workload, whose `Groups` array is empty anyway, is untouched.

**This widened the row's grant from one test file to three files.** The row
asked only for proof; proof that the behaviour is absent is worth little without
the behaviour. The widening was deliberate, was taken with no other row running,
and is recorded here rather than quietly obeyed — the same shape as tasks 87 and
79d-2.

The test uses its own fixture rather than the file's shared twenty-operator
packing, and task 89 is the reason it has to: in that packing every mover is
stalled against another body within a step or two, so a comparison window there
would compare two runs that are both standing still — which is precisely the
mistake task 52a's deleted fifth test made. The fixture is one walker on a clear
route plus one enemy placed beyond `DetectRangeWu` from every point of that
route, so nothing senses, shoots at, or blocks anything. It carries no combat
and therefore cannot use `AssertRunWasActive`; **sustained movement inside the
compared window is its activity check instead, asserted on both sides**, along
with the premise that the request had already published by the midpoint.

Break-proofs, each reverted:

| Break | Result |
| --- | --- |
| the constructor's recomputation call removed | the new test fails alone |
| the recomputation run with the start and goal cells swapped | the new test fails alone, so it binds the recomputed polyline's identity and not merely the existence of some path |

Merged at `e22a542`.

#### Both gates, run after all three merges

`main` moved underneath this session between the task 89 merge and the task 88
branch: another session merged `ranged-units` at `9daa271`. Tasks 88 and 90 were
therefore branched from the newer `main` and needed no rebase, and the Hukbo
figures below are not comparable to the ones this session recorded at its start.
Check `git log --oneline -1` before quoting any of them.

`./scripts/verify.ps1 -Game Sandata`, exit 0: `Sandata.Core.Tests` 1,113 of
1,113, `Sandata.Client.Tests` 199 of 199, and the seed-1 workload reporting
`stateHash` `BDD56EBD06F76674`, `eventHash` `7C1B37876769DEC7`, 70 and 64
survivors, `outcome: Ongoing`, `deterministic: true`, `allocatedBytes`
6,078,234,120 — about 6.08 GB, down from about 42.18 GB.

`./scripts/verify.ps1` with no flags, exit 0: `Hukbo.Core.Tests` 2,433 of 2,433
and `Hukbo.Client.Tests` 3,499 of 3,499, both raised by the `ranged-units` merge
rather than by anything here, and two headless workloads — the recorded baseline
at `stateHash` `1B73FC5923879AA0` / `eventHash` `AC55684F24D39344`, unchanged,
and a second at `C8023D3B5BEB005E` / `F709A345E2F7370E` that arrived with
`ranged-units` and belongs to that session to record.

The two runs are two results and are never added together.

#### What is now open

Wave 12's task list is closed: 79a through 79d-2b, and 80 through 91, are all
done. What these three rows leave behind is not a task list but three questions,
none of them settled here:

- **Should a blocked mover re-plan around a static body?** Task 89's stall is
  correct behaviour in every component and a dead end in composition. Answering
  it means deciding whether operators enter the nav search's `blocked` span,
  whether a group re-requests its path when its mover stops making progress, or
  neither.
- **Should stage 5's remaining per-tick arrays be cut too?** After task 88 the
  tick allocates about 330 KB per simulation-tick, of which stage 10 is now the
  largest single contributor at about 46,500 bytes. Nothing here is urgent and
  no row asks for it; it is recorded so that the next measurement starts from
  the right number rather than from the old one.
- **The nine open design questions from the previous session all still stand**,
  and task 90 sharpened none of them — but it did demonstrate that a rule
  written plainly in design section 4 had gone unimplemented for eleven waves
  without any test noticing, which is worth carrying into how the remaining
  design rules are treated.
