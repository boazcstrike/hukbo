# Weapon clash — session handoff

Written 2026-07-27 to move this work into a fresh session. Read this file first,
then the four files in section 2, then start at section 7.

---

## 1. Where the work lives

| | |
| --- | --- |
| Worktree | `C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\weapon-clash` |
| Branch | `worktree-weapon-clash` |
| Base | merged with `main` at `7abf8fc`, which carried `2d88b43` |
| Merged into `main`? | **No.** Nothing is merged. The branch is ahead by the whole feature |

**Run every command from the worktree, not the main checkout.** The scripts are
PowerShell 7 — invoke them with the PowerShell tool, not the Bash tool, or they
fail with `syntax error near unexpected token '['`.

Do not `git stash` in this repository; the stash stack is shared with other
worktrees and other sessions. Use a temporary WIP commit instead.

---

## 2. Read these, in this order

1. `CLAUDE.md` — §5 non-negotiables, §7 historical accuracy policy, §9 the
   do-not list. Binding.
2. `docs/plans/2026-07-27-weapon-clash.md` — the task plan. 68 tasks, T01–T68,
   grouped into five phases. **Read the "How to read this plan" subsection on
   RED/GUARD before touching any test.**
3. `docs/plans/2026-07-27-weapon-clash-design.md` — the design. §2.2 the tuning
   tables, §3.3 the six-step arithmetic, §3.8 spectator discoverability, §5 the
   existing-test dispositions, §5.1 the ruleset seam, §9 the control run.
4. `docs/research/WEAPON_CLASH_1500s.md` — the historical evidence and every
   provisional number, with its confidence.

Also worth reading before specific work:

- `SIMULATION-GAME-STANDARDS.md` §4 determinism contract, §10 the nine questions.
- `.claude/skills/hukbo-determinism-change/` — before editing anything that
  reaches a hash.
- `.claude/skills/hukbo-verify-and-record/` — before running or reporting the gate.
- `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` — the deferred sound work.

---

## 3. What the feature is

Three parts, from one request:

1. **Weapon clash** — a defensive resolution stage in `Hukbo.Core`. An accepted
   attack can be blocked by a shield, parried hard, deflected softly, evaded, or
   land. Five outcomes on `AttackResolution`.
2. **Sword swing animation** — presentation only, in `Hukbo.Client`. The pawn
   weapon is currently a static line in `PawnGeometry`.
3. **Clash sounds** — **deferred by the owner.** See section 6.

---

## 4. State as of this handoff

### Phase 0 — done, Barrier B0 green

Twelve tasks. Contract types, the ruleset injection seam, neutral stubs, and the
pre-change digest fixture. **Nothing changes behaviour yet**: `ClashResolver.Resolve`
returns `Landed` unconditionally, which is why the hashes below still match a
clean run.

What Phase 0 landed and later phases depend on:

- `AttackResolution` enum, values pinned `Landed = 0` through `Evaded = 4`.
- `ClashProfile` with `ClashProfile.Neutral`, an all-zero-interception profile
  that is the constructor default.
- `BattleEvent.Resolution`, nullable, **optional on the `Attack` factory
  defaulting to `Landed`** — it has twenty call sites across eleven files and a
  required parameter would have made the barrier unsatisfiable.
- `CombatMetrics` plus both consumers stubbed: `BattleSimulation.LastTickCombat`
  and `RunReport.CombatMetrics`.
- `ClashResolver` stub: `MixClash`, `Resolve`, `SplitWeaponChannel`, and
  `ComputeChannels` returning a zeroed `(shield, weapon, hard, soft, void)`.
- `CombatRuleset.ClashProfile` and `WithClashProfile(ClashProfile)`.
- **The seam**: `BattleSimulation.Create(Scenario, CombatRuleset)` and
  `CreateForTesting(Scenario, CombatRuleset, params AgentState[])`, plus
  `StateHasher.Compute` now taking `ulong contentHash` instead of re-fetching the
  ruleset, plus `internal ulong ComputeStateHash(ulong contentHash)`.
- `NaiveClashResolution.cs`, the independent oracle. It calls no production helper.
- `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`.

### Phase 1 — done, Barrier B1 satisfied

Eleven test tasks, T13–T23, in five commits: `9f0bb41`, `d275c18`, `9dc54f6`,
`f961506`, `95a3036`.

**All 38 named cases matched their labels.** 81 failing cases across 29 methods,
and the 81 are exactly the RED set — no RED passed, no GUARD failed, no compile
error. The Core suite is *expected to be red* right now. After the `main` merge
described in section 5 the suite grew by main's own 71 cases and the count reads:

```
Total tests: 539
     Passed: 457
     Failed: 82
```

81 of those 82 are the RED set, unchanged. The eighty-second is
`RepeatedCollisionTicksHaveBoundedAllocations`, which the merge broke and which
is described at the end of section 5.

`./scripts/test.ps1` throws on the Core failure and never reaches the Client
project, so run `Hukbo.Client.Tests` directly to see it: 564 passed, 0 failed.

**Do not "fix" those 81.** They are the specification for Phase 2. They go green
as the resolver, the preset values, and the attack-stage integration land.

The two guards that matter most already pass:
`ZeroInterceptionProfile_ReproducesThePreClashDigest` across all 1081 rows, and
`ZeroInterceptionProfile_ReproducesTheRecordedStateHash`.

**One deviation, and it was the right call.** T22 asks for the event-hash theory
to cover "every resolution pair including the null sentinel". That row is not
satisfiable as a RED: a null `Resolution` is unreachable on an attack event —
`BattleEvent.Attack` requires a defined value and `NonAttack` forces null — so any
null-versus-defined pair also differs in `Kind` and its hashes already differ
today. It would have been a RED that passes, which the barrier treats as hard a
block as a build error. Implemented as the 10 distinct pairs of the five defined
resolutions, all RED, with the pre-existing
`EventHashMixer_NullCombatContextIsStableAndDistinctFromDefinedValues` gaining
resolution clauses and staying GUARD. Reasoning recorded at
`tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs:132-136`.

### Phases 2 to 4 — not started

- Phase 2 (T24–T34): the resolver, ruleset fold, preset values, attack-stage
  integration, metrics, hash re-baseline, acceptance re-tune.
- Phase 3 (T35–T59): client fan-out. **3b and 3c are dropped** with the sound
  deferral; 3a, the swing animation, still stands.
- Phase 4 (T60–T68): gate, workloads, oracle re-record, smoke rows, archive.

---

## 5. Recorded values — use these, ignore older ones

`main` has been merged three times during this feature. The current merge,
`7abf8fc`, brought the last-stand formation and the collision priority
amendment, both authoritative movement changes, plus the JSON Lines debug log,
the sound gain compensation, and the documentation archive move. **Any number you
find from before that merge is stale.**

```
terminal tick   1154        (657, then 1081, then 1176, now 1154)
state hash      5BEBA7A68F69BE0D
event hash      D379B60B2E30FFFC
content hash    0x59FB4CA563D87A49UL     unchanged by every merge so far
fixture rows    1154
allocated       78,806,784 bytes
```

Both hashes are byte-identical to what `main` reports on its own, which is the
evidence that Phase 0 remains hash-neutral: `ClashResolver.Resolve` still returns
`Landed` unconditionally and the neutral profile intercepts nothing. Allocation
is 9.9 per cent above main's 71,698,480, the documented cost of widening
`BattleEvent` from 80 to 88 bytes for the nullable resolution.

The content hash is still pinned twice in the suite, at
`tests/Hukbo.Core.Tests/CombatConfigurationTests.cs:145` and
`tests/Hukbo.Core.Tests/DeterminismTests.cs:54`. **T32 re-baselines both, and only
after T19 goes green.** The literal `0x59FB4CA563D87A49UL` that T21 passes as a
*content-hash argument* is not one of those goldens and must not be swept up in
that edit.

### What the `7abf8fc` merge cost, and what it left open

`main` took the **last-stand formation** (`6b4f809`), which redirects a faction's
last survivors onto their own leader once it drops to
`Scenario.LastStandThresholdAgents` or fewer, and then the **collision priority
amendment** (`c01ea9f`), which resolves contested ground by a per-tick priority
key. Both are authoritative movement changes and both moved the hashes. The
merge itself also had to resolve one conflict and left one test failing:

- **One merge conflict**, in `SeedsOneThroughTwentyProduceVictoriesForBothFactions`.
  Both sides had extended the same test. Both properties are kept: main's
  fairness clause, at least four victories per faction rather than the original
  one, and this branch's T22/T23 termination clause, nineteen of twenty seeds
  decisive with a median at or below 5,000. The `outcomes` HashSet was dropped
  because the victory counters subsume it.
- **The digest fixture was recaptured** by the procedure below and now holds
  1154 rows captured at `7abf8fc`. `ZeroInterceptionProfile_ReproducesThePreClashDigest`
  passes across every row.
- **`DeterminismTests.PreClashTerminalStateHash` was re-baselined** to
  `0x5BEBA7A68F69BE0D`. It is the value the capture harness recorded, not a
  golden edited to match output; the per-tick digest guard proves the same run
  row by row and it passes.
- **Criterion two was re-derived.** At a mean interception of 0.325 the 1.48
  factor now predicts a terminal tick near 1710 rather than 1600, still inside
  the 5,000 median clause.
- **`RepeatedCollisionTicksHaveBoundedAllocations` is left failing**, at 988,192
  bytes against its 900,000 ceiling. The test is named nowhere in the plan or the
  design, so no task owns it. `main` passes it at roughly 898,000 bytes, within a
  fifth of a per cent of its own ceiling, and Phase 0's 88-byte `BattleEvent`
  pushed it over — the same 9.9 per cent that shows up in the whole-workload
  allocation figure. It is a budget whose input legitimately grew, not a
  regression in the collision stage, but raising a ceiling is a decision rather
  than a merge mechanic and it was left visible instead of quietly widened.

Five deployment changes have now landed on `main` during this feature's planning
and first two phases. Assume a sixth. The cost each time is one fixture recapture
plus a plan-constant sweep, so merging early and often is cheaper than merging at
the end.

### If the hashes move again

They will, the moment Phase 2 lands — that is intended and T32 re-records them.
They will also move on any merge like the one above. The digest fixture must then
be recaptured:

1. The capture harness source is embedded in the fixture's own
   `provenance.harnessSource` array. Extract it to
   `tests/Hukbo.Core.Tests/PreClashDigestCaptureHarness.cs`.
2. Set `HUKBO_PRECLASH_DIGEST_OUT`, `HUKBO_PRECLASH_HARNESS_SRC`, and
   `HUKBO_PRECLASH_COMMIT`, then
   `dotnet test tests/Hukbo.Core.Tests --configuration Release --filter FullyQualifiedName~PreClashDigestCaptureHarness`.
3. Delete the harness file. It is never committed.
4. Correct every recorded hash, row count, and derived prediction in both plan
   documents.

---

## 6. Owner decisions already made — do not re-litigate

**Sounds are deferred.** No clash slots in `GameSoundId` or `SoundCatalog`, no
cue mapping, no WAV files. A clash resolves in the simulation and names itself in
the battle event log, and the audio layer stays silent. The owner wants each
sound decided individually against a real battle. T12 is marked DEFERRED in the
plan and Phase 3b and 3c fall away with it.

**There is also a hard blocker behind that**, recorded in
`src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md`: after `main`'s font overhaul
the sound log panel's expected-files section caps at 200 pixels at the real
`420x396`, and the nine current slots need exactly 200 — zero slack. Row height
now derives from measured baked Caption-rung line spacing, so it cannot shrink
without clipping descenders. **Any tenth slot silently hides itself.** Adding one
requires a taller panel (which takes height from the event log and touches
`RightColumnSplit`), a scrollable list, or dropping the cue log's reserved rows.
That decision is unmade and should be made once, for however many slots are
eventually wanted.

**Swing sounds are an open question with no design**, also in that file.

---

## 7. What to do next

**Start Phase 2, T24–T34.** Phase 1 is done and B1 is satisfied.

1. Settle `RepeatedCollisionTicksHaveBoundedAllocations` — see the end of
   section 5. It is a failing guard that no task owns, and the plan's own barrier
   rule treats a failing guard as a hard block.
2. T24's nine existing-test dispositions come **before** any attack-stage edit.
   They use the seam; do not hand-pick lucky-roll seeds. No shipped pairing is
   clash-neutral — the minimum total is 2000 basis points.
3. Then the resolver, the ruleset content-hash fold, the preset values, the
   attack-stage integration, metrics accumulation, and the hash re-baseline.
4. T32 re-baselines the two golden content-hash constants, and **only after T19
   is green.**
5. Then Phase 3a, the swing animation, **plus T54 rescued out of the dropped
   Phase 3b**. T54 is not audio work: it gives the battle event log a distinct
   action label per resolution, stops a non-landed attack reading as a bare zero
   damage line, and extends the feed's defence-in-depth guard. Without it no
   spectator can tell a parry from a block from a landed blow, which fails the
   discoverability question in `CLAUDE.md` §6, and T65's smoke row requiring the
   event log to distinguish all five resolutions is unsatisfiable. The owner
   approved the rescue on 2026-07-27. The rest of 3b and all of 3c stay dropped.
6. Then Phase 4.

The 81 red Core tests are Phase 2's specification. Watch them go green; any that
does not is either an incomplete task or a defect worth stopping for.

Barriers, in the plan's own words, are hard. Do not start a phase whose
predecessor's barrier is not green.

### The gate

```powershell
./scripts/verify.ps1 -SkipBootstrap
```

Five stages: format verification, Release build, Core tests, Client tests, and a
200-agent / 10,000-tick / seed-1 headless determinism workload. **There is no CI.**
Never claim a change is verified without pasting the actual output.

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
```

---

## 8. Things that have already bitten, and will again

**RED versus GUARD is the barrier.** Every Phase 1 and Phase 3a case is labelled.
A RED must fail now on an assertion and pass after Phase 2. A GUARD must pass
now and keep passing. A RED that passes blocks the barrier as hard as a build
error, and so does a GUARD that fails. This scheme exists because an earlier
revision demanded every case fail, which was false for eight of them and would
have forced either a weakened guard or a dishonest report.

**A test that fails to compile is not a red test.** Five separate times, review
found a Phase 1 test referencing a type or member that Phase 2 adds. One missing
type fails the whole assembly and takes every other case with it. Phase 0 exists
to stub the entire surface for exactly this reason. If you hit it again, the fix
is a Phase 0 stub, never a workaround in the test.

**No resolver test may read `PhilippineCombatPreset`.** At Phase 1 the profile is
neutral, so a test reading it compares zeros against zeros, passes green, and
proves nothing. T13, T15, T16 and T17 each construct an explicit literal
`ClashProfile` in the test file.

**The oracle must stay independent.** `NaiveClashResolution.cs` calls no
production helper. Design §3.3 states normatively that the hard/soft split applies
to the **post-rescale** weapon channel; the oracle's comment cites that sentence.
If both implementations are written from an ambiguous spec by the same reader,
they agree with each other and prove nothing.

**Files not named in a Files column will bite you.** Phase 0 hit this with
`SoundLogPanel.Layout.cs`, which appeared nowhere in the plan's ownership table
and which a twelfth sound slot silently overflowed. Four review rounds missed it
because every gate checked *consumers* of `SoundCatalog.AllSounds` — all written
against `.Count` — and none checked panel *geometry*.

**Allocation moved and is expected to move again.** Phase 0 took it from
42,568,888 to 46,785,664 bytes, +9.9%, measured to `BattleEvent` widening from 80
to 88 bytes for the nullable enum plus alignment. Not a hash change. The oracle
figure in `docs/development/testing.md` is stale; **T63 owns re-recording it.**

**Smoke rows are human-only.** No agent may flip a row in
`docs/development/testing.md` away from `PENDING`. Compilation, unit tests, and a
window-opening probe do not count as verification of interactive behaviour.

---

## 9. Known defects and open items, none blocking

- **T10's verification** names a smoke assertion that reads the fixture back, but
  its Files column lists no test file to hold it. No committed test read the
  fixture before Phase 1; it is now covered incidentally by T21, which asserts
  terminal tick 1081 and `Faction1Victory` from the fixture.
- **T03's Files column** omits `ClashProfileTests.cs`, which §4 assigns to T02,
  T03 and T18.
- **Two test files exceed the 800-line hard maximum** in the coding standards:
  `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` at 1388 and
  `ClashResolverTests.cs` at 1013. Both are the files the plan's ownership table
  names for their tasks, and `BattleSimulationTests.cs` is also declared for T24
  and T30, so splitting either now would contradict the plan. Backlog item, worth
  doing after Phase 2 lands.
- **The tall-hardwood shield currently confers no survival advantage at all.**
  `ShieldedRosterEntriesSurviveMoreOftenThanShieldlessOnesAcrossSeedsOneThroughTwenty`
  measured 31 of 2000 against 31 of 2000 — exactly equal. The shield only
  reweights hit location while damage per attack is flat, so it changes *where* a
  warrior is hit and never *whether*. **That gap is precisely what the clash is
  meant to close**, and this test is the measurement of whether it did.

---

## 10. Why the plan looks the way it does

It went through five revisions and four adversarial review rounds, with two
independent gates — a senior-engineering reviewer and a test strategist — plus two
rounds of historical research. No code was written until both gates returned GO.

The gates found, in order: seven of eight test tasks that were compile errors
rather than red tests; a barrier that demanded eight passing tests fail; clash
sounds that would have shipped permanently silent because `SoundDirector` passes a
non-null hit class to classless slots; goldens that descended entirely from one run
of the code under test; a seam opened on a factory that skips spawn placement; and
an assertion made unsatisfiable by the fix for an earlier, correct finding.

**Every sentence in a task row is load-bearing and most exist because a specific
defect was found.** If a row looks wrong or redundant, it is far more likely to be
protecting against something than to be noise. Stop and report rather than working
around it.

The research produced one finding worth carrying forward, recorded in
`docs/research/WEAPON_CLASH_1500s.md` §5.7: pre-modern battles were decided by
morale collapse and rout, not attrition. Hukbo has no morale model and `CLAUDE.md`
§9 correctly defers one, so Hukbo must reach decisions by a mechanism that
historically did not decide battles. Its interception rate therefore has to sit
below what the historical record would suggest. **That is a design compensation,
explicitly not a historical claim, and it must never be cited back as evidence
about how often people parried.**
