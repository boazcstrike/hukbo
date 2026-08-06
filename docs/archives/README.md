# Archives — reference only

Everything in this folder is **deprecated**. These are finished or abandoned
plans and design documents, kept so a decision can be traced back to its
reasoning. They are a dump, not a source of truth.

## Layout

Files live under a `YYYY-MM-DD/` subfolder named for the date the plan was
archived (checked via `git log --follow`, not the date in the filename — a
plan written on one day can be archived days later). Filenames keep whatever
name they had in `docs/plans/`, date prefix and all; do not strip or rename on
move. When archiving something today, create the folder if it does not exist
yet — `mkdir docs/archives/<today>` — rather than dropping the file at the
`docs/archives/` root.

## Rules

- **Never execute an archived plan.** Its task lists, commands, and verification
  steps are historical, not instructions.
- **Never treat an archived document as current.** Versions, tooling references,
  file paths, and acceptance criteria drift out of date here on purpose and are
  not maintained.
- **Never cite an archive as the reason to do something.** Cite the live
  contract instead.
- **Never link into a dated subfolder from outside this directory without the
  full path.** A link that reads `docs/archives/2026-07-26-foo.md` broke the
  day this reorg landed — always include the date segment:
  `docs/archives/2026-07-26/2026-07-26-foo.md`.
- Reading one to answer "why was it built this way" is the intended use.

## Pruned on 2026-08-07

The dated folders `2026-07-26` through `2026-07-31` were removed on 2026-08-07.
That is 109 finished plans, designs, and agent notes covering the foundation
work, the visual improvement package, the weapon and movement workstreams, and
the collision and formation changes. Nothing was lost: every one of those files
is still in git history and any of them can be read with
`git show b144b7d:docs/archives/<date>/<file>` or restored with
`git checkout b144b7d -- <path>`.

Roughly a hundred and sixty comments in `src/`, `tests/`, `tools/`, and the live
documents still cite those paths. Those citations now name a file that is not in
the working tree. They are prose references inside comments, so nothing fails to
build and no test asserts them, but a reader who follows one will find nothing
there and should reach for `git show` rather than assuming the reference was
wrong.

The earlier note about two plans listing GitHub Actions in their tech stack
applied to files removed in that prune. The repository still uses local-only
verification and still has no CI.

## Where the live contract lives

| Question | Source |
| --- | --- |
| How agents work in this repo | `CLAUDE.md` |
| Determinism, tick order, reviewer checklist | `SIMULATION-GAME-STANDARDS.md` |
| Verification and evidence | `docs/development/testing.md` |
| Active plans | `docs/plans/` |
| Task procedures | `.claude/skills/` |

Every file here carries an "Archived: reference only" banner directly under its
title. A file in this folder without that banner has not been reviewed — add it.
