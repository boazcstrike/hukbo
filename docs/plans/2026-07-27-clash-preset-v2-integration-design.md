# Weapon clash on preset V2 — integration design

Date: 2026-07-27
Status: design, awaiting approval. **This document does not authorize implementation.**
Branch: `clash-integration`, based on `main` at `de19c57`.
Integrates: `worktree-weapon-clash` (57 commits, tip `3cd4bc6`) into `main`.

## 1. Why this document exists

The weapon-clash feature and the preset V2 weapon-identity feature were built
in parallel from a common ancestor and never saw each other. The true merge
base is `2d88b43`. The commit `7abf8fc`, titled "Merge main into the
weapon-clash worktree", is a merge commit that lives only on the clash branch;
it folded an intermediate snapshot of `main` into the clash history and is not
an ancestor of today's `main`. Anyone reasoning from `7abf8fc` as the base will
draw the wrong conclusions about `BattleEvent` in particular.

A direct `git merge` of the two branches produces eleven conflicted files. That
number badly understates the work. The merge is not a text reconciliation; it
is a design problem, because each branch changed the meaning of a shared
abstraction in a way the other branch's data depends on.

This document records the decisions. The task list lives in
`2026-07-27-clash-preset-v2-integration.md`.

## 2. What each branch did

### 2.1 Preset V2, on `main` (commit `32e4f1a`)

Weapons stopped sharing one damage, reach, and cooldown value. `WeaponProfile`
carries those three per weapon, split by grip, and
`CombatRuleset.ResolveWeaponProfile(WeaponId, ShieldId)` became the single read
path. Combat preset V1 was frozen and restated rather than edited: a new file
`PhilippineCombatPresetV2.cs` was added alongside it, registered under a new
`CombatPresetId.PrecolonialPhilippinesV2 = 2`, and `Scenario.CombatPreset` now
defaults to V2.

The roster grew from four loadouts to six. Kampilan and Wasay are two-handed.
Kalis and Itak each field a solo loadout and a shield-paired loadout.

`WeaponId` members were renamed — `GreatBlade` to `Kampilan`, `HeavyChopper` to
`Wasay`, `ThrustingBlade` to `Kalis`, `Bolo` to `Itak` — with the numeric values
1 through 4 unchanged, which is what made that rename hash-neutral.

`BattleEvent` gained the attacker's shield, and `Weapon`, `Shield`, and
`HitLocation` were packed into a single `int` so the event shrank from 80 bytes
to 72 and the collision allocation budget held at its existing 900,000-byte
ceiling.

### 2.2 Weapon clash, on `worktree-weapon-clash`

A defensive-resolution step was inserted into the accepted-attack path in
`GatherAndCommitAttacks`. Damage stopped being certain once an attack was
accepted. `ClashResolver.Resolve` derives a stateless keyed roll and walks a
five-way cumulative interval to pick one `AttackResolution`: `Landed`,
`ShieldBlocked`, `Parried`, `Deflected`, or `Evaded`.

`ClashProfile` carries the tuning: a sixteen-cell weapon-intercept matrix keyed
by `(defending weapon, attacking weapon)`, a flat shield-intercept scalar, and
three per-weapon rows — void channel, hard-share base, hard-share multiplier.
`CombatMetrics` accumulates per-tick counters that reach neither hash.
`BattleEvent` gained a `Resolution` field as a fourth plain nullable enum,
widening the event from 80 bytes to 88 and pushing the allocation ceiling to
1,100,000.

The clash branch bumped `PhilippineCombatPreset.Version` from 1 to 2 in place,
keeping the same `CombatPresetId.PrecolonialPhilippinesV1`.

## 3. The four structural collisions

### 3.1 The roster assumption that clash tables were built on

This is the load-bearing problem. `ClashProfile`'s own documentation states the
assumption in a single sentence:

> Keyed on the weapon rather than on the loadout pair because weapon and shield
> are correlated one to one in the shipped roster.

Under the V1 roster that was true: Kalis and Itak always carried a
`TallHardwood` shield, so a `WeaponId` key implied a shield state. Preset V2
makes it false. Kalis and Itak each now appear both with `ShieldId.None` and
with `ShieldId.TallHardwood`.

The consequence is not cosmetic. The shipped weapon-intercept values for Kalis
and Itak are deliberately low — between 0.03 and 0.06 — precisely because the
shield channel at 0.24 was doing the defensive work for those loadouts. A
shieldless Kalis under the merged code would resolve against a 0.05 weapon
intercept and a zero shield intercept, leaving it very close to undefendable.
The key type is insufficient, so no amount of retuning the existing cells fixes
it.

`ShieldInterceptBasisPoints` is the exception. It is already keyed on
`ShieldId` and already returns zero for `ShieldId.None`, so it behaves
correctly for a solo loadout with no change at all.

The hard-share tables are also fine as they stand. The research is explicit
that the hard-versus-soft split is driven by weapon identity — the mass and
leverage of the blow being turned and of the weapon turning it — not by whether
the defender also carries a shield. Kalis-solo and Kalis-paired can share one
hard-share row without contradiction.

### 3.2 Two incompatible answers to "which preset carries this feature"

`main` froze V1 at `Version = 1` and added V2 as a separate preset under a new
identity. The clash branch edited V1 in place and bumped it to `Version = 2`.

Git merges these without a conflict marker, because the edits touch different
files and different line ranges. The auto-merged result is incoherent: a preset
registered as `PrecolonialPhilippinesV1` whose internal version constant reads
`2`, sitting next to an unrelated `PrecolonialPhilippinesV2` that carries no
clash tables at all. Since V2 is the scenario default, the clash feature would
be silently inert in the only preset the game actually runs.

### 3.3 Two independent additions to `BattleEvent`

At the true base the event carried `Weapon` and `HitLocation` as two plain
nullable enum fields. `main` added `Shield` and packed all three into one `int`.
The clash branch added `Resolution` as a fourth plain nullable field and never
touched the packing, because the packing did not exist on its side.

The two allocation ceilings in the repository are therefore both measurements
of shapes that will not exist after the merge. `main` holds 900,000 by shrinking
the event to 72 bytes. The clash branch raised the ceiling to 1,100,000 after
measuring 988,192 bytes against an 88-byte event. Neither figure describes an
event carrying both `Shield` and `Resolution`.

### 3.4 A rename that breaks 41 files without a single conflict marker

The clash branch never edited `CombatIdentity.cs`, so `main`'s `WeaponId`
rename merges cleanly and the merged enum carries only the new names. Forty-one
files on the clash side still reference `WeaponId.GreatBlade`,
`HeavyChopper`, `ThrustingBlade`, and `Bolo` — including the thirty-six
references inside `BuildClashProfile` itself, seventeen files under
`tests/Hukbo.Client.Tests`, eleven under `tests/Hukbo.Core.Tests`, and both
harnesses under `tools/`.

The same shape of hazard appears in the test call sites for `BattleEvent.Attack`.
Git flags three conflicted test files; five more auto-merge cleanly and keep
`main`'s nine-argument call shape, then break the moment the constructor takes
both `Shield` and `Resolution`.

## 4. Decisions

### D1 — Preset V2 carries the clash tables. V1 stays frozen.

`PhilippineCombatPreset` returns to `Version = 1`, keeps
`CombatPresetId.PrecolonialPhilippinesV1`, and is constructed with no clash
profile. `PhilippineCombatPresetV2` carries the clash tables.

The clash branch's in-place version bump is reverted. Its stated reasoning — that
the preset's identity was not changing, only its version — was correct on a
branch where V2 did not exist, and is wrong now that it does.

*Rejected:* giving both presets clash tables. V1 exists to be a frozen
reference point whose content hash is pinned; changing what it resolves to
defeats the reason it was frozen.

### D2 — The clash profile folds into the content hash only when one was supplied.

`ComputeContentHash` folds the clash block conditionally, exactly mirroring the
precedent `main` already set for the weapon-attribute block: when the
constructor received no profile, nothing is folded — not even a zero count.

This is what keeps V1's pinned content hash at `0x59FB4CA563D87A49` across the
merge. That constant surviving untouched is the cheapest available proof that
the integration did not disturb the frozen preset, and the plan treats it as a
regression guard rather than a value to re-baseline.

The clash branch's fold is unconditional and its re-baselined
`0x4EAFE27A42DE87B2` is discarded along with the in-place version bump that
produced it.

Fold order within `ComputeContentHash`: the weapon-attribute block first, then
the clash block, both after the roster block. The order is arbitrary but must
be fixed, documented, and never reordered without a preset version bump.

### D3 — The defender key becomes `(WeaponId, ShieldId)`.

Two tables are re-keyed:

| Table | Old key | New key |
| --- | --- | --- |
| `_weaponIntercept` | `(WeaponId defender, WeaponId attacker)` | `(WeaponId defender, ShieldId defenderShield, WeaponId attacker)` |
| `_voidChannel` | `WeaponId` | `(WeaponId, ShieldId)` |
| `_hardShareBases` | `WeaponId` attacker | unchanged |
| `_hardShareMultipliers` | `WeaponId` defender | unchanged |
| `ShieldInterceptBasisPoints` | flat scalar on `ShieldId` | unchanged |

`ClashResolver.Resolve` already receives `defenderShield` as a parameter and
already folds it into the mix, so its signature does not change — only the
lookups inside it do. `ValidateClashProfileCoversTheRoster` already iterates
roster loadouts rather than the `WeaponId` enum, so it supplies both halves of
the new key without restructuring.

*Rejected:* keying on the full `CombatLoadout`. Armor plays no part in the
clash channels, so a loadout key would invite a future reader to believe it
does.

*Rejected:* two synthetic weapon identities for the solo variants. That would
put a presentation concern into the enum that the state hash depends on, and
`WeaponId` values are pinned.

#### D3.1 — The content-hash fold and the ordered accessors are part of the re-key

Added 2026-07-27, after review found the decision stopped one layer short of
where its data is consumed.

`CombatRuleset.FoldClashProfile` reads two accessors on `ClashProfile`, and both
are shaped by the old key:

- `OrderedWeaponIntercepts` yields `(key, value)` pairs whose key the fold
  reduces to `key.Defender` and `key.Attacker`. Under the new key the defender
  shield is never folded, so two profiles differing only in whether a cell
  describes a shielded or a bare defender would produce **the same content
  hash**. That is precisely the failure the fold exists to prevent: a save or a
  replay would accept a materially different configuration as the same one.
- `OrderedWeaponRows` is built by iterating `_voidChannel`, keyed on `WeaponId`,
  and joining the two hard-share tables onto each weapon. Once the void channel
  is keyed on `(WeaponId, ShieldId)` while the hard-share tables stay
  weapon-keyed, that join no longer describes one row per weapon and the
  accessor has no coherent shape.

The decision is therefore extended: the defender shield joins both the ordering
comparator and the folded bytes for the weapon-intercept table, and the void
channel separates from the hard-share tables into its own ordered accessor and
its own fold block rather than being carried as a column of a weapon row. The
hard-share tables keep their existing weapon-keyed accessor and fold unchanged,
which is consistent with section 3.1: the research drives the hard-versus-soft
split from weapon identity alone.

Fold order is extended to: roster, weapon attributes, weapon intercepts, shield
scalar, void channels, hard-share rows. As in D2, the order is arbitrary but
must be fixed, documented, and never reordered without a preset version bump.

### D4 — Table validation moves from the enum cross-product to the roster.

`ClashProfile.ValidateMatrix` currently demands exactly
`Enum.GetValues<WeaponId>().Length` squared cells. Under the new key that
demand is wrong in both directions: it would require cells for combinations
that are not legal loadouts, such as a two-handed Kampilan carrying a shield.

`ClashProfile`'s constructor therefore validates value ranges, clamp ordering,
and internal consistency only. Roster coverage stays where it already lives, in
`CombatRuleset.ValidateClashProfileCoversTheRoster`, which is the only place
that knows what the roster is. `ClashProfile.Neutral` becomes a profile that
resolves to zero for any key rather than a dense enum cross-product.

This makes the missing-cell failure a construction-time exception naming the
exact defender weapon, defender shield, and attacker weapon — which is the
behavior the existing validation already aimed at.

### D5 — `Resolution` packs into the fourth byte of `_combatContext`.

`main`'s packing scheme uses bits 0 through 23 of a signed `int`:
`WeaponShift = 16`, `ShieldShift = 8`, hit location in the low byte. Bits 24
through 31 are free. `AttackResolution` has five values numbered 0 through 4, so
it fits in three bits of that spare byte.

`ResolutionShift = 24` is added. The event stays at 72 bytes, the collision
allocation ceiling stays at 900,000, and the clash branch's raise to 1,100,000
is reverted rather than merged.

One interaction must be checked rather than assumed: `Landed = 0`, and
`CombatContextAbsent = 0` for the whole field. A landed attack contributes zero
to the resolution byte, which is safe only because the weapon field is non-zero
for every attack event, and "absent" is tested on the whole field rather than on
any one byte. A test pins that reasoning.

### D6 — The merged attack line carries the weapon pair label and the resolution.

`BattleEventFormatter.FormatAttack` takes weapon, shield, hit location, and
resolution. It resolves the label through `main`'s
`GetWeaponLabel(weapon, shield)`, which produces the pair form the historical
accuracy policy requires along with the grip suffix, then switches on
resolution the way the clash branch does, substituting that label into each of
the five branches.

The clash branch's bare English labels — "Great Blade", "Heavy Chopper" — are
discarded. They predate the pair-form requirement and would violate the policy
in section 7 of `CLAUDE.md` if shipped.

### D7 — `main`'s pawn silhouettes win; the clash swing pose applies on top.

`PawnLayout` keeps both `ShieldBounds` from `main` and `SwingTrail` from the
clash branch.

The per-weapon geometry constants are a genuine conflict rather than a merge:
both branches independently retuned the same weapon roles to different numbers,
and the clash branch's version has no shield silhouette and dropped the Wasay
axe-head block. `main`'s constants are kept, because they are the shipped,
gate-verified silhouettes and they encode the axe-versus-blade and
shielded-versus-solo distinctions the V2 roster needs. `ApplySwing` is then
re-applied on top of them.

This is the one decision most likely to look wrong on screen, and it is the one
the manual smoke checklist has to cover.

### D8 — Every acceptance measurement is retaken.

The clash branch met both acceptance criteria: a defence-attributable
non-landed share of 0.3414 at seed 1 with a 0.3137 to 0.3478 range across seeds
1 through 20, against a 0.25 to 0.45 band; and 20 of 20 seeds deciding before
the tick cap with a median decisive tick of 1,916 against a 5,000 ceiling.

Those numbers were measured against the four-loadout roster with a shield on
every Kalis and Itak. The roster is now six loadouts, two of them shieldless,
and ten new table cells exist. Every figure is retaken and re-recorded. The old
figures move to a superseded section rather than being deleted.

## 5. Historical accuracy

Every value in `ClashProfile` is already labelled **Provisional
reconstruction** in the type's own remarks, which state that all sixteen cells
of the weapon-intercept matrix have no evidentiary confidence and only their
relative ordering is argued, weakly. No `Documented` tuning value exists to
preserve, which materially lowers the risk of re-deriving the tables.

The ten new cells — four weapon-intercept cells each for shieldless Kalis and
shieldless Itak, plus two void-channel entries — have no per-pair figure in the
research at all. `WEAPON_CLASH_1500s.md` offers only a loadout-level band for a
one-handed blade with no shield: a weapon share of 0.40 to 0.55 of active
defence and a void share of 0.45 to 0.60, applied against a shieldless
mass-melee aggregate of 0.25 with a 0.18 to 0.32 spread. That yields a
weapon-intercept band of roughly 0.10 to 0.18 and a void band of roughly 0.11 to
0.19.

Each new cell is labelled **Provisional reconstruction** in the code comment
that introduces it, with the band it was drawn from named. None is presented as
a measurement. The direction is defensible and stated plainly: a shieldless
one-handed defender turns more with the weapon and evades more, because it has
nothing else to turn with.

The shield channel remains the only defensive channel with any sixteenth-century
documentary support, and even there only its direction is anchored — by
documented shield use at Mactan and by Cole's 1922 account of angled deflection,
which is **Documented, form uncertain**. Its magnitude of 0.24 is invented and
stays labelled as such.

## 6. Determinism

Both hashes move. That is expected and is the point of the change.

| Constant | Disposition |
| --- | --- |
| V1 preset content hash `0x59FB4CA563D87A49` | **Unchanged.** Regression guard for D2. If this moves, the conditional fold is wrong. |
| V2 preset content hash `0xE653F1802A447662` | Re-baselined. V2 now folds a clash profile. |
| Clash branch's `0x4EAFE27A42DE87B2` | Discarded with the in-place version bump. |
| Seed-1 event hash `CF8C3EDBC59C3319` | Re-baselined. Resolution enters the event hash. |
| Seed-1 state hash `C669281B67CF8871` | Re-baselined. Damage is now conditional on `Landed`. |
| Clash branch's pre-clash digest fixture | Kept. It pins the zero-interception control run and is not a re-baseline target. |

`ClashResolver` draws no value from the simulation's `SplitMix64` stream. It
computes a fresh FNV-1a mix per call under its own domain tag `HKBO_CLS`, in the
same manner `HitLocationResolver` uses `HKBO_HIT`. The two are independent
deterministic functions of the same input tuple, not sequential draws off a
shared generator, so no draw-order dependency exists between them and inserting
the clash call after the hit-location call cannot perturb any other stream.

`CombatMetrics` reaches neither hash, and the clash branch already carries the
before-and-after evidence pair proving it. That property is re-verified after
integration rather than assumed.

Preset V2's version constant is bumped, because its content changes.

## 7. Spectator discoverability

Section 10 of `SIMULATION-GAME-STANDARDS.md` asks whether a spectator can
discover the effect without reading source. For four of the five resolutions the
answer is yes through several independent channels:

| Channel | `Landed` | `ShieldBlocked` | `Parried` | `Deflected` | `Evaded` |
| --- | --- | --- | --- | --- | --- |
| Event log line | damage line | "stopped by the shield" | "parried" | "turned aside" | "stepped off the line" |
| Blood spray | yes | suppressed | suppressed | suppressed | suppressed |
| Impact ring | yes | absent | absent | absent | absent |
| Clash cross | absent | yes | yes | yes | absent |
| Swing pose | stops on target | recoil | recoil | recoil | follows through |

`Evaded` is the weak case: it is distinguished by one positive channel, the
event-log line, and three absences. This is recorded honestly rather than
claimed as covered.

Sound is silent for every clash outcome. The three clash sound slots were
**deferred by owner decision on 2026-07-27** and are explicitly out of scope
here. The `SoundLogPanel` expected-files section has no spare pixels at its
200-pixel cap with the current nine slots, and a tenth would clip off-panel —
the exact failure
`SoundLogPanelTests.CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize`
exists to catch.

The swing pose is marked provisional in the clash branch's own design, which
doubts whether the arithmetic is visible at shipped zoom — the motion may be
sub-pixel at the Medium detail tier. Under D7 it is now applied to `main`'s
different silhouette constants, so that doubt carries forward and is a manual
smoke row, not an automated assertion.

## 8. Risks

1. **D7 produces a silhouette that reads badly in motion.** The swing pose was
   tuned against geometry that no longer exists. Only a human at an interactive
   desktop can settle this. Mitigation: a dedicated smoke row, and the swing
   pose is the one piece of this integration that can be reverted on its own
   without touching the simulation.
2. **The ten new cells push the defence share outside the 0.25 to 0.45 band.**
   Two shieldless loadouts entering the roster changes the mix regardless of
   the cell values. Mitigation: the 20-seed sweep is a gate task, not a
   reporting task, and the bands are the acceptance criteria.
3. **A silent compile break is missed.** Forty-one files reference renamed
   symbols and five test files auto-merge into a stale call shape. Mitigation:
   the build is the detector, and the plan sequences the rename sweep before
   anything that depends on it compiling.
4. **The packed resolution byte interacts with the absent marker.** Covered by
   D5 and pinned by a test.

## 9. Out of scope

- The three clash sound slots. Deferred by owner decision; see section 7.
- Any campaign, economy, or map-generation state. Gate 3 has not passed.
- Retuning `main`'s weapon attributes or V2's damage, reach, and cooldown
  values. This integration changes what happens after an attack is accepted,
  not what an attack is.
- Adding clash tables to preset V1. See D1.
