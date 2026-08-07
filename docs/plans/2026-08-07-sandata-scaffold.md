# Sandata scaffold — task plan

Date: 2026-08-07
Status: plan, not yet authorized for implementation
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
