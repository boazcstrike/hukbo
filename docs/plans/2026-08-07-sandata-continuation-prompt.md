# Sandata continuation prompt

Paste everything below the line into a fresh agent session started in
`C:\Users\boazs\webdev\autonomous-arena`.

---

You are continuing work on **Sandata**, a second game being built inside the
`autonomous-arena` repository. Waves 1 through 4 of a 12-wave plan are complete,
merged, and verified. Your job is to continue from wave 5.

## Read these first, in this order. Do not re-derive their contents.

Everything is on branch `sandata-scaffold`, in the worktree
`C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\sandata-scaffold`.
Work there, not in the parent checkout.

1. `CLAUDE.md` — the repository contract. Sections 4, 5, 6, 9, and 10 bind you.
2. `docs/plans/2026-08-07-sandata-scaffold.md` — the 56-task plan. **Read the
   trailing "Status log", "Plan defect", "Open item", and amendment sections at
   the very end.** They record every place implementation disproved the plan and
   they are more current than the task table above them.
3. `docs/plans/2026-08-07-sandata-scaffold-design.md` — the design. Authoritative
   over any prompt, including this one. If they disagree, the design wins and you
   report the discrepancy.
4. `docs/research/2026-08-07-sandata-research-consolidated.md` — the evidence, with
   verified `file:line` citations.

## Current state, verified

- Branch `sandata-scaffold`, 63 commits ahead of `main`. **Nothing is pushed.
  `main` is untouched.**
- Waves 1-4 done: tasks 1-25 plus task 56.
- 6,366 tests pass, zero failures. Sandata.Core 608, Sandata.Client 25,
  Hukbo.Core 2,635, Hukbo.Client 3,098.
- `./scripts/verify.ps1` passes all five stages. The seed-1 workload reproduces
  `stateHash 1B73FC5923879AA0` and `eventHash AC55684F24D39344`, byte-identical
  to the same workload on untouched `main`. Adding a second game moved no Hukbo
  hash, and it must stay that way.

Pinned values later work must respect:

| Value | Pinned at |
| --- | --- |
| `SandataRuleset.ContentHash` | 8955292433887190872 |
| `FirearmRuleset.ContentHash` | 12611003062847309889 |
| `angle-house.hkmap` MapContentHash | 11909359227906322716 |

Two map rules are settled and test-pinned: a map's `NAME` does **not** reach the
content hash, and a door's hinge is **absolute** to canonical endpoint order.

## The one thing blocking wave 5 — ask the user before starting it

The user asked for bots that "automatically create the pathway" and are
"automatically grouped together". Door Kickers 2 does the opposite: players
hand-draw every trooper's path and there is no group move; only enemy AI
pathfinds. The plan is built for **(A)** but this was never confirmed.

- **(A)** Autonomous bots that path and group themselves — Hukbo's spine with
  Door Kickers geometry. What the plan assumes.
- **(B)** Literal Door Kickers 2 — the player draws every path.
- **(C)** Both — autonomous by default, manual takeover available.

Waves 1-4 hold under any answer. Wave 5 does not, and being wrong costs roughly
thirty tasks. **Ask, and wait, before starting wave 5.**

Three lower-stakes questions are also open and block nothing: the working name
`Sandata`, whether shipped display strings use real weapon names or the generic
aliases (a `WeaponNameSetId` field already switches between them), and the audio
spend below.

## Never do this without explicit user authorisation

`scripts/sfx.ps1` calls ElevenLabs and costs real money. The catalog declares 106
slots and **524 variants**, about 104,800 credits, roughly 22 USD best case and 99
USD with a realistic reject rate. Task 40 builds a manifest generator that
contains **no network code at all** — its acceptance test greps the script for any
HTTP verb and requires zero hits. Produce the manifest, show the user every
filename and prompt and the cost, and **stop**. Do not generate a single file
before they say yes.

## How to work

Follow `CLAUDE.md` section 10 and the `hukbo-orchestrate` skill. What worked
across four waves:

- One git worktree per implementer, branched from `sandata-scaffold`. Merge each
  branch back as it lands and re-run the Sandata suites after every merge.
- At most eight parallel agents. Coding tasks run on Sonnet, every time.
- Give each agent an explicit, non-overlapping file list, and tell it to **stop and
  report** rather than edit a file outside that list. Every real defect this
  session was found because an agent did exactly that.
- Audit the wave both ways before dispatching: no file claimed by two tasks, and
  every file named in a task's "What" column claimed by exactly one. The second
  check matters — three unowned files were found the hard way.
- Run `./scripts/verify.ps1` yourself after integrating. It is never delegated and
  no agent report substitutes for its output.
- `scripts/test.ps1` still runs only the two Hukbo test projects. Task 41 adds
  `-Game`. Until then run `dotnet test` against the Sandata projects directly and
  report those numbers separately.

## Traps this session hit, so you do not hit them again

- **A pure rename is not a safe rename.** `FacingRules` had a same-namespace type
  dependency carrying no `using`, and `Fnv1a.cs` carried an assembly-level
  `InternalsVisibleTo` that travelled with the file and broke the Headless build.
  Before extracting anything in tier 2 (collision, theming, audio), compile the
  candidate in isolation. An import scan will not find these.
- **Roslyn inlines `const` fields** and emits no assembly reference, so a "does not
  reference X" test passes falsely when the only use is a constant. That is why
  the reference test and the source-text scan both exist.
- **Write acceptance criteria as properties, not as symbols that must be absent.**
  "No `Trig` call" was meant as "no cosine comparison" and forced a duplicated
  257-entry sine table that task 56 had to unwind.
- **The research consolidation has no test behind it.** It dropped the AK-15's
  two-round burst and every downstream step implemented the error faithfully. Treat
  it with the same suspicion as generated code.
- Design section 11's analysis of the pawn draw path cites line numbers in
  `PawnGeometry.cs` and `PawnRenderer.cs` that are **stale** — `main` gained gait
  animation mid-session and rewrote both. Task 37 must re-derive them. The
  structural claims still hold.
- `LogPaths.ApplyRetention` sweeps only `hukbo-*.jsonl`, so Sandata log files
  accumulate without bound, and no task owns `LogPaths.cs`. Assign it.

## Start here

1. Read the four documents above.
2. Confirm the state: `git -C .claude/worktrees/sandata-scaffold log --oneline -1`
   and `./scripts/verify.ps1`.
3. Ask the user the (A)/(B)/(C) question.
4. While waiting, wave 5's tasks 26 through 33 are listed in the plan. Tasks 26
   (funnel string-pull) and 29 (recursive shadowcasting) are ports of proven
   algorithms and are answer-independent — they can start immediately. Tasks 27,
   28, 30, 31, 32 depend on the answer.

Report honestly. If a gate fails, paste the failure. If a smoke row cannot be
verified by a human at a desktop, leave it `PENDING` — no agent may flip one.
