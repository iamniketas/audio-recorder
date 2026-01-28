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
- Be concise. Don't say "I will now fix the bug..." — just fix it and show the result.
- Stop using phrases like "You can try..." or "I suggest...". Use "I have fixed...", "I have deployed...".
- ALWAYS respond to the user in Russian (на русском языке).
- Keep technical terms and code snippets in their original form (English).
- Internal thoughts/reasoning can be in Russian too.

## Project Overview

AudioRecorder — нативное Windows-приложение для записи и расшифровки любого аудио: звонки, игры, подкасты, голосовые заметки. Локальная STT с диаризацией и интеллектуальное резюмирование.

**Current Status:** MVP в разработке - базовый UI и захват аудио через WASAPI реализованы.

## Target Stack (Windows Native)

- **UI Framework:** WinUI 3 + .NET 8 (C#) — нативный Fluent Design для Windows 11
- **Audio Capture:** NAudio + WASAPI (loopback для системного звука, capture для микрофона)
- **STT Engine:** Whisper.cpp (C++ с CUDA) или Faster-Whisper через Python interop
- **ML Runtime:** ONNX Runtime с CUDA для GPU-ускорения
- **Embeddings:** BGE-M3 для мультиязычного семантического поиска
- **Storage:** SQLite + sqlite-vec, Markdown export
- **Languages:** Russian/English

**macOS версия** — отдельная разработка позже (SwiftUI + Core Audio), после валидации Windows MVP.

## Audio Sources

Приложение обрабатывает аудио из трёх источников:

1. **System Output (Loopback)** — всё, что пользователь слышит: звонки, игры, видео, музыка
2. **Microphone Input** — голос пользователя (для полной записи диалогов)
3. **File Import** — загрузка готовых аудиофайлов (голосовые заметки, подкасты, записи)

Поддерживаемые форматы: WAV, MP3, FLAC, OGG, M4A, OPUS.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     WinUI 3 UI Layer                        │
├─────────────────────────────────────────────────────────────┤
│  Audio Service    │  Transcription    │  LLM Service        │
│  (NAudio/WASAPI)  │  (Whisper.cpp)    │  (Local/Optional)   │
├─────────────────────────────────────────────────────────────┤
│              Core Services (.NET 8)                         │
│  - Project Management                                       │
│  - Storage (SQLite + Vectors)                              │
│  - Search & Indexing (RAG)                                 │
│  - Export (Markdown)                                       │
└─────────────────────────────────────────────────────────────┘
```

**Pipeline:**
1. **Capture/Import** → WASAPI loopback + mic, или импорт файла
2. **Preprocessing** → VAD (отсечение тишины), нормализация громкости
3. **STT + Diarization** → Whisper с разделением по спикерам (S1, S2...)
4. **Post-processing** → LLM: очистка текста, структурирование, извлечение решений/рисков/задач
5. **Indexing** → Эмбеддинги для семантического поиска
6. **Export** → Markdown, позже интеграции (Slack, Telegram, Jira)

Audio is temporary (deleted after transcription). Only text and metadata are stored locally.

## Quality Profiles

- **Quality (Large)** — maximum accuracy
- **Balance (Medium)** — compromise
- **Speed (Small/Distil)** — fast draft

Performance target: 1 hour audio in <30 min (achieved ~6-8 min on RTX 3070 Ti).

## Roadmap

- **v0.1 (MVP Windows):** Запись системного звука + микрофона, импорт файлов, локальная STT с диаризацией, Markdown export
- **v0.2:** Семантический поиск по всем записям, LLM post-processing (конспект, задачи)
- **v0.3:** Автопротокол → задачи, дайджесты, экспорт в Telegram/Slack/Jira
- **v1.0:** Стабильные сборки, профили качества, опциональные платные функции
- **v1.x:** macOS версия (SwiftUI + Core Audio) — отдельная кодовая база

## Project Structure

```
audio-recorder/
├── AudioRecorder.sln          # Solution файл
├── src/
│   ├── AudioRecorder.App/     # WinUI 3 приложение (UI слой)
│   ├── AudioRecorder.Core/    # Core модели и интерфейсы
│   └── AudioRecorder.Services/ # Сервисы (Audio, STT, Storage)
└── plan.txt                   # Полная спецификация на русском
```

## Development Commands

### Build & Run
```bash
# Собрать весь solution
dotnet build AudioRecorder.sln

# Запустить приложение (только через Visual Studio или напрямую из bin/)
# WinUI 3 приложения нельзя запустить через `dotnet run`
```

### Package Management
```bash
# Добавить NuGet пакет
dotnet add src/AudioRecorder.Services/AudioRecorder.Services.csproj package <PackageName>

# Восстановить пакеты
dotnet restore
```

### Project References
```bash
# Добавить ссылку на проект
dotnet add src/AudioRecorder.App/AudioRecorder.csproj reference src/AudioRecorder.Services/AudioRecorder.Services.csproj
```

## Key Features (Implemented)

### Audio Capture
- **WasapiAudioCaptureService** (`src/AudioRecorder.Services/Audio/WasapiAudioCaptureService.cs`)
  - Loopback capture для системного звука
  - Capture для микрофонов
  - Real-time запись в WAV формат
  - События изменения состояния
  - Автоконвертация WAV в MP3 для экономии места

### Transcription
- **WhisperTranscriptionService** (`src/AudioRecorder.Services/Transcription/WhisperTranscriptionService.cs`)
  - Интеграция с faster-whisper-xxl
  - Диаризация через pyannote_v3.1 (разделение спикеров)
  - Детальный прогресс с отображением скорости и времени
  - Парсинг временных меток и сегментов
  - Обработка файлов любой длины (60+ минут)
  - Игнорирование cleanup crash (-1073740791) при успешном результате
  - Полное логирование процесса для диагностики

### UI
- **MainPage** (`src/AudioRecorder.App/Views/MainPage.xaml`)
  - Выбор источника аудио (dropdown)
  - Кнопки управления записью (старт/стоп/пауза)
  - Real-time отображение статуса (длительность, размер)
  - Импорт аудиофайлов
  - Детальный прогресс транскрипции (процент, скорость, время)
  - Статистика результата (символы, слова, размер файла)
  - Редактирование транскрипции: текст сегментов и имена спикеров
  - Воспроизведение аудио по клику на временную метку
  - Индикатор несохранённых изменений

## Notes

- Записи сохраняются в `%USERPROFILE%\Documents\AudioRecorder\`
- Формат файлов: `recording_YYYYMMDD_HHmmss.wav` → `recording_YYYYMMDD_HHmmss.mp3`
- WAV автоматически конвертируется в MP3 и удаляется
- Логи Whisper сохраняются как `{имя}_whisper.log` рядом с результатом
- Launcher скрипты в корне: `run-debug.bat`, `run-release.bat`, `build-and-run.ps1`