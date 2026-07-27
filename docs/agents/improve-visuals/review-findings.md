# Independent Review — Hukbo Visual Improvement Planning Package

Review date: 2026-07-28. Reviewer: independent review agent (did not author any
reviewed document). Scope: all seven documents under
`docs/plans/improve-visuals/`, both agent documents under
`docs/agents/improve-visuals/`, and all three research documents under
`docs/research/improve-visuals/`. Ground truth for code claims was the source
tree on disk (verified via direct reads and searches, cited per finding);
ground truth for policy was `CLAUDE.md` sections 5 through 7 and
`SIMULATION-GAME-STANDARDS.md` sections 4 and 10.

This document reports findings only. It edits no reviewed document and
authorizes nothing.

## Findings table

| ID | Severity | Document(s) | Description | Evidence | Proposed resolution |
| --- | --- | --- | --- | --- | --- |
| RF-01 | **Critical** | README.md; requirements.md (R-W6.15); visual-system-integration-design.md; implementation-plan-draft.md (milestone intro, VIS-038, VIS-045, VIS-047); existing-code-analysis.md §9 | The package repeatedly names `0x5BEBA7A68F69BE0D` as "the seed-1 state hash" the canonical gate must reproduce. That value is not the gate workload's hash. It is the terminal hash of the **zero-interception control run** in `DeterminismTests` — preset V1, `ClashProfile.Neutral`, computed against a frozen pre-clash content hash. The current gate-workload oracle is `stateHash 27DC94C6E9A01E35` / `eventHash 372C9217E5CB8BE9`. | `tests/Hukbo.Core.Tests/DeterminismTests.cs:33` (`PreClashTerminalStateHash`), `:725-748` (`CreateZeroInterceptionControlRun` pins V1 + neutral clash), `:706-722`; current oracle in `docs/development/testing.md:399-417` ("The Phase 2 reference pair") | See Critical detail below — six concrete edits. |
| RF-02 | **High** | warrior-appearance-design.md (Minimum differentiation criterion + preset roster); implementation-plan-draft.md (VIS-039) | The shipped 53-preset roster fails its own automated pairwise differentiation criterion. Three cross-block pairs are recipe-identical and at least five more differ in only one countable category, so the VIS-039 suite as specified would fail on the design's own content. | Identical recipes: VIS-01 ≡ LEV-01 (`B1,C5,D1,E1,G2,K1`), TAG-05 ≡ LUZ-05 (`B1,C5,D1,E1,G2,K2`), LEV-02 ≡ LUZ-06 (`B3,C5,D1,E1,G3,K1`). One-category pairs: VIS-04 vs TAG-06 (I2 only), VIS-01 vs TAG-05 (K only), LEV-01 vs TAG-05 (K only), TAG-09 vs LEV-02 (K only), LUZ-05 vs VIS-01 (K only) | See High detail below — amend the criterion's scope or revise the colliding rows. |
| RF-03 | **High** | warrior-appearance-design.md (roster rows VIS-18, TAG-14; "Regional grouping and the prohibitions" rule 6); implementation-plan-draft.md (VIS-020, VIS-021 automated verification) | Two roster rows carry gold components on rows not marked elite, violating the design's own prohibition-6 encoding and the research's "E2 elite presets only" rule. The VIS-020/VIS-021 test "gold only on elite-marked rows" would fail on the design's own tables. | VIS-18 (`…D4 + I4, E2…`, scope column plain "Visayan") and TAG-14 (`…D2, E2, I4…`, scope plain "Tagalog"); design rule 6 permits gold only on elite/chief/leader rows or single-accent I4 on "prosperous-freeman" rows — no row carries that marker; research E2: "Must not generalize: elite presets only" | See High detail below — retier or strip the two rows. |
| RF-04 | Medium | requirements.md (R-W4.8); battlefield-environment-design.md; implementation-plan-draft.md (inconsistency 1, VIS-029) | Planner-flagged inconsistency 1 **confirmed**: R-W4.8 marks dust MUST; the battlefield design scopes it MAY "per orchestrator direction" and discloses the deviation. | requirements.md R-W4.8 ("MUST"); battlefield design "Dust and disturbed vegetation (optional scope — MAY)" and its Open decisions entry | Resolve at user review: either amend R-W4.8 to SHOULD/MAY with a dated user-approval note in requirements.md, or confirm dust as mandatory and unblock VIS-029. The requirement document must be edited either way so the two documents agree before implementation. |
| RF-05 | Medium | visual-system-integration-design.md §2; weapon-visuals-design.md; shield-visuals-design.md; implementation-plan-draft.md (inconsistency 2, VIS-002) | Planner-flagged inconsistency 2 **confirmed**: the integration design specifies lowercase three-segment IDs (`shield.tallhardwood.s1`); the weapon design mints four-segment camelCase IDs (`weapon.kampilan.tint.freshIron`) and the shield design camelCase segments (`shield.tallHardwood.mactanThin`). | Integration design "Identifier naming convention" paragraph vs the sibling designs' ID tables | Accept the planner default: adopt the sibling designs' concrete camelCase IDs with an explicit optional `tint.` sub-segment, and amend the integration design's naming paragraph (one edit, marked post-decision) so the VIS-002 grammar regex matches the shipped tables. Record the decision in VIS-046. |
| RF-06 | Medium | battlefield-environment-design.md (Ground shading); requirements.md (R-W6.2); implementation-plan-draft.md (inconsistency 3, VIS-027) | Planner-flagged inconsistency 3 **confirmed**: the battlefield design hashes the corner lattice "with the scenario seed and the existing plains salt", contradicting R-W6.2 / integration design §3 ("new salts never reuse … the plains salt"). | Battlefield design Ground shading, first bullet; requirements R-W6.2; the real plains salt is `PlainsBackdropGeometry.cs:107` (`0x504C41494E530001`) | Accept the planner default: a new named corner-lattice salt registered in VIS-001. Edit the battlefield design's ground-shading bullet to say "a new named salt", keeping the pin that existing decal placement (still under the old salt) is unchanged. |
| RF-07 | Medium | warrior-appearance-historical-research.md (Category I tally); requirements.md (R-W3.1); warrior-appearance-design.md; implementation-plan-draft.md (inconsistency 4, VIS-019) | Planner-flagged inconsistency 4 **confirmed, and deeper than the planner states**: the research's own tally line "Eight adornment options (three renderable at pawn scale, five as inspector texture)" does not match its own option annotations. By option count, four render (I1, I2, I4, I5) and four are inspector-only (I3, I6, I7, I8). The planner's "I1/I2 count as one channel" reading fixes "three renderable" but leaves "five as inspector texture" wrong under either counting. | Research Category I: I1 "Safe to depict: … skin-tone shift", I2 "the I1 tone shift applied to arms", I4 "a single gold pixel", I5 "a gold accent line"; I3/I6/I7/I8 inspector-only; closing tally line | VIS-019's resolution task should correct the **research tally line** (a factual self-count, not a historical claim) to "four renderable (I1, I2, I4, I5; the two tattoo options share one tone-shift channel), four as inspector texture", and amend R-W3.1's "(8, three renderable)" to match. Record the reading in the catalog doc comment as planned. |
| RF-08 | Medium | shield-visuals-design.md (skin table, proportion envelope); requirements.md (R-W2.1) | The shield design adds a fourth difference channel R-W2.1 does not authorize: per-skin **proportion deltas** ("proportion at the tall end of the shared envelope" for S2, "narrowest proportion" for S5). R-W2.1 limits skins to face tone, a rattan-binding accent line, and slight outline curvature. Unlike the dust deviation, this one is not flagged anywhere. Proportion variation is also the channel closest to the false-cause hazard R-X.12 guards. | Shield design skin table rows S2/S5 and "Proportion envelope" paragraph; requirements R-W2.1 | Either (a) amend R-W2.1 to add "and per-skin proportion deltas of a few layout pixels inside one shared aspect-ratio band, footprint never below the current Low-tier block" as a user-approved requirement edit, or (b) drop the per-skin proportion deltas and differentiate S2/S5 by tone and accent only. The design must at minimum surface the deviation the way the battlefield design surfaced dust. |
| RF-09 | Medium | implementation-plan-draft.md (VIS-032 rollback and step 2) | VIS-032's rollback claim misstates the store's actual behavior. The current load path **rejects the whole file** when `SchemaVersion` is not exactly the supported value (pattern match `SchemaVersion: SupportedSchemaVersion`), returning defaults and losing the saved theme. A reverted schema-3 build reading a version-4 file therefore does not "still load … theme preserved". Separately, the new schema-4 load path must explicitly relax that strict equality to accept version-3 files — a change the task steps never call out. | `src/Hukbo.Client/Settings/ClientSettingsStore.cs:59-93` (schema mismatch → `Default(defaultThemeId)`); `:14` (`SupportedSchemaVersion = 3`) | Rewrite the VIS-032 rollback paragraph honestly ("reverting to schema-3 code discards a version-4 file to defaults; the theme is re-selected once") and add an explicit implementation step: change the schema check from strict equality to an accepted-version set {3, 4} with a version-3 migration path, covered by the already-planned migration test. |
| RF-10 | Medium | requirements.md (R-W4.9); battlefield-environment-design.md; implementation-plan-draft.md (VIS-025, coverage map) | R-W4.9 requires the ground be "labelled **Provisional reconstruction** in metadata", and VIS-025 repeats the phrase, but no task creates any backdrop metadata artifact — there is no backdrop catalog task, and the `backdrop.*` identifier domain minted by the integration design's naming convention is never populated. The obligation has no implementing mechanism or home. | VIS-025 file list (`GrassGeometry.cs`, `ArenaGame.cs`, tests — no catalog/metadata file); integration design §2 mints `backdrop.grass.cluster` as an example; coverage map maps R-W4.9 to "VIS-025 (metadata framing), VIS-046" | Name the mechanism in VIS-025: either a minimal one-entry backdrop metadata declaration (reusing the VIS-002 entry shape) whose tier is Provisional reconstruction, or amend R-W4.9 to its honest minimum ("no player-facing text names vegetation, region, or land use; the provisional framing is recorded in the design document and code doc comments"), user-approved. |
| RF-11 | Medium | requirements.md (OD-8, R-W4.8); battlefield-environment-design.md; implementation-plan-draft.md (VIS-029, VIS-032) | If dust ships as MUST (RF-04 resolved that way), the reduced-motion setting's relationship to dust is undefined. Dust puffs are ambient motion by the package's own clock-classification, yet `MotionIntensity` gates only grass sway; OD-8's "future ambient motion" wording does not cover motion shipping in this same pass. | R-W5.8 and VIS-031 gate sway only; VIS-029 lists no MotionIntensity input; OD-8 text ("only grass sway or … future ambient motion") | When RF-04 resolves, state explicitly in VIS-029 and the OD-8 decision record whether `MotionIntensity` Off suppresses dust spawning (recommended: yes at Off, unchanged at Reduced), and add the truth-table row to VIS-031 if so. |
| RF-12 | Low | warrior-appearance-design.md (TAG-13 row) | TAG-13's tier is marked `D` (Documented) but its recipe contains G2 (Cloth Belt), which the research tiers "Documented (visually …), form uncertain" — the weakest-link rule the design itself declares yields DFU. | Warrior design evidence section ("A preset's evidence tier is the weakest tier among its rendered components"); research Category G2 tier | Change TAG-13's tier cell to DFU, or explicitly exclude the belt/sash line from the weakest-link computation alongside K and record why. |
| RF-13 | Low | requirements.md (R-X.6); warrior-appearance-historical-research.md (I6) | Classification mismatch on *kolombiga*: the research says the term "clears the bar via Morga 1609" and reserves the pair form for the inspector pending spelling review; R-X.6 lists it among terms "whose attestation is PENDING verification". The requirement is more conservative than its evidence source, without saying so. | Research I6; R-X.6 pending list | Harmless in direction (conservative), but record the reading: annotate R-X.6's kolombiga entry "attestation clears the bar per the research; held pending spelling review only". |
| RF-14 | Low | README.md (document map) | The document map still lists four design documents as "Being authored in parallel" although all four exist and are complete. The package is presented for user review with a stale state table. | README table rows for weapon/shield/warrior/battlefield designs | Update the four status cells to "Written" before the user review; do not wait for VIS-046. |
| RF-15 | Low | implementation-plan-draft.md (VIS-035) | The pre-integration baseline "on the pre-package commit" requires the instrumentation seam and probe, which are themselves package deliverables. The procedure works only via a branch containing solely the seam and probe cut from the pre-package commit, which the task does not spell out. | VIS-035 dependencies ("the baseline run must execute on a build without the package's visual changes") vs its file list (seam in `ArenaGame.cs`) | Add one sentence to VIS-035: the baseline is taken on a branch off the pre-package commit carrying only the probe and the opt-in seam, and the branch's diff is recorded with the baseline JSON. |
| RF-16 | Low | implementation-plan-draft.md (VIS-013) | With a skin-stream modulus of 1, every shielded pawn selects S1 at the milestone, so the acceptance wording "the only visual change is the S1 face tone on skin-selected pawns" understates a global change: all shields shift from charred wood to pale palm-wood and the `default` entry is unreachable by the stream. | VIS-013 step 2 ("modulo the shipped skin count (1 at the milestone)") and acceptance criteria | Reword the acceptance line ("every shielded pawn shows the S1 tone at the milestone; the default entry is the fallback target, not a rolled skin") so the milestone human review is not surprised by a fleet-wide tone change. |
| RF-17 | Low | warrior-appearance-design.md (roster preamble) | "LEV-01 falls back to the diagnostic placeholder chain" reads as skipping chain step 3 (the model-category default — today's three-trait drawing), which the integration design guarantees always exists. | Warrior design fallback sentence vs integration design §4 step 3 ("the unadorned torso … exactly today's drawables") | Reword to "LEV-01 falls back through the full resolution chain (model-category default, then the diagnostic placeholder)". |
| RF-18 | Low | warrior-appearance-design.md; battlefield-environment-design.md; implementation-plan-draft.md (VIS-046) | Three open decisions carry no identifier: block-assignment breadth (warrior design), the trample `Attack` feed, and the sway wave shape (battlefield design). VIS-046 must record "every open decision's outcome", which is fragile for unnamed decisions. | The named lists in VIS-046 vs the designs' Open decisions sections | Assign IDs (e.g. OD-W3-a, OD-W4-a, OD-W4-b) in the owning designs so the VIS-046 decision record has stable keys. |

Totals: 1 Critical, 2 High, 8 Medium, 7 Low.

## Critical detail

### RF-01 — The package names the wrong seed-1 oracle hash

Every statement of the determinism-neutrality contract in the package quotes
`0x5BEBA7A68F69BE0D` as the seed-1 state hash the gate must leave unchanged.
Verified against source, that constant is `PreClashTerminalStateHash` in
`tests/Hukbo.Core.Tests/DeterminismTests.cs:33`, and the run that produces it
(`CreateZeroInterceptionControlRun`, lines 725-748) is **not** the gate
workload: it pins `CombatPresetId.PrecolonialPhilippinesV1` and swaps in
`ClashProfile.Neutral`, and the hash is computed against the frozen literal
`PreClashContentHash`. The canonical gate (`scripts/verify.ps1`) runs
`benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` on the **current default
preset (V2, clash enabled)**, and the current recorded reference pair for that
workload is `stateHash 27DC94C6E9A01E35` / `eventHash 372C9217E5CB8BE9`
(`docs/development/testing.md`, "The Phase 2 reference pair", measured at
commit `cffbb6c`, explicitly the values "a Phase 4 run of the same workload
must reproduce").

Why this is Critical rather than High: VIS-045 and VIS-047 are the package's
terminal verification tasks, and both instruct the integrator to confirm "the
state hash `0x5BEBA7A68F69BE0D` … unchanged" in the gate output. The gate will
never print that value. An integrator following the plan verbatim either
reports a false failure, or — the genuinely dangerous branch — concludes a
baseline moved and starts a re-baselining investigation against the wrong
oracle. The error originates in `existing-code-analysis.md` §9 (which describes
the constant as "the recorded seed-1 terminal state hash" without its
zero-interception/V1/pre-clash qualifiers) and propagated into every downstream
document.

Concrete edits required:

1. `docs/agents/improve-visuals/existing-code-analysis.md` §9 — describe
   `0x5BEBA7A68F69BE0D` correctly as the zero-interception V1 control-run
   golden, and add the Phase 2 reference pair (`27DC94C6E9A01E35` /
   `372C9217E5CB8BE9`, testing.md) as the current gate-workload oracle.
2. `docs/agents/improve-visuals/requirements.md` R-W6.15 — replace the quoted
   hash with the reference-pair citation, or drop the literal and say "the
   recorded seed-1 reference pair in `docs/development/testing.md`".
3. `docs/plans/improve-visuals/README.md` boundary 1 — same replacement.
4. `docs/plans/improve-visuals/visual-system-integration-design.md` — testing
   strategy leg 3 and acceptance criterion 1: same replacement.
5. `docs/plans/improve-visuals/implementation-plan-draft.md` — milestone
   introduction, VIS-038, VIS-045, VIS-047: replace the literal with "the
   recorded seed-1 reference pair (state `27DC94C6E9A01E35`, event
   `372C9217E5CB8BE9`, testing.md Phase 2 reference)" and note that the
   `DeterminismTests` control-run golden is a separate, additional guard that
   must also stay green.
6. Wherever "event hash" is claimed unchanged, name the event-hash oracle
   explicitly (`372C9217E5CB8BE9`) rather than leaving only the wrong state
   hash as the quoted anchor.

## High detail

### RF-02 — The preset roster fails its own differentiation test

The warrior design defines a mechanical pairwise criterion ("for every
unordered pair of shipped presets, either … one silhouette-affecting category,
or … at least two countable categories") and states the test runs "over
exactly this list" — all 53 presets, which includes cross-block pairs. The
tabled roster contains three recipe-identical cross-block pairs
(VIS-01 ≡ LEV-01, TAG-05 ≡ LUZ-05, LEV-02 ≡ LUZ-06 — the levy preamble fixes
all LEV rows to D1/E1, making their recipes complete) and at least five pairs
differing in exactly one countable category (VIS-04 vs TAG-06 differ only in
I2; VIS-01 vs TAG-05, LEV-01 vs TAG-05, TAG-09 vs LEV-02, and LUZ-05 vs VIS-01
differ only in K). VIS-039's "full pairwise differentiation" suite therefore
fails on the design's own shipped content — an acceptance criterion that
cannot pass as written.

Proposed resolution (one of the two, decided at user review, edited into
`warrior-appearance-design.md` "Minimum differentiation criterion" and the
roster tables, and mirrored in VIS-039):

- **(a) Scope the pairwise test within a regional block**, and justify it in
  the design: cross-block near-duplicates are historically honest (a plain
  bare-chested spearman looked similar everywhere; the scope tag is the
  differing claim) and the blocks never co-exist inside one faction's army in
  one match (block assignment gives each faction one block). If chosen, the
  criterion paragraph must say "within each block" explicitly and the identical
  triplets remain acceptable.
- **(b) Keep the global criterion and revise the colliding rows** so every
  cross-block pair clears it (for example, give LUZ-05 a G3 cord belt and K5,
  give TAG-05 a G3, differentiate TAG-06 from VIS-04 by lower-garment or
  sash) — about six row edits.

Option (a) is the smaller, more defensible change; option (b) preserves the
stronger "no two shipped presets are near-twins" property. Either way the
design text and VIS-039 must agree before implementation.

### RF-03 — Gold on rows the gold rule forbids

The design's own enforcement text (Regional grouping, rule 6) restricts gold
components (C3, I4, I5, E2's gold pixel) to rows whose scope column marks
elite, chief, or leader, with a carve-out for "single-accent I4 only" on
"prosperous-freeman" rows — a marker no row in the tables carries. Two rows
violate it:

- **VIS-18** — scope column plain "Visayan", recipe `D4 + I4, E2, G1` (gold
  earring **and** gold-edged dyed bahag).
- **TAG-14** — scope column plain "Tagalog", recipe `D2, E2, I4` (same two
  gold components).

The research is explicit that E2 is "elite presets only", and prohibition 6
("gold ensemble on low-status presets") plus co-occurrence rule 3 back the
design's rule. The automated checks named in VIS-020/VIS-021 ("gold only on
elite-marked rows") would fail on these rows as tabled.

Proposed resolution, edited into the two roster rows in
`warrior-appearance-design.md`:

- Mark VIS-18 and TAG-14 with an explicit "prosperous-freeman" scope marker
  **and** reduce each to the single I4 accent (replace E2 with E1), which the
  design's own carve-out then permits; or
- Promote both rows to elite scope as-is; or
- Delete both rows (roster drops to 51, still above the 50 floor).

The first option preserves the intended mid-status texture with the least
change and keeps the two red/gold status systems clean. Whichever is chosen,
the "prosperous-freeman" marker must actually appear in the table if the
carve-out is to be testable.

## Medium detail

**RF-04 (dust MUST/MAY).** Confirmed exactly as the planner reported. The
deviation is honestly surfaced in the battlefield design and the plan blocks
VIS-029 on it. The missing piece is that the *requirements document* is the
authority (its own header says a dropped MUST "needs the user's explicit
approval"), so the resolution must land as an edit to R-W4.8 with a dated
approval note, or as an unblocking of VIS-029 — silence in requirements.md
after the decision would leave the contradiction alive. Note also that
R-W4.4's dust cap and R-W4.2's dust shade obligations become conditional on
the same decision and should be annotated in the same edit.

**RF-05 (identifier grammar).** Confirmed. The planner's default (adopt the
sibling designs' camelCase IDs, permit a `tint.` sub-segment, amend the
integration design's paragraph) is the right call because the sibling tables
are the reviewed content and the IDs are pinned forever at VIS-002. The
integration design edit must be marked as a post-decision amendment per its
own change discipline.

**RF-06 (ground-shading salt).** Confirmed; the battlefield design's wording
directly contradicts R-W6.2. The planner's default (new named corner-lattice
salt, registered in VIS-001, decal placement pinned unchanged under the old
salt) is correct — corner-averaged shading changes every ground shade anyway,
so reusing the plains salt buys zero continuity and breaks the uniform rule.
One additional edit: the battlefield design's Ground shading bullet should be
corrected, not just overridden by the plan, so no future reader implements
from the design text.

**RF-07 (adornment count).** Confirmed, with the added fact that the research
document's own tally line is internally inconsistent (see table). The
resolution belongs in VIS-019 as planned, but must touch the research tally
line and R-W3.1, not only a catalog doc comment — otherwise the "counts match
the research" test in VIS-019's verification is written against a wrong
number.

**RF-08 (shield proportion deltas).** The only unflagged requirement deviation
found in the package. It is small and bounded, but it is precisely the channel
R-X.12's false-cause rationale worries about (a "narrowest" skin reads as less
coverage), and R-W2.1's list is exhaustive by construction ("expressed only
as"). It needs the same treatment dust got: surface it, and let the user amend
the requirement or shrink the design.

**RF-09 (settings rollback).** The claim contradicts the verified load path:
schema mismatch rejects the whole file, theme included
(`ClientSettingsStore.cs:59-93`). The task's happy path (bump to 4, load 3) is
fine, but the step list should name the concrete code change (strict equality
becomes an accepted-version set) because the "GoreIntensity precedent copied
end to end" framing hides the one place the precedent does not transfer — the
precedent added a field within a schema version; this task changes the version
itself for the first time.

**RF-10 (backdrop metadata home).** R-W4.9's "labelled Provisional
reconstruction in metadata" is currently an obligation with no artifact. Either
give it one (a single backdrop metadata entry through the VIS-002 shape) or
amend the requirement to the honest minimum its own discoverability note
already concedes ("at minimum, no player-facing claim is made"). Both are
small; what is not acceptable is shipping with the requirement satisfied by a
phrase in a design document nobody wires to anything.

**RF-11 (reduced motion vs dust).** Only material if dust ships. Recommend
deciding it inside the RF-04 resolution so the setting's player-facing caption
(VIS-032, OD-8 wording) is written once, correctly.

## Low detail

RF-12 through RF-18 are listed fully in the table; each is a one-to-three-line
edit in the named document. None blocks approval on its own, but RF-14 (stale
README statuses) should be fixed before the user reads the package, since the
README is the entry point and currently misdescribes four of its seven
documents.

## Verification record

Checks performed with results, beyond the findings above.

**Code claims verified accurate** (direct source reads): the three
`PawnAppearanceFactory` salts (`0xA0761D6478BD642F`, `0xE7037ED1A0B428DB`,
`0x8EBC6AF09C88C6E3`, `PawnAppearanceFactory.cs:27-29`); the plains salt
`0x504C41494E530001` and `MaximumBackdropInterpolation = 0.22f`
(`PlainsBackdropGeometry.cs:107, 80`); `MaximumDecalCount = 256`; enum pins
`Kampilan=1, Wasay=2, Kalis=3, Itak=4`, `ShieldId.None=1, TallHardwood=2`
(`CombatIdentity.cs`); camera zoom 0.05–12 (`SpectatorCamera.cs:9-10`);
apparent-scale clamp 0.72/2.40, zoom scale 1.35, tier thresholds 0.95/1.80
(`PawnGeometry.cs:65-69`); weapon draw constants Itak 0.30/2.1, Kampilan
0.22/2.45, Wasay 0.28/2.9, Kalis 0.16/1.5 (`PawnRenderer.cs:427-461`); fixed
faction colors (64,164,255 / 255,91,105 / 231,199,84,
`FactionColorPalette.cs:15-17`); `SupportedSchemaVersion = 3` and the
GoreIntensity independent-validation shape (`ClientSettingsStore.cs`);
`GoreIntensity.Off = 0` with pinned values; `WeaponEvidenceTier` on
`PawnAppearance`; the seven existing `assets.*` `LogEvents` constants; the
four hand-run `tools/` harnesses (CueDemand, MixAnalysis, VoiceStress,
WeaponBalance); `AgentInspectorContent.cs` / `AgentInspectorPanel.cs` exist as
named; `BattleEvent` carries entity ids, not positions (trample/dust must take
positions from the agents snapshot at ingest, as `HitEffectSystem` already
does — the designs are compatible with this, no finding).

**Skins-as-gameplay sweep (scope item 2):** no task or design adds a
`WeaponId`, `ShieldId`, or any Core state; the false-cause rule is applied
correctly and repeatedly (visual reach pinned identical across weapon tints;
smaller shield skins forbidden; S4/S6/S7 correctly quarantined as
future-mechanics flags). The only soft spot is RF-08's proportion deltas.

**Determinism sweep (scope item 3):** no `System.Random`, `GetHashCode`,
iteration-order, or wall-clock variation source anywhere in the designs; the
sway clock is a client frame-time accumulator with the exact-zero off path;
trample/dust are bounded event-fed pools; the salt-reuse hazard is exactly the
planner's inconsistency 3 (RF-06) and nothing beyond it. Block assignment
mixes `FactionId` into a salted SplitMix64 stream — sound.

**Fallback sweep (scope item 4):** weapon, shield, and appearance chains all
terminate in the conspicuous placeholder with once-per-identifier `warn`
diagnostics through a bounded seen-set; totality tests are specified. The
backdrop domain is the one gap (RF-10). Missing-asset diagnostics are fully
specified (VIS-004) against the enforced `LogEvents` rules.

**Licensing sweep (scope item 5):** clean. All three research registers mark
every external item reference-only, the Boxer Codex per-file verification
caveat is carried, the Met CC0 caveat is carried, and R-X.15 forbids
copying/tracing. No design treats any source image as an importable asset.

**Bounded-allocation sweep (scope item 6):** every cap is a named constant
with a test (320/4 grass, 128 trample, 32 dust, 64 seen-set, 256 decals, 48x48
grid, 2-accent/2-pixel caps, amplitude/frequency bounds); steady-state
zero-allocation is stated for every per-frame path and the harness verifies
via GC counters; the submission-counting seam (VIS-034) covers creep.

**Readability sweep (scope item 7):** the faction > weapon role > shield >
state > clothing order is preserved consistently — protected Low-tier set,
sub-threshold-at-Low rules, dye-palette faction-distance floor, grass under
pawns, border margin, and the manual rows to judge the result.

**Accessibility sweep (scope item 8):** motion-off is exact `Vector2.Zero` by
test; high-contrast forces amplitude 0 and minimal shade spread; color-blind
handling is honestly scoped as a no-regression floor with OD-7 tracking the
real marker, and the VIS-033 manual row wording is honest about what a
typical-vision reviewer can judge. RF-11 is the one open edge.

**Acceptance-criteria spot check (scope item 9):** fourteen tasks checked
(VIS-001 through VIS-006, VIS-010, VIS-015, VIS-018, VIS-026, VIS-032,
VIS-035, VIS-036, VIS-041/042). All carry objective, testable acceptance
criteria; the two weakest are VIS-026's "asserted by code review against the
diff" (acceptable, concrete artifact) and VIS-029's placeholder row wording
(explicitly deferred until unblocked). The milestone's five levy presets do
pass the pairwise criterion among themselves — RF-02 bites only at full
roster.

**Coverage-map spot check (scope item 11):** ten MUSTs traced — R-X.4,
R-X.6, R-W1.7, R-W2.2, R-W3.4, R-W3.8, R-W4.1, R-W4.3, R-W5.5, R-W6.5,
R-W6.13 — all map to tasks whose steps and verification genuinely satisfy
them. The map's two self-declared conditional rows (R-W4.8, R-W3.10) are
stated honestly. The "Uncovered MUST requirements: none" claim held under
spot check, with the caveat that R-W4.9's mapping is weak (RF-10) and R-W3.4's
mapped suite currently cannot pass (RF-02).

**Honesty checks (scope item 12):** all pass. Manual rows are created
PENDING-only with human-only flips stated at every occurrence; every budget
number carries ESTIMATE; no CI is proposed anywhere (explicitly rejected
twice); the gate is never delegated (VIS-045/047 state it in bold); all seven
plan/design documents carry the non-authorization statement. All five design
documents have the identical twelve H2 sections in identical order (Status,
Scope, Current state, Evidence, Requirements, Alternatives considered,
Recommended approach, Rejected approaches, Dependencies, Risks, Open
decisions, Acceptance criteria) plus the title and the closing
non-authorization line; if the working template counts thirteen sections, the
thirteenth is the non-authorization statement, present in all five — there is
no ordering or structural drift between documents.

**Historical-generalization sweep (scope item 1):** no pan-archipelagic
mashups found in the designs; the block structure, scope tags, C6/tattoo/red
confinements, and excluded forms (W2, K2 pommels, S8–S11, panabas, salakot,
taming) all match the research. Inspiration tags name places and times. The
gold-placement violations (RF-03) and the TAG-13 tier slip (RF-12) are the
only points where the roster tables drift from the research's rules.

## Verdict

The package is unusually disciplined: the requirements are genuinely testable,
the designs stay inside the historical evidence with the discipline the policy
demands, the plan's 47 tasks carry real verification, and the honesty
machinery (PENDING rows, ESTIMATE budgets, non-delegated gate, disclosed
deviations) is intact everywhere it was checked.

It is **not approvable as-is**. One Critical defect (RF-01, the wrong seed-1
oracle quoted in the package's terminal verification instructions) must be
fixed before any task runs, because it corrupts the package's definition of
"unchanged". Two High defects (RF-02, RF-03) mean the warrior-appearance
roster, as tabled, fails two of its own planned automated suites — the design
and its tests must be reconciled before VIS-018/VIS-020/VIS-021/VIS-039 are
executable. The four planner-flagged inconsistencies are all confirmed real,
correctly surfaced, and resolvable with the proposed defaults (RF-04 through
RF-07); the remaining Medium findings are small, contained edits.

Recommended path: apply the RF-01 edits and the RF-02/RF-03 roster decisions,
fold the Medium resolutions into the same revision, then re-present the
package for the user's approval alongside the open decisions OD-1 through
OD-8. Nothing found challenges the package's architecture, its procedural
rendering decision, or its determinism story — the defects are in content
tables and in one propagated factual error, not in the design's bones.

## Resolution record (2026-07-28, post-review revision)

Applied by the orchestrating session after independently re-verifying RF-01
against `tests/Hukbo.Core.Tests/DeterminismTests.cs:33` and
`docs/development/testing.md` ("The Phase 2 reference pair").

- RF-01 (Critical) — resolved. Every gate-oracle statement in the package now
  cites the seed-1 reference pair (`stateHash 27DC94C6E9A01E35`,
  `eventHash 372C9217E5CB8BE9`); the remaining mentions of
  `0x5BEBA7A68F69BE0D` all describe it as the zero-interception V1
  control-run golden, a separate additional guard.
- RF-02 (High) — resolved with option (a): the pairwise differentiation
  criterion is scoped within each regional block, with the historical
  justification recorded, and VIS-018/VIS-039 mirror the scope.
- RF-03 (High) — resolved with the first option: VIS-18 and TAG-14 carry the
  prosperous-freeman scope marker and are reduced to a single I4 accent
  (E2 replaced with E1); VIS-020/VIS-021 recognize the carve-out.
- RF-05, RF-06, RF-07, RF-09, RF-10, RF-11, RF-12 through RF-18 — resolved
  per the proposed resolutions in this document.
- RF-04 and RF-08 — not silently resolved, because each would relax a MUST
  requirement, which needs the user's explicit approval. Both are annotated
  in `requirements.md` and tracked as package-level open decisions OD-9
  (dust) and OD-10 (shield proportion deltas) in the README.

No finding was closed by weakening a requirement, deleting a test, or
softening an acceptance criterion.
