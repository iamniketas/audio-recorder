# Whisper Runtime Layout (Production)

## Каноничное расположение
- Runtime root: `%LocalAppData%\AudioRecorder\runtime\faster-whisper-xxl`
- Whisper executable: `%LocalAppData%\AudioRecorder\runtime\faster-whisper-xxl\faster-whisper-xxl.exe`
- Models root: `%LocalAppData%\AudioRecorder\runtime\faster-whisper-xxl\_models`
- large-v2: `%LocalAppData%\AudioRecorder\runtime\faster-whisper-xxl\_models\faster-whisper-large-v2`

## Источник бинарника
- Официальные релизы `Purfview/whisper-standalone-win`
- Используется tag `Faster-Whisper-XXL`, windows-asset `Faster-Whisper-XXL_*_windows.7z`

## Источник модели
- Hugging Face `Systran/faster-whisper-large-v2` (files via `resolve/main`)

## Переменные окружения
Приложение выставляет (Process + User):
- `AUDIORECORDER_WHISPER_EXE`
- `AUDIORECORDER_WHISPER_ROOT`
- `AUDIORECORDER_WHISPER_MODELS_DIR`
- `AUDIORECORDER_WHISPER_MODEL_LARGE_V2_DIR`

Это позволяет другим локальным сервисам использовать те же файлы без дублирования.
