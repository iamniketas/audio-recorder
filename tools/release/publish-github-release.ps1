param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$RepoUrl = "https://github.com/iamniketas/audio-recorder",
    [string]$PackId = "AudioRecorder",
    [string]$MainExe = "AudioRecorder.exe",
    [string]$PackTitle = "AudioRecorder",
    [string]$PackAuthors = "iamniketas",
    [string]$ReleaseNotesPath = "",
    [string]$Tag = "",
    [string]$ReleaseName = "",
    [string]$Channel = "win",
    [string]$OutputRoot = "artifacts",
    [switch]$NoDownloadExisting,
    [switch]$NoUpload,
    [switch]$PreRelease,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-VpkInvoker {
    param([string]$VelopackVersion)

    $globalVpk = Get-Command vpk -ErrorAction SilentlyContinue
    if ($globalVpk) {
        return @{ Prefix = @("vpk") }
    }

    $dnx = Get-Command dnx -ErrorAction SilentlyContinue
    if ($dnx) {
        return @{ Prefix = @("dnx", "vpk", "--version", $VelopackVersion) }
    }

    throw "Не найден 'vpk' и 'dnx'. Установите vpk: dotnet tool install -g vpk --version $VelopackVersion"
}

function Resolve-GitHubToken {
    if (-not [string]::IsNullOrWhiteSpace($env:VPK_TOKEN)) {
        return $env:VPK_TOKEN
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return $env:GITHUB_TOKEN
    }

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($gh) {
        try {
            $token = (& gh auth token 2>$null).Trim()
            if (-not [string]::IsNullOrWhiteSpace($token)) {
                return $token
            }
        }
        catch {
            # ignore
        }
    }

    return ""
}

function Invoke-External {
    param(
        [string[]]$Command,
        [switch]$Dry
    )

    $display = ($Command | ForEach-Object {
        if ($_ -match "\s") { '"' + $_ + '"' } else { $_ }
    }) -join " "

    Write-Host "`n> $display" -ForegroundColor Cyan

    if ($Dry) {
        return
    }

    & $Command[0] $Command[1..($Command.Length - 1)]
    if ($LASTEXITCODE -ne 0) {
        throw "Команда завершилась с кодом ${LASTEXITCODE}: $display"
    }
}

function Get-VelopackVersion {
    param([string]$CsprojPath)

    [xml]$proj = Get-Content $CsprojPath
    $node = $proj.SelectSingleNode("//PackageReference[@Include='Velopack']")

    if (-not $node) {
        throw "В $CsprojPath не найден PackageReference Include=`"Velopack`""
    }

    $versionAttr = $node.Attributes["Version"]
    $version = if ($versionAttr) { $versionAttr.Value } else { "" }
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "У PackageReference Velopack отсутствует атрибут Version в $CsprojPath"
    }

    return $version
}

if ($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
    throw "Version должен быть в формате semver, например 0.2.3"
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$appCsproj = Join-Path $projectRoot "src\AudioRecorder.App\AudioRecorder.csproj"
if (-not (Test-Path $appCsproj)) {
    throw "Не найден проект приложения: $appCsproj"
}

$resolvedOutputRoot = Join-Path $projectRoot $OutputRoot
$publishDir = Join-Path $resolvedOutputRoot "publish\$Version\$Runtime"
$releaseDir = Join-Path $resolvedOutputRoot "releases\$Version"

$releaseTag = if ([string]::IsNullOrWhiteSpace($Tag)) { "v$Version" } else { $Tag }
$releaseTitle = if ([string]::IsNullOrWhiteSpace($ReleaseName)) { "AudioRecorder $Version" } else { $ReleaseName }

$velopackVersion = Get-VelopackVersion -CsprojPath $appCsproj
$vpk = Resolve-VpkInvoker -VelopackVersion $velopackVersion

Write-Host "Project root: $projectRoot"
Write-Host "Publish dir:  $publishDir"
Write-Host "Release dir:  $releaseDir"
Write-Host "vpk source:   $($vpk.Prefix -join ' ')"

if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
}
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
}

Invoke-External -Command @(
    "dotnet", "publish", $appCsproj,
    "-c", $Configuration,
    "-r", $Runtime,
    "-p:WindowsPackageType=None",
    "-o", $publishDir
) -Dry:$DryRun

if (-not $NoDownloadExisting) {
    $downloadArgs = @(
        "download", "github",
        "--repoUrl", $RepoUrl,
        "--channel", $Channel,
        "--outputDir", $releaseDir
    )

    $token = Resolve-GitHubToken
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $downloadArgs += @("--token", $token)
    }

    Invoke-External -Command ($vpk.Prefix + $downloadArgs) -Dry:$DryRun
}

$packArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $Version,
    "--packDir", $publishDir,
    "--mainExe", $MainExe,
    "--channel", $Channel,
    "--outputDir", $releaseDir,
    "--packTitle", $PackTitle,
    "--packAuthors", $PackAuthors
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
    $resolvedNotes = Join-Path $projectRoot $ReleaseNotesPath
    if (-not (Test-Path $resolvedNotes)) {
        throw "Файл release notes не найден: $resolvedNotes"
    }

    $packArgs += @("--releaseNotes", $resolvedNotes)
}

Invoke-External -Command ($vpk.Prefix + $packArgs) -Dry:$DryRun

if (-not $NoUpload) {
    $token = Resolve-GitHubToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Для upload нужен токен. Установите VPK_TOKEN или GITHUB_TOKEN."
    }

    $uploadArgs = @(
        "upload", "github",
        "--repoUrl", $RepoUrl,
        "--channel", $Channel,
        "--outputDir", $releaseDir,
        "--token", $token,
        "--publish",
        "--tag", $releaseTag,
        "--releaseName", $releaseTitle,
        "--merge"
    )

    if ($PreRelease) {
        $uploadArgs += "--pre"
    }

    Invoke-External -Command ($vpk.Prefix + $uploadArgs) -Dry:$DryRun
}

Write-Host "`nГотово. Артефакты: $releaseDir" -ForegroundColor Green
