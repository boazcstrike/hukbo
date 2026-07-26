Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

function Assert-CommandAvailable {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [string] $InstallHint
    )

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. $InstallHint"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Assert-CommandAvailable -Name 'dotnet' -InstallHint 'Install the .NET 10 SDK: winget install --id Microsoft.DotNet.SDK.10 --exact'
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Restore-RepositoryTools {
    $root = Get-RepositoryRoot
    if (Test-Path -LiteralPath (Join-Path $root '.config/dotnet-tools.json') -PathType Leaf) {
        Push-Location $root
        try {
            Invoke-DotNet -Arguments @('tool', 'restore')
        }
        finally {
            Pop-Location
        }
    }
}

function Invoke-RepositoryScript {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [string[]] $Arguments = @()
    )

    $scriptPath = Join-Path (Join-Path (Get-RepositoryRoot) 'scripts') $Name
    & $scriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}
