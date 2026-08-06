# Leader character — design

Status: design only. This document does not authorize implementation; the
ordered task list lives in `docs/plans/2026-08-07-leader-character.md`.

Scope: `Hukbo.Client` presentation only. `Hukbo.Core` is not touched by
anything described here.

## 1. Problem statement

The request that started this work was: "Plan and implement leader mechanics. I
want leaders to have a unique character so I can know that they are indeed
leaders."

The simulation already elects a leader for every contingent, every tick.
`MovementRules.ScanContingentLeadersAndLivingCounts`
(`src/Hukbo.Core/Movement/MovementRules.cs:96-140`) fills one leader slot per
`(FactionId, ContingentId)` pair, sixteen slots in all, and the chosen entity
is published to presentation as `AgentView.IsLeader`
(`src/Hukbo.Core/Simulation/AgentView.cs:135`). None of that is in doubt. What
is missing is the ability of a person watching the battle to see it.

Two verified facts define the gap.

**First, the on-sprite leader marker is colour-only.** `PawnRenderer` draws a
cyan horizontal band roughly eight pixels wide and two pixels tall above the
head of any warrior whose `isLeader` flag is set
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1362-1385`, drawn at
`:437-440`, geometry in `PawnRenderer.GetLeaderMarkBounds`). It changes no
silhouette, occupies less than one percent of the pawn's drawn area, and is
drawn identically at every detail tier including Low. At the zoom a spectator
actually watches a two-hundred-warrior battle from, a thin horizontal line of a
slightly different colour is indistinguishable from rendering noise.

**Second, and much worse: the "leader" appearance presets are not gated on
leadership at all.** The appearance catalog already ships rows built
specifically for chiefs and datus — `AppearancePresetsVisayan.Vis15`, labelled
"Visayan Datu"
(`src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Visayan.cs:509-541`),
`AppearancePresetsTagalog.Tag13`, labelled "Tagalog Chief"
(`src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Tagalog.cs:454-485`),
and `AppearancePresetsTagalog.Tag15`, labelled "Tagalog Leader"
(`.../AppearancePresets.Tagalog.cs:532-559`). These rows are selected by the
same weighted random walk as every other row.
`AppearancePresets.SelectPreset`
(`src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Levy.cs:734-770`)
mixes the entity identity with `PresentationSalts.AppearancePresetSelectionSalt`
and walks the pool's cumulative `RarityWeight`. The leader rows carry a rarity
weight of `1` against a standard weight of `6` in the Visayan block
(`AppearancePresets.Visayan.cs:52,62`) and `4` in the Tagalog block
(`AppearancePresets.Tagalog.cs:75,82`), which makes them rare — and rare is all
it makes them. A search of `src/Hukbo.Client/Presentation` for `IsLeader`
returns zero hits.

The consequence is exactly backwards from what the player wants. The gold
necklace, the gold-edged head wrap, the red chinina and the richly dyed bahag
land on randomly chosen rank-and-file warriors at roughly one percent each,
while the warrior the simulation actually elected to lead the contingent almost
always looks like everybody else. A spectator who learns to read the elite kit
as "that one is in charge" is being taught something false by the renderer.

Closing that second gap is the single highest-value change in this design. The
assets exist, the historical review that authorized them is already done, and
the only thing missing is the one boolean that decides who wears them.

## 2. What the request actually asks for

The phrase "unique character" is read here as **identification, not power**. The
sentence that follows it in the request — "so I can know that they are indeed
leaders" — states the goal directly: the player wants to look at the battle and
tell who is leading. Nothing in the request asks for a combat bonus, an aura, a
command radius, or a morale effect, and this design gives none of those.

That reading is also the one the historical sources support. Morga (1609,
**Documented**) describes distinction in war as producing *more followers*, not
a better fighter. The phrasing the repository's own research adopted is worth
quoting because it is the spine of this design: a leading chief did not become a
stronger fighter, he became one whom more people stood with. Plasencia (1589,
**Documented**) says the datos "governed them and were captains in their wars" —
an office, not an attribute. A leader in Hukbo is therefore something you
*recognise*, not something you *fear*, and the whole of this design lives in the
presentation layer for that reason.

Because leadership is derived per tick and never stored or hashed
(`MovementRules.cs:96-140`), and because everything presentation needs is
already published on `AgentView.IsLeader` and `AgentView.Rank`
(`src/Hukbo.Core/Simulation/AgentView.cs:134-135`), no simulation change is
required to satisfy the request. There is no new `MovementPresetId`, no new
`AgentState` field, no new `BattleEventKind`, no state-hash change, and no
movement-preset frozen-digest change anywhere in this design.

## 3. Identification channels

A spectator can be told something in six places. This design uses three of them
and deliberately declines the other three.

| Channel | Available today | Used here | Why |
| --- | --- | --- | --- |
| Appearance preset (what the warrior wears) | Yes, ungated | **Yes** | The assets already exist and are already historically reviewed. Gating them on real leadership is the highest-value change available. |
| On-sprite marker above the head | Yes, colour-only band | **Yes** | The only channel that works at the zoom a battle is actually watched from. Must become a shape, not a colour. |
| Agent inspector text | Partially — a `(leading)` suffix that disappears in one case | **Yes** | The confirmation channel. Turns "that one looks important" into "that one is leading contingent 3". |
| Battle event feed | Yes, general | No | Leadership does not change on an event; it is a standing condition re-derived every tick. A per-tick feed line would flood the two-hundred-entry buffer with nothing new. |
| Battle report | Yes, general | No | The report's existing "Leaderboard" section is a kills top-ten (`src/Hukbo.Client/Presentation/BattleReportAccumulator.cs:375`, `src/Hukbo.Client/UI/BattleReportLayout.cs`) and has nothing to do with contingent leadership. Conflating the two words would make both worse. |
| Sound | Yes, general | No | No leader-related sound slot exists, and a per-leader cue would fire sixteen times a battle with no event to attach to. |

The division of labour between the two visual channels is set by the detail-tier
system and is not a matter of taste. Appearance components declare the detail
tier at which they render, and the components that carry the leader look are
mostly `VisualDetailTier.High`: the gold-edged head wrap C3
(`src/Hukbo.Client/Presentation/Catalogs/AppearanceComponentCatalog.cs:460-472`),
the richly dyed gold-edged bahag E2 (`:670-681`), and the gold necklace I5
(`:1048-1060`) all render only at High tier. The red chinina D3 (`:595-607`) and
the Visayan full-body tattoo I1 (`:973-986`) are `VisualDetailTier.Low` and do
survive a zoomed-out view, but D3 is Tagalog-only and I1 marks prominence rather
than office.

So the appearance channel is a *close-inspection* channel: zoom in on a warrior
and its kit tells you what it is. The on-sprite marker is the *at-a-glance*
channel: it must work when the pawn is a dozen pixels tall. Each one carries the
load the other cannot, and neither is sufficient alone. That is why this design
changes both rather than picking one.

## 4. The design

### 4.1 Gate the leader appearance on actual leadership

`AppearancePresetEntry` gains a status marker naming what social position the
row depicts. Two values are enough:

- `AppearancePresetStatus.General` — the default, carried by every row that
  ships today unless named below.
- `AppearancePresetStatus.Leader` — carried by exactly three rows:
  `AppearancePresetsVisayan.Vis15` ("Visayan Datu"),
  `AppearancePresetsTagalog.Tag13` ("Tagalog Chief"), and
  `AppearancePresetsTagalog.Tag15` ("Tagalog Leader").

`AppearancePresets.SelectPreset` gains a `bool isLeader` argument and resolves
against two pools instead of one:

- When `isLeader` is `true`, the pool is the block's loadout-compatible rows
  whose status is `Leader`. When that pool is non-empty the existing weighted
  walk runs over it unchanged, so a block with more than one leader row (the
  Tagalog block, which has two) still varies between contingents.
- When `isLeader` is `false`, the pool is the block's loadout-compatible rows
  whose status is `General` — that is, today's pool minus the three leader rows.
- When the leader pool is empty for a block, the function falls back to the
  general pool. This is not a corner case: the Northern Luzon block ships no
  chief row at all, deliberately
  (`src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.NorthernLuzon.cs:47-53`),
  and neither does the generic levy block. A leader in one of those blocks
  therefore looks like its neighbours and is identified by the on-sprite marker
  and the inspector alone. Inventing a Cagayan chief costume to fill the hole
  would be exactly the fabrication the historical accuracy policy forbids.

Two consequences are worth stating rather than discovering later.

The rank-and-file pool loses three rows, so every non-leader's preset selection
can change even though its entity identity and salt did not. That is a
presentation-only difference with no hash consequence, but it does mean the
appearance of the whole battlefield shifts on the commit that lands this, and
that is intended.

The *elite* rows — `Vis13` ("Visayan Elite, Gold-Edged Head Wrap") and `Vis14`
("Visayan Elite, Side Blade"), and the prosperous-freeman rows such as `Tag14` —
stay in the general pool at their existing rarity weights. Elite is a statement
about wealth and prominence, not about office; Morga's tattooed, gold-adorned
figures are prominent, not necessarily in command. Removing them from the
general population to sharpen the leader signal would erase a documented social
distinction in order to make a UI cue louder, and the on-sprite marker is the
right place to buy that loudness. See section 9 for the rejected alternative.

### 4.2 Thread leadership through the appearance path

`PawnAppearanceFactory.Create`
(`src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs:37-107`) gains an
`isLeader` argument and forwards it to `SelectPreset`. It influences nothing
else: stature, build, skin, clothing colour, head treatment, weapon tint and
shield skin keep their own salted streams untouched, and equipment identity
stays loadout-only exactly as the file's existing comments require.

**Leadership must become part of the appearance cache key.**
`PawnAppearanceCache` (`src/Hukbo.Client/Presentation/PawnAppearanceCache.cs`)
currently keys on the triple `(entityId, weapon, shield)` and its class remarks
state, as a load-bearing assumption, that "no key input can change during a
battle" and that the cache therefore "fills exactly once and never evicts".
Leadership breaks that sentence: when a contingent's leader dies, the next
tick's scan elects a different living member, and both warriors' appearances
change. The cache's own design already handles this correctly — the stored key
is compared on every read, so a slot whose leadership has flipped produces a
miss and a recomputation, never a stale answer — but the remarks and the test
that assert immutability must be rewritten to say what is now true: one key
input *can* change mid-battle, and correctness rests on the key comparison
rather than on immutability.

Both call sites must pass the real value, and there are exactly two:

- `src/Hukbo.Client/ArenaGame.Rendering.cs:903-907`, the per-frame
  `PawnAppearances.Resolve` call.
- `src/Hukbo.Client/UI/AgentInspectorPanel.cs:143-146`, the direct
  `PawnAppearanceFactory.Create` call that feeds the inspector's weapon label,
  evidence tier, and appearance-preset lines.

If either one is missed the feature silently does not fire, which is the most
expensive failure mode this repository has. A source-scan assertion is specified
in the plan for that reason.

### 4.3 Make the on-sprite marker read as a leader at a glance

The marker stops being a single flat band and becomes a shape with vertical
extent, so that the leader's silhouette above the head differs from a
non-leader's rather than merely being tinted differently.

The proposed form is an upward chevron: a base band the full width of the head
plus two short segments rising from its ends toward the centre, three quads in
total against today's one. It is drawn in the same slot `GetLeaderMarkBounds`
returns, so the structural non-collision guarantee is preserved by construction
— `GetBreakOffMarkBounds`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1408-1419`) derives its own
rectangle from the leader slot and sits immediately above it, which is what lets
a selected leader that is also breaking off show three marks that cannot
overlap.

The slot itself grows. `GetLeaderMarkBounds` moves from a width of
`headBounds.Width / 2` to the full head width and from a height of
`headBounds.Height / 6` to `headBounds.Height / 4`, keeping the existing floors
so a one-pixel head still produces a drawable rectangle. That is a deliberate
change to pinned arithmetic and it moves two pinned test bodies, both named in
the plan.

The marker stays an abstract interface glyph in the existing cyan
(`PawnRenderer.LeaderColor`, `PawnRenderer.cs:39`). It is explicitly **not** a
depicted object: no plume, no standard, no banner, no helmet crest. A rendered
object would be a historical claim, and no source in the corpus gives a
Philippine war leader of this period a visual insignia of military rank. A HUD
glyph makes no such claim, which is the same reasoning that already licenses the
selection ring, the dead mark, and the break-off band.

The marker keeps drawing at Low detail tier, unconditionally, because Low tier
is precisely the zoom at which it is the only channel left.

### 4.4 State leadership in the inspector without losing it

`AgentInspectorContent.FormatContingentLine`
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:290-302`) appends `(leading)` to
the contingent row, and returns `null` outright when `ContingentState` is
`None`. Under that combination — which the frozen `IndependentPursuitV1` preset
produces for every agent, and `PersistentContingentsV2` produces before its
contingent stage first resolves — a leader's leadership is invisible in the one
panel whose job is to explain a warrior.

`BuildLowerLines` therefore emits a standalone leadership row whenever
`agent.IsLeader` is true and the contingent row was suppressed. Because the two
are mutually exclusive by construction, the deepest row count is unchanged and
`MaximumLowerRowCount` stays at `20`
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:65`), so no panel geometry moves
and `ComputeRequiredHeight` keeps its pinned value.

The row's wording follows the discipline the existing code already documents at
`AgentInspectorContent.cs:283-289`: the suffix reads "leading", never "chief"
and never "commander", because the contingent-succession rule it reflects is a
**Provisional reconstruction** and either of those words would assert an
unearned historical rank. This design does not relax that. What it adds is a
pointer to where the historical claim *is* made properly: the appearance-preset
lines the panel already renders through
`AgentInspectorContent.BuildAppearancePresetLines`, called at
`src/Hukbo.Client/UI/AgentInspectorPanel.cs:294-295`, which print the preset's
own display label, its scope tag, its evidence tier, and its per-component
tiers and notes. Once section 4.1 lands, a Visayan contingent leader's inspector
reads "Visayan Datu", scoped Visayan, tier **Documented, form uncertain** — pair
form and evidence tier, sourced from the catalog rather than hand-written in the
panel. That is the whole of the pair-form requirement, satisfied by making the
existing machinery point at the right warrior instead of by adding new text.

## 5. Determinism and stability

Nothing here reaches the state hash, the event hash, the winner, or the ordered
event stream. `Hukbo.Core` is not edited, no `AgentState` field is added, no
`MovementRuleset` field is folded into a content hash, and no movement preset's
frozen trajectory digest can move. The canonical gate's determinism workload is
expected to reproduce its recorded seed-1 baseline byte for byte, and if it does
not, the change is wrong rather than the baseline.

Presentation still has its own stability obligation, and it is the one the user
would notice first if it were broken: a leader whose gold necklace flickers on
and off between frames is a bug even though no hash moved. Three properties
together guarantee it does not.

**The selection stream is a pure function of identity, not of a frame counter or
a clock.** `SelectPreset` mixes `entityId` with
`PresentationSalts.AppearancePresetSelectionSalt` through the SplitMix64
finalizer and walks a pre-built, statically ordered pool
(`AppearancePresets.Levy.cs:734-770`). Adding `isLeader` to the argument list
changes which pool is walked; it introduces no new source of entropy and needs
no new salt. Two calls with the same `(entityId, block, weapon, isLeader)` return
the same row, in the same process and in a different one.

**The leadership input is itself stable within a tick and across replays.**
`AgentView.IsLeader` is written from the authoritative per-tick scan, which
selects one entity per `(FactionId, ContingentId)` slot by lowest `EntityId`, or
by highest `Rank` then lowest `EntityId` under
`MovementPresetId.PersistentContingentsV5` and later
(`MovementRules.cs:96-140`, `MovementRuleset.SelectsLeaderByRank`,
`src/Hukbo.Core/Movement/MovementRuleset.cs:239`). Position never enters the
tie-break. The incumbent therefore holds the slot for as long as it lives, and
appearance changes exactly when leadership changes — on a death — rather than
oscillating. Every frame drawn between two ticks reads the same
`AgentView`, so the appearance cannot change within a tick either.

**The cache cannot serve a stale answer.** With `isLeader` in the key,
`PawnAppearanceCache.Resolve` compares the full stored key on every read and
recomputes on any mismatch (`PawnAppearanceCache.cs:159-199`). A leadership flip
produces one miss and one overwrite on that ordinal, and the fill counter does
not double-count because it only increments on a previously unoccupied slot.

The cost is bounded and small: at most sixteen leader slots exist in a battle, so
a leadership change costs at most two recomputations of a function that is
already computed once per agent per battle.

## 6. Historical evidence

Every visual choice below is a component that already ships in
`AppearanceComponentCatalog` and already passed the repository's historical
review. This design adds no new component, no new preset row, and no new
cultural label. What it changes is *who wears the rows that already exist*.

The governing rule, quoted from
`docs/research/improve-visuals/warrior-appearance-historical-research.md:964`:

> Depict leaders as *denser in gold and dye*, not larger in body.

A larger leader sprite is therefore prohibited by this design, and nothing below
scales a pawn.

### 6.1 Components carried by the three leader rows

| Component | Catalog symbol and line | Culture scope | Evidence tier | Detail tier | Source |
| --- | --- | --- | --- | --- | --- |
| C3 — Putong (Head Wrap, Gold-Edged) | `HeadCoveringC3PutongGoldEdged`, `AppearanceComponentCatalog.cs:460-472` | Unscoped generic | Documented | High | Pigafetta 1521; Loarca 1582. Elite only; must not generalize to every figure wearing a putong. |
| D3 — Chinina (Collarless Jacket, Red — Chiefly) | `TorsoD3ChininaRedChiefly`, `:595-607` | **Tagalog only** | Documented | Low | Morga 1609: headmen wore red chininas. Sole shipped user is `Tag13`. |
| E2 — Bahag (Loincloth, Richly Dyed) | `LowerGarmentE2DyedGoldEdged`, `:670-681` | Unscoped generic | Documented | High | Pigafetta 1521 – Morga 1609. Elite presets only. |
| E3 — Waist Cloth | `LowerGarmentE3WaistCloth`, `:694-707` | Unscoped generic | (per catalog entry) | — | Silhouette-bearing lower garment the repository reserves for leader presets; carried by `Vis15`. |
| G1 — Red Waist Sash | `SashBeltG1RedWaistSash`, `:855-865` | **Visayan only** | Documented, form uncertain | Medium | Early Visayan lexical record; sash lines on Boxer Codex figures. Its colour symbolism is explicitly *not* the earned-red head-wrap rule. |
| H1 — Sheathed Side Blade (gold-hilted elite variant) | `AccessoryH1SheathedSideBlade`, `:918-930` | Unscoped generic | (per catalog entry) | — | Carried by `Vis14`, which is Wasay-armed only by H1's own rule. |
| H2 — Draped Shoulder Cloth | `AccessoryH2DrapedShoulderCloth`, `:943-955` | Unscoped generic | Documented, form uncertain | — | Boxer Codex silhouette and colour only, not technical cataloguing. Catalog note prefers it on leader presets; carried by `Vis15`. |
| I1 — Full-Body Tattoos | `AdornmentI1FullBodyTattoos`, `:973-986` | **Visayan only** | Documented | Low | Pigafetta 1521; Loarca 1582; Boxer Codex; Chirino 1604. Marks *prominence*, earned and cumulative — not office. Must never appear on Tagalog, Cagayan, or generic presets. |
| I4 — Gold Earrings | `AdornmentI4GoldEarrings`, `:1022-1035` | Unscoped generic | Documented | (per catalog entry) | Pigafetta – Morga, plus an object corpus (Ayala Museum; Surigao Treasure). Density is status-graded; never on a slave-status figure. |
| I5 — Gold Necklace | `AdornmentI5GoldNecklace`, `:1048-1060` | Unscoped generic | Documented | High | Pigafetta 1521; Morga 1609; pre-1521 archaeology. "Leaders and the wealthiest only." |

### 6.2 The three rows this design elects

| Preset | Label | Block | Evidence tier | Components |
| --- | --- | --- | --- | --- |
| `Vis15` (`AppearancePresets.Visayan.cs:509-541`) | "Visayan Datu" | Visayan | Documented, form uncertain (weakest link: G1, H2) | B4, C3, D1 + I1, E3, G1, H2, I4, I5, K1 |
| `Tag13` (`AppearancePresets.Tagalog.cs:454-485`) | "Tagalog Chief" | Tagalog | Documented, form uncertain (weakest link: G2) | B4, C3, D3, E2, G2, I4, I5, K1 |
| `Tag15` (`AppearancePresets.Tagalog.cs:532-559`) | "Tagalog Leader" | Tagalog | Documented, form uncertain | B4, C3, and the row's own recipe |

### 6.3 What the sources refuse to support

These are load-bearing exclusions, not caveats. Each one is a thing a designer
would naturally reach for and each one is unavailable.

- **A badge, uniform, or insignia of military rank.** No source in the corpus
  gives one. Status in this period is displayed as wealth on a common base, not
  as a mark of appointment. The on-sprite chevron is an abstract interface glyph
  for exactly this reason and must never be drawn as an object the warrior is
  wearing or carrying.
- **A bodyguard ring, a command radius, or a circular formation around the
  leader.** Unsupported
  (`docs/research/battles/03-deep-past-formations-and-tactics.md:307`).
- **"Leaders fought in the front rank."** Unsupported. This design asserts
  nothing about where a leader stands, and adds no positional cue.
- **The C2 earned red head wrap of the proven warrior.** Deliberately unshipped
  (`AppearanceComponentCatalog.cs:210-214,445-447`) because it would require
  per-pawn kill-record state; the catalog's own instruction is that "C2 must
  ship in no roster". This design does not use it and does not add the state it
  would need.
- **`Bagani`.** Banned. Its earliest attestation is 1913, which fails the
  hundred-year rule in `CLAUDE.md` section 7 exactly as the panabas did. It is
  not used as a label, not used as a preset name, and not shipped behind a
  "provisional" badge.
- **A larger leader sprite.** Prohibited by the quoted rule above.
- **A Cagayan or generic-levy chief costume.** The Northern Luzon block ships no
  chief row on purpose (`AppearancePresets.NorthernLuzon.cs:47-53`). This design
  leaves the hole open and lets the marker and the inspector carry
  identification there rather than inventing a costume.

## 7. The nine acceptance questions

`SIMULATION-GAME-STANDARDS.md` section 10, lines 320-330, states: "Every feature
proposal states:". The nine items are quoted verbatim below, each with its
answer.

**1. "User-visible outcome"**

A spectator watching a battle can tell which warriors are leading their
contingents. At a distance, a leader carries a chevron above its head whose
shape — not merely whose colour — differs from every other pawn's outline. Up
close, in the Visayan and Tagalog blocks, a leader wears the block's chief kit:
gold-edged head wrap, gold earrings and necklace, richly dyed or draped cloth,
and for a Tagalog chief the red chinina. Clicking any warrior states in the
inspector whether it is currently leading, and names the appearance preset in
pair form with its evidence tier. No combat value changes.

**2. "Tick stage and state read/written"**

None. This is a presentation-only change and it lands in no tick stage. It reads
`AgentView.IsLeader` and `AgentView.ContingentId`/`ContingentState`, values the
simulation has already written and published, and writes nothing the simulation
can observe. `Hukbo.Core` is not edited.

**3. "Numeric units/bounds and same-tick conflict rule"**

The only numbers are screen pixels. `GetLeaderMarkBounds` moves from
`max(2, headWidth / 2)` by `max(1, headHeight / 6)` to `headWidth` by
`max(2, headHeight / 4)`, positioned above the head by the existing
`GetMarkGap`. Quad cost per leader pawn rises from one to three. There is no
same-tick conflict rule to state because presentation resolves no conflicts:
`AgentView.IsLeader` already has exactly one value per agent per tick, decided
by the simulation's own total ordering.

**4. "Total ordering and random-stream policy"**

No new random stream and no new salt. `SelectPreset` keeps mixing `entityId`
with the existing `PresentationSalts.AppearancePresetSelectionSalt` and keeps
walking a statically ordered, pre-built pool; `isLeader` only chooses which of
two statically ordered pools is walked. The leadership value itself carries the
simulation's total order — lowest `EntityId`, or highest `Rank` then lowest
`EntityId` under V5 and later — and position never enters it.

**5. "Cache source/invalidation or 'no cache'"**

One cache is affected and no cache is added. `PawnAppearanceCache` keeps its
single flat array of `Capacity = 2 * ArmyCompositionStepper.MaximumUnitsPerTeam`
entries, allocated once, never resized, never evicting. Its **key** gains
`isLeader`, becoming `(entityId, weapon, shield, isLeader)`. Its **invalidation**
clause changes from "none, because no key input can change during a battle" to
"by key comparison on every read": a leadership change on an ordinal produces a
miss, a recomputation from `PawnAppearanceFactory.Create` — still the single
authority — and an overwrite of that one slot. Lifetime is unchanged: one
battle, cleared on scenario reset, next round, and full reset.

**6. "Save/event/version effect or 'presentation only'"**

Presentation only. No snapshot field, no `BattleEventKind`, no
`MovementPresetId`, no preset version bump, no golden-expectation change, no
frozen-digest change.

**7. "Worst-case complexity and benchmark workload"**

`SelectPreset` remains O(pool size) with no allocation, and the pools it walks
are strictly smaller than today's single pool because the rows are partitioned
rather than duplicated. The render path adds at most two extra quads per living
leader, bounded by sixteen leader slots, so the worst case is +32 quads per
frame against a 200-agent frame that already submits thousands. The benchmark
workload is the canonical gate's own: 200 agents, 10,000 ticks, seed 1, run
headless — which exercises the simulation side and must reproduce its recorded
baseline unchanged. A 500-agent result is reported from
`./scripts/benchmark.ps1 -Agents 500`; because no simulation code changes, the
expected finding is no measurable movement, and any movement is a signal that
something crossed the boundary.

**8. "Spectator explanation: reason code, event, or inspector field"**

This is the question the whole feature exists to answer, so it gets a concrete
answer rather than a gesture.

A spectator discovers leadership through three independent channels, without
reading source code:

- *At battle zoom, no interaction.* The leader's chevron rises above the head
  and changes the pawn's outline. It is drawn at every detail tier including
  Low, and it occupies a slot the break-off band and the selection ring cannot
  overlap by construction, so it stays legible on a warrior that is
  simultaneously selected and breaking off.
- *At close zoom, no interaction.* In the Visayan and Tagalog blocks the leader
  wears the chief kit and its neighbours do not. In the Northern Luzon and
  generic-levy blocks it does not, and this design says so plainly rather than
  faking it — those two blocks rely on the first and third channels.
- *On click.* The inspector states leadership on the contingent row as
  `Contingent: 3 — Holding (leading)`, or on a standalone leadership row when
  the contingent row is suppressed, so the fact is never invisible. Below it the
  panel already prints the appearance preset's own display label, scope tag, and
  evidence tier — "Visayan Datu", scoped Visayan, **Documented, form uncertain**
  — read from the catalog rather than written into the panel.

There is no event and no reason code, because leadership is a standing condition
re-derived every tick rather than something that happens.

**9. "Tests that fail before implementation and pass afterward"**

Named per task in the plan document. The four that most directly encode the
feature are: a test that a leader in the Visayan block resolves
`appearance.presetVisayan.vis15` while the same entity as a non-leader does not;
a test that no leader row is reachable from the non-leader pool; a test that the
appearance cache returns the new appearance after a leadership flip on the same
ordinal; and a test that `BuildLowerLines` states leadership when
`ContingentState` is `None`. All four fail today.

### Acceptance conditions this feature cannot and need not meet

`SIMULATION-GAME-STANDARDS.md:332-333` requires "same-seed repeat, invariants,
golden replays, relevant save/resume, cold-cache equivalence, the 200-agent
contract, and a reported 500-agent result". Every one of those is a statement
about the simulation, and this feature changes none of it. The honest form of
the claim is: these conditions must all still hold *unchanged*, and the gate
proves that by reproducing the recorded seed-1 baseline. Cold-cache equivalence
does apply in a presentation sense, and it is covered by the cache test named
above.

## 8. Out of scope

Each exclusion below cites a live source. `docs/archives/` is deprecated by
definition and nothing in it is cited as justification anywhere in this
document.

| Excluded | Live source | Why |
| --- | --- | --- |
| Morale, fear, or rout, under any name | `CLAUDE.md` section 9 ("Do not ... Start terrain, pathfinding, morale ... before the gate that authorizes them") | The gate that would authorize it has not been passed, and the request did not ask for it. |
| Any combat effect of leadership — damage, reach, accuracy, aura, command radius | `CLAUDE.md` section 7; Morga 1609 as recorded in `docs/research/HISTORICAL_1500s_RANKS.md` | Distinction in war produced more followers, not a better fighter. A buff would contradict the source and change the state hash. |
| Booty, ransom, or reward economy | `docs/research/ARMY-COMPOSITION.md` section 11.4 | Campaign-layer concern; `Hukbo.Core` may not learn it, and `Hukbo.Campaign` does not exist. |
| Command signals of any kind — horn, gong, drum, flag, messenger | Unsupported across the source set | No evidence licenses depicting or simulating one. |
| Mid-battle allegiance switching | Not in the request; no supporting source | Would be a simulation change with a hash effect. |
| A new `ContingentState` value | `CLAUDE.md` section 5 (enum order and values are version-gated) | Would require a new preset version and new golden expectations. |
| A new `MovementPresetId`, `AgentState` field, or `BattleEventKind` | `CLAUDE.md` section 5 | Scope decision: this feature is achievable entirely in presentation. |
| Terrain, pathfinding, projectile ammunition, persistence migrations, multiplayer, mod APIs | `CLAUDE.md` section 9 | Gated. |
| A hosted CI workflow | `CLAUDE.md` section 4 ("There is no CI") | Verification is local and deliberate. |
| The battle report's "Leaderboard" | `src/Hukbo.Client/Presentation/BattleReportAccumulator.cs:375`; `src/Hukbo.Client/UI/BattleReportLayout.cs` | It is a kills top-ten and shares only a word with contingent leadership. Conflating the two would make both harder to read. |
| A leader sound cue | No slot exists; `.claude/skills/hukbo-sound-effects` | Leadership is a standing condition with no event to attach a cue to. |
| The C2 earned red head wrap | `AppearanceComponentCatalog.cs:210-214,445-447` | Needs per-pawn kill-record state; the catalog states "C2 must ship in no roster". |
| A leader mark on the inspector's small portrait | `src/Hukbo.Client/UI/AgentInspectorPanel.cs:146-150` | The portrait deliberately leaves no head space for a mark. The standalone leadership row is the intended substitute; revisiting the portrait would be its own design. |

## 9. Rejected alternatives

**Make the leader sprite larger, or scale it up.** Rejected outright.
`warrior-appearance-historical-research.md:964` says to depict leaders as denser
in gold and dye, *not larger in body*, and no source supports a size difference.
This is not a close call and it is the most obvious thing a reader would
otherwise propose.

**Give the leader a distinct outline colour or a faction-coloured rim.** A
colour-only cue is precisely what fails today. Changing the hue of an eight-by-
two pixel band to a different hue leaves the failure intact, and a full-pawn rim
would collide with the selection ring and the hit pulse, both of which already
own colour as their channel.

**Remove the elite rows from the rank-and-file pool as well as the leader rows,
so that gold appears only on leaders.** This would make the appearance channel a
perfect leadership signal, and it was seriously considered. It is rejected
because elite kit in the sources means wealth and prominence, not office —
Morga's gold-adorned, tattooed figures are prominent, and I1 tattoo coverage is
explicitly earned and cumulative rather than appointed
(`AppearanceComponentCatalog.cs:973-986`). Erasing the wealthy freeman from the
battlefield to sharpen a UI cue would trade a documented social distinction for
legibility that the on-sprite chevron supplies for free. This is flagged as a
decision the user may reasonably overrule; overruling it is a one-line change to
which statuses the general pool admits.

**Emit a `BattleEventKind` for leadership changes and let the event feed carry
it.** Rejected. Leadership is derived every tick and stored nowhere; there is no
transition the simulation currently computes, so an event would have to be
invented, which means a `Hukbo.Core` change, a new enum value, a preset version
bump, and new golden expectations — all to say something the spectator can
already see. It also collides directly with the two-hundred-event retention
bound in `CLAUDE.md` section 5.

**Add a leader field to `AgentState` so leadership is stored rather than
derived.** Rejected for the same reasons and one more: it would change the state
hash, and the derived-per-tick design is already correct and already cheap.

**Draw a plume, banner, standard, or helmet crest.** Rejected. Any of these is a
depicted object and therefore a historical claim, and no source in the corpus
gives a Philippine war leader of this period an insignia of military rank. An
abstract HUD glyph makes no claim, which is why the design uses one.

**Label the inspector row "Datu" or "Chief" instead of "leading".** Rejected,
and the existing code already documents why at
`AgentInspectorContent.cs:283-289`: the contingent-succession rule that elects
the leader is a Provisional reconstruction, so naming the holder a datu would
assert a historical rank the mechanic has not earned. The cultural label reaches
the player through the appearance preset's own catalog label, which carries its
evidence tier with it.

**Show leadership only in the inspector and skip the sprite work.** Rejected
because it fails the request. "So I can know that they are indeed leaders" is
about looking at the battle, and a fact only discoverable by clicking each of
two hundred warriors in turn is not discoverable at all.

## 10. Blocked work that would need a Core phase

Nothing in this design is blocked. Identification is fully achievable in
`Hukbo.Client` because the two values it needs — `AgentView.IsLeader` and
`AgentView.Rank` — are already published, already deterministic, and already
totally ordered.

Two adjacent things *would* need a Core phase, and neither is required by the
request. They are recorded here only so a future reader does not mistake their
absence for an oversight.

- **Any behavioural consequence of leadership** — a rally that a follower can be
  seen obeying, a leader-death effect, a succession event — is simulation work.
  It would need a new `MovementPresetId`, new golden expectations, and a
  re-recorded seed-1 baseline, and it is out of scope here on both the
  orchestrator's scope decision and the historical reading in section 2.
- **The C2 earned red head wrap**, and any other appearance that depends on what
  a warrior has done rather than on what it is, needs per-pawn kill-record state
  that `Hukbo.Core` does not carry and that presentation may not invent. The
  catalog already records this at `AppearanceComponentCatalog.cs:210-214`.

