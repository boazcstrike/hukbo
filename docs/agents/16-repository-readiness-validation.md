# 16 — Repository Readiness Validation

## Scope

Execute the final automated gate, package the Windows client, classify failures,
and record only observed readiness evidence.

## Inputs inspected

- All canonical scripts.
- Delivery-worktree source snapshot.
- Readiness acceptance criteria and interactive checklist.

## Decisions and work

Replaced the original destructive `git clean -xfd` instruction with
non-destructive locked restore and explicit diff inspection. Defined
`verify.ps1` as the canonical non-graphical gate and kept UI smoke separate.

## Files

- `scripts/verify.ps1`
- `scripts/package.ps1`
- `docs/repository-readiness-report.md`

## Verification

Doctor, tool restore, locked package restore, formatting, zero-warning Release
build, 42/42 tests, deterministic 200-agent headless execution, and
self-contained Windows packaging passed. The client window opened, advanced,
and closed normally. Hosted CI and direct menu interaction remain unrun.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

Synthetic keyboard injection did not reach the SDL input layer, so it cannot
substitute for the short manual menu checklist.

## Next action

Execute the Play/Pause/Exit manual smoke and run the workflow on GitHub.
