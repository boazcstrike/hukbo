# Existing-Code Analysis — Visual Improvement Planning

Read-only analysis of the current Hukbo rendering, presentation, and asset
stack, written to ground a visual-improvement plan. Every load-bearing claim
below was verified against the current source on disk on 2026-07-28. File
references are relative to the repository root
(`C:\Users\boazs\webdev\autonomous-arena`).

## 1. How pawns are drawn today

**There are no sprites.** Every pawn is composed at draw time from solid-color
rectangles and rotated line segments, all drawn with a single runtime-created
1x1 white `Texture2D` (`src/Hukbo.Client/ArenaGame.cs:199-200`). The shapes and
their positions come from `PawnGeometry.Create`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:90-216`), which returns a
`PawnLayout` record of rectangles and line endpoints; `PawnRenderer.Draw`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:64-169`) only fills what the
layout computed.

Draw order within one pawn (`PawnRenderer.cs:107-168`), back to front:

1. Ground base ring in the faction color with a shadow inset
   (`DrawGroundBase`, lines 171-184).
2. Secondary equipment — the Wasay axe head, or the Itak off-hand piece at
   Medium/High detail only (`DrawSecondaryEquipment`, lines 290-329).
3. Torso as a "stepped capsule" (three stacked rectangles) with an outline
   pass then a clothing-color pass, plus an accent-color belt at High detail
   (`DrawTorso`, lines 186-219).
4. Shield block, deliberately after the torso so it overlaps it — drawn at
   every detail tier because it distinguishes solo from shielded warriors
   (`DrawShield`, lines 337-364; rationale in `PawnGeometry.cs:262-274`).
5. Head as a stepped disk, outline then skin color (`DrawHead`, lines
   221-229).
6. Head treatment (cropped hair / headcloth / wrapped cloth), skipped at Low
   detail (`DrawHeadTreatment`, lines 231-288).
7. Swing arc trail, stroked as six line segments along an arc the layout
   already computed (`DrawSwingTrail`, lines 371-395).
8. Weapon: a grip line, a broad blade line, and a highlight line, with
   per-weapon `gripEnd` and `widthMultiplier` values — Itak 0.30/2.1,
   Kampilan 0.22/2.45, Wasay 0.28/2.9, Kalis 0.16/1.5 (`DrawWeapon` /
   `DrawBlade`, lines 406-503). Per-weapon start/end offsets and thickness
   live in `PawnGeometry.CreateWeaponLayout` (`PawnGeometry.cs:300-369`).
9. Selection corner marks (hover yellow, selected white) or a dead X mark
   (lines 149-168, 580-639).

Sizes scale with `apparentScale = clamp(cameraZoom * 1.35, 0.72, 2.40) *
scaleMultiplier`, and that scale picks a detail tier: Low below 0.95, Medium
below 1.80, High above (`PawnGeometry.cs:65-116`). Base proportions: torso
12 pawn-units tall by 7 wide (modified by stature/build multipliers), head 7,
ground ring 13x4 (`PawnGeometry.cs:118-151`).

**Weapon and shield visualization** is purely these primitive shapes; there is
no per-weapon texture or sprite anywhere. The Wasay is a thin haft plus a
square iron head at the far end (`PawnGeometry.cs:412-416`); the shield is a
solid charred-wood block with a lighter vertical seam at Medium/High detail
(`PawnRenderer.cs:348-363`).

**Frustum culling is pose-blind by contract**: `PawnRenderer.GetBounds`
(`PawnRenderer.cs:48-57`) ignores the swing pose so the drawn-pawn set is
never a function of animation phase. The doc comment (lines 27-47) names this
"draw-list determinism" and records the accepted cost (weapon tips clip at the
panel edge). Any visual change that grows a pawn's possible extent must keep
this property.

**Per-frame appearance construction**: `ArenaGame.DrawPawns`
(`src/Hukbo.Client/ArenaGame.Rendering.cs:238-299`) recomputes
`PawnAppearanceFactory.Create(entityId, weapon, shield)` for every live pawn
every frame — appearance is a pure function, never cached.

### How PawnAppearanceFactory selects appearance

`src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs:22-41`. Deterministic
inputs are exactly three: `ulong entityId`, `WeaponId`, `ShieldId`.

- The entity ID is XORed with three distinct salt constants
  (`0xA0761D6478BD642F`, `0xE7037ED1A0B428DB`, `0x8EBC6AF09C88C6E3`) and each
  result runs through a private SplitMix64-finalizer-style `Mix` function
  (lines 116-125: add golden-ratio constant, two xor-shift-multiply rounds).
  This produces three independent streams: bodyMix, clothingMix, detailMix.
- Modulo selection from those streams: stature 0.90/1.00/1.10, build
  0.86/1.00/1.18, head treatment (3 kinds), clothing color (4: cream, indigo,
  textile red, patina green), accent color (3), skin color (3), head-treatment
  color (3). Different bit windows (`>> 8`, `>> 16`) of one stream feed
  different traits.
- **The load-bearing rule** (comment at lines 19-21, pinned by tests):
  equipment identity — weapon role and shield role — comes only from the
  authoritative Core loadout, never from the entity ID.
  `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs` pins identical
  appearance for a stable ID+weapon (line 8), distinct silhouettes for all
  four weapons (line 48), never deriving weapon role from ID alone (line 60),
  and shield role from loadout only, never entity ID (lines 198, 222).

This factory is the established precedent for any new stable variant
selection: salt the entity ID, mix, take a modulo, and keep everything
equipment-identity-related out of it.

`PawnAppearance` (`src/Hukbo.Client/Presentation/PawnAppearance.cs:55-158`)
also carries the player-facing pair-form weapon labels ("Kampilan — Great
Blade"), the `WeaponEvidenceTier` enum, and evidence notes — the historical
accuracy policy's presentation metadata lives here, not in Core.

## 2. Data available to the Client

`AgentView` (`src/Hukbo.Core/Simulation/AgentView.cs:11-22`) is the entire
per-agent surface the Client sees:

- `ulong EntityId` — stable, unique, the deterministic tie-breaker.
- `int FactionId`
- `int XRaw, YRaw` — fixed-point position; the Client divides by
  `FixedPoint.Scale` to get world floats (`ArenaGame.Rendering.cs:253-255`).
- `int HitPoints, MaximumHitPoints`
- `ulong? TargetEntityId`
- `AgentIntent Intent`
- `bool IsAlive`
- `CombatLoadout Loadout`
- `MovementResolution MovementResolution` (defaulted; explains collision
  outcomes for the spectator).

`CombatLoadout` (`src/Hukbo.Core/Combat/CombatIdentity.cs:111-114`) is
`(WeaponId Weapon, ArmorId Armor, ShieldId Shield)`. The enums carry explicit
numeric values that are part of the deterministic replay and content-hash
contract — `Kampilan=1, Wasay=2, Kalis=3, Itak=4`; `ArmorId.LightOrganic=1`;
`ShieldId.None=1, TallHardwood=2` — with "do not renumber or reorder" doc
comments (`CombatIdentity.cs:3-13, 69-85`).

**Immutable identity for stable variant selection**: `EntityId` plus `Loadout`
(and `FactionId`). These are fixed for an agent's lifetime within a match, so
any deterministic hash of them yields a stable per-warrior variant across
frames and across replays of the same seed. There is no name, age, or other
identity data. The scenario `Seed` is also available to the Client (used by
the backdrop, `ArenaGame.Rendering.cs:217-222`) for match-stable but
warrior-independent variation.

## 3. Asset loading and sprite batching

**Content pipeline** (`src/Hukbo.Client/Content/Content.mgcb`): exactly six
`.spritefont` entries (UiCaption, UiBody, UiLabel, UiSubtitle, UiTitle,
UiDisplay), platform DesktopGL, profile Reach. **Nothing else goes through the
pipeline — no textures, no sprites, no audio.**

What exists on disk under `src/Hukbo.Client/Content/`:

- `Fonts/` — the six spritefonts plus their source TTFs (Bebas Neue Regular,
  Rajdhani SemiBold) and OFL licenses.
- `Audio/` — raw PCM WAV files (per-weapon, per-hit-location attack sounds
  and others), loaded at runtime via `SoundEffect.FromStream`
  (`src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs:219`), bypassing the
  pipeline entirely.
- `Themes/ui-theme-standards.json` — the theme catalog document, read and
  validated at runtime by `UiThemeCatalog`.

**There is no texture or sprite loading anywhere in the game today.** The only
texture ever created is the 1x1 white `_pixel` in `ArenaGame.LoadContent`
(`ArenaGame.cs:191-200`), disposed on unload (line 247). There is no atlas
support, no animation framework, no sprite-layering system. Any plan that
assumes those exists is wrong; they would all be new infrastructure.

**Sprite batching** (`src/Hukbo.Client/ArenaGame.Rendering.cs`): exactly two
`SpriteBatch.Begin/End` pairs per frame.

1. Arena layer (lines 60-81): `SpriteSortMode.Deferred`,
   `BlendState.AlphaBlend`, `SamplerState.PointClamp`, scissor rectangle set
   to the arena panel with a scissor-enabled `RasterizerState`.
2. UI layer (lines 94-156): Deferred, AlphaBlend, `LinearClamp`; draws
   status bar, control bar, inspector, event log, sound log, summary, menu,
   army composition panel.

Because everything samples one texture, the deferred batcher effectively
submits the arena as a small number of GPU batches; the CPU cost is the
per-`Draw` call count. Every rectangle and every line segment is one
`spriteBatch.Draw` call. Draw order inside a batch is call order —
`layerDepth` is always 0 (`PawnRenderer.cs:665`); there is no depth sorting.

## 4. Camera

`src/Hukbo.Client/SpectatorCamera.cs`:

- Zoom: `MinimumZoom = 0.05f`, `MaximumZoom = 12f`, default `_zoom = 1f`
  (lines 9-17). Mouse wheel multiplies by `1.15^steps` where a step is one
  120-unit wheel detent, clamped to that range (lines 101-108).
- Initial fit: `Fit` runs once (guarded by `_isFitted`) and picks the zoom
  that shows the map at 88% of panel width / 80% of panel height, clamped
  (lines 113-129). Called every frame from `Draw`
  (`ArenaGame.Rendering.cs:40`) but a no-op after the first.
- Pan: WASD and arrow keys, 420 screen px/s divided by zoom (lines 11,
  94-99). `Update` returns true when the spectator panned, so auto-pan yields.
- Auto-pan: `ArenaAutoPanController` (`src/Hukbo.Client/ArenaAutoPanController.cs`)
  is a two-state assistant — idle while a live agent is visible, otherwise
  drives `MoveCenterTo` — with the pure math in
  `src/Hukbo.Client/ArenaAutoPan.cs`, both covered by
  `tests/Hukbo.Client.Tests/ArenaAutoPanTests.cs`.
- Projection is a plain linear transform:
  `screen = (world - center) * zoom + panelCenter` (lines 134-138), inverse
  at lines 143-149. No rotation, no smoothing/inertia, no zoom-to-cursor.

How zoom reaches rendering: the camera zoom is passed into
`PawnGeometry.Create` and clamped into `apparentScale` in [0.72 * multiplier,
2.40 * multiplier] (`PawnGeometry.cs:107-110`), which selects the three
detail tiers. Decals clamp similarly to [0.35, 3.0] apparent scale
(`PlainsBackdropGeometry.cs:303-331`). So pawns and decals stop growing or
shrinking past the clamps even though the world-to-screen mapping keeps
scaling — at maximum zoom (12) the map is enormous but a pawn caps at
apparentScale 2.40.

## 5. Backdrop

`PlainsBackdropRenderer.Draw` (`src/Hukbo.Client/Rendering/PlainsBackdropRenderer.cs:34-48`)
draws two things, both beneath pawns and the arena border, both flat-color
rectangles on the pixel texture:

1. **Ground grid**: cells targeting 64 world units, capped at 48x48
   (`PlainsBackdropGeometry.cs:39-46`), each filled with one of three shades
   produced by lerping from `theme.Colors.ArenaSurface` toward
   `theme.Colors.ArenaBorder` by 0.00 / 0.06 / 0.12
   (`GroundShadeInterpolation`, line 86-87; `GetShade`,
   `PlainsBackdropRenderer.cs:137-141`). The shade index is a deterministic
   hash of cell column, row, and the scenario seed XOR a named
   `PresentationSalt`, through `SplitMix64`
   (`PlainsBackdropGeometry.cs:221-228`), so ground shading is stable for a
   match regardless of camera.
2. **Scatter decals**: up to 256 (`MaximumDecalCount`), one per 6,000 square
   world units, kinds `GrassTuft`, `DirtPatch`, `Rock`
   (`PlainsBackdropGeometry.cs:14-19, 50-58`). Each is a plain square, base
   size 5 px at apparent scale 1, shaded by lerp values 0.10 / 0.16 / 0.22
   (`DecalKindInterpolation`, line 94-95). Positions and scale factors come
   from `SplitMix64(seed ^ PresentationSalt)` in `GenerateDecals` (lines
   249-290), which runs exactly twice per match — scenario construction and
   reset (`ArenaGame.cs:166, 952`) — never per tick or frame.

**There is no texture, no grass blades, no wind, no motion of any kind.** The
backdrop is fully static within a match. A hard ceiling
`MaximumBackdropInterpolation = 0.22` (line 80) exists so the backdrop can
never compete with pawn silhouettes, specifically protecting the
high-contrast theme. The "GrassTuft" name is aspirational — visually it is a
slightly-tinted square.

The grid render loop calls the tested per-cell formula directly to stay
zero-allocation (`PlainsBackdropRenderer.cs:26-31` records the duplicated-
formula bug that motivated this shape — a known repository lesson).

## 6. Effects: how they are driven and where state lives

All presentation effect state lives in `PresentationCoordinator`
(`src/Hukbo.Client/Presentation/PresentationCoordinator.cs`) and its five
owned systems, all fixed-capacity arrays allocated at construction, never
grown:

- `BattleEventFeed` (capacity from caller; the 200-event cap comes from
  CLAUDE.md §5).
- `HitEffectSystem` (256) — keyed on `Damage` events; drives the impact ring
  (drawn by `HitEffectRenderer`) and a 0.09-second white hit pulse blended
  into pawn colors (`HitEffectSystem.cs:7, 96-108`;
  `PawnRenderer.ApplyHitPulse`, line 684-685).
- `BloodEffectSystem` (256 bursts / 384 ground marks / 32 lethal spurts) —
  keyed on `Attack` events, draws only for `Landed` resolutions
  (`BloodEffectSystem.cs:25-48`). Holds the `GoreIntensity` value; setting
  `Off` clears everything on screen (lines 56-75).
- `SwingAnimationSystem` (256) — at most one in-flight swing per attacker,
  upsert-in-place, oldest-evicted with a sequence tie-break
  (`SwingAnimationSystem.cs:159-193`).
- `ClashEffectSystem` (256) — crosses where a blow was blocked, parried, or
  deflected (fixed-capacity pool copying the hit-effect shape,
  `ClashEffectSystem.cs:6-23`).

**Timing model** — the critical distinction (`PresentationCoordinator.cs:59-78`
and `SwingAnimationSystem.cs:10-17`):

- `IngestTick(events, agents)` runs once per completed simulation tick and
  starts effects from authoritative `BattleEvent`s.
- `AdvanceEffects(elapsedSeconds, speedMultiplier)` runs on wall-clock frame
  time. The swing clock alone is multiplied by playback speed (1x/2x/4x),
  because attacks arrive at playback speed and unscaled swings would render
  every warrior permanently mid-swing at 4x. Hit, blood, and clash effects
  advance on unscaled presentation seconds — they are "wounds already dealt".

Swing animation: four phases (Anticipation 36%, Strike 20%, ImpactHold 20%,
Recovery 24% — all marked PROVISIONAL) with linear keyframe interpolation and
three impact-hold branches by `AttackResolution` (landed stops on target,
blocked/parried/deflected recoil, evaded follows through)
(`src/Hukbo.Client/Rendering/SwingGeometry.cs:78-244`). The per-frame pose
map is filled into a caller-owned dictionary by `SwingPoseResolver.Resolve`
to keep the draw path allocation-free
(`src/Hukbo.Client/Rendering/SwingPoseResolver.cs:39-68`).

**Settings hooks**: `GoreIntensity` is the only visual-effect setting. There
is no reduced-motion setting anywhere in the Client (verified by search).

## 7. Settings system

`ClientSettings` (`src/Hukbo.Client/Settings/ClientSettings.cs:3-7`):
`(int SchemaVersion, string SelectedThemeId, ArmyComposition Composition,
GoreIntensity GoreIntensity)`. Schema version is 3
(`ClientSettingsStore.cs:14`). Persisted as camelCase JSON at
`%LocalAppData%\Hukbo\settings.json` (`ClientSettingsStore.cs:34-43`), written
atomically via a temp file plus `File.Replace` (lines 149-181), with every
load/save/failure logged on the `settings` channel.

**The GoreIntensity precedent for adding a visual setting** is fully worked
and worth copying exactly:

- An enum with explicit numeric values and a "do not renumber" persisted-
  contract comment (`GoreIntensity.cs:4-8`).
- Deserialization through a private `RawClientSettings` record whose fields
  are all nullable/unvalidated, so each field validates independently — a
  field added later cannot cause an older file to be discarded, and a corrupt
  value in one field resolves to its default without losing the saved theme
  (`ClientSettingsStore.cs:59-63, 235-269`).
- A small manager owning the live value with a persist delegate injected so
  it is testable without the filesystem, persisting the moment a change is
  made, not rolling back on save failure (`GoreIntensityManager.cs:9-44`).
- A menu selector UI (`src/Hukbo.Client/UI/GoreIntensitySelector.cs`) and
  tests (`GoreIntensityManagerTests.cs`, `GoreIntensitySelectorTests.cs`,
  `ClientSettingsStoreTests.cs`).

## 8. Themes

`UiThemeColors` (`src/Hukbo.Client/Theming/UiTheme.cs:11-38`) declares exactly
27 semantic color roles: CanvasBackground, ArenaSurface, ArenaBorder,
StatusSurface, OverlayScrim, PanelSurface, PanelAlternate, PanelBorder,
TextPrimary, TextSecondary, TextDisabled, TextInverse, ActionDefault,
ActionHover, ActionFocus, ActionPressed, ActionActive, ActionDisabled,
StatusInfo, StatusSuccess, StatusWarning, StatusDanger, TeamA, TeamB,
OtherFaction, Selection, NewEvent.

The catalog document (`src/Hukbo.Client/Content/Themes/ui-theme-standards.json`)
requires five themes: `command` (default), `field-manual`, `signal`,
`broadcast`, `high-contrast`. `UiThemeCatalog` validates the document at load —
theme count, required IDs, all 27 roles present, and **contrast pairs with
minimum ratios** (`UiThemeCatalog.cs:134, 287-294, 528-556`), falling back to
a built-in theme on failure (`UiThemeCatalogFallback.cs`, logged as
`assets.theme.fallback`).

**Faction colors on the battlefield are theme-independent by design**:
`FactionColorPalette` (`src/Hukbo.Client/UI/FactionColorPalette.cs:15-36`)
paints pawns in fixed colors — faction A blue (64,164,255), faction B red
(255,91,105), other gold (231,199,84) — because pawns sit on the arena canvas,
not a themed panel surface. The themed `TeamA`/`TeamB` roles are used for UI
text (inspector, event log) via `GetThemeColor`. All the pawn body/clothing
colors are likewise fixed constants in `PawnAppearanceFactory` and
`PawnRenderer`, not theme roles.

**Color-blind considerations**: nothing explicit exists. What exists is the
contrast-pair validation machinery and the `high-contrast` theme, plus the
backdrop-interpolation ceiling written specifically to protect it
(`PlainsBackdropGeometry.cs:72-80`). The blue-versus-red faction pair has no
color-blind alternative today; shape (not color) already distinguishes
equipment, which is the direction the codebase's own comments push
(shield drawn at every tier so the distinction survives distance).

## 9. Tests that bind visual work

`tests/Hukbo.Client.Tests/` — 50+ test files, all GPU-independent. The
governing pattern (also in CLAUDE.md §5 and the `hukbo-client-ui` skill):
**Client presentation tests must never construct `ArenaGame`, a graphics
device, a sprite batch, or a window.** Consequently:

- All drawing *logic* lives in pure geometry/value classes that are tested:
  `PawnGeometryTests`, `PawnAppearanceFactoryTests`,
  `PlainsBackdropGeometryTests`, `SwingGeometryTests`,
  `SwingPoseResolverTests`, `SwingAnimationSystemTests`,
  `HitEffectGeometryTests`, `HitEffectSystemTests`, `BloodGeometryTests`,
  `BloodEffectSystemTests`, `ClashEffectGeometryTests`,
  `ClashEffectSystemTests`, `PresentationCoordinatorTests`,
  `ArenaAutoPanTests`, `UiThemeCatalogTests`, `UiThemeManagerTests`.
- The renderer classes (`PawnRenderer.Draw`, `PlainsBackdropRenderer`,
  `BloodRenderer`, `HitEffectRenderer`, `ClashEffectRenderer`) are *not* unit
  tested, deliberately (`PlainsBackdropRenderer.cs:24` says so explicitly).
  New visual logic must be pushed into geometry types to be testable; the
  renderers should stay draw-only sinks.

Hygiene and boundary tests a visual change can trip:

- `SourceHygieneTests.OnlyTheEntryPointsWriteDirectlyToTheConsole`
  (`tests/Hukbo.Client.Tests/SourceHygieneTests.cs:27-44`) — scans all of
  `src/` for `Console.`; only the two `Program.cs` files may touch it.
- `SourceHygieneTests.TheCoreProjectDoesNotImportTheDiagnosticsNamespace`
  (lines 52-64) — text-scans `src/Hukbo.Core` for `Hukbo.Diagnostics`;
  the assembly-level counterpart is
  `tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs`, which also
  runs the workload silent versus traced and requires identical outcomes.
- `tests/Hukbo.Core.Tests/DeterminismTests.cs` — pins same-seed runs to
  identical event streams and state hashes. The constant at line 33
  (`0x5BEBA7A68F69BE0D`, `PreClashTerminalStateHash`) is the terminal state
  hash of the zero-interception control run — preset V1 with
  `ClashProfile.Neutral` — and is a separate guard, not the gate workload's
  oracle. The canonical gate workload (200 agents, 10,000 ticks, seed 1,
  current default preset V2) is checked against the recorded seed-1
  reference pair in `docs/development/testing.md` ("The Phase 2 reference
  pair"): `stateHash 27DC94C6E9A01E35`, `eventHash 372C9217E5CB8BE9`.
  Client-side visual work cannot move either set of values, and must not
  add anything to Core that could.
- `PawnAppearanceFactoryTests` — pins the appearance-determinism and
  equipment-from-loadout-only rules (section 1 above), plus every weapon
  carrying an evidence note (line 184).

The canonical gate (`./scripts/verify.ps1`) runs restore, format
verification, Release build, Core + Client tests, then the 200-agent /
10,000-tick / seed-1 headless determinism workload. Interactive visual
behavior is only proven by the manual smoke checklist in
`docs/development/testing.md` — no test may flip those rows.

## 10. DiagnosticLog fit for a missing-asset diagnostic

`Hukbo.Diagnostics` provides `DiagnosticLog` writing JSON Lines to
`artifacts/logs/`. Channels (`src/Hukbo.Diagnostics/LogChannel.cs:8-42`):
`boot`, `assets`, `settings`, `sim`, `audio`, `input`, `ui` (reserved,
nothing emits on it yet).

The `assets` channel is documented as "Content loaded from disk at runtime:
themes, fonts, sounds" (line 16) and already carries exactly the precedent a
texture/sprite loader would follow (`src/Hukbo.Diagnostics/LogEvents.cs:29-35`):
`assets.font.loaded` / `assets.font.failed`, `assets.sound.scanned` /
`assets.sound.missing` / `assets.sound.loadFailed`, `assets.theme.loaded` /
`assets.theme.fallback`. A missing-texture diagnostic would be a new `const`
on `LogEvents` (for example `assets.texture.missing`), emitted at `warn` with
an optional `msg`, following the four enforced rules: stable dotted
identifier declared on `LogEvents`, six leading fields in order, flat
camelCase payload, zero allocation when disabled. The `ClientSettingsStore`
shows the exact call shape (`ClientSettingsStore.cs:76-92`). Full procedure in
`.claude/skills/hukbo-debug-logging/SKILL.md`.

## 11. Performance baseline

- **Per-tick allocation budget**: the collision system's warm-tick allocation
  ceiling is 900,000 bytes, pinned in the standards
  (`SIMULATION-GAME-STANDARDS.md:831`, "the event stays at 72 bytes and the
  collision allocation ceiling stays at 900,000") and recorded in
  `docs/development/testing.md:136`; a later measurement discussion at
  `testing.md:355` shows the budget is actively defended rather than
  casually raised. Presentation follows the same discipline informally: the
  draw path is kept free of heap allocation (`SwingPoseResolver.cs:19-21`,
  zero-allocation grid loop, fixed-capacity effect pools).
- **Benchmark harness**: `scripts/benchmark.ps1` runs the headless workload
  with defaults `-Agents 200 -Ticks 10000 -Seed 1`. It measures simulation,
  not rendering — **there is no render/draw-call benchmark today**. `tools/`
  holds only hand-run audio and balance harnesses (`Hukbo.Tools.CueDemand`,
  `MixAnalysis`, `VoiceStress`, `WeaponBalance`), none in the solution or
  gate.
- **Current draw-call characteristics** (counted from the code, not
  measured): everything is `spriteBatch.Draw` on one 1x1 texture in two
  deferred batches. Ground grid worst case 48x48 = 2,304 cell fills plus up
  to 256 decals per frame; each pawn is roughly 10-25 draw calls depending on
  detail tier and state (ring 2, torso 6+belt, head 6, treatment 1-2, shield
  1-2, weapon 3, secondary 0-1, trail 0-6, selection 8). At the default 204
  agents that is on the order of 3,000-8,000 sprite submissions per frame —
  cheap on GPU (single texture, deferred), linear CPU cost. Any sprite/atlas
  plan changes this profile and has no existing measurement to regress
  against.

## 12. Constraints inventory binding visual work

From `CLAUDE.md` and `SIMULATION-GAME-STANDARDS.md` (§4, §8-10), as they land
on Client visual work:

- **Presentation must never affect the simulation.** Same seed + build +
  commands must yield identical state hash, event hash, winner, and event
  stream regardless of anything visual. The logging-neutrality test is the
  enforcement model to copy.
- **`Hukbo.Core` boundaries**: Core must not reference MonoGame, the
  filesystem, network, windowing, audio, wall clock, or `Hukbo.Diagnostics`.
  Visual variant data, appearance catalogs, and evidence-tier metadata belong
  in the Client (the `PawnAppearance` precedent). Client must not decide
  targeting, damage, retreat, or victory.
- **RNG**: `System.Random` is banned everywhere. Presentation randomness uses
  `SplitMix64` seeded from stable inputs with a named presentation salt —
  `PlainsBackdropGeometry.PresentationSalt` and the `PawnAppearanceFactory`
  mix constants are the two precedents.
- **Fixed-point** for anything reaching the state hash; presentation floats
  are fine because they never feed back (the Client converts `XRaw/YRaw`
  outward only).
- **Enum stability**: changing `WeaponId`/`ShieldId` numeric values, enum
  order, roster order, or weights requires a new preset version plus new
  golden expectations. Visual work should never need to touch them.
- **`TreatWarningsAsErrors` repo-wide, nullable enabled.** No weakening tests
  or analyzers to get green.
- **Dependencies**: versions centralized in `Directory.Packages.props`;
  adding any package (e.g. a texture/atlas library) is a reviewed dependency
  change with lock-file regeneration.
- **No CI**; the local gate `./scripts/verify.ps1` is canonical and its real
  output is the only verification evidence.
- **Battle event feed retains at most 200 ordered events.**
- **No unbounded caches; do not cache targets.** Fixed-capacity pools with
  named cap constants are the accepted pattern (effect systems, decal cap).
- **No derived caches, render data, or metrics in snapshots.**
- **Game stays fully offline**: no runtime network asset fetching; `sfx.ps1`
  is an authoring-time exception for audio only.
- **Historical policy (CLAUDE.md §7)**: cultural identifications only in pair
  form with a recorded evidence tier; a post-period name (>1 century gap) is
  unusable; Boxer Codex guides silhouette and color, not exact cataloging;
  gameplay tuning values marked PROVISIONAL in comments (already the practice
  in `PawnGeometry`/`SwingGeometry`). New visual variants depicting clothing,
  tattoos, or regalia will need evidence handling of the same kind.
- **Workflow**: design doc then plan doc before implementation; interactive
  visuals are only proven by the manual smoke checklist; §10 of the standards
  asks nine acceptance questions including "can a spectator discover this
  effect without reading source code?".
- **Console/logging rules**: no `Console.*` outside the two entry points; new
  diagnostics through `DiagnosticLog` with `LogEvents` constants.
- **Do-nots**: no rigid-body physics, no ECS framework before a profiler
  demands it, no terrain/pathfinding/morale before their gates.

## 13. Extension points and rigid spots

Natural attachment points, in the order a visual plan would likely use them:

- **Variant catalogs**: `PawnAppearance` is a record built by a pure factory
  from `(entityId, weapon, shield)`. Adding a trait means adding a field,
  drawing another modulo from an existing or new salted mix stream, and
  teaching `PawnGeometry`/`PawnRenderer` to lay it out and fill it. The tests
  in `PawnAppearanceFactoryTests` define the invariants any new trait must
  keep (stability, equipment-from-loadout-only).
- **Layered pawn composition**: the layered back-to-front draw already exists
  inside `PawnRenderer.Draw` (ground, equipment, torso, shield, head,
  treatment, trail, weapon, marks), keyed off `PawnLayout` rectangles. New
  layers slot into this order; new geometry goes on `PawnLayout` so it stays
  testable. The detail-tier switch (`Low`/`Medium`/`High` from apparent
  scale) is the built-in LOD hook — existing precedent for what survives at
  distance (shield: always; Itak off-hand: Medium+; head treatment: Medium+;
  swing trail: Medium+).
- **Grass/backdrop systems**: `PlainsDecalKind` plus `GenerateDecals` is the
  seam for richer ground scatter — the caps (`MaximumDecalCount`,
  `WorldAreaPerDecal`, `MaximumBackdropInterpolation`) are named constants a
  design would revise deliberately. Any motion (wind) would be a new
  frame-time-driven presentation system following the
  `AdvanceEffects` pattern, and must respect the interpolation ceiling so
  high-contrast stays clean.
- **A new visual setting** copies the GoreIntensity chain end to end: enum
  with pinned numeric values → nullable field on `RawClientSettings` with
  independent validation → manager with persist delegate → menu selector →
  tests.
- **Swing/pose growth**: `SwingPose` is a record struct whose `default` is
  neutral; new pose channels (e.g. facing for silhouette mirroring, noted as
  deliberately out of scope in `PawnGeometry.cs:376-382`) extend it without
  breaking callers.
- **Missing-asset diagnostics**: `assets` log channel with the
  `assets.sound.missing` precedent (section 10).

What is rigid:

- **The single-pixel-texture pipeline.** Introducing real sprites means new
  content pipeline entries in `Content.mgcb` (or a runtime loader like the
  audio path), a texture-loading and lifetime story, sampler-state decisions
  (the arena batch is `PointClamp`), and a fallback/diagnostic path — none of
  which exists. It also breaks the "one texture, one batch" performance
  profile with nothing measuring the regression.
- **Pose-blind culling** (`PawnRenderer.cs:27-47`): visual bounds must remain
  independent of animation phase.
- **The tested-geometry / untested-renderer split**: any formula placed
  directly in a renderer is untestable by convention and repeats the
  documented plains-backdrop bug.
- **Fixed faction pawn colors** and the fixed appearance palette: they are
  constants, not theme roles, by recorded design intent
  (`FactionColorPalette.cs:6-12`). Making them theme-aware is a design
  decision, not a refactor.
- **No persistent per-pawn presentation state**: appearance is recomputed
  every frame from immutable identity. A variant system that needs state
  (e.g. wear accumulating over a match) would be a new kind of thing and
  needs the fixed-capacity, clear-on-reset shape the effect systems use.
- **`ArenaGame` is banned from tests and split for file size** — new draw
  wiring lands in `ArenaGame.Rendering.cs` but all logic must live outside
  it.
