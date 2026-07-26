# 09 — Configuration and Package Management

## Scope

Pin repository-wide SDK, compiler, warning, formatting, package-source,
dependency-version, and lock behavior.

## Inputs inspected

- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `NuGet.config`
- `.editorconfig`, `.gitattributes`, `.gitignore`
- Project lock files.

## Decisions and work

Confirmed `net10.0`, C# 14, nullable, warnings-as-errors, deterministic builds,
central package management, explicit nuget.org-only source, and locked project
graphs.

## Files

- Root configuration files (foundation inputs; not modified by this workstream)
- `docs/dependency-inventory.md`
- `docs/development/coding-standards.md`

## Verification

Locked restore passed. Formatting verification examined the current solution
and changed 0 of 17 files.

## Status

**COMPLETE**

## Limitations

The orchestrator owns any required root tool-manifest or package-lock update
caused by final Client integration.

## Next action

Review all lock-file changes during integration and reject unrequested feeds or
floating versions.
