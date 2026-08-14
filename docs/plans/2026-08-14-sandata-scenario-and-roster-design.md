# Sandata scenario and roster design

Status: draft design document. A design authorizes nothing; it does not
schedule work and it is not a task list.

## Sections

1. Context and the two decisions this document answers
2. Sources read
3. Q1 — Where per-operator data lives
4. Q2 — Which fields become authored
5. Q3 — Map format version and the pinned content hash
6. Q4 — Backward compatibility and defaults
7. Q5 — Does this move any hash
8. Q6 — What a scenario minimally is
9. Q7 — Who owns the defaults
10. Spectator discoverability
11. What this design does not decide
12. Interaction with the concurrent mission-outcome work

## 1. Context and the two decisions this document answers

Two decisions were taken on 2026-08-14 and both point at the same gap.

**Decision A — a scenario system.** Today there is exactly one hardcoded map,
one hardcoded roster, and no mission select. `SandataGame.BuildInitialState`
reads real position, faction, and facing for each operator from the map's
`SPAWN` records, and then fills every other field of that operator — health,
firearm, magazine rounds, crouch state, and the rest — from a fixed set of
placeholder values that are copied, not shared, from
`HeadlessRunner.BuildInitialState`. Neither the client's nor the headless
runner's placeholder set is the map's data; both are invented after the map is
loaded, and they already disagree with each other (see Q4 and Q5). There is no
file, record, or type today whose job is "per-operator data for this
mission" — a scenario, in the ordinary sense of the word, does not exist yet.

**Decision B — per-spawn health.** `SandataGame.PlaceholderOperatorHealth` was
raised from 100 to 300 on 2026-08-14. The doc comment on that constant records
why: at 100, against the 7.62x39 round's 25 damage, the fourth round fired
killed the target, so an automatic burst could never run past 0.30 seconds and
could not be heard. Raising the constant to 300 buys a twelve-round, roughly
1.2-second burst, at the cost of every engagement on the placeholder map
taking proportionally longer to resolve. That is a tuning patch over the
symptom. The user asked for the real fix, and named it directly in
`docs/plans/TODO.md`'s 2026-08-14 entry: "The real fix is a scenario system
that carries health per spawn rather than a single constant for every
operator on the map." One constant applied to every operator, rifle or pistol,
point-man or last-alive, is the wrong shape regardless of what value it holds.

Both decisions resolve to the same question: what carries per-operator data
for a mission, and where does it live. This document answers that question
and the seven questions the dispatching brief posed about it. It does not
implement anything.

## 2. Sources read

Primary sources, all read before this document was written:

- `src/Sandata.Client/SandataGame.cs` — `BuildInitialState`,
  `LoadoutForIndex`, `PlaceholderOperatorHealth` and its doc comment, and the
  wider family of `Placeholder*` constants this client already carries.
- `src/Sandata.Core/Maps/MapTokenizer.cs`, `MapValidator.cs`,
  `MapCanonicalizer.cs`, `MapContentHash.cs`, `MapRecord.cs`,
  `MapRecordKind.cs` — the full map parsing and canonicalisation pipeline.
- `src/Sandata.Core/Simulation/MissionState.cs` — `OperatorState`'s complete
  field list and its custom equality.
- `src/Sandata.Core/Simulation/Mission.cs` — `MissionContentHash`'s fixed
  fold order, and its own doc comment already anticipating that "map
  reference" needed a concrete type decision.
- `src/Sandata.Core/Determinism/SandataStateHasher.cs` — confirms
  `Mission.MissionContentHash` folds into the overall state hash as a single
  rolled-up value, not re-folded field by field.
- `src/Sandata.Core/Weapons/FirearmCatalog.cs`, `FirearmId.cs` — the 38-row
  firearm roster and its per-row `MagazineCapacity`.
- `src/Sandata.Core/Combat/OutcomeRules.cs`, `MissionOutcome.cs` — the
  existing, elimination-only mission-outcome resolver.
- `src/Sandata.Headless/HeadlessRunner.cs` — its own, independently
  drifting `BuildInitialState`/`LoadoutForIndex`, and its hardcoded
  `mapContentHash: 1UL`.
- `docs/development/map-format.md` — the map grammar reference, including
  the "a format change means a new filename" rule and the pinned content
  hash for the shipped fixture map.
- `docs/plans/2026-08-07-sandata-scaffold-design.md`, sections 4
  (determinism contract) and 12 (map format) — binding, and outranking
  anything inferred here.
- `tests/Sandata.Core.Tests/AngleHouseFixtureTests.cs` — the pinned
  `MapContentHash` literal for the shipped fixture map.
- `docs/plans/TODO.md` — the 2026-08-14 entry that names Decision B in the
  user's own words.

An archived plan or design document is never linked from here even where one
exists on the same topic; if a reader needs to know an archived document
existed, it is named in prose, not linked, per `CLAUDE.md` section 6.

## 3. Q1 — Where per-operator data lives

Three alternatives were weighed.

**Alternative A — extend the `SPAWN` record itself.** `MapTokenizer` enforces
a fixed token count per record kind (`RequireTokenCount`); `SPAWN` requires
exactly five tokens today (`SPAWN <faction> <x> <y> <facing>`). Appending
health, a firearm id, and a magazine count to that line would mean every
existing `.hkmap` file's `SPAWN` lines become malformed the moment the
tokenizer's required count changes — there is no optional-tail concept in
this grammar, and adding one is itself a grammar change with its own cost.
It also conflates two things that change on different schedules: level
geometry (which is authored once, by a level designer, and rarely touched)
and mission composition (which authored content wants to vary per playthrough
without touching the map at all — the same `angle-house.hkmap` should be
playable with different rosters). Rejected: cheapest to type, most expensive
to live with, and it is a grammar break for every map that already exists.

**Alternative B — a new `.hkmap` record kind**, for example `OPERATOR`,
carrying the extra fields and correlated to a `SPAWN` record by position.
`MapRecordKind` is deliberately packed with no gaps — `Hkmap=0` through
`End=8`, nine values, all assigned, and the doc comment on that enum states
outright that the packing is relied on so `(int)kind` can be used directly as
both the canonical sort ordinal and the content-hash kind byte. Inserting a
new kind touches `MapTokenizer` (a new case in the record-kind switch and a
new `RequireTokenCount` entry), `MapCanonicalizer` (a new case in the sort
key and in `BodyFields`), `MapContentHash` (a new case in `FieldsOf`), and
`MapValidator` if the new kind wants any cross-record check (for instance, an
`OPERATOR` record needs to validate that a matching `SPAWN` actually exists at
its stated position — a rule that doesn't exist for any other pairing today).
This is real surface area across four files in `Sandata.Core`, for a payoff
that is still tangled with the map file's own identity: an `OPERATOR` record
inside `angle-house.hkmap` is still one roster, hardcoded to that map, exactly
as SPAWN-extension would be. Rejected: more invasive than Alternative A for
no corresponding gain in flexibility.

**Alternative C — a separate roster file, sibling to the `.hkmap` file,
correlated by spawn coordinate.** `MapValidator.ValidateSpawnSeparation`
already guarantees no two `SPAWN` records on a map — of either faction — sit
closer than one body diameter (8.5 world units, via `BodyRadiusQuarterWu`).
That means a spawn's `(faction, x, y)` triple is already a stable,
collision-free key inside one map, with no new id field needed anywhere. A
roster file keyed by that triple can assign per-operator data to a specific
spawn without touching `MapTokenizer`, `MapCanonicalizer`, `MapContentHash`,
or `MapValidator` at all, and without bumping `HKMAP`'s version field. It
also decouples geometry from composition cleanly: the same map file supports
many rosters (a four-operator skirmish and an eight-operator assault on the
same building), and the same roster shape is reusable across maps that share
a spawn layout convention.

**Decision: Alternative C.** A scenario's roster is a new, separate record
type and a new, separate content hash, not a change to the `.hkmap` grammar.
The roster references a map by that map's own `MapContentHash` (the same
pattern `Mission` already uses to reference a map — see Q3 and Q5), and
references an individual operator's slot inside that map by the operator's
`SPAWN` record's `(Faction, X, Y)` triple. This keeps the map format's own
determinism contract, its pinned fixture hash, and its four-file parsing
pipeline completely untouched by a roster change, and it matches the existing
split in this codebase between "what describes the building" and "what
describes the mission fought inside it."

Where the new roster type lives, what it parses from, and whether it is a
line-oriented text format like `.hkmap` or something else entirely, is
deliberately not decided here — see section 11.

## 4. Q2 — Which fields become authored

**Health becomes authored, per operator.** This is Decision B's direct
requirement, and it is also the field with the clearest evidence of harm from
staying a constant: `SandataGame.PlaceholderOperatorHealth` (300) and
`HeadlessRunner`'s hardcoded `Health: 100` literal have already drifted apart
from each other while both claim to be "the" placeholder, and neither
distinguishes a point man who should go down in one burst from a
last-alive who plausibly took cover and is on higher effective health. A
roster field replaces one number that is wrong for every operator with a
number chosen per operator.

**Firearm becomes authored, per operator**, replacing `LoadoutForIndex`'s
index-parity alternation in the client and the "one in four" alternation in
the headless runner. `LoadoutForIndex`'s own doc comment already calls the
client's version an accident that happens to work only because
`angle-house.hkmap` ships exactly four operators; SD-4 (the reload package)
worked "by accident" for the same reason — parity alternation only produces
a plausible rifle/pistol mix at this one specific operator count. Once the
firearm is authored per spawn instead of derived from array index, that
accident stops being load-bearing and a five- or eight-operator roster stops
silently reshuffling weapon assignment.

**Magazine rounds are derived, not separately authored.** `BuildInitialState`
today hardcodes `MagazineRounds: 30` for every operator regardless of which
firearm they actually carry; `FirearmCatalog`'s per-row `MagazineCapacity`
already varies (`Ak47` = 30, `Glock17Gen5` = 17, `M7` = 20, and so on), so the
existing constant is already wrong for every operator not carrying an
`Ak47`-capacity weapon. Once firearm is authored, a full magazine is simply
`FirearmCatalog.Rows[(int)Firearm].MagazineCapacity` — a value that must stay
consistent with the chosen firearm by construction, not a second number a
roster author could set inconsistently with the first.

**Faction is already authored, and stays exactly where it is** — the map's
`SPAWN` record's `Faction` field. It is not duplicated into the roster; the
roster's spawn-coordinate key already carries a `Faction` component (Q1), so
a roster entry inherits its faction from the spawn it is keyed to rather than
declaring a second, independently-editable copy that could disagree with the
map.

**Squad membership is explicitly out of scope for authoring**, and this is a
binding constraint, not a stylistic preference: design section 4's
determinism contract states plainly that squad slot index is derived each
tick from positions and entity ids, not stored or hashed, per section 8's
squad model. A roster field for squad membership would be authoring a value
the simulation is required to compute fresh every tick regardless of what was
authored. If a future scenario wants operators grouped for narrative or
UI purposes only — a label, never a mechanic — that is a distinct decision
this document does not make.

**Starting stance (crouched vs standing, weapon lowered vs raised) is
deferred**, not decided either way. The current placeholder sets both to a
fixed default (`IsCrouched: false`, an implicit raised weapon) for every
operator; nothing this document has read shows either a spectator-visible
need for per-operator starting stance or a concrete design for what values
would be meaningful. Leaving it a single documented default (Q4) is
consistent with the rest of this decision: an unauthored field is fine as
long as it is one documented default rather than two independently-invented
ones.

## 5. Q3 — Map format version and the pinned content hash

Under the Alternative C decision (Q1), nothing about the `.hkmap` grammar
changes. `MapTokenizer`'s `HKMAP <version>` header check, still requiring
exactly `1`, is untouched; `MapCanonicalizer`'s sort key and `MapContentHash`'s
`FieldsOf` switch are untouched because no new record kind and no new field on
an existing record kind is introduced. `angle-house.hkmap` itself is not
edited at all — a roster is a new, separate file, never a modification of the
map file it references. Consequently `angle-house.hkmap`'s pinned content
hash, `11909359227906322716UL`, asserted in
`AngleHouseFixtureTests.FixtureContentHashIsPinned`, is untouched by this
design, and no new map filename is required either — there is no in-place
edit to trigger `docs/development/map-format.md`'s rule that "a format change
means a new filename, not an edit in place."

That rule remains the correct one to cite for the two rejected alternatives,
for the record: had Alternative A (extending `SPAWN`'s token count) or
Alternative B (a new `.hkmap` record kind) been chosen instead, either one is
a grammar change under that rule's own definition, and either would have
required bumping `HKMAP`'s version field, shipping the changed map under a
new filename with its own `NAME` id, and recording a new pinned content hash
for that new file — `angle-house.hkmap`'s existing pinned hash would stay
correct for the old file, but no map using the new grammar could reuse it.
Choosing the separate-roster-file alternative is precisely what avoids
that cost.

## 6. Q4 — Backward compatibility and defaults

An existing map with no roster file present for it must still load and run —
`angle-house.hkmap` is the shipped fixture and the one map this game plays
today, and nothing about adding a roster format may make it stop loading
without one. The mechanism for this is the same one already governing
`Sandata.Core`'s other optional structures: a documented default roster
entry, applied per spawn when no roster file names that spawn, or when no
roster file is supplied for the map at all.

That default entry is exactly what today's two placeholder implementations
already attempt and already disagree on — this is the second-hidden-
placeholder risk the brief warned against, and the disagreement already
exists in the shipped code: `SandataGame.PlaceholderOperatorHealth` is 300,
`HeadlessRunner`'s inline `Health: 100` literal is a different, unnamed
number, and both are asserted nowhere against each other. The fix is not to
pick a winner between 300 and 100 and call it done; it is to have exactly one
named default, defined once, in the roster type itself (or immediately
beside it), that both the client and the headless runner read rather than
each inventing their own. Where precisely that default constant lives is a
Q7 question (who owns the roster), not a new question here, but the
requirement standing on its own is: one documented default, not a second
constant with the same job under a different name in a different assembly.

The default's content — what health value, what firearm, what magazine
state — is not chosen by this document. Choosing it is implementation, and
this design only establishes that it must be singular and documented, with
its rationale recorded next to it exactly as `PlaceholderOperatorHealth`'s
own doc comment already models for one field today.

## 7. Q5 — Does this move any hash

Yes, for any run that actually loads a roster with authored per-operator
values different from today's placeholders — and that is the correct,
expected consequence, not a defect. `MissionState`'s `OperatorState` already
lists `Health`, `MagazineRounds`, and `Firearm` as authoritative, hashed
fields (`OperatorState`'s custom `Equals`/`GetHashCode` fold every field,
`Firearm` included). Changing what value flows into those fields for a given
seed and a given map necessarily changes the resulting state hash, exactly
as CLAUDE.md section 5 requires for "changing... weights" — a new roster
default or a new authored roster is functionally a new preset input, and any
recorded golden expectation that exercises it needs a new golden value the
same way a new preset version would.

Whether *today's* golden fixtures and the seed-1 headless workload move is a
separate, narrower question, and the answer is no, for a specific and
verifiable reason: neither one builds its `Mission` or its operators from a
real map file at all. `HeadlessRunner.BuildInitialState` constructs its
`Mission` with a hardcoded `mapContentHash: 1UL` literal — never a call to
`MapContentHash.Compute()` — and its own operator roster is a second,
independent synthetic generator (`Health: 100`, `LoadoutForIndex`'s "one in
four" rule) that never touches `MapTokenizer` or `MapValidator` either. The
two Sandata golden replay fixtures in
`tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` and the recorded
seed-1 baseline in `docs/development/testing.md` are both built through this
same headless path. As long as this design's roster format is not wired into
`HeadlessRunner` itself — and nothing here proposes that it should be, since
`HeadlessRunner` exists precisely to exercise the simulation without a real
map — those fixtures stay insulated by construction, not by luck. The moment
a future change *does* route `HeadlessRunner` through a real map and a real
roster, that insulation ends and a new golden baseline becomes due at that
point, not before.

`Mission.MissionContentHash` itself (`FormatVersion`, `Seed`,
`MapContentHash`, `TickPolicy`'s two fields, each `FactionSetups` entry's two
fields, then `RulesetId`, folded through `SandataHash` in that fixed
declaration order) is not proposed to change shape by this design. A roster
reference does not need to fold into `MissionContentHash` to have its effect
felt in the state hash: `MissionContentHash` identifies *which mission is
being fought*, while the roster's authored values land in each
`OperatorState` at mission-build time and are hashed there, in
`SandataStateHasher`'s per-operator fold, exactly as they are today from the
placeholder values. Whether a roster's own identity (a hypothetical
`RosterContentHash`) should additionally fold into `MissionContentHash`
alongside `MapContentHash` — so that two missions built from different
rosters against the same map are provably different missions even before any
tick runs — is a real question, but it is an implementation-level wiring
question for whoever builds the roster type, not one this document needs to
resolve to answer Q5 correctly today.

## 8. Q6 — What a scenario minimally is

The smallest thing worth calling a scenario, on the evidence gathered here,
is three references bound together: a map (identified by its
`MapContentHash`, exactly as `Mission` already identifies one), a roster
(the per-spawn data this document has been describing), and a reference to a
win condition that a mission-outcome resolver can act on. That third piece
does not need its own design here — section 12 names the seam with the
concurrent work on exactly that resolver — but a scenario without any
pointer to how the mission ends is not meaningfully different from what
`SandataGame.BuildInitialState` already assembles today by hand: a map plus
some operators plus (currently) elimination as the only outcome anyone has
wired up. A scenario is the named, reusable, versioned bundle of those three
references; nothing less earns the name, because everything the client
invents ad hoc today already exists in some unnamed form once a map, a
roster, and an outcome rule are all present.

**Explicitly out of scope for a v1 scenario:**

- **A campaign.** No sequencing of scenarios, no persistence of outcome
  between missions, no meta-progression. `CLAUDE.md`'s own Hukbo section
  states the equivalent boundary for that game's campaign layer in stronger
  terms than this document needs to restate for Sandata; the same caution
  applies here by direct analogy — a scenario is the tactical resolution
  unit, not a container for anything above it.
- **Persistence beyond what already exists.** Sandata's existing
  snapshot/resume mechanism (recomputing every outstanding path from its
  stored request record on resume, per design section 4's "derived
  structures are never hashed and never snapshotted" rule) is not extended,
  touched, or assumed by this document. A scenario reference is data loaded
  at mission start, not a new save-format concern.
- **Mission-select UI.** Nothing here proposes a menu, a list, a file picker,
  or any client-side flow for choosing among scenarios. `SandataGame` still
  loads exactly one thing at startup under this design; which one, and
  whether there is ever more than one, is unaffected by this document. The
  only change in scope is what "the one thing" consists of — a bundle of map
  plus roster plus outcome reference, instead of a map plus invented
  placeholders.
- **A scenario file format's own concrete syntax.** This document establishes
  that the roster is a separate artifact from the map (Q1) and what it
  authors (Q2); it does not choose a text grammar, a serialization scheme, or
  a parser architecture for either the roster or the scenario bundle around
  it. That is deliberate — see section 11.

## 9. Q7 — Who owns the defaults

Today the client invents them: `SandataGame.BuildInitialState` and
`LoadoutForIndex` live in `Sandata.Client`, and `HeadlessRunner` invents its
own separate set in `Sandata.Headless`. Neither is `Sandata.Core`, and the
result is exactly the drift documented in Q4 — two presentation/runner-layer
assemblies each deciding, independently, what an unauthored operator looks
like.

The map format's own split is the precedent to follow, and it points the
same direction for the roster: `MapTokenizer`, `MapCanonicalizer`,
`MapValidator`, and `MapContentHash` all live in `Sandata.Core`, and both
`Sandata.Client` and `Sandata.Headless` only ever consume their output — a
client never invents map geometry, it loads and trusts what `Sandata.Core`
parsed and validated. A roster is authoritative, hashed data with exactly
the same character as map geometry: both feed `MissionState` construction,
both must produce identical results regardless of which of the two runner
assemblies loads them, and both need one canonical parser, one canonical
validator, and one canonical content hash rather than two.

**Decision: `Sandata.Core` owns the roster type, its parsing (if it is
text-authored, following the map format's own precedent), its validation,
its content hash, and its one documented default entry (Q4). `Sandata.Client`
and `Sandata.Headless` each read it, exactly as they already only read
`.hkmap` output rather than each re-deriving level geometry.** This is also
the only ownership choice consistent with the neither-Core-may-touch-the-
filesystem rule already binding both simulation projects: `Sandata.Core`
defines the roster *type* and validates an in-memory instance of it, exactly
as `MapTokenizer` parses a stream `Sandata.Core` never opened itself; reading
the actual roster file from disk stays a runner-layer concern, the same way
loading a `.hkmap` file from disk today is not something `MapTokenizer`
itself does unprompted.

## 10. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10 asks, for every feature proposal:
can a spectator discover this effect without reading source code? For a
per-operator health and firearm roster, the answer is yes, directly, and by
the same means the game already exposes those exact fields today: `Health`,
`MagazineRounds`, and `Firearm` are all already present, per operator, in
`OperatorState`, and are therefore already available to whatever HUD or
inspector surface reads `MissionState` for display. Today those numbers are
uniform and uninteresting because every operator is built from the same
placeholder; once they are authored per spawn, a spectator watching one
operator take noticeably longer to go down than another, or watching two
operators visibly carrying different weapon silhouettes and firing at
different cyclic rates, is discovering the roster's effect by watching the
fight, exactly as the standard asks — no new UI surface is required for the
effect to become legible, because the fields were already wired to be shown,
only their values were previously uniform. The one gap flagged by that
standard's spirit rather than its letter: nothing in this design proposes
that a scenario's *identity* — which roster, which map, which win condition —
becomes visible to a spectator anywhere (a title, a HUD label, a debug
overlay). That is a presentation decision left to whoever implements the
scenario type, not decided here, and it is worth naming rather than leaving
silently assumed.

## 11. What this design does not decide

- The concrete file format or serialization of the roster (text grammar
  like `.hkmap`, a different structured format, or something else). Only
  that it is a separate artifact from the map file, in `Sandata.Core`'s
  ownership.
- The concrete file format or type of the "scenario" bundle that references
  a map, a roster, and a win condition together (Q6). Whether it is its own
  file, or simply a tuple assembled at load time by the runner from a map
  path plus a roster path plus an outcome-rule selector, is unresolved.
- The default roster entry's actual values (what health number, what
  firearm, what magazine state) — only that there must be exactly one,
  documented, and owned by `Sandata.Core` rather than duplicated per runner.
- Starting stance as an authored field — deferred outright in Q2, not merely
  left to a later implementation pass.
- Whether a roster's own content hash folds into `Mission.MissionContentHash`
  alongside `MapContentHash`, and if so, where in the fixed fold order — Q5
  identifies this as a real, open wiring question without resolving it.
- Any mission-select UI, any campaign sequencing, any persistence format
  change — all named explicitly out of scope in Q6.
- The shape of a non-elimination win condition (objective capture, timed
  extraction, or anything else) — that belongs to the concurrent
  mission-outcome work, named next.

## 12. Interaction with the concurrent mission-outcome work

A separate, concurrent session is designing mission-outcome behaviour beyond
today's elimination-only resolver. `Sandata.Core.Combat.OutcomeRules.Resolve`
currently inspects only which faction still has a living operator and
returns `Faction0Victory`, `Faction1Victory`, `Draw`, or `Ongoing` — there is
no objective-based win condition anywhere in the codebase today, and
`ObjectiveRecord` (the one map record kind that already carries an explicit
`Index` field, unlike `SpawnRecord`) exists in the map format without any
consumer that resolves a mission around it.

The seam this design creates, and does not cross, is Q6's third component: a
scenario, minimally, references a win condition. Whatever shape the
concurrent session's outcome work settles on — an enum selecting among
outcome rules, a small record naming an objective index and a rule kind, or
something else — a scenario bundle needs to carry a reference to it that
`OutcomeRules` (or its successor) can resolve against, in exactly the same
spirit that a scenario carries a `MapContentHash` reference rather than
embedding the map itself. This document deliberately does not propose what
that reference looks like, what new outcome rules exist, or how
`OutcomeRules.Resolve` should change signature to accept one. It only names
the requirement that the two pieces of work agree on that shape before either
one is implemented: a scenario type built without knowing how the outcome
session's win condition will be referenced risks needing a second revision
the moment that session lands, and an outcome-rule change built without
knowing how a scenario names which rule applies risks the same problem in
reverse.
