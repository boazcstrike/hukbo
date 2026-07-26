# Dependency Decisions

## Selected

- MonoGame DesktopGL 3.8.5 provides the smallest suitable renderer/input shell.
- MonoGame Content Builder Task 3.8.5 compiles the planned SpriteFont.
- VSTest with xUnit is retained for the initial non-graphical test suite.
- .NET base libraries provide command parsing support, timing, and JSON output;
  no additional CLI or benchmark framework is justified.

All packages are centrally pinned and project lock files are committed.

## Explicitly omitted

Physics, ECS, dependency injection, logging frameworks, telemetry, networking,
serialization libraries, mocking libraries, fluent assertions, and benchmark
frameworks are omitted because the first milestone has no measured need for
them. Core remains package-free.

## Compatibility boundary

The selected packages target the pinned .NET SDK and Windows x64 DesktopGL
client. The package is self-contained for `win-x64`. Trimming, native AOT,
non-Windows runtime identifiers, and store packaging have not been validated
and are not promised.
