using AudioRecorder.Core.Models;
using AudioRecorder.Core.Services;
using AudioRecorder.Services.Audio;
using AudioRecorder.Services.Notifications;
using AudioRecorder.Services.Pipeline;
using AudioRecorder.Services.Models;
using AudioRecorder.Services.Settings;
using AudioRecorder.Services.Transcription;
using AudioRecorder.Services.Updates;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Velopack;

namespace AudioRecorder.Views;

/// <summary>
/// ViewModel РґР»СЏ РёСЃС‚РѕС‡РЅРёРєР° Р°СѓРґРёРѕ СЃ РїРѕРґРґРµСЂР¶РєРѕР№ РІС‹Р±РѕСЂР°
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
/// ViewModel РґР»СЏ СЃРїРёРєРµСЂР°
/// </summary>
public class SpeakerViewModel : INotifyPropertyChanged
{
    private string _name;

    public string Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public SpeakerViewModel(string id, string name)
    {
        Id = id;
        _name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel РґР»СЏ СЃРµРіРјРµРЅС‚Р° С‚СЂР°РЅСЃРєСЂРёРїС†РёРё
/// </summary>
public class TranscriptionSegmentViewModel : INotifyPropertyChanged
{
    private string _speakerName;
    private string _text;

    public TimeSpan Start { get; }
    public TimeSpan End { get; }
    public string SpeakerId { get; }

    public string SpeakerName
    {
        get => _speakerName;
        set
        {
            if (_speakerName != value)
            {
                _speakerName = value;
                OnPropertyChanged();
            }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }

    public string TimestampDisplay => Start.ToString(@"mm\:ss");

    public TranscriptionSegmentViewModel(TranscriptionSegment segment, string speakerName)
    {
        Start = segment.Start;
        End = segment.End;
        SpeakerId = segment.Speaker;
        _speakerName = speakerName;
        _text = segment.Text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Р“Р»Р°РІРЅР°СЏ СЃС‚СЂР°РЅРёС†Р° РїСЂРёР»РѕР¶РµРЅРёСЏ РґР»СЏ Р·Р°РїРёСЃРё Р°СѓРґРёРѕ
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ISettingsService _settingsService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ISessionPipelineService _sessionPipelineService;
    private readonly AppUpdateService _appUpdateService;
    private readonly WhisperRuntimeInstallerService _runtimeInstallerService;
    private readonly WhisperModelDownloadService _modelDownloadService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _updateTimer;
    private string? _lastRecordingPath;
    private string? _lastTranscriptionPath;
    private CancellationTokenSource? _transcriptionCts;
    private bool _isSettingsPanelVisible = true;
    private TranscriptionSegmentViewModel? _playingSegment;
    private bool _hasUnsavedChanges;
    private bool _isRuntimeDownloadInProgress;
    private CancellationTokenSource? _runtimeDownloadCts;
    private bool _isModelDownloadInProgress;
    private CancellationTokenSource? _modelDownloadCts;
    private bool _isUpdateFlowRunning;
    private UpdateInfo? _availableUpdateInfo;
    private VelopackAsset? _readyToApplyRelease;
    // РЎР»РѕРІР°СЂСЊ: РѕСЂРёРіРёРЅР°Р»СЊРЅС‹Р№ ID СЃРїРёРєРµСЂР° в†’ С‚РµРєСѓС‰РµРµ РёРјСЏ
    private readonly Dictionary<string, string> _speakerNameMap = new();

    public ObservableCollection<AudioSourceViewModel> OutputSources { get; } = new();
    public ObservableCollection<AudioSourceViewModel> InputSources { get; } = new();
    public ObservableCollection<SpeakerViewModel> Speakers { get; } = new();
    public ObservableCollection<TranscriptionSegmentViewModel> TranscriptionSegments { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        _audioCaptureService = new WasapiAudioCaptureService();
        _audioCaptureService.RecordingStateChanged += OnRecordingStateChanged;

        _transcriptionService = new WhisperTranscriptionService();
        _transcriptionService.ProgressChanged += OnTranscriptionProgressChanged;

        _settingsService = new LocalSettingsService();
        _playbackService = new AudioPlaybackService();
        _sessionPipelineService = new SessionPipelineService();
        _playbackService.StateChanged += OnPlaybackStateChanged;
        _appUpdateService = new AppUpdateService();
        _runtimeInstallerService = new WhisperRuntimeInstallerService();
        _modelDownloadService = new WhisperModelDownloadService();

        NotificationService.Initialize();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _updateTimer = _dispatcherQueue.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _updateTimer.Tick += (s, e) => UpdateRecordingInfo();

        Loaded += OnPageLoaded;
        Unloaded += (s, e) =>
        {
            _runtimeDownloadCts?.Cancel();
            _runtimeDownloadCts?.Dispose();
            _runtimeDownloadCts = null;
            _modelDownloadCts?.Cancel();
            _modelDownloadCts?.Dispose();
            _modelDownloadCts = null;
            _appUpdateService.Dispose();
        };
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAudioSourcesAsync();
        LoadOutputFolderSetting();

        if (_runtimeInstallerService.IsRuntimeInstalled())
        {
            WhisperPaths.RegisterEnvironmentVariables(_runtimeInstallerService.GetRuntimeExePath(), "large-v2");
        }

        _ = CheckForUpdatesAsync(userInitiated: false);
        UpdateTranscriptionAvailabilityUi();
        _ = TryAutoSetupWhisperAsync();
    }

    private async Task TryAutoSetupWhisperAsync()
    {
        if (_isRuntimeDownloadInProgress || _isModelDownloadInProgress)
            return;

        if (!_runtimeInstallerService.IsRuntimeInstalled())
        {
            await StartRuntimeDownloadAsync(autoStarted: true);
        }

        if (_runtimeInstallerService.IsRuntimeInstalled() && !_modelDownloadService.IsModelInstalled())
        {
            await StartModelDownloadAsync(autoStarted: true);
        }
    }

    private void UpdateTranscriptionAvailabilityUi()
    {
        var whisperAvailable = _transcriptionService.IsWhisperAvailable;
        var modelInstalled = _modelDownloadService.IsModelInstalled();

        DownloadRuntimeButton.Visibility = Visibility.Collapsed;
        CancelRuntimeDownloadButton.Visibility = Visibility.Collapsed;
        RuntimeDownloadProgressBar.Visibility = Visibility.Collapsed;
        RuntimeDownloadStatusText.Visibility = Visibility.Collapsed;
        DownloadModelButton.Visibility = Visibility.Collapsed;
        CancelModelDownloadButton.Visibility = Visibility.Collapsed;
        ModelDownloadProgressBar.Visibility = Visibility.Collapsed;
        ModelDownloadStatusText.Visibility = Visibility.Collapsed;

        if (!whisperAvailable)
        {
            WhisperWarningBar.Title = "Whisper не найден";
            WhisperWarningBar.Message = "Скачайте движок faster-whisper-xxl. Он будет установлен в LocalAppData.";
            WhisperWarningBar.IsOpen = true;

            TranscribeButton.IsEnabled = false;

            DownloadRuntimeButton.Visibility = _isRuntimeDownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
            CancelRuntimeDownloadButton.Visibility = _isRuntimeDownloadInProgress ? Visibility.Visible : Visibility.Collapsed;
            RuntimeDownloadProgressBar.Visibility = _isRuntimeDownloadInProgress ? Visibility.Visible : Visibility.Collapsed;
            RuntimeDownloadStatusText.Visibility = Visibility.Visible;
            return;
        }

        if (modelInstalled)
        {
            WhisperWarningBar.IsOpen = false;
            TranscribeButton.IsEnabled = true;
            return;
        }

        WhisperWarningBar.Title = "Модель Whisper не загружена";
        WhisperWarningBar.Message = "Скачайте модель large-v2. Это требуется один раз.";
        WhisperWarningBar.IsOpen = true;
        TranscribeButton.IsEnabled = false;

        DownloadModelButton.Visibility = _isModelDownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
        CancelModelDownloadButton.Visibility = _isModelDownloadInProgress ? Visibility.Visible : Visibility.Collapsed;
        ModelDownloadProgressBar.Visibility = _isModelDownloadInProgress ? Visibility.Visible : Visibility.Collapsed;
        ModelDownloadStatusText.Visibility = Visibility.Visible;
    }

    private async Task StartModelDownloadAsync(bool autoStarted)
    {
        if (_isModelDownloadInProgress)
            return;

        _isModelDownloadInProgress = true;
        _modelDownloadCts = new CancellationTokenSource();

        DownloadModelButton.Visibility = Visibility.Collapsed;
        CancelModelDownloadButton.Visibility = Visibility.Visible;
        ModelDownloadProgressBar.Visibility = Visibility.Visible;
        ModelDownloadProgressBar.Value = 0;
        ModelDownloadStatusText.Visibility = Visibility.Visible;
        ModelDownloadStatusText.Text = autoStarted
            ? "Первая настройка: скачиваем модель Whisper..."
            : "Скачиваем модель Whisper...";

        try
        {
            var result = await _modelDownloadService.DownloadModelAsync(
                progress =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        ModelDownloadProgressBar.Value = progress.Percent;
                        var downloadedMb = progress.DownloadedBytes / (1024.0 * 1024.0);
                        var totalMb = progress.TotalBytes > 0 ? progress.TotalBytes / (1024.0 * 1024.0) : 0;

                        ModelDownloadStatusText.Text = progress.TotalBytes > 0
                            ? $"Скачивание модели: {progress.Percent}% ({downloadedMb:F1}/{totalMb:F1} MB), файл: {progress.CurrentFile}"
                            : $"Скачивание модели: файл {progress.CurrentFile}";
                    });
                },
                _modelDownloadCts.Token);

            ModelDownloadStatusText.Text = result.StatusMessage;
            if (result.Success)
            {
                WhisperPaths.RegisterEnvironmentVariables(_modelDownloadService.GetWhisperPath(), "large-v2");
            }
        }
        finally
        {
            _modelDownloadCts?.Dispose();
            _modelDownloadCts = null;
            _isModelDownloadInProgress = false;
            UpdateTranscriptionAvailabilityUi();
        }
    }

    private async void OnDownloadModelClicked(object sender, RoutedEventArgs e)
    {
        await StartModelDownloadAsync(autoStarted: false);
    }

    private void OnCancelModelDownloadClicked(object sender, RoutedEventArgs e)
    {
        _modelDownloadCts?.Cancel();
    }

    private async Task StartRuntimeDownloadAsync(bool autoStarted)
    {
        if (_isRuntimeDownloadInProgress)
            return;

        _isRuntimeDownloadInProgress = true;
        _runtimeDownloadCts = new CancellationTokenSource();

        DownloadRuntimeButton.Visibility = Visibility.Collapsed;
        CancelRuntimeDownloadButton.Visibility = Visibility.Visible;
        RuntimeDownloadProgressBar.Visibility = Visibility.Visible;
        RuntimeDownloadProgressBar.Value = 0;
        RuntimeDownloadStatusText.Visibility = Visibility.Visible;
        RuntimeDownloadStatusText.Text = autoStarted
            ? "Первая настройка: скачиваем движок Whisper XXL..."
            : "Скачиваем движок Whisper XXL...";

        try
        {
            var result = await _runtimeInstallerService.InstallAsync(
                progress =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        RuntimeDownloadProgressBar.Value = progress.Percent;
                        RuntimeDownloadStatusText.Text = progress.StatusMessage;
                    });
                },
                _runtimeDownloadCts.Token);

            RuntimeDownloadStatusText.Text = result.StatusMessage;

            if (result.Success && !string.IsNullOrWhiteSpace(result.WhisperExePath))
            {
                WhisperPaths.RegisterEnvironmentVariables(result.WhisperExePath, "large-v2");
            }
        }
        finally
        {
            _runtimeDownloadCts?.Dispose();
            _runtimeDownloadCts = null;
            _isRuntimeDownloadInProgress = false;
            UpdateTranscriptionAvailabilityUi();
        }

        if (_runtimeInstallerService.IsRuntimeInstalled() && !_modelDownloadService.IsModelInstalled())
        {
            await StartModelDownloadAsync(autoStarted: true);
        }
    }

    private async void OnDownloadRuntimeClicked(object sender, RoutedEventArgs e)
    {
        await StartRuntimeDownloadAsync(autoStarted: false);
    }

    private void OnCancelRuntimeDownloadClicked(object sender, RoutedEventArgs e)
    {
        _runtimeDownloadCts?.Cancel();
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_isUpdateFlowRunning)
            return;

        _isUpdateFlowRunning = true;
        SetUpdateUiBusy(true);
        _availableUpdateInfo = null;
        _readyToApplyRelease = null;
        ApplyUpdateButton.Visibility = Visibility.Collapsed;
        UpdateProgressBar.Visibility = Visibility.Collapsed;
        UpdateProgressBar.Value = 0;

        try
        {
            UpdateStatusText.Text = "Проверка обновлений...";
            var checkResult = await _appUpdateService.CheckForUpdatesAsync();

            if (!checkResult.Success)
            {
                UpdateStatusText.Text = checkResult.StatusMessage;
                return;
            }

            if (!checkResult.UpdateAvailable || checkResult.UpdateInfo == null)
            {
                UpdateStatusText.Text = userInitiated
                    ? "Обновлений нет. У вас последняя версия."
                    : "Автопроверка: обновлений нет.";
                return;
            }

            _availableUpdateInfo = checkResult.UpdateInfo;
            UpdateStatusText.Text = $"{checkResult.StatusMessage} Скачивание...";
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;

            var downloadResult = await _appUpdateService.DownloadUpdateAsync(
                _availableUpdateInfo,
                progress =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateProgressBar.Visibility = Visibility.Visible;
                        UpdateProgressBar.Value = Math.Max(0, Math.Min(100, progress));
                        UpdateStatusText.Text = $"Скачивание обновления: {progress}%";
                    });
                });

            if (!downloadResult.Success || downloadResult.ReadyToApplyRelease == null)
            {
                UpdateStatusText.Text = downloadResult.StatusMessage;
                return;
            }

            _readyToApplyRelease = downloadResult.ReadyToApplyRelease;
            UpdateStatusText.Text = downloadResult.StatusMessage;
            ApplyUpdateButton.Visibility = Visibility.Visible;
        }
        finally
        {
            SetUpdateUiBusy(false);
            _isUpdateFlowRunning = false;
        }
    }

    private void SetUpdateUiBusy(bool isBusy)
    {
        CheckUpdatesButton.IsEnabled = !isBusy;
    }

    private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(userInitiated: true);
    }

    private async void OnApplyUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (_readyToApplyRelease == null)
            return;

        var dialog = new ContentDialog
        {
            Title = "Установить обновление",
            Content = "Приложение будет перезапущено для завершения установки.",
            PrimaryButtonText = "Установить и перезапустить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var started = _appUpdateService.ApplyUpdateAndRestart(_readyToApplyRelease);
        if (!started)
        {
            await ShowErrorDialogAsync("Не удалось применить обновление.");
        }
    }

    private void LoadOutputFolderSetting()
    {
        var savedFolder = _settingsService.LoadOutputFolder();
        if (!string.IsNullOrEmpty(savedFolder))
        {
            OutputFolderTextBox.Text = FormatFolderPath(savedFolder);
            ToolTipService.SetToolTip(OutputFolderTextBox, savedFolder);
        }
        else
        {
            var defaultFolder = GetDefaultOutputFolder();
            OutputFolderTextBox.Text = FormatFolderPath(defaultFolder);
            ToolTipService.SetToolTip(OutputFolderTextBox, defaultFolder);
        }
    }

    private static string FormatFolderPath(string fullPath)
    {
        // РџРѕРєР°Р·С‹РІР°РµРј РїРѕСЃР»РµРґРЅРёРµ 2 РєРѕРјРїРѕРЅРµРЅС‚Р° РїСѓС‚Рё СЃ ... РІРїРµСЂРµРґРё РµСЃР»Рё РїСѓС‚СЊ РґР»РёРЅРЅС‹Р№
        var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length > 3)
        {
            return $"...{Path.DirectorySeparatorChar}{string.Join(Path.DirectorySeparatorChar, parts.TakeLast(2))}";
        }
        return fullPath;
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

    private async void OnSelectFolderClicked(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            OutputFolderTextBox.Text = FormatFolderPath(folder.Path);
            ToolTipService.SetToolTip(OutputFolderTextBox, folder.Path);
            _settingsService.SaveOutputFolder(folder.Path);
        }
    }

    private void OnToggleSettingsClicked(object sender, RoutedEventArgs e)
    {
        _isSettingsPanelVisible = !_isSettingsPanelVisible;

        if (_isSettingsPanelVisible)
        {
            SettingsColumn.Width = new GridLength(280);
            SettingsPanel.Visibility = Visibility.Visible;
            ToggleSettingsButton.Content = "В«";
        }
        else
        {
            SettingsColumn.Width = new GridLength(0);
            SettingsPanel.Visibility = Visibility.Collapsed;
            ToggleSettingsButton.Content = "В»";
        }
    }

    private async void OnStartStopClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = _audioCaptureService.GetCurrentRecordingInfo();

            if (info.State == RecordingState.Stopped)
            {
                var selectedSources = OutputSources.Concat(InputSources)
                    .Where(vm => vm.IsSelected)
                    .Select(vm => vm.Source)
                    .ToList();

                if (selectedSources.Count == 0)
                {
                    await ShowErrorDialogAsync("Выберите хотя бы один источник аудио");
                    return;
                }

                _settingsService.SaveSelectedSourceIds(selectedSources.Select(s => s.Id));

                _lastRecordingPath = GetOutputPath();
                await _audioCaptureService.StartRecordingAsync(selectedSources, _lastRecordingPath);

                StartStopButton.Content = "Остановить";
                PauseResumeButton.IsEnabled = true;
                OutputSourcesListView.IsEnabled = false;
                InputSourcesListView.IsEnabled = false;

                CurrentFileTextBlock.Text = Path.GetFileName(_lastRecordingPath);

                _updateTimer.Start();
            }
            else
            {
                await _audioCaptureService.StopRecordingAsync();

                StartStopButton.Content = "Начать запись";
                PauseResumeButton.IsEnabled = false;
                PauseResumeButton.Content = "Пауза";
                OutputSourcesListView.IsEnabled = true;
                InputSourcesListView.IsEnabled = true;

                _updateTimer.Stop();

                if (_lastRecordingPath != null && File.Exists(_lastRecordingPath))
                {
                    if (AudioConverter.IsWavFile(_lastRecordingPath))
                    {
                        StateTextBlock.Text = "Конвертация в MP3...";
                        try
                        {
                            _lastRecordingPath = await AudioConverter.ConvertToMp3Async(
                                _lastRecordingPath, bitrate: 192, deleteOriginal: true);
                            CurrentFileTextBlock.Text = Path.GetFileName(_lastRecordingPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка конвертации: {ex.Message}");
                        }
                        StateTextBlock.Text = "Остановлено";
                    }

                    ShowTranscriptionSection();
                    await ShowRecordingSavedDialogAsync(_lastRecordingPath);
                }
            }
        }
        catch (Exception ex)
        {
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
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".m4v");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".avi");
        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".webm");
        picker.FileTypeFilter.Add(".wmv");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                if (AudioConverter.IsVideoFile(file.Path))
                {
                    TranscriptionControlSection.Visibility = Visibility.Visible;
                    TranscriptionProgressPanel.Visibility = Visibility.Visible;
                    TranscriptionProgressBar.IsIndeterminate = true;
                    TranscriptionStatusText.Text = "Извлекаем аудио из видео...";
                    TranscribeButton.IsEnabled = false;
                    ImportButton.IsEnabled = false;

                    var mp3Path = await AudioConverter.ExtractAudioToMp3Async(file.Path);
                    _lastRecordingPath = mp3Path;
                    CurrentFileTextBlock.Text = Path.GetFileName(mp3Path);
                }
                else
                {
                    _lastRecordingPath = file.Path;
                    CurrentFileTextBlock.Text = file.Name;
                }

                ShowTranscriptionSection();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync($"Не удалось импортировать файл: {ex.Message}");
            }
            finally
            {
                ImportButton.IsEnabled = true;
                TranscriptionProgressPanel.Visibility = Visibility.Collapsed;
                TranscriptionProgressBar.IsIndeterminate = false;
            }
        }
    }

    private void ShowTranscriptionSection()
    {
        TranscriptionControlSection.Visibility = Visibility.Visible;
        TranscribeButton.IsEnabled = true;
        TranscriptionProgressPanel.Visibility = Visibility.Collapsed;
        _lastTranscriptionPath = null;

        // РћС‡РёС‰Р°РµРј РїСЂРµРґС‹РґСѓС‰СѓСЋ С‚СЂР°РЅСЃРєСЂРёРїС†РёСЋ
        TranscriptionSegments.Clear();
        Speakers.Clear();
        SpeakersPanel.Visibility = Visibility.Collapsed;
        SaveTranscriptionButton.Visibility = Visibility.Collapsed;

        UpdateTranscriptionAvailabilityUi();
    }

    private async void OnTranscribeClicked(object sender, RoutedEventArgs e)
    {
        if (_lastRecordingPath == null || !File.Exists(_lastRecordingPath))
        {
            await ShowErrorDialogAsync("Файл записи не найден");
            return;
        }

        TranscribeButton.IsEnabled = false;
        TranscriptionProgressPanel.Visibility = Visibility.Visible;
        TranscriptionProgressBar.IsIndeterminate = true;
        TranscriptionStatusText.Text = "Подготовка...";
        TranscriptionStatsPanel.Visibility = Visibility.Collapsed; // РЎРєСЂС‹РІР°РµРј СЃС‚Р°С‚РёСЃС‚РёРєСѓ

        _transcriptionCts = new CancellationTokenSource();

        try
        {
            var result = await _transcriptionService.TranscribeAsync(_lastRecordingPath, _transcriptionCts.Token);

            if (result.Success)
            {
                _lastTranscriptionPath = result.OutputPath;
                TranscriptionStatusText.Text = $"Готово! {result.Segments.Count} сегментов";
                TranscriptionProgressBar.IsIndeterminate = false;
                TranscriptionProgressBar.Value = 100;

                // Р’С‹С‡РёСЃР»СЏРµРј Рё РїРѕРєР°Р·С‹РІР°РµРј СЃС‚Р°С‚РёСЃС‚РёРєСѓ
                if (result.OutputPath != null && File.Exists(result.OutputPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(result.OutputPath);
                        var text = await File.ReadAllTextAsync(result.OutputPath);

                        var charCount = text.Length;
                        var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        var fileSizeKB = fileInfo.Length / 1024.0;

                        var statsText = $"Символов: {charCount:N0} • Слов: {wordCount:N0} • Размер файла: {fileSizeKB:F1} КБ";
                        TranscriptionStatsText.Text = statsText;
                        TranscriptionStatsPanel.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        // Р•СЃР»Рё РЅРµ СѓРґР°Р»РѕСЃСЊ РїРѕР»СѓС‡РёС‚СЊ СЃС‚Р°С‚РёСЃС‚РёРєСѓ - РЅРµ СЃС‚СЂР°С€РЅРѕ
                    }
                }

                // РћР±РЅРѕРІР»СЏРµРј РїСѓС‚СЊ Рє С„Р°Р№Р»Сѓ
                var mp3Path = Path.ChangeExtension(_lastRecordingPath, ".mp3");
                if (File.Exists(mp3Path))
                {
                    _lastRecordingPath = mp3Path;
                    CurrentFileTextBlock.Text = Path.GetFileName(mp3Path);
                }

                // Р—Р°РіСЂСѓР¶Р°РµРј С‚СЂР°РЅСЃРєСЂРёРїС†РёСЋ РІ UI
                await LoadTranscriptionToUI(result.Segments);

                // Post-processing pipeline: clean -> Ollama -> append to markdown.
                TranscriptionStatusText.Text = "Постобработка через Ollama...";
                var pipelineCt = _transcriptionCts?.Token ?? CancellationToken.None;
                var rawWhisperText = await LoadRawWhisperTextAsync(result, pipelineCt);
                var pipelineResult = await _sessionPipelineService.ProcessSessionAsync(
                    rawWhisperText,
                    result.OutputPath,
                    pipelineCt);

                if (pipelineResult.Success)
                {
                    TranscriptionStatusText.Text = pipelineResult.UsedBackup
                        ? $"Транскрипция сохранена в backup: {Path.GetFileName(pipelineResult.TargetPath)}"
                        : $"Транскрипция и выжимка сохранены: {Path.GetFileName(pipelineResult.TargetPath)}";
                }
                else
                {
                    TranscriptionStatusText.Text = "Транскрипция готова, но постобработка завершилась с ошибкой";
                    AudioRecorder.Services.Logging.AppLogger.LogWarning(
                        $"Session pipeline failed after transcription: {pipelineResult.ErrorMessage}");
                }

                // Р—Р°РіСЂСѓР¶Р°РµРј Р°СѓРґРёРѕ РґР»СЏ РІРѕСЃРїСЂРѕРёР·РІРµРґРµРЅРёСЏ
                if (_lastRecordingPath != null && File.Exists(_lastRecordingPath))
                {
                    await _playbackService.LoadAsync(_lastRecordingPath);
                }

                // РЈРІРµРґРѕРјР»РµРЅРёРµ
                var fileName = Path.GetFileName(_lastRecordingPath ?? "recording");
                NotificationService.ShowTranscriptionCompleted(fileName, result.Segments.Count, result.OutputPath);
            }
            else
            {
                await ShowErrorDialogAsync($"Ошибка транскрипции:\n{result.ErrorMessage}");
                TranscriptionProgressPanel.Visibility = Visibility.Collapsed;
                TranscribeButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync($"Ошибка: {ex.Message}");
            TranscriptionProgressPanel.Visibility = Visibility.Collapsed;
            TranscribeButton.IsEnabled = true;
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
        }
    }

    private Task LoadTranscriptionToUI(IReadOnlyList<TranscriptionSegment> segments)
    {
        TranscriptionSegments.Clear();
        Speakers.Clear();
        _speakerNameMap.Clear();

        // РЎРѕР±РёСЂР°РµРј СѓРЅРёРєР°Р»СЊРЅС‹С… СЃРїРёРєРµСЂРѕРІ
        var speakerIds = segments.Select(s => s.Speaker).Distinct().ToList();
        var speakerMap = new Dictionary<string, SpeakerViewModel>();

        foreach (var id in speakerIds)
        {
            var speaker = new SpeakerViewModel(id, id);
            Speakers.Add(speaker);
            speakerMap[id] = speaker;
            // РРЅРёС†РёР°Р»РёР·РёСЂСѓРµРј СЃР»РѕРІР°СЂСЊ: РѕСЂРёРіРёРЅР°Р»СЊРЅРѕРµ РёРјСЏ = С‚РµРєСѓС‰РµРјСѓ
            _speakerNameMap[id] = id;
        }

        // РЎРѕР·РґР°С‘Рј ViewModels РґР»СЏ СЃРµРіРјРµРЅС‚РѕРІ
        foreach (var segment in segments)
        {
            var speakerName = speakerMap.TryGetValue(segment.Speaker, out var speaker) ? speaker.Name : segment.Speaker;
            TranscriptionSegments.Add(new TranscriptionSegmentViewModel(segment, speakerName));
        }

        // РџРѕРєР°Р·С‹РІР°РµРј РїР°РЅРµР»СЊ СЃРїРёРєРµСЂРѕРІ РµСЃР»Рё РёС… Р±РѕР»СЊС€Рµ РѕРґРЅРѕРіРѕ
        SpeakersPanel.Visibility = Speakers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SaveTranscriptionButton.Visibility = Visibility.Visible;
        SetUnsavedChanges(false);

        return Task.CompletedTask;
    }

    private static async Task<string> LoadRawWhisperTextAsync(TranscriptionResult result, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(result.OutputPath) && File.Exists(result.OutputPath))
        {
            return await File.ReadAllTextAsync(result.OutputPath, ct);
        }

        // Fallback if txt file is unavailable: build raw text from parsed segments.
        var sb = new StringBuilder();
        foreach (var segment in result.Segments)
        {
            sb.Append('[')
              .Append(segment.Start.ToString(@"hh\:mm\:ss"))
              .Append("] ")
              .Append(segment.Speaker)
              .Append(' ')
              .AppendLine(segment.Text);
        }

        return sb.ToString();
    }

    private void OnSpeakerNameChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is SpeakerViewModel speaker)
        {
            // РћР±РЅРѕРІР»СЏРµРј РёРјСЏ СЃРїРёРєРµСЂР° РІРѕ РІСЃРµС… СЃРµРіРјРµРЅС‚Р°С…
            foreach (var segment in TranscriptionSegments.Where(s => s.SpeakerId == speaker.Id))
            {
                segment.SpeakerName = speaker.Name;
            }
            // РћР±РЅРѕРІР»СЏРµРј СЃР»РѕРІР°СЂСЊ СЃРѕРѕС‚РІРµС‚СЃС‚РІРёР№
            _speakerNameMap[speaker.Id] = speaker.Name;
            SetUnsavedChanges(true);
        }
    }

    private void OnSegmentTextChanged(object sender, TextChangedEventArgs e)
    {
        SetUnsavedChanges(true);
    }

    private void SetUnsavedChanges(bool hasChanges)
    {
        _hasUnsavedChanges = hasChanges;
        // РћР±РЅРѕРІР»СЏРµРј РёРЅРґРёРєР°С‚РѕСЂ: Р¶С‘Р»С‚С‹Р№ = РЅРµСЃРѕС…СЂР°РЅРµРЅРѕ, Р·РµР»С‘РЅС‹Р№ = СЃРѕС…СЂР°РЅРµРЅРѕ
        if (SaveIndicator != null)
        {
            SaveIndicator.Fill = hasChanges
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 193, 7)) // Р–С‘Р»С‚С‹Р№
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80)); // Р—РµР»С‘РЅС‹Р№
        }
    }

    private void OnSpeakerRightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        // РљРѕРЅС‚РµРєСЃС‚РЅРѕРµ РјРµРЅСЋ РїРѕРєР°Р¶РµС‚СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё С‡РµСЂРµР· ContextFlyout
    }

    private async void OnRenameSpeakerClicked(object sender, RoutedEventArgs e)
    {
        TranscriptionSegmentViewModel? segment = null;

        // РџРѕРґРґРµСЂР¶РєР° РІС‹Р·РѕРІР° РёР· Border Рё MenuFlyoutItem
        if (sender is MenuFlyoutItem menuItem)
        {
            segment = menuItem.Tag as TranscriptionSegmentViewModel;
        }

        if (segment == null) return;

        var speakerId = segment.SpeakerId;
        var currentName = segment.SpeakerName;

        // РЎРѕР·РґР°С‘Рј РґРёР°Р»РѕРі РґР»СЏ РІРІРѕРґР° РЅРѕРІРѕРіРѕ РёРјРµРЅРё
        var inputBox = new TextBox
        {
            Text = currentName,
            PlaceholderText = "Введите имя спикера",
            SelectionStart = 0,
            SelectionLength = currentName.Length
        };

        var dialog = new ContentDialog
        {
            Title = $"Переименовать спикера",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Оригинальный ID: {speakerId}" },
                    inputBox
                }
            },
            PrimaryButtonText = "Переименовать",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
        {
            var newName = inputBox.Text.Trim();

            // РћР±РЅРѕРІР»СЏРµРј РёРјСЏ РІРѕ РІСЃРµС… СЃРµРіРјРµРЅС‚Р°С… СЃ СЌС‚РёРј speakerId
            foreach (var seg in TranscriptionSegments.Where(s => s.SpeakerId == speakerId))
            {
                seg.SpeakerName = newName;
            }

            // РћР±РЅРѕРІР»СЏРµРј РІ СЃРїРёСЃРєРµ СЃРїРёРєРµСЂРѕРІ
            var speakerVm = Speakers.FirstOrDefault(s => s.Id == speakerId);
            if (speakerVm != null)
            {
                speakerVm.Name = newName;
            }

            // РћР±РЅРѕРІР»СЏРµРј СЃР»РѕРІР°СЂСЊ СЃРѕРѕС‚РІРµС‚СЃС‚РІРёР№
            _speakerNameMap[speakerId] = newName;
            SetUnsavedChanges(true);
        }
    }

    private async void OnTimestampClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TranscriptionSegmentViewModel segment)
        {
            // Р•СЃР»Рё С‚РѕС‚ Р¶Рµ СЃРµРіРјРµРЅС‚ СѓР¶Рµ РёРіСЂР°РµС‚ вЂ” РѕСЃС‚Р°РЅР°РІР»РёРІР°РµРј
            if (_playingSegment == segment && _playbackService.State == PlaybackState.Playing)
            {
                _playbackService.Stop();
                _playingSegment = null;
                return;
            }

            // Р—Р°РіСЂСѓР¶Р°РµРј С„Р°Р№Р» РµСЃР»Рё РµС‰С‘ РЅРµ Р·Р°РіСЂСѓР¶РµРЅ
            if (_lastRecordingPath != null && _playbackService.LoadedFilePath != _lastRecordingPath)
            {
                try
                {
                    await _playbackService.LoadAsync(_lastRecordingPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки аудио: {ex.Message}");
                    await ShowErrorDialogAsync($"Не удалось загрузить аудио: {ex.Message}");
                    return;
                }
            }

            // Р’РѕСЃРїСЂРѕРёР·РІРѕРґРёРј СЃРµРіРјРµРЅС‚
            _playbackService.PlaySegment(segment.Start, segment.End, loop: true);
            _playingSegment = segment;
        }
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (state == PlaybackState.Stopped)
            {
                _playingSegment = null;
            }
        });
    }

    private async void OnSaveTranscriptionClicked(object sender, RoutedEventArgs e)
    {
        if (_lastTranscriptionPath == null)
            return;

        try
        {
            var sb = new StringBuilder();

            foreach (var segment in TranscriptionSegments)
            {
                // РџРѕР»СѓС‡Р°РµРј РёРјСЏ СЃРїРёРєРµСЂР° РёР· СЃР»РѕРІР°СЂСЏ (РёР»Рё РёСЃРїРѕР»СЊР·СѓРµРј С‚РµРєСѓС‰РµРµ)
                var speakerName = _speakerNameMap.TryGetValue(segment.SpeakerId, out var name)
                    ? name
                    : segment.SpeakerName;

                // Р•СЃР»Рё РµСЃС‚СЊ timestamp вЂ” СЃРѕС…СЂР°РЅСЏРµРј СЃ РЅРёРјРё
                if (segment.Start != TimeSpan.Zero || segment.End != TimeSpan.Zero)
                {
                    var startStr = $"{(int)segment.Start.TotalHours:00}:{segment.Start.Minutes:00}:{segment.Start.Seconds:00}.{segment.Start.Milliseconds:000}";
                    var endStr = $"{(int)segment.End.TotalHours:00}:{segment.End.Minutes:00}:{segment.End.Seconds:00}.{segment.End.Milliseconds:000}";
                    sb.AppendLine($"[{startStr} --> {endStr}] [{speakerName}]: {segment.Text}");
                }
                else
                {
                    // Fallback СЃРµРіРјРµРЅС‚С‹ вЂ” С‚РѕР»СЊРєРѕ С‚РµРєСЃС‚
                    sb.AppendLine(segment.Text);
                }
            }

            await File.WriteAllTextAsync(_lastTranscriptionPath, sb.ToString());
            SetUnsavedChanges(false);
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync($"Ошибка сохранения: {ex.Message}");
        }
    }

    private void OnTranscriptionProgressChanged(object? sender, TranscriptionProgress progress)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            TranscriptionStatusText.Text = progress.StatusMessage ?? progress.State.ToString();

            if (progress.ProgressPercent >= 0)
            {
                TranscriptionProgressBar.IsIndeterminate = false;
                TranscriptionProgressBar.Value = progress.ProgressPercent;
            }
            else
            {
                TranscriptionProgressBar.IsIndeterminate = true;
            }

            // РџРѕРєР°Р·С‹РІР°РµРј РґРµС‚Р°Р»СЊРЅСѓСЋ РёРЅС„РѕСЂРјР°С†РёСЋ С‚РѕР»СЊРєРѕ РІРѕ РІСЂРµРјСЏ С‚СЂР°РЅСЃРєСЂРёРїС†РёРё
            if (progress.State == TranscriptionState.Transcribing && progress.ProgressPercent > 0)
            {
                TranscriptionDetailsGrid.Visibility = Visibility.Visible;

                // РћР±РЅРѕРІР»СЏРµРј РґРµС‚Р°Р»Рё
                if (progress.ElapsedTime.HasValue)
                {
                    ElapsedTimeText.Text = FormatTimeSpan(progress.ElapsedTime.Value);
                }

                if (progress.Speed.HasValue && progress.Speed.Value > 0)
                {
                    SpeedText.Text = $"{progress.Speed.Value:F1}x";
                }

                if (progress.RemainingTime.HasValue && progress.RemainingTime.Value.TotalSeconds > 5)
                {
                    RemainingTimeText.Text = $"в‰€{FormatTimeSpan(progress.RemainingTime.Value)}";
                }
                else
                {
                    RemainingTimeText.Text = "скоро...";
                }
            }
            else
            {
                TranscriptionDetailsGrid.Visibility = Visibility.Collapsed;
            }
        });
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"h\:mm\:ss");
        return ts.ToString(@"m\:ss");
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
            RecordingState.Recording => "Запись",
            RecordingState.Paused => "Пауза",
            _ => "Неизвестно"
        };

        DurationTextBlock.Text = info.Duration.ToString(@"hh\:mm\:ss");
        FileSizeTextBlock.Text = FormatFileSize(info.FileSizeBytes);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public (RecordingState State, bool HasSourceSelection) GetTrayStateSnapshot()
    {
        var info = _audioCaptureService.GetCurrentRecordingInfo();
        bool hasSelection = OutputSources.Any(s => s.IsSelected) || InputSources.Any(s => s.IsSelected);
        return (info.State, hasSelection);
    }

    public void ToggleRecordingFromTray()
    {
        OnStartStopClicked(this, new RoutedEventArgs());
    }

    public void TogglePauseFromTray()
    {
        OnPauseResumeClicked(this, new RoutedEventArgs());
    }

    private string GetOutputPath()
    {
        // РСЃРїРѕР»СЊР·СѓРµРј СЃРѕС…СЂР°РЅС‘РЅРЅСѓСЋ РїР°РїРєСѓ РёР»Рё РїР°РїРєСѓ РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ
        var savedFolder = _settingsService.LoadOutputFolder();
        var outputFolder = !string.IsNullOrEmpty(savedFolder)
            ? savedFolder
            : GetDefaultOutputFolder();

        Directory.CreateDirectory(outputFolder);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(outputFolder, $"recording_{timestamp}.wav");
    }

    private static string GetDefaultOutputFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var contoraFolder = Path.Combine(documents, "Contora");
        var legacyFolder = Path.Combine(documents, "AudioRecorder");

        if (Directory.Exists(contoraFolder))
            return contoraFolder;
        if (Directory.Exists(legacyFolder))
            return legacyFolder;
        return contoraFolder;
    }

    private async Task ShowRecordingSavedDialogAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = FormatFileSize(new FileInfo(filePath).Length);

        var dialog = new ContentDialog
        {
            Title = "Запись сохранена",
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
                        Content = "Открыть папку",
                        Tag = filePath
                    }
                }
            },
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

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
}




