# Combat Preset V3 — Attack Combinations — Plan

Date: 2026-07-27

Status: plan. This document authorizes implementation of the tasks listed in
section 6, in the dependency order stated there. It implements
`docs/plans/2026-07-27-combat-preset-v3-combos-design.md`, which stays as-is
and is not edited by this plan.

## 1. Scope recap

V3 is V2 minus the two paired loadouts, plus attack combinations. The four
solo loadouts already fielded by V2 — Kampilan, Wasay, solo Kalis, solo Itak —
are the entire V3 roster. Nothing about V1 or V2 is retuned, no weapon is
removed, and shields are not touched; the paired loadouts and every shield
mechanic V2 built simply are not fielded by V3's roster. `CombatPresetId`
gains a third value, `PrecolonialPhilippinesV3 = 3`, registered alongside V1
and V2, not instead of them. The new mechanic — an opening roll on a landed
blow, a continuation roll on each following blow, a maximum chain length
driven by a placeholder fighter level, and a faster cooldown while a chain is
active — is described in full in the design doc sections 3 and 4 and is not
re-argued here.

## 2. Resolution of the three open questions

**Question 1 — is a chain bound to one target?** Yes, strictly. The moment
the attacker's `TargetEntityId` for the current tick differs from the target
identity the chain opened against, the chain is over, even if the old target
is still alive and in range. Justification: `SelectTargetsAndIntents`
recomputes every agent's target from scratch, every tick, from raw nearest-
enemy distance — retargeting is not a rare edge case gated on losing sight of
an enemy, it is the ordinary outcome of any tick where a different enemy
becomes nearest, and a chain that survived a target switch would report as
one continuous flurry landing on two different victims, which contradicts the
single-attacker-single-target invariant the design assumes throughout.

**Question 2 — does the opening roll require a landed blow, or does an
attempted attack suffice?** Landed only. Justification: `AttackResolution`
already distinguishes a blow that connects from one that is
`ShieldBlocked`/`Parried`/`Deflected`/`Evaded`, and only `Landed` feeds
damage. Opening — or continuing — a chain on a blow the defender successfully
stopped would mean a defender's successful defense rewards the attacker with
a faster follow-up, which inverts the defensive-resolution contract in
`SIMULATION-GAME-STANDARDS.md` section 14. Gating on `Landed` also uses a
distinction the codebase already computes, rather than inventing a new one.

**Question 3 — can the combo cadence let a chaining attacker outpace
movement/collision?** No, and this needs no runtime guard, only the
observation recorded here. `GatherMovementProposals` only builds a movement
proposal for an agent whose `Intent` is `Moving` or `Regrouping`; an agent
whose `Intent` is `Attacking` gets no movement proposal and is already
stationary, and `Intent` is recomputed from scratch every tick by
`SelectTargetsAndIntents`, which runs before movement and is not influenced by
`AttackCooldownRemaining` or `ComboStepsRemaining`. A faster cooldown changes
how soon `GatherAndCommitAttacks` lets the next attack through; it changes
nothing that `GatherMovementProposals`, `ResolveCollisions`, or
`CommitMovement` read. No implementation task in section 6 needs to touch
movement or collision code because of this question.

**A fourth judgment call the design doc leaves implicit, resolved here so no
implementer has to guess:** what happens to chain state when a mid-chain
attacker's follow-up attack is attempted but does not land (blocked, parried,
deflected, or evaded)? Section 3.2's four break conditions do not list "the
follow-up missed," and section 3.1 states the continuation roll only happens
"when the next blow lands" — so a non-landed follow-up is neither a roll nor
a break. `ComboStepsRemaining` and `ComboTargetEntityId` are left exactly as
they were, and the cooldown written after a non-landed attempt stays the
combo cadence (`ComboCooldownTicks`) if the attacker was already chaining
before the attempt, or the normal cadence (`AttackCooldownTicks`) if it was
not. The attacker simply gets to try again next cooldown expiry, at the same
chain position it already held. This is the smallest reading consistent with
"only these four things end a chain."

**A fifth judgment call, also resolved here:** design 3.2's conditions 3 and
4 ("the target dies" / "the target is no longer within reach") describe
states that can become true on a tick where the chaining attacker does not
even attempt an attack — for example the target is killed by a different
attacker, or drifts out of reach while this attacker's cooldown has not yet
expired. `GatherAndCommitAttacks` already has a pre-check that skips an
attacker with no target, a dead target, or an out-of-range target before
resolving anything. Section 3, part 3(c) of this plan requires that same
pre-check, when it fires for an attacker currently holding
`ComboStepsRemaining > 0`, to also clear `ComboStepsRemaining` to `0` and
`ComboTargetEntityId` to `null` before skipping — otherwise the chain state
would sit stale and could be misread as still active if the same target later
came back into range.

## 3. The exact chain state machine

`AgentState.ComboStepsRemaining` holds, at every moment, the number of
*additional* blows a currently-active chain may still land after the blow
that most recently set it. It is `0` whenever no chain is active — before any
chain has ever opened for that agent, immediately after a chain breaks or
caps out, and while a landed blow's opening roll has just failed.
`AgentState.ComboTargetEntityId` holds the entity the active chain is bound
to, and is `null` exactly when `ComboStepsRemaining == 0`. Neither field is a
constructor parameter; both are initialized to `0` / `null` when an
`AgentState` is constructed and are mutated only inside the attack-resolution
stage described below. `AgentState.Level` is set once, at construction, from
`Scenario.PlaceholderFighterLevel`, and never changes afterward — there is no
leveling system yet, per design section 3.3.

`maxSteps` (the cap for the currently-open chain, if any) is never stored. It
is always recomputed on demand as `Math.Min(source.Level, weaponProfile.
ComboMaxSteps)`, which is safe because neither `Level` nor the attacker's
weapon changes for the duration of a chain — this is what lets
`ComboStepsRemaining` stay a single field rather than a counter-plus-maximum
pair, matching design section 5.2's stated intent.

This state machine lives inside the existing attack-resolution stage,
`BattleSimulation.GatherAndCommitAttacks`, pass 1 (the per-attacker loop that
today resolves hit location and `AttackResolution` before buffering a
proposal). No new tick stage is introduced and the pipeline order in
`AdvanceOneTick` does not change, per design section 5.4.

### 3(a). The pre-check gate (runs before any of the below)

The existing pre-check that skips an attacker with no living target, a dead
target, or a target out of the weapon's reach must gain one clause: **if the
attacker is currently chaining (`ComboStepsRemaining > 0`) and this pre-check
is about to skip it for any of those three reasons, clear
`ComboStepsRemaining = 0` and `ComboTargetEntityId = null` before skipping.**
This is what makes design 3.2's checks 3 and 4 observable on the tick the
attacker itself discovers them, not just the tick the target actually died or
left reach.

An attacker that passes the pre-check (has a living target in range and its
`AttackCooldownRemaining == 0` after `DecrementCooldowns`) proceeds to 3(b).

### 3(b). Resolve the attack (unchanged)

`source.Intent = AgentIntent.Attacking` is set, `HitLocationResolver.Resolve`
runs, and `ClashResolver.Resolve` runs, producing `AttackResolution` exactly
as today. Nothing about this step changes.

### 3(c). Combo transition — replaces the old unconditional cooldown reset

Today, `source.AttackCooldownRemaining = AttackCooldownTicks` is written
unconditionally, before resolution is known. For V3 this write must move to
*after* `AttackResolution` is known, because which cooldown to write depends
on the combo outcome. The full replacement logic, in order:

1. `wasChaining = source.ComboStepsRemaining > 0`.
2. `retargeted = wasChaining && source.ComboTargetEntityId !=
   source.TargetEntityId`. This is Question 1's target-binding check,
   evaluated first because the retarget, if any, already happened earlier
   this same tick in `SelectTargetsAndIntents`.
3. If `retargeted`: set `source.ComboStepsRemaining = 0`,
   `source.ComboTargetEntityId = null`, and treat `wasChaining` as `false`
   for the remainder of this algorithm — the attacker is now chain-free for
   this blow, exactly as if no chain had ever been open.
4. If `resolution != AttackResolution.Landed` (Question 2 and the fourth
   judgment call above): this event's chain-position value is `null`.
   `ComboStepsRemaining` and `ComboTargetEntityId` are left exactly as they
   are (post step 3). The cooldown written to
   `source.AttackCooldownRemaining` is `weaponProfile.ComboCooldownTicks` if
   `wasChaining` is still `true`, else `weaponProfile.AttackCooldownTicks`.
   Stop here; do not evaluate 5 or 6.
5. If `resolution == AttackResolution.Landed` and `wasChaining` is `false`
   (post step 3) — an unchained landed blow, eligible to open a chain:
   - Roll `MixCombo(Scenario.Seed, Tick, source.EntityId, target.EntityId,
     source.Loadout.Weapon, comboStepsRemaining: 0, ComboResolver.
     ComboOpenTag) % ClashProfile.BasisPointScale` and compare it against
     `weaponProfile.ComboOpenChanceBasisPoints` (success when
     `roll < ComboOpenChanceBasisPoints`).
   - **Roll fails:** this event's chain-position is `null`.
     `ComboStepsRemaining` stays `0`, `ComboTargetEntityId` stays `null`.
     Cooldown written: `weaponProfile.AttackCooldownTicks`.
   - **Roll succeeds:** `maxSteps = Math.Min(source.Level, weaponProfile.
     ComboMaxSteps)`. This event's chain-position is `1`.
     `source.ComboStepsRemaining = maxSteps - 1`.
     `source.ComboTargetEntityId = target.EntityId`. Cooldown written:
     `weaponProfile.ComboCooldownTicks` if `source.ComboStepsRemaining > 0`,
     else `weaponProfile.AttackCooldownTicks` (the chain opened but is
     already at its own cap — with `PlaceholderFighterLevel = 1` this is the
     normal case for every weapon, since every weapon's cap and the level
     both bound the chain to at most two blows).
6. If `resolution == AttackResolution.Landed` and `wasChaining` is `true`
   (post step 3) — a continuing blow candidate:
   - `maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps)`
     (recomputed; safe, see above).
   - `thisPosition = maxSteps - source.ComboStepsRemaining + 1` — the
     position of the blow that just landed within its chain.
   - Roll `MixCombo(Scenario.Seed, Tick, source.EntityId, target.EntityId,
     source.Loadout.Weapon, source.ComboStepsRemaining, ComboResolver.
     ComboContinueTag) % ClashProfile.BasisPointScale` and compare it against
     `weaponProfile.ComboContinueChanceBasisPoints`
     (`continuationSucceeded = roll < ComboContinueChanceBasisPoints`) — this
     is design 3.2 check 1.
   - `killedByThisBlow = target.HitPoints - source.DamagePerAttack <= 0`,
     computed against `target.HitPoints` as it stands before the
     end-of-pass-1 damage-application loop applies it (mirror however the
     existing `_damageTotals` accumulation reads `target.HitPoints` for this
     same purpose) — this is design 3.2 check 3, evaluated inline because the
     kill, if it happens, happens on this exact blow.
   - This event's chain-position is always `thisPosition` (the blow landed,
     so it always counts, regardless of what happens next).
   - Determine whether the chain survives past this blow, checking in
     design 3.2's exact order: it does **not** survive if
     `!continuationSucceeded` (check 1), or if `thisPosition >= maxSteps`
     (check 2, "max length reached"), or if `killedByThisBlow` (check 3).
     Check 4 ("target out of reach") cannot fire here — the pre-check in
     3(a) already guaranteed the target is in range for this tick's attempt
     — it fires on a *later* tick via 3(a)'s clearing clause instead.
   - **Chain does not survive:** `source.ComboStepsRemaining = 0`,
     `source.ComboTargetEntityId = null`. Cooldown written:
     `weaponProfile.AttackCooldownTicks`.
   - **Chain survives:** `source.ComboStepsRemaining -= 1` (guaranteed
     `> 0` here, since `thisPosition < maxSteps` was just confirmed).
     `source.ComboTargetEntityId` is unchanged. Cooldown written:
     `weaponProfile.ComboCooldownTicks`.

The event's chain-position value computed in 4/5/6 above is what
`AddAttackEvent` (BattleSimulation.cs private helper, called from pass 2) is
given as the new `comboPosition` argument threaded through the buffered
attack-proposal tuple in `_attackProposals`, exactly the way `hitLocation` and
`resolution` are already threaded from pass 1 into pass 2's event emission.

## 4. BattleEvent bit-packing and event hash fold

### 4(a). Packing

`BattleEvent._combatContext` widens from `int` to `long`. The existing four
fields keep their exact current shifts and the existing `FieldMask = 0xFF`
(cast to `long` at each use site): `HitLocation` at bits 0–7, `Shield` at bits
8–15, `Weapon` at bits 16–23, `Resolution` at bits 24–31 — unchanged, so no
existing accessor's *shift* changes, only the field's storage width and the
casts inside it. A new constant is added: `private const int
ComboPositionShift = 32;`, placing the chain-position byte at bits 32–39 of
the widened field, immediately above the four existing bytes and not
overlapping any of them.

Chain position is independently nullable *within* an attack event — most
attacks are not part of any chain even though `Weapon`/`HitLocation`/
`Resolution` are always present on an attack event — so it cannot reuse the
existing "whole-field-zero means absent" trick, which only distinguishes an
attack event from a non-attack event. Instead: valid chain positions start at
`1` (never `0`, since a position of `0` blows never gets an event), so `0` in
the new byte means "not part of a chain," reachable independently of whether
`_combatContext` as a whole is `CombatContextAbsent`.

Packing, inside the existing `Attack(...)` factory (the private constructor
that builds `_combatContext`): add a `comboPosition` parameter of type
`int?`, and OR in `((long)(comboPosition ?? 0) << ComboPositionShift)`
alongside the four existing terms, only inside the branch that already builds
a non-zero `_combatContext` (i.e., only when `weapon is { }`). The
`NonAttack(...)` factory continues to force `_combatContext` to
`CombatContextAbsent` and therefore implicitly forces chain position absent
too — no separate handling needed there.

New read accessor, following the existing accessors' shape exactly:

```
public int? ComboPosition =>
    _combatContext == CombatContextAbsent
        ? null
        : (int)((_combatContext >> ComboPositionShift) & FieldMask) is var raw && raw == 0
            ? null
            : raw;
```

(Write this however reads most naturally in the surrounding file's style —
the two-step gate is the requirement: whole-field-absent first, then the
per-byte zero-means-absent check second.)

`Attack(...)`'s existing validation (weapon/shield/hitLocation/resolution
must all be defined enum values) gains one more check: if `comboPosition` is
not `null`, it must be `>= 1`. No upper bound is enforced at this layer
(the weapon-profile-and-level-derived cap is enforced where the position is
computed, in section 3, not re-validated here).

### 4(b). Event hash fold

`HeadlessRunner.AddEventToHash` gains a 12th folded word, appended after the
existing 11 (Sequence, Tick, Kind, SourceEntityId, TargetEntityId??0, Value,
FactionId, Weapon, Shield, HitLocation, Resolution) — the existing 11 are not
reordered:

```
AddToHash(ref hash, battleEvent.ComboPosition is { } position
    ? (ulong)(uint)position
    : ulong.MaxValue);
```

This is the same "present value, else `ulong.MaxValue`" sentinel convention
already used for Weapon/Shield/HitLocation/Resolution in this same function,
applied consistently to the new field.

## 5. Exact new field, type, and file names

| Symbol | Type | File | Notes |
| --- | --- | --- | --- |
| `AgentState.Level` | `int` (get-only) | `src/Hukbo.Core/Simulation/AgentState.cs` | New constructor parameter, set once at spawn from `Scenario.PlaceholderFighterLevel`, never mutated afterward. |
| `AgentState.ComboStepsRemaining` | `int` (get; set) | `src/Hukbo.Core/Simulation/AgentState.cs` | Not a constructor parameter; initialized to `0`, mutated only in `GatherAndCommitAttacks` per section 3. |
| `AgentState.ComboTargetEntityId` | `ulong?` (get; set) | `src/Hukbo.Core/Simulation/AgentState.cs` | Not a constructor parameter; initialized to `null`, mutated only alongside `ComboStepsRemaining`. New authoritative state beyond the two fields named in the determinism brief — required to make Question 1's strict target-binding enforceable at all, since `TargetEntityId` is recomputed fresh every tick and cannot itself answer "did the target change since the chain's last blow." Folded into `StateHasher` as a third new per-agent word (section 5, `StateHasher` row below); this is a deliberate, documented expansion of the literal "two fields" instruction, not an oversight. |
| `Scenario.PlaceholderFighterLevel` | `int` (get; init; default `1`) | `src/Hukbo.Core/Simulation/Scenario.cs` | Same `init`-only-with-default-literal shape as `CombatPreset`. Added to the manual `Equals`/`GetHashCode` overrides and to `Validate()` (must be `>= 1`). |
| `WeaponProfile.ComboOpenChanceBasisPoints` | `int` | `src/Hukbo.Core/Combat/WeaponProfile.cs` | Basis points out of `ClashProfile.BasisPointScale` (10,000) — no new scale constant is declared; the existing one on `ClashProfile` is reused. Validated `0 <= value <= ClashProfile.BasisPointScale`. |
| `WeaponProfile.ComboContinueChanceBasisPoints` | `int` | `src/Hukbo.Core/Combat/WeaponProfile.cs` | Same validation as above. |
| `WeaponProfile.ComboMaxSteps` | `int` | `src/Hukbo.Core/Combat/WeaponProfile.cs` | Validated positive (`>= 1`). |
| `WeaponProfile.ComboCooldownTicks` | `int` | `src/Hukbo.Core/Combat/WeaponProfile.cs` | Validated positive, same rule as the existing `AttackCooldownTicks` validation. |
| `ComboResolver.ComboOpenTag` | `private const ulong` | `src/Hukbo.Core/Combat/ComboResolver.cs` (new file) | `0x484B424F5F4F504EUL` — ASCII `HKBO_OPN`. Distinct from `HitLocationResolver.HitLocationTag` (`HKBO_HIT`) and `ClashResolver.ClashTag` (`HKBO_CLS`). |
| `ComboResolver.ComboContinueTag` | `private const ulong` | `src/Hukbo.Core/Combat/ComboResolver.cs` (new file) | `0x484B424F5F434E54UL` — ASCII `HKBO_CNT`. |
| `ComboResolver.MixCombo` | `internal static ulong` | `src/Hukbo.Core/Combat/ComboResolver.cs` (new file) | Signature: `MixCombo(ulong seed, long tick, ulong sourceEntityId, ulong targetEntityId, WeaponId weapon, int comboStepsRemaining, ulong salt)`. Body mirrors `HitLocationResolver.MixAttack` exactly: `Fnv1a.OffsetBasis`, then `Fnv1a.Add` for `salt`, `seed`, `tick` (unchecked cast), `sourceEntityId`, `targetEntityId`, `(ulong)weapon`, and finally `(ulong)(uint)comboStepsRemaining`, returned raw (the caller reduces it with `% ClashProfile.BasisPointScale`, exactly as `ClashResolver.MixClash` does internally for its own bounded roll — `MixCombo` itself returns the raw 64-bit hash so both the open and continue call sites can each take the modulo themselves against the same two named tags). |
| `BattleEvent.ComboPosition` | `int?` (get-only, computed accessor) | `src/Hukbo.Core/Simulation/BattleEvent.cs` | See section 4(a). |

**A consequence that must not be missed.** `AgentState`, `Scenario`,
`StateHasher`, and `BattleEvent`/`HeadlessRunner.AddEventToHash` are shared
by every `CombatPresetId`, not just V3. Adding `Level`/`ComboStepsRemaining`/
`ComboTargetEntityId` to the per-agent `StateHasher` fold, and
`ComboPosition` to the event hash fold, changes the state hash and event hash
for **every** scenario compiled against the new build — including V1 and V2
scenarios whose gameplay is otherwise untouched. Every existing pinned hash
literal in `tests/Hukbo.Core.Tests/DeterminismTests.cs` — not only new V3
assertions — must be re-recorded against the new build before the suite can
pass again; this is expected and is exactly the "new golden expectations"
CLAUDE.md section 5 requires for a hash-moving change, not a sign anything is
wrong. `CombatRuleset.ContentHash` is different: V1 declares no
`WeaponProfile` instances at all, so its pinned `ContentHash`
(`0x59FB4CA563D87A49UL`) is unaffected by widening the `WeaponProfile` record
and does not need to move. V2's pinned `ContentHash`
(`0x10AB1CC226AB3636UL`) **does** need to move, because V2's `Build()` must
now supply the four new fields for all six of its roster entries (see task 1
in section 6) with real, non-zero copied values.

**V2's combo values must be a true no-op, not merely "authored."** Because
the attack-resolution logic in section 3 is generic Hukbo.Core code that runs
for any scenario with `HasWeaponProfiles`, and V2 has weapon profiles, V2's
`Build()` must set every profile's `ComboOpenChanceBasisPoints = 0`. At
`0`, the roll comparison `roll < 0` can never succeed (the roll is always in
`[0, 9999]`), so no chain can ever open under V2, and V2's battles play out
identically to before this change — an extra roll is computed and discarded
per landed blow, but it changes no target, no damage, no hit location, and no
clash outcome. `ComboContinueChanceBasisPoints`, `ComboMaxSteps`, and
`ComboCooldownTicks` are never read once `ComboOpenChanceBasisPoints = 0`
guarantees a chain never opens, but they still need valid, positive values to
satisfy `WeaponProfile.Validate()` — author them as the same weapon's V3
solo-row values (or, for the two weapons V3 does not field at all — none, V3
fields all four weapons V2's solo rows cover — copy each row's own solo
values). This is the same "author the neutral default, do not invent a
divergent value" instruction design section 4 already gives for the
paired-profile combo columns; it now also applies to every V2 solo profile.

## 6. Task table

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1. Preset & weapon-profile data layer | Add the four `ComboXxx` fields (+ validation) to `WeaponProfile`; add `CombatPresetId.PrecolonialPhilippinesV3 = 3`; add its `CombatPresetRegistry.IsRegistered`/`Get` arms; widen every `WeaponProfile` construction call in V2's `Build()` to supply the four new fields as true no-op values (`ComboOpenChanceBasisPoints = 0`, the rest set to that weapon's V3 solo-row values, per section 5's "V2 no-op" note, on **both** solo and paired rows — paired rows copy their own weapon's solo-row values, per design section 4); author a new `PhilippineCombatPresetV3.cs` whose `Build()` fields exactly the four solo loadouts (Kampilan, Wasay, solo Kalis, solo Itak) with V2's existing damage/reach/cooldown/target-weight/grip/clash values for those four weapons plus the section-4 combo table's real values; update `WeaponProfileTests.cs` for the new fields (widen the existing InlineData Theory pinning V2's six attribute rows to the new column count, using the same no-op V2 values; add the V3-registration/roster/grip Facts mirroring the existing V2 ones). | `src/Hukbo.Core/Combat/WeaponProfile.cs`, `src/Hukbo.Core/Combat/CombatIdentity.cs`, `src/Hukbo.Core/Combat/CombatPresetRegistry.cs`, `src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs`, `src/Hukbo.Core/Combat/PhilippineCombatPresetV3.cs` (new), `tests/Hukbo.Core.Tests/WeaponProfileTests.cs` | `dotnet build` succeeds with `TreatWarningsAsErrors`; `EveryCombatPresetIdIsRegistered` and the widened V2 attribute Theory both pass; `PresetV3` mirrors of `PresetV2DeclaresTheApprovedGrip` and `PresetV2RosterDeclaresSoloBeforePairedWithinEachWeapon` (four-entry, all-solo) pass. | — | `dotnet test tests/Hukbo.Core.Tests --filter WeaponProfileTests` |
| 2. Core mechanic & event surface | Add `Level`/`ComboStepsRemaining`/`ComboTargetEntityId` to `AgentState` and populate `Level` at spawn in `BattleSimulation.CreateAgent` from `Scenario.PlaceholderFighterLevel`; add `PlaceholderFighterLevel` to `Scenario` (default `1`, in `Equals`/`GetHashCode`/`Validate`); add `ComboResolver.cs` with the two salt constants and `MixCombo`; fold the three new `AgentState` words into `StateHasher.Compute`, appended after the existing 17 per-agent words, in the order `Level`, `ComboStepsRemaining`, `ComboTargetEntityId ?? 0`; implement the section 3 state machine inside `BattleSimulation.GatherAndCommitAttacks` (both the 3(a) pre-check clearing clause and the 3(c) resolution logic), threading `comboPosition` through `_attackProposals` into `AddAttackEvent`; widen `BattleEvent._combatContext` to `long` and add `ComboPosition` per section 4(a); fold `ComboPosition` into `HeadlessRunner.AddEventToHash` as the 12th word per section 4(b); extend `AgentState.ToView()`/`AgentView` to carry `Level` (needed by task 3); extend `BattleEventFormatter.FormatAttack` so a landed attack whose `ComboPosition` is non-null appends `" (combo {position})"` to the existing landed-blow phrasing (`"hit {target}'s {part} with {weaponLabel} for {value}"`), producing exactly design section 6's example line; non-landed and non-chained landed attacks are formatted exactly as today. | `src/Hukbo.Core/Simulation/AgentState.cs`, `src/Hukbo.Core/Simulation/Scenario.cs`, `src/Hukbo.Core/Combat/ComboResolver.cs` (new), `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/BattleEvent.cs`, `src/Hukbo.Headless/HeadlessRunner.cs`, `src/Hukbo.Client/Presentation/BattleEventFormatter.cs` | `dotnet build` succeeds; a manual headless run against a V3 scenario (`Scenario.CreateDefault(...) with { CombatPreset = CombatPresetId.PrecolonialPhilippinesV3 }`) completes without exceptions and its event feed contains at least one `(combo N)` line across a few thousand ticks; the same seed run twice produces identical state hash, event hash, and ordered event stream (manual two-run diff, not yet the pinned assertions — those are task 4). | 1 | Manual two-run same-seed diff via a scratch console/script (documented inline as a code comment or scratch file, not committed); no automated test is added in this task — task 4 owns the pinned assertions. |
| 3. Agent inspector UI | Show `AgentView.Level` and, when the agent's weapon profile resolves, the four combo attributes (opening chance, continuation chance, max steps, combo cooldown) in the inspector panel, following the existing `TryResolveProfile` / `FormatAttributeLine` pattern immediately below the existing V2 attribute line; bump `MaximumLowerRowCount` if the new rows are unconditional. | `src/Hukbo.Client/UI/AgentInspectorContent.cs` | `dotnet build` succeeds for `Hukbo.Client`; a focused Client presentation test (GPU-free, per CLAUDE.md section 5) asserts the new lines' exact text for a resolved V3 profile and their absence when no profile resolves. | 2 | `dotnet test tests/Hukbo.Client.Tests --filter AgentInspectorContent` |
| 4. Determinism tests, pinned hashes, and benchmark recording | Add pinned-hash Facts for V3 mirroring the existing V1/V2 ones (`ContentHash`, and a seed-1 state/event hash pair) to `DeterminismTests.cs`; **re-record every existing pinned hash literal in that file** against the new build, per section 5's "consequence that must not be missed" note (V2's `ContentHash`, and any literal state/event hash pinned for V1 or V2 scenarios); add a new `ComboChainTests.cs` covering the section 3 state machine directly (opening succeeds/fails, continuation succeeds/fails/caps, target-switch break, target-death break, target-out-of-range break via the 3(a) clearing clause, non-landed follow-up preserving state) using constructed `AgentState`/`WeaponProfile` fixtures, not full battles; run `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` against V3 (once a `--preset`/equivalent selection path exists — if it does not yet exist on `HeadlessRunner`/`benchmark.ps1`, add the minimal `--preset` argument and `CombatPresetId` parse as part of this task, since no other task owns that gap) and record the resulting state hash, event hash, and outcome as a new dated section in `docs/development/testing.md`, following the existing V2 section's shape. | `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `tests/Hukbo.Core.Tests/ComboChainTests.cs` (new), `docs/development/testing.md` | `dotnet test tests/Hukbo.Core.Tests` passes in full with zero pinned-hash mismatches anywhere in the suite; `docs/development/testing.md` carries a new dated section with real, pasted benchmark output (state hash, event hash, outcome, win rates) for V3 at seed 1. | 2 | `dotnet test tests/Hukbo.Core.Tests` (full run, not filtered) plus the pasted benchmark output in `docs/development/testing.md` |
| 5. T32 tool extension — chain metrics and level sweep | Extend `tools/Hukbo.Tools.WeaponBalance/Program.cs` to run its existing TTK/win-rate suite against `CombatPresetId.PrecolonialPhilippinesV3`, additionally tallying, per weapon, the fraction of landed blows whose `ComboPosition` was non-null and the mean realized chain length (the maximum `ComboPosition` reached per opened chain, averaged), swept across `Scenario.PlaceholderFighterLevel` values `1` through `5` per design section 7; extend the `Report` record and console output with these new columns; append the results as a new dated section to `docs/development/testing.md` (same file task 4 touches — this task runs after task 4's section is already appended, so append below it; if both land in the same integration pass, resolve any conflict by appending in commit order, not by editing each other's section). | `tools/Hukbo.Tools.WeaponBalance/Program.cs` | The tool runs to completion for all five level values without exceptions; its output table matches design section 7's required metrics (mean TTK, chain fraction, mean realized chain length, win rate) per weapon per level; results are pasted into `docs/development/testing.md` with an explicit note on whether the itak's realized throughput exceeds the wasay's (the specific inversion check design section 7 calls out). | 2 | Direct run of the tool (`dotnet run --project tools/Hukbo.Tools.WeaponBalance`) with its console output pasted into `docs/development/testing.md`; this tool is not part of `Hukbo.slnx` or the canonical gate, so this is the only verification it gets. |

Tasks 3, 4, and 5 all depend only on task 2 and touch disjoint files, so they
can run in parallel with each other once task 2 has landed. Task 2 depends on
task 1 because it reads the four new `WeaponProfile` fields and the V3
preset. No two tasks in this table edit the same file.

## 7. Verification

`./scripts/verify.ps1` is run exactly once, by the orchestrator, after every
task above has landed — never by a sub-agent, and never per-task. Its actual
pasted output is the only thing that flips this plan to done. The
per-task "Verified by" commands in section 6 are fast local feedback for the
implementer of that task; they are not a substitute for the canonical gate
and do not by themselves authorize marking any task, or this plan, complete.
