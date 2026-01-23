using System;
using System.IO;
using System.Threading;

namespace AudioRecorder.Services.Logging
{
    public static class AppLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath = string.Empty;

        public static void Initialize(string logFilePath)
        {
            _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
            // Создаем директорию, если она не существует
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            // Добавим проверку, что файл создался
            try
            {
                File.AppendAllText(_logFilePath, $"[INFO] Лог-файл инициализирован: {DateTime.Now}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // Если не можем записать в лог, выводим в консоль
                System.Console.WriteLine($"Ошибка инициализации лога: {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] {message}{Environment.NewLine}";

                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // Игнорируем ошибки логирования, чтобы не прерывать работу приложения
            }
        }

        public static void LogInfo(string message)
        {
            Log($"INFO: {message}");
        }

        public static void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }

        public static void LogError(string message)
        {
            Log($"ERROR: {message}");
        }

        public static void LogError(Exception ex)
        {
            Log($"ERROR: {ex.Message}{Environment.NewLine}Stack trace: {ex.StackTrace}");
        }
    }
}