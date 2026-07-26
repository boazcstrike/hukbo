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

The dot texture remains runtime-generated. The only compiled content is the
SpriteFont used for menu and diagnostics. Bootstrap restores the
repository-local dotnet-mgcb 3.8.5 tool.

## Files

- `src/Hukbo.Client/Content/**` (owned by Client/Menu workstream)
- `.config/dotnet-tools.json` (owned by orchestrator)
- `scripts/bootstrap.ps1`
- `docs/dependency-risk-register.md`

## Verification

Tool restore passed. The Content Builder compiled `Default.spritefont` from the
Windows Arial font during the zero-warning Release build and self-contained
package publish.

## Status

**COMPLETE**

## Limitations

Font redistribution/license provenance must be recorded before public
distribution.

## Next action

Resolve the font redistribution/provenance requirement before public
distribution or replace it with a project-owned font.
