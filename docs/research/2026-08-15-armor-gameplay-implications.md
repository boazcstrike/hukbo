# Armor gameplay implications: what the code does today and what change would cost

Date: 2026-08-15
Status: engineering assessment, read-only research

## 1. Purpose

This document records what Hukbo's simulation and client code currently do with
armor, and what it would actually cost — in code, in pinned expectations, and in
process — to turn armor into a mechanic that changes the outcome of a battle.

**This document authorizes nothing.** It is not a design document, it is not a
proposal, and it does not select a mechanic. It proposes no damage numbers, no
multipliers, no new enum members, and no tick-stage placement. Under the
repository workflow recorded in `CLAUDE.md` section 6, any non-trivial feature
requires a design document under `docs/plans/YYYY-MM-DD-<slug>-design.md` first,
and a design document by itself still does not authorize implementation — a plan
document has to follow it. No live plan in `docs/plans/` authorizes an armor
feature today. Nothing here changes that. The only thing this assessment is
qualified to do is tell a future design author what they are walking into.

The historical evidence question — what armor people in the sixteenth-century
Philippine archipelago actually wore, and how confidently that is attested — is
out of scope here and is handled separately in
`docs/research/HISTORICAL_1500s_ARMOR.md`. This document is about code.

## 2. Executive summary

Hukbo has an armor socket with nothing plugged into it.

`ArmorId` exists as a real, first-class combat identity. It is declared in
`src/Hukbo.Core/Combat/CombatIdentity.cs:106-109`, it is carried on every
warrior's `CombatLoadout` (`src/Hukbo.Core/Combat/CombatIdentity.cs:275-279`), it
is folded into the combat ruleset's content hash
(`src/Hukbo.Core/Combat/CombatRuleset.cs:787-791` and `:809-811`), it is folded
into the per-tick authoritative state hash for every agent
(`src/Hukbo.Core/Determinism/StateHasher.cs:151`), and it forms one third of the
movement system's loadout key (`src/Hukbo.Core/Movement/MovementRuleset.cs:376-387`).
Every determinism obligation that a live gameplay identity would carry is already
being met for armor.

What armor does not do is affect anything. The enum has exactly one member,
`LightOrganic = 1`. No combat resolver reads it. There are zero occurrences of the
string `Armor` in `src/Hukbo.Core/Combat/HitLocationResolver.cs` and zero in
`src/Hukbo.Core/Combat/ClashResolver.cs` — the two files that decide, respectively,
which body part an accepted attack strikes and whether that attack lands at all.
The only defense-side multiplier the ruleset exposes,
`CombatRuleset.ResolveDefenseMultiplier` at
`src/Hukbo.Core/Combat/CombatRuleset.cs:222-233`, is keyed on `ShieldId` alone.
There is no armor-keyed multiplier anywhere in the simulation. Armor does not
affect movement either: every movement lookup that includes armor in its key
pattern-matches `ArmorId.LightOrganic` as a constant in every single arm, so the
armor component of the key is currently a fixed value that cannot discriminate
between rows.

The practical consequences of that shape are the useful findings, and the rest of
this document develops them:

- The wiring cost of making armor authoritative is unusually low, because the
  identity, the hash folds, the loadout plumbing, and the movement key are all
  already in place and already correct. Nobody has to invent a place to put
  armor; the slot is cut.
- The *decision* cost is the real cost. Nothing in the codebase expresses any
  opinion about what armor should do, so a design has to originate the rule, its
  numbers, its tick stage, and — most demanding under this repository's own bar —
  its spectator explanation.
- The *pinned-expectation* cost is concrete and enumerable. Five recorded
  occurrences of the preset V1 content-hash literal across three files would have
  to move together the moment a new armor is declared in a preset roster, and one
  of those occurrences lives inside explanatory prose rather than in a code
  literal.
- There is one live crash trap. Adding a second `ArmorId` member and getting it
  onto an agent's loadout throws at runtime in the client inspector, and the
  existing Client test suite does not catch it.
- There is an existing, unrelated defect worth fixing regardless of whether armor
  ever becomes a mechanic: the inspector's `Armor:` row carries no evidence tier,
  which the historical-accuracy policy in `CLAUDE.md` section 7 requires of
  cultural identifications shown to the player.

## 3. Two unrelated systems share the word "armor"

This is the single most important thing for a reader to understand before
touching anything, because the two systems have almost nothing to do with each
other and are easy to confuse from a symbol name alone.

The first is the **simulation armor identity**, `ArmorId`. It lives in
`Hukbo.Core`, it is authoritative, it is hashed, and it currently changes no
outcome.

The second is the **cosmetic armor wardrobe**, research category F. It lives
entirely in `Hukbo.Client`, it is presentation-only, it is never hashed, and it
has five distinct options that visibly change how a pawn is drawn. It is selected
by the appearance-preset system, not by the warrior's combat loadout.

| | Simulation `ArmorId` | Cosmetic armor wardrobe |
| --- | --- | --- |
| Declared at | `src/Hukbo.Core/Combat/CombatIdentity.cs:106-109` | `src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs:720`, `:749`, `:775`, `:800`, `:826` |
| Member count | One: `LightOrganic = 1` | Five: `ArmorF1Unarmored`, `ArmorF2CordedFiberArmor`, `ArmorF3HideCorselet`, `ArmorF4WoodenBreastplate`, `ArmorF5ShellSetHelmet` |
| Carried on | `CombatLoadout.Armor` (`CombatIdentity.cs:277`) | `AppearancePresetRecipe.Armor`, an optional slot (`src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Levy.cs:80`, `:105-106`) |
| Chosen by | The combat preset's roster, e.g. `src/Hukbo.Core/Combat/PhilippineCombatPresetV6.cs:253-256` | The regional appearance presets, e.g. `AppearancePresets.Visayan.cs:385`, `AppearancePresets.Tagalog.cs:377`, `AppearancePresets.NorthernLuzon.cs:265` |
| Reaches the state hash | Yes — `src/Hukbo.Core/Determinism/StateHasher.cs:151` | No, never |
| Reaches the content hash | Yes — `src/Hukbo.Core/Combat/CombatRuleset.cs:787-791`, `:810` | No, never |
| Affects combat | No | No |
| Affects movement | No (constant in every key arm) | No |
| Affects what you see | Only the inspector's `Armor:` text row | Yes: torso capsule width and an armor material tone |

The cosmetic path runs from the selected appearance recipe's category-F entry
into an armor width factor and a material tone, then into
`PawnGeometry.CreateArmor`, which produces `PawnLayout.ArmorBounds`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:180`, documented at `:2072-2089`),
which `PawnRenderer.DrawArmor` (`src/Hukbo.Client/Rendering/PawnRenderer.cs:849-881`,
`private static`) fills as two flank bars via
`PawnGeometry.GetArmorFlankBars` (`src/Hukbo.Client/Rendering/PawnGeometry.cs:2250`).
An unarmored pawn resolves an armor width factor of `1f`, `ArmorBounds` comes back
`Rectangle.Empty`, and `DrawArmor` returns immediately
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:855-858`). At the `Low` detail tier
the design confines armor to a tone contribution folded into the torso fill rather
than a separate rectangle, which is also documented at
`src/Hukbo.Client/Rendering/PawnGeometry.cs:2078-2082`.

Note that `PawnAppearanceFactory` contains no reference to `ArmorId` and no
reference to the category-F catalog entries at all; the cosmetic armor arrives
through the appearance preset it selects, not through the combat loadout it is
handed. That separation is deliberate and is stated in the factory's own comment
at `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs:20-22`: weapon and
shield roles come only from the authoritative Core loadout, while entity ID drives
clothing and appearance and must never influence equipment identity. Armor sits on
the appearance side of that line today.

The historical evidence behind the five cosmetic options is documented at
`docs/research/improve-visuals/warrior-appearance-historical-research.md:462-569`
(Category F, entries F1 through F5).

A consequence worth stating plainly: a pawn can be drawn wearing a hardwood
breastplate while its authoritative loadout says `LightOrganic` and the simulation
treats it as identical to a bare-chested pawn. That is not a bug today, because
armor is not a mechanic today. It becomes a legibility problem the moment armor
starts changing outcomes, and section 8 returns to it.

## 4. What armor does today, with file and line for every claim

### 4.1 The identity itself

`src/Hukbo.Core/Combat/CombatIdentity.cs:106-109` declares the whole enum:

```csharp
public enum ArmorId
{
    LightOrganic = 1,
}
```

Its doc comment at `:102-105` states that the numeric values are part of the
deterministic replay and content-hash contract and must not be renumbered or
reordered. `CombatLoadout` carries it as its second positional component at
`src/Hukbo.Core/Combat/CombatIdentity.cs:275-279`, alongside `Weapon`, `Shield`,
and a defaulted `Rank`.

### 4.2 Where armor is written

Every shipped combat preset declares the same single-element armor set and then
repeats `ArmorId.LightOrganic` in every roster row:

- `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs:109` and rows `:134-137`
- `src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs:189` and rows `:217-222`
- `src/Hukbo.Core/Combat/PhilippineCombatPresetV3.cs:189` and rows `:215-218`
- `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:192` and rows `:219-222`
- `src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:280` and rows `:312-320`
- `src/Hukbo.Core/Combat/PhilippineCombatPresetV6.cs:227` and rows `:253-256`

In every one of those files the declaration is literally
`var armors = new[] { ArmorId.LightOrganic };`. There has never been a second
armor value in a shipped roster.

### 4.3 Where armor is read

Armor is read in exactly eight places across `src/`, and none of them lets it
change an outcome:

| Site | What it does with armor |
| --- | --- |
| `src/Hukbo.Core/Combat/CombatRuleset.cs:787-791` | Folds `_armors.Count` and each sorted member into `ContentHash` |
| `src/Hukbo.Core/Combat/CombatRuleset.cs:810` | Folds each roster loadout's `Armor` into `ContentHash` |
| `src/Hukbo.Core/Determinism/StateHasher.cs:151` | Folds each agent's `Loadout.Armor` into the per-tick state hash |
| `src/Hukbo.Core/Movement/MovementRuleset.cs:673` | Folds each movement profile's `Loadout.Armor` into `MovementRuleset.ContentHash` |
| `src/Hukbo.Core/Movement/MovementRuleset.cs:376-387` | Part of the six-row canonical loadout key; `LightOrganic` constant in every arm |
| `src/Hukbo.Core/Movement/MovementRouteRules.cs:302-309` | Same six-row key for `CanonicalOpponentIndex`; `LightOrganic` constant in every arm |
| `src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs:70-84` | Same six-row key for composition bucketing; `LightOrganic` constant in every arm |
| `src/Hukbo.Client/UI/AgentInspectorContent.cs:300` | Renders one `Armor:` text row in the agent inspector |

`src/Hukbo.Core/Movement/LoadoutMovementProfile.cs:142-143` also reconstructs a
`CombatLoadout` from `loadout.Weapon, loadout.Armor, loadout.Shield`, which
carries the field forward but reads no meaning from it.

### 4.4 Where armor is conspicuously absent

The two files that decide combat outcomes contain zero occurrences of the string
`Armor`:

- `src/Hukbo.Core/Combat/HitLocationResolver.cs` — decides which `BodyPart` an
  accepted attack strikes. Its own doc comment at `:19-20` states the contract
  explicitly: only `CombatLoadout.Weapon` affects the attacker side, and only
  `CombatLoadout.Shield` affects the defender side. Its single defender-side read
  is `rules.ResolveEffectiveWeights(attacker.Weapon, defender.Shield)` at `:60`.
- `src/Hukbo.Core/Combat/ClashResolver.cs` — decides whether an accepted attack
  lands at all. Its channel computation at `:198-209` takes `defenderWeapon` and
  `defenderShield` and nothing else from the defender's equipment.

There is no armor-keyed lookup in `CombatRuleset` at all. The only defensive
lookup is `ResolveDefenseMultiplier(ShieldId shield, BodyPart bodyPart)` at
`src/Hukbo.Core/Combat/CombatRuleset.cs:222-233`, backed by the
`_shieldMultipliers` dictionary declared at `:36`.

### 4.5 The code's own commentary on the situation

Two places in the codebase already acknowledge that armor is a placeholder, and
both are worth reading before designing anything.

`src/Hukbo.Core/Combat/CombatRuleset.cs:178-181`, in the doc remark on
`WithClashProfile` (not on the constructor), says that the armor set "has no
accessor yet is folded into the content hash", and that reconstructing it by hand
"happens to be faithful today only because `ArmorId` has one member". That is a
latent fragility: the moment a second armor exists, that reconstruction stops
being trivially correct — though `WithClashProfile` itself passes `_armors`
through directly at `src/Hukbo.Core/Combat/CombatRuleset.cs:201`, so the remark
describes the alternative it avoided rather than a live defect.

`src/Hukbo.Core/Movement/MovementRuleset.cs:343-353` explains why armor is in the
movement key at all despite being constant: the lookup "throws for an unmapped key
… rather than returning a default, so a future armor or shield fails loudly
instead of silently inheriting another row's footwork." The same reasoning is
repeated at `src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs:62-67`, which
says an out-of-range triple throws "so a future weapon, armor, or shield fails
loudly here instead of vanishing from every composition-driven decision."

This is the correct reading of those three pattern matches: they are a
forward-compatibility guard, deliberately designed to fail fast when a new armor
appears. They are not evidence that armor differentiates movement today. Armor is
a carried field only.

## 5. `ShieldId` as the worked precedent

`ShieldId` is the same kind of identity as `ArmorId` — declared four lines below
it, at `src/Hukbo.Core/Combat/CombatIdentity.cs:115-119`, under the same
"do not renumber or reorder" doc comment, carried in the same `CombatLoadout`,
folded into the same three hashes. The difference is that `ShieldId` is live. It is
the best available model of what a fully realised defensive identity looks like in
this codebase, and an armor design should be measured against it rather than
against a blank page.

`ShieldId` has two members, `None = 1` and `TallHardwood = 2`, and it changes
outcomes through two distinct channels.

**Channel one: hit-location weighting.** `CombatRuleset` holds a
shield-keyed table of `TargetWeightProfile` values, `_shieldMultipliers`, declared
at `src/Hukbo.Core/Combat/CombatRuleset.cs:36`, exposed through
`ResolveDefenseMultiplier(ShieldId, BodyPart)` at `:222-233`. Those multipliers
are not consulted per attack. They are baked once, at ruleset construction, into
a precomputed effective-weight table: `BuildEffectiveWeightTables` at
`src/Hukbo.Core/Combat/CombatRuleset.cs:629-652` walks every (weapon, shield)
pair and every body part, multiplying the weapon's own target weight by the
shield's defense multiplier at `:640-642`, and stores the resulting weights and
their total. `ValidateResolvedTotals` at `:613-627` then refuses to construct a
ruleset where any (weapon, shield) pair resolves to a zero total, so no shield can
make a warrior untargetable. At attack time,
`HitLocationResolver.Resolve` performs a single lookup —
`rules.ResolveEffectiveWeights(attacker.Weapon, defender.Shield)` at
`src/Hukbo.Core/Combat/HitLocationResolver.cs:60` — and draws against the
precomputed table.

That precomputation pattern is the load-bearing detail. It means a defensive
identity in this codebase costs nothing per attack; the cost is paid once at
construction, and the per-attack path stays a single indexed read. Any armor rule
that wants to be cheap has this template available to copy. It also means the
combinatorial size of the baked table is (weapons × shields) today and would
become (weapons × shields × armors) if armor joined it, which is a real
consideration once armor has more than one member.

**Channel two: defensive resolution.** `ClashResolver` takes `defenderShield`
directly. It folds the shield into its keyed roll at
`src/Hukbo.Core/Combat/ClashResolver.cs:70`, and `ComputeChannels` at `:198-209`
resolves a shield-intercept channel (`profile.ResolveShieldIntercept(defenderShield)`
at `:207`), a weapon-intercept channel that also reads the shield at `:208`, and a
void channel that reads it again at `:209`. A successful shield channel produces
`AttackResolution.ShieldBlocked` at `:120`. The comment at `:110-115` records that
with `ShieldId.None` the shield interval is `[0, 0)` and is stepped over rather
than selected,
which is the honest way to make a "no equipment" member a real member rather than
a special case bolted on outside the model.

The whole defensive-resolution mechanic is documented as a standing contract at
`SIMULATION-GAME-STANDARDS.md:820-836`, including its pinned tick stage: it runs
inside `GatherAndCommitAttacks`, after the reach and cooldown gates and after
`HitLocationResolver.Resolve` has chosen a body part, and before damage is
applied. The five-member `AttackResolution` enum, with pinned numeric values and
an append-only rule, is at `SIMULATION-GAME-STANDARDS.md:837-847`.

Finally, `ShieldId` is legible to the spectator. The inspector renders a
`Shield:` row at `src/Hukbo.Client/UI/AgentInspectorContent.cs:301`, and
`ShieldBlocked` is a named outcome in the event stream rather than an invisible
probability adjustment. Section 8 returns to why that matters.

## 6. The cost of making armor authoritative

### 6.1 Adding an unused enum member alone moves no hash

This is worth establishing first, because it determines how a change could be
staged.

`CombatRuleset.NormalizeArmors` at `src/Hukbo.Core/Combat/CombatRuleset.cs:745-756`
sorts and deduplicates the armor list a preset actually supplies. It is a
duplicate guard; it does not enumerate the members of the `ArmorId` type.
`ComputeContentHash` at `:787-791` then folds `_armors.Count` followed by each
member of that supplied list, and `:809-811` folds each roster loadout's weapon,
armor, and shield. On the state side, `StateHasher` at
`src/Hukbo.Core/Determinism/StateHasher.cs:151` folds `(int)agent.Loadout.Armor`
for each agent.

Every one of those folds reads values that are actually present, not the shape of
the enum type. Adding a member to `ArmorId` that no preset declares and no agent
carries therefore changes neither hash. Hashes move when the new armor is *wired
in* — the moment it appears in a preset's `armors` array, or in a roster row, or
on an agent.

That said, appending a member is not free of obligations. `CLAUDE.md:245-248` says
that changing enum numeric values, enum order, roster order, weights, or a hash
mixer requires a new preset version plus new golden expectations. An append at the
end that renumbers nothing does not trip the letter of that rule, but the moment a
preset roster changes, it does. And section 6.4 below describes a runtime crash
that an unused enum member can still cause the moment anything routes it to the
client.

### 6.2 A new preset version, not an edit to an existing one

The shipped presets are immutable historical records, not editable configuration.
`PhilippineCombatPreset` through `PhilippineCombatPresetV6` each exist as a
separate file with its own `CombatPresetId` value
(`src/Hukbo.Core/Combat/CombatIdentity.cs:127-129` onward), precisely so that a rule
change never mutates a preset a recorded replay depends on. An armor mechanic
that changes any warrior's loadout, or that adds an armor-keyed table to the
ruleset, requires a new preset version by the `CLAUDE.md:245-248` rule quoted
above, plus new golden expectations for it.

Note that the armor set is a constructor parameter of `CombatRuleset`
(`src/Hukbo.Core/Combat/CombatRuleset.cs:62`) with no public accessor, which is
what the `WithClashProfile` remark at `:178-181` complains about. If armor gains a
per-armor table analogous to `_shieldMultipliers`, that table would need the same
treatment `_shieldMultipliers` gets — construction validation, a deterministic
ordered fold into the content hash, and a decision about whether the derived
effective-weight table grows a third key dimension.

### 6.3 The pinned-hash blast radius: five occurrences across three files

The preset V1 content hash `0x59FB4CA563D87A49` is recorded in five places
outside build output. All five move together or the suite goes red, and one of
them is not a code literal at all.

| Site | Form |
| --- | --- |
| `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs:177` | `Assert.Equal(0x59FB4CA563D87A49UL, PhilippineCombatPreset.Rules.ContentHash);` |
| `tests/Hukbo.Core.Tests/DeterminismTests.cs:25` | `private const ulong PreClashContentHash = 0x59FB4CA563D87A49UL;` |
| `tests/Hukbo.Core.Tests/DeterminismTests.cs:135` | `Assert.Equal(0x59FB4CA563D87A49UL, first.ContentHash);` |
| `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json:7` | Embedded in the fixture's `stateHashSpecification` explanatory prose |
| `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json:216` | Embedded again inside the fixture's reproduced harness source, which the fixture carries as an array of escaped C# source lines |

The two fixture occurrences are the ones that are easy to miss. They are prose and
reproduced source, not assertions, so a search that only looks at `.cs` assertion
sites will not find them, and neither will a compiler. Anyone moving this hash has
to edit explanatory English inside a JSON file and keep it truthful, not just
retype a number. The fixture is 10,192 lines long, so a full read is not a
practical way to find them; search for the literal.

Both `DeterminismTests.cs` occurrences also carry warnings in their surrounding
comments, though only the comment guarding `:135` uses the words "must never
move" verbatim — those comments are about the
conditional-fold behaviour they guard (preset V1 declares no weapon attributes and
no clash profile, so neither block is folded, not even a zero count), so a design
that moves this hash owes an explanation of why the movement is legitimate rather
than a silent re-baseline. `DeterminismTests.cs:20-25` says explicitly that
`PreClashContentHash` is *not* one of the golden constants an implementation phase
re-baselines and must not be swept up in such an edit.

Separately, the repository's other recorded baselines would need attention. Any
change that reaches the state hash moves the seed-1 workload digests recorded in
`docs/development/testing.md`, and the gate's five Hukbo workloads each carry their
own expectations. This document does not enumerate those, because the correct
figures are whatever the newest entry in `docs/development/testing.md` says at the
time the work is done.

### 6.4 The `GetArmorLabel` crash trap

`src/Hukbo.Client/UI/AgentInspectorContent.cs:1483-1491` is:

```csharp
internal static string GetArmorLabel(ArmorId armor) =>
    armor switch
    {
        ArmorId.LightOrganic => "Light Organic",
        _ => throw new ArgumentOutOfRangeException(
            nameof(armor),
            armor,
            null),
    };
```

There is no default label. A second `ArmorId` member that reaches an agent's
loadout makes the agent inspector throw `ArgumentOutOfRangeException` at runtime
the moment a spectator selects that agent — not at build time, and not in a test.

The test suite does not catch this. `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs:44-51`
is a single `[Fact]` that exercises `LightOrganic` only, through
`SampleAgent.Loadout.Armor`, and `SampleAgent` itself is built with
`ArmorId.LightOrganic` hardcoded at `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs:28`.
By contrast the shield equivalent immediately below it at `:53-55` is a `[Theory]`
with an `InlineData` row per `ShieldId` member — still not exhaustive by
construction, but at least covering every member that exists.

This is a known failure class in this repository: a `Hukbo.Core` enum addition
reddening or crashing `Hukbo.Client` because a client-side label switch has a
throwing default arm. The same shape has bitten before with a different enum.
Anyone adding an `ArmorId` member must update `GetArmorLabel` in the same change,
and should consider converting the armor test to a theory driven by the enum's
members so the next addition fails loudly in the test suite instead of quietly at
runtime.

### 6.5 Movement key implications

Three separate `Hukbo.Core` sites pattern-match the full `(WeaponId, ArmorId, ShieldId)`
triple against six literal tuples, with `ArmorId.LightOrganic` constant in every
arm and a throwing default:

- `src/Hukbo.Core/Movement/MovementRuleset.cs:376-387` — `CanonicalLoadoutIndex`,
  which resolves a warrior's `LoadoutMovementProfile`
- `src/Hukbo.Core/Movement/MovementRouteRules.cs:302-309` — `CanonicalOpponentIndex`,
  which resolves opponent-distance offset rows
- `src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs:70-84` — `Add`, which buckets
  a loadout for composition-driven decisions

All three throw `ArgumentOutOfRangeException` for an unmapped triple, by design.
The `MovementRuleset` doc comment at `:343-353` and the `LoadoutCompositionCounts`
doc comment at `:62-67` both state the reason in the same words: a future armor
should fail loudly rather than silently inherit another row's footwork or vanish
from a composition decision. `MovementRuleset.cs:484` also uses
`CanonicalLoadoutIndex` to order the profile collection for its own content-hash
fold, so the canonical order is binding on that hash too.

The practical consequence is that a new armor cannot be introduced quietly. It
either gets six new rows across three files with real movement values behind them,
or the first agent carrying it crashes the movement stage. There is no
middle path where armor exists but movement ignores it, unless the design
deliberately maps the new armor's rows to the same values as `LightOrganic`'s —
which is expressible, but is a decision that should be written down rather than
arrived at by copy-paste. Note also that `MovementRuleset` presets whose
`UsesEquipmentRelativeFootwork` is false throw for *every* key
(`src/Hukbo.Core/Movement/MovementRuleset.cs:348-350`), so the armor question only
arises for presets that use equipment-relative footwork at all.

## 7. The nine acceptance questions, answered as far as the evidence allows

`SIMULATION-GAME-STANDARDS.md:323-336` requires every feature proposal to answer
nine questions; the numbered list itself is at `:327-335`. This section walks them
for a hypothetical armor mechanic. Several of them cannot be answered from code
evidence at all, because the answer *is* the design decision. Marking those
honestly is the point of this section: it shows a design author exactly which
questions the repository has already answered for them and which ones they have to
originate.

**1. User-visible outcome — UNANSWERABLE without a design decision.** Nothing in
the codebase expresses any intent about what armor should do to a battle. There
is no dormant armor multiplier, no commented-out armor branch, no design document
in `docs/plans/` proposing one. The `MovementRuleset` and `LoadoutCompositionCounts`
comments anticipate "a future armor" existing but say nothing about its effect.
This question has no evidence-based answer.

**2. Tick stage and state read/written — PARTIALLY ANSWERED.** If armor follows
the shield precedent, the pipeline position is already fixed and documented.
`SIMULATION-GAME-STANDARDS.md:820-836` pins defensive resolution inside
`GatherAndCommitAttacks`, after the reach and cooldown gates, after
`HitLocationResolver.Resolve` has chosen a body part, and before damage is
applied. An armor rule that biases where a blow lands would join the hit-location
weighting (an armor-keyed multiplier folded into `BuildEffectiveWeightTables`,
`src/Hukbo.Core/Combat/CombatRuleset.cs:629-652`); an armor rule that decides
whether the blow lands at all would join `ClashResolver`'s channel model; an armor
rule that mitigates a landed blow would sit after `AttackResolution.Landed` and
before hit points are decremented. Which of those three it is remains a design
decision, but the standards document is explicit that a new rule must position
itself against this existing pipeline rather than build a parallel one. State
written: none new is strictly required if armor only modifies an existing
resolution, since `CombatLoadout.Armor` is already snapshotted and hashed.

**3. Numeric units and bounds, and the same-tick conflict rule — UNANSWERABLE
without a design decision, but the units are constrained.** The repository has
established idioms: `_shieldMultipliers` holds integer multipliers applied to
integer target weights, and the clash profile works in basis points. Anything
reaching the state hash must be fixed-point or integer per `CLAUDE.md` section 5.
So an armor value would be an integer multiplier or a basis-point share, not a
float. The actual numbers, and the bounds validation analogous to
`ValidateResolvedTotals` (`src/Hukbo.Core/Combat/CombatRuleset.cs:613-627`) that
would keep armor from making a warrior untargetable or invulnerable, are design
decisions.

**4. Total ordering and random-stream policy — ANSWERED, with one hard
requirement.** `SIMULATION-GAME-STANDARDS.md:147` requires that random streams
derive from `(match_seed, system_tag, entity_id or event_id)`. If an armor rule
introduces any new roll, it needs its own fresh 64-bit domain tag that has never
been used before. The existing tags are inventoried at
`SIMULATION-GAME-STANDARDS.md:865-872` — `HKBO_CLS` for the clash resolver,
`HKBO_HIT` for `HitLocationResolver`, the last-stand jitter tag
`0x484B424F5F4C5354`, the collision-priority tag, and `HKBO_CTG`
(`0x484B424F5F435447`) for `ContingentOffset.Compute`. The inventory exists
precisely so unrelated draws never correlate; an armor tag must be added to it and
must not reuse any of those. Alternatively, an armor rule that only reweights an
existing draw (the way shields reweight the hit-location table) needs no new
stream at all, which is the cheaper and better-precedented option.

**5. Cache source and invalidation — ANSWERED by precedent.** The shield model
already demonstrates the correct answer: no runtime cache. `BuildEffectiveWeightTables`
precomputes once at ruleset construction and the per-attack path is a single
indexed read. `CLAUDE.md` section 9 forbids unbounded caches and forbids saving
derived caches into snapshots, so an armor table must be construction-time and
derived-from-the-ruleset, never accumulated during a match. The one new
consideration is table size: the baked table is currently keyed on
(weapon, shield); adding armor as a third dimension multiplies its size by the
armor count.

**6. Save, event, and version effect — ANSWERED, and it is the expensive one.**
This is not presentation-only. `CombatLoadout.Armor` is already folded into the
content hash (`src/Hukbo.Core/Combat/CombatRuleset.cs:810`) and the per-tick state
hash (`src/Hukbo.Core/Determinism/StateHasher.cs:151`), and it is also folded into
the movement ruleset's own content hash
(`src/Hukbo.Core/Movement/MovementRuleset.cs:673`), so any change to what
armor an agent carries moves all three hashes immediately. A new preset version is
required by `CLAUDE.md:245-248`. Section 6.3 enumerates the five recorded
occurrences of the preset V1 content-hash literal. If the mechanic adds a new
outcome to the event stream, `AttackResolution` is append-only with pinned numeric
values (`SIMULATION-GAME-STANDARDS.md:837-847`), which is the precedent to follow
for adding a defensive channel — append, never renumber.

**7. Worst-case complexity and benchmark workload — PARTIALLY ANSWERED.** The
per-attack cost of the shield model is O(1): one table lookup plus one weighted
draw. An armor rule copying that structure inherits the same per-attack cost, with
the construction-time table growing by the armor-count factor described above. The
benchmark workload is already fixed by the repository: the canonical gate runs a
200-agent, 10,000-tick, seed-1 headless determinism workload, and
`SIMULATION-GAME-STANDARDS.md:337-338` additionally requires a reported 500-agent
result for a feature to pass. What is unanswerable is whether a specific proposed
armor rule adds work outside that O(1) envelope, because no rule is proposed.

**8. Spectator explanation — UNANSWERABLE without a design decision, and this is
the question most likely to sink a weak proposal.** See section 8 below; it is
substantial enough to deserve its own section.

**9. Tests that fail before and pass after — PARTIALLY ANSWERABLE.** The shape of
the required tests is clear from the existing suites even though the assertions
are not. At minimum a change would need: a `CombatRuleset` test pinning the new
preset's content hash the way `CombatConfigurationTests.cs:177` pins V1's; a
resolver test showing that two loadouts differing only in armor produce different
outcomes from the same seed (the test that would fail today, because armor is
inert); a determinism test showing same-seed reproducibility under the new preset;
a movement registration test covering the new canonical loadout rows; and a
`GetArmorLabel` test covering every `ArmorId` member, which section 6.4 shows does
not exist today. `CLAUDE.md` section 7 additionally requires that any tuning value
be marked provisional in code comments and tests rather than presented as a
historical measurement.

Summary of what is answerable today: questions 4, 5, and 6 are effectively
answered by existing precedent and existing rules; questions 2, 3, 7, and 9 are
half-answered — the framework exists, the values do not; questions 1 and 8 are
entirely design decisions with no evidence in the repository pointing at any
particular answer.

## 8. Spectator discoverability: the bar an armor rule has to clear

Hukbo is spectator-only. The player does not issue orders, so the only thing the
player gets from a mechanic is the ability to watch it happen and understand why.
`CLAUDE.md` section 6 makes this a completeness criterion rather than a nicety:
every feature proposal must answer whether a spectator can discover the effect
without reading source code, and if not, the feature is incomplete.

The repository has already ruled on almost exactly this question in a closely
related area. `docs/research/WEAPON_CLASH_1500s.md:570-576` records that a single
hidden scalar clash probability "is not discoverable and should not ship." That
conclusion is what produced the five named `AttackResolution` outcomes instead of
one invisible dice roll: `ShieldBlocked`, `Parried`, `Deflected`, and `Evaded` are
distinguishable in the event stream, so a spectator watching a battle can see
*which* defense worked, not merely that something did.

An armor rule expressed as "armored warriors take 15 percent less damage" would
fail that bar for the same reason the hidden clash probability did. It would be a
hidden scalar. Nothing on screen would change, no event would name it, and the
only way to learn it existed would be to read `CombatRuleset`.

Clearing the bar means at least one of the following, and the design has to pick
deliberately rather than assume:

- **A named outcome in the event stream.** The `AttackResolution` precedent at
  `SIMULATION-GAME-STANDARDS.md:837-847` is append-only with pinned numeric values,
  so a new armor outcome could be appended without disturbing the existing four.
  This is the strongest form of discoverability, because it produces a line in the
  battle event feed the spectator can read directly.
- **An inspector field that shows the effect, not just the label.** The inspector
  already renders an `Armor:` row at
  `src/Hukbo.Client/UI/AgentInspectorContent.cs:300`, but that row is a name only.
  The weapon rows do better: `FormatAttributeLines`
  (`src/Hukbo.Client/UI/AgentInspectorContent.cs:842-847`, called at `:283`)
  renders the weapon's resolved damage, reach, and recovery ticks, so a spectator
  can compare two warriors numerically.
  An armor row that showed its resolved effect would follow that existing pattern.
- **A visible difference on the pawn.** This is where the two-systems ambiguity of
  section 3 becomes a real problem rather than a naming curiosity. The cosmetic
  wardrobe already draws five visually distinct armor states, and those states are
  currently chosen by the appearance preset, entirely independently of
  `CombatLoadout.Armor`. If armor becomes authoritative and the pawn's drawn armor
  still comes from an unrelated roll, the game will show armored-looking warriors
  who are mechanically unarmored and vice versa — a legibility defect worse than
  showing nothing at all. A design that wants visual discoverability has to
  reconcile the two systems, and that reconciliation is itself a non-trivial
  change to `PawnAppearanceFactory` and the appearance-preset recipes, constrained
  by the factory's existing rule at
  `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs:20-22` that equipment
  identity comes only from the authoritative Core loadout.

There is also a historical-accuracy constraint on any player-facing armor label.
`CLAUDE.md` section 7 requires that a specific cultural identification appear in
player-facing UI only in the paired form — Filipino name, em dash, plain English
descriptor — and only when the evidence tier is recorded in metadata and shown in
the agent inspector. The current `Light Organic` label
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:1486`) is a generic English
descriptor with no cultural identification, so it does not currently trip that
rule; a named armor type would.

## 9. Known gaps and risks

### 9.1 The inspector's armor row carries no evidence tier

This is a live gap against the repository's own historical-accuracy policy, and it
exists today regardless of whether armor ever becomes a mechanic.

`CLAUDE.md` section 7 requires that a cultural identification shown in player-facing
UI be backed by an evidence tier recorded in metadata and shown in the agent
inspector. The inspector adds an `Armor:` row at
`src/Hukbo.Client/UI/AgentInspectorContent.cs:300` and a `Shield:` row at `:301`.
The weapon gets an evidence-tier line at `:293` via `FormatEvidenceTierLine`
(`:895`), documented as "the weapon's evidence tier label" at `:218`. The cosmetic
shield variant gets its own tier line through `BuildShieldVariantLines`
(`:1087-1115`), which emits `FormatShieldVariantTierLine` (`:1120-1121`). The armor row
gets neither. It is a bare label with no tier and no note.

The mitigating fact is that the current label is `Light Organic`
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:1486`) — a generic English
descriptor, not a cultural identification — so the policy's paired-name rule is not
being violated by the text that ships.

It is worth recording that the sibling document
`docs/research/HISTORICAL_1500s_ARMOR.md` reaches a finding directly relevant here:
its section 7 grades *baluti* at the `Documented` tier on a 1521 eyewitness
attestation and gives the compliant pair-form label **Baluti — Quilted Armor**.
That means the labelling question is not blocked on evidence — a compliant named
label already exists for a Visayan quilted-armor identity, should one ever be
defined. Whether such an identity should exist at all is a design question this
document does not answer.

But the tier is still missing, and armor is
specifically the category where `docs/research/improve-visuals/warrior-appearance-historical-research.md:462-469`
records that the evidence is thinnest relative to popular belief and the accuracy
policy does the most work. This should be reported as a finding and fixed on its
own merits.

### 9.2 Armor durability, degradation, or repair is unauthorized

`CLAUDE.md:505-518` lists what may not be started before the gate authorizes it,
and the ammunition entry is explicit that any stock-and-consumption model for a
projectile is exactly the thing the bullet exists to stop. Armor that wears out,
degrades under repeated blows, or needs repair is the same class of system: a
stock that depletes and has to be replenished. It would be an economy, and it is
unauthorized. Any armor design must be a static property of the loadout, not a
consumable resource, unless and until a separate authorization exists.

### 9.3 Numbers would be a gameplay model, not a measurement

`docs/research/HISTORICAL_1500s_WEAPONS.md:263-344` records the boundary explicitly
for the existing combat preset: the shipped targeting numbers are a gameplay model
built from the document's evidence, not a measured historical claim, and the two
must not be read as making the same kind of assertion. Armor mitigation values
would fall under exactly the same rule. Per `CLAUDE.md` section 7, gameplay tuning
values must be marked provisional in code comments and tests and never presented
as historical measurement. Given how thin the armor evidence is — see the sibling
document `docs/research/HISTORICAL_1500s_ARMOR.md` for that assessment — this
constraint is more binding for armor than it was for weapons, not less.

### 9.4 The forward-compatibility guards are a benefit that has a sharp edge

The three throwing pattern matches described in section 6.5 are good design: they
guarantee a new armor cannot silently inherit another row's behaviour. But they
also mean the first agent to carry a new armor crashes the movement stage, and the
`GetArmorLabel` switch of section 6.4 means the first spectator to inspect that
agent crashes the inspector. Both are loud, which is what they were built to be,
but neither is caught by the test suite in its current shape. A change that adds an
`ArmorId` member and forgets any one of the four sites will compile cleanly and
pass the gate.

### 9.5 The effective-weight table gains a dimension

If armor joins the hit-location weighting, the precomputed table at
`src/Hukbo.Core/Combat/CombatRuleset.cs:629-652` grows from
(weapon × shield) entries to (weapon × shield × armor). With preset V5's roster the
weapon count is already seven distinct weapons across nine roster rows
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:312-320`), so the growth factor
is worth stating in any design rather than discovering at construction time. The
construction-time validation at `:613-627` would also have to walk the larger
product.

### 9.6 Line numbers in this document are a snapshot

Every `path:line` in this document was verified against the working tree on
2026-08-15. Several of the files cited in section 3 —
`src/Hukbo.Client/Rendering/PawnRenderer.cs` among them — had uncommitted
modifications at the time of writing, as part of unrelated in-flight sprite work.
Re-verify any line number before acting on it.

## 10. What would have to happen next, procedurally

This document does not write the design, and deliberately stops short of it. What
follows is only the sequence the repository's own workflow requires, recorded so
that nobody mistakes this assessment for a step that has already been taken.

1. **A decision that armor should become a mechanic at all.** No such decision
   exists. Armor is currently a correctly-plumbed placeholder, and leaving it that
   way is a legitimate outcome. The forward-compatibility guards already in the
   movement layer were written on the assumption that a future armor *might*
   arrive, not that one will.
2. **A design document**, at `docs/plans/YYYY-MM-DD-<slug>-design.md`, per
   `CLAUDE.md` section 6. It has to answer the nine questions of
   `SIMULATION-GAME-STANDARDS.md:327-335` — including the two this document marks
   unanswerable, which are precisely the ones only a design can settle — and it has
   to answer the spectator-discoverability question of section 8 concretely rather
   than by assertion. A design document does not authorize implementation.
3. **A plan document**, at `docs/plans/YYYY-MM-DD-<slug>.md`, with an ordered task
   list, named files per task, and verification criteria per task. This is where the
   five pinned-hash occurrences of section 6.3, the `GetArmorLabel` update of
   section 6.4, and the three movement pattern-match sites of section 6.5 become
   explicit tasks rather than things someone remembers.
4. **Implementation, then the canonical gate.** `./scripts/verify.ps1` is run once
   after integration and its real output is the evidence. No agent report
   substitutes for it, and no smoke-checklist row may be flipped without a human at
   an interactive desktop.
5. **A recorded baseline update** in `docs/development/testing.md` for whichever
   hashes moved, with the reasoning for why each movement was legitimate — the
   `DeterminismTests.cs:20-25` comment is explicit that `PreClashContentHash` is not
   something an implementation phase silently re-baselines.

Nothing in steps 2 through 5 has been started. No gate has been run for this
document, no smoke-checklist row has been touched, and no code has been changed.

