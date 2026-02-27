# Changelog

Все значимые изменения в проекте Contora документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и проект придерживается [семантического версионирования](https://semver.org/lang/ru/).

## [0.3.0] - 2026-02-27

### Added
- Video import support in transcription flow (`.mp4`, `.m4v`, `.mov`, `.avi`, `.mkv`, `.webm`, `.wmv`).
- Automatic audio track extraction to MP3 via `ffmpeg` when importing video files.

### Changed
- Transcription pipeline now accepts imported video by normalizing it to MP3 before Whisper/diarization.
- Added `ffmpeg` resolution strategy: `CONTORA_FFMPEG_EXE` env var, bundled runtime path, app directory, then `PATH`.

## [0.2.7] - 2026-02-27

### Fixed
- Fixed intermittent mojibake in Russian UI labels by normalizing runtime UI strings that overwrite XAML defaults.
- Added repository `.editorconfig` UTF-8 policy to prevent accidental ANSI saves for source/UI files.

### Release
- Published Windows installer as a GitHub Release asset (`Contora-win-Setup.exe`).
- Kept Whisper runtime/model out of installer package; runtime and model are downloaded in-app on demand.

## [0.2.6] - 2026-02-19

### Fixed
- Stabilized WinUI title bar icon application to prevent fallback to default system icon.
- Finalized tray/title bar icon consistency in release runtime environment.

### Changed
- Bumped stable release version for GitHub distribution and rollback point.

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
- Release build/publish flow for current branded binary.

## [0.2.2] - 2026-01-28

### Исправлено
- **Критический краш на длинных файлах:** Решена проблема с ошибкой `-1073740791` (0xC0000409) при транскрипции аудио длительностью 60+ минут. Whisper успешно завершает транскрипцию, но крашится при cleanup (освобождение памяти pyannote/CUDA). Теперь приложение игнорирует этот краш, если выходной файл успешно создан.
- **Переполнение памяти на больших файлах:** Исправлена утечка памяти при накоплении всего вывода Whisper в `StringBuilder`. Теперь хранятся только последние 200 строк для error message.
- **Race condition при завершении процесса:** Добавлено ожидание завершения обработки stdout/stderr streams после `WaitForExitAsync` для предотвращения преждевременного возврата управления.

### Добавлено
- **Детальный прогресс транскрипции:** Отображение в реальном времени:
  - Процент выполнения
  - Скорость расшифровки (audio seconds/s)
  - Прошедшее время с начала транскрипции
  - Расчётное оставшееся время
  - Хронометраж обработанного/общего аудио
- **Статистика результатов:** После завершения транскрипции отображается:
  - Количество символов в тексте
  - Количество слов
  - Размер выходного файла (КБ)
- **Полное логирование Whisper:** Весь вывод процесса Whisper (stdout + stderr) сохраняется в файл `{имя_аудио}_whisper.log` рядом с результатом для диагностики проблем.
- **Launcher скрипты:** Добавлены `run-debug.bat`, `run-release.bat` и `build-and-run.ps1` для быстрого запуска приложения из корня проекта без навигации по вложенным директориям.

### Изменено
- **Парсинг прогресса Whisper:** Обновлён regex для корректной обработки формата вывода `faster-whisper-xxl` с флагом `-pp`:
  ```
  1% |   35/4423 | 00:01<<02:22 | 30.73 audio seconds/s
  ```
- **UI прогресса:** Упрощён интерфейс для исключения дублирования информации. Основная строка показывает процент и хронометраж, детали (прошло, скорость, осталось) выводятся отдельными полями.
- **Параметры Whisper:** Убраны экспериментальные параметры `--chunk_length` и `--compute_type`, которые вызывали краш на финализации. Добавлены флаги `-pp` (print_progress) и `--standard` для стабильного вывода.

## [0.2.1] - 2026-01-23

### Добавлено
- Редактирование транскрипции: прямое редактирование текста сегментов в UI
- Переименование спикеров через контекстное меню (ПКМ)
- Воспроизведение аудио по клику на временную метку
- Индикатор несохранённых изменений (зелёная галочка / жёлтый круг)

### Исправлено
- Парсинг транскрипции для корректной обработки временных меток
- Обрезка длинных путей в UI с добавлением tooltip

### Изменено
- Улучшена эстетика интерфейса: компактная раскладка без дублирования информации

## [0.2.0] - 2026-01-19

### Добавлено
- Базовый UI для отображения транскрипции с сегментами
- Поддержка диаризации через pyannote_v3.1
- Редактирование имён спикеров

## [0.1.0] - 2026-01-15

### Добавлено
- Запись системного звука через WASAPI loopback
- Запись микрофона
- Импорт аудиофайлов (WAV, MP3, FLAC, OGG, M4A, OPUS)
- Локальная транскрипция через faster-whisper-xxl
- Конвертация WAV в MP3 для экономии места
- Базовый WinUI 3 интерфейс
- Уведомления о завершении транскрипции


