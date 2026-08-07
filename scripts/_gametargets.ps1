# Shared project-path table for the two games this repository builds. A
# script that needs to know which project to restore, build, test, run, or
# publish calls Get-GameTarget instead of hardcoding a path, so the table has
# exactly one place to change when a third game arrives.
#
# The Hukbo branch below carries the exact literal paths every game-specific
# script hardcoded before -Game existed:
# scripts/run.ps1:21, scripts/benchmark.ps1:42, scripts/test.ps1:15-16, and
# scripts/package.ps1:12. That is deliberate, not incidental: a caller that
# never passes -Game must resolve to precisely the same paths it always has,
# so the default gate stays byte-identical to what it ran before this table
# existed. See docs/plans/2026-08-07-sandata-scaffold-design.md section 14.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GameTarget {
    <#
    .SYNOPSIS
    Returns the project-path table for the named game.

    .DESCRIPTION
    Client and Headless are single project paths; Tests is an ordered array
    of every test project that game's suite runs. LogPrefix names the
    artifacts/logs/<prefix>-<utc>-<pid>.jsonl file stem the game's entry
    points write through Hukbo.Diagnostics.

    .PARAMETER Game
    Which game's table to return. Defaults to 'Hukbo' so a caller that never
    passes -Game gets today's behavior unchanged.
    #>
    param(
        [ValidateSet('Hukbo', 'Sandata')]
        [string] $Game = 'Hukbo'
    )

    switch ($Game) {
        'Hukbo' {
            @{
                Client   = 'src/Hukbo.Client/Hukbo.Client.csproj'
                Headless = 'src/Hukbo.Headless/Hukbo.Headless.csproj'
                Tests    = @(
                    'tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj'
                    'tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj'
                )
                LogPrefix = 'hukbo'
            }
        }
        'Sandata' {
            @{
                Client   = 'src/Sandata.Client/Sandata.Client.csproj'
                Headless = 'src/Sandata.Headless/Sandata.Headless.csproj'
                Tests    = @(
                    'tests/Sandata.Core.Tests/Sandata.Core.Tests.csproj'
                    'tests/Sandata.Client.Tests/Sandata.Client.Tests.csproj'
                )
                LogPrefix = 'sandata'
            }
        }
    }
}
