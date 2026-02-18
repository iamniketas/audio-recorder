param(
    [Parameter(Position = 0)]
    [string]$Version = "",
    [switch]$PreRelease
)

$ErrorActionPreference = "Stop"

function Get-AutoVersion {
    $tags = @(git tag --list "v*" 2>$null)
    $stable = $tags |
        Where-Object { $_ -match '^v(\d+)\.(\d+)\.(\d+)$' } |
        ForEach-Object {
            [PSCustomObject]@{
                Tag = $_
                Major = [int]$Matches[1]
                Minor = [int]$Matches[2]
                Patch = [int]$Matches[3]
            }
        } |
        Sort-Object Major, Minor, Patch

    if ($stable.Count -eq 0) {
        return "0.1.0"
    }

    $last = $stable[-1]
    return "$($last.Major).$($last.Minor).$([int]($last.Patch + 1))"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-AutoVersion
    Write-Host "Auto version: $Version"
}

$script = Join-Path $PSScriptRoot "tools\release\publish-github-release.ps1"
if (-not (Test-Path $script)) {
    throw "Не найден скрипт релиза: $script"
}

$arguments = @(
    "-ExecutionPolicy", "Bypass",
    "-File", $script,
    "-Version", $Version,
    "-ReleaseNotesPath", "CHANGELOG.md"
)

if ($PreRelease) {
    $arguments += "-PreRelease"
}

& powershell @arguments
exit $LASTEXITCODE
