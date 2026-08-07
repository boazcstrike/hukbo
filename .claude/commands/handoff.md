---
description: Write a session handoff document so the next agent can resume this work without re-deriving anything
argument-hint: [optional slug or focus, e.g. "rally-stall-escape" or "movement research"]
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
---

# Handoff

Produce a handoff document for the work in progress so a fresh agent, in a new
session with no memory of this one, can pick it up and continue.

**Focus (optional):** $ARGUMENTS

If no focus is given, infer it from the current branch, the uncommitted diff,
and the most recently touched files under `docs/plans/`.

## Gather evidence first

Do not write the document from memory of the conversation alone. Confirm the
actual state of the checkout:

1. `git status --short` and `git branch --show-current` — what is modified,
   staged, untracked, and which branch the work sits on.
2. `git log --oneline -10` — what has already landed.
3. `git diff --stat` and `git diff --stat --cached` — the shape of the
   uncommitted change.
4. `git worktree list` — whether related work is isolated in a worktree under
   `.claude/worktrees/`.
5. Read the active plan document in `docs/plans/` if one exists, and record which
   tasks are ticked and which are not.

Use `tokensave` MCP tools for any code discovery needed to describe the change
accurately. Do not spawn an Explore agent.

## Write the document

Path: `docs/plans/YYYY-MM-DD-<slug>-handoff.md`, dated today, `<slug>` taken from
the focus or the branch name. Update an existing handoff for the same work rather
than creating a second one.

Write in full, normal English prose. Do not compress it — this file is repository
documentation, not an agent-to-agent prompt.

Sections, in this order:

- **Goal** — what the work is trying to achieve, in two or three sentences,
  including why it was started.
- **State on disk** — branch, worktree, commits landed, files modified but not
  committed. Paste the real `git status --short` output.
- **What is done** — completed tasks, with the file and symbol names that changed.
- **What is not done** — remaining tasks, in order, each small enough for one
  agent to finish, with the files each one touches.
- **Verification status** — the last `./scripts/verify.ps1` result with its real
  output, or the explicit statement that the gate has not been run since the last
  change. Never imply a gate result that was not observed. Manual smoke-checklist
  rows in `docs/development/testing.md` stay `PENDING` unless a human at an
  interactive desktop flipped them.
- **Determinism impact** — whether the change reaches the state hash or the event
  hash, and whether a new preset version and new golden expectations are required.
  State "no simulation state touched" explicitly when that is the case.
- **Open questions and risks** — decisions the next agent must make, assumptions
  currently in force, and anything that looked wrong but was left alone.
- **How to resume** — the exact first commands to run, including the worktree to
  enter if the work is isolated.

## Rules that bind this command

- The document goes under `docs/plans/`, never the repository root and never
  `docs/archives/`. Archived plans are reference only.
- Do not commit, push, merge, or switch branches. Writing the handoff file is the
  only change this command makes.
- Do not implement any remaining task. This command records state; it does not
  advance the work.

## Output

Report back:

- Path to the handoff document
- Branch and worktree the work lives on
- Count of tasks done versus remaining
- Verification status in one line, stated honestly
