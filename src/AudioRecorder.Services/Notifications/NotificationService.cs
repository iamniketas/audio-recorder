using System.Runtime.InteropServices;

namespace AudioRecorder.Services.Notifications;

/// <summary>
/// Сервис Windows уведомлений
/// </summary>
public static class NotificationService
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);

    // Типы звуков Windows
    private const uint MB_ICONASTERISK = 0x00000040;      // Информация (звёздочка)
    private const uint MB_ICONEXCLAMATION = 0x00000030;   // Предупреждение

    /// <summary>
    /// Инициализация системы уведомлений
    /// </summary>
    public static void Initialize()
    {
        // Для будущей расширенной инициализации
    }

    /// <summary>
    /// Показать уведомление о завершении транскрипции (звук)
    /// </summary>
    public static void ShowTranscriptionCompleted(string fileName, int segmentsCount, string? filePath = null)
    {
        try
        {
            // Воспроизводим системный звук "Информация"
            MessageBeep(MB_ICONASTERISK);
        }
        catch
        {
            // Игнорируем ошибки
        }
    }

    /// <summary>
    /// Показать уведомление об ошибке (звук)
    /// </summary>
    public static void ShowError(string title, string message)
    {
        try
        {
            MessageBeep(MB_ICONEXCLAMATION);
        }
        catch
        {
            // Игнорируем ошибки
        }
    }

    /// <summary>
    /// Мигание окна в панели задач
    /// </summary>
    public static void FlashTaskbarIcon(IntPtr windowHandle)
    {
        try
        {
            FlashWindow(windowHandle, true);
        }
        catch
        {
            // Игнорируем ошибки
        }
    }

    /// <summary>
    /// Очистка ресурсов
    /// </summary>
    public static void Unregister()
    {
        // Ничего не требуется
    }
}
