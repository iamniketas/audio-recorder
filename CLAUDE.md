# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Agentic Behavior Rules

## Mindset
- You are not an assistant; you are an **Autonomous Software Engineer**.
- DO NOT suggest code changes in text. **ALWAYS apply them directly** using tools.
- Do not ask for permission to edit files or run tests. **Just do it.**
- If you see a bug or a task, your goal is to reach the "Done" state without user intervention.

## Tool Usage
- If the task requires changing code, the only acceptable output is the successful execution of `edit_file`.
- Use `shell_execute` to run compilers, linters, and tests immediately after editing.
- After fixing a bug, verify it by running the code before reporting back to me.

## Communication Style
- Be concise. Don't say "I will now fix the bug..." вЂ” just fix it and show the result.
- Stop using phrases like "You can try..." or "I suggest...". Use "I have fixed...", "I have deployed...".
- ALWAYS respond to the user in Russian (РЅР° СЂСѓСЃСЃРєРѕРј СЏР·С‹РєРµ).
- Keep technical terms and code snippets in their original form (English).
- Internal thoughts/reasoning can be in Russian too.

## Project Overview

Contora вЂ” РЅР°С‚РёРІРЅРѕРµ Windows-РїСЂРёР»РѕР¶РµРЅРёРµ РґР»СЏ Р·Р°РїРёСЃРё Рё СЂР°СЃС€РёС„СЂРѕРІРєРё Р»СЋР±РѕРіРѕ Р°СѓРґРёРѕ: Р·РІРѕРЅРєРё, РёРіСЂС‹, РїРѕРґРєР°СЃС‚С‹, РіРѕР»РѕСЃРѕРІС‹Рµ Р·Р°РјРµС‚РєРё. Р›РѕРєР°Р»СЊРЅР°СЏ STT СЃ РґРёР°СЂРёР·Р°С†РёРµР№ Рё РёРЅС‚РµР»Р»РµРєС‚СѓР°Р»СЊРЅРѕРµ СЂРµР·СЋРјРёСЂРѕРІР°РЅРёРµ.

**Current Status:** MVP РІ СЂР°Р·СЂР°Р±РѕС‚РєРµ - Р±Р°Р·РѕРІС‹Р№ UI Рё Р·Р°С…РІР°С‚ Р°СѓРґРёРѕ С‡РµСЂРµР· WASAPI СЂРµР°Р»РёР·РѕРІР°РЅС‹.

## Target Stack (Windows Native)

- **UI Framework:** WinUI 3 + .NET 8 (C#) вЂ” РЅР°С‚РёРІРЅС‹Р№ Fluent Design РґР»СЏ Windows 11
- **Audio Capture:** NAudio + WASAPI (loopback РґР»СЏ СЃРёСЃС‚РµРјРЅРѕРіРѕ Р·РІСѓРєР°, capture РґР»СЏ РјРёРєСЂРѕС„РѕРЅР°)
- **STT Engine:** Whisper.cpp (C++ СЃ CUDA) РёР»Рё Faster-Whisper С‡РµСЂРµР· Python interop
- **ML Runtime:** ONNX Runtime СЃ CUDA РґР»СЏ GPU-СѓСЃРєРѕСЂРµРЅРёСЏ
- **Embeddings:** BGE-M3 РґР»СЏ РјСѓР»СЊС‚РёСЏР·С‹С‡РЅРѕРіРѕ СЃРµРјР°РЅС‚РёС‡РµСЃРєРѕРіРѕ РїРѕРёСЃРєР°
- **Storage:** SQLite + sqlite-vec, Markdown export
- **Languages:** Russian/English

**macOS РІРµСЂСЃРёСЏ** вЂ” РѕС‚РґРµР»СЊРЅР°СЏ СЂР°Р·СЂР°Р±РѕС‚РєР° РїРѕР·Р¶Рµ (SwiftUI + Core Audio), РїРѕСЃР»Рµ РІР°Р»РёРґР°С†РёРё Windows MVP.

## Audio Sources

РџСЂРёР»РѕР¶РµРЅРёРµ РѕР±СЂР°Р±Р°С‚С‹РІР°РµС‚ Р°СѓРґРёРѕ РёР· С‚СЂС‘С… РёСЃС‚РѕС‡РЅРёРєРѕРІ:

1. **System Output (Loopback)** вЂ” РІСЃС‘, С‡С‚Рѕ РїРѕР»СЊР·РѕРІР°С‚РµР»СЊ СЃР»С‹С€РёС‚: Р·РІРѕРЅРєРё, РёРіСЂС‹, РІРёРґРµРѕ, РјСѓР·С‹РєР°
2. **Microphone Input** вЂ” РіРѕР»РѕСЃ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ (РґР»СЏ РїРѕР»РЅРѕР№ Р·Р°РїРёСЃРё РґРёР°Р»РѕРіРѕРІ)
3. **File Import** вЂ” Р·Р°РіСЂСѓР·РєР° РіРѕС‚РѕРІС‹С… Р°СѓРґРёРѕС„Р°Р№Р»РѕРІ (РіРѕР»РѕСЃРѕРІС‹Рµ Р·Р°РјРµС‚РєРё, РїРѕРґРєР°СЃС‚С‹, Р·Р°РїРёСЃРё)

РџРѕРґРґРµСЂР¶РёРІР°РµРјС‹Рµ С„РѕСЂРјР°С‚С‹: WAV, MP3, FLAC, OGG, M4A, OPUS.

## Architecture

```
в”Њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ђ
в”‚                     WinUI 3 UI Layer                        в”‚
в”њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”¤
в”‚  Audio Service    в”‚  Transcription    в”‚  LLM Service        в”‚
в”‚  (NAudio/WASAPI)  в”‚  (Whisper.cpp)    в”‚  (Local/Optional)   в”‚
в”њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”¤
в”‚              Core Services (.NET 8)                         в”‚
в”‚  - Project Management                                       в”‚
в”‚  - Storage (SQLite + Vectors)                              в”‚
в”‚  - Search & Indexing (RAG)                                 в”‚
в”‚  - Export (Markdown)                                       в”‚
в””в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”
```

**Pipeline:**
1. **Capture/Import** в†’ WASAPI loopback + mic, РёР»Рё РёРјРїРѕСЂС‚ С„Р°Р№Р»Р°
2. **Preprocessing** в†’ VAD (РѕС‚СЃРµС‡РµРЅРёРµ С‚РёС€РёРЅС‹), РЅРѕСЂРјР°Р»РёР·Р°С†РёСЏ РіСЂРѕРјРєРѕСЃС‚Рё
3. **STT + Diarization** в†’ Whisper СЃ СЂР°Р·РґРµР»РµРЅРёРµРј РїРѕ СЃРїРёРєРµСЂР°Рј (S1, S2...)
4. **Post-processing** в†’ LLM: РѕС‡РёСЃС‚РєР° С‚РµРєСЃС‚Р°, СЃС‚СЂСѓРєС‚СѓСЂРёСЂРѕРІР°РЅРёРµ, РёР·РІР»РµС‡РµРЅРёРµ СЂРµС€РµРЅРёР№/СЂРёСЃРєРѕРІ/Р·Р°РґР°С‡
5. **Indexing** в†’ Р­РјР±РµРґРґРёРЅРіРё РґР»СЏ СЃРµРјР°РЅС‚РёС‡РµСЃРєРѕРіРѕ РїРѕРёСЃРєР°
6. **Export** в†’ Markdown, РїРѕР·Р¶Рµ РёРЅС‚РµРіСЂР°С†РёРё (Slack, Telegram, Jira)

Audio is temporary (deleted after transcription). Only text and metadata are stored locally.

## Quality Profiles

- **Quality (Large)** вЂ” maximum accuracy
- **Balance (Medium)** вЂ” compromise
- **Speed (Small/Distil)** вЂ” fast draft

Performance target: 1 hour audio in <30 min (achieved ~6-8 min on RTX 3070 Ti).

## Roadmap

- **v0.1 (MVP Windows):** Р—Р°РїРёСЃСЊ СЃРёСЃС‚РµРјРЅРѕРіРѕ Р·РІСѓРєР° + РјРёРєСЂРѕС„РѕРЅР°, РёРјРїРѕСЂС‚ С„Р°Р№Р»РѕРІ, Р»РѕРєР°Р»СЊРЅР°СЏ STT СЃ РґРёР°СЂРёР·Р°С†РёРµР№, Markdown export
- **v0.2:** РЎРµРјР°РЅС‚РёС‡РµСЃРєРёР№ РїРѕРёСЃРє РїРѕ РІСЃРµРј Р·Р°РїРёСЃСЏРј, LLM post-processing (РєРѕРЅСЃРїРµРєС‚, Р·Р°РґР°С‡Рё)
- **v0.3:** РђРІС‚РѕРїСЂРѕС‚РѕРєРѕР» в†’ Р·Р°РґР°С‡Рё, РґР°Р№РґР¶РµСЃС‚С‹, СЌРєСЃРїРѕСЂС‚ РІ Telegram/Slack/Jira
- **v1.0:** РЎС‚Р°Р±РёР»СЊРЅС‹Рµ СЃР±РѕСЂРєРё, РїСЂРѕС„РёР»Рё РєР°С‡РµСЃС‚РІР°, РѕРїС†РёРѕРЅР°Р»СЊРЅС‹Рµ РїР»Р°С‚РЅС‹Рµ С„СѓРЅРєС†РёРё
- **v1.x:** macOS РІРµСЂСЃРёСЏ (SwiftUI + Core Audio) вЂ” РѕС‚РґРµР»СЊРЅР°СЏ РєРѕРґРѕРІР°СЏ Р±Р°Р·Р°

## Project Structure

```
contora/
в”њв”Ђв”Ђ Contora.sln          # Solution С„Р°Р№Р»
в”њв”Ђв”Ђ src/
в”‚   в”њв”Ђв”Ђ AudioRecorder.App/     # WinUI 3 РїСЂРёР»РѕР¶РµРЅРёРµ (UI СЃР»РѕР№)
в”‚   в”њв”Ђв”Ђ AudioRecorder.Core/    # Core РјРѕРґРµР»Рё Рё РёРЅС‚РµСЂС„РµР№СЃС‹
в”‚   в””в”Ђв”Ђ AudioRecorder.Services/ # РЎРµСЂРІРёСЃС‹ (Audio, STT, Storage)
в””в”Ђв”Ђ plan.txt                   # РџРѕР»РЅР°СЏ СЃРїРµС†РёС„РёРєР°С†РёСЏ РЅР° СЂСѓСЃСЃРєРѕРј
```

## Development Commands

### Build & Run
```bash
# РЎРѕР±СЂР°С‚СЊ РІРµСЃСЊ solution
dotnet build Contora.sln

# Р—Р°РїСѓСЃС‚РёС‚СЊ РїСЂРёР»РѕР¶РµРЅРёРµ (С‚РѕР»СЊРєРѕ С‡РµСЂРµР· Visual Studio РёР»Рё РЅР°РїСЂСЏРјСѓСЋ РёР· bin/)
# WinUI 3 РїСЂРёР»РѕР¶РµРЅРёСЏ РЅРµР»СЊР·СЏ Р·Р°РїСѓСЃС‚РёС‚СЊ С‡РµСЂРµР· `dotnet run`
```

### Package Management
```bash
# Р”РѕР±Р°РІРёС‚СЊ NuGet РїР°РєРµС‚
dotnet add src/AudioRecorder.Services/AudioRecorder.Services.csproj package <PackageName>

# Р’РѕСЃСЃС‚Р°РЅРѕРІРёС‚СЊ РїР°РєРµС‚С‹
dotnet restore
```

### Project References
```bash
# Р”РѕР±Р°РІРёС‚СЊ СЃСЃС‹Р»РєСѓ РЅР° РїСЂРѕРµРєС‚
dotnet add src/AudioRecorder.App/AudioRecorder.csproj reference src/AudioRecorder.Services/AudioRecorder.Services.csproj
```

## Key Features (Implemented)

### Audio Capture
- **WasapiAudioCaptureService** (`src/AudioRecorder.Services/Audio/WasapiAudioCaptureService.cs`)
  - Loopback capture РґР»СЏ СЃРёСЃС‚РµРјРЅРѕРіРѕ Р·РІСѓРєР°
  - Capture РґР»СЏ РјРёРєСЂРѕС„РѕРЅРѕРІ
  - Real-time Р·Р°РїРёСЃСЊ РІ WAV С„РѕСЂРјР°С‚
  - РЎРѕР±С‹С‚РёСЏ РёР·РјРµРЅРµРЅРёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ
  - РђРІС‚РѕРєРѕРЅРІРµСЂС‚Р°С†РёСЏ WAV РІ MP3 РґР»СЏ СЌРєРѕРЅРѕРјРёРё РјРµСЃС‚Р°

### Transcription
- **WhisperTranscriptionService** (`src/AudioRecorder.Services/Transcription/WhisperTranscriptionService.cs`)
  - РРЅС‚РµРіСЂР°С†РёСЏ СЃ faster-whisper-xxl
  - Р”РёР°СЂРёР·Р°С†РёСЏ С‡РµСЂРµР· pyannote_v3.1 (СЂР°Р·РґРµР»РµРЅРёРµ СЃРїРёРєРµСЂРѕРІ)
  - Р”РµС‚Р°Р»СЊРЅС‹Р№ РїСЂРѕРіСЂРµСЃСЃ СЃ РѕС‚РѕР±СЂР°Р¶РµРЅРёРµРј СЃРєРѕСЂРѕСЃС‚Рё Рё РІСЂРµРјРµРЅРё
  - РџР°СЂСЃРёРЅРі РІСЂРµРјРµРЅРЅС‹С… РјРµС‚РѕРє Рё СЃРµРіРјРµРЅС‚РѕРІ
  - РћР±СЂР°Р±РѕС‚РєР° С„Р°Р№Р»РѕРІ Р»СЋР±РѕР№ РґР»РёРЅС‹ (60+ РјРёРЅСѓС‚)
  - РРіРЅРѕСЂРёСЂРѕРІР°РЅРёРµ cleanup crash (-1073740791) РїСЂРё СѓСЃРїРµС€РЅРѕРј СЂРµР·СѓР»СЊС‚Р°С‚Рµ
  - РџРѕР»РЅРѕРµ Р»РѕРіРёСЂРѕРІР°РЅРёРµ РїСЂРѕС†РµСЃСЃР° РґР»СЏ РґРёР°РіРЅРѕСЃС‚РёРєРё

### UI
- **MainPage** (`src/AudioRecorder.App/Views/MainPage.xaml`)
  - Р’С‹Р±РѕСЂ РёСЃС‚РѕС‡РЅРёРєР° Р°СѓРґРёРѕ (dropdown)
  - РљРЅРѕРїРєРё СѓРїСЂР°РІР»РµРЅРёСЏ Р·Р°РїРёСЃСЊСЋ (СЃС‚Р°СЂС‚/СЃС‚РѕРї/РїР°СѓР·Р°)
  - Real-time РѕС‚РѕР±СЂР°Р¶РµРЅРёРµ СЃС‚Р°С‚СѓСЃР° (РґР»РёС‚РµР»СЊРЅРѕСЃС‚СЊ, СЂР°Р·РјРµСЂ)
  - РРјРїРѕСЂС‚ Р°СѓРґРёРѕС„Р°Р№Р»РѕРІ
  - Р”РµС‚Р°Р»СЊРЅС‹Р№ РїСЂРѕРіСЂРµСЃСЃ С‚СЂР°РЅСЃРєСЂРёРїС†РёРё (РїСЂРѕС†РµРЅС‚, СЃРєРѕСЂРѕСЃС‚СЊ, РІСЂРµРјСЏ)
  - РЎС‚Р°С‚РёСЃС‚РёРєР° СЂРµР·СѓР»СЊС‚Р°С‚Р° (СЃРёРјРІРѕР»С‹, СЃР»РѕРІР°, СЂР°Р·РјРµСЂ С„Р°Р№Р»Р°)
  - Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ С‚СЂР°РЅСЃРєСЂРёРїС†РёРё: С‚РµРєСЃС‚ СЃРµРіРјРµРЅС‚РѕРІ Рё РёРјРµРЅР° СЃРїРёРєРµСЂРѕРІ
  - Р’РѕСЃРїСЂРѕРёР·РІРµРґРµРЅРёРµ Р°СѓРґРёРѕ РїРѕ РєР»РёРєСѓ РЅР° РІСЂРµРјРµРЅРЅСѓСЋ РјРµС‚РєСѓ
  - РРЅРґРёРєР°С‚РѕСЂ РЅРµСЃРѕС…СЂР°РЅС‘РЅРЅС‹С… РёР·РјРµРЅРµРЅРёР№

## Notes

- Р—Р°РїРёСЃРё СЃРѕС…СЂР°РЅСЏСЋС‚СЃСЏ РІ `%USERPROFILE%\\Documents\\Contora\\`
- Р¤РѕСЂРјР°С‚ С„Р°Р№Р»РѕРІ: `recording_YYYYMMDD_HHmmss.wav` в†’ `recording_YYYYMMDD_HHmmss.mp3`
- WAV Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РєРѕРЅРІРµСЂС‚РёСЂСѓРµС‚СЃСЏ РІ MP3 Рё СѓРґР°Р»СЏРµС‚СЃСЏ
- Р›РѕРіРё Whisper СЃРѕС…СЂР°РЅСЏСЋС‚СЃСЏ РєР°Рє `{РёРјСЏ}_whisper.log` СЂСЏРґРѕРј СЃ СЂРµР·СѓР»СЊС‚Р°С‚РѕРј
- Launcher СЃРєСЂРёРїС‚С‹ РІ РєРѕСЂРЅРµ: `run-debug.bat`, `run-release.bat`, `build-and-run.ps1`

