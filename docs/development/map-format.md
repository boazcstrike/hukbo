# The `.hkmap` format

Sandata maps are hand-written plain text. There is no map editor, and none is
planned in the near term — the user's own stated position is that authoring
stays hand-written. Because of that, the grammar has to be documented well
enough that authoring a second map does not require reading
`MapTokenizer.cs`. This document is that reference. It is a description of
what the loader already accepts and rejects, not a proposal for a new
feature.

The three types that own this format are all in `src/Sandata.Core/Maps/`:

- `MapTokenizer.cs` parses raw text into validated `MapRecord` values,
  one record per line, and enforces every per-line and per-record rule.
- `MapCanonicalizer.cs` sorts a tokenized record stream into one canonical
  order, so two files that describe the same map but were typed with lines
  in a different order, or wall endpoints written in the opposite direction,
  produce byte-identical canonical output.
- `MapValidator.cs` runs the cross-record checks that need to see the whole
  map at once — spawn presence, spawn separation, full enclosure, and
  reachability — none of which a single line can fail or pass on its own.
- `MapContentHash.cs` folds the canonical record stream into one `ulong`
  content hash, which is what actually reaches the mission content hash and
  the game's determinism contract.

The only fixture that exists today is
`tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap`, quoted and annotated
in full at the end of this document.

## Units

Sandata's world unit is `wu`. Per `CLAUDE.md` section 3's unit table, 1 metre
equals 16 world units. Every coordinate, dimension, and radius field in an
`.hkmap` file is a `wu` value. The tokenizer's integer fields are plain
32-bit integers — the file format itself has no fixed-point scale and no
fractional syntax; `FixedPoint`'s internal raw scale of 1024 is a simulation
concern, not a map-file concern. A wall endpoint of `420` in the file means
420 world units, i.e. 26.25 metres, from the map origin.

Angles that are not `Facing16` values are `Bam16` — Sandata's `ushort`
binary angular measurement over a full turn of 65,536, where 0 is `+X`
(east) and increasing values turn toward `+Y` (screen-down): 16,384 is
south, 32,768 is west, 49,152 is north. `DOOR`'s hinge and state fields are
not angles; `COVER`'s `arcCentreBam` and `SPAWN`'s `facingBam` are.

## File shape

An `.hkmap` file is line-oriented, ASCII, one record per non-blank,
non-comment line. Per `MapTokenizer.cs`'s own doc comment:

- A blank line, or a line whose first character is `#`, is stripped before
  parsing and does not count as a record at all — it cannot violate a
  header-order rule and it cannot be the misplaced record after `END`.
- Tokens on a record line are separated by exactly one literal space
  character. Any other whitespace character — a tab, for example — is left
  attached to its neighbouring token, so the integer parser rejects it as
  leading or trailing whitespace on that token rather than treating it as a
  second separator.
- Every malformed line — an unknown record kind, the wrong token count, a
  non-integer token, an out-of-range field value, a header out of order, or
  a record after `END` — throws `MapLoadException` immediately, naming the
  line number and the specific rule broken. No line is ever skipped and no
  field is ever silently defaulted.

A well-formed file has exactly three header records, in this fixed order,
followed by zero or more body records in any order, followed by exactly one
terminator:

```
HKMAP <version>
NAME <id>
GRID <widthWu> <heightWu> <cellWu>
... any number of WALL / DOOR / COVER / SPAWN / OBJECTIVE lines, any order ...
END
```

`HKMAP` must be the very first record and must sit on line 1 of the file
(blank and comment lines before it still count against this, since they are
stripped before the line-1 check runs against the first surviving record).
`NAME` must be the record immediately after `HKMAP`. `GRID` must be the
record immediately after `NAME`. Any other order for these three throws
`MapLoadException` with the rule `header-out-of-order`.

## The nine record kinds

`MapRecordKind.cs` enumerates all nine kinds. The enum's numeric values are
deliberately chosen so that `Wall` (1) through `Objective` (5) double as the
canonicalisation sort ordinal design section 12 assigns those five body
kinds; `Hkmap` (0), `Name` (6), `Grid` (7), and `End` (8) sit outside that
range so a header or terminator kind can never collide with a body ordinal.

### `HKMAP <version>` — required, exactly one, must be line 1

One field: `version`, which must equal `1`. This is the only version
`MapTokenizer` accepts today; a different value throws `wrong-version`.

### `NAME <id>` — required, exactly one, must follow `HKMAP`

One field: `id`, a string matching `[a-z0-9-]{1,32}` — lowercase letters,
digits, and hyphens only, 1 to 32 characters. An id outside that pattern
throws `invalid-name-id`. `NAME`'s id is the only field in the whole format
that is a string rather than an integer, and it is also the only field that
never reaches `MapContentHash` — see "What reaches the content hash" below.

### `GRID <widthWu> <heightWu> <cellWu>` — required, exactly one, must follow `NAME`

Three integer fields, all in world units:

- `cellWu` must be a power of two (checked first). A non-power-of-two value
  throws `grid-cell-size-not-power-of-two`.
- `widthWu` and `heightWu` must each be a strictly positive multiple of
  `cellWu`. A failure here throws `out-of-range-field`.
- `widthWu / cellWu` and `heightWu / cellWu` must each be at most 512 —
  `MapTokenizer`'s own `MaxGridCellsPerAxis` constant, which matches
  `NavGrid.MaxDimensionCells`. A larger grid throws
  `grid-dimension-over-512`.

Every `WALL`, `DOOR`, `COVER`, `SPAWN`, and `OBJECTIVE` coordinate that
follows is bounds-checked against this record's `[0, widthWu] x [0,
heightWu]` closed interval — a point may legitimately sit exactly on the
map's far edge.

### `WALL <x1> <y1> <x2> <y2> <material>` — optional, any count, any order

An impassable segment. `(x1, y1)` and `(x2, y2)` must both lie inside the
grid's bounds and must differ from each other — an endpoint pair typed twice
throws `out-of-range-field`. There is no requirement that a wall be
axis-aligned; `angle-house.hkmap` has five walls that are neither horizontal
nor vertical. `material` must be `0`, `1`, `2`, or `3`. The fixture uses
material `1` for its axis-aligned shell and interior partitions, material
`2` for its five diagonal walls, and material `3` for exactly one interior
wall the design document and its pinned test both call the breachable one.
This document does not have a source citation for what each material number
means beyond that it is an integer `0..3` field on the record; if that
mapping matters to a reader, it belongs in the rendering or destructible-wall
code, not invented here.

### `DOOR <x1> <y1> <x2> <y2> <hinge> <state>` — optional, any count, any order

A door segment. Endpoints follow the same in-bounds and must-differ rules as
`WALL`, and in addition must be axis-aligned — `x1 == x2` or `y1 == y2`, but
not the diagonal case a `WALL` is allowed. A non-axis-aligned door throws
`out-of-range-field`.

`hinge` is `0` or `1`. `MapCanonicalizer.cs` normalises a door's endpoints to
lexicographic-ascending order — `(x1, y1)` always becomes the smaller
written endpoint — but never touches `hinge` or `state` during that swap.
`MapCanonicalizer.cs`'s own doc comment, written when that normalisation was
first added, calls what a written endpoint swap should do to `hinge` "a
named gap" left open for whoever next touched door semantics. `MapValidator.cs`,
written later, settles it: `hinge` is an **absolute** reading, not a
relative one. Hinge `0` always names whichever endpoint sorts
lexicographically smaller after normalisation — the one that becomes `X1`/
`Y1` — regardless of which order the map's author actually typed the four
coordinates, and hinge `1` names the larger one. Under this reading, the
same physical door authored twice, with its two endpoints typed in opposite
order but its hinge written against the same physical point both times,
canonicalises to byte-identical output. `MapValidator.cs`'s remarks work
through why the alternative, relative reading was rejected — under a
relative reading, leaving `hinge` untouched during the endpoint swap would
silently change which physical point a door hinges on whenever an author
happened to type its endpoints in descending order, with no error and no
symptom beyond the door swinging the wrong way in play — and the fixture
test `MapValidatorTests.DoorHingeIsAbsoluteToCanonicalEndpointOrder_NotRelativeToAuthoredOrder`
pins the absolute reading by asserting exactly this byte-identical outcome.
When authoring a door, write `hinge` against the coordinate pair that will
sort smaller once `x1`/`y1` are compared to `x2`/`y2` lexicographically, not
against whichever endpoint you happened to type first.

`state` is `0` or `1`. Per `NavBake.cs`'s own named constant
`DoorStateClosed = 0`: **state `0` means closed, state `1` means open.**
`angle-house.hkmap` has three doors, two closed (`state 0`) and one open
(`state 1`), and a pinned test,
`AngleHouseFixtureTests.FixtureHasOneOpenAndTwoClosedDoors`, asserts exactly
that count.

A door does not need to be collinear with, or aligned to, any wall — that
kind of cross-record geometric check, if it exists at all, is deferred to
`MapValidator`, not enforced by `MapTokenizer`.

### `COVER <minX> <minY> <maxX> <maxY> <arcCentreBam> <arcHalfBam> <height>` — optional, any count, any order

A directional cover object over the axis-aligned box `(minX, minY)` to
`(maxX, maxY)`. `minX` must be strictly less than `maxX`, and `minY` strictly
less than `maxY` — a degenerate or inverted box throws `out-of-range-field`.
Both corners must lie inside the grid's bounds.

`arcCentreBam` is a full `Bam16` value, `0..65535`. `arcHalfBam` is `1..32768`
— half the angular width of the arc this cover protects from, centred on
`arcCentreBam`. The maximum, `32768`, is called out explicitly in
`MapRecord.cs`'s doc comment as meaning the object covers from every
direction — a 360-degree cover. `angle-house.hkmap` has exactly one cover
record with `arcHalfBam` at that maximum, and a pinned test,
`AngleHouseFixtureTests.FixtureHasExactlyOne360DegreeCover`, asserts exactly
one such record exists.

`height` is `0..2`. `MapRecord.cs`'s doc comment states only the numeric
range for this field; this document could not find a named mapping from
`0`/`1`/`2` to a specific real-world cover height (crouching, standing, and
so on) in any file read while writing this document. If that mapping is
defined somewhere, it was not found here — treat the field as an integer
`0..2` whose gameplay meaning lives wherever the cover-eligibility or
line-of-sight code that consumes it is defined, not in this document.

### `SPAWN <faction> <x> <y> <facingBam>` — required, at least one per faction, any order

`faction` is `0` or `1` — Sandata has exactly two factions. `(x, y)` must lie
inside the grid's bounds. `facingBam` is a full `Bam16` value, `0..65535`.
Per-line validation stops there; `MapTokenizer` does not check that a spawn
is on passable ground, that there are at least two spawns, or that spawns of
the same faction are or are not close together — those are `MapValidator`'s
job, covered below, because they need the baked passability of the whole
map, not just one line's fields.

### `OBJECTIVE <index> <x> <y> <radiusWu>` — optional, any count, any order

`index` must be non-negative. `(x, y)` must lie inside the grid's bounds.
`radiusWu` must be strictly positive. Per-line validation does not check
that indices are dense from zero, or that an objective is reachable — both
are `MapValidator` checks, covered below.

### `END` — required, exactly one, must be the last record in the file

No fields. Any record — of any kind, even a repeated `END` — appearing after
the first `END` throws `end-not-last`. A file that never reaches an `END`
line throws `missing-end-record` once end of input is reached.

## Required versus optional records

| Record | Required | Count |
| --- | --- | --- |
| `HKMAP` | Yes | Exactly 1, must be line 1 |
| `NAME` | Yes | Exactly 1, must follow `HKMAP` |
| `GRID` | Yes | Exactly 1, must follow `NAME` |
| `WALL` | No | 0 or more |
| `DOOR` | No | 0 or more |
| `COVER` | No | 0 or more |
| `SPAWN` | Yes, in practice | At least one `faction 0` and at least one `faction 1`, or `MapValidator.Validate` throws |
| `OBJECTIVE` | No | 0 or more |
| `END` | Yes | Exactly 1, must be last |

`SPAWN` is not marked required by `MapTokenizer` itself — a file with zero
`SPAWN` lines still tokenizes successfully — but `MapValidator.Validate`
throws `missing-faction-0-spawn` or `missing-faction-1-spawn` the moment it
runs against such a file, so a map with no spawns for both factions cannot
pass full loading in practice.

## Every `MapLoadException` rule, and what throws it

`MapLoadException.Rules` (`src/Sandata.Core/Maps/MapLoadException.cs`) names
seventeen stable, machine-checkable identifiers. A test asserts against
these string constants, never against exception message text, so they are
also the right thing to grep for when a map fails to load:

| Rule identifier | What throws it |
| --- | --- |
| `negative-sign` | An integer token began with `-`. The format has no syntax for a negative number at all. |
| `decimal-point` | An integer token contained `.`. There is no fractional syntax. |
| `group-separator` | An integer token contained `,`. |
| `leading-whitespace` | An integer token began with a whitespace character other than the single-space token separator. |
| `trailing-whitespace` | An integer token ended with such a character. |
| `empty-token` | Two separators (or a separator at a line boundary) left an empty token between them. |
| `unknown-record-kind` | The first token on a line was not one of the nine record keywords. |
| `wrong-token-count` | A record had more or fewer tokens than its kind requires. |
| `non-integer-token` | A token could not be parsed as a 32-bit integer for a reason not covered above — including a value too large for `int`. |
| `out-of-range-field` | A field parsed as an integer but its value fell outside the range this document states for that field — an out-of-bounds coordinate, a degenerate `WALL`/`DOOR` pair, an inverted `COVER` box, a `material` outside `0..3`, an `arcHalfBam` outside `1..32768`, and so on. This one rule covers every per-field range check in the tokenizer. |
| `hkmap-not-line-1` | The first record was not `HKMAP`, or `HKMAP` did not appear on line 1. |
| `wrong-version` | `HKMAP`'s version field was not `1`. |
| `end-not-last` | A record appeared after the file's first `END`. |
| `grid-cell-size-not-power-of-two` | `GRID`'s `cellWu` was not a power of two. |
| `grid-dimension-over-512` | `GRID`'s width or height spans more than 512 cells on either axis. |
| `invalid-name-id` | `NAME`'s id did not match `[a-z0-9-]{1,32}`. |
| `header-out-of-order` | A header record appeared out of the fixed `HKMAP`, `NAME`, `GRID` order, or a header-kind keyword appeared again in the body. |
| `missing-end-record` | The file ended without ever reaching an `END` record. |

One more rule identifier exists outside this list, declared on
`MapCanonicalizer` itself rather than on `MapLoadException.Rules`, because it
belongs to the canonicaliser, not the tokenizer: `duplicate-canonical-record`
(`MapCanonicalizer.DuplicateRecordRule`), thrown when two body records
canonicalise to the exact same `(kindOrdinal, field1, field2, …)` key — see
"Canonical order" below.

## Canonical order

`MapCanonicalizer.Canonicalize` turns the tokenizer's file-order output into
one canonical sequence, in two steps, so that two files describing the same
map but typed differently produce identical output:

1. Every `WALL` and `DOOR` has its endpoints normalised to lexicographic
   ascending order: if the written `(x1, y1)` is greater than the written
   `(x2, y2)` — comparing `x` first, then `y` — the two endpoints swap. A
   segment authored right-to-left canonicalises identically to the same
   segment authored left-to-right. `DOOR`'s `hinge` and `state` fields are
   never touched by this swap; see the `DOOR` section above for what that
   means for `hinge`.
2. The five body kinds — `WALL` through `OBJECTIVE`, ordinals 1 through 5 —
   are sorted ascending by `(kindOrdinal, field1, field2, …)`, using exactly
   the field order this document lists for each kind. The three header
   records and the trailing `END` are carried through unchanged, in their
   already-fixed relative positions.

Because `System.Collections.Generic.List<T>.Sort` is introsort and is **not**
a stable sort, two records that compare exactly equal under
`(kindOrdinal, field1, field2, …)` would have an order that is an
implementation detail of the sort algorithm rather than a property of the
input — which would break the promise that the same map always canonicalises
to the same byte stream. `MapCanonicalizer` closes that hole by treating a
tied key as a hard error: `DetectDuplicates` walks the sorted body once and
throws `MapCanonicalizer.DuplicateRecordRule` the moment two adjacent
records compare equal, naming both line numbers. This is a narrower,
earlier version of `MapValidator`'s own broader "no duplicate record of any
kind" rule — this check exists purely so the sort itself stays a true total
order, not as a substitute for full duplicate validation.

`angle-house.hkmap` is already written in canonical order — a pinned test,
`AngleHouseFixtureTests.FixtureCanonicalisesToItselfByteForByte`, asserts
that canonicalising the parsed fixture produces the exact same byte stream
as encoding the raw tokenized order directly.

## What reaches the content hash

`MapContentHash.Compute` folds an FNV-1a hash over the canonical record
stream — never over the raw file text. The encoding
(`MapContentHash.Encode`) writes, for every record in canonical order, one
byte carrying `(byte)(int)record.Kind`, followed by each of that record's
integer fields as four big-endian bytes, in the exact field order this
document lists for that kind.

Two things are deliberately excluded:

- `NameRecord.Id` — `NAME`'s only field — never contributes anything beyond
  its kind byte. A map's identifier is a label for a file path and a debug
  print, not part of its spatial content, so renaming a file's `NAME` id
  must not silently invalidate every golden hash and replay that cites the
  old one. `MapValidator.cs`'s own remarks pin this as "Decision 1" and cite
  a test, `MapValidatorTests.MapNameDoesNotReachTheContentHash_TwoMapsDifferingOnlyByNameHashIdentically`,
  that asserts two otherwise-identical maps differing only by `NAME` hash to
  the same value.
- `MapRecord.LineNumber` — every record carries the 1-based source line it
  came from, for error messages, but that number is never folded into the
  hash and never participates in canonical sort comparisons. Two records
  differing only in which line they were typed on are the same canonical
  record.

`MapContentHash` folds into the mission content hash alongside
`SandataRuleset.ContentHash`, so an edit to a single wall coordinate moves
the state hash and forces a new golden expectation — the same rule
`CLAUDE.md` section 5 states for any change that would otherwise move
gameplay silently.

## Cross-record validation

`MapValidator.Validate` runs six checks, in this order, against a canonical
record list, and throws `MapLoadException` naming the first one a map
breaks. `MapValidator.Rules` names them:

| Rule identifier | Condition |
| --- | --- |
| `missing-faction-0-spawn` | No `SPAWN` record has `faction 0`. |
| `missing-faction-1-spawn` | No `SPAWN` record has `faction 1`. |
| `spawns-too-close` | Two `SPAWN` records are closer than one body diameter. `MapValidator` uses a body radius of 4.25 world units — declared as `BodyRadiusQuarterWu = 17` (quarter-world-units, so `17/4 = 4.25`), matching `Hukbo.Core.Simulation.CollisionRules.DefaultBodyRadiusRaw`'s numeric value. The check compares squared distances with exact integer arithmetic rather than representing `4.25` or `8.5` as anything but an integer ratio. |
| `objective-indices-not-dense-from-zero` | `OBJECTIVE` indices, sorted, are not exactly `0, 1, 2, …` with no gap and no repeat. This is the one duplicate shape `MapCanonicalizer`'s field-equality comparator cannot see on its own, since two objectives sharing an index at two different coordinates have different field tuples and never collide during canonical sorting. |
| `map-not-fully-enclosed` | A flood fill seeded from every cell on the outer edge of the grid's bounding box reaches a `SPAWN` cell. This check treats a `DOOR` as structural — an intentional, controlled opening is not a hole in the shell — so it rasterises walls and doors together, regardless of a door's authored `state`. |
| `objective-unreachable-from-faction-0` | A faction-0 `SPAWN` cannot reach an `OBJECTIVE` with every `DOOR` treated as passable, regardless of `state`. This check rasterises walls only — a closed door is deliberately not an obstacle for this reachability question, per design section 12's own wording. |

Two of these checks build their own coarse per-cell blocked grid directly
from `GridRecord.CellWu`, independent of `Navigation.NavGrid.CellSizeWu`'s
own fixed constant of `4`, because `MapValidator.cs` has no dependency on
the navigation-baking code and does not assume it has already run.

**A different body radius governs `MapValidator`'s spawn-separation check
than governs navigation baking.** `BodyRadiusQuarterWu = 17` (4.25 wu) above
is a constant private to `MapValidator.cs` and used only for that one check.
`NavBake.Bake`'s `bodyRadiusWu` parameter is a caller-supplied integer with
no fixed value in `Sandata.Core` at all — `Sandata.Core` cannot reference
`Hukbo.Core`'s shared body-radius constant, so every caller of `NavBake.Bake`
supplies its own value. Both call sites found while writing this document
use `5` world units: `PlaceholderBodyRadiusWu` in `src/Sandata.Client/SandataGame.cs`
and `BenchmarkBodyRadiusWu` in `src/Sandata.Headless/NavBenchmark.cs`. These
are two different numbers (4.25 wu versus 5 wu) serving two different
purposes — do not conflate them when reading either file.

## From records to passability: `NavBake.Bake`

`NavBake.Bake` (`src/Sandata.Core/Navigation/NavBake.cs`) turns a map's
`WALL` and `DOOR` records, plus one mover body radius in whole world units,
into a `NavGrid`'s `Passability` array. `NavGrid.CellSizeWu` is a fixed
constant, `4` world units per cell, chosen as a power of two so that
converting a world coordinate to a cell coordinate is an arithmetic right
shift rather than a division. Baking happens in exactly three steps, every
time `Bake` runs:

1. **Rasterize walls and closed doors.** Every `WallRecord`, and every
   `DoorRecord` whose `State` equals `DoorStateClosed` (`0`), is walked with
   an integer supercover algorithm (`SupercoverCells`) that visits every
   cell the segment's line touches, including a cell it only grazes at a
   diagonal corner — so a wall's rasterization never leaves a gap a body
   could slip through at a corner. A wall always wins a conflict: a cell any
   wall segment touches is marked `Blocked` and stays `Blocked` regardless of
   a door record also touching that same cell. An open door (`State 1`) is
   skipped entirely at this step — it contributes nothing to the bake.
2. **Inflate by body radius.** Every cell within the given `bodyRadiusWu`,
   converted to whole cells and measured by Chebyshev (king-move) distance —
   not Euclidean, so this step stays free of `double` and `Math.Sqrt`, both
   of which are banned tokens in `Sandata.Core` — of an originally
   wall-blocked cell is itself forced `Blocked`. This is the step that turns
   "a single point fits here" into "a body of the baked radius fits here",
   and it is also what narrows a doorway: the design document's own worked
   example describes a 0.9-metre-wide doorway that is three cells wide
   before inflation and becomes one cell wide after it, because the
   flanking walls' inflation eats the two side cells and only the centre
   cell survives. Inflation sources are snapshotted before any cell is
   mutated, so a cell that becomes `Blocked` only because of inflation is
   never itself treated as a further inflation source — the baked radius
   never grows past exactly the body radius from a real wall.
3. **Keep surviving door tags.** A closed door's own cells keep the `Door`
   flag from step 1 unless step 1 already gave that cell to a wall.
   Inflation from a nearby wall does not demote a surviving door cell to
   `Blocked`, because inflation only ever originates from a cell step 1
   marked `Blocked` by a wall — a closed door cell is never itself an
   inflation source. A door cell that survives both steps is passable to
   the pathfinding planner at high cost and impassable to an actual mover
   until the door's state changes to open, at which point a later rebake
   turns the affected cells `Open`.

`Bake` resets the whole `Passability` array to `Open` before doing any of
this, so calling it again — after a door's state changes, or with a
different body radius — always starts from a clean slate rather than
compounding a stale bake on top of an earlier one. Every array `Bake`
writes is derived, not authoritative, state: nothing it produces is ever
stored in a save file, folded into the state hash, or captured in a
snapshot. It is cheap enough to rebuild from the map's own records and the
body radius that it always is, on load and on every door-state change.

## A format change means a new filename, not an edit in place

This is not a single quoted sentence found anywhere in the source read for
this document — no such sentence was found. It is stated here as the
practical implication of how `MapContentHash` is used elsewhere in the
repository, and it is written that way rather than as a quoted rule:

`angle-house.hkmap`'s content hash is pinned as a literal test expectation —
`AngleHouseFixtureTests.FixtureContentHashIsPinned` asserts
`MapContentHash.Compute(canonical)` equals `11909359227906322716UL` for this
exact file, a value that test's own doc comment says "was measured from a
real run of this test, never calculated by hand." `MapContentHash` folds
into the mission content hash used by Sandata's replay and determinism
tests, exactly as `CLAUDE.md` section 5 requires for the state hash. Editing
`angle-house.hkmap`'s spatial content in place — moving a wall, adding a
cover object, changing a door's state — changes what `MapContentHash.Compute`
returns for that file, which breaks the pinned literal above and, more
importantly, silently moves every recorded golden state hash and replay
baseline that was ever taken against the old file, with the map's own
`NAME` giving no signal that anything changed underneath it.

The safe authoring practice that falls out of this is: when a map's spatial
content needs to differ from what is already shipped — including a
genuinely new map, or a materially different version of an existing one —
write a new `.hkmap` file, with its own `NAME` id, rather than editing a
file whose content hash is already cited by a pinned test or a recorded
baseline. A pure rename of an unchanged file's `NAME` id is explicitly safe,
by the same "Decision 1" reasoning in `MapValidator.cs` described above,
since `NAME` never reaches the content hash at all.

## `angle-house.hkmap`, quoted and annotated

This is the full, current text of
`tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap` — the only map that
exists in this repository at the time of writing — followed by a line-by-line
annotation of what each record contributes to the map.

```
HKMAP 1
NAME angle-house
GRID 640 720 4
WALL 0 0 0 720 1
WALL 0 0 640 0 1
WALL 0 640 300 640 1
WALL 0 720 640 720 1
WALL 60 260 200 340 2
WALL 60 460 60 580 1
WALL 60 460 100 460 1
WALL 60 580 180 580 1
WALL 120 120 320 220 2
WALL 140 460 180 460 1
WALL 160 400 340 520 2
WALL 180 460 180 580 1
WALL 320 220 520 160 2
WALL 340 640 640 640 1
WALL 380 380 560 300 2
WALL 420 60 420 120 1
WALL 420 60 600 60 1
WALL 420 160 420 200 1
WALL 420 200 600 200 3
WALL 600 60 600 200 1
WALL 640 0 640 720 1
DOOR 100 460 140 460 0 0
DOOR 300 640 340 640 0 0
DOOR 420 120 420 160 1 1
COVER 200 200 260 240 49152 8192 1
COVER 260 100 340 140 16384 8192 2
COVER 440 440 520 500 49152 8192 1
COVER 500 540 560 600 0 32768 1
SPAWN 0 296 690 49152
SPAWN 0 320 690 49152
SPAWN 1 120 520 49152
SPAWN 1 500 120 16384
OBJECTIVE 0 500 120 48
OBJECTIVE 1 120 520 48
END
```

Line-by-line, in the file's own order (this file happens to already be in
canonical order, per the test cited above, so file order and canonical
order agree here):

- `HKMAP 1` — format version 1, must be line 1.
- `NAME angle-house` — the map's id. Never reaches the content hash.
- `GRID 640 720 4` — the map is 640 by 720 world units (40 by 45 metres, at
  16 wu per metre), with a 4 wu navigation cell — 160 by 180 cells,
  well inside the 512-cell-per-axis limit.
- The next four `WALL` lines (`0 0 0 720 1`, `0 0 640 0 1`, `0 640 300 640 1`,
  `0 720 640 720 1`) plus `WALL 640 0 640 720 1` further down form the
  map's outer shell: the left edge (`x = 0`), the top edge (`y = 0`), a
  partial bottom edge stopping at `x = 300`, the full bottom edge at
  `y = 720`, and the right edge (`x = 640`). The gap in the bottom edge
  between `x = 300` and `x = 340` is closed by a `DOOR`, not a `WALL` — see
  below.
- The five diagonal (non-axis-aligned) `WALL` lines — `60 260 200 340 2`,
  `120 120 320 220 2`, `160 400 340 520 2`, `320 220 520 160 2`, and
  `380 380 560 300 2` — are the "angle" in `angle-house`: interior walls cut
  at oblique angles rather than laid out on a rectangular grid, each using
  material `2`. A pinned test,
  `AngleHouseFixtureTests.FixtureHasExactlyFiveWallsNeitherHorizontalNorVertical`,
  asserts there are exactly five of them.
- The remaining axis-aligned `WALL` lines with material `1` — around
  `x = 60`/`y = 460..580`, around `x = 180`/`y = 460..580`, and around
  `x = 420`/`x = 600`/`y = 60..200` — form the walls of two interior rooms,
  one in the lower-left area of the map and one in the upper-right area.
- `WALL 420 200 600 200 3` is the map's single material-`3` wall — the one
  wall a pinned test, `AngleHouseFixtureTests.FixtureHasOneMaterial3WallNotOnTheOuterShell`,
  confirms touches none of the four outer boundary lines, making it an
  interior wall rather than part of the shell.
- `DOOR 100 460 140 460 0 0` closes the gap between the two `x = 60..100`
  and `x = 140..180` wall segments at `y = 460` — a closed door (`state 0`)
  into the lower-left room.
- `DOOR 300 640 340 640 0 0` closes the gap in the bottom outer wall between
  `x = 300` and `x = 340` — a closed door (`state 0`) that is itself part of
  the outer shell.
- `DOOR 420 120 420 160 1 1` closes the gap between the `y = 60..120` and
  `y = 160..200` wall segments at `x = 420` — an open door (`state 1`) into
  the upper-right room.
- `COVER 200 200 260 240 49152 8192 1` places a cover object over the box
  from `(200, 200)` to `(260, 240)`, oriented toward `49152` Bam (north),
  with a 45-degree half-arc (`8192` of `65536`, i.e. a 90-degree total
  arc), at height `1`.
- `COVER 260 100 340 140 16384 8192 2` places a second cover object,
  oriented toward `16384` Bam (south), same half-arc, at height `2`.
- `COVER 440 440 520 500 49152 8192 1` places a third cover object in the
  upper-right area, also oriented north, at height `1`.
- `COVER 500 540 560 600 0 32768 1` places the map's one 360-degree cover
  object (`arcHalfBam` at the maximum, `32768`), oriented toward `0` Bam
  (east, though direction is moot at the 360-degree maximum), at height `1`
  — the one record a pinned test,
  `AngleHouseFixtureTests.FixtureHasExactlyOne360DegreeCover`, confirms is
  unique on this map.
- `SPAWN 0 296 690 49152` and `SPAWN 0 320 690 49152` place two faction-0
  spawn points near the bottom of the map, both facing `49152` Bam (north)
  — into the map, toward the interior.
- `SPAWN 1 120 520 49152` places a faction-1 spawn inside the lower-left
  room, facing north.
- `SPAWN 1 500 120 16384` places a second faction-1 spawn inside the
  upper-right room, facing `16384` Bam (south).
- `OBJECTIVE 0 500 120 48` places objective index `0` at `(500, 120)` with a
  48 wu radius — the exact same coordinate as the second faction-1 `SPAWN`
  above. `OBJECTIVE 1 120 520 48` places objective index `1` at `(120, 520)`
  with the same radius — the exact same coordinate as the first faction-1
  `SPAWN`. Both observations are read directly off the coordinates in this
  file; this document draws no further conclusion about mission design from
  that coincidence beyond stating it, since no mission-design source was
  read while writing this document.
- `END` — terminates the file. No fields.

Loading this file exercises every non-degenerate case this document
describes: a header in the required order, axis-aligned and diagonal walls,
one open and two closed doors, four cover objects including one 360-degree
case, spawns for both factions, two objectives with dense indices, and a
fully enclosed shell with every objective reachable from a faction-0 spawn
with doors treated as passable — which is exactly the shape
`AngleHouseFixtureTests.FixturePassesEveryMapValidatorCrossRecordRule`
exists to confirm.
