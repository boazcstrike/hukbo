# 11 — Content and Asset Pipeline

## Scope

Provide the smallest reproducible content build needed for labels and
diagnostics while avoiding unnecessary binary assets.

## Inputs inspected

- Client `.mgcb` content definition.
- MonoGame Content Builder Task package.
- Menu requirement for a SpriteFont.
- Client workstream report that `dotnet-mgcb` requires a pinned local tool
  manifest.

## Decisions and work

The dot texture remains runtime-generated. The only planned compiled content is
a redistributable SpriteFont for menu and diagnostics. Bootstrap restores the
repository-local tool manifest when present.

## Files

- `src/AutonomousArena.Client/Content/**` (owned by Client/Menu workstream)
- `.config/dotnet-tools.json` (owned by orchestrator)
- `scripts/bootstrap.ps1`
- `docs/dependency-risk-register.md`

## Verification

The delivery worktree did not contain the integrated SpriteFont/tool manifest,
so content compilation was not claimed. The orchestrator separately reported a
pinned `dotnet-mgcb` 3.8.5 tool restore; final Client build remains required.

## Status

**DEFERRED**

## Limitations

Font redistribution/license provenance must be recorded before public
distribution.

## Next action

Integrate the manifest and Client content, run bootstrap, then verify the Client
Release build from a restored state.
