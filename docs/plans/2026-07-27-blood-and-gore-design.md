# Blood and Gore — Design

Date: 2026-07-27
Status: Design. This document does not authorize implementation. The ordered
task list lives in `docs/plans/2026-07-27-blood-and-gore.md`.

## 1. Purpose

Hukbo currently renders a blow as an expanding warm-white ring with a handful of
radial shards. It reads as an abstract impact marker. It does not tell a
spectator which direction a blow came from, which weapon delivered it, or where
on the field the line actually broke.

This feature adds blood as a presentation layer on top of the existing hit
effect: a directional spray at the moment of impact, and ground marks that
persist after the warriors have moved on. The goal is legibility first and
spectacle second. A spectator watching a 200-agent engagement should be able to
glance at the arena and see where the fighting was heaviest without reading the
event log.

The feature is entirely client-side. `Hukbo.Core` does not change, and neither
the state hash nor the event hash moves.

## 2. What the simulation already tells the client

The client receives one ordered batch of `BattleEvent` values per tick, together
with the full `AgentView` list. Within a tick the simulation emits events in a
fixed order: `Move`, then `Attack`, then `Damage`, then `Death`, then at most one
`Outcome` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:138`).

The important observation is that the existing hit effect is built from `Damage`
events (`src/Hukbo.Client/Presentation/HitEffectSystem.cs:50`), and `Damage`
events are constructed through `BattleEvent.NonAttack`, which forces both
`Weapon` and `HitLocation` to `null`
(`src/Hukbo.Core/Simulation/BattleEvent.cs:134`). A `Damage` event also sets both
its source and its target to the victim
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:385`), so it carries no attacker
identity at all.

Everything blood needs is on the `Attack` event instead:

| Blood needs | Where it comes from |
| --- | --- |
| Attacker identity | `BattleEvent.SourceEntityId` on the `Attack` event |
| Victim identity | `BattleEvent.TargetEntityId` on the `Attack` event |
| Weapon class | `BattleEvent.Weapon` (`src/Hukbo.Core/Simulation/BattleEvent.cs:69`) |
| Body part struck | `BattleEvent.HitLocation` (`src/Hukbo.Core/Simulation/BattleEvent.cs:75`) |
| Severity of that single blow | `BattleEvent.Value` |
| Whether the victim died this tick | A `Death` event for the victim in the same batch |

Spray direction is derived from the two agents' positions rather than from any
facing field, because no facing, heading, or velocity field exists anywhere in
`Hukbo.Core`. That derivation is exact rather than approximate: movement commits
in tick stage 7 (`GatherAndCommitMovement`) strictly before attacks resolve in
stage 8 (`GatherAndCommitAttacks`), and nothing in the attack stage moves an
agent. The end-of-tick position in `AgentView` is therefore the position at the
instant the blow landed.

Agents are never removed from the agent array; death only flips `IsAlive`. An
attacker who dies in the same tick as their own blow is still resolvable by
entity ID.

## 3. Design decisions

These were open questions raised during research. They are settled here so that
the plan document does not have to relitigate them.

### 3.1 Blood is a separate system, not a change to `HitEffectSystem`

`HitEffectSystem` stays exactly as it is, keyed on `Damage`. A new
`BloodEffectSystem` is added alongside it, keyed on `Attack`. Both are owned by
`PresentationCoordinator` and both are ingested from the same tick batch.

The alternative — migrating `HitEffectSystem` to `Attack` keying so that
granularity matches — would rewrite behavior that is already covered by tests in
`tests/Hukbo.Client.Tests/HitEffectSystemTests.cs` and would change how existing
impacts look, for no benefit to this feature. The visible consequence of keeping
them separate is that a victim struck by two attackers in one tick shows one ring
and two sprays. That is acceptable and arguably more informative.

### 3.2 Every blow on a dying victim renders at lethal tier

When two attackers kill the same victim in the same tick, the simulation emits
two `Attack` events, one `Damage` event, and one `Death` event. There is no
authoritative killing blow — the standards document states plainly that
simultaneous lethal attacks resolve together and mutual kills are possible.

Rather than inventing an attribution rule, every `Attack` on a victim who dies in
that tick renders at the lethal tier. This is honest about what the simulation
actually says. The rejected alternative, "highest sequence wins", would
systematically award every contested kill to the higher entity ID, because attack
events are emitted in ascending agent-array index and agents are ordered by
entity ID. That bias would be visible on screen and has no basis in the rules.

### 3.3 Severity is clamped

`HitPoints` floors at zero when damage is applied, so a final blow can exceed the
victim's remaining hit points by a wide margin. Any severity ratio used to scale
spray volume is clamped to the range zero through one, so an overkill blow does
not produce an outsized spray.

### 3.4 Blood color is a fixed constant, not a theme role

`FactionColorPalette` carries an explicit doc comment explaining why pawn colors
bypass the theme system: silhouettes are painted directly onto the arena canvas
rather than through a themed panel surface
(`src/Hukbo.Client/UI/FactionColorPalette.cs:6`). `HitEffectRenderer` follows the
same precedent with its private `WarmWhite` and `WarmShard` constants
(`src/Hukbo.Client/Rendering/HitEffectRenderer.cs:10`).

Blood follows that established convention. Adding a twenty-eighth theme role
would require touching the `UiThemeColors` record, the catalog reader, the
fallback constructor, the `requiredColorRoles` array and all five theme blocks in
`Content/Themes/ui-theme-standards.json`, plus the catalog tests — a large,
unrelated diff for a canvas-layer color.

The colors chosen must remain distinguishable from both faction colors and from
the arena surface. That is a review criterion, checked by eye against all five
themes during the smoke checklist, not an automated contrast assertion.

### 3.5 Detail degrades with camera zoom, but blood stays visible at the default view

The existing detail thresholds are `LowDetailScale = 0.95` in
`HitEffectGeometry` and `MediumDetailScale = 0.95` / `HighDetailScale = 1.80` in
`PawnGeometry`. At a 1280×720 window the initial `SpectatorCamera.Fit` produces
an apparent scale near 0.79, which sits below every one of those thresholds.

If blood used the same ladder, a spectator on first launch would see no blood at
all until they zoomed in, and the feature would fail the standards requirement
that an effect be discoverable without reading source. Blood therefore uses its
own lower threshold so that the default fit view still shows spray and ground
marks, with droplet counts reduced rather than zeroed.

### 3.6 Ground marks are bounded twice

Ground marks fade on a fixed lifetime *and* live in a fixed-capacity buffer whose
overflow policy overwrites the oldest entry, matching `HitEffectSystem.Add`
(`src/Hukbo.Client/Presentation/HitEffectSystem.cs:125`). Either bound alone
would be sufficient in the common case; both together mean neither a long
stalemate nor a sudden mass casualty event can saturate the arena.

### 3.7 A gore intensity setting is required, and defaults to Stylized

This is a game about real warfare among real people, built on contested
colonial-era sources. `CLAUDE.md` §7 governs. Three levels:

- **Off** — no blood is drawn and no blood state is allocated or advanced. The
  ability to decline is first-class and reachable from the in-game menu, not
  buried in a config file.
- **Stylized** (default) — directional impact spray and fading ground marks.
- **Full** — adds a sustained spurt on lethal blows and longer-lived, denser
  ground marks.

The default is Stylized rather than Off because a feature that is off by default
is not discoverable, and rather than Full because a sustained spurt carries an
anatomical reading that the evidence does not support.

### 3.8 No dismemberment, ever

`BodyPart` is documented as metadata that does not change damage, health, cooldown,
future actions, or death (`src/Hukbo.Core/Combat/BodyPart.cs:8`), and the
research document is explicit that the combat preset's numeric weights "carry no
equivalent evidentiary confidence at all" and must never be presented to a
spectator as a historical measurement
(`docs/research/HISTORICAL_1500s_WEAPONS.md:261`, `:308`).

Rendering a severed limb for `BodyPart.WeaponArm` would present a hand-authored
gameplay weight as an anatomical and, in this setting, an ethnographic claim. It
would also lead a spectator to correctly infer a wound system that does not
exist. Body part may influence where on the victim's silhouette a spray
originates and how large it is; it may never produce mutilation.

### 3.9 Blood never touches the simulation clock

Blood advances on unscaled presentation seconds, exactly as `HitEffectSystem`
does. Hit stop, freeze frames, time dilation, knockback, and screen shake are all
out of scope. Screen shake is specifically excluded because it would displace the
camera transform that `SelectAtPointer` inverts to pick agents, which would make
selection jitter under a stationary pointer.

## 4. Architecture

Three new files in `Hukbo.Client`, following the established triad of stateful
system, pure geometry helper, and dumb renderer:

| File | Role |
| --- | --- |
| `Presentation/BloodEffect.cs` | Value types: one burst record and one ground mark record |
| `Presentation/BloodEffectSystem.cs` | Fixed-capacity buffers, `Ingest`, `Advance`, `Clear` |
| `Rendering/BloodGeometry.cs` | Pure layout computation from a burst plus camera zoom |
| `Rendering/BloodRenderer.cs` | Quad submission only, no logic |

Plus a gore intensity enum and its menu control, and the settings plumbing to
persist it.

`PresentationCoordinator` gains a `Blood` property, ingests into it from
`IngestTick`, advances it from `AdvanceEffects`, and clears it from `ResetFor`
alongside the existing hit effects. `ArenaGame.Rendering` draws blood in the
arena-scissored batch. Ground marks draw beneath pawns; bursts draw above pawns
and alongside the existing hit effect.

### Determinism of appearance

All visual variation derives from a pure hash of authoritative identifiers, the
same way `HitEffectGeometry.CreateSeed` mixes sequence and target entity ID
(`src/Hukbo.Client/Rendering/HitEffectGeometry.cs:117`) and
`PawnAppearanceFactory` mixes entity ID. Blood extends that to include the
attacker:

```
burstSeed   = Mix(sequence + K1) ^ Mix(targetEntityId + K2) ^ Mix(sourceEntityId + K3)
dropletSeed = Mix(burstSeed + index * K4)
```

`Sequence` is unique across a whole match, so this is collision-free per blow and
identical on replay. `System.Random` is banned repo-wide; the wall clock, frame
counters, and `Hukbo.Core`'s RNG are all off limits to presentation code.

Two playbacks at different frame rates will land droplets a fraction of a pixel
apart, because effects advance on variable wall time. That is accepted. What must
be reproducible is the derived set: how many bursts, from which blow, with which
droplet count, direction, tier, and seed. Those are what the tests assert, with a
fixed elapsed step.

### Budget

Every quad is one `spriteBatch.Draw` of the shared one-pixel texture. For
reference, an ordinary hit effect is about twenty-four quads and a lethal one
about fifty-two, and the existing hit effect capacity of 256 already implies a
ceiling near thirteen thousand quads.

Blood is budgeted well below that:

| Buffer | Capacity | Quads each | Worst case |
| --- | ---: | ---: | ---: |
| Droplets | 2048 | 1 | 2048 |
| Ground marks | 384 | up to 3 | 1152 |
| Lethal spurts (Full only) | 32 | up to 12 | 384 |

Roughly seventy-five kilobytes resident, allocated once in the constructor and
never grown. Ingest, advance, and draw allocate nothing: fixed arrays, in-place
compaction with a read index and a write index, `ReadOnlySpan` exposure, and no
LINQ or closures anywhere on those paths.

Bursts are culled against `arenaBounds` before submission, matching `DrawPawns`.
The existing `HitEffectRenderer` does not cull and relies on the scissor
rectangle, which discards work only after the quad has been submitted; blood does
not copy that.

## 5. Acceptance criteria

Each criterion is stated so that it can be confirmed either by a pure unit test
or by a spectator watching the running game.

1. Every accepted attack produces exactly one blood burst at the victim. The
   number of bursts created in a tick equals the number of `Attack` events in
   that tick.
2. A burst's spray direction points away from the attacker, along the vector from
   the attacker's position to the victim's position in the same snapshot.
3. Spray shape differs visibly and consistently across the four weapon classes.
4. A blow landing on a victim who dies in that same tick renders at a distinct
   lethal tier.
5. When two or more attackers strike the same victim in one tick, every blow
   renders; none is dropped and no attacker is designated the killer.
6. Concurrent droplets and ground marks never exceed their configured capacities.
   Overflow overwrites the oldest entry rather than growing a buffer.
7. Blood advances on unscaled presentation time and never gates, pauses,
   reorders, or delays simulation advancement. Ticks per wall-clock second at
   1×, 2×, and 4× are identical with gore at Full and at Off.
8. The seed-1 200-agent 10,000-tick state hash and event hash are unchanged, and
   the 500-agent stress run still terminates on the same tick.
9. No `System.Random`, no wall-clock read, and no `Hukbo.Core` RNG call anywhere
   in the blood path. All variation derives from a pure function of sequence,
   source entity ID, target entity ID, and index.
10. A gore intensity setting with values Off, Stylized, and Full is reachable
    from the in-game menu, persists across restarts, and defaults to Stylized
    when the stored settings file predates the setting.
11. At Off, no blood quad is submitted and no droplet or ground mark slot is
    occupied.
12. Blood adds no heap allocation per tick or per frame.
13. No client test constructs `ArenaGame`, a `GraphicsDevice`, or a `SpriteBatch`.
14. Lethal blows leave a ground mark at the victim's position that outlives the
    burst and fades rather than popping.
15. Blood detail degrades with camera zoom, and at the default fit view blood is
    still visible.
16. Blood clears on Next Round and on Full Reset, along with the other
    disposable presentation state.

## 6. Explicitly out of scope

- Dismemberment, severed limbs, or any per-body-part mutilation silhouette.
- Wound marks accumulating on a pawn. Dead agents are not drawn at all
  (`src/Hukbo.Client/ArenaGame.Rendering.cs:185`), so there is no corpse to mark,
  and it would require a body-part-to-geometry mapping that §3.8 forbids.
- Screen-edge or lens splatter. This is a windowed top-down spectator view with
  fixed UI panels, not a first-person camera.
- Screen shake, hit stop, freeze frames, and knockback.
- Blood that spreads, merges, or flows over time. That is a simulation, and it
  would drift toward being read as a wound model.
- Audio of any kind.
- A rendering benchmark. The standards document names a `render-500` workload,
  but `scripts/benchmark.ps1` is headless and cannot measure frame time. The
  quad budget in §4 is a stated hypothesis, not a measurement, and the plan says
  so rather than claiming otherwise.

## 7. Risks

| Risk | Mitigation |
| --- | --- |
| Blood is invisible at the default zoom, so nobody discovers the feature | §3.5 gives blood its own lower detail threshold; smoke checklist verifies at default fit |
| Red blood is unreadable against the red faction, worst in the high-contrast theme | Colour choice is reviewed by eye against all five themes before the feature is called done |
| Frame time regresses at 500 agents | Capacities in §4 are hard caps; there is no rendering benchmark, so the honest statement is that this is unmeasured and the caps are conservative |
| A settings schema bump discards the user's saved theme | The plan must preserve `SelectedThemeId` when reading a settings file written before this feature |
| Scope creep toward a wound model | §3.8 and §6 are binding; body part may shift spray origin and size only |
