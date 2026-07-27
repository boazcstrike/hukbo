# Weapon clash — session handoff

Written 2026-07-27 to move this work into a fresh session. Read this file first,
then the four files in section 2, then start at section 7.

---

## 1. Where the work lives

| | |
| --- | --- |
| Worktree | `C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\weapon-clash` |
| Branch | `worktree-weapon-clash` |
| Base | merged with `main` at `b70d812` |
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

### Phase 1 — was running when this file was written

Eleven test tasks, T13–T23, one agent. **Check `git log` first — its commits may
already be present.** Its barrier is B1, which asserts the RED/GUARD
classification per case rather than blanket failure.

### Phases 2 to 4 — not started

- Phase 2 (T24–T34): the resolver, ruleset fold, preset values, attack-stage
  integration, metrics, hash re-baseline, acceptance re-tune.
- Phase 3 (T35–T59): client fan-out. **3b and 3c are dropped** with the sound
  deferral; 3a, the swing animation, still stands.
- Phase 4 (T60–T68): gate, workloads, oracle re-record, smoke rows, archive.

---

## 5. Recorded values — use these, ignore older ones

Merging `main` brought mirrored starting formations, which changed spawn
placement and lengthened the seed-1 battle. **Any number you find from before the
merge is stale.**

```
terminal tick   1081        (was 657 before the merge)
state hash      DC7F2E7A107C885A
event hash      6C641E90DDF0B943
content hash    0x59FB4CA563D87A49UL     unchanged by the merge
fixture rows    1081
```

The content hash is still pinned twice in the suite, at
`tests/Hukbo.Core.Tests/CombatConfigurationTests.cs:145` and
`tests/Hukbo.Core.Tests/DeterminismTests.cs:54`. **T32 re-baselines both, and only
after T19 goes green.** The literal `0x59FB4CA563D87A49UL` that T21 passes as a
*content-hash argument* is not one of those goldens and must not be swept up in
that edit.

### If the hashes move again

They will, the moment Phase 2 lands — that is intended and T32 re-records them.
They will also move if `main` is merged again with any change to deployment,
movement, or targeting. In that case the digest fixture must be recaptured:

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

1. `git log --oneline -15` to see whether Phase 1 landed.
2. If Phase 1 is incomplete, finish T13–T23 and reach Barrier B1.
3. Then Phase 2, T24–T34, in order. T24's nine existing-test dispositions come
   **before** any attack-stage edit.
4. Then Phase 3a only, the swing animation.
5. Then Phase 4.

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

## 9. Two known planning defects, unfixed and harmless

- **T10's verification** names a smoke assertion that reads the fixture back, but
  its Files column lists no test file to hold it. It was verified out of band
  instead. By the plan's own rule this is a planning defect.
- **T03's Files column** omits `ClashProfileTests.cs`, which §4 assigns to T02,
  T03 and T18.

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
