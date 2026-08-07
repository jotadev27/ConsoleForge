using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.Storage;
using ConsoleForge.UI.Models;
using ConsoleForge.UI.Services;

namespace ConsoleForge.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxLogLines = 4000;

    private readonly IProbeService _probeService;
    private readonly IVideoConverter _converter;
    private readonly IEncoderAvailability _encoderAvailability;
    private readonly IConfigStore _configStore;
    private readonly IDialogService _dialogService;
    private readonly IThumbnailService _thumbnailService;
    private readonly AppConfig _config;
    private readonly string _previewDirectory;

    private readonly HashSet<QueueItemViewModel> _realizedItems = [];

    private CancellationTokenSource? _runCancellation;
    private CancellationTokenSource? _activeItemCancellation;
    private QueueItemViewModel? _activeItem;
    private bool _applyingEncoder;
    private int _coverRevision;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileHeadline))]
    [NotifyPropertyChangedFor(nameof(EncoderNote))]
    [NotifyPropertyChangedFor(nameof(HasEncoderNote))]
    private DeviceProfile _selectedProfile = DeviceProfileCatalog.Psp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedEncoderOrNull))]
    private VideoEncoderKind _selectedEncoder = VideoEncoderKind.Libx264;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEncoderNote))]
    private bool _encoderIsUserChoice;

    [ObservableProperty]
    private string _outputRoot = string.Empty;

    [ObservableProperty]
    private bool _advancedMode;

    [ObservableProperty]
    private ResolutionOption? _selectedResolutionOption;

    [ObservableProperty]
    private int _videoBitrateKbps;

    [ObservableProperty]
    private int _audioBitrateKbps;

    [ObservableProperty]
    private int _audioChannels;

    [ObservableProperty]
    private int _maxFrameRate;

    [ObservableProperty]
    private bool _allowUpscale;

    [ObservableProperty]
    private QueueItemViewModel? _selectedItem;

    [ObservableProperty]
    private double _overallPercent;

    [ObservableProperty]
    private string _statusMessage = "Idle.";

    [ObservableProperty]
    private string _toolchainStatus = string.Empty;

    [ObservableProperty]
    private bool _toolchainReady;

    [ObservableProperty]
    private bool _isRunning;

    public MainWindowViewModel(
        IProbeService probeService,
        IVideoConverter converter,
        IEncoderAvailability encoderAvailability,
        IConfigStore configStore,
        IDialogService dialogService,
        IThumbnailService thumbnailService,
        bool toolchainReady,
        string toolchainStatus)
    {
        _probeService = probeService;
        _converter = converter;
        _encoderAvailability = encoderAvailability;
        _configStore = configStore;
        _dialogService = dialogService;
        _thumbnailService = thumbnailService;
        _config = configStore.Load();
        _previewDirectory = Path.Combine(Path.GetTempPath(), "consoleforge-previews");

        ToolchainReady = toolchainReady;
        ToolchainStatus = toolchainStatus;

        Profiles = new ObservableCollection<DeviceProfile>(DeviceProfileCatalog.All);
        AvailableEncoders = new ObservableCollection<VideoEncoderKind> { VideoEncoderKind.Libx264 };
        AudioChannelOptions = new ObservableCollection<int> { 1, 2, 6 };
        FrameRateOptions = new ObservableCollection<int> { 24, 25, 30, 50, 60 };
        VideoBitrateOptions = [];
        AudioBitrateOptions = [];
        ResolutionOptions = [];
        Queue = [];
        LogEntries = [];

        Queue.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(QueueSummary));
            StartCommand.NotifyCanExecuteChanged();
            ClearQueueCommand.NotifyCanExecuteChanged();
        };

        OutputRoot = _config.OutputRoot ?? DefaultOutputRoot();
        AdvancedMode = _config.AdvancedMode;
        SelectedProfile = DeviceProfileCatalog.For(_config.LastDevice);

        RebuildResolutionOptions(SelectedProfile);
        ApplyProfileDefaults(SelectedProfile);
        ApplyEncoderForProfile();

        AppendLog($"ConsoleForge ready. {toolchainStatus}");
    }

    public ObservableCollection<DeviceProfile> Profiles { get; }

    public ObservableCollection<VideoEncoderKind> AvailableEncoders { get; }

    public ObservableCollection<int> AudioChannelOptions { get; }

    public ObservableCollection<int> FrameRateOptions { get; }

    public ObservableCollection<int> VideoBitrateOptions { get; }

    public ObservableCollection<int> AudioBitrateOptions { get; }

    public ObservableCollection<ResolutionOption> ResolutionOptions { get; }

    public ObservableCollection<QueueItemViewModel> Queue { get; }

    public ObservableCollection<string> LogEntries { get; }

    public string ProfileHeadline =>
        $"{SelectedProfile.MaxResolution} max | H.264 {SelectedProfile.H264Profile} L{SelectedProfile.H264Level} | " +
        $"{SelectedProfile.DefaultVideoBitrateKbps / 1000.0:0.#} Mbps | {SelectedProfile.CompatibilityNote}";

    public VideoEncoderKind? SelectedEncoderOrNull
    {
        get => SelectedEncoder;
        set
        {
            if (value is { } encoder) SelectedEncoder = encoder;
            else OnPropertyChanged();
        }
    }

    public string? EncoderNote => SelectedProfile.PreferredEncoderReason;

    public bool HasEncoderNote => !EncoderIsUserChoice && !string.IsNullOrWhiteSpace(EncoderNote);

    public string QueueSummary
    {
        get
        {
            if (Queue.Count == 0) return "Queue empty. Add files or a folder to begin.";
            var done = Queue.Count(item => item.Status == QueueItemStatus.Completed);
            var failed = Queue.Count(item => item.Status == QueueItemStatus.Failed);
            var text = $"{Queue.Count} file(s) | {done} done";
            return failed > 0 ? $"{text} | {failed} failed" : text;
        }
    }

    public async Task InitializeAsync()
    {
        var encoders = await _encoderAvailability.GetAvailableEncodersAsync().ConfigureAwait(true);

        foreach (var encoder in encoders.Where(encoder => !AvailableEncoders.Contains(encoder)))
        {
            AvailableEncoders.Add(encoder);
        }

        foreach (var stale in AvailableEncoders.Where(encoder => !encoders.Contains(encoder)).ToList())
        {
            AvailableEncoders.Remove(stale);
        }

        ApplyEncoderForProfile();

        AppendLog(AvailableEncoders.Contains(VideoEncoderKind.NvencH264)
            ? "NVENC detected: hardware encoding available."
            : "NVENC not available: falling back to libx264 (CPU).");
    }

    partial void OnSelectedProfileChanged(DeviceProfile value)
    {
        RebuildResolutionOptions(value);
        ApplyProfileDefaults(value);
        ApplyEncoderForProfile();
        _config.LastDevice = value.Device;
        PersistConfig();
        RefreshTargetSummaries();

        InvalidateCovers();
        _ = RefreshRealizedCoversAsync();
    }

    partial void OnSelectedEncoderChanged(VideoEncoderKind value)
    {
        if (_applyingEncoder) return;

        _config.SetEncoder(SelectedProfile.Device, value);
        PersistConfig();
        EncoderIsUserChoice = true;

        AppendLog($"{SelectedProfile.ShortName}: encoder set to {value} and remembered for this device.");
    }

    private void ApplyEncoderForProfile()
    {
        var resolution = EncoderSelector.Resolve(
            SelectedProfile, AvailableEncoders, _config.GetEncoder(SelectedProfile.Device));

        var previous = SelectedEncoder;

        _applyingEncoder = true;
        SelectedEncoder = resolution.Encoder;
        _applyingEncoder = false;

        EncoderIsUserChoice = resolution.IsUserChoice;

        if (resolution.Encoder == previous) return;

        if (resolution.IsUserChoice)
        {
            AppendLog($"{SelectedProfile.ShortName}: encoder restored to {resolution.Encoder} (saved preference).");
        }
        else if (SelectedProfile.PreferredEncoder == resolution.Encoder)
        {
            AppendLog($"{SelectedProfile.ShortName}: encoder set to {resolution.Encoder}. " +
                      SelectedProfile.PreferredEncoderReason);
        }
    }

    partial void OnAdvancedModeChanged(bool value)
    {
        _config.AdvancedMode = value;
        PersistConfig();
        if (!value) ApplyProfileDefaults(SelectedProfile);
        RefreshTargetSummaries();
    }

    partial void OnOutputRootChanged(string value)
    {
        _config.OutputRoot = value;
        PersistConfig();
    }

    partial void OnSelectedResolutionOptionChanged(ResolutionOption? value) => RefreshTargetSummaries();

    partial void OnVideoBitrateKbpsChanged(int value) => RefreshTargetSummaries();

    partial void OnAudioBitrateKbpsChanged(int value) => RefreshTargetSummaries();

    partial void OnAudioChannelsChanged(int value) => RefreshTargetSummaries();

    partial void OnMaxFrameRateChanged(int value) => RefreshTargetSummaries();

    partial void OnAllowUpscaleChanged(bool value) => RefreshTargetSummaries();

    private void RebuildResolutionOptions(DeviceProfile profile)
    {
        var previous = SelectedResolutionOption?.Resolution;

        ResolutionOptions.Clear();
        ResolutionOptions.Add(new ResolutionOption($"Auto (fit {profile.MaxResolution})", null));
        foreach (var resolution in profile.SelectableResolutions)
        {
            var suffix = resolution.PixelCount > profile.MaxResolution.PixelCount ? "  [homebrew player]" : string.Empty;
            ResolutionOptions.Add(new ResolutionOption(resolution + suffix, resolution));
        }

        SelectedResolutionOption =
            ResolutionOptions.FirstOrDefault(option => option.Resolution == previous) ?? ResolutionOptions[0];
    }

    private void RebuildBitrateOptions(DeviceProfile profile)
    {
        VideoBitrateOptions.Clear();
        foreach (var value in BitrateOptions.ForVideo(profile)) VideoBitrateOptions.Add(value);

        AudioBitrateOptions.Clear();
        foreach (var value in BitrateOptions.ForAudio(profile)) AudioBitrateOptions.Add(value);
    }

    private void ApplyProfileDefaults(DeviceProfile profile)
    {
        RebuildBitrateOptions(profile);
        VideoBitrateKbps = profile.DefaultVideoBitrateKbps;
        AudioBitrateKbps = profile.DefaultAudioBitrateKbps;
        AudioChannels = profile.MaxAudioChannels;
        MaxFrameRate = profile.MaxFrameRate;
        AllowUpscale = false;
        if (ResolutionOptions.Count > 0) SelectedResolutionOption = ResolutionOptions[0];
    }

    private ConversionOverrides BuildOverrides()
    {
        if (!AdvancedMode) return ConversionOverrides.None;

        return new ConversionOverrides
        {
            Resolution = SelectedResolutionOption?.Resolution,
            VideoBitrateKbps = VideoBitrateKbps > 0 ? VideoBitrateKbps : null,
            AudioBitrateKbps = AudioBitrateKbps > 0 ? AudioBitrateKbps : null,
            AudioChannels = AudioChannels > 0 ? AudioChannels : null,
            MaxFrameRate = MaxFrameRate > 0 ? MaxFrameRate : null,
            AllowUpscale = AllowUpscale
        };
    }

    private void RefreshTargetSummaries()
    {
        var overrides = BuildOverrides();
        foreach (var item in Queue)
        {
            if (item.MediaInfo is not { } info)
            {
                item.TargetSummary = "-";
                continue;
            }

            var plan = ConversionPlanner.Preview(info, SelectedProfile, SelectedEncoder, overrides);
            item.TargetSummary = plan.PlanSummary;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private async Task AddFilesAsync()
    {
        var files = await _dialogService.PickVideoFilesAsync(_config.LastInputFolder).ConfigureAwait(true);
        if (files.Count == 0) return;

        _config.LastInputFolder = Path.GetDirectoryName(files[0]);
        PersistConfig();
        await EnqueueAsync(files).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogService.PickFolderAsync(_config.LastInputFolder).ConfigureAwait(true);
        if (folder is null) return;

        _config.LastInputFolder = folder;
        PersistConfig();

        var files = MediaFileScanner.Scan(folder);
        if (files.Count == 0)
        {
            StatusMessage = "No video files found in that folder.";
            AppendLog($"No video files found under {folder}");
            return;
        }

        await EnqueueAsync(files).ConfigureAwait(true);
    }

    public async Task EnqueueAsync(IEnumerable<string> paths)
    {
        var existing = Queue.Select(item => item.InputPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<QueueItemViewModel>();

        foreach (var path in paths)
        {
            if (!File.Exists(path) || !existing.Add(path)) continue;
            var item = new QueueItemViewModel(path);
            Queue.Add(item);
            added.Add(item);
        }

        if (added.Count == 0) return;

        StatusMessage = $"Probing {added.Count} file(s)...";
        foreach (var item in added)
        {
            await ProbeItemAsync(item).ConfigureAwait(true);
        }

        foreach (var item in added)
        {
            item.SupportsCover = SelectedProfile.GeneratesThumbnail;
        }

        StatusMessage = $"Added {added.Count} file(s).";
        OnPropertyChanged(nameof(QueueSummary));
    }

    private async Task ProbeItemAsync(QueueItemViewModel item)
    {
        item.Status = QueueItemStatus.Probing;
        try
        {
            var info = await _probeService.ProbeAsync(item.InputPath).ConfigureAwait(true);
            item.MediaInfo = info;
            item.Status = QueueItemStatus.Ready;

            var plan = ConversionPlanner.Preview(info, SelectedProfile, SelectedEncoder, BuildOverrides());
            item.TargetSummary = plan.PlanSummary;
        }
        catch (Exception exception)
        {
            item.Status = QueueItemStatus.Unsupported;
            item.ErrorMessage = exception.Message;
            item.SourceSummary = "unreadable";
            AppendLog($"[probe] {item.FileName}: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task PickCoverAsync(QueueItemViewModel? item)
    {
        if (item is null || !item.SupportsCover) return;

        var path = await _dialogService.PickImageFileAsync(_config.LastInputFolder).ConfigureAwait(true);
        if (path is null) return;

        item.CustomCoverPath = path;
        AppendLog($"[{item.FileName}] custom cover selected: {path}");

        await EnsureCoverAsync(item, force: true).ConfigureAwait(true);
    }

    public void NotifyRowRealized(QueueItemViewModel item)
    {
        if (!_realizedItems.Add(item)) return;

        _ = EnsureCoverAsync(item, force: false);
    }

    public void NotifyRowUnrealized(QueueItemViewModel item) => _realizedItems.Remove(item);

    public async Task RefreshRealizedCoversAsync()
    {
        foreach (var item in Queue.Where(_realizedItems.Contains).ToList())
        {
            await EnsureCoverAsync(item, force: false).ConfigureAwait(true);
        }
    }

    private void InvalidateCovers()
    {
        _coverRevision++;

        foreach (var item in Queue)
        {
            item.SupportsCover = SelectedProfile.GeneratesThumbnail;
            item.InvalidateCover();
        }
    }

    private async Task EnsureCoverAsync(QueueItemViewModel item, bool force)
    {
        item.SupportsCover = SelectedProfile.GeneratesThumbnail;

        if (!item.SupportsCover || item.MediaInfo is null || SelectedProfile.ThumbnailResolution is not { } size)
        {
            item.ClearCover();
            item.CoverRevision = _coverRevision;
            return;
        }

        if (item.IsCoverLoading) return;
        if (!force && item.CoverRevision == _coverRevision) return;

        item.IsCoverLoading = true;
        var previewPath = Path.Combine(_previewDirectory, $"{item.PreviewKey}-{Guid.NewGuid():N}.jpg");

        try
        {
            await _thumbnailService.CreateAsync(new ThumbnailRequest
            {
                OutputPath = previewPath,
                Size = size,
                VideoPath = item.InputPath,
                FramePosition = ThumbnailRequest.DefaultFramePosition(item.MediaInfo.Duration),
                CustomImagePath = item.CustomCoverPath
            }).ConfigureAwait(true);

            item.SetCoverFromFile(previewPath);
        }
        catch (Exception exception)
        {
            item.ClearCover();
            AppendLog($"[{item.FileName}] cover preview failed: {exception.Message}");
        }
        finally
        {
            item.CoverRevision = _coverRevision;
            item.IsCoverLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelItemAsync(QueueItemViewModel? item)
    {
        if (item is null || !item.CanCancel) return;

        var isEncoding = item.Status == QueueItemStatus.Converting;

        var confirmed = await _dialogService.ConfirmAsync(
            isEncoding ? "Cancel conversion" : "Remove from queue",
            isEncoding
                ? $"\"{item.DisplayName}\" is currently encoding.\n\n"
                  + "Are you sure you want to cancel this conversion? The partial output file will be deleted."
                : $"Remove \"{item.DisplayName}\" from the queue?").ConfigureAwait(true);

        if (!confirmed) return;

        if (isEncoding && ReferenceEquals(_activeItem, item))
        {
            item.Status = QueueItemStatus.Cancelling;
            item.PauseRequested = false;
            _activeItemCancellation?.Cancel();
            return;
        }

        Queue.Remove(item);
        NotifyRowUnrealized(item);
        AppendLog($"[{item.DisplayName}] removed from the queue.");
        OnPropertyChanged(nameof(QueueSummary));
    }

    [RelayCommand]
    private void PauseItem(QueueItemViewModel? item)
    {
        if (item is null || !item.CanPause || !ReferenceEquals(_activeItem, item)) return;

        item.PauseRequested = true;
        item.Status = QueueItemStatus.Cancelling;
        _activeItemCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task ResumeItemAsync(QueueItemViewModel? item)
    {
        if (item is null || !item.CanResume) return;

        item.PauseRequested = false;
        item.Reset();
        AppendLog($"[{item.DisplayName}] resumed; encoding restarts from the beginning.");

        StartCommand.NotifyCanExecuteChanged();

        if (!IsRunning) await StartAsync().ConfigureAwait(true);
    }

    public async Task<bool> ConfirmExitAsync()
    {
        var encoding = Queue.Any(item =>
            item.Status is QueueItemStatus.Converting or QueueItemStatus.Cancelling);

        if (!encoding) return true;

        var confirmed = await _dialogService.ConfirmAsync(
            "Exit ConsoleForge",
            "A conversion is currently running.\n\n"
            + "Are you sure you want to exit? The running encode will be stopped and its partial "
            + "output file deleted.").ConfigureAwait(true);

        if (!confirmed) return false;

        _runCancellation?.Cancel();
        _activeItemCancellation?.Cancel();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private void RemoveSelected()
    {
        if (SelectedItem is null) return;
        Queue.Remove(SelectedItem);
        SelectedItem = null;
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private void ClearQueue()
    {
        Queue.Clear();
        SelectedItem = null;
        OverallPercent = 0;
        StatusMessage = "Queue cleared.";
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private void ClearCompleted()
    {
        foreach (var item in Queue.Where(item => item.Status == QueueItemStatus.Completed).ToList())
        {
            Queue.Remove(item);
        }
        OnPropertyChanged(nameof(QueueSummary));
    }

    private bool CanEditQueue() => !IsRunning;

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var folder = await _dialogService.PickFolderAsync(OutputRoot).ConfigureAwait(true);
        if (folder is not null) OutputRoot = folder;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var pending = Queue
            .Where(item => item.MediaInfo is not null && item.Status != QueueItemStatus.Completed)
            .ToList();

        if (pending.Count == 0)
        {
            StatusMessage = "Nothing to convert.";
            return;
        }

        foreach (var item in pending.Where(item => item.Status != QueueItemStatus.Paused)) item.Reset();
        pending = pending.Where(item => item.Status != QueueItemStatus.Paused).ToList();

        if (pending.Count == 0)
        {
            StatusMessage = "Every remaining file is paused.";
            return;
        }

        _runCancellation = new CancellationTokenSource();
        var token = _runCancellation.Token;
        IsRunning = true;
        NotifyRunStateChanged();

        var overrides = BuildOverrides();
        var completed = 0;
        var failed = 0;

        AppendLog($"=== Run started: {pending.Count} file(s) -> {SelectedProfile.ShortName} " +
                  $"({SelectedEncoder}) into {OutputRoot} ===");

        try
        {
            for (var index = 0; index < pending.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var item = pending[index];
                if (item.Status is QueueItemStatus.Paused or QueueItemStatus.Cancelled) continue;

                var baseline = (double)index / pending.Count * 100.0;
                var slice = 100.0 / pending.Count;

                item.Status = QueueItemStatus.Converting;
                item.Percent = 0;
                StatusMessage = $"[{index + 1}/{pending.Count}] {item.DisplayName}";

                var progress = new Progress<ConversionProgress>(report =>
                {
                    item.ApplyProgress(report);
                    OverallPercent = baseline + report.Percent / 100.0 * slice;
                });

                using var itemCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                _activeItem = item;
                _activeItemCancellation = itemCancellation;

                try
                {
                    var plan = ConversionPlanner.Create(
                        item.MediaInfo!, SelectedProfile, SelectedEncoder, OutputRoot, overrides,
                        item.CustomCoverPath, item.OutputBaseName);

                    item.OutputPath = plan.OutputPath;
                    AppendLog($"[{item.DisplayName}] -> {plan.OutputPath}");

                    await _converter
                        .ConvertAsync(plan, progress, LogFromWorker, itemCancellation.Token)
                        .ConfigureAwait(true);

                    await GenerateThumbnailAsync(plan, item, itemCancellation.Token).ConfigureAwait(true);

                    item.Status = QueueItemStatus.Completed;
                    item.Percent = 100;
                    item.Remaining = "-";
                    completed++;
                    AppendLog($"[{item.DisplayName}] completed.");
                }
                catch (Exception exception)
                {
                    token.ThrowIfCancellationRequested();

                    if (itemCancellation.IsCancellationRequested)
                    {
                        item.Status = item.PauseRequested ? QueueItemStatus.Paused : QueueItemStatus.Cancelled;
                        item.PauseRequested = false;
                        item.Percent = 0;
                        item.Throughput = "-";
                        item.Remaining = "-";
                        item.ErrorMessage = null;

                        AppendLog(item.Status == QueueItemStatus.Paused
                            ? $"[{item.DisplayName}] paused; resuming re-encodes from the start."
                            : $"[{item.DisplayName}] cancelled.");
                    }
                    else
                    {
                        item.Status = QueueItemStatus.Failed;
                        item.ErrorMessage = exception.Message;
                        failed++;
                        AppendLog($"[{item.DisplayName}] FAILED: {exception.Message}");
                    }
                }
                finally
                {
                    _activeItem = null;
                    _activeItemCancellation = null;
                }

                OverallPercent = baseline + slice;
                OnPropertyChanged(nameof(QueueSummary));
            }

            StatusMessage = failed == 0
                ? $"Finished: {completed} converted."
                : $"Finished: {completed} converted, {failed} failed.";
        }
        catch (OperationCanceledException)
        {
            foreach (var item in pending.Where(item => item.Status == QueueItemStatus.Converting))
            {
                item.Status = QueueItemStatus.Cancelled;
            }
            StatusMessage = $"Cancelled after {completed} file(s).";
            AppendLog("=== Run cancelled ===");
        }
        finally
        {
            _runCancellation?.Dispose();
            _runCancellation = null;
            IsRunning = false;
            NotifyRunStateChanged();
            OnPropertyChanged(nameof(QueueSummary));
            AppendLog($"=== Run finished: {completed} ok, {failed} failed ===");
        }
    }

    private async Task GenerateThumbnailAsync(
        ConversionPlan plan, QueueItemViewModel item, CancellationToken cancellationToken)
    {
        if (ThumbnailRequest.ForPlan(plan) is not { } request) return;

        try
        {
            await _thumbnailService.CreateAsync(request, cancellationToken).ConfigureAwait(true);

            AppendLog(request.UsesCustomImage
                ? $"[{item.FileName}] thumbnail written from custom cover -> {request.OutputPath}"
                : $"[{item.FileName}] thumbnail written from auto frame -> {request.OutputPath}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppendLog($"[{item.FileName}] thumbnail generation failed: {exception.Message}");
        }
    }

    private bool CanStart() => !IsRunning && ToolchainReady && Queue.Count > 0;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _runCancellation?.Cancel();
        StatusMessage = "Cancelling...";
    }

    private bool CanCancel() => IsRunning;

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        var suggested = $"consoleforge_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var path = await _dialogService.SaveLogFileAsync(suggested).ConfigureAwait(true);
        if (path is null) return;

        try
        {
            await File.WriteAllLinesAsync(path, LogEntries).ConfigureAwait(true);
            StatusMessage = $"Log exported to {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not export log: {exception.Message}";
        }
    }

    [RelayCommand]
    private void ClearLog() => LogEntries.Clear();

    private void LogFromWorker(string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AppendLog(message);
            return;
        }

        Dispatcher.UIThread.Post(() => AppendLog(message));
    }

    public void AppendLog(string message)
    {
        var stamped = $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}  {message}";
        LogEntries.Add(stamped);
        while (LogEntries.Count > MaxLogLines) LogEntries.RemoveAt(0);
    }

    private void NotifyRunStateChanged()
    {
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        ClearQueueCommand.NotifyCanExecuteChanged();
        ClearCompletedCommand.NotifyCanExecuteChanged();
    }

    private void PersistConfig() => _configStore.Save(_config);

    private static string DefaultOutputRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ConsoleForge");
}
