# Warrior personal names — design

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Date: 2026-07-29

Status: implemented. This document is the design record for the naming
feature that ships in `Hukbo.Client`; the research it rests on is
[docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md](../../names/HISTORICAL_1500s_PERSONAL_NAMES.md).

## 1. What this adds

Every generated warrior now carries a personal name drawn from a
sixteenth-century regional corpus. A spectator meets that name in two places:

1. **The battle report.** The warrior leaderboard, the first-blood and
   decisive-kill highlights, the longest-survivor line, and each faction's top
   killer all name the warrior instead of printing a bare entity number.
2. **The agent inspector.** The top identity row shows the name, and a
   provenance block below it shows the evidence tier, the recorded spelling
   where it differs from the displayed one, the regional corpus the faction was
   assigned, the source document, what the source records about the bearer's or
   example's gender, and what is and is not being claimed by reusing the form.
3. **The event log.** Every row names its actor, the selected-event detail
   pane names it in full, and the text filter matches on a name.

The entity identifier is kept beside the name in every one of those places.
The name pools are far smaller than a roster, so two warriors in one faction
can honestly share a name; the identifier is what tells them apart, and it is
also what the event-log filter matches on.

## 2. Where the feature lives, and why

Names are **presentation-only**, in `Hukbo.Client`, on exactly the same footing
as `PawnAppearance`:

| Concern | Decision |
| --- | --- |
| Does `Hukbo.Core` store a name? | No. Nothing in the simulation knows a warrior has one. |
| Does a name reach the state hash or event hash? | No. It cannot: `Hukbo.Core` declares no project reference and cannot name a client type. |
| Does a name influence targeting, damage, retreat, or victory? | No. |
| Is a name stable across a replay? | Yes. It is a pure function of `EntityId`, `FactionId`, and `Scenario.Seed`. |
| Does the canonical gate's determinism workload change? | No. The headless runner draws no names at all. |

This is the decisive design choice. Putting names in `Hukbo.Core` would have
made the roster part of simulation state, would have required a new preset
version plus new golden expectations under `SIMULATION-GAME-STANDARDS.md` §4,
and would have bought nothing: no rule in the simulation has any use for a
name. Deriving the name in the client from identity the simulation already
publishes gives the same replay stability for free.

### Derivation

Two new salts join the registry in `PresentationSalts`, following the
one-salt-per-trait-stream rule:

- `WarriorNameRegionSalt` — mixed with `Scenario.Seed` and `FactionId`, chooses
  the whole faction's regional corpus, so every warrior under one banner shares
  one regional grammar.
- `WarriorNameSelectionSalt` — mixed with `EntityId`, chooses the form within
  that corpus.

Both run through the same SplitMix64 finalizer every other presentation-salt
consumer in this repository carries locally. Neither stream correlates with the
appearance, weapon-tint, or shield-skin draws for the same warrior.

### Why the region assignment is not the appearance block

`AppearancePresets.SelectBlock` already assigns a faction a `VisualScopeTag`
(Visayan, Tagalog, Cagayan, or unscoped-generic). Reusing that assignment for
names was considered and rejected: the appearance roster has a Cagayan block
for which the names research clears no corpus at all, and the name corpus has a
Mindanao River region for which the appearance roster has no block. A shared
draw would therefore have to either leave half the factions nameless or mix a
name from one region with clothing from another. The two assignments are
independent streams instead.

**Open question for the user:** a faction can currently draw Visayan clothing
and Tondo names in the same match. Tying the two together needs either a
Cagayan name corpus (new archival work) or a Mindanao appearance block (new
visual work). Recorded here rather than silently resolved.

## 3. What the corpus contains

Three regional pools, each a source dossier from one place and one decade,
never mixed:

| Region | Source | Forms |
| --- | --- | --- |
| Central Philippines, 1521 and 1565 | Pigafetta; Legazpi expedition relations | 20 |
| Tondo and Tagalog records, 1589 and 1604 | Conspiracy Against the Spaniards; Chirino | 20 |
| Mindanao River, 1579 | Ribera expedition records | 10 |

Every form is printed by one of the opened translations. Nothing is invented,
compounded from dictionary roots, or carried over from a modern name list.

## 4. What the corpus deliberately excludes

This implements the research's Approach B core — a region-scoped ledger of
recorded forms — and none of its optional layers. Each exclusion is pinned by a
test in `WarriorNameCatalogTests`, so a later edit that reintroduces one fails
the build rather than the review:

- **Famous historical bearers.** Lapulapu, Humabon, Zula, Colambu, Tupas,
  Sikatuna, Soliman, Magat Salamat, and Limasancay stay reference-only, so a
  roster never reads as a bag of copies of the same handful of figures.
- **Parenthood forms.** `Amanicalao`, `Amarlangagui`, and `Amaghicon` are
  recorded, but a parenthood name refers to a specific firstborn and may only
  be generated when that child exists. A battle roster has no family tree.
- **Titles.** `Datu`, `Raja`, `Gat`, `Lakan`, and `Dayang` encode standing and
  are never prefixed to an ordinary warrior.
- **Reputation and friendship names.** Colin's 1663 `Pamagat` and `Casolasi`
  material is later comparison only.
- **Christian-plus-local forms.** Those belong to a dated contact context, and
  a scenario carries no date yet. The Spanish first names printed beside the
  Tondo elements are not reproduced.
- **Place names.** Settlements that appear beside chiefs in the same passage
  never leak into a personal-name pool.
- **Uncleared traditions.** Kalantiaw, the 1907 *Maragtas* cast, Urduja, and
  the folkloric Humamay are excluded however widely they circulate.

## 5. Honesty surfaces

Three things stay visible rather than being smoothed over.

**Procedural reuse is itself a reconstruction.** Most forms name a particular
recorded person. Lending that person's name to a generated warrior is the
reconstruction, not the attestation, and every entry's reuse note says so in
the inspector.

**The record is a record of elites and defendants.** The pools come from
narratives of chiefs, envoys, interpreters, and people already in trouble with
Spanish institutions, because that is who colonial records name. The inspector
names the source document for every warrior, so a spectator can see what kind
of document it is.

**Women's names are almost absent.** The opened sixteenth-century sources
record no local birth name for any woman. `Iloguin`, from Chirino in 1604, is
the earliest explicit example available, and the research forbids manufacturing
more by appending a suffix to men's names. A standalone inspector note records
that gap; the catalog records the gender the source states and never claims a
form was restricted to one gender.

## 5a. The event log's two constraints

The event log is the one surface where naming a warrior ran into real limits,
and both were resolved without touching `Hukbo.Core`.

**A target keeps its bare identifier.** `BattleEvent` carries the actor's
faction but not the target's, and the faction is what selects the regional name
corpus. Adding a target faction to the event would change the authoritative
event record, and therefore the event hash, for a cosmetic label — which the
determinism contract does not permit. A line reads
`Blue Salonga #7 hit #12's shoulder`; when `#12` acts, it is named in its own
row.

**The row column holds about fifteen characters.** It cannot fit faction, name,
and identifier together, so the row shows `Salonga #7` and drops the faction
word. Nothing is lost: the row already draws that text in the faction's own
color, the detail pane below prints both a full `Source:` line with the faction
and a separate `Faction:` line, and the battle report names the faction on
every line. A test pins the row label at fifteen characters or fewer for every
shipped name across a five-hundred-warrior roster, so a name is never drawn
truncated mid-word.

The feed's text filter searches the drawn line plus the older
faction-and-identifier form of the same actor, so both a name query and a
`blue #7` query still match.

## 6. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` §10 asks whether a spectator can discover the
effect without reading source code. They can: the name appears on every
leaderboard row and highlight line in the battle report, and selecting any
warrior shows the name and its full provenance in the inspector. Nothing about
the feature is inferable only from the source.

## 7. Verification

- `dotnet build Hukbo.slnx -c Debug` — succeeded, 0 warnings, 0 errors.
- `dotnet test tests/Hukbo.Client.Tests` — 2,627 passed, 0 failed.
- The canonical gate, `./scripts/verify.ps1`, is the integration evidence and
  is recorded in the implementation plan alongside its real output.

## 8. Follow-up work not done here

- A log line's target stays `#<id>`, for the reason in section 5a. Naming it
  would need either a target faction on `BattleEvent` — a determinism change —
  or a client-side entity-to-faction lookup threaded into the feed.
- The match summary panel names no individual warrior and needed no change.
- The regional-coherence question in section 2 is open.
- The research's own remaining work list (section 11 of the research document)
  is unchanged: per-row page anchors, the 1613 Tagalog dictionary check,
  specialist review of every regional pack, and archival work on women's names.
  This implementation does not close any of those; it ships what the research
  already cleared and labels the rest where a spectator can see it.
