# 05 — Dependency Compatibility

## Scope

Record selected packages, compatibility boundaries, omitted dependency groups,
and dependency risks before final integration.

## Inputs inspected

- `Directory.Packages.props`
- All project files and lock files.
- Platform/architecture design.
- Current NuGet source configuration.

## Decisions and work

Kept Core package-free; selected only MonoGame framework/content tooling and
xUnit/VSTest packages. Recorded self-contained Windows packaging, action
pinning, advisory drift, content licensing, and graphics runtime risks.

## Files

- `docs/dependency-inventory.md`
- `docs/dependency-decisions.md`
- `docs/dependency-risk-register.md`

## Verification

Locked restore passed for all four projects. A live nuget.org transitive
vulnerability audit reported no vulnerable packages for any project on
2026-07-26.

## Status

**COMPLETE**

## Limitations

Native AOT, trimming, self-contained publishing, and non-Windows runtime
identifiers are outside the accepted scope.

## Next action

Repeat the transitive vulnerability audit after final restore and before each
release because advisory data changes.
