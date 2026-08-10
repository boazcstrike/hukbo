---
name: hukbo-orchestrate
description: Runs the three-stage Hukbo agent pipeline — parallel research, a single planner, then scoped implementers — including worktree isolation, the eight-agent ceiling, the prompt contract every sub-agent gets, and the planner-output audit before implementation starts. Use for any non-trivial feature, refactor, or investigation in this repository, and whenever a request would otherwise be answered by spawning agents by hand. Covers what each stage may and may not write, why the canonical gate is never delegated, and the cross-session worktree hazards specific to this checkout.
---

# Orchestrating work in Hukbo

This skill is the executable form of the pipeline in `CLAUDE.md` §10. Invoke it
instead of pasting the diagram into a prompt.

```
Research agents (plan and knowledge)
         ↙        ↘
Requirements     Existing code
        ↘        ↙
Task planner agent (list of granular tasks)
         ↓
Developer agents
```

## Before stage 1

Two things happen before any agent is spawned.

**Decide whether the work needs the pipeline at all.** A typo, a comment, a
one-line constant, or a question answerable from the code graph does not. Run
those inline. The pipeline costs several agents and a review pass; spending that
on a two-line change is waste, not rigor.

**Decide the isolation.** Create a git worktree when the work will touch source
files, because this checkout is regularly shared by more than one live session
and by `dotnet test` runs that fight over `obj/`.

```powershell
git worktree add .claude/worktrees/<slug> -b <slug> main
```

Branch off **local** `main`, and confirm the worktree actually sits on the commit
you expect before handing it to an agent — a stale or mis-based worktree has
caused real rework here. Other worktrees under `.claude/worktrees/` and in the
scratchpad frequently belong to a different session. Never clean up, commit, or
merge a worktree you did not create in this session without saying so first.

## Stage 1 — research, in parallel, read-only

Two groups, spawned in the same message so they actually run concurrently.

| Group | Establishes | Typical questions |
| --- | --- | --- |
| Requirements | What the change must do | Acceptance criteria, the user-visible effect, the spectator-discoverability answer from `SIMULATION-GAME-STANDARDS.md` §10, historical evidence when weapons or culture are involved |
| Existing code | What the repository already does | The types involved, the tick stage the change lands in, the tests already covering the area, the conventions to match |

Rules that bind this stage:

- **Read-only.** A research agent that wants a file changed returns a proposed
  task, not an edit.
- **No Explore agents for code research.** Code discovery goes through the
  `tokensave` MCP tools and the codebase-memory graph. This applies to the
  prompts you write for sub-agents exactly as it applies to your own work.
- Historical claims come back labelled **Documented**, **Documented, form
  uncertain**, or **Provisional reconstruction** — never unlabelled.

## Stage 2 — planning, one agent

A single planner reads both research outputs and writes the design document and
the plan document described in `CLAUDE.md` §6. It writes no code.

The task table needs these columns, because they are what makes a task
delegable:

| Task | What | Files | Done when | Depends on | Verified by |

**Audit the planner output before spawning anyone.** Read the task list and
check it yourself: are the tasks granular enough for one agent each, are the
file sets genuinely disjoint, does every task name its verification, and does
the dependency order actually hold? A planner that contradicts itself between
the design document and the plan is common enough to be worth the two minutes.
Send it back for a second round rather than implementing a plan you do not
believe.

## Stage 3 — implementation

Developer agents execute the task list.

- **Eight parallel agents is the ceiling**, and fewer is usually better. Beyond
  that the results arrive faster than they can be read.
- Give each agent an **explicit, non-overlapping** set of files. Two agents in
  one file is a merge conflict you created deliberately.
- Give each one its **own worktree** when their test runs would otherwise
  collide.
- Go **serial** when the work funnels through a shared seam — a single tick
  stage, a single event type, a single test fixture. Parallelism there buys
  nothing and costs a conflict.

## The prompt contract

Every sub-agent prompt names four things: the **files and symbols** it may
touch, the **evidence** it must ground its answer in, the **verification** that
proves it finished, and the **exact shape of the answer** you want back. An
agent that returns prose you then have to re-read has cost more than it saved.

Agent-to-agent prompts may be compressed caveman style. Repository
documentation, commits, and user-facing prose may not.

## After implementation

1. Integrate, then run `./scripts/verify.ps1` **yourself, once**. The canonical
   gate is never delegated and no sub-agent report substitutes for its output.
   See `hukbo-verify-and-record`.
2. Re-check any claim a reviewer or research agent made that you are about to
   act on. Sub-agent findings in this repository have been wrong often enough
   that verifying the decisive ones is cheaper than acting on them.
3. Record evidence honestly. No agent may flip an interactive smoke-checklist
   row.
4. Move the finished plan to `docs/archives/<YYYY-MM-DD>/`, dated for the day
   of archiving, with the "Archived: reference only" banner. That folder's own
   `README.md` holds the layout rules. Never leave a link pointing into the
   archive behind — the folder is deleted periodically, so name the moved
   document in prose instead.

## Known failure modes

- **`[Request interrupted by user]` on a spawn can be spurious.** The sub-agent
  usually finishes anyway. Check its output or worktree before re-spawning and
  paying for the work twice.
- **A concurrent session moved your files.** Untracked files appearing in
  `docs/` or `.claude/worktrees/` are frequently another session's work. Check
  before assuming a change is yours to commit.
- **A worktree that will not build** is usually based on the wrong commit, not
  broken code. Check its merge base first.
