# Ranged Audio — the naming contract, the trigger path, and how eighty generated files must be spent

**Date:** 2026-08-07
**Status:** Research. Read-only survey of the existing audio subsystem plus one
recommended allocation. This document does not authorize implementation and it
is not a design document.
**Scope:** Everything a planner needs in order to specify eighty ElevenLabs
generations — twenty each for the Bangkaw, the Busog, the Sumpit, and the
imported arquebus — without guessing at how the game names, catalogues, loads,
selects, or throttles a sound.

All file paths are relative to the repository root. Line numbers are from the
`ranged-units` worktree at
`C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\ranged-units`, whose
`src/` tree matches `main` at `ae7bf04`.

> **Discovery-tool note.** `CLAUDE.md` section 8 requires the `tokensave` MCP
> tools for code discovery. Neither the `tokensave` server nor
> `codebase-memory-mcp` was exposed to this session's tool set; two explicit
> tool searches returned no matching tools. Every claim below therefore comes
> from reading files directly with `Read`, `Grep`, and `Bash`, and every claim
> carries a `file:line` citation so it can be checked. The sibling research
> document `2026-08-07-RANGED-SIM-MECHANICS.md` records the same absence, so
> this is a property of the session rather than of one agent.

## Contents

1. [The existing slot list and which slots have files](#1-the-existing-slot-list-and-which-slots-have-files)
2. [The file naming contract](#2-the-file-naming-contract)
3. [HitClass, and why melee lands on ten files per weapon](#3-hitclass-and-why-melee-lands-on-ten-files-per-weapon)
4. [The trigger path, from a Core event to a sound](#4-the-trigger-path-from-a-core-event-to-a-sound)
5. [Voice limiting and capacity](#5-voice-limiting-and-capacity)
6. [The recommended twenty-file allocation per ranged weapon](#6-the-recommended-twenty-file-allocation-per-ranged-weapon)
7. [What `scripts/sfx.ps1` needs](#7-what-scriptssfxps1-needs)
8. [Every audio test that constrains this](#8-every-audio-test-that-constrains-this)

## 1. The existing slot list and which slots have files

There are thirteen slots. They are declared twice, and the two declarations must
agree: the enum `GameSoundId` at `src/Hukbo.Client/Audio/AudioTypes.cs:8-29`
gives each slot a stable numeric value, and the fixed list
`SoundCatalog.AllSounds` at `src/Hukbo.Client/Audio/SoundCatalog.cs:32-47` gives
the display and iteration order. The list is written out by hand rather than
derived from `Enum.GetValues` precisely so that the order the panel shows never
depends on reflection order, and
`SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce`
(`tests/Hukbo.Client.Tests/SoundCatalogTests.cs:10-22`) is what fails when
somebody adds a member to one and forgets the other.

The canonical file name for each slot comes from `SoundCatalog.GetBaseName` at
`src/Hukbo.Client/Audio/SoundCatalog.cs:57-77`.

The counts below were taken by listing
`src/Hukbo.Client/Content/Audio/` and counting the files whose names begin with
each slot's base name. The folder holds **70 `.wav` files** plus three Markdown
files — `GENERATED.md`, `PENDING-SOUNDS.md`, and `README.md` — which is where
the figure of seventy-three directory entries comes from.

| # | `GameSoundId` | Base name | Hit-location driven | Files on disk | Status |
| ---: | --- | --- | --- | ---: | --- |
| 0 | `AttackKampilan` | `attack-kampilan` | yes | 10 | READY, all six classes covered |
| 1 | `AttackWasay` | `attack-wasay` | yes | 10 | READY, all six classes covered |
| 2 | `AttackKalis` | `attack-kalis` | yes | 10 | READY, all six classes covered |
| 3 | `AttackItak` | `attack-itak` | yes | 10 | READY, all six classes covered |
| 4 | `Death` | `death` | no | 10 | READY, ten numbered takes |
| 5 | `VictoryBlue` | `victory-blue` | no | 1 | READY, bare single only |
| 6 | `VictoryRed` | `victory-red` | no | 1 | READY, bare single only |
| 7 | `Draw` | `draw` | no | 1 | READY, bare single only |
| 8 | `UiClick` | `ui-click` | no | 1 | READY, bare single only |
| 9 | `ClashShieldKampilan` | `clash-shield-kampilan` | no | 4 | READY, four numbered takes |
| 10 | `ClashShieldWasay` | `clash-shield-wasay` | no | 4 | READY, four numbered takes |
| 11 | `ClashShieldKalis` | `clash-shield-kalis` | no | 4 | READY, four numbered takes |
| 12 | `ClashShieldItak` | `clash-shield-itak` | no | 4 | READY, four numbered takes |

Every slot has at least one file, so no slot is `MISSING` today. The per-class
breakdown for the four hit-location driven slots is:

| Slot | skull | neck | ribcage | gut | limb | extremity | total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `attack-kampilan` | 2 | 2 | 2 | 1 | 2 | 1 | 10 |
| `attack-wasay` | 2 | 1 | 1 | 1 | 3 | 2 | 10 |
| `attack-kalis` | 1 | 2 | 2 | 3 | 1 | 1 | 10 |
| `attack-itak` | 2 | 2 | 1 | 1 | 1 | 3 | 10 |

Two things about that table are worth carrying into the ranged plan. First,
every class of every weapon has at least one take, so the fallback chain
described in section 3 never actually fires under the shipped content — it is
insurance, not a working part of the mix. Second, each weapon's heaviest class
is the one its default prompt in `scripts/sfx.ps1` describes: the Kalis prompt
at `scripts/sfx.ps1:189-193` asks for a narrow blade in a belly and the Kalis
has three `gut` takes, the Wasay prompt at `scripts/sfx.ps1:184-188` asks for an
axe cleaving a shoulder and the Wasay has three `limb` takes. The prompt table
and the take distribution were authored together.

### 1.1 Two slots that are proposed but do not exist

`src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` lists two weapon-clash slots
that were designed and deliberately not built — `clash-blade-hard` and
`clash-blade-soft` — and four swing slots that were never designed at all. None
of the six exists in `GameSoundId`. The swing entry matters directly to the
ranged work, because it records the reasoning that a per-attack cue which fires
before resolution "is the single most likely sound in the game to become
continuous noise". A ranged release cue is exactly that shape, and section 6
answers the objection rather than ignoring it.

### 1.2 The audio folder is not part of the MonoGame content pipeline

`src/Hukbo.Client/Content/Content.mgcb` is 181 lines and contains **zero**
occurrences of `audio` or `wav` (verified by `grep -c -i`). The WAV files reach
the build output through a plain copy rule at
`src/Hukbo.Client/Hukbo.Client.csproj:30-33`, which the file's own comment
describes as "Sound files the owner drops in are copied verbatim: the audio
system reads them at runtime instead of going through the content pipeline".

The practical consequence for eighty new files is that **no `.mgcb` edit is
required and no content build step is involved**. Dropping eighty correctly
named WAV files into `src/Hukbo.Client/Content/Audio/` is the whole of the
content change. `MonoGameSoundPlayer.Load` reads the directory at startup —
`src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs:39-76` — and
`SoundEffect.FromStream` at `MonoGameSoundPlayer.cs:219` parses each file,
which is why the format must be uncompressed PCM WAV and nothing else
(`SoundCatalog.cs:10-14`).

## 2. The file naming contract

### 2.1 The generator side

`scripts/sfx.ps1` builds every file name it writes in one function, and that
function is the readable statement of the pattern. Quoted in full from
`scripts/sfx.ps1:274-297`:

```powershell
function Get-SlotPath {
    <#
        Builds "<slot>[-<class>][-NN].wav". The hit class lives in the file name
        rather than in a code-side table so the mapping between a body part and
        the sound it makes cannot silently drift from the files on disk.
    #>
    param(
        [Parameter(Mandatory)] [string] $SlotName,
        [Parameter(Mandatory)] [string] $Directory,
        [string] $ClassName,
        [int] $VariantIndex
    )

    $name = $SlotName
    if (-not [string]::IsNullOrWhiteSpace($ClassName)) {
        $name += "-$ClassName"
    }

    if ($VariantIndex -gt 0) {
        $name += '-{0:D2}' -f $VariantIndex
    }

    return Join-Path $Directory "$name.wav"
}
```

Both bracketed parts are independently optional in the script, which means the
script can write four shapes: `<slot>.wav`, `<slot>-<class>.wav`,
`<slot>-NN.wav`, and `<slot>-<class>-NN.wav`. Only three of those four are
recognised by the game. The `<slot>-<class>.wav` form — a class with no index —
matches nothing on the reading side, because a class-scoped name is only ever
looked up through a numbered prefix. Section 2.6 covers that trap.

### 2.2 What a slot is

A **slot** is one member of `GameSoundId`
(`src/Hukbo.Client/Audio/AudioTypes.cs:8-29`) together with the lowercase
kebab-case token `SoundCatalog.GetBaseName` returns for it
(`src/Hukbo.Client/Audio/SoundCatalog.cs:57-77`). It is the unit the game
addresses a sound by. The base name is an internal file-system key; the
catalog's own documentation comment at `SoundCatalog.cs:49-56` is explicit that
it is "never shown to a spectator" and that the player-facing pair form required
by the historical accuracy policy is built elsewhere.

`SoundCatalogTests.GetFileName_IsUniqueLowercaseKebabWavForEverySlot`
(`tests/Hukbo.Client.Tests/SoundCatalogTests.cs:24-44`) enforces four properties
of a base name: it ends in `.wav` once the extension is appended, it is entirely
lowercase, it contains no space, it contains no underscore, and it is unique
across slots. A ranged slot named `attack-bangkaw` satisfies all five; one named
`attack_bangkaw` or `Attack-Bangkaw` fails the build.

### 2.3 What a class is

A **class** is one member of `HitClass`
(`src/Hukbo.Client/Audio/HitClass.cs:13-21`) rendered through
`HitClassCatalog.GetToken` (`src/Hukbo.Client/Audio/HitClass.cs:76-89`), which
produces exactly one of `skull`, `neck`, `ribcage`, `gut`, `limb`, `extremity`.
A class token appears in a file name only for a slot where
`SoundCatalog.IsHitLocationDriven` returns `true`
(`SoundCatalog.cs:90-94`), which today is exactly the four weapon attack slots.

`SoundCatalog.GetVariantPrefix` at `SoundCatalog.cs:103-113` is the single place
the class token is joined to the base name, and it throws
`ArgumentException` for a slot that is not hit-location driven
(`SoundCatalog.cs:105-110`). The whole file name for a class variant is built by
`SoundCatalog.GetVariantFileName` at `SoundCatalog.cs:127-137`, and the
classless form by `SoundCatalog.GetSlotVariantFileName` at
`SoundCatalog.cs:143-150`.

### 2.4 What NN is, and how a variant is chosen at playback

`NN` is a **one-based, exactly-two-digit** ordering key. Three separate rules
constrain it, all of them in `SoundLibrary.TryParseVariantIndex` at
`src/Hukbo.Client/Audio/SoundLibrary.cs:325-353`:

- the file must end in `.wav`, compared with `OrdinalIgnoreCase`
  (`SoundLibrary.cs:332-337`);
- the part after the prefix must be **exactly**
  `SoundCatalog.VariantIndexDigits` characters long, and that constant is `2`
  (`SoundCatalog.cs:26`), so `-1.wav` and `-100.wav` are both rejected
  (`SoundLibrary.cs:346`);
- it must parse under `NumberStyles.None` and be strictly greater than zero, so
  `-00.wav` is rejected (`SoundLibrary.cs:347-352`).

The practical range is therefore `01` through `99`, which is exactly the range
`scripts/sfx.ps1`'s `-Index` parameter validates at `scripts/sfx.ps1:98`.

**`NN` is not the number the game plays.** It is only a sort key. The pipeline
is:

1. `SoundLibrary.NormalizeAndSort` (`SoundLibrary.cs:271-281`) sorts the whole
   directory listing with `StringComparer.Ordinal`, so discovery does not depend
   on the operating system's directory enumeration order.
2. `SoundLibrary.FindNumberedMatches` (`SoundLibrary.cs:309-323`) collects every
   file matching the prefix and re-orders them ascending by the parsed index with
   a stable `OrderBy`.
3. That produces a `List<string>` of file names. `MonoGameSoundPlayer.Load`
   (`MonoGameSoundPlayer.cs:59-67`) loads them in that order into a
   `SoundEffect[]` keyed by `(GameSoundId, HitClass?)`.
4. At playback, `SoundDirector.Resolve` asks
   `Player.GetVariantCount(sound, hitClass)`
   (`src/Hukbo.Client/Audio/SoundDirector.cs:214`) and hands the count to
   `SoundVariantSelector.Select(tick, sourceEntityId, variantCount)`
   (`SoundDirector.cs:215`).
5. `SoundVariantSelector.Select`
   (`src/Hukbo.Client/Audio/SoundVariantSelector.cs:20-32`) returns a
   **zero-based ordinal into that array**, not a file number. It mixes the tick
   and the source entity ID with the golden-ratio constant at
   `SoundVariantSelector.cs:13`, seeds a `SplitMix64`, and draws
   `NextInt(variantCount)`. A count of zero or one returns zero without touching
   the generator (`SoundVariantSelector.cs:24-27`).

Two consequences the planner should write into the plan. **Gaps in the numbering
are legal but they shift every file after the gap.** Files `01`, `03`, `07`
resolve to a list of length three at ordinals 0, 1, 2, so deleting `02` from a
set of eight silently changes which take a given tick plays. **Nothing about
this reaches a hash.** The selector reads `SplitMix64` from `Hukbo.Core`
(`SoundVariantSelector.cs:1`) so that a replay requests the same take without
storing any state, but the draw happens in the Client and no simulation value
depends on it. Adding, removing, or renumbering an audio file cannot move a
state hash or an event hash.

### 2.5 The bare single, and the fallback chain

A file named exactly `<slot>.wav` with no index is the **bare single**. It is
found by `SoundLibrary.FindExactMatch` (`SoundLibrary.cs:283-299`) and it is a
last resort in two different places:

- for a classless slot, `SoundLibrary.ResolveVariants` prefers numbered files and
  falls back to the bare single only when there are none
  (`SoundLibrary.cs:118-131`);
- for a hit-location driven slot, `ResolveClassVariant`
  (`SoundLibrary.cs:194-229`) tries the class's own files, then each class in
  `HitClassCatalog.GetFallbackChain` in order (`SoundLibrary.cs:210-221`), then
  the bare single (`SoundLibrary.cs:223-226`), and only then reports `Missing`.

The chain itself is a fixed table at `HitClass.cs:100-113`: extremity falls back
to limb then ribcage, skull to neck then ribcage, neck and gut and limb each
fall back directly to ribcage, and ribcage has no class fallback at all. Ribcage
is therefore the universal backstop, and a hit-location driven slot with **no
ribcage take and no bare single** can resolve `Missing` for a class even though
its other five classes are full.

### 2.6 What happens to a file that does not match

Nothing, silently. `SoundLibrary.Resolve` and `SoundLibrary.ResolveVariants`
both walk the catalog and pull matching names out of the directory listing; a
name nobody asked for is simply never read. There is no warning, no log line,
and no panel row. `SoundLibraryTests.Resolve_IgnoresUnsupportedExtensionsAndUnknownNames`
(`tests/Hukbo.Client.Tests/SoundLibraryTests.cs:93-105`) and
`SoundLibraryTests.ResolveVariants_IgnoresAFileWhoseIndexIsNotExactlyTwoDigits`
(`SoundLibraryTests.cs:201-215`) pin that behaviour.

The specific ways eighty new files could be silently wasted are worth listing,
because every one of them costs a paid generation:

| Bad name | Why it is ignored |
| --- | --- |
| `attack-busog-skull.wav` | class token with no index; the reader only looks up a class through a numbered prefix (`SoundLibrary.cs:255-258`) |
| `attack-busog-skull-1.wav` | one digit, not two (`SoundLibrary.cs:346`) |
| `attack-busog-skull-001.wav` | three digits (`SoundLibrary.cs:346`) |
| `attack-busog-skull-00.wav` | index must be greater than zero (`SoundLibrary.cs:352`) |
| `release-busog-01.wav` before the slot exists in `GameSoundId` | the reader iterates `SoundCatalog.AllSounds`, so a file for an undeclared slot is never searched for (`SoundLibrary.cs:245`) |
| `attack-busog-skull-01.wav` while `IsHitLocationDriven(AttackBusog)` is `false` | the reader registers that slot only under `(sound, null)` (`SoundLibrary.cs:260-265`), so every class file is invisible |
| an MP3 or Ogg renamed to `.wav` | discovered, then fails `SoundEffect.FromStream`; the slot reports `FAILED` rather than `MISSING` (`MonoGameSoundPlayer.cs:212-233`) |

Letter case never matters. Comparisons at `SoundLibrary.cs:292`, `:334`, and
`:340` are all `OrdinalIgnoreCase`, so `Attack-Busog-Skull-01.WAV` resolves.

## 3. HitClass, and why melee lands on ten files per weapon

### 3.1 The six values

`HitClass` is declared at `src/Hukbo.Client/Audio/HitClass.cs:13-21` and has six
members with fixed numeric values:

| Value | Member | File token |
| ---: | --- | --- |
| 0 | `Skull` | `skull` |
| 1 | `Neck` | `neck` |
| 2 | `Ribcage` | `ribcage` |
| 3 | `Gut` | `gut` |
| 4 | `Limb` | `limb` |
| 5 | `Extremity` | `extremity` |

The tokens come from `HitClassCatalog.GetToken` at `HitClass.cs:76-89`. The
fixed iteration order is the hand-written list `HitClassCatalog.All` at
`HitClass.cs:36-44`, which the comment above it states is "never derived from
`Enum.GetValues`" for the same reason `SoundCatalog.AllSounds` is not.

`HitClass` lives in `Hukbo.Client`, not in `Hukbo.Core`. The simulation has
never heard of it. It is purely an acoustic grouping invented by the audio
layer.

### 3.2 How a melee weapon maps a hit to a class

The authoritative value is `BodyPart`, declared in
`src/Hukbo.Core/Combat/BodyPart.cs:19-33` with thirteen members numbered 1
through 13. Its documentation comment is explicit that hit location "is metadata
only: it does not change damage, health capacity, cooldown, future actions, or
death", and that the numeric IDs are part of the deterministic replay contract.

`HitClassCatalog.FromBodyPart` at `HitClass.cs:51-68` collapses those thirteen
into six:

| `HitClass` | `BodyPart` values folded in |
| --- | --- |
| `Skull` | `Head`, `Face` |
| `Neck` | `Neck` |
| `Ribcage` | `Chest` |
| `Gut` | `Abdomen` |
| `Limb` | `Shoulder`, `Thigh`, `Knee` |
| `Extremity` | `WeaponArm`, `ShieldArm`, `Shin`, `Hands`, `Feet` |

An undefined `BodyPart` throws rather than defaulting (`HitClass.cs:64-67`), and
`HitClassCatalogTests.FromBodyPart_CoversEveryDeclaredBodyPartExactlyOnce`
(`tests/Hukbo.Client.Tests/HitClassCatalogTests.cs:30-39`) fails the build if a
fourteenth body part is added without a class.

Critically, the class is **not** read off the event. `SoundDirector.Ingest`
derives it from the slot that was mapped:

```csharp
var hitClass = SoundCatalog.IsHitLocationDriven(sound)
    ? HitClassCatalog.FromBodyPart(battleEvent.HitLocation!.Value)
    : (HitClass?)null;
```

That is `src/Hukbo.Client/Audio/SoundDirector.cs:141-143`, and the
twelve-line comment immediately above it at `SoundDirector.cs:129-140` explains
why: a shield-blocked attack still carries a hit location, so deriving the class
from the event would look a classless clash slot up as `(slot, HitClass.Skull)`,
a key that is registered nowhere, which resolves `Missing` forever with no
crash, no failing test, and no complaint in the panel. **A new classless ranged
slot therefore needs no change in the director at all**, and reverting that
derivation would silence every classless slot whose event happens to carry a hit
location.

### 3.3 Why melee lands on ten files per weapon

Ten is not a rule written down anywhere in code. It is what the shipped content
happens to be, and the reasoning behind it is reconstructible from three
measurable facts.

**Fact one: six classes need coverage, and the classes are not equally likely.**
The shared base target-weight profile at
`src/Hukbo.Core/Combat/PhilippineCombatPreset.cs:35-50` — restated unchanged in
V4 at `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:58-73` — assigns
thirteen weights summing to 99. Folding them through the class map of section
3.2 gives the share of blows each acoustic class receives before any per-weapon
override:

| `HitClass` | Summed weight | Share |
| --- | ---: | ---: |
| `Extremity` | 35 | 35.4% |
| `Limb` | 24 | 24.2% |
| `Skull` | 17 | 17.2% |
| `Neck` | 9 | 9.1% |
| `Chest` → `Ribcage` | 7 | 7.1% |
| `Abdomen` → `Gut` | 7 | 7.1% |

Six classes at one take each would be six files and would repeat audibly on the
two classes that carry sixty per cent of all blows. Ten files buy one take
everywhere plus a second or third on whatever a given weapon hits most, which is
exactly the distribution the table in section 1 shows.

**Fact two: the design research puts the repetition threshold at three to five
takes.** `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` records that "design
research suggests 3 to 5 numbered takes per slot before repetition becomes
audible, and that at 200 agents individual identity dissolves into texture above
roughly 4 to 6 concurrent impacts anyway". Ten takes spread over six classes
puts the busiest class at two or three, which lands inside that band, and spends
nothing on deepening a class a spectator will never hear twice in a row.

**Fact three: the fallback chain makes thin classes safe, so nothing forces a
tenth file.** Because `ResolveClassVariant` (`SoundLibrary.cs:194-229`)
substitutes down the chain, a weapon could ship with one ribcage take and be
audible for every class. Ten is therefore a quality choice rather than a
coverage requirement, and a ranged weapon is free to choose a different number.

**Add four for the shield clash.** Each weapon also owns a classless
`clash-shield-<weapon>` slot with four numbered takes. That brings the true
per-melee-weapon total to fourteen files, not ten: forty attack takes plus
sixteen clash takes across the four weapons, which with ten deaths and four
single-file cues is the seventy WAV files on disk.

## 4. The trigger path, from a Core event to a sound

### 4.1 How the Client learns an attack happened

`Hukbo.Core` cannot call `Hukbo.Client`. There is no callback, no event handler,
no observer interface, and no dependency in that direction — `CLAUDE.md` section
3 forbids it and the assembly reference does not exist.

The mechanism is **pull, driven by the Client's own loop over an object it
owns**. `ArenaGame` holds the `BattleSimulation` instance, calls
`AdvanceOneTick()` on it, and then reads a read-only list the simulation exposes
as a property:

```csharp
public IReadOnlyList<BattleEvent> LastEvents => _lastEvents;
```

That is `src/Hukbo.Core/Simulation/BattleSimulation.cs:282`, and its remarks at
`BattleSimulation.cs:276-281` state that "the returned collection is owned by the
simulation and is overwritten by a future `AdvanceOneTick` call once its backing
buffer comes back around for reuse. Callers read it within the tick that
produced it and never retain it."

The simulation alternates between two buffers so that a caller still holding the
previous tick's list keeps seeing unchanged data. The write target is chosen at
`BattleSimulation.cs:613-614` and the swap happens at the end of the tick at
`BattleSimulation.cs:652-660`, where a tick with no events exposes a shared
`EmptyEvents` instance instead so an empty tick allocates nothing.

The audio layer is one of several consumers of that same list. The others —
the event feed, the battle report, hit effects, blood, swings, clash effects,
trample, and dust — all go through `PresentationCoordinator.IngestTick` at
`src/Hukbo.Client/Presentation/PresentationCoordinator.cs:139-146`. Sound is
deliberately not among them; it is called separately, one line later.

### 4.2 The ordered call chain

Every function in order, with its call site and its definition.

**Once at startup**

| # | Call | Where |
| ---: | --- | --- |
| 1 | `MonoGameSoundPlayer.Load(SoundLibrary.GetDefaultDirectoryPath())` | called `src/Hukbo.Client/ArenaGame.cs:595-596`, defined `src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs:39` |
| 2 | `SoundLibrary.ListFileNames` | `MonoGameSoundPlayer.cs:43`, defined `src/Hukbo.Client/Audio/SoundLibrary.cs:28` |
| 3 | `SoundLibrary.Resolve` — raw, pre-fallback binding per slot | `MonoGameSoundPlayer.cs:44`, defined `SoundLibrary.cs:65` |
| 4 | `SoundLibrary.ResolveVariants` — fallback-substituted list per `(slot, class)` | `MonoGameSoundPlayer.cs:45`, defined `SoundLibrary.cs:93` |
| 5 | `MonoGameSoundPlayer.LoadEffects` → `SoundEffect.FromStream` | `MonoGameSoundPlayer.cs:59`, `:219` |
| 6 | `SoundDirector.AttachPlayer` | called `ArenaGame.cs:597`, defined `src/Hukbo.Client/Audio/SoundDirector.cs:73` |
| 7 | `SoundDirector.LogBindings` — one `assets.sound.scanned` line plus one warning per broken slot | `SoundDirector.cs:77`, defined `SoundDirector.cs:297` |

**Once per rendered frame**

| # | Call | Where |
| ---: | --- | --- |
| 8 | `SoundDirector.BeginFrame(gameTime.ElapsedGameTime.TotalSeconds)` | called `ArenaGame.cs:666`, defined `SoundDirector.cs:101` |
| 9 | `SoundCueBudget.BeginFrame` — clears both counters | `SoundDirector.cs:103`, defined `src/Hukbo.Client/Audio/SoundCueBudget.cs:53` |
| 10 | `SoundVoiceLedger.Advance` — retires voices whose clips finished | `SoundDirector.cs:104`, defined `src/Hukbo.Client/Audio/SoundVoiceLedger.cs:42` |

Note the ordering hazard the plan must respect: `BeginFrame` runs at
`ArenaGame.cs:666`, near the top of `Update`, while the simulation is advanced
much later at `ArenaGame.cs:944`. The budget is therefore per rendered frame and
not per tick, and at high playback speed several ticks share one frame's budget.

**Once per simulation tick**

| # | Call | Where |
| ---: | --- | --- |
| 11 | `ArenaGame.AdvanceSimulation` | called `ArenaGame.cs:944`, defined `ArenaGame.cs:1512` |
| 12 | `BattleSimulation.AdvanceOneTick()` | called `ArenaGame.cs:1542`, defined `src/Hukbo.Core/Simulation/BattleSimulation.cs:599` |
| 13 | stage 10, `GatherAndCommitAttacks` | called `BattleSimulation.cs:640`, defined `BattleSimulation.cs:3579` |
| 14 | `BattleSimulation.AddAttackEvent` — one call per accepted attack proposal | called `BattleSimulation.cs:3766`, defined `BattleSimulation.cs:4183` |
| 15 | `BattleEvent.Attack(...)` — validates and packs weapon, shield, hit location, resolution, and combo position into one `long` | `BattleSimulation.cs:4197`, defined `src/Hukbo.Core/Simulation/BattleEvent.cs:223` |
| 16 | buffer swap, then `LastEvents` exposes the tick's list | `BattleSimulation.cs:652-660`, property at `BattleSimulation.cs:282` |
| 17 | `SoundDirector.Ingest(_simulation.LastEvents)` | called `ArenaGame.cs:1549`, defined `SoundDirector.cs:120` |

**Once per event, inside `Ingest`**

| # | Call | Where |
| ---: | --- | --- |
| 18 | `SoundCueMapper.Map(battleEvent)` | `SoundDirector.cs:127`, defined `src/Hukbo.Client/Audio/SoundCueMapper.cs:24` |
| 19 | → `MapAttack(weapon, resolution)` | `SoundCueMapper.cs:27`, defined `SoundCueMapper.cs:46` |
| 20 | → `MapShieldClash(weapon)` when the resolution is `ShieldBlocked`, else `MapWeapon(weapon)` | `SoundCueMapper.cs:49-51`, defined `SoundCueMapper.cs:78` and `SoundCueMapper.cs:59` |
| 21 | → `MapOutcome(factionId)` for a terminal event | `SoundCueMapper.cs:31`, defined `SoundCueMapper.cs:88` |
| 22 | `SoundCatalog.IsHitLocationDriven(sound)`, then `HitClassCatalog.FromBodyPart` if true | `SoundDirector.cs:141-143`, defined `SoundCatalog.cs:90` and `HitClass.cs:51` |
| 23 | `SoundDirector.Resolve(sound, hitClass, tick, sourceEntityId)` | `SoundDirector.cs:144`, defined `SoundDirector.cs:164` |
| 24 | `ISoundPlayer.GetStatus` — a non-`Ready` status short-circuits and is logged | `SoundDirector.cs:170`, implemented `MonoGameSoundPlayer.cs:78` |
| 25 | mute check — logged as `Muted` and returns | `SoundDirector.cs:188-199` |
| 26 | `SoundCueBudget.TryConsume(sound)` — logged as `Suppressed` and returns on refusal | `SoundDirector.cs:201`, defined `SoundCueBudget.cs:63` |
| 27 | `ISoundPlayer.GetVariantCount` | `SoundDirector.cs:214`, implemented `MonoGameSoundPlayer.cs:83` |
| 28 | `SoundVariantSelector.Select(tick, sourceEntityId, variantCount)` | `SoundDirector.cs:215`, defined `src/Hukbo.Client/Audio/SoundVariantSelector.cs:20` |
| 29 | `SoundVoiceLedger.GetGainForNextCue(CueVolume)` | `SoundDirector.cs:224`, defined `SoundVoiceLedger.cs:83` |
| 30 | `ISoundPlayer.Play(sound, hitClass, variantIndex, gain)` | `SoundDirector.cs:225`, implemented `MonoGameSoundPlayer.cs:97` |
| 31 | `SoundEffect.Play(volume, pitch: 0f, pan: 0f)` — the only MonoGame audio call in the client | `MonoGameSoundPlayer.cs:113-116` |
| 32 | `SoundVoiceLedger.Add(Player.GetDurationSeconds(...))` on success | `SoundDirector.cs:238`, defined `SoundVoiceLedger.cs:60` |
| 33 | `SoundDirector.Record` — appends the on-screen row and, at debug level, one `audio.cue` line carrying slot, hit class, status, variant, gain, and voice count | `SoundDirector.cs:239-246`, defined `SoundDirector.cs:256` |

**The non-simulation path.** A UI command bypasses the mapper entirely:
`ArenaGame.ApplyClientCommand` calls
`_soundDirector.RequestCue(GameSoundId.UiClick, _simulation.Tick)` at
`ArenaGame.cs:1207`, and `RequestCue` (`SoundDirector.cs:155-156`) enters
`Resolve` with a null hit class and a fixed source entity ID of zero. This is
the path any future non-simulation ranged cue would have to use, and its fixed
entity ID means every such cue at the same tick draws the same variant.

### 4.3 Three properties of this path that constrain the ranged design

**One event, one cue.** Every accepted attack produces exactly one
`BattleEvent`, at `BattleSimulation.cs:3766`, inside the single attack stage.
There is no release event, no projectile event, no impact event separate from
the attack, and no reload event. If the ranged simulation work keeps the
existing hitscan shape, **the Client can play exactly one sound per shot**, and
any allocation that assumes a separate release cue and a separate impact cue is
buying files the game can never trigger. Section 6 states what the plan must
therefore ask of `Hukbo.Core`.

**`Damage` and `Move` are deliberately silent.** `SoundCueMapper.Map` returns
`null` for both (`SoundCueMapper.cs:32`), and the remarks at
`SoundCueMapper.cs:17-23` give the reason: every damage event accompanies the
attack event that caused it, so mapping both would double every hit. A ranged
design that emits damage on a later tick than the attack could exploit this, but
only by changing the mapper.

**`Evaded` currently plays the weapon's impact sound, and the Core comment says
it should not.** `AttackResolution.Evaded` is documented at
`src/Hukbo.Core/Combat/AttackResolution.cs:46-51` as "The defender stepped off
the line and the blow met empty air. Carries no sound and no contact effect; the
absence is the signal." But `MapAttack` at `SoundCueMapper.cs:49-51` routes only
`ShieldBlocked` away from the weapon slot, so an `Evaded` attack — produced by
`ClashResolver` at `src/Hukbo.Core/Combat/ClashResolver.cs:138` — reaches
`MapWeapon` and plays the weapon's flesh-impact take. The mapper's own remarks
at `SoundCueMapper.cs:41-44` say so plainly: "`Landed`, `Parried`, `Deflected`,
and `Evaded` still share one cue."

These two statements contradict each other and one of them is wrong. For melee
the cost is small. For a ranged weapon it is not: a missed arrow that plays the
sound of an arrow entering a body is the single most audible way a ranged
feature can read as broken. The planner should treat this as a decision to be
taken, not as background — section 6 allocates a `miss-<weapon>` slot on the
assumption it is resolved in favour of the Core comment.

## 5. Voice limiting and capacity

### 5.1 The measured ceiling

`docs/research/SOUND-CAPACITY-MEASUREMENTS.md` records the hard numbers. The
audio backend plays **256 simultaneous voices** and throws
`InstancePlayLimitException` on the 257th (`SOUND-CAPACITY-MEASUREMENTS.md:65-74`).
That figure is not a tuning value: MonoGame's
`OpenALSoundController.MAX_NUMBER_OF_SOURCES` is a compile-time constant of 256
on desktop and 32 on iOS and Android, so it **cannot be raised by
configuration** (`SOUND-CAPACITY-MEASUREMENTS.md:126-138`). The backend does not
leak sources — capacity returns to the full 256 after every one of six
saturation rounds (`:109-120`) — and the CPU cost is negligible even at
1 600 cues per second, twenty-five times anything this game produces
(`:83-98`).

### 5.2 What the game itself allows

Three independent mechanisms, in the order a cue meets them.

**The per-frame budget.** `SoundCueBudget` allows
`DefaultMaximumPerSound = 16` and `DefaultMaximumTotal = 64`
(`src/Hukbo.Client/Audio/SoundCueBudget.cs:27-28`), reset once per rendered
frame by `BeginFrame` (`SoundCueBudget.cs:53-57`). A cue that fails
`TryConsume` (`SoundCueBudget.cs:63-75`) is recorded as
`SoundCueStatus.Suppressed` and never reaches the device
(`SoundDirector.cs:201-212`). The type's own remarks call it "a backstop against
a pathological scenario, not a throttle on ordinary play"
(`SoundCueBudget.cs:3-6`) and record that the earlier limits of three and eight
"discarded real cues for no benefit" (`SoundCueBudget.cs:9-13`).

**The gain correction.** `SoundVoiceLedger` tracks how many clips are still
sounding and divides each new cue's gain by the square root of that count plus
one: `baseGain / MathF.Sqrt(_endTimes.Count + 1)`
(`src/Hukbo.Client/Audio/SoundVoiceLedger.cs:87`). The base gain is
`SoundDirector.CueVolume = 0.65f` (`src/Hukbo.Client/Audio/SoundDirector.cs:32`),
lowered from 0.8 because 0.8 still let 500 agents at normal speed reach
+1.6 dBFS and flatten two samples (`SoundDirector.cs:23-31`). The correction is
deliberately unfloored: at the measured worst case of 113 voices the gain lands
near 0.075 (`SoundVoiceLedger.cs:76-82`). The ledger stops tracking at
`MaximumTrackedVoices = 512` (`SoundVoiceLedger.cs:27`).

**The backend's own refusal.** `MonoGameSoundPlayer.Play` returns `false` when
MonoGame's managed instance pool is exhausted and catches
`InstancePlayLimitException` when the OpenAL source list is
(`MonoGameSoundPlayer.cs:107-127`). The director records that as
`SoundCueStatus.Refused` and, importantly, does **not** charge it against the
voice count (`SoundDirector.cs:225-236`).

### 5.3 There is no dedupe and there is no cooldown

This is worth stating explicitly because it is the assumption most likely to be
made wrongly. **Nothing in the audio path suppresses a cue for being the same as
another cue.** There is no minimum interval between two plays of one slot, no
"same sound within N milliseconds" filter, and no per-agent throttle. The only
thing that stops a cue is the per-frame count in `SoundCueBudget`, which is
blind to what the cue is beyond which slot it belongs to.

The one collapsing rule that does exist is cosmetic. `SoundCueLog.Append`
(`src/Hukbo.Client/Audio/SoundCueLog.cs:34-49`) increments the newest row's
count when the tick, slot, and status all match, "so a tick of forty suppressed
attacks costs one row rather than flushing the log". That affects the on-screen
log only. Every one of those forty cues was already played or already
suppressed by the time the log saw it.

### 5.4 What happens at 500 agents today

From `SOUND-CAPACITY-MEASUREMENTS.md:170-174` and `:339-344`:

| Agents / speed | Cues played | Suppressed | Peak concurrent voices | Peak level |
| --- | ---: | ---: | ---: | ---: |
| 200 / 1x | 2 186 | 0 | 29 | −2.6 dBFS |
| 200 / 4x | 2 186 | 0 | 93 | −5.1 dBFS |
| 500 / 1x | 5 511 | 0 | 41 | **−0.2 dBFS** |
| 500 / 4x | 5 511 | 0 | 113 | −6.1 dBFS |

A 500-agent battle runs 2 668 ticks and emits 5 510 cues, a mean of 2.07 per
tick, with a p95 of 6, a p99 of 9, and a **busiest single tick of 15**
(`SOUND-CAPACITY-MEASUREMENTS.md:170-174`). Nothing is suppressed anywhere.
Peak concurrency of 113 sits comfortably inside the 256-voice ceiling.

The number to hold on to is **−0.2 dBFS at 500 agents and normal speed**. That
is two tenths of a decibel of headroom, and it is the configuration the
measurement singles out as the one that forced `CueVolume` down from 0.8 to
0.65. The current mix is not comfortably under full scale; it is barely under
it, in exactly one configuration, by design.

### 5.5 What a volley breaks

A volley is not a busier melee. It is a structurally different demand, and it
attacks all three mechanisms at once.

**The per-slot cap of 16 binds long before the total cap of 64 does.** Melee
never triggers the per-slot cap because attacks are staggered by independent
cooldowns and spread across four weapon slots. A volley is by definition
synchronised and by definition single-slot: forty archers loosing in one tick
all land on `release-busog`. Cues seventeen through forty are `Suppressed` while
the frame's total budget still has forty-eight unused slots. The current 500-
agent peak of fifteen cues in a tick is spread across five slots; a volley puts
its whole count into one.

**The gain correction is calibrated for the wrong material.** `SoundVoiceLedger`
divides by the square root of the voice count, and its own comment states the
reason: that is "the standard correction for summing uncorrelated material,
which impacts from different agents largely are"
(`SoundVoiceLedger.cs:70-75`). `SOUND-CAPACITY-MEASUREMENTS.md:258-265` states
the other half of the rule: N uncorrelated signals sum to roughly
10·log₁₀(N) dB, but N **correlated** signals approach 20·log₁₀(N), and 1/√N only
corrects the first case. A volley is the correlated case in its purest form —
many copies of clips of the same slot, starting in the same tick, phase-aligned
because they were triggered by the same event. Against 0.2 dB of measured
headroom, that under-correction is what clips.

**Variant selection helps, but the pigeonhole bites.**
`SoundVariantSelector.Select` mixes the tick with the source entity ID
(`SoundVariantSelector.cs:29`), so forty archers at one tick do draw across the
whole variant list rather than all playing take one. With six takes and forty
archers, though, roughly seven agents land on each take, and those seven are
literally the same waveform starting within one frame. Seven coherent copies is
about +16.9 dB where seven incoherent ones would be about +8.4 dB. **The number
of takes in a volley slot is therefore a mix-headroom decision, not only a
repetition decision** — which is the strongest argument in this document for
spending generations on the release slot rather than on a sixth hit-class
variant.

**The hardware ceiling is still not the constraint.** Even doubling the cue rate
by adding a release cue to every shot leaves peak concurrency far under 256, and
the CPU cost stays negligible. What breaks first, in order, is: the per-slot
budget of 16, then the correlated-summing headroom, then — nowhere near — the
device.

**None of this can be settled by arithmetic alone.** The mix harness at
`tools/Hukbo.Tools.MixAnalysis` is what measured every figure in section 5.4,
and `SOUND-CAPACITY-MEASUREMENTS.md:468-473` warns that its slot mapping,
hit-class mapping, fallback chain, and variant draw are **replicas** of the
client's, so "if the client's mapping changes, this harness must change with
it". Adding ranged slots changes the client's mapping. The plan should carry a
task to update that harness and re-run the 500-agent rendering, because it is
the only way to learn what a volley does to the peak before eighty files have
been paid for and generated.

## 6. The recommended twenty-file allocation per ranged weapon

### 6.1 What was ruled out, and why

Four of the categories in the original brief do not survive contact with the
code. Ruling them out first is what makes twenty files affordable.

**Projectile flight and pass-by: cut.** The game has no positional audio.
`MonoGameSoundPlayer.Play` calls `SoundEffect.Play(volume, pitch: 0f, pan: 0f)`
at `MonoGameSoundPlayer.cs:113-116`, with pan hard-coded to centre and no
distance attenuation anywhere. A pass-by cue that cannot pass by is a whoosh at
the centre of the mix, fired once per shot, on top of the release cue that is
already firing once per shot. `PENDING-SOUNDS.md` already reached this
conclusion about melee swings: a per-attack cue that fires before resolution "is
the single most likely sound in the game to become continuous noise". A flight
cue is that argument with a longer clip.

**Impact on armor: cut, it is not observable.** `ArmorId` exists at
`src/Hukbo.Core/Combat/CombatIdentity.cs:72-75` and has exactly one member,
`LightOrganic`. More decisively, armor is **not carried on `BattleEvent` at
all** — the packed combat context holds resolution, weapon, shield, hit
location, and combo position and nothing else
(`src/Hukbo.Core/Simulation/BattleEvent.cs:53-59`). The Client cannot distinguish
an armored hit from an unarmored one, and with one armor value there would be
nothing to distinguish.

**Reload as its own cue: cut.** No event marks a reload.
`DecrementCooldowns` at `BattleSimulation.cs:941` ticks
`AttackCooldownTicks` down silently and emits nothing. Giving reload a sound
means emitting a new event on a cooldown boundary for every ranged agent every
few ticks, which is a far higher event rate than attacks themselves. For the
arquebus, whose slow reload is its defining characteristic, the honest place to
put that character is in the length and tail of the release cue.

**New `HitClass` values: not needed.** The six acoustic classes describe where a
projectile arrives exactly as well as where a blade does, and
`HitClassCatalog.FromBodyPart` (`HitClass.cs:51-68`) must stay total over the
thirteen `BodyPart` values, so a `Shield` or `Ground` class could never be
produced from a body part anyway. Shield and ground are slot distinctions, not
class distinctions, and the shipped `clash-shield-<weapon>` slots already prove
that pattern works.

### 6.2 The allocation

Four slot families per weapon, plus one arquebus-only fifth. Every slot follows
the existing `<slot>[-<class>][-NN].wav` contract unchanged.

| Slot | Class-driven | Fires when | Bangkaw | Busog | Sumpit | Arquebus |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| `release-<weapon>` | no | the shot leaves the weapon | 5 | 6 | 8 | 7 |
| `attack-<weapon>` | **yes**, six classes | the shot reaches a body | 9 | 8 | 6 | 6 |
| `clash-shield-<weapon>` | no | the shot is stopped by a shield | 3 | 3 | 3 | 3 |
| `miss-<weapon>` | no | the shot resolves `Evaded` and spends itself | 3 | 3 | 3 | 2 |
| `misfire-arquebus` | no | the charge fails | — | — | — | 2 |
| **total** | | | **20** | **20** | **20** | **20** |

**Why `release` gets the largest share on three of four weapons.** It is the
only cue that fires on one hundred per cent of shots, so it is the cue a
spectator hears most and the cue that repeats soonest. It is also the cue that
volleys, which by section 5.5 makes its take count a mix-headroom decision as
well as a repetition one: with six takes and forty simultaneous archers, roughly
seven agents share each waveform and sum coherently. Every extra take divides
that coherent group. This is the single highest-value place to spend a
generation.

**Why the counts differ by weapon.** Takes follow how much of the weapon's
character lives in each moment.

- The **Sumpit** gets eight release takes and only six impacts because the puff
  of a blowgun *is* the weapon acoustically; a dart arriving in a body is a
  thin tick that barely varies by hit class. Six impact takes is exactly one
  per class — the minimum that gives every class its own file with no fallback.
- The **Bangkaw** is the reverse: a thrown spear is a quiet release and a very
  loud arrival, and a heavy shaft into a skull genuinely differs from one into a
  thigh. Nine impact takes buys a second take on the two classes that receive
  sixty per cent of blows.
- The **Busog** sits between them at six and eight.
- The **Arquebus** spends two on `misfire` because a misfire is the one moment
  that must be unmistakable, and takes them from `miss`, since a lead ball
  striking earth varies less than a shaft does. Its impact count is six rather
  than nine because the report dominates and the impact is a short slap.

**Why three shield takes rather than four.** The four shipped melee clash slots
carry four takes each. Three is one fewer, and it is where the twentieth file
comes from. A shield block is a minority resolution and does not volley the way
a release does, so a take there buys less than a take on `release`.

**Why `miss` earns three takes despite firing on a minority of shots.** Because
of the discrepancy in section 4.3: an `Evaded` ranged attack currently plays the
flesh-impact take. A missed arrow that sounds like an arrow entering a body is
the most audible possible failure of a ranged feature.

**If the planner decides `Evaded` should stay silent** — honouring the comment
at `AttackResolution.cs:46-51` rather than the mapper — then the `miss-<weapon>`
slot is not built and its takes move to `release-<weapon>`, giving 8/9/11/9
release takes respectively. That redistribution is the correct fallback, because
`release` is where extra takes do the most work.

### 6.3 Worked example — every filename for the Busog

Twenty files, exactly as they must appear in
`src/Hukbo.Client/Content/Audio/`.

```
release-busog-01.wav
release-busog-02.wav
release-busog-03.wav
release-busog-04.wav
release-busog-05.wav
release-busog-06.wav

attack-busog-extremity-01.wav
attack-busog-extremity-02.wav
attack-busog-limb-01.wav
attack-busog-limb-02.wav
attack-busog-skull-01.wav
attack-busog-neck-01.wav
attack-busog-ribcage-01.wav
attack-busog-gut-01.wav

clash-shield-busog-01.wav
clash-shield-busog-02.wav
clash-shield-busog-03.wav

miss-busog-01.wav
miss-busog-02.wav
miss-busog-03.wav
```

The eight impact takes are weighted by the class shares derived in section 3.3:
`extremity` at 35.4 per cent and `limb` at 24.2 per cent get two each, and the
four thinner classes get one each. `ribcage` must be among them, because it is
the universal fallback target for every other class
(`HitClass.cs:100-113`) and a hit-location driven slot without it can resolve
`Missing` for a class whose own file is absent.

The generating commands, in order, are given in section 7.3.

### 6.4 The weapon token is not yet decided

`SoundCatalog.GetBaseName` names a weapon slot for the weapon's identity —
`WeaponId.Itak` becomes `attack-itak` (`SoundCatalog.cs:49-63`). The tokens above
assume the ranged `WeaponId` members will be `Bangkaw`, `Busog`, `Sumpit`, and
something rendering as `arquebus`. The first three are used as such in
`docs/research/ranged/2026-08-07-RANGED-WEAPONS-EVIDENCE.md:20-23`. The fourth
is written there as "Imported Arquebus" with no Filipino identity, so its enum
member name — and therefore its file token — is an open decision the simulation
work owns. **The audio plan must take the token from whatever `WeaponId` member
lands, not the other way round.** Nothing else in this section changes if the
token is `arkabus` instead of `arquebus`.

### 6.5 Every code change these eighty files require

**New `GameSoundId` values: yes, seventeen.**
`src/Hukbo.Client/Audio/AudioTypes.cs:8-29`. Four `Attack*`, four `Release*`,
four `ClashShield*`, four `Miss*`, and `MisfireArquebus`, appended at 13 through
29 so no existing value moves. These values are presentation-only — nothing in
`Hukbo.Core` knows the enum exists — so no preset version and no golden
expectation is affected by them.

**New `HitClass` values: no.** `src/Hukbo.Client/Audio/HitClass.cs` is
untouched. See section 6.1.

**New slot patterns: no.** Every one of the eighty names is
`<slot>-NN.wav` or `<slot>-<class>-NN.wav`, both already parsed by
`SoundLibrary.TryParseVariantIndex` (`SoundLibrary.cs:325-353`).

The full change list, by file:

| File | Change |
| --- | --- |
| `src/Hukbo.Client/Audio/AudioTypes.cs` | seventeen new `GameSoundId` members, appended |
| `src/Hukbo.Client/Audio/SoundCatalog.cs` | seventeen entries in `AllSounds` (`:32-47`); seventeen arms in `GetBaseName` (`:57-77`); extend `IsHitLocationDriven` (`:90-94`) to the four new attack slots |
| `src/Hukbo.Client/Audio/SoundCueMapper.cs` | four arms in `MapWeapon` (`:59-67`); four in `MapShieldClash` (`:78-86`); a new `MapMiss` plus a new `Evaded` branch in `MapAttack` (`:46-51`); a branch for whatever event carries a release |
| `src/Hukbo.Core/Simulation/BattleEvent.cs` | a new `BattleEventKind` member for release, if the release cue is built — see below |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | emission of that event inside the attack stage |
| `src/Hukbo.Client/Audio/SoundCueBudget.cs` | a decision on `DefaultMaximumPerSound = 16` (`:27`) against a volley — see section 5.5 |
| `scripts/sfx.ps1` | the `-Class` guard and the default-prompt table — see section 7 |
| `src/Hukbo.Client/Content/Audio/README.md` | seventeen rows; it is the naming contract a person reads |
| `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` | the swing-slot section is partly answered by the release decision |
| `tools/Hukbo.Tools.MixAnalysis` | its replica mapping, which `SOUND-CAPACITY-MEASUREMENTS.md:468-473` requires be kept in lockstep |
| `docs/development/testing.md` | new manual smoke rows; only a human may flip one |

**The release cue is the one item that reaches `Hukbo.Core`, and it is not
free.** There is no event today at the moment a shot leaves a weapon (section
4.3). Two shapes are available, and the planner must pick one:

- *Add a `BattleEventKind` member.* Appending `Release = 5` after
  `Outcome = 4` (`BattleEvent.cs:5-12`) leaves every existing numeric value in
  place, but it adds events to the ordered stream, which changes the event hash.
  Under `CLAUDE.md` section 5 that requires a **new preset version plus new
  golden expectations**. It also has to be carried by `SoundCueMapper.Map`
  (`SoundCueMapper.cs:24-33`), and it puts a presentation-motivated event into
  the authoritative stream, which the planner should weigh on its own terms.
- *Play release and impact from the single existing `Attack` event.* This costs
  nothing in Core and is wrong for any projectile with flight time, because the
  two sounds would be simultaneous. It is only defensible if the ranged
  simulation stays hitscan.

Until that decision is taken, **twenty files per weapon cannot all be
triggered**. The `release-<weapon>` slot is between five and eight of every
twenty. A plan that generates eighty files before this is settled risks paying
for twenty-six of them that no event can reach.

## 7. What `scripts/sfx.ps1` needs

### 7.1 The slot-parsing regex accepts every proposed name unchanged

`Get-CatalogSlot` at `scripts/sfx.ps1:260-272` reads `SoundCatalog.cs` as one
string and pulls the slot names out of the `GetBaseName` switch with a single
regular expression:

```powershell
$catalogText = Get-Content -Raw -LiteralPath $catalogPath
$slotMatches = [regex]::Matches($catalogText, 'GameSoundId\.\w+\s*=>\s*"([a-z0-9-]+)"')
```

The capture group is `[a-z0-9-]+` — lowercase ASCII letters, digits, and the
hyphen. Checked against every proposed name:

| Proposed slot | Matches `[a-z0-9-]+`? |
| --- | --- |
| `attack-bangkaw`, `attack-busog`, `attack-sumpit`, `attack-arquebus` | yes |
| `release-bangkaw`, `release-busog`, `release-sumpit`, `release-arquebus` | yes |
| `clash-shield-bangkaw` … `clash-shield-arquebus` | yes |
| `miss-bangkaw` … `miss-arquebus` | yes |
| `misfire-arquebus` | yes |

**No change to the regex is required.** Two supporting points. `\s*` in .NET
matches newlines, and the file is read with `-Raw`, so a switch arm wrapped
across two lines still matches. And the regex cannot pick up a false positive
from elsewhere in the catalog: `GetStatusLabel` at `SoundCatalog.cs:152-162`
uses `SoundBindingStatus.Ready => "READY"`, whose left side does not begin
`GameSoundId.` and whose right side is uppercase.

The only way to break `Get-CatalogSlot` is to introduce an underscore or a
capital letter into a base name, and
`SoundCatalogTests.GetFileName_IsUniqueLowercaseKebabWavForEverySlot`
(`tests/Hukbo.Client.Tests/SoundCatalogTests.cs:24-44`) already fails the build
for both.

### 7.2 The `-Class` guard works — because the slots are named `attack-`

The class guard is at `scripts/sfx.ps1:626-628`:

```powershell
if (-not [string]::IsNullOrWhiteSpace($Class) -and -not $Slot.StartsWith('attack-')) {
    throw "-Class applies only to an attack slot. '$Slot' events carry no hit location, so a class would name a file the game can never select."
}
```

It tests a **string prefix**, not `SoundCatalog.IsHitLocationDriven`. Naming the
four ranged impact slots `attack-bangkaw`, `attack-busog`, `attack-sumpit`, and
`attack-arquebus` therefore makes the guard correct with no edit at all, and it
correctly rejects `-Class` on `release-*`, `miss-*`, `clash-shield-*`, and
`misfire-arquebus`. This is a concrete reason to keep the `attack-` prefix
rather than renaming the ranged impact slot to `impact-`: renaming it would
silently disable the guard for the very slots that need it.

The `-Class` parameter's own `ValidateSet` at `scripts/sfx.ps1:94` lists the six
class tokens and needs no change, since section 6.1 adds no class.
`-Index` validates 1 through 99 at `scripts/sfx.ps1:98`, which covers every
count in the allocation.

### 7.3 The default prompt table does not need eighty entries

The table is keyed by **slot**, not by file. The lookup at
`scripts/sfx.ps1:584-590` is `$defaultPrompts[$Slot].Prompt`, so a slot has one
default and every file in that slot inherits it unless `-Prompt` overrides it.
Seventeen new slots means seventeen new entries, bringing the table from
thirteen to thirty. Eighty entries is not what the mechanism asks for.

But seventeen entries is also not what actually produced the melee content.
`src/Hukbo.Client/Content/Audio/GENERATED.md` shows that every one of the forty
melee class variants was generated with its own explicit `-Prompt`. The
provenance rows for the Kampilan alone carry ten distinct prompts —
"one heavy two-handed blade splitting into a skull, hard dry bone crack with a
thin wet edge and a brief metallic shiver" for `skull-01`, "one heavy blade
landing across a face, sharp bone crack opening into a shallow wet cut" for
`skull-02`, and so on. The table's own comment at `scripts/sfx.ps1:172-177`
confirms this was the intent: "Each of these is the weapon's most common hit,
and each doubles as the single-file fallback for its slot."

So the real question is whether eighty prompts have to be typed at the command
line. Three options, in increasing order of work and value:

**Option A — seventeen slot defaults, eighty explicit `-Prompt` values.** What
melee did. Nothing changes in the script beyond adding seventeen table entries.
The eighty prompts live in the plan document, and each generating command passes
one. Honest, and it is the shape the tool was built for.

**Option B — parameterize by class.** Add an optional nested table so a
hit-location driven slot carries a prompt per class:

```powershell
'attack-busog' = @{
    Prompt   = '...'          # the fallback and the bare single
    Duration = 0.5
    Trim     = $true
    Classes  = @{ skull = '...'; neck = '...'; ... }
}
```

with the resolution at `scripts/sfx.ps1:584-590` preferring
`$defaultPrompts[$Slot].Classes[$Class]` when a class was given. That is one new
branch and it turns the twenty-eight class-scoped ranged files into twenty-eight
table rows rather than twenty-eight command-line strings. It changes the
`-List` output shape, so `-List` would need to show the class rows too.

**Option C — compose a stem and a class fragment.** Because the class fragment
is largely weapon-independent — "into a skull, hard bone crack under thin
flesh" reads the same whether a spear or a ball arrives — a shared
`$classFragments` table of six strings plus one stem per slot covers all
twenty-eight class files from twenty-three authored strings. This is the
smallest amount of writing, and it is the most likely to produce four ranged
weapons that sound like variations of one library rather than four unrelated
sets. It is also the most speculative: nobody has heard whether a composed
prompt generates as well as a hand-written one.

**Recommendation: Option B.** It removes the per-command typing that is the
actual source of error across eighty paid generations, it keeps every prompt
hand-written and reviewable in one place, and it is a single branch in an
authoring script that no test and no gate touches. Option C should be tried on
one weapon first and adopted only if the takes come back usable.

### 7.4 The `-List` wart gets much worse

`-List` at `scripts/sfx.ps1:558-564` probes existence with
`Get-SlotPath -SlotName $name -Directory $defaultOutputDirectory` and no class
and no index, which resolves to the **bare** `<slot>.wav`. Today only four slots
have a bare single — `draw`, `ui-click`, `victory-blue`, `victory-red` — so
`-List` already reports the other nine as `MISSING` even though all seventy
files are present. `src/Hukbo.Client/Content/Audio/README.md` documents this
explicitly.

At thirty slots the same code reports twenty-six of thirty as `MISSING` after
every file has been generated and paid for. That is a bad signal to hand a
person doing eighty generations, and it is the one place in the tooling where
the ranged work makes an existing wart materially worse. The fix is small —
have `-List` count matching files with the same prefix rules the game uses,
rather than testing one exact path — and the plan should carry it as its own
task.

### 7.5 The generating commands for the worked example

Assuming Option B is implemented, the twenty Busog files are:

```powershell
1..6 | ForEach-Object { ./scripts/sfx.ps1 -Slot release-busog -Index $_ }

./scripts/sfx.ps1 -Slot attack-busog -Class extremity -Index 1
./scripts/sfx.ps1 -Slot attack-busog -Class extremity -Index 2
./scripts/sfx.ps1 -Slot attack-busog -Class limb      -Index 1
./scripts/sfx.ps1 -Slot attack-busog -Class limb      -Index 2
./scripts/sfx.ps1 -Slot attack-busog -Class skull     -Index 1
./scripts/sfx.ps1 -Slot attack-busog -Class neck      -Index 1
./scripts/sfx.ps1 -Slot attack-busog -Class ribcage   -Index 1
./scripts/sfx.ps1 -Slot attack-busog -Class gut       -Index 1

1..3 | ForEach-Object { ./scripts/sfx.ps1 -Slot clash-shield-busog -Index $_ }
1..3 | ForEach-Object { ./scripts/sfx.ps1 -Slot miss-busog -Index $_ }
```

Three operational facts the plan should carry. The API refuses anything under
0.5 seconds, so a short impact is generated long and trimmed — the trim rules
are at `scripts/sfx.ps1:423-502` and the floor constant at
`scripts/sfx.ps1:158`. A take peaking below ten per cent of full scale is
rejected without writing anything (`scripts/sfx.ps1:161-162`, `:736-740`), so
re-running the same command is safe and is usually all that is needed. And the
script retries a rate-limit response six times with exponential backoff
(`scripts/sfx.ps1:166`, `:682-710`), which matters when eighty generations are
run in a batch.

## 8. Every audio test that constrains this

Eleven test files in `tests/Hukbo.Client.Tests/` cover the audio subsystem, with
148 test methods between them, counted by
`grep -c "    public void "` per file:
`HitClassCatalogTests.cs` 12, `SoundCatalogTests.cs` 11,
`SoundCueBudgetTests.cs` 6, `SoundCueFormatterTests.cs` 5,
`SoundCueLogTests.cs` 10, `SoundCueMapperTests.cs` 6,
`SoundDirectorTests.cs` 26, `SoundLibraryTests.cs` 25,
`SoundLogPanelTests.cs` 27, `SoundVariantSelectorTests.cs` 7, and
`SoundVoiceLedgerTests.cs` 13. Every path below is relative to
`tests/Hukbo.Client.Tests/`.

### 8.1 Tests that will fail and must be edited

| Test | Location | Why it moves |
| --- | --- | --- |
| `SoundCatalogTests.EveryDefinedWeapon_HasAnAttackSlot` | `SoundCatalogTests.cs:51-72` | Enumerates `Enum.GetValues<WeaponId>()`. Goes red the moment a ranged `WeaponId` is added and stays red until `SoundCueMapper.MapWeapon` has an arm for it. This is the designed safety net, not a defect — the comment at `:54-56` says so. |
| `SoundCatalogTests.EveryDefinedWeapon_HasAShieldClashSlot` | `SoundCatalogTests.cs:74-98` | Same net, aimed at the shield-block branch. |
| `SoundCatalogTests.IsHitLocationDriven_IsTrueOnlyForTheFourWeaponSlots` | `SoundCatalogTests.cs:136-153`, `[InlineData]` rows at `:137-149` | One `[InlineData]` per slot, hand-written. Needs seventeen new rows, and the method name becomes wrong once there are eight hit-location driven slots. |
| `SoundCueMapperTests.Map_ReturnsTheWeaponSlotForAnAttack` | `SoundCueMapperTests.cs:11-33`, rows at `:12-17` | One row per weapon; needs four more. |
| `SoundCueMapperTests.Map_RoutesAShieldBlockToTheMatchingClashSlot` | `SoundCueMapperTests.cs:35-46`, rows at `:36-39` | Same; needs four more. |
| `SoundCueMapperTests.Map_KeepsTheWeaponSlotForEveryOtherResolution` | `SoundCueMapperTests.cs:48-57`, and specifically `[InlineData(AttackResolution.Evaded)]` at `:52` | **This is the test that pins the contradiction in section 4.3.** It asserts that an `Evaded` attack maps to the weapon's flesh-impact slot. Building a `miss-<weapon>` slot makes it fail, and that failure is the decision being taken, not an accident. |

### 8.2 Tests that pin behaviour the change must not break

| Test | Location | What it protects |
| --- | --- | --- |
| `SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce` | `SoundCatalogTests.cs:9-22` | Adding a `GameSoundId` member without the matching `AllSounds` entry fails the build. |
| `SoundCatalogTests.GetFileName_IsUniqueLowercaseKebabWavForEverySlot` | `SoundCatalogTests.cs:24-44` | Lowercase, no space, no underscore, unique. This is what keeps `Get-CatalogSlot`'s regex working. |
| `SoundCatalogTests.GetBaseName_RejectsAnUndeclaredSlot` | `SoundCatalogTests.cs:46-49` | A new member without a `GetBaseName` arm throws. |
| `SoundCatalogTests.GetVariantFileName_BuildsTheSlotClassIndexPattern` | `SoundCatalogTests.cs:155-162` | Pins the literal `attack-kampilan-skull-01.wav`. |
| `SoundCatalogTests.GetVariantFileName_RejectsANonAttackSlot` | `SoundCatalogTests.cs:164-170` | A class variant may not be built for a classless slot. |
| `SoundCatalogTests.GetSlotVariantFileName_BuildsTheSlotIndexPattern` | `SoundCatalogTests.cs:172-176` | Pins the literal `death-01.wav`. |
| `SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation` | `SoundDirectorTests.cs:41-74` | **The most important guard for this work.** Every new classless ranged slot depends on the director deriving the class from `IsHitLocationDriven` rather than from the event. Reverting that silences them all with no other test going red. |
| `SoundDirectorTests.Ingest_MapsTheEventHitLocationToTheAcousticHitClass` | `SoundDirectorTests.cs:28-40` | The other half of the same rule. |
| `SoundDirectorTests.Ingest_UsesANullHitClassForAnEventWithNoHitLocation` | `SoundDirectorTests.cs:75-86` | Death and outcome events. |
| `SoundDirectorTests.Ingest_SpreadsVariantSelectionAcrossDifferentSourceEntities` | `SoundDirectorTests.cs:87-121` | The property section 5.5 depends on when a volley draws across takes. |
| `SoundDirectorTests.Ingest_SuppressesCuesPastTheFrameBudgetAndCollapsesTheLog` | `SoundDirectorTests.cs:225-249` | Budget behaviour under load. |
| `SoundDirectorTests.Ingest_KeepsTheBudgetAcrossTheTicksOfOneFrame` | `SoundDirectorTests.cs:269-284` | Why the budget is per frame, not per tick. |
| `SoundDirectorTests.Ingest_LowersTheGainAsVoicesAccumulateWithinAFrame` | `SoundDirectorTests.cs:380-409` | The 1/√N correction. |
| `SoundDirectorTests.Ingest_LogsARefusedCueRatherThanReportingItAsPlayed` | `SoundDirectorTests.cs:453-471` | Backend refusal is never reported as a play. |
| `SoundDirectorTests.Ingest_DoesNotChargeARefusedCueAgainstTheVoiceCount` | `SoundDirectorTests.cs:472-488` | A refusal does not inflate the gain divisor. |
| `SoundCueBudgetTests.DefaultLimits_CapOneSlotAndTheFrameAtTheDeclaredMaxima` | `SoundCueBudgetTests.cs:59-79` | Pins 16 per slot and 64 per frame. Changing either for volleys changes this test. |
| `SoundCueBudgetTests.TryConsume_RejectsASlotThatIsNotInTheCatalog` | `SoundCueBudgetTests.cs:50-58` | A slot missing from `AllSounds` throws rather than being silently free. |
| `SoundVariantSelectorTests.Select_IsDeterministicForTheSameInputs` | `SoundVariantSelectorTests.cs:34-42` | Replay picks the same take. |
| `SoundVariantSelectorTests.Select_SpreadsSelectionAcrossDifferentSourceEntitiesAtTheSameTick` | `SoundVariantSelectorTests.cs:43-57` | The volley-decorrelation property, at the selector level. |
| `SoundVariantSelectorTests.Select_IsWithinBoundsForEveryInput` | `SoundVariantSelectorTests.cs:21-33` | No index escapes the resolved list. |

### 8.3 Tests that pin the naming contract itself

Every one of these is pure — it takes file names as data and never touches a
directory — so all of them constrain the eighty new names without any of them
needing to change.

| Test | Location |
| --- | --- |
| `SoundLibraryTests.Resolve_ReturnsOneBindingPerSlotInCatalogOrder` | `SoundLibraryTests.cs:9-24` |
| `SoundLibraryTests.Resolve_MatchesFileNameCaseInsensitively` | `SoundLibraryTests.cs:51-61` |
| `SoundLibraryTests.Resolve_CountsNumberedVariantsForAClasslessSlot` | `SoundLibraryTests.cs:80-92` |
| `SoundLibraryTests.Resolve_IgnoresUnsupportedExtensionsAndUnknownNames` | `SoundLibraryTests.cs:93-105` |
| `SoundLibraryTests.Resolve_ReportsRawPerClassCountsForAHitLocationDrivenSlot` | `SoundLibraryTests.cs:114-139` |
| `SoundLibraryTests.Resolve_IsReadyWhenAnyFileExistsAnywhereForTheSlot` | `SoundLibraryTests.cs:140-154` |
| `SoundLibraryTests.ResolveVariants_ReturnsOneEntryPerClassForAHitLocationDrivenSlot` | `SoundLibraryTests.cs:155-169` |
| `SoundLibraryTests.ResolveVariants_ReturnsOneNullClassEntryForAClasslessSlot` | `SoundLibraryTests.cs:170-179` |
| `SoundLibraryTests.ResolveVariants_OrdersFilesAscendingByIndex` | `SoundLibraryTests.cs:180-200` |
| `SoundLibraryTests.ResolveVariants_IgnoresAFileWhoseIndexIsNotExactlyTwoDigits` | `SoundLibraryTests.cs:201-215` |
| `SoundLibraryTests.ResolveVariants_ExtremityFallsBackToLimbThenRibcage` | `SoundLibraryTests.cs:216-245` |
| `SoundLibraryTests.ResolveVariants_SkullFallsBackToNeckThenRibcage` | `SoundLibraryTests.cs:246-268` |
| `SoundLibraryTests.ResolveVariants_FallsBackDirectlyToRibcage` | `SoundLibraryTests.cs:269-285` |
| `SoundLibraryTests.ResolveVariants_RibcageFallsBackToTheBareSingle` | `SoundLibraryTests.cs:286-297` |
| `SoundLibraryTests.ResolveVariants_StaysSilentWhenNoFileExistsAnywhereForTheClass` | `SoundLibraryTests.cs:298-307` |
| `SoundLibraryTests.ResolveVariants_HandlesAPartiallyPopulatedSet` | `SoundLibraryTests.cs:308-325` |
| `SoundLibraryTests.ResolveVariants_ClasslessSlotPrefersNumberedFilesOverTheBareSingle` | `SoundLibraryTests.cs:326-336` |
| `SoundLibraryTests.ResolveVariants_ClasslessSlotFallsBackToTheBareSingle` | `SoundLibraryTests.cs:337-345` |
| `SoundLibraryTests.GetDefaultDirectoryPath_PointsAtTheContentAudioFolder` | `SoundLibraryTests.cs:385-393` |
| `HitClassCatalogTests.GetToken_IsLowercaseAndMatchesTheGeneratorContract` | `HitClassCatalogTests.cs:72-77` |
| `HitClassCatalogTests.All_ListsEveryDeclaredClassExactlyOnceInFixedOrder` | `HitClassCatalogTests.cs:45-71` |
| `HitClassCatalogTests.FromBodyPart_CoversEveryDeclaredBodyPartExactlyOnce` | `HitClassCatalogTests.cs:30-39` |
| `HitClassCatalogTests.GetFallbackChain_*` (six methods) | `HitClassCatalogTests.cs:83-119` |

### 8.4 Panel tests, and the row arithmetic that changes

`BuildBindingRows` emits one header row per slot plus one indented sub-row per
hit class for each hit-location driven slot — thirty-seven rows at thirteen
slots, four of them class-driven. At thirty slots with eight class-driven, that
becomes **seventy-eight rows**. The viewport is capped at
`SoundCatalog.AllSounds.Count` rows by `SoundLogPanel.CalculateLayout`
(`src/Hukbo.Client/UI/SoundLogPanel.Layout.cs:120-122`), and the list scrolls, so
every row stays reachable.

| Test | Location | Effect |
| --- | --- | --- |
| `SoundLogPanelTests.CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight` | `SoundLogPanelTests.cs:342-375` | Reads `SoundCatalog.AllSounds.Count` and self-adjusts. Its comment at `:364-368` derives the height threshold as `216 + 20 * slotCount`, which is 476 at thirteen slots and **816 at thirty**; the test uses a height of 2000 and still clears it. The comment's arithmetic goes stale and should be updated. |
| `SoundLogPanelTests.CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen` | `SoundLogPanelTests.cs:377-413` | Still passes. Its derivation is `min(20 + 13 * 20, H - 196)`; at thirty slots the first term rises to 620 while `H - 196` is 220 at `H = 416`, so `min` still picks the available space and the answers stay 9 and 10. The literal `13` in the comment at `:394` becomes wrong and should be updated. |
| `SoundLogPanelTests.ClampBindingScroll_ReachesTheLastRow` | `SoundLogPanelTests.cs:414-434` | Passes literal figures of 37 and 13 rather than reading the catalog, deliberately — the comment at `:419-421` says it is "a statement about the clamp, not about how many slots exist". Unaffected. |
| `SoundLogPanelTests.ClampBindingScroll_RefusesToScrollPastEitherEnd` | `SoundLogPanelTests.cs:436-450` | Same, unaffected. |
| `SoundLogPanelTests.BuildBindingRows_AddsOneHeaderRowForAClasslessSlot` | `SoundLogPanelTests.cs:269-290` | Constructs its own bindings; unaffected. |
| `SoundLogPanelTests.BuildBindingRows_AddsOneIndentedSubRowPerClassForAWeaponSlot` | `SoundLogPanelTests.cs:291-319` | Same. |
| `SoundLogPanelTests.BuildBindingRows_ShowsAPlainStatusWithNoCountWhenNotReady` | `SoundLogPanelTests.cs:320-337` | Same. |

### 8.5 Files with no test that this change touches

`SoundCueLogTests.cs` (10 methods), `SoundCueFormatterTests.cs` (5 methods), and
`SoundVoiceLedgerTests.cs` (13 methods) contain nothing that depends on how many
slots exist or what they are named, and none of them needs to change.

### 8.6 What no test covers

Two gaps the plan should know about rather than discover.

**No test asserts that a file on disk matches a slot.** Every `SoundLibrary`
test feeds file names in as data. Nothing walks
`src/Hukbo.Client/Content/Audio/` and checks that the seventy files there are
files the game will actually read, and nothing would catch eighty new files
landing with a typo. That is what makes section 2.6 expensive here in a way it
was not for melee.

**No automated test can confirm a sound was heard.** The `hukbo-verify-and-record`
skill and `CLAUDE.md` section 6 are explicit that only a human at an interactive
desktop may flip a row in `docs/development/testing.md` to `PASS`. The canonical
gate builds `Release`, where the debug log defaults to off and the determinism
workload runs headless with no audio device at all. Eighty generated files can
pass the entire gate while being inaudible, mis-named, or wrong.

