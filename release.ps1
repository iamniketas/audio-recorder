param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,
    [switch]$PreRelease
)

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
