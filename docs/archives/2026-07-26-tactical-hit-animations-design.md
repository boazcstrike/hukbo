# Tactical Hit Animations Design

## Goal

Add short, readable hit feedback to Hukbo's spectator arena without changing
combat timing, authoritative state, deterministic hashes, or battle outcomes.

The approved direction is tactical impact: ordinary damage produces a restrained
target pulse and a small procedural impact burst, while lethal damage produces a
larger and geometrically distinct burst. This first pass intentionally excludes
camera shake, hit-stop, knockback, damage numbers, audio, and weapon-specific
effects.

## Architecture

Add a Client-only hit-effect system owned by the presentation coordinator. It
ingests each completed simulation tick's events together with that tick's agent
views, creates one effect bundle for each aggregated `Damage` event, advances
effect lifetimes from presentation time, and clears all active effects on round
or full reset.

The system uses a fixed-capacity pool of value-type effect records. Each record
captures the event sequence, target entity ID, target world position, damage
value, lethal state, and presentation age. Shard orientation and count are pure
functions of stable event data rather than simulation or framework randomness.

Rendering remains procedural and uses the existing white pixel texture. A
dedicated hit-effect renderer converts captured world positions through the
spectator camera, applies the current zoom policy, and draws rings and shards
inside the arena clip. Pawn drawing receives a short-lived hit-pulse strength
for living targets; lethal bursts remain visible at the captured position after
the dead pawn stops drawing.

No Core types, battle-event fields, simulation systems, state hashing, content
assets, or authoritative random-number streams change.

## Event and update flow

`BattleSimulation` already emits one aggregated `Damage` event per damaged
target per tick, followed by a `Death` event when that damage is lethal. After
each `AdvanceOneTick`, the client passes the complete event batch and current
agent views to both the event feed and hit-effect system.

The hit-effect system first identifies deaths in the batch, then processes
damage events. It captures the damaged target's position even when the target is
already dead in the post-tick snapshot. This preserves lethal feedback while
avoiding duplicate effects from the one-per-attacker `Attack` events.

Ingestion occurs inside the simulation-advance loop so playback frames that
process multiple ticks retain every hit. Presentation lifetimes advance once
per client update using unscaled elapsed time, keeping effects readable at
different playback speeds without influencing simulation progression.

## Visual language

An ordinary hit lasts approximately 180 milliseconds and contains:

- a restrained warm-white pawn pulse during the opening portion;
- one thin expanding ring centered on the captured impact position; and
- four to six short, deterministic radial shards that move outward and fade.

A lethal hit lasts approximately 280 milliseconds and contains:

- a larger double-ring burst;
- eight longer shards with a wider travel distance; and
- a distinct high-contrast geometry treatment that remains readable without
  relying on color.

Effects scale with camera zoom but clamp to a readable screen-space range.
Secondary shards may be suppressed at the lowest detail tier while the primary
ring remains. The arena clip prevents effects from drawing into surrounding UI.

The pulse affects only the rendered pawn color and always returns to the normal
appearance. It is local, brief, and backed by shape-based feedback; there is no
full-screen flash, camera motion, zoom punch, or time freeze.

## Capacity and lifecycle

The effect pool has an explicit fixed maximum. When saturated, it replaces the
oldest active effect rather than allocating or growing without bound. Shards are
derived during geometry calculation from the parent effect seed, so ingestion
does not allocate per-particle objects.

Expired effects are compacted or recycled. Reset clears the entire pool, and
terminal match processing does not create or retain effects beyond their normal
lifetime. Every pulse, ring, and shard is derived from effect age, so no mutated
pawn, camera, or renderer state can linger.

## Testing

GPU-independent Client tests cover:

- filtering so only `Damage` events create effect bundles;
- one effect per aggregated damaged target per tick;
- lethal classification from a same-batch `Death` event;
- target-position capture for lethal hits;
- ingestion of consecutive event batches before a render frame;
- deterministic shard count and orientation from stable event data;
- ordinary and lethal lifetime expiry;
- fixed-capacity replacement behavior;
- living-target pulse lookup and return to zero after expiry; and
- complete clearing on next-round and full reset.

Integration verification includes focused Client tests, the repository
verification script, and a manual Windows smoke pass at 1x and 4x playback. The
smoke pass checks fitted, minimum, and maximum zoom; crowded exchanges; lethal
hits; pause/resume; round reset; full reset; resize; and containment inside the
arena.

## Risks and mitigations

- Dense exchanges can become noisy. Use aggregated `Damage` events, restrained
  counts, zoom-based detail suppression, and a fixed pool.
- Multi-tick playback frames can drop feedback. Ingest effects inside the
  simulation tick loop rather than reading only the latest frame state.
- Dead pawns disappear before rendering. Capture their post-tick world position
  when ingesting the lethal damage batch.
- Visual variation can undermine reproducibility. Derive it only from stable
  event data and keep it outside Core.
- Hit pulses can leave pawn colors altered. Compute color from current effect
  age during drawing; do not mutate appearance descriptors.
- The repository contains an in-progress Hukbo rename and spectator UI work.
  Touch only the immediate Client presentation, rendering, integration, test,
  and plan files, and never stage unrelated changes.

## Success criteria

- Every aggregated `Damage` event produces exactly one correctly positioned
  tactical effect bundle.
- Ordinary and lethal hits are visually distinct at normal spectator zoom.
- Hits from every simulation tick remain visible during accelerated playback.
- Same-seed replays produce the same effect geometry and ordering.
- Core simulation hashes and outcomes are unchanged.
- Active effects remain bounded, expire completely, and clear on reset.
- Focused and repository-level verification passes with no unrelated diff.
