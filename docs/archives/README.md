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
- **No file outside `docs/archives/` may link or cite a path into it at all.**
  Section 6 of `CLAUDE.md` states the rule without an exception: the folder is
  deleted periodically, so a path into it is a path that breaks, and a
  fully-qualified path with the date segment in it breaks exactly as readily as
  a bare one. Name the archived document in prose if a reader needs to know it
  existed — its **title**, so it can be found by searching for that title — and
  never write the path as a link. This binds documentation, plans, research
  notes, skills, and source comments alike. Inside this directory a path is
  fine, because it dies with the folder it points into.
- Reading one to answer "why was it built this way" is the intended use.

## Pruned on 2026-08-07

The dated folders `2026-07-26` through `2026-07-31` were removed on 2026-08-07.
That is 109 finished plans, designs, and agent notes covering the foundation
work, the visual improvement package, the weapon and movement workstreams, and
the collision and formation changes. Nothing was lost: every one of those files
is still in git history and any of them can be read with
`git show b144b7d:docs/archives/<date>/<file>` or restored with
`git checkout b144b7d -- <path>`.

The 159 citations of those paths in `src/`, `tests/`, `tools/`, and the live
documents were rewritten on the same day. Each one now names its document in
prose — "the formation and movement realism design", "the contingent close-latch
plan", "the wasay movement plan" — instead of pointing at a path that resolves
to nothing. Section numbers, task identifiers, and the surrounding reasoning are
unchanged, so a comment that cited section 3.5 still cites section 3.5.

To read one of those documents, find it by name in the prune commit's parent:

```powershell
git show b144b7d --stat -- docs/archives | Select-String formation
git show b144b7d:docs/archives/2026-07-28/2026-07-28-formation-movement-realism-design.md
```

Documents archived on 2026-08-07 or later may not be cited by path either.
The rule above admits no date cutoff: a file outside this directory may not
point at one inside it, however recently that file was archived and however
plainly it is present today. A later archive prune removes it on the same terms
the 2026-08-07 prune removed its predecessors, and a path written on the
strength of the file being present now is a path that breaks then.

Path citations written before that rule hardened do still survive here and
there in the tree. They are known debt, not a licence: fix one when the
surrounding file is being edited anyway, rewriting it to name the archived
document's title in prose, and never add a new one.

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
| What each active plan is, and its state | `docs/plans/README.md` |
| Task procedures | `.claude/skills/` |

Every file here carries an "Archived: reference only" banner directly under its
title. A file in this folder without that banner has not been reviewed — add it.
