# Changelog

All notable changes to the Contora project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and the project adheres to [Semantic Versioning](https://semver.org/).

## [0.3.0] - 2026-02-27

### Added
- Video import support in the transcription flow (`.mp4`, `.m4v`, `.mov`, `.avi`, `.mkv`, `.webm`, `.wmv`).
- Automatic extraction of audio tracks to MP3 via `ffmpeg` when importing video files.

### Changed
- The transcription pipeline now accepts imported video by normalizing it to MP3 before Whisper/diarization.
- Added `ffmpeg` resolution strategy: `CONTORA_FFMPEG_EXE` environment variable, bundled runtime path, app directory, then `PATH`.

## [0.2.7] - 2026-02-27

### Fixed
- Fixed intermittent mojibake in Russian UI labels by normalizing runtime UI strings that overwrite XAML defaults.
- Added repository `.editorconfig` UTF-8 policy to prevent accidental ANSI saves for source/UI files.

### Release
- Published a Windows installer as a GitHub Release asset (`Contora-win-Setup.exe`).
- Kept Whisper runtime/model out of the installer package; runtime and model are downloaded in-app on demand.

## [0.2.6] - 2026-02-19

### Fixed
- Stabilized WinUI title bar icon application to prevent fallback to the default system icon.
- Finalized tray/title bar icon consistency in the release runtime environment.

### Changed
- Bumped the stable release version for GitHub distribution and rollback point.

## [0.2.5] - 2026-02-19

### Added
- System tray integration with actions: show/hide window, start/stop recording, pause/resume, exit.
- Minimize/close-to-tray behavior for desktop workflow.

### Changed
- Project/app branding switched to **Contora** across app metadata, docs, and release tooling.
- App title now shows `Contora 0.2.5`.
- Release packaging updated around `Contora.exe` naming and publish/release paths.

### Fixed
- Consistent app icon rendering in executable, taskbar, tray, and title bar.
- Release build/publish flow for the current branded binary.

## [0.2.2] - 2026-01-28

### Fixed
- **Critical crash on long files:** Resolved the `-1073740791` (0xC0000409) failure during transcription of 60+ minute audio. Whisper could complete transcription but crash during cleanup (pyannote/CUDA memory teardown). The app now ignores this specific cleanup crash when the output file has been produced successfully.
- **Memory pressure on large files:** Fixed memory growth caused by buffering the full Whisper output in `StringBuilder`. The app now keeps only the last 200 lines for error messages.
- **Race condition at process shutdown:** Added explicit waiting for stdout/stderr stream handlers after `WaitForExitAsync` to prevent premature completion.

### Added
- **Detailed transcription progress:** Real-time display of:
- Completion percentage
- Transcription speed (audio seconds/s)
- Elapsed transcription time
- Estimated remaining time
- Processed/total audio timeline
- **Result statistics:** After transcription completes, the UI shows:
- Character count
- Word count
- Output file size (KB)
- **Full Whisper logging:** Complete Whisper process output (stdout + stderr) is saved to `{audio_name}_whisper.log` next to the result for troubleshooting.
- **Launcher scripts:** Added `run-debug.bat`, `run-release.bat`, and `build-and-run.ps1` for quick app startup from the repository root.

### Changed
- **Whisper progress parsing:** Updated regex parsing for `faster-whisper-xxl` output with `-pp`, for example:
```text
1% |   35/4423 | 00:01<<02:22 | 30.73 audio seconds/s
```
- **Progress UI:** Simplified the interface to avoid duplicated information. The primary line shows percentage and timeline; details (elapsed, speed, remaining) are shown in dedicated fields.
- **Whisper parameters:** Removed experimental `--chunk_length` and `--compute_type` flags that caused finalization crashes. Added `-pp` (print_progress) and `--standard` for stable output formatting.

## [0.2.1] - 2026-01-23

### Added
- Transcription editing: direct editing of segment text in the UI.
- Speaker renaming via context menu (right click).
- Audio playback on timestamp click.
- Unsaved changes indicator (green checkmark / yellow dot).

### Fixed
- Transcription parsing for correct timestamp handling.
- Long path truncation in UI with tooltip support.

### Changed
- Improved interface aesthetics: compact layout without duplicate information.

## [0.2.0] - 2026-01-19

### Added
- Base UI for displaying segmented transcription.
- Diarization support via `pyannote_v3.1`.
- Speaker name editing.

## [0.1.0] - 2026-01-15

### Added
- System audio recording via WASAPI loopback.
- Microphone recording.
- Audio file import (WAV, MP3, FLAC, OGG, M4A, OPUS).
- Local transcription via `faster-whisper-xxl`.
- WAV to MP3 conversion for disk space savings.
- Base WinUI 3 interface.
- Notifications on transcription completion.
