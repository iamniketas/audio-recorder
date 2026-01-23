using AudioRecorder.Core.Models;
using AudioRecorder.Core.Services;
using AudioRecorder.Services.Audio;
using AudioRecorder.Services.Settings;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioRecorder.Views;

/// <summary>
/// ViewModel для источника аудио с поддержкой выбора
/// </summary>
public class AudioSourceViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public AudioSource Source { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public AudioSourceViewModel(AudioSource source, bool isSelected = false)
    {
        Source = source;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Главная страница приложения для записи аудио
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _updateTimer;
    private string? _lastRecordingPath;

    public ObservableCollection<AudioSourceViewModel> OutputSources { get; } = new();
    public ObservableCollection<AudioSourceViewModel> InputSources { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        _audioCaptureService = new WasapiAudioCaptureService();
        _audioCaptureService.RecordingStateChanged += OnRecordingStateChanged;

        _settingsService = new LocalSettingsService();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Таймер для обновления UI каждые 500мс
        _updateTimer = _dispatcherQueue.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _updateTimer.Tick += (s, e) => UpdateRecordingInfo();

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAudioSourcesAsync();
    }

    private async Task LoadAudioSourcesAsync()
    {
        try
        {
            var sources = await _audioCaptureService.GetAvailableSourcesAsync();
            var savedSourceIds = _settingsService.LoadSelectedSourceIds();

            OutputSources.Clear();
            InputSources.Clear();

            foreach (var source in sources)
            {
                var isSelected = savedSourceIds.Contains(source.Id) ||
                               (savedSourceIds.Count == 0 && source.IsDefault);

                var viewModel = new AudioSourceViewModel(source, isSelected);

                if (source.Type == AudioSourceType.SystemOutput)
                {
                    OutputSources.Add(viewModel);
                }
                else if (source.Type == AudioSourceType.Microphone)
                {
                    InputSources.Add(viewModel);
                }
            }

            UpdateStartButtonState();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync($"Ошибка загрузки устройств: {ex.Message}");
        }
    }

    private void OnSourceSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateStartButtonState();
    }

    private void UpdateStartButtonState()
    {
        var hasSelection = OutputSources.Any(s => s.IsSelected) || InputSources.Any(s => s.IsSelected);
        StartStopButton.IsEnabled = hasSelection;
    }

    private async void OnStartStopClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = _audioCaptureService.GetCurrentRecordingInfo();

            if (info.State == RecordingState.Stopped)
            {
                // Начинаем запись
                var selectedSources = OutputSources.Concat(InputSources)
                    .Where(vm => vm.IsSelected)
                    .Select(vm => vm.Source)
                    .ToList();

                if (selectedSources.Count == 0)
                {
                    await ShowErrorDialogAsync("Выберите хотя бы один источник аудио");
                    return;
                }

                // Сохраняем выбор
                _settingsService.SaveSelectedSourceIds(selectedSources.Select(s => s.Id));

                _lastRecordingPath = GetOutputPath();
                await _audioCaptureService.StartRecordingAsync(selectedSources, _lastRecordingPath);

                StartStopButton.Content = "Остановить запись";
                PauseResumeButton.IsEnabled = true;
                OutputSourcesListView.IsEnabled = false;
                InputSourcesListView.IsEnabled = false;

                _updateTimer.Start();
            }
            else
            {
                // Останавливаем запись
                await _audioCaptureService.StopRecordingAsync();

                StartStopButton.Content = "Начать запись";
                PauseResumeButton.IsEnabled = false;
                PauseResumeButton.Content = "Пауза";
                OutputSourcesListView.IsEnabled = true;
                InputSourcesListView.IsEnabled = true;

                _updateTimer.Stop();

                // Показываем уведомление о сохранении
                if (_lastRecordingPath != null && File.Exists(_lastRecordingPath))
                {
                    await ShowRecordingSavedDialogAsync(_lastRecordingPath);
                }
            }
        }
        catch (Exception ex)
        {
            // Добавляем дополнительную обработку ошибок для улучшения отладки
            System.Diagnostics.Debug.WriteLine($"Ошибка в OnStartStopClicked: {ex.Message}");
            await ShowErrorDialogAsync($"Ошибка: {ex.Message}");
        }
    }

    private async void OnPauseResumeClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = _audioCaptureService.GetCurrentRecordingInfo();

            if (info.State == RecordingState.Recording)
            {
                await _audioCaptureService.PauseRecordingAsync();
                PauseResumeButton.Content = "Возобновить";
            }
            else if (info.State == RecordingState.Paused)
            {
                await _audioCaptureService.ResumeRecordingAsync();
                PauseResumeButton.Content = "Пауза";
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync($"Ошибка: {ex.Message}");
        }
    }

    private async void OnImportClicked(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary
        };

        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".ogg");

        // Инициализация для WinUI 3
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            // TODO: Обработка импорта файла
            await ShowInfoDialogAsync($"Выбран файл: {file.Path}\n\nФункция импорта будет реализована позже.");
        }
    }

    private void OnRecordingStateChanged(object? sender, RecordingInfo info)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            UpdateRecordingInfo(info);
        });
    }

    private void UpdateRecordingInfo()
    {
        var info = _audioCaptureService.GetCurrentRecordingInfo();
        UpdateRecordingInfo(info);
    }

    private void UpdateRecordingInfo(RecordingInfo info)
    {
        StateTextBlock.Text = info.State switch
        {
            RecordingState.Stopped => "Остановлено",
            RecordingState.Recording => "Идёт запись",
            RecordingState.Paused => "Пауза",
            _ => "Неизвестно"
        };

        DurationTextBlock.Text = info.Duration.ToString(@"hh\:mm\:ss");
        FileSizeTextBlock.Text = FormatFileSize(info.FileSizeBytes);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private string GetOutputPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var audioRecorderPath = Path.Combine(documentsPath, "AudioRecorder");

        Directory.CreateDirectory(audioRecorderPath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(audioRecorderPath, $"recording_{timestamp}.wav");
    }

    private async Task ShowRecordingSavedDialogAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = FormatFileSize(new FileInfo(filePath).Length);

        var dialog = new ContentDialog
        {
            Title = "✅ Запись сохранена",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Файл: {fileName}\nРазмер: {fileSize}",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new HyperlinkButton
                    {
                        Content = "Открыть папку с записью",
                        Tag = filePath
                    }
                }
            },
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        // Находим кнопку и подписываемся на клик
        var stackPanel = (StackPanel)dialog.Content;
        var button = (HyperlinkButton)stackPanel.Children[1];
        button.Click += (s, e) =>
        {
            try
            {
                var folderPath = Path.GetDirectoryName(filePath);
                if (folderPath != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch { }
        };

        await dialog.ShowAsync();
    }

    private async Task ShowErrorDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Ошибка",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async Task ShowInfoDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Информация",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
