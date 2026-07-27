# Precolonial Philippine Battles Research Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Build a ten-file, citation-backed research set that separates
deep-past warfare from early-contact warfare and resolves each track through
four increasingly granular analytical depths for future Hukbo planning.

**Architecture:** Use two mirrored historical tracks, a shared evidence
protocol, and a final synthesis that keeps historical observation,
interpretation, simulation proposal, and tuning separate. Parallel research is
allowed only across non-overlapping files; the orchestrator owns integration,
cross-track consistency, source verification, and final reader testing.

**Tech Stack:** Markdown, scholarly and primary-source web research, repository
knowledge graph, Git, and link/citation validation.

---

## 1. Locked scope and success criteria

Create only:

- `docs/research/battles/README.md`
- `docs/research/battles/01-deep-past-overall-warfare.md`
- `docs/research/battles/02-deep-past-forces-and-command.md`
- `docs/research/battles/03-deep-past-formations-and-tactics.md`
- `docs/research/battles/04-deep-past-individual-combat.md`
- `docs/research/battles/05-early-contact-overall-warfare.md`
- `docs/research/battles/06-early-contact-forces-and-command.md`
- `docs/research/battles/07-early-contact-formations-and-tactics.md`
- `docs/research/battles/08-early-contact-individual-combat.md`
- `docs/research/battles/09-gameplay-planning-synthesis.md`

Do not:

- modify simulation or Client behavior;
- rewrite `docs/research/HISTORICAL_1500s_WEAPONS.md`;
- present a pan-Philippine doctrine unsupported by regional evidence;
- use modern martial-arts claims as proof of sixteenth-century technique;
- invent precise formations, force sizes, or attribute values;
- silently fold later colonial practices into the pre-contact tracks; or
- stage or commit unrelated working-tree changes.

Binary completion criteria:

1. All ten files exist and are mutually navigable.
2. Every major historical claim is cited.
3. Deep-past and early-contact evidence remain visibly separate.
4. Each historical file states region, chronology, evidence limits, and
   confidence.
5. The files progress through the approved four depths without duplicating
   entire sections.
6. The synthesis traces every proposed game concept to historical findings and
   labels design invention.
7. A fresh reader can correctly explain what is attested, reconstructed,
   inferred, and unknown.
8. Link, Markdown, whitespace, and final-diff checks pass.

## 2. Research ownership

### Orchestrator

**Objective:** Control scope, source standards, integration, synthesis, and
verification.

**Inputs:** Approved design, existing weapons research, current source
contracts, and all worker findings.

**Owned files or subsystem:**

- `docs/research/battles/README.md`
- `docs/research/battles/09-gameplay-planning-synthesis.md`
- both research planning documents
- final cross-file edits and verification

**Expected output:** Coherent navigation, evidence vocabulary, game-planning
translation, and final verified diff.

**Success condition:** All completion criteria pass with no unresolved
Critical or High review finding.

**Dependencies:** Worker research and current-source inspection.

**Prohibited scope:** No source, test, existing research, or unrelated doc
edits.

### Worker A: Deep-past research owner

**Objective:** Research warfare before sustained Spanish observation, with
explicit limits on archaeological and comparative inference.

**Inputs:** Approved design, archaeological publications, material-culture
research, linguistic scholarship, and geographically relevant regional
studies.

**Owned files or subsystem:**

- `docs/research/battles/01-deep-past-overall-warfare.md`
- `docs/research/battles/02-deep-past-forces-and-command.md`
- `docs/research/battles/03-deep-past-formations-and-tactics.md`
- `docs/research/battles/04-deep-past-individual-combat.md`

**Expected output:** Four sourced files or a source-and-claim packet sufficient
for the orchestrator to create them.

**Success condition:** No early-contact description is projected backward
without a labeled inference; gaps in formation and duel evidence are explicit.

**Dependencies:** Evidence protocol in the approved design.

**Prohibited scope:** Early-contact files, synthesis, final numeric mechanics,
and existing weapons research.

### Worker B: Early-contact research owner

**Objective:** Research sixteenth- and early-seventeenth-century warfare using
source-critical primary accounts and modern scholarship.

**Inputs:** Reliable editions or translations of early accounts, archaeology,
museum publications, and scholarly analyses of Luzon, Visayan, Mindanao, and
Sulu warfare.

**Owned files or subsystem:**

- `docs/research/battles/05-early-contact-overall-warfare.md`
- `docs/research/battles/06-early-contact-forces-and-command.md`
- `docs/research/battles/07-early-contact-formations-and-tactics.md`
- `docs/research/battles/08-early-contact-individual-combat.md`

**Expected output:** Four sourced files or a source-and-claim packet sufficient
for the orchestrator to create them.

**Success condition:** Primary accounts are contextualized, regional examples
are named, and the formation discussion does not imply European-style drill
without evidence.

**Dependencies:** Evidence protocol in the approved design.

**Prohibited scope:** Deep-past files, synthesis, modern martial-arts
genealogies, and invented battlefield precision.

### Independent reader/reviewer

**Objective:** Review the complete set without conversation context.

**Inputs:** Only the finished ten-file research set and the approved design.

**Owned files or subsystem:** None; review is read-only.

**Expected output:** Reader-question results and findings classified Critical,
High, Medium, or Low.

**Success condition:** The reviewer can distinguish periods, regions, evidence
grades, and history from game proposals; no Critical or High finding remains.

**Dependencies:** Integrated draft and link audit.

**Prohibited scope:** Edits, new mechanics, or scope expansion.

## 3. Task sequence

### Task 1: Record repository and research baseline

**Files:**

- Read:
  `docs/plans/2026-07-27-precolonial-philippine-battles-research-design.md`
- Read: `docs/research/HISTORICAL_1500s_WEAPONS.md`
- Read:
  `docs/archives/2026-07-26/2026-07-26-philippine-combat-configuration-design.md`
- Read: `src/Hukbo.Core/Combat/**`
- Read: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Read: `src/Hukbo.Core/Simulation/Scenario.cs`

**Step 1: Record starting state**

Run:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
```

Expected: the pre-existing dirty files remain visible; none is staged by this
task.

**Step 2: Discover current combat contracts**

Use the repository knowledge graph to locate combat presets, loadouts,
hit-location rules, target selection, movement, attack resolution, and exposed
agent fields. Verify the results against current source.

Expected: a short internal list of current mechanics that the synthesis must
classify as existing behavior, not historical findings.

**Step 3: Establish research questions**

For each historical file, require answers to:

- What is known?
- Where and when does the evidence apply?
- What kind of source supports it?
- What is uncertain or disputed?
- What should a battle simulation do with the finding, if anything?

Expected: every draft uses the same analytical frame.

### Task 2: Build and audit the source base

**Files:**

- Create later: all files under `docs/research/battles/`

**Step 1: Collect primary-source families**

Locate reliable editions or institutional reproductions of relevant early
accounts, including where available:

- Antonio Pigafetta;
- Miguel de Loarca;
- the Boxer Codex;
- Francisco Alcina;
- early expedition and colonial reports collected in scholarly editions; and
- region-specific accounts for Mindanao and Sulu.

Expected: bibliographic identity, author/date, described region, observer
position, and a stable link are recorded for every used primary source.

**Step 2: Collect scholarly-source families**

Prioritize archaeology, academic-press histories, peer-reviewed studies, and
museum or university publications addressing:

- political organization and raiding;
- maritime technology and naval fighting;
- fortifications and settlement defense;
- weapons and protective equipment;
- slavery, captives, prestige, alliance, and trade;
- regional military organization; and
- limits of reconstructing technique from material culture.

Expected: at least two independent source families support every major
cross-cutting conclusion where the literature permits.

**Step 3: Create a claim ledger before prose**

For each planned claim, record:

```text
Claim:
Period:
Region or society:
Source:
Source type:
Evidence label:
Bias or limitation:
Destination file:
```

Expected: unsupported detail is removed or explicitly labeled unknown before
drafting.

**Step 4: Verify sources**

Open each cited source or authoritative catalog record. Reject links that only
repeat unsourced popular claims, cannot identify the underlying publication, or
conflate modern martial arts with precolonial practice.

Expected: every retained source is usable by a future reader.

### Task 3: Draft the deep-past track

**Files:**

- Create: `docs/research/battles/01-deep-past-overall-warfare.md`
- Create: `docs/research/battles/02-deep-past-forces-and-command.md`
- Create:
  `docs/research/battles/03-deep-past-formations-and-tactics.md`
- Create: `docs/research/battles/04-deep-past-individual-combat.md`

**Step 1: Draft Depth 1**

Cover chronology, evidence types, conflict motives, geography, maritime and
land settings, strategic objectives, and limitations.

Expected: broad warfare is described without borrowing early-contact detail.

**Step 2: Draft Depth 2**

Cover political leadership, mobilization, probable force composition,
logistical constraints, weapons evidence, signaling evidence, and unknowns.

Expected: every organizational conclusion names its evidence basis.

**Step 3: Draft Depth 3**

Cover what archaeology can and cannot reveal about deployment, ambush,
fortification, missile use, close combat, pursuit, and withdrawal.

Expected: no precise formation diagram is presented as attested unless a source
supports it.

**Step 4: Draft Depth 4**

Cover individual equipment affordances, reach, protection, movement, awareness,
cooperation, training uncertainty, and the absence or presence of duel evidence.

Expected: weapon affordances are distinguished from reconstructed techniques.

**Step 5: Run track audit**

Search the four files for unqualified terms such as "Filipinos," "always,"
"formation," "martial art," "duel," and exact force-size claims.

Expected: every broad or precise claim is narrowed, cited, or removed.

### Task 4: Draft the early-contact track

**Files:**

- Create: `docs/research/battles/05-early-contact-overall-warfare.md`
- Create: `docs/research/battles/06-early-contact-forces-and-command.md`
- Create:
  `docs/research/battles/07-early-contact-formations-and-tactics.md`
- Create:
  `docs/research/battles/08-early-contact-individual-combat.md`

**Step 1: Draft Depth 1**

Cover raiding, feud, alliance, settlement defense, conquest, maritime power,
political objectives, captives, prestige, and regional variation.

Expected: source perspectives and geographic coverage are explicit.

**Step 2: Draft Depth 2**

Cover leaders, retainers, allied contingents, recruitment, force-size
uncertainty, equipment, watercraft, logistics, reconnaissance, signals, and
cohesion.

Expected: terms from primary accounts are defined rather than flattened into
modern ranks.

**Step 3: Draft Depth 3**

Reconstruct battle phases and group geometry: approach, concealment, opening
missiles, boarding or shock contact, local clustering, flanking, fortification,
pursuit, and retreat.

Expected: the document says whether each arrangement is directly described,
inferred from an event, or merely mechanically plausible.

**Step 4: Draft Depth 4**

Cover range judgment, shield use, weapon roles, footwork constraints,
targeting, aggression, self-preservation, cooperation, fatigue, wounds, and
one-versus-one encounters inside larger fights.

Expected: no choreography is invented and no formal duel system is assumed.

**Step 5: Run primary-source bias audit**

For every primary-account section, check author, date, location, translation,
purpose, access, and colonial or hostile incentives.

Expected: readers are warned where evidence may exaggerate numbers, savagery,
order, disorder, or Spanish effectiveness.

### Task 5: Create navigation and evidence guide

**Files:**

- Create: `docs/research/battles/README.md`

**Step 1: Add scope and terminology**

Explain the two tracks, four depths, modern umbrella use of "Filipino," date
ranges, regional limits, and evidence labels.

Expected: the README prevents a new reader from treating the set as one
uniform doctrine.

**Step 2: Add reading paths**

Provide:

- chronological reading order;
- formations-focused path;
- individual-combat path; and
- game-planning path.

Expected: each path links to exact files.

**Step 3: Add source-criticism guide**

Summarize how to read colonial accounts, archaeological inference, regional
comparison, and modern martial-arts claims.

Expected: future agents know which evidence can support which degree of detail.

### Task 6: Produce gameplay-planning synthesis

**Files:**

- Create: `docs/research/battles/09-gameplay-planning-synthesis.md`
- Read: current Core combat and simulation contracts

**Step 1: State translation rules**

For each item, use:

```text
Historical observation:
Interpretation:
Confidence:
Possible Hukbo abstraction:
Design invention:
Do not infer:
```

Expected: history and design remain visually distinct.

**Step 2: Map battle hierarchy**

Cover strategic posture, force command, group state, formation or local
geometry, individual intent, and attack resolution.

Expected: every level identifies inputs, outputs, and uncertainty without
prescribing implementation architecture.

**Step 3: Map tactical states**

Consider, only where supported:

- muster and approach;
- scouting and concealment;
- raid, ambush, defense, or open confrontation;
- missile harassment;
- close engagement or boarding;
- local support and target concentration;
- cohesion loss;
- withdrawal, rout, pursuit, and disengagement.

Expected: proposed state machines are labeled design abstractions.

**Step 4: Map skills and attributes**

Organize candidate attributes into:

- perception and awareness;
- mobility and endurance;
- weapon familiarity and reach judgment;
- shield or defensive skill;
- aggression and courage;
- discipline and cohesion;
- tactical judgment;
- command influence; and
- fatigue, wounds, and morale state.

Expected: distinguish trainable skill, physical capacity, equipment effect,
social state, and temporary condition. Do not assign final numbers.

**Step 5: Compare with current Hukbo behavior**

Identify what exists, what the research suggests, what is unsupported, and what
would require a future design decision.

Expected: the synthesis is useful in a later planning session without
authorizing implementation.

### Task 7: Citation, consistency, and reader verification

**Files:**

- Verify: `docs/research/battles/*.md`

**Step 1: Validate Markdown and whitespace**

Run:

```powershell
git diff --check -- docs/research/battles
```

Expected: no errors.

**Step 2: Validate the file set and internal links**

Confirm exactly the ten approved files exist and every relative Markdown link
resolves.

Expected: no missing file or broken internal path.

**Step 3: Validate external citations**

Check every unique external link for a successful response or a documented
access limitation. Confirm page titles and publication identities match the
bibliography.

Expected: no citation points to an unrelated page or search-results page.

**Step 4: Run claim spot checks**

Select at least three consequential claims per file and verify them against the
cited passage or publication.

Expected: claims do not outrun their sources.

**Step 5: Run fresh-reader questions**

Ask an independent reader:

1. What differs between the deep-past and early-contact evidence?
2. What do we actually know about large battlefield formations?
3. How were forces likely organized and commanded?
4. How should one-versus-one combat be understood?
5. Which individual skills and attributes are historically supported?
6. Which proposed game mechanics are design inventions?
7. Where does the research explicitly say "unknown"?
8. Which conclusions vary by region?

Expected: answers identify the correct files and evidence grades without
inventing certainty.

**Step 6: Resolve review findings**

Classify findings:

- Critical: fabricated evidence, materially false claim, or broken provenance;
- High: period conflation, regional overgeneralization, or historical/design
  confusion;
- Medium: missing nuance, resilience, or navigability issue;
- Low: optional wording or formatting improvement.

Resolve all Critical and High findings. Limit Medium and Low edits to immediate
scope.

**Step 7: Inspect final diff**

Run:

```powershell
git status --short
git diff --stat -- docs/research/battles
git diff -- docs/research/battles
```

Expected: only the ten approved research files appear in the task diff; no
temporary notes, scraped pages, credentials, or unrelated files remain.

## 4. Completion report

Return:

```text
Implemented:
- The two historical tracks and four depths.
- Navigation, evidence rules, and gameplay-planning synthesis.

Verification:
- Source and claim audit.
- Internal/external link results.
- Markdown and final-diff results.
- Independent reader result.

Key decisions:
- Deep past and early contact remain separate.
- Formation precision is limited by evidence.
- One-versus-one combat is not assumed to be a formal duel tradition.
- Game mechanics are proposals, not historical facts.

Files changed:
- Research navigation.
- Deep-past track.
- Early-contact track.
- Gameplay synthesis.

Unresolved:
- Source-access limitations, disputed claims, and irreducible historical gaps.
```

Do not claim completion if citations were not checked, Critical or High review
findings remain, or the four-depth distinction cannot be recovered by a fresh
reader.
