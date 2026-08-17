# Shield size against projectile size — backlog after the merge

Date: 2026-08-18
Status: open. The shield-projectile-block package is implemented, verified, and
merged into `main`; this records what it left behind.

Read `2026-08-15-shield-projectile-block-design.md` first — it is authoritative
over this document, and where the two disagree the design wins and the
discrepancy is worth reporting. Its section 6.1 carries a correction made during
implementation and is the one section most worth reading before touching
movement.

## What shipped

- `ShieldId.NarrowBreastHigh = 3`, a breast-high board beside the body-length
  `TallHardwood`.
- Size-aware interception on `ClashProfile`:
  `base × span / (span + bulk)`, integer throughout, applied to melee and to
  projectile arrival alike.
- `WeaponProfile.ShieldDefeatBulkRaw` — Busog 2, Bangkaw 6, Arquebus 30 world
  units, melee 0.
- Combat preset `PrecolonialPhilippinesV7 = 7`, rebuilt on V5's eleven-row
  roster, **shipped by the client**.
- Movement preset `ShieldEncumbranceV16 = 16`, carrying shield encumbrance
  (speed scale at agent creation) and the block-recovery window (5 ticks tall,
  3 narrow, pace clamped to 4,000 basis points). **Registered and selectable,
  but not the client default** — see section 1.
- `AgentState.ShieldBlockRecoveryTicksRemaining`, hashed behind its own gate and
  surfaced on `AgentView`.
- Client: narrow shield draws narrower and shorter, its own catalog skin,
  inspector label and live `Block:  recovering Nt` line, event-log suffix naming
  the shield size, and both narrow-shield rows in the default army composition.

Five recorded seed-1 baselines are unmoved — 6/4, 5/8, 5/10, 5/11, 5/13 — and
`./scripts/verify.ps1` exits 0 for both games.

---

## 1. The shipped movement default does not carry the shield's movement half

**This is the largest open item and it needs a decision, not an
implementation.**

The in-fight evasion package landed `MovementPresetId.EvasiveFootworkV14` as the
client's default movement preset before this package merged. The two presets are
mutually exclusive: both restate `CohortLateralSpreadV13`, and a battle selects
exactly one. Claiming the default would have silently removed in-fight evasion
from the only build a spectator ever runs, so this package left main's default
alone and shipped `ShieldEncumbranceV16` as a selectable option instead.

What that costs is precisely the movement half of the feature:

| Effect | Lives in | Shipped by default? |
| --- | --- | --- |
| Three shield sizes | Combat preset V7 | **Yes** |
| Size-aware interception | Combat preset V7 | **Yes** |
| Projectile bulk | Combat preset V7 | **Yes** |
| Shield encumbrance (bigger shield is slower) | Movement preset V16 | No |
| Block-recovery window (blocking checks the pace) | Movement preset V16 | No |

So the blocking behaviour the package exists for is live by default; the two
movement consequences are reachable only by choosing V16 in the Army Composition
panel.

Three ways out, in increasing order of cost:

1. **Flip the default to V16.** One line in `ClientSettingsStore`
   (`DefaultMovementPreset`). Drops in-fight evasion from the shipped build.
   Cheap, and a straight trade of one feature for another.
2. **Compose the two into one preset.** A V17 restating V13 plus the evasive
   gate plus the shield gates. The evasive behaviour is gated on preset
   *identity* at roughly eight call sites in `BattleSimulation`, plus the
   `foldsEvasiveAction` hash gate, so V17 has to be admitted to each of them.
   This changes another package's feature surface and its digests, and needs
   that package's owner to agree.
3. **Leave it.** The shield's movement half stays an opt-in preset. Honest, and
   the smoke rows record it, but `SPB-5` and `SPB-7` then test something a
   default player never sees.

Nothing here is authorized yet. Option 2 is the only one that keeps both
features, and it is the only one that needs a design document.

## 2. The block line does not name the shield that blocked

`BattleEventFormatter` still renders `stopped by the shield` for a
`ShieldBlocked` resolution, without saying which shield stopped it.

This was deliberate, not an oversight. `BattleEvent`'s packed `Shield` field is
**the attacker's** shield, not the defender's (`BattleEvent.cs`, the `Shield`
property's own doc comment says so), so naming the blocking shield needs a new
event field. Adding one moves the event hash for every preset that emits it,
which is a new-preset event under `CLAUDE.md` section 5 and was out of scope for
this package.

What did ship instead: the one-handed grip suffix now names the size — `(solo)`,
`(tall shield)`, `(narrow shield)` — which is the attacker's shield and was
already carried. That closes `SPB-8` but not this item.

If a reader needs to know which shield stopped a blow, that is a small design
with a real cost: one packed field, a new combat preset, and new golden
expectations.

## 3. The inspector shows no shield span

Design section 8 asks for a span line in the agent inspector alongside the
shield label and its evidence tier. It was not built.

The obstacle is plumbing rather than difficulty: span lives on the combat
ruleset's per-shield table, and `AgentInspectorContent` is handed an `AgentView`
and a `CombatLoadout`, not a ruleset. Threading a ruleset — or projecting span
onto `AgentView` — is the work, and it is worth doing deliberately rather than
by widening a signature in passing.

What shipped in its place is the live `Block:  recovering Nt` line, which was the
part a spectator cannot infer from the silhouette. Span is inferable: the two
shields draw at visibly different widths.

## 4. Nine smoke rows are unrun

`SPB-1` through `SPB-9` in `docs/development/smoke-checklist.md` are all
`PENDING`. Only a person at an interactive Windows desktop may close one; no
agent and no automated run may.

Two of them, `SPB-5` (pace ordering solo > narrow > tall) and `SPB-7` (the pace
check after a block), **cannot be run against the shipped default at all** while
section 1 stands. They need the Army Composition panel set to
`V16 Shield Encumbrance` and a full reset first. That instruction is not in the
row text; whoever runs them should add it or read this section.

`SPB-9` asks that the preset be "the one selected by default on a fresh settings
file". **That row is now wrong** — it was written before the numbering collision
with main. It should read that `V16 Shield Encumbrance` is present in the
selector, not that it is the default. Fix the row before running it, or it will
fail for the wrong reason.

## 5. Unreferenced movement rows are parked in the tree

`Profiles/NarrowBreastHighMovementProfiles.KalisRow` and `ItakRow`, and
`TallHardwoodMovementProfiles.KalisRowV14` and `ItakRowV14`, are written,
commented, and covered by tests — and referenced by no registered preset.

They are the wreckage of the corrected design: the first draft expressed shield
pace as `LoadoutMovementProfile` rows and turned on equipment-relative footwork,
which crashed on the first ranged warrior because `CanonicalLoadoutIndex` maps no
key for one. The rows were kept rather than deleted because the canonical index
entries 6 and 7 and the six-or-eight-row validation were kept with them, and the
first equipment-relative preset that fields a narrow shield will want all of it.

Nothing is broken by their presence. If a future audit asks why four movement
rows exist that nothing registers, this is the answer. If the answer stops being
satisfying, deleting them and the two canonical indices together is a clean
removal — but do not delete the rows and leave the indices, which would let
`ResolveLoadoutProfile` return a row that does not exist.

## 6. Tuning values are provisional and unplayed

Every number this package authored is provisional gameplay tuning under
`CLAUDE.md` section 7, marked as such in code, and **none of it has been watched
by a person in a real battle**. Specifically:

- The per-shield interception bases, 2,400 tall and 1,700 narrow.
- The spans, 12 and 6 world units, and the bulks, 2 / 6 / 30.
- The pace scales, 9,000 tall and 9,600 narrow — a 4% gap between the two
  shields, which may well be too small to see, and `SPB-5` is the row that will
  say so.
- The recovery durations, 5 and 3 ticks, and the 4,000 basis-point clamp.

The interception table has a property worth protecting when retuning: the
proportional loss from bulk must stay larger for the narrow shield than the
broad one. That is what makes "a small shield struggles against a large
projectile" true rather than decorative, and it is pinned by a test in
`ShieldSizeInterceptionTests`. A retune that trips that test has broken the
feature's premise, not the test.

## 7. Two corrections worth not repeating

Recorded here because both cost real time and neither is obvious from the code.

- **Combat presets V5 and V6 are parallel branches off V4, not a chain.** V6
  carries no ranged weapon and no shield. V7 was first built on V6 and fielded
  neither, which would have made the whole feature unobservable. Print a
  preset's roster before basing a new one on it.
- **The shipped movement preset is not equipment-relative.** V13 registers
  `usesEquipmentRelativeFootwork: false` with zero loadout rows. Turning that
  flag on is not a small change; it routes every warrior through
  `ResolveLoadoutProfile`, which throws for every ranged loadout. Prove any
  movement-preset change through the headless runner
  (`--preset N --movement-preset M`) before trusting a green unit suite — the
  suites did not catch this.
