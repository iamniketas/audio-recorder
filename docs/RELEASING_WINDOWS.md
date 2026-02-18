# Релиз установщика Windows (Velopack + GitHub Releases)

Скрипт: `tools/release/publish-github-release.ps1`

Что делает:
1. `dotnet publish` для `AudioRecorder.App` (`win-x64` по умолчанию).
2. `vpk download github` (подтягивает прошлые релизы для корректных delta-пакетов).
3. `vpk pack` (создает `*-Setup.exe`, `RELEASES`, `*.nupkg`).
4. `vpk upload github` (публикует в GitHub Release).

## Требования
- .NET SDK 8+
- `vpk` (или `dnx`)
- Токен для публикации: `VPK_TOKEN` или `GITHUB_TOKEN` (с `contents:write`)

## Быстрый запуск
Самый простой вариант:
```powershell
powershell -ExecutionPolicy Bypass -File .\release.ps1
```
Скрипт сам возьмет последний тег `vX.Y.Z` и выпустит следующую patch-версию (`X.Y.(Z+1)`).

Полная команда:
```powershell
$env:GITHUB_TOKEN = "<token_with_repo_write>"
powershell -ExecutionPolicy Bypass -File .\tools\release\publish-github-release.ps1 -Version 0.2.3 -ReleaseNotesPath CHANGELOG.md
```

Примечание: если `gh auth status` показывает активный вход, токен можно не передавать вручную. Скрипт возьмет его через `gh auth token`.

## Dry-run (без реальной сборки/публикации)
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release\publish-github-release.ps1 -Version 0.2.3 -NoUpload -DryRun
```

## Полезные параметры
- `-Runtime win-x64`
- `-NoDownloadExisting` (если не нужны delta-пакеты)
- `-NoUpload` (только локальная упаковка)
- `-PreRelease` (пометить GitHub Release как pre-release)
- `-Tag v0.2.3` и `-ReleaseName "AudioRecorder 0.2.3"`
- `-OutputRoot artifacts` (по умолчанию)

## Где искать результат
- Publish: `artifacts/publish/<version>/<runtime>/`
- Velopack packages: `artifacts/releases/<version>/`
