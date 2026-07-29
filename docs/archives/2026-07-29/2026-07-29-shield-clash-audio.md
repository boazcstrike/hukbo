# Shield-clash audio — task list

> **Archived: reference only.** This plan is finished and is kept so the
> decision can be traced to its reasoning. Do not execute it, do not treat its
> versions or file paths as current, and do not cite it as justification for a
> change. The live contract is `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`, and
> `docs/development/testing.md`.


Date: 2026-07-30
Branch: `shield-clash-audio`, based on `main` at 69f20d0
Design: [2026-07-29-shield-clash-audio-design.md](2026-07-29-shield-clash-audio-design.md)

Revision 4. Revision 3's T1 and T3 were circularly dependent through
`SoundLogPanelTests.cs`: T1 raised the panel to 416 and asserted ten visible
binding rows, which is false at the nine slots on disk when T1 lands, while T3
added the four slots under a layout test asserting the visible count is at least
`AllSounds.Count`, which is false at 62 percent. Revision 4 splits the panel work
in two and puts the slot task between the halves, so **every task's stated
verification is true at that task's own landing point given only its declared
dependencies**. The arithmetic is in the `Ordering proof` section at the end.

Task IDs have been renumbered again. The order is now: retire the stale layout
assertion, add the slots, make the list scroll, then raise the panel.

Read the design document before starting any task. It carries the judgement
calls, the historical accuracy tiers, the panel arithmetic, the two scroll
precedents and which one T3 follows, and the `SoundDirector` trap that every code
task below depends on understanding.

## Outcome

Recorded 2026-07-30. Tasks T1 through T10 are implemented, committed, and green.
T11 is still outstanding: it needs the `ELEVENLABS_API_KEY` and has to be run by
hand by the owner, so this plan stays in `docs/plans/` and is not archived.

Commits on the `shield-clash-audio` branch, in the order they landed:

| Commit | Message | Tasks |
| --- | --- | --- |
| `0e4e202` | test(client): pin the real sound-log viewport cap and fix a stale test name | T1, T10 |
| `ae883f9` | feat(client): make the sound log expected-files list scrollable | T3 |
| `41c524e` | feat(client): give a shield block its own sound keyed to the attacking weapon | T2 |
| `e29998e` | Merge branch 'shield-clash-scroll' into shield-clash-audio | — |
| `7511b9b` | feat(client): widen the sound log to a ten-row viewport | T4 |
| `c4c146c` | chore(scripts): add default prompts for the four shield-clash slots | T5 |
| `b936f1b` | docs: record the shield-clash sound channel and correct four stale claims | T6, T7, T8, T9 |

The branch was also fast-forwarded onto `main` at `7f948e8` partway through the
work. That is why an unrelated `Hukbo.Core` test that was red at the original
branch point is green now: the fix for it came in from `main`, not from anything
in this plan.

The canonical gate `./scripts/verify.ps1` was run once after integration and
PASSED. Its real output reported: exit code 0; 747 Core tests passed with 0
failed; 2458 Client tests passed with 0 failed; and a headless determinism
workload of 1 run at 200 agents, 10,000 ticks, seed 1, finishing at tick 1279
with state hash `2410DD94F26C82E2` and event hash `56F66BBC10E69F0E`.

## What is not verified

The following statements are deliberately blunt. Nothing below has been proven,
and none of it may be reported as done.

- **The sixteen WAV files do not exist yet.** T11 has not been run. Until it is,
  all four clash slots report `MISSING`, and a shield block is therefore
  **silent** rather than wrong. The gate passing says nothing at all about
  whether the sounds are any good, because there are no sounds yet.
- **Smoke rows 172 through 176 in `docs/development/testing.md` are PENDING.**
  No interactive run was performed by anyone working on this plan. Only a human
  at an interactive Windows desktop may flip one of those rows.
- **Nothing automated covers the battle event log's loss of height** from the
  sound log going from 62 to 65 percent. `BattleEventLogPanel`'s constants are
  private and `ArenaGame` is banned from tests, so there is no test that can
  observe the shrunken event log. That change rests entirely on smoke row 176.
- **A `Hukbo.Core` test failed once during the implementer's run and once on the
  first canonical gate attempt.** It could not be reproduced in twelve
  consecutive Core runs afterwards, and its name was not captured either time.
  Nothing in this change touches `Hukbo.Core`. Both failures happened while
  other agents were running builds concurrently. This is recorded as an open
  observation about the Core suite, **not** as something this change caused or
  fixed, and it deserves its own investigation.

## How to verify while working

For a fast loop on one test class:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj `
  --configuration Release --filter "FullyQualifiedName~SoundCatalogTests"
```

For a task's full "Done when":

```powershell
./scripts/test.ps1 -Configuration Release
```

**No task below runs `./scripts/verify.ps1`.** The canonical gate is run once by
the orchestrator, after integration, and no agent's report substitutes for it.
No agent may flip a smoke-checklist row to `PASS`; every new row lands as
`PENDING`.

`TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not weaken a
test, a warning, or an analyzer to get green. `Hukbo.Core` is not opened by any
task on this list, and no task touches `ISoundPlayer.cs`,
`MonoGameSoundPlayer.cs`, `SoundLibrary.cs`, `SoundVariantSelector.cs`, or
`HitClass.cs` — the four new slots are ordinary classless slots and those files
already handle them.

## Tasks

| Task | What | Files | Done when | Depends on | Verified |
| --- | --- | --- | --- | --- | --- |
| **T1** | **SERIAL — the foundation for T2, T3, and T4. Retire the false layout assertion and replace it with a true one.** Delete `SoundLogPanelTests.CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize`, including its comment block at `:322-340` and its `420x396` literal at `:341`. **This is not weakening a test to get green — it is removing an assertion whose premise is false and replacing it with strictly stronger coverage in the same edit.** The comment claims "the panel is the documentation of what to name a file, so at the layout the client actually uses it must be able to list every slot", but `CalculateLayout` caps the binding viewport at `SoundCatalog.AllSounds.Count` rows (`Layout.cs:110-117`) while `BuildBindingRows` emits thirty-three today and thirty-seven after T2, so the panel has never listed every slot and no height can make it. In its place add `CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight`, which pins the real guarantee, and write the deleted comment's honest replacement above it: the panel shows a viewport of the first `AllSounds.Count` rows, the cap is deliberate, and reaching the rest is what T3's scrolling is for. Choose this name now so no later task has to rename it. Change nothing else in the file and do not touch `ArenaGame.cs` — the panel percentage moves in T4, after the slot count is final. | modify `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` | `./scripts/test.ps1 -Configuration Release` is green with the nine slots on disk. `CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight` asserts `GetVisibleBindingRowCount(CalculateLayout(new Rectangle(0, 0, 420, 2000))) == SoundCatalog.AllSounds.Count`, written as that expression and not as a literal, so it is true at nine slots now and at thirteen after T2 without being edited again. `grep -rn "ShowsEveryExpectedFileName" tests/` returns nothing. `grep -n "396" tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` returns nothing. `RightColumnSplitTests.cs` is unmodified and every method in it still passes. | — | Done — 0e4e202 |
| **T2** | **SERIAL after T1. PARALLEL-SAFE with T3 — the two file sets are disjoint. Add the four classless clash slots, the `ShieldBlocked` routing, and the mandatory director fix.** Append `ClashShieldKampilan = 9`, `ClashShieldWasay = 10`, `ClashShieldKalis = 11`, `ClashShieldItak = 12` to `GameSoundId` **and** all four to the end of `SoundCatalog.AllSounds` in the same edit — `AllSounds_ListsEveryDeclaredSlotExactlyOnce` enumerates the enum, so splitting them fails the build. Add the four `GetBaseName` arms returning `clash-shield-kampilan`, `clash-shield-wasay`, `clash-shield-kalis`, `clash-shield-itak`. Leave `IsHitLocationDriven` returning `false` for all four — they are ordinary classless slots and ride the existing `GetSlotVariantPrefix` path, so `SoundLibrary`, `MonoGameSoundPlayer`, and `ISoundPlayer` need no change at all and must not be opened. In `SoundCueMapper`, read `battleEvent.Resolution`: an `Attack` whose resolution is `ShieldBlocked` maps to the clash slot matching its `Weapon`, every other resolution keeps today's weapon impact slot, and an unmapped weapon still returns `null` and stays silent. **Then fix `SoundDirector.Ingest` at `:129-131` to derive the hit class from the mapped slot rather than from the event** — `SoundCatalog.IsHitLocationDriven(sound) ? HitClassCatalog.FromBodyPart(battleEvent.HitLocation!.Value) : (HitClass?)null`. This is mandatory, not optional: without it every clash cue resolves `Missing` forever, silently, with no crash, no failing test, and no panel complaint. See design section 6. | modify `src/Hukbo.Client/Audio/AudioTypes.cs`, `src/Hukbo.Client/Audio/SoundCatalog.cs`, `src/Hukbo.Client/Audio/SoundCueMapper.cs`, `src/Hukbo.Client/Audio/SoundDirector.cs`, `tests/Hukbo.Client.Tests/SoundCatalogTests.cs`, `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs`, `tests/Hukbo.Client.Tests/SoundDirectorTests.cs` | `./scripts/test.ps1 -Configuration Release` is green at the panel's unchanged 62 percent, because T1 has already removed the only assertion that compared visible rows against the slot count. `AllSounds_ListsEveryDeclaredSlotExactlyOnce` and `GetFileName_IsUniqueLowercaseKebabWavForEverySlot` pass with thirteen slots. `IsHitLocationDriven_IsTrueOnlyForTheFourWeaponSlots` gains four `false` `[InlineData]` rows, one per clash slot, taking it from nine rows to thirteen — it is a hand-written `[Theory]`, not an enumeration, so it silently would not cover them otherwise. New `SoundCatalogTests.EveryDefinedWeapon_HasAShieldClashSlot`, mirroring `EveryDefinedWeapon_HasAnAttackSlot` but building the event with `AttackResolution.ShieldBlocked`, so a weapon added later without a clash slot fails here instead of going silent. New `SoundCueMapperTests.Map_RoutesAShieldBlockToTheMatchingClashSlot`, a `[Theory]` over the four weapons. New `SoundCueMapperTests.Map_KeepsTheWeaponSlotForEveryOtherResolution`, a `[Theory]` over `Landed`, `Parried`, `Deflected`, `Evaded`. New `SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation`, ingesting a `ShieldBlocked` attack built with a real non-null `BodyPart.Head` and asserting `Assert.Null(Assert.Single(player.Played).HitClass)` **and** that the played slot is `ClashShieldKampilan` — the guard for the silent regression, which must fail if the director's derivation is reverted. Existing `Ingest_MapsTheEventHitLocationToTheAcousticHitClass` still passes unchanged. `RecordingSoundPlayer` is not modified. Every slot-count-sensitive assertion elsewhere is already derived rather than literal and passes untouched: `SoundLibraryTests.cs:14,47` read `SoundCatalog.AllSounds.Count`, and `SoundCueFormatterTests.cs:54-59` passes its own literals to `FormatAvailability` rather than reading the catalog. `git diff --name-only` lists no file under `src/Hukbo.Core/`, and none of `ISoundPlayer.cs`, `MonoGameSoundPlayer.cs`, `SoundLibrary.cs`, `ArenaGame.cs`, `SoundLogPanel.cs`, `SoundLogPanel.Layout.cs`, `SoundLogPanelTests.cs`. | T1 | Done — 41c524e |
| **T3** | **SERIAL against T1 (shares `SoundLogPanelTests.cs`). PARALLEL-SAFE with T2. Make the bindings list scrollable.** Follow **precedent B**, `BattleReportPanel` (`:60,71-91,113,135-162`), not precedent A, `BattleEventLogPanel`/`BattleEventFeed` — design section 5 gives the reason: precedent A needs a mutable model to host the offset and the bindings list has none, its rows coming from the pure static `BuildBindingRows` over an `IReadOnlyList<SoundBinding>` the audio layer owns. So `SoundLogPanel` gains a private `_bindingScrollStart` field, exactly as `BattleReportPanel` holds `_scrollStart` for a leaderboard it does not own. Do not put scroll state in `ISoundPlayer`, in `SoundCatalog`, or in `ArenaGame`. Add `BindingScrollbarTrackBounds` to `SoundLogPanelLayout` in `SoundLogPanelTypes.cs`, **appended as the last positional member** so no existing argument position moves; the existing `ScrollbarTrackBounds` belongs to the cue list, keeps its name, and must keep working. Narrow `BindingRowsBounds` by `ScrollbarWidth + 4` and place the new track at the right edge of the bindings area, mirroring `CueRowsBounds` at `Layout.cs:141-150`; this is a width change only and leaves every row-count assertion untouched. Add two pure helpers to `SoundLogPanel.Layout.cs`: `ClampBindingScroll(int scrollStart, int totalRowCount, int visibleRowCount)` returning a value in `[0, Math.Max(0, totalRowCount - visibleRowCount)]`, and `GetWheelTarget(SoundLogPanelLayout layout, Point pointer)` returning a new `SoundLogScrollTarget` enum. Call `ClampBindingScroll` from both `Update` and `Draw`, as `BattleReportPanel.ClampScroll` is called from both. Replace `Update:46-50`, which today sends every wheel delta to the cue log whenever the pointer is anywhere inside the panel — the **innermost-scrollable-region rule** is that the wheel scrolls the list under the pointer: `BindingsBounds` scrolls the bindings, `CueListBounds` scrolls the cues, and anywhere else in the panel keeps today's behaviour and scrolls the cues so the wheel is never swallowed. This refines the `hukbo-client-ui` skill's "the wheel over a panel scrolls only that panel" one level inward and does not conflict with it. **Delete the overflow line** at `SoundLogPanel.cs:169-170,201-207`: with a scrollable list `+N more (enlarge the panel)` names a remedy that does not work, so draw real content in all `visibleRowCount` rows and let the thumb be the affordance, matching the cue list at `:259-275`, which draws no overflow line. **Keep** the `bindingsHeight` cap at `desiredBindingsHeight` (`Layout.cs:110-117`): it caps the viewport, not the list, which is correct for a scrollable list, and removing it would let the bindings section grow on a tall window until only the reserved three cue rows remained. Do not add scrollbar click-paging — the cue list has none and the two must stay consistent. Do not rename `CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight`; T1 already gave it its final name. | modify `src/Hukbo.Client/UI/SoundLogPanel.cs`, `src/Hukbo.Client/UI/SoundLogPanel.Layout.cs`, `src/Hukbo.Client/UI/SoundLogPanelTypes.cs`, `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` | `./scripts/test.ps1 -Configuration Release` is green. Every new test is pure-helper style per the `hukbo-client-ui` skill — no `GraphicsDevice`, no `SpriteBatch`, and none constructs a `SoundDirector` or reads `SoundCatalog`, so none depends on whether T2 has landed. New `ClampBindingScroll_ReachesTheLastRow` uses a synthetic 37-row total and a 13-row viewport and asserts the maximum start is 24, so that `24 + 13 == 37` and the last row is reachable. New `ClampBindingScroll_RefusesToScrollPastEitherEnd` asserts a start of `-5` clamps to 0 and a start of `999` clamps to the maximum. New `ClampBindingScroll_ReturnsZeroWhenEveryRowFits` asserts 0 for any start when `totalRowCount <= visibleRowCount`, so a short list cannot scroll. New `GetWheelTarget_RoutesTheWheelToTheListUnderThePointer` asserts `Bindings` for a point inside `BindingsBounds` and `Cues` for a point inside `CueListBounds`. New `GetWheelTarget_FallsBackToTheCueListOutsideBothLists` asserts `Cues` for a point in `HeaderBounds`. `SoundLogPanelTests.Regions` at `:348-358` yields `BindingScrollbarTrackBounds` as a ninth region and the existing containment test still passes. `SoundCueLogTests` passes with that file unmodified, proving the cue list's own scrolling is unaffected. `grep -rn "enlarge the panel" src/` returns nothing. `git diff --name-only` lists no file outside the four named here. | T1 | Done — ae883f9 |
| **T4** | **SERIAL after T2 and T3. Raise the panel and pin the new exact fit.** Set `ArenaGame.SoundLogHeightPercent` from 62 to 65 and rewrite the derivation comment above it. The comment must now say: the percentage buys a **ten-row viewport** onto a thirteen-slot, thirty-seven-row list, not a view of the whole list; ten rows need a real panel height of 416; `640 * 65 / 100 == 416` exactly under integer division; and the rest of the list is reached by scrolling, which T3 added. Leave `SoundLogMinimumHeight = 236` alone and say in the comment why it is unaffected — it reserves a single binding row and is independent of slot count. Then add `SoundLogPanelTests.CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen`, pinning both sides of the boundary so 416 is recorded as the exact minimum rather than a comfortable one, with a comment carrying the derivation: `available = H - 110`, `bindingsHeight = min(20 + 13*20, H - 196)`, ten rows need `H >= 416`, and the right column at the default 1280x720 window is `720 - 68 - 12 = 640` tall. Change no other test. | modify `src/Hukbo.Client/ArenaGame.cs`, `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs` | `./scripts/test.ps1 -Configuration Release` is green. `CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen` asserts `GetVisibleBindingRowCount(CalculateLayout(new Rectangle(0, 0, 420, 415))) == 9` **and** `GetVisibleBindingRowCount(CalculateLayout(new Rectangle(0, 0, 420, 416))) == 10`, both of which hold only because T2 has already taken `AllSounds.Count` to thirteen — see the `Ordering proof` section. `CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight` from T1 still passes and now evaluates to thirteen. Every method in `RightColumnSplitTests.cs` still passes with that file unmodified, because it passes its own `soundLogMinimumHeight: 168, soundLogHeightPercent: 45` literals and never reads the `ArenaGame` constants. | T2, T3 | Done — 7511b9b |
| **T5** | **PARALLEL-SAFE with T6, T7, T8, T9, T10.** Add four default prompts to the generation script. Insert four `$defaultPrompts` entries at `:179-233`, keyed `clash-shield-kampilan`, `clash-shield-wasay`, `clash-shield-kalis`, `clash-shield-itak`, each with `Duration = 0.5` and `Trim = $true`, using the four prompt texts **verbatim** from design section 8. No prompt may name a weapon, a shield type, or a people — that half of the naming rule is binding. **Change nothing else.** The `-Class` `ValidateSet` at `:94` and the `attack-` throw at `:601-603` stay exactly as written: four classless slots need no `-Class`, and the existing throw correctly rejects `-Slot clash-shield-wasay -Class skull`. `Get-CatalogSlot` at `:241` picks the four new slots out of `SoundCatalog.cs` with no edit. `Get-SlotPath` at `:249-272` already produces `clash-shield-wasay-01.wav` from `-Index 1`. | modify `scripts/sfx.ps1` | `./scripts/sfx.ps1 -List` prints thirteen slots — true only because T2 added the four `GetBaseName` arms the script scrapes — each with a default prompt and none reading `(no default prompt)`. `./scripts/sfx.ps1 -Slot clash-shield-wasay -Index 1 -DryRun` prints an output path ending `clash-shield-wasay-01.wav` and exits at the `-DryRun` return on `:622-625`, which is **before** `Get-ApiKey` on `:627`, so this is verifiable with no API key present. `./scripts/sfx.ps1 -Slot clash-shield-wasay -Class skull -DryRun` still throws the `-Class applies only to an attack slot` error. `./scripts/sfx.ps1 -Slot attack-kampilan -Class skull -Index 1 -DryRun` still resolves `attack-kampilan-skull-01.wav`. `git diff scripts/sfx.ps1` touches only the `$defaultPrompts` block. | T2 | Done — c4c146c |
| **T6** | **PARALLEL-SAFE with T5, T7, T8, T9, T10.** Correct and extend the audio folder's naming contract. Add a `Variants` subsection documenting the `<slot>-NN.wav` scheme the file has never described: `NN` is exactly two digits and one-based, matching is case-insensitive, the four attack slots additionally carry a hit-class token in the form `<slot>-<class>-NN.wav`, and a bare `<slot>.wav` is a last-resort fallback that the shipped set does not use. Add four rows to the `File names` table, one per clash slot, each reading that it plays when a blow from that weapon is stopped by a shield. State that each weapon is its own slot, so there is no substitution between them: a slot with no take is silent and reports `MISSING`. Update the paragraph about opening the sound log to say that the expected-files list now scrolls with the wheel while the pointer is over it, so every slot is reachable even though ten rows show at once. Correct `:83-84`: the budget is **16 cues of one slot and 64 in total per frame**, not three and eight. | modify `src/Hukbo.Client/Content/Audio/README.md` | The file contains no occurrence of "three cues" or "eight cues"; `grep -n "16\|64" src/Hukbo.Client/Content/Audio/README.md` shows both figures in the rate-limiting section. The `File names` table has thirteen rows, matching the thirteen slots T2 shipped. The Variants subsection names `clash-shield-kampilan-01.wav` and `attack-kampilan-skull-01.wav` as worked examples. The "ten rows show at once" figure matches the 65 percent T4 shipped. Prose is full normal English with no compression pass. | T2, T3, T4 | Done — b936f1b |
| **T7** | **PARALLEL-SAFE with T5, T6, T8, T9, T10.** Retire the shield cue from the deferred list and correct three wrong statements. Remove the `clash-shield.wav` row from the `Clash slots` table, leaving `clash-blade-hard` and `clash-blade-soft`, and add a short paragraph recording that the shield cue shipped on 2026-07-30 as **four** weapon-keyed classless slots rather than the one classless single this file proposed, with a link to the design document. **Delete the words "Wood and hide"** wherever they survive: hide appears nowhere in the research for shields, only as carabao-hide armor — see design section 7. Rewrite `:24-45` and `:33-35`. The claim that a tenth slot "silently hides itself" was wrong on two counts: the panel already drew `+N more`, and the real constraint was that the binding list was capped at `AllSounds.Count` visible rows while `BuildBindingRows` emits thirty-seven, so twenty-four rows were unreachable at any window size. **Both are now resolved** — the list scrolls, every rendered row is reachable, and the panel-space question this file said "gates every slot on this page" is answered and gates nothing further. Say that plainly and delete the three-option list of possible remedies, since the scrolling option was taken. Correct `:75` from "3 per slot and 8 per frame" to 16 and 64. Update the opening line from "nine members" to thirteen. In `What to do when a slot is wanted`, drop step 1 entirely and sharpen step 3: a classless slot must reach the player under `(sound, null)`, and since `SoundDirector` now derives the hit class from `SoundCatalog.IsHitLocationDriven(sound)` rather than from the event, adding a classless slot needs no director change — but changing that derivation back would silence every classless slot that carries a hit location. | modify `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` | `grep -in "hide" src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` returns nothing. `grep -n "silently hides itself"` returns nothing. `grep -n "3 per slot and 8 per frame"` returns nothing. `grep -n "scrollable expected-files list"` returns nothing, the option having been taken rather than proposed. The `Clash slots` table has two rows. The "thirteen members" figure matches the enum T2 shipped and the scrolling claim matches what T3 shipped. The swing-slot section is left intact. Prose is full normal English with no compression pass. | T2, T3 | Done — b936f1b |
| **T8** | **PARALLEL-SAFE with T5, T6, T7, T9, T10.** Correct the contract text this change falsifies. Rewrite the sentence at `:894-896` — currently "The three clash sound slots that would have given it a fourth channel are deferred by owner decision and are not part of this contract" — to state that a shield block now has a sound channel carried by four slots keyed to the attacking weapon, that `clash-blade-hard` and `clash-blade-soft` remain deferred by owner decision, and that `Evaded` still has no sound channel of its own and remains the weakest case for the same reason as before. Add a `Sound cue` row to the Spectator channels table at `:886-892` reading `weapon impact` under `Landed`, `` `clash-shield-<weapon>` `` under `ShieldBlocked`, and `weapon impact` under each of `Parried`, `Deflected`, and `Evaded` — the row must be honest that four of the five resolutions still share one cue. Confirm the anchor text byte-for-byte with `Grep` before editing; the rendered view of this file is lossily compressed. | modify `SIMULATION-GAME-STANDARDS.md` | `grep -n "three clash sound slots" SIMULATION-GAME-STANDARDS.md` returns nothing. `grep -n "Sound cue" SIMULATION-GAME-STANDARDS.md` returns one row inside the Spectator channels table, and that table still has six columns. This task states no slot count and no panel figure, so it depends on T2 only for the routing behaviour it describes. The pinned preset hash `0x59FB4CA563D87A49` and every other figure in the surrounding section are unchanged — `git diff --stat SIMULATION-GAME-STANDARDS.md` shows edits confined to the Spectator channels block. | T2 | Done — b936f1b |
| **T9** | **PARALLEL-SAFE with T5, T6, T7, T8, T10.** Correct three stale smoke rows and add a new section. Row 24 at `:3264`: "all nine expected file names" becomes thirteen, and `MISSING 9/9` becomes `MISSING 13/13`; add that the list now scrolls, so all thirteen are reachable even though ten show at once. Row 25: `MISSING 8/9` becomes `MISSING 12/13`. Row 48: keep the existing per-class expectations for the four attack slots and `death`, and add that each of the four clash slots reports `READY` with four takes, sixteen across the four, and that each weapon is its own slot so a missing one shows its real count with no substitution from another weapon. Edit these rows in place; do not add siblings and leave the originals stale. Append a new `### Shield-clash audio smoke` section at the end of the interactive smoke checklist, immediately before `## Failure classification` at `:4145` — row 171 at `:3878` is the highest existing number, so the new rows begin at 172. Open the section with a scoping note stating that no interactive run was performed, naming the automated tests that exist, and saying what they do not prove. The rows: **172** a shield-blocked blow sounds like a weapon striking a light wooden board, plainly different from a landed cut, without reading the event log; **173** the War Axe reads heavier and blunter than the Work Blade against the same shield, and the Work Blade is the quietest of the four; **174** open the sound log, put the pointer over the expected-files list, and scroll — the list moves through all thirty-seven rows, reaches `clash-shield-kampilan` through `clash-shield-itak` at the bottom with each reading `READY (4)`, refuses to scroll past either end, and shows no `+N more` line anywhere; scrolling with the pointer over the cue log below still scrolls only the cue log, and neither scroll zooms the arena camera; a run with `-LogLevel dbg` whose `assets.sound.scanned` line reports thirteen slots and thirteen ready is a secondary confirmation of the same fact; **175** through a full 200-agent battle the shield cue does not become a wall of noise and the cue log shows no `LIMITED` or `REFUSED` row for any clash slot; **176** with the sound log open at its new height the battle event log still reads — the selected-event pane shows its header and two detail lines, and nothing is clipped. **Every new row lands as `Not run` / `PENDING`.** | modify `docs/development/testing.md` | `grep -n "MISSING 9/9\|MISSING 8/9" docs/development/testing.md` returns nothing. The `13/13` and `12/13` figures match the thirteen slots T2 shipped, the scrolling in row 174 matches what T3 shipped, and the "ten show at once" figure in row 24 together with row 176's two detail lines match the 65 percent T4 shipped — which is why this task depends on all three. Rows 172 through 176 exist, each with a `Not run` column and a `PENDING` verdict, and `grep -c PENDING` over the new section returns 5. No existing row's verdict changed — `git diff docs/development/testing.md` shows no `PENDING` becoming `PASS` anywhere. Prose is full normal English with no compression pass. | T2, T3, T4 | Done — b936f1b |
| **T10** | **PARALLEL-SAFE with every other task, including T1 — no other task touches this file.** Rename `SoundCueBudgetTests.DefaultLimits_AllowAtMostThreeOfOneSlotAndEightInTotal` to `DefaultLimits_CapOneSlotAndTheFrameAtTheDeclaredMaxima`. The body already asserts against `SoundCueBudget.DefaultMaximumPerSound` and `DefaultMaximumTotal` rather than literals, so only the name is wrong; the real limits have been 16 and 64 since the capacity measurements. Do not change any assertion. | modify `tests/Hukbo.Client.Tests/SoundCueBudgetTests.cs` | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --configuration Release --filter "FullyQualifiedName~SoundCueBudgetTests"` is green and lists the new method name. The test body iterates `SoundCatalog.AllSounds` but asserts only against the two constants, so it is true at nine slots and at thirteen. `grep -rn "ThreeOfOneSlotAndEight" tests/` returns nothing. | — | Done — 0e4e202 |
| **T11** | **HUMAN — needs `ELEVENLABS_API_KEY`, run by hand, never by an agent.** Generate sixteen takes: `-Index 1` through `-Index 4` for each of `clash-shield-kampilan`, `clash-shield-wasay`, `clash-shield-kalis`, `clash-shield-itak`. With T5 landed the prompts are defaults, so `./scripts/sfx.ps1 -Slot clash-shield-wasay -Index 2 -PromptInfluence 0.5` is the whole command. Do **not** generate a bare `clash-shield-<weapon>.wav`; the shipped attack slots carry no bare single either. Judge every take by ear before keeping it: no metallic ring, no reverb tail, no voice, and the four audibly distinct with the War Axe heaviest and the Work Blade quietest. Re-roll anything that vocalises or peaks low. `GENERATED.md` is written by the script through `Add-ProvenanceRow` and must not be hand-edited. | create sixteen `.wav` files under `src/Hukbo.Client/Content/Audio/`; `src/Hukbo.Client/Content/Audio/GENERATED.md` is appended by the script | Sixteen files exist with the exact names above. `./scripts/sfx.ps1 -List` still reports all four clash slots MISSING, which is expected and pre-existing — `-List` probes the bare single, exactly as it already does for the four attack slots. Scrolling the sound log's expected-files list to the bottom shows all four reading `READY (4)`. `GENERATED.md` has sixteen new provenance rows written by the script. The key never appears in a tracked file, an echoed line, or a commit message. | T5 | Done — 765ca38 |

## Dependency order

```
T10  ─────────────────────────────────────────────────  (independent, any time)

              ┌──▶  T2 (slots)  ──┬──▶  T4 (percent)  ──┬──▶  T6, T9
T1 (retire) ──┤                   ├──▶  T5  ──▶  T11 (human)
              └──▶  T3 (scroll)  ─┤                     └──▶  T7
                                  └──▶  T8
```

T1 first, because it is the only task that removes the false assertion, and both
T2 and T4 are blocked by it in opposite directions. T2 and T3 are genuinely
parallel: disjoint files, and T3's tests are synthetic and never read
`SoundCatalog`, so they do not care whether the slot count is nine or thirteen.
T4 last of the code tasks, because its exact-fit assertion is true only at
thirteen slots, and because it shares `SoundLogPanelTests.cs` with T3.

## Stage 3 — how to spawn the implementers

Seven agent slots across four waves, under the eight-agent ceiling. Every agent
gets a file set that no other agent in its wave touches.

**Wave 1 — two agents in parallel.**

- **Agent A — T1.** Owns `tests/Hukbo.Client.Tests/SoundLogPanelTests.cs`.
- **Agent B — T10.** Owns `tests/Hukbo.Client.Tests/SoundCueBudgetTests.cs` and
  nothing else. No task in any wave touches that file.

**Wave 2 — two agents in parallel, after Agent A reports T1 green.**

- **Agent C — T2.** Owns four files under `src/Hukbo.Client/Audio/` and three
  Client test files.
- **Agent D — T3.** Owns the three `SoundLogPanel*` files under
  `src/Hukbo.Client/UI/` plus `SoundLogPanelTests.cs`. Disjoint from Agent C.

**Wave 3 — one agent, after both T2 and T3 are green.**

- **Agent E — T4.** Owns `src/Hukbo.Client/ArenaGame.cs` and
  `SoundLogPanelTests.cs`. It is alone in its wave because it needs the slot count
  from T2 and the test file from T3, and because it is a two-file change that does
  not justify splitting.

**Wave 4 — three agents in parallel, after T4 is green.**

- **Agent F — T5.** Owns `scripts/sfx.ps1`. Strictly this only needs T2, so it may
  be pulled forward into wave 3 alongside Agent E if the orchestrator prefers; its
  files collide with nothing.
- **Agent G — T6 and T7.** Owns `src/Hukbo.Client/Content/Audio/README.md` and
  `PENDING-SOUNDS.md`. Adjacent in subject and both correcting the same stale
  budget figure, so one agent keeps the two consistent.
- **Agent H — T8 and T9.** Owns `SIMULATION-GAME-STANDARDS.md` and
  `docs/development/testing.md`. Both are contract documents describing the same
  spectator channel, so one agent keeps the two statements consistent.

**Then, outside the agent pipeline:** the owner runs T11 by hand with the API
key, and the orchestrator runs `./scripts/verify.ps1` once after integration and
records the real output. No agent runs the gate, and no agent flips a
smoke-checklist row.

## Ordering proof

Every layout assertion in the plan, evaluated at the slot count in force when its
task lands. `CalculateLayout(new Rectangle(0, 0, 420, H))` reduces to:

```
verticalPadding = 10                     (H/8 exceeds 10 for any H >= 80)
available       = (H - 10) - 100         = H - 110
cap             = available - Gap(6) - reservedCue(80)   = H - 196
desired         = SectionHeaderHeight(20) + N * BindingRowHeight(20)
bindingsHeight  = min(desired, max(0, cap))
visibleRows     = (bindingsHeight - 20) / 20
```

**The two failures revision 3 contained, both confirmed against
`SoundLogPanel.Layout.cs:110-117`.**

Revision 3's T1 raised the panel to 416 and asserted ten visible rows while nine
slots were still on disk:

```
H = 416, N = 9:   cap = 220,  desired = 200,  bindings = 200,  rows = 9
```

Nine, not ten. Ten visible rows need `desired >= 220`, so `N >= 10`, which only
the slot task delivers. Revision 3's T1 could not go green alone.

Revision 3's T3 added the four slots while
`ShowsEveryExpectedFileNameAtTheDefaultSize` still asserted
`visibleRows >= AllSounds.Count` at the unchanged 62 percent:

```
H = 396, N = 13:  cap = 200,  desired = 280,  bindings = 200,  rows = 9
```

and `9 >= 13` is false, so revision 3's T3 could not go green alone either. The
two tasks each needed the other first. Both computations are as the coordinator
gave them.

**Revision 4, every assertion at its own landing point.**

| Task | N in force | Assertion | Arithmetic | Holds |
| --- | ---: | --- | --- | --- |
| T1 | 9 | `CapsTheBindingViewport...` at `H = 2000` equals `AllSounds.Count` | cap 1804, desired 200, bindings 200, rows 9 | 9 == 9 ✓ |
| T1 | 9 | nothing else asserts a row count, the false test having been deleted | — | ✓ |
| T2 | 13 | `CapsTheBindingViewport...` still passes, untouched | cap 1804, desired 280, bindings 280, rows 13 | 13 == 13 ✓ |
| T2 | 13 | no assertion compares rows against slots at 62 percent, T1 removed it | — | ✓ |
| T3 | 13 | scroll helpers are synthetic and read no catalog | totals passed as literals | ✓ |
| T4 | 13 | `FitsExactlyTenBindingRows...` at `H = 415` | cap 219, desired 280, bindings 219, rows `199 / 20` | 9 ✓ |
| T4 | 13 | `FitsExactlyTenBindingRows...` at `H = 416` | cap 220, desired 280, bindings 220, rows `200 / 20` | 10 ✓ |
| T4 | 13 | `CapsTheBindingViewport...` at `H = 2000` still passes | as T2 | 13 == 13 ✓ |

The coordinator's figures for the two-sided test at thirteen slots — 416 gives
ten, 415 gives `min(280, 219) = 219` and `(219 - 20) / 20 = 9` — are correct as
given.

The window height that makes `CapsTheBindingViewport...` meaningful is any `H`
where `cap >= desired`, that is `H >= 196 + 20 + 20N`: 376 at nine slots, 476 at
thirteen. `H = 2000` clears both by a wide margin, so the test is introduced at a
point where it is true and stays true across the slot-count change. Below that
threshold the cap is the layout, not the slot count, and the assertion would be
comparing the wrong two numbers.

Finally, the real column geometry the percentages act on, unchanged from
revision 3: `RightColumnSplit.Split` receives a column
`720 - StatusBarHeight(68) - LayoutMargin(12) = 640` tall and takes
`availableHeight * percent / 100` under integer division, so
`640 * 62 / 100 = 396` today and `640 * 65 / 100 = 416` after T4, leaving the
battle event log `640 - 416 - 10 = 214`.
