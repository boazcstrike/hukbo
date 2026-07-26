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

Doctor, locked restore, 7/7 available Core primitive tests, script parsing, and
formatting passed. Complete build, 200-agent headless run, Windows package,
hosted CI, and interactive client smoke were not completed in this pre-
integration snapshot.

## Status

**DEFERRED**

## Limitations

The source workstreams must be integrated before a readiness decision can be
upgraded.

## Next action

Run `./scripts/verify.ps1`, package `win-x64`, execute the interactive smoke,
and update this report from those exact outputs.
