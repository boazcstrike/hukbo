# Blood and Gore — Plan

Date: 2026-07-27
Status: Plan. Ordered task list and verification criteria for
`docs/plans/2026-07-27-blood-and-gore-design.md`. The design document's
decisions are settled and are not relitigated here.

## 0. Deviations from the requested phase shape

Two adjustments to the suggested six-phase shape, made because the dependency
order does not work otherwise:

- **The `GoreIntensity` enum moves from Phase 4 into Phase 1.** `BloodEffectSystem`
  (Phase 2) needs a concrete `Intensity` property from the moment it is written,
  because "Off is a no-op" and "Full adds a lethal spurt" are core behaviors of
  that class, not something bolted on afterward. Defining the enum in Phase 4
  would force a retrofit edit to `BloodEffectSystem` mid-plan for no benefit.
  The enum is a plain value type with no logic, matching the zero-test
  precedent of `src/Hukbo.Client/Presentation/ClientCommand.cs`, so folding it
  into "Phase 1: value types" costs nothing.
- **Phase 4's scope is "settings persistence," not "GoreIntensity enum + settings
  persistence."** The enum already exists by Phase 4 (see above). Phase 4 is
  exclusively the `ClientSettings` / `ClientSettingsStore` / `GoreIntensityManager`
  work.

Every other phase keeps its suggested position and intent.

## 1. Non-negotiable constraints (restated, not new)

- `src/Hukbo.Core` is untouched. Zero files under it may change in this feature.
- The seed-1 200-agent 10,000-tick state hash (`6EBB1EA63114F6CE`) and event hash
  (`941377BD43C556FF`), recorded in `docs/development/testing.md:49`, must not
  move. The 500-agent stress run must still terminate at tick 309
  (`docs/development/testing.md:54`).
- `System.Random`, the wall clock, and any `Hukbo.Core` RNG call are banned in
  every new file. All visual variation is a pure hash of `Sequence`,
  `SourceEntityId`, `TargetEntityId`, and droplet index, per design §4.
- Zero heap allocation in `Ingest`, `Advance`, and `Draw`. Fixed arrays allocated
  once in each system's constructor, in-place compaction with a read index and a
  write index, `ReadOnlySpan<T>` exposure, no LINQ, no closures on those paths.
- `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Never weaken a
  warning, a test, or an analyzer to reach green.
- No client test may construct `ArenaGame`, a `GraphicsDevice`, or a
  `SpriteBatch`. Every new test file follows the pure-helper pattern in
  `.claude/skills/hukbo-client-ui/SKILL.md`.
- No new NuGet package. `Directory.Packages.props` stays untouched.

## 2. Explicit non-goals (copied from design §6 — do not drift toward these)

- Dismemberment, severed limbs, or any per-body-part mutilation silhouette.
- Wound marks accumulating on a pawn. Dead agents are not drawn at all
  (`src/Hukbo.Client/ArenaGame.Rendering.cs:185`), so there is no corpse to mark.
- Screen-edge or lens splatter. This is a windowed top-down spectator view, not a
  first-person camera.
- Screen shake, hit stop, freeze frames, and knockback.
- Blood that spreads, merges, or flows over time.
- Audio of any kind.
- A rendering benchmark. `scripts/benchmark.ps1` is headless and cannot measure
  frame time; the quad budget in design §4 is a stated hypothesis, not a
  measurement, and this plan does not claim otherwise.

## 3. Ambiguities found while planning, with a recommendation for each

These are implementation-detail questions the design document does not pin down
numerically. Each is flagged so the implementer does not have to guess silently.

**Resolved 2026-07-27: all five recommendations are accepted. Reading B applies
in every case.** The implementer follows the recommendation column and does not
revisit these. One clarification on question 5: the reading-B answer concerns
droplets only. Ground marks outlive the burst that produced them, so they remain
a separately stored fixed-capacity buffer as described in the design document.

| # | Question | Reading A | Reading B | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | Which attacks leave a ground mark? | Only attacks where the victim dies that tick (matches AC14's literal wording) | Every accepted attack, sized and timed by severity/lethality (matches design §1's "see where the fighting was heaviest" and §3.7's unqualified "fading ground marks") | **B.** AC14 is then a tested subset (the lethal case), not the whole rule. Only reading B serves the stated spectator-legibility purpose. |
| 2 | What does "no blood state is allocated" mean at Off (design §3.7)? | The fixed buffers are not constructed at all when Off is active | The fixed buffers are constructed once in the constructor exactly like every other system in this codebase, but `Ingest`/`Advance`/`Draw` become no-ops so no slot is ever occupied | **B.** Matches the unconditional constructor-allocation pattern already used by `HitEffectSystem`, `BattleEventFeed`, and the whole codebase. AC11's actual wording ("no droplet or ground mark slot is occupied") is a behavior claim, not a memory-layout claim. Runtime intensity changes (via the menu, without restart) also require the buffers to already exist. |
| 3 | Where does the new gore-intensity control sit in `MenuOverlay`'s focus order? | Interleaved right after the theme selector (new index 1), pushing every button index up by one | Appended after the last button (new terminal index, `_buttons.Length + 1`) | **B.** Zero risk to the existing button-index arithmetic and to `MenuOverlayFocusTests.cs`'s existing numeric expectations. See §7 for the exact scheme. |
| 4 | Does a corrupt `goreIntensity` JSON value invalidate the whole settings file, matching today's schema-mismatch behavior? | Yes — any single bad field resets `SchemaVersion`, `SelectedThemeId`, and `GoreIntensity` all to default | No — schema version and theme validate independently from gore intensity; a bad gore value alone resets only that field | **B.** Reading A reintroduces exactly the "settings bump discards the user's saved theme" risk the design document names in §7. A gore-intensity field is independent of theme choice and should fail independently. |
| 5 | Is "Droplets: 2048" (design §4 budget table) a literal stored particle buffer, or a derived worst-case quad count? | `BloodEffectSystem` stores up to 2048 individual droplet particles, each aged and compacted independently | `BloodEffectSystem` stores up to 256 `BloodBurst` records (mirroring `HitEffectSystem`'s single stored `HitEffect` per hit); `BloodGeometry`/`BloodRenderer` expand each burst into up to 8 droplet quads at draw time, purely from the burst's seed, with no separate storage. 256 x 8 = 2048, matching the table exactly | **B.** Matches the established `HitEffectSystem` / `HitEffectGeometry` split precisely: one stored effect expands into many quads at draw time, nothing else is stored per quad. Reading A would duplicate the geometry math that draw time already needs, for no stated benefit. Burst capacity 256 and per-burst droplet cap 8 are **assumed**, inferred from this arithmetic — pin the exact numbers with the Phase 1/2 tests, not this document. |

## 4. Surface map

| Concern | File : line |
| --- | --- |
| Existing hit-effect system (keying, capacity, overflow) | `src/Hukbo.Client/Presentation/HitEffectSystem.cs:1-149` |
| Existing hit-effect value type | `src/Hukbo.Client/Presentation/HitEffect.cs:1-13` |
| Existing pure geometry (seed mixing, detail thresholds) | `src/Hukbo.Client/Rendering/HitEffectGeometry.cs:1-136` |
| Existing dumb renderer (no logic, not unit tested) | `src/Hukbo.Client/Rendering/HitEffectRenderer.cs:1-133` |
| Coordinator owning all presentation systems | `src/Hukbo.Client/Presentation/PresentationCoordinator.cs:1-77` |
| `Attack` event shape (`Weapon`, `HitLocation`, `Value`) | `src/Hukbo.Core/Simulation/BattleEvent.cs:28-161` |
| Tick-stage order (`Move` -> `Attack` -> `Damage` -> `Death` -> `Outcome`) | `src/Hukbo.Core/Simulation/BattleSimulation.cs:151-152` |
| Weapon identity (four classes: `GreatBlade`, `HeavyChopper`, `ThrustingBlade`, `Bolo`) | `src/Hukbo.Core/Combat/CombatIdentity.cs:7-21` |
| Body-part taxonomy (metadata only, never mutilation) | `src/Hukbo.Core/Combat/BodyPart.cs:1-33` |
| Client constructor, theme manager construction | `src/Hukbo.Client/ArenaGame.cs:65-96` (theme manager at `:72-76`) |
| Tick-advance loop (`IngestTick` call site) | `src/Hukbo.Client/ArenaGame.cs:364-372` |
| Presentation-effect advance call site | `src/Hukbo.Client/ArenaGame.cs:125-126` |
| Arena draw path, existing hit-effect draw order | `src/Hukbo.Client/ArenaGame.Rendering.cs:130-146` |
| Pawn draw (dead agents skipped) | `src/Hukbo.Client/ArenaGame.Rendering.cs:175-224` (skip at `:185`) |
| Client settings record | `src/Hukbo.Client/Settings/ClientSettings.cs:1-5` |
| Settings store load/save + schema version | `src/Hukbo.Client/Settings/ClientSettingsStore.cs:7,33-60,62-113` |
| Settings store tests (existing invalid-file coverage) | `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs:35-47` |
| Theme manager (two-constructor testability pattern to mirror) | `src/Hukbo.Client/Theming/UiThemeManager.cs:1-51` |
| Menu button array | `src/Hukbo.Client/MenuOverlay.cs:21-28` |
| Menu focus resolver (pure, tested) | `src/Hukbo.Client/MenuOverlay.cs:259-274` |
| Menu selector template to mirror | `src/Hukbo.Client/UI/UiThemeSelector.cs:1-240` |
| Existing selector test pattern to mirror | `tests/Hukbo.Client.Tests/UiThemeSelectorTests.cs:1-89` |
| Existing menu-focus test pattern to mirror | `tests/Hukbo.Client.Tests/MenuOverlayFocusTests.cs:1-39` |
| Existing coordinator test pattern to mirror | `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs:1-148` |
| Smoke checklist (rows to extend) | `docs/development/testing.md:104-120` |
| Recorded gate baseline (hashes, allocation) | `docs/development/testing.md:40-78` |

## 5. Existing patterns to reuse

| Need | Pattern already in the repo |
| --- | --- |
| Stateful effect system with fixed capacity, overflow-replaces-oldest, `Ingest`/`Advance`/`Clear` | `src/Hukbo.Client/Presentation/HitEffectSystem.cs` |
| Pure geometry expansion from one stored record into several draw primitives, seeded by `Mix(sequence) ^ Mix(entityId)` | `src/Hukbo.Client/Rendering/HitEffectGeometry.cs:117-135` |
| Dumb renderer, no logic, zero test file | `src/Hukbo.Client/Rendering/HitEffectRenderer.cs` |
| Settings-backed manager with a public convenience constructor and an `internal` fully-testable constructor | `src/Hukbo.Client/Theming/UiThemeManager.cs` |
| Cycling selector control (previous/next, keyboard, pointer) | `src/Hukbo.Client/UI/UiThemeSelector.cs` |
| Fixed-value-with-no-theme-role color choice, with a documented reason | `src/Hukbo.Client/UI/FactionColorPalette.cs:6-11`, `src/Hukbo.Client/Rendering/HitEffectRenderer.cs:10-11` |
| Plain enum, zero dedicated test file | `src/Hukbo.Client/Presentation/ClientCommand.cs` |

## 6. Data flow

1. `BattleSimulation.AdvanceOneTick()` runs; `Move` commits strictly before
   `Attack` resolves (`BattleSimulation.cs:151-152`), so `AgentView` positions at
   end-of-tick are exact for the attack that just landed.
2. `ArenaGame.AdvanceSimulation` (`ArenaGame.cs:364-372`) calls
   `_presentation.IngestTick(_simulation.LastEvents, _simulation.Agents)`.
3. `PresentationCoordinator.IngestTick` fans out to `EventFeed`, `HitEffects`
   (keyed on `Damage`, unchanged), and — after Phase 3 — `Blood` (keyed on
   `Attack`).
4. `BloodEffectSystem.Ingest` reads every `Attack` event in the batch, resolves
   attacker and victim `AgentView`s by entity ID, computes the direction vector,
   clamps severity, checks the same-tick `Death` set for lethal tier, and stores
   one `BloodBurst` (plus, per §3 ambiguity 1, one `GroundMark`) per accepted
   attack, subject to `Intensity` gating.
5. `ArenaGame.Update` calls `_presentation.AdvanceEffects(elapsedSeconds)`
   (`ArenaGame.cs:125-126`), which now also calls `Blood.Advance`, aging and
   compacting bursts/marks/spurts on unscaled presentation time.
6. `ArenaGame.Draw` -> `DrawArena` (`ArenaGame.Rendering.cs:130-146`) draws
   ground marks beneath pawns, then pawns, then bursts above pawns alongside the
   existing `HitEffectRenderer.Draw`. `BloodGeometry` expands each stored burst
   into droplet quads at this point, purely from the burst's own fields.
7. Menu: `MenuOverlay.Update` resolves the new gore-intensity control's
   interaction; `ArenaGame.Update` applies a selected value to `GoreIntensityManager`,
   which persists it via `ClientSettingsStore.TrySave`, and immediately pushes
   the new value onto `_presentation.Blood.Intensity` so the change is visible
   without a restart.

## 7. Exact menu focus-order change

Current scheme (`MenuOverlay.cs:84-134`, `:259-274`): index `0` is the theme
selector; indices `1..N` are the `N` buttons (currently `N = 5`,
`MenuOverlay.cs:21-28`); `ResolveFocusedControlIndex` is called with
`controlCount = _buttons.Length + 1` (today, `6`).

**New scheme:** index `0` stays the theme selector, unchanged. Indices `1..N`
stay the buttons, unchanged — no existing index arithmetic for buttons is
touched. The new gore-intensity selector takes the **new terminal index**
`N + 1` (today, `6`). `controlCount` grows from `_buttons.Length + 1` to
`_buttons.Length + 2` (today, `6` -> `7`). Hover detection for the gore selector
mirrors the existing theme-selector hover check at `MenuOverlay.cs:84-87`
(`_goreSelector.Bounds.Contains(input.MousePosition) ? _buttons.Length + 1 : hoveredControlIndex`,
evaluated after the button loop so it does not clobber a hovered button).
Keyboard wraparound (`Up`/`Down`/`Tab`/`W`/`S`) already wraps modulo
`controlCount` (`MenuOverlay.cs:259-274`), so it reaches the new index for free
once `controlCount` is updated at every call site.

## 8. Exact settings-migration mechanism

`ClientSettingsStore.SupportedSchemaVersion` (`ClientSettingsStore.cs:7`) **stays
`1`. It is not bumped.** Bumping it would make the existing pattern match at
`ClientSettingsStore.cs:45-51` (`SchemaVersion: SupportedSchemaVersion`) fail for
every settings file written before this feature, discarding the user's saved
theme — exactly the risk the design document names in §7.

Concrete mechanism:

1. `ClientSettings` gains a third field: `GoreIntensity GoreIntensity`.
2. `Load` deserializes into a new private DTO,
   `RawClientSettings(int SchemaVersion, string? SelectedThemeId, GoreIntensity? GoreIntensity)`,
   instead of directly into `ClientSettings`. This keeps the two validations
   independent instead of coupling them through one non-nullable record's
   constructor.
3. Schema/theme validation is unchanged: `SchemaVersion == SupportedSchemaVersion
   && SelectedThemeId is { Length: > 0 }`. If this fails, the whole result is
   `Default(defaultThemeId)` exactly as today (schema mismatch or malformed JSON
   still resets everything — that behavior is intentionally preserved for those
   failure modes; see §3 ambiguity 4 for why the gore field alone is treated
   differently).
4. Gore-intensity resolution is independent and always succeeds:
   `raw.GoreIntensity is { } g && Enum.IsDefined(g) ? g : GoreIntensity.Stylized`.
   A pre-feature file has no `goreIntensity` JSON property at all, so `raw.GoreIntensity`
   is `null`, and this line alone supplies the default — the theme is preserved
   because step 3 already passed independently.
5. `TrySave` changes signature from `TrySave(string selectedThemeId)` to
   `TrySave(string selectedThemeId, GoreIntensity goreIntensity)`. This is a
   breaking signature change with exactly one existing call site
   (`UiThemeManager`'s public convenience constructor, see Phase 4).

Tradeoff: the DTO adds one small type and one extra mapping step compared to
relying on System.Text.Json's constructor-default-parameter behavior for a
missing property. That reliance is not used here because this plan does not
assert unverified serializer behavior — the DTO approach is explicit and its
correctness is provable by the Phase 4 tests without depending on a
System.Text.Json version-specific guarantee.

## 9. Acceptance-criteria traceability

| # | Criterion (design §5) | Task(s) |
| --- | --- | --- |
| 1 | One burst per accepted attack | P2-T1, P2-T2 |
| 2 | Direction points attacker -> victim | P1-T3, P1-T4, P2-T1, P2-T2 |
| 3 | Spray differs across the four weapon classes | P1-T3, P1-T4 |
| 4 | Dying-victim blow renders lethal tier | P1-T3, P1-T4, P2-T1, P2-T2 |
| 5 | Multiple same-tick attackers: all render, none dropped, no killer bias | P2-T1, P2-T2 |
| 6 | Bounded capacities, oldest overwritten | P2-T1, P2-T2 |
| 7 | Unscaled presentation time; never gates simulation; tick rate unaffected | P2-T2 (structural), P6-T4 row 21 |
| 8 | Seed-1 hash/event-hash and 500-agent tick unchanged | P6-T1, P6-T2, P6-T3 |
| 9 | No `System.Random`/wall clock/Core RNG; pure function of IDs | P1-T3, P1-T4, P2-T1, P2-T2 |
| 10 | Gore setting Off/Stylized/Full, menu-reachable, persists, defaults Stylized on pre-feature file | P4-T1..P4-T6, P5-T1..P5-T5 |
| 11 | At Off, no quad submitted, no slot occupied | P2-T1, P2-T2, P3-T3, P3-T4 |
| 12 | Zero heap allocation per tick/frame | P2-T2, P3-T3 (implementation constraint; Draw-path allocation is unmeasured by the headless gate — see §11) |
| 13 | No client test constructs `ArenaGame`/`GraphicsDevice`/`SpriteBatch` | Every RED task, P1-P5 |
| 14 | Lethal ground mark outlives burst, fades not pops | P1-T3, P1-T4, P2-T1, P2-T2 |
| 15 | Detail degrades with zoom; visible at default fit | P1-T3, P1-T4 |
| 16 | Clears on Next Round and Full Reset | P3-T1, P3-T2, P6-T4 row 19 |

## 10. File ownership by phase

| Phase | Files owned (created or modified) |
| --- | --- |
| P1 | `src/Hukbo.Client/Settings/GoreIntensity.cs` (new), `src/Hukbo.Client/Presentation/BloodEffect.cs` (new), `src/Hukbo.Client/Rendering/BloodGeometry.cs` (new), `tests/Hukbo.Client.Tests/BloodGeometryTests.cs` (new) |
| P2 | `src/Hukbo.Client/Presentation/BloodEffectSystem.cs` (new), `tests/Hukbo.Client.Tests/BloodEffectSystemTests.cs` (new) |
| P3 | `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` (modify), `src/Hukbo.Client/Rendering/BloodRenderer.cs` (new), `src/Hukbo.Client/ArenaGame.Rendering.cs` (modify), `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` (modify) |
| P4 | `src/Hukbo.Client/Settings/ClientSettings.cs` (modify), `src/Hukbo.Client/Settings/ClientSettingsStore.cs` (modify), `src/Hukbo.Client/Settings/GoreIntensityManager.cs` (new), `src/Hukbo.Client/Theming/UiThemeManager.cs` (modify — delete unused convenience constructor), `src/Hukbo.Client/ArenaGame.cs` (modify — constructor), `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs` (modify), `tests/Hukbo.Client.Tests/GoreIntensityManagerTests.cs` (new) |
| P5 | `src/Hukbo.Client/UI/GoreIntensitySelector.cs` (new), `src/Hukbo.Client/MenuOverlay.cs` (modify), `src/Hukbo.Client/ArenaGame.cs` (modify — `Update`), `tests/Hukbo.Client.Tests/GoreIntensitySelectorTests.cs` (new), `tests/Hukbo.Client.Tests/MenuOverlayFocusTests.cs` (modify) |
| P6 | `docs/development/testing.md` (modify — smoke rows and recorded gate result) |

**Serialization points (files touched by more than one phase, must not run in
parallel):**

- `src/Hukbo.Client/ArenaGame.cs` — touched by P4 (constructor) and P5
  (`Update`). P4 must land first; P5's `Update` hunk assumes `_goreManager`
  already exists.
- `src/Hukbo.Client/Theming/UiThemeManager.cs` — touched only by P4, but it is
  the one file in this feature that couples theme work and gore work together
  (deleting the convenience constructor because `TrySave`'s signature changed).
  Do not let a parallel theme-related change land in this file mid-plan without
  re-checking P4-T5.
- `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` and
  `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` — owned entirely by
  P3; do not split across two workstreams.
- `src/Hukbo.Client/MenuOverlay.cs` — five separate hunks (ctor, index scheme,
  `Update`, `Draw`, `Layout`) all belong to P5-T4 and must land as one coherent
  edit, not divided between parallel agents.

Everything else in P1-P3 is net-new files with no existing readers, so those
three phases can run with no file contention between them except the ordering
dependency (P2 needs P1's types; P3 needs P2's system).

## 11. Risks, assumptions, unknowns

| Item | Status | Note |
| --- | --- | --- |
| Burst capacity 256 / droplets-per-burst 8 | **assumed** | Inferred from design §4's "Droplets: 2048" via 256 x 8 = 2048 (see §3 ambiguity 5). Pin exact numbers with Phase 1/2 tests. |
| Default-fit apparent scale ~0.79 at 1280x720 | **assumed**, per design §3.5 | Carried over from the approved design document's own math; not independently recomputed here, per instructions not to relitigate settled design decisions. |
| Ground-mark creation rule (every attack vs. lethal-only) | **assumed: every attack** | See §3 ambiguity 1. |
| `Ingest`/`Advance`/`Draw` allocate zero heap bytes | **unverified by automated gate for the Draw path** | The headless workload (`scripts/benchmark.ps1`) never calls `Draw`; only `Ingest`/`Advance` run under it. Draw-path allocation is unmeasured, matching design §6's own admission that there is no rendering benchmark. |
| No `System.Random`/wall-clock/Core-RNG call anywhere in new files | **verified by code review, not by tooling** | `scripts/verify.ps1` does not grep for banned APIs; this is a manual review item for the Phase 1/2/3 reviewer pass, not an automated gate check. |
| `TrySave`'s signature change has exactly one existing call site | **verified** | Confirmed by `Grep` — only `src/Hukbo.Client/ArenaGame.cs` and `src/Hukbo.Client/Theming/UiThemeManager.cs` reference `UiThemeManager`; no other file calls `ClientSettingsStore.TrySave`. |
| Reusing `UiThemeSelectorLayout`/`standards.Shared.Selector` metrics for the gore selector avoids any `ui-theme-standards.json` change | **assumed** | Reasonable given both selectors are visually the same shape (a labeled box with previous/next arrows); if the gore selector needs different sizing, this becomes a small JSON+catalog-test change scoped to P5, not a new phase. |

## 12. Phased task list

### Phase 1 — Value types + pure geometry + tests

Risk/rollback: revert the four new files listed below. Nothing else references
them yet, so rollback is a clean deletion with zero coupling to any other file.

- [x] **P1-T1** (setup, no test — mirrors the zero-test precedent of
  `HitEffect.cs` and `ClientCommand.cs`): create
  `src/Hukbo.Client/Settings/GoreIntensity.cs` with
  `internal enum GoreIntensity { Off = 0, Stylized = 1, Full = 2 }`, plus a
  doc comment stating the numeric values are part of the persisted
  settings-file contract and must not be renumbered or reordered.
  Verification: `./scripts/verify.ps1 -SkipBootstrap` builds clean.
- [x] **P1-T2** (setup, no test — mirrors `HitEffect.cs`): create
  `src/Hukbo.Client/Presentation/BloodEffect.cs` with `BloodBurst` (carrying
  `Sequence`, `SourceEntityId`, `TargetEntityId`, `XRaw`, `YRaw`, direction
  components, `Weapon`, `HitLocation`, a clamped `SeverityRatio`, `IsLethal`,
  `AgeSeconds`) and `GroundMark` (carrying `Sequence`, `XRaw`, `YRaw`,
  `IsLethal`, `AgeSeconds`). A `LethalSpurt` value type may be added here or
  deferred to Phase 2 if the Full-tier shape is clearer once `BloodEffectSystem`
  exists — implementer's call, noted so it is not forgotten.
  Verification: `./scripts/verify.ps1 -SkipBootstrap` builds clean.
- [x] **P1-T3 (RED)**: write `tests/Hukbo.Client.Tests/BloodGeometryTests.cs`
  covering: repeated `Create` calls return identical geometry (determinism, AC9);
  sequence/target/source changes alter the derived seed/angle; spray direction
  matches the burst's stored direction (AC2); shape/droplet-count differs across
  all four `WeaponId` values (AC3); lethal tier differs from ordinary tier in
  droplet count, spread, or duration (AC4, AC14); an overkill severity input
  still clamps `SeverityRatio`-derived visuals to the same range as a
  severity-ratio-1 input; a ground mark's lifetime, when `IsLethal`, exceeds the
  paired burst's lifetime (AC14); apparent scale at a zoom near the design's
  documented default-fit value (~0.79, design §3.5) still yields a nonzero
  droplet count (AC15); scale is monotonic and clamped at zoom extremes,
  mirroring `HitEffectGeometryTests.Create_ScaleIsMonotonicAndClampedAtZoomExtremes`;
  `Create` throws `ArgumentOutOfRangeException` for negative/NaN/infinite zoom,
  mirroring `HitEffectGeometryTests.Create_RejectsInvalidCameraZoom`.
  Verification: this file fails to compile (`BloodGeometry` does not exist) —
  confirmed RED.
- [x] **P1-T4 (GREEN)**: implement `src/Hukbo.Client/Rendering/BloodGeometry.cs`
  with a `BloodGeometry.Create(BloodBurst burst, float cameraZoom)` returning a
  layout record, following `HitEffectGeometry.CreateSeed`/`Mix` exactly, extended
  per design §4's formula
  (`burstSeed = Mix(sequence + K1) ^ Mix(targetEntityId + K2) ^ Mix(sourceEntityId + K3)`,
  `dropletSeed = Mix(burstSeed + index * K4)`), plus a ground-mark geometry helper
  (same file or a sibling `GroundMarkGeometry`, implementer's call) for
  radius/alpha-over-age.
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~BloodGeometryTests`
  all green, then `./scripts/verify.ps1 -SkipBootstrap`.

### Phase 2 — `BloodEffectSystem` + tests

Risk/rollback: revert `BloodEffectSystem.cs` and its test file. Phase 1 files
are untouched by this revert and remain unreferenced by anything else, so the
repository returns to the Phase-1-complete state.

- [x] **P2-T1 (RED)**: write `tests/Hukbo.Client.Tests/BloodEffectSystemTests.cs`,
  mirroring the shape of `HitEffectSystemTests.cs`, covering: constructor
  capacity validation for burst/ground-mark/spurt capacities (throws on
  non-positive values); exactly one burst is created per accepted `Attack`
  event in a batch, and `Move`/`Damage`/`Outcome` events are ignored (AC1, §3.1);
  a burst's direction is derived from attacker position to victim position in
  the same `AgentView` batch (AC2); a burst carries the `Attack` event's
  `Weapon`/`HitLocation`/`Value`; a burst is marked lethal when its target
  appears in the same tick's `Death` set (AC4); when two attackers strike the
  same dying victim in one tick, both attacks produce a lethal burst and
  neither is dropped, with no attacker preferred over the other (AC5); an
  overkill `Value` still yields a clamped severity ratio; when the burst buffer
  is full, the oldest burst is overwritten (mirroring
  `HitEffectSystemTests.Ingest_WhenFull_ReplacesOldestEffect`), same for the
  ground-mark buffer (AC6); `Advance` ages and independently compacts bursts and
  ground marks, rejecting negative/non-finite elapsed time (mirroring
  `HitEffectSystemTests.Advance_RejectsNegativeOrNonFiniteElapsedTime`); `Clear`
  empties every buffer; setting `Intensity = GoreIntensity.Off` makes `Ingest`
  and `Advance` no-ops so no slot is ever occupied (AC11); setting
  `Intensity = GoreIntensity.Full` adds a lethal spurt only for lethal bursts,
  and produces longer-lived ground marks than `Stylized` (§3.7's Full bullet);
  two independently constructed systems fed identical event/agent batches and
  identical `Advance` steps produce identical stored state (AC9).
  Verification: fails to compile (`BloodEffectSystem` does not exist) —
  confirmed RED.
- [x] **P2-T2 (GREEN)**: implement
  `src/Hukbo.Client/Presentation/BloodEffectSystem.cs`, following
  `HitEffectSystem.cs`'s shape (a reusable `Dictionary<ulong, AgentView>` and
  `HashSet<ulong>` cleared and repopulated each `Ingest`, not reallocated; fixed
  arrays for bursts/ground marks/spurts sized at construction; in-place
  compaction in `Advance`; a mutable `Intensity` property defaulting to
  `GoreIntensity.Stylized`).
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~BloodEffectSystemTests`
  all green, then `./scripts/verify.ps1 -SkipBootstrap`.

### Phase 3 — Renderer + `PresentationCoordinator` wiring + draw order

Risk/rollback: revert `PresentationCoordinator.cs`, its test additions,
`BloodRenderer.cs`, and the `ArenaGame.Rendering.cs` hunk. `BloodEffectSystem`
and `BloodGeometry` remain but unreferenced — no compile break, since nothing
requires them to be used.

- [x] **P3-T1 (RED)**: extend `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`
  with `IngestTick_ForwardsEveryBatchToBlood` (mirroring
  `IngestTick_ForwardsEveryBatchToFeedAndHitEffects`), an assertion added to
  `ResetFor_ClearsDisposableStateAndPausesPlayback` asserting
  `coordinator.Blood`'s buffers are empty after reset (AC16), and
  `AdvanceEffects_AdvancesBloodAlongsideHitEffects`.
  Verification: fails to compile (`PresentationCoordinator.Blood` does not
  exist) — confirmed RED.
- [x] **P3-T2 (GREEN)**: modify `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`
  to add a `Blood` property (`BloodEffectSystem`), an optional capacity
  constructor parameter mirroring `hitEffectCapacity`, and wire it into
  `IngestTick`, `AdvanceEffects`, and `ResetFor` alongside `HitEffects`.
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~PresentationCoordinatorTests`
  all green.
- [x] **P3-T3** (no test — mirrors the zero-test precedent of
  `HitEffectRenderer.cs`, per `.claude/skills/hukbo-client-ui/SKILL.md`'s
  "Draw methods... are not unit tested"): implement
  `src/Hukbo.Client/Rendering/BloodRenderer.cs` with separate
  `DrawGroundMarks(...)` and `DrawBursts(...)` entry points (so the caller can
  interleave them with pawn drawing), each culling against `arenaBounds` before
  submission (matching `ArenaGame.Rendering.cs:204`'s `DrawPawns` culling, not
  `HitEffectRenderer`'s scissor-only approach, per design §4).
  Verification: `./scripts/verify.ps1 -SkipBootstrap` builds clean (no
  behavior yet visible since nothing calls it).
- [x] **P3-T4**: wire `src/Hukbo.Client/ArenaGame.Rendering.cs`'s `DrawArena`
  (`:130-146`) to call `BloodRenderer.DrawGroundMarks(...)` immediately before
  `DrawPawns(...)` and `BloodRenderer.DrawBursts(...)` immediately after,
  alongside the existing `HitEffectRenderer.Draw(...)` call.
  Verification: `./scripts/verify.ps1` (full gate — build, Core+Client tests,
  seed-1 determinism workload unaffected since only `Hukbo.Client` changed);
  visual confirmation deferred to the Phase 6 smoke checklist.

### Phase 4 — Settings persistence

Risk/rollback: revert `ClientSettings.cs`, `ClientSettingsStore.cs` and its test
file, `GoreIntensityManager.cs` and its test file, the `ArenaGame.cs`
constructor hunk, and the `UiThemeManager.cs` deleted-constructor hunk together
— they are one coupled change (see §10's serialization-point note); reverting
only part of it leaves a dangling `TrySave` signature mismatch.

- [x] **P4-T1 (RED)**: extend `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs`
  with: a missing file returns the provided theme default and
  `GoreIntensity.Stylized`; a hand-written pre-feature JSON file
  (`{"schemaVersion":1,"selectedThemeId":"signal"}`, no `goreIntensity` key)
  loads with `SelectedThemeId == "signal"` **and** `GoreIntensity == GoreIntensity.Stylized`
  (AC10, the exact risk named in design §7); `TrySave("command", GoreIntensity.Full)`
  round-trips both fields; a hand-written file with a valid schema/theme but an
  out-of-range `goreIntensity` (e.g. `99`) loads with the theme preserved and
  gore defaulted to `Stylized` (proving field-level isolation, §3 ambiguity 4);
  the existing `InvalidSettingsReturnDefault` theory cases are unchanged
  (schema mismatch and malformed JSON still fully reset).
  Verification: fails to compile (`ClientSettings.GoreIntensity` does not exist,
  `TrySave`'s new overload does not exist) — confirmed RED.
- [x] **P4-T2 (GREEN)**: modify `src/Hukbo.Client/Settings/ClientSettings.cs`
  (add `GoreIntensity GoreIntensity`) and
  `src/Hukbo.Client/Settings/ClientSettingsStore.cs` per the exact mechanism in
  §8: add a private `RawClientSettings` DTO, keep `SupportedSchemaVersion = 1`
  unchanged, validate schema/theme independently from gore intensity, and
  change `TrySave` to `TrySave(string selectedThemeId, GoreIntensity goreIntensity)`.
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~ClientSettingsStoreTests`
  all green.
- [x] **P4-T3 (RED)**: write `tests/Hukbo.Client.Tests/GoreIntensityManagerTests.cs`,
  mirroring `UiThemeManagerTests.cs`: selecting a new value changes it
  immediately and persists it; selecting the current or an undefined value
  changes nothing and does not persist; an unknown/invalid initial value falls
  back to `GoreIntensity.Stylized`.
  Verification: fails to compile (`GoreIntensityManager` does not exist) —
  confirmed RED.
- [x] **P4-T4 (GREEN)**: create `src/Hukbo.Client/Settings/GoreIntensityManager.cs`,
  mirroring `UiThemeManager.cs`'s two-constructor shape (a public convenience
  constructor and an `internal GoreIntensityManager(GoreIntensity initialValue, Func<GoreIntensity, bool> persist)`
  constructor used directly by tests).
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~GoreIntensityManagerTests`
  all green.
- [x] **P4-T5**: wire `src/Hukbo.Client/ArenaGame.cs`'s constructor (`:72-76`):
  store the `ClientSettingsStore` instance in a field, load settings once,
  construct `_themeManager` via `UiThemeManager`'s existing internal
  testable constructor with a persist delegate `id => settingsStore.TrySave(id, _goreManager.Value)`,
  construct `_goreManager` via `GoreIntensityManager`'s internal constructor
  with a persist delegate `value => settingsStore.TrySave(_themeManager.ActiveTheme.Id, value)`,
  and delete `UiThemeManager`'s now-unused public two-argument convenience
  constructor from `src/Hukbo.Client/Theming/UiThemeManager.cs` (it cannot
  correctly incorporate the sibling gore value, and `Grep` confirms it has
  exactly one caller, this one).
  Verification: `./scripts/verify.ps1 -SkipBootstrap` (build + existing
  `UiThemeManagerTests.cs` unaffected, since it exercises the internal
  constructor directly, not the deleted one).

  **Implemented with a deviation.** The parallel army-composition workstream
  had already landed `ArmyComposition` in `ClientSettings`, changed
  `TrySave` to take a composition, and rewritten `UiThemeManager`'s
  convenience constructor to re-read the whole settings file at save time
  (`UiThemeManager.TryPersistTheme`). That re-read carries the sibling gore
  value forward correctly, so the reason for deleting the convenience
  constructor no longer holds and it was kept. `ArenaGame` reuses the
  existing `_settingsStore` field, constructs `_goreManager` with a persist
  delegate (`ArenaGame.TryPersistGoreIntensity`) that mirrors
  `TryPersistTheme`, and `TrySave` now takes
  `(themeId, composition, goreIntensity)` rather than the two-argument shape
  §8 anticipated.
- [x] **P4-T6**: in the same constructor, immediately after `_presentation` is
  initialized, set `_presentation.Blood.Intensity = _goreManager.Value;` so a
  restored preference (e.g. `Full`) takes effect from tick zero without opening
  the menu.
  Verification: `./scripts/verify.ps1 -SkipBootstrap`; interactive confirmation
  deferred to the Phase 6 smoke checklist.

### Phase 5 — Menu selector control + focus order + tests

Risk/rollback: revert `GoreIntensitySelector.cs` and its test file, the
`MenuOverlay.cs` hunks (constructor, index scheme, `Update`, `Draw`, `Layout`),
the `MenuOverlayFocusTests.cs` additions, the `MenuInteraction` field addition,
and the `ArenaGame.cs` `Update` hunk. `MenuOverlay.cs`'s five hunks must land
together (see §10).

- [x] **P5-T1 (RED)**: write `tests/Hukbo.Client.Tests/GoreIntensitySelectorTests.cs`,
  mirroring `UiThemeSelectorTests.cs`: exposes the three ordered names ("Off",
  "Stylized", "Full"); previous/next wrap at both ends; `Keys.Left`/`Right`/`Enter`/`Space`
  select the expected adjacent value only when focused; pointer activation on
  the previous/next arrow bounds selects, hover alone does not.
  Verification: fails to compile (`GoreIntensitySelector` does not exist) —
  confirmed RED.
- [x] **P5-T2 (GREEN)**: create `src/Hukbo.Client/UI/GoreIntensitySelector.cs`,
  mirroring `UiThemeSelector.cs`'s shape exactly but over the three
  `GoreIntensity` values instead of the theme catalog, reusing
  `standards.Shared.Selector` (`UiThemeSelectorLayout`) for sizing so no
  `Content/Themes/ui-theme-standards.json` change is needed (§11's assumed
  item).
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~GoreIntensitySelectorTests`
  all green.

  **Implemented with a deviation.** Reusing `standards.Shared.Selector` for
  sizing was not sufficient on its own: the army-composition workstream added
  a sixth menu button, so the theme selector, six buttons, and a second
  96px-tall selector no longer fit inside `menu.panelHeight` (`590`). The
  panel height was raised to `660` in both
  `src/Hukbo.Client/Content/Themes/ui-theme-standards.json` and the built-in
  fallback in `src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs` — the small
  JSON+catalog change §11 anticipated. A new test,
  `MenuOverlayFocusTests.ThePanelIsTallEnoughForEveryMenuControl`, guards it
  against future button additions.
- [x] **P5-T3 (RED)**: extend `tests/Hukbo.Client.Tests/MenuOverlayFocusTests.cs`
  with a case using `controlCount: 7` (the new total, `_buttons.Length + 2`)
  proving the new terminal index (`6`) participates in keyboard wraparound the
  same way index `0` does today. **The literal numbers are now `8` and `7`,
  not `7` and `6`, because the parallel army-composition workstream added a
  sixth menu button; the added tests read `MenuOverlay.ControlCount` and
  `MenuOverlay.GoreSelectorControlIndex` instead of hardcoding either total.**
  Verification: this test can pass immediately against the existing
  `ResolveFocusedControlIndex` (it is a pure function already generic over
  `controlCount`), so it is not RED in the compile sense — it is RED in the
  sense that it encodes the not-yet-wired new control count before
  `MenuOverlay.cs` itself is updated to pass `7` at its call sites; write it
  first regardless, per the phase's TDD ordering.
- [x] **P5-T4 (GREEN)**: modify `src/Hukbo.Client/MenuOverlay.cs` per the exact
  scheme in §7: add a `_goreSelector` field constructed in the constructor;
  extend `MenuInteraction` with a new `GoreIntensity? SelectedGoreIntensity`
  field; update `Layout` to position `_goreSelector.Bounds` below the last
  button; update `Update` to pass `controlCount: _buttons.Length + 2`, add
  gore-selector hover detection at the new terminal index, and return a
  `MenuInteraction` carrying `SelectedGoreIntensity` when the gore selector is
  activated; update `Draw` to draw `_goreSelector` after the buttons loop.
  Verification: `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~MenuOverlayFocusTests`
  all green, then `./scripts/verify.ps1 -SkipBootstrap`.

  Two additions the plan did not name, both required for correctness: the
  button-activation paths now guard on `MenuOverlay.IsButtonControlIndex`,
  because the previous `index > 0` test would have indexed `_buttons` out of
  range once a control existed past the last button; and `Draw` takes the
  active gore level so the selector can paint it, which also updated the one
  call site in `src/Hukbo.Client/ArenaGame.Rendering.cs`.
- [x] **P5-T5**: wire `src/Hukbo.Client/ArenaGame.cs`'s `Update` (near `:151`,
  where `menuInteraction.SelectedThemeId` is handled today): when
  `menuInteraction.SelectedGoreIntensity is { } selectedGore`, call
  `_goreManager.TrySelect(selectedGore)` and set
  `_presentation.Blood.Intensity = _goreManager.Value`.
  Verification: `./scripts/verify.ps1 -SkipBootstrap`; interactive confirmation
  deferred to the Phase 6 smoke checklist.

### Phase 6 — Gate run + smoke checklist rows

Risk/rollback: verification-only phase; a gate failure routes back to the
implicated phase's own rollback line above, not a fresh revert here.

- [ ] **P6-T1**: run `./scripts/verify.ps1` (full, not `-SkipBootstrap`) and
  record the exact output — per `CLAUDE.md` §4, never claim a change is
  verified without pasting the actual result.
- [ ] **P6-T2**: confirm the seed-1 200-agent state hash and event hash from
  this run match the recorded baseline in `docs/development/testing.md:49`
  (`6EBB1EA63114F6CE` / `941377BD43C556FF`) exactly (AC8).
- [ ] **P6-T3**: confirm the 500-agent stress run still terminates at tick 309
  (`docs/development/testing.md:54`) (AC8).
- [ ] **P6-T4**: add the following rows to the "Spectator clarity smoke" table
  in `docs/development/testing.md` (numbered continuing from the existing 15),
  left `PENDING` — only a human at an interactive desktop may flip a row to
  `PASS`, per `CLAUDE.md` §6:

  | Check | Expected observation | Actual | Status |
  | --- | --- | --- | --- |
  | 16. Observe default-view blood | At the default gore setting (Stylized) and the default camera fit, landing a blow shows a directional spray and, on a kill, a fading ground mark, both visible without zooming in. | Not run | PENDING |
  | 17. Change gore intensity via menu | Open Menu; the new Gore Intensity control cycles Off -> Stylized -> Full -> Off via Left/Right and the pointer arrows; each choice visibly changes blow rendering: Off shows nothing, Stylized shows spray and a fading mark, Full additionally shows a sustained spurt on a kill and denser, longer-lived marks. | Not run | PENDING |
  | 18. Gore intensity persists | Set gore to Full (or Off), fully close and relaunch the game; the same setting is active immediately, without reopening the menu. | Not run | PENDING |
  | 19. Blood clears on round reset | With visible sprays or marks on screen, trigger Next Round, and separately Full Reset; in both cases blood clears immediately alongside the event log, inspector, and summary. | Not run | PENDING |
  | 20. Blood readability across themes | Cycle all five visual themes while blood is on screen; blood remains visually distinguishable from both faction pawn colors and the arena surface in every theme, including High Contrast. | Not run | PENDING |
  | 21. Speed/gore independence | At 1x, 2x, and 4x speed, toggle gore Off versus Full and confirm the tick counter in the window title advances at the same visible rate regardless of the gore setting. | Not run | PENDING |
