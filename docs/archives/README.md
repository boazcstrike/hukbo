# Archives — reference only

Everything in this folder is **deprecated**. These are finished or abandoned
plans and design documents, kept so a decision can be traced back to its
reasoning. They are a dump, not a source of truth.

## Rules

- **Never execute an archived plan.** Its task lists, commands, and verification
  steps are historical, not instructions.
- **Never treat an archived document as current.** Versions, tooling references,
  file paths, and acceptance criteria drift out of date here on purpose and are
  not maintained.
- **Never cite an archive as the reason to do something.** Cite the live
  contract instead.
- Reading one to answer "why was it built this way" is the intended use.

Known stale content, left in place deliberately: two of these plans list GitHub
Actions in their tech stack. The repository uses local-only verification and has
no CI. Do not act on that line.

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
