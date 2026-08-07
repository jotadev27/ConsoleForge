using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Services;
using ConsoleForge.UI.Models;

namespace ConsoleForge.UI.ViewModels;

public sealed partial class QueueItemViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    [NotifyPropertyChangedFor(nameof(CanResume))]
    [NotifyPropertyChangedFor(nameof(ShowsPauseOrResume))]
    private QueueItemStatus _status = QueueItemStatus.Pending;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private string _sourceSummary = "reading...";

    [ObservableProperty]
    private string _targetSummary = "-";

    [ObservableProperty]
    private string _throughput = "-";

    [ObservableProperty]
    private string _remaining = "-";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomCover))]
    [NotifyPropertyChangedFor(nameof(CoverTooltip))]
    private string? _customCoverPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlaceholder))]
    private Bitmap? _coverImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlaceholder))]
    [NotifyPropertyChangedFor(nameof(CoverTooltip))]
    private bool _supportsCover;

    private MediaInfo? _mediaInfo;

    public QueueItemViewModel(string inputPath)
    {
        InputPath = inputPath;
        FileName = Path.GetFileName(inputPath);
        PreviewKey = Guid.NewGuid().ToString("N");
    }

    public string PreviewKey { get; }

    public bool PauseRequested { get; set; }

    public int CoverRevision { get; set; } = -1;

    public bool IsCoverLoading { get; set; }

    public bool HasCustomCover => !string.IsNullOrWhiteSpace(CustomCoverPath);

    public bool ShowsPlaceholder => CoverImage is null;

    public string CoverTooltip => !SupportsCover
        ? "This device does not use cover thumbnails"
        : HasCustomCover
            ? $"Custom cover: {CustomCoverPath}\nClick to replace"
            : "Auto frame at 10% of the video\nClick to choose a custom cover";

    public void SetCoverFromFile(string path)
    {
        var previous = CoverImage;

        try
        {
            using var stream = File.OpenRead(path);
            CoverImage = new Bitmap(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CoverImage = null;
        }

        previous?.Dispose();
    }

    public void ClearCover()
    {
        var previous = CoverImage;
        CoverImage = null;
        previous?.Dispose();
    }

    public void InvalidateCover()
    {
        CoverRevision = -1;
        ClearCover();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _outputBaseName;

    [ObservableProperty]
    private bool _isEditingName;

    [ObservableProperty]
    private string _nameEditText = string.Empty;

    public string InputPath { get; }

    public string FileName { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(OutputBaseName) ? FileName : OutputBaseName + ".mp4";

    public bool CanCancel => Status is QueueItemStatus.Converting or QueueItemStatus.Ready
        or QueueItemStatus.Pending or QueueItemStatus.Paused;

    public bool CanPause => Status is QueueItemStatus.Converting;

    public bool CanResume => Status is QueueItemStatus.Paused;

    public bool ShowsPauseOrResume => CanPause || CanResume;

    public void BeginNameEdit()
    {
        NameEditText = string.IsNullOrWhiteSpace(OutputBaseName)
            ? Path.GetFileNameWithoutExtension(FileName)
            : OutputBaseName;

        IsEditingName = true;
    }

    public void CancelNameEdit() => IsEditingName = false;

    public bool CommitNameEdit()
    {
        var candidate = NameEditText?.Trim() ?? string.Empty;

        if (!OutputPathResolver.IsValidFileName(candidate)) return false;

        OutputBaseName = candidate;
        IsEditingName = false;
        return true;
    }

    public MediaInfo? MediaInfo
    {
        get => _mediaInfo;
        set
        {
            _mediaInfo = value;
            SourceSummary = value?.SourceSummary ?? "unreadable";
            OnPropertyChanged(nameof(Duration));
        }
    }

    public string Duration => MediaInfo is { } info && info.Duration > TimeSpan.Zero
        ? FormatDuration(info.Duration)
        : "--:--:--";

    public bool IsActive => Status is QueueItemStatus.Converting or QueueItemStatus.Probing;

    public bool IsFinished => Status is QueueItemStatus.Completed or QueueItemStatus.Failed
        or QueueItemStatus.Cancelled or QueueItemStatus.Unsupported;

    public string StatusText => Status switch
    {
        QueueItemStatus.Pending => "PENDING",
        QueueItemStatus.Probing => "PROBING",
        QueueItemStatus.Ready => "READY",
        QueueItemStatus.Converting => "ENCODING",
        QueueItemStatus.Cancelling => "CANCELLING",
        QueueItemStatus.Paused => "PAUSED",
        QueueItemStatus.Completed => "DONE",
        QueueItemStatus.Failed => "FAILED",
        QueueItemStatus.Cancelled => "CANCELLED",
        QueueItemStatus.Unsupported => "UNSUPPORTED",
        _ => "?"
    };

    public void ApplyProgress(ConversionProgress progress)
    {
        Percent = progress.Percent;
        Throughput = progress.Speed > 0
            ? $"{progress.Speed:0.00}x / {progress.Fps:0}fps"
            : "-";
        Remaining = progress.Remaining is { } remaining ? FormatDuration(remaining) : "-";
    }

    public void Reset()
    {
        PauseRequested = false;
        Percent = 0;
        Throughput = "-";
        Remaining = "-";
        ErrorMessage = null;
        OutputPath = null;
        Status = MediaInfo is null ? QueueItemStatus.Pending : QueueItemStatus.Ready;
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"00:{value.Minutes:00}:{value.Seconds:00}";
}
