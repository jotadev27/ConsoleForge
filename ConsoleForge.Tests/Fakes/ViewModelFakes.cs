using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.UI.Services;

namespace ConsoleForge.Tests.Fakes;

public sealed class FakeProbeService : IProbeService
{
    public Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MediaInfo
        {
            FilePath = filePath,
            FileSizeBytes = 1024,
            ContainerFormat = "matroska",
            Duration = TimeSpan.FromMinutes(2),
            VideoCodec = "h264",
            Resolution = new VideoResolution(1920, 1080),
            FrameRate = 30,
            AudioCodec = "aac",
            AudioChannels = 2
        });
}

public sealed class RecordingConverter : IVideoConverter
{
    public List<ConversionPlan> Plans { get; } = [];

    public List<string> KilledOutputs { get; } = [];

    public TaskCompletionSource? BlockUntil { get; set; }

    public Exception? ThrowOnCancel { get; set; }

    public Exception? ThrowAlways { get; set; }

    public ConversionPlan? InFlight { get; private set; }

    public async Task ConvertAsync(
        ConversionPlan plan,
        IProgress<ConversionProgress>? progress,
        Action<string>? log,
        CancellationToken cancellationToken = default)
    {
        Plans.Add(plan);
        InFlight = plan;

        var directory = Path.GetDirectoryName(plan.OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(plan.OutputPath, "stub");

        try
        {
            if (ThrowAlways is { } always) throw always;

            if (BlockUntil is { } gate)
            {
                await using var registration = cancellationToken.Register(() => gate.TrySetCanceled());
                await gate.Task.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            KilledOutputs.Add(plan.OutputPath);
            if (File.Exists(plan.OutputPath)) File.Delete(plan.OutputPath);

            if (ThrowOnCancel is { } disguised) throw disguised;
            throw;
        }
        finally
        {
            InFlight = null;
        }
    }
}

public sealed class RecordingThumbnailService : IThumbnailService
{
    public List<ThumbnailRequest> Requests { get; } = [];

    public Task CreateAsync(ThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var directory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(request.OutputPath, MinimalJpeg);

        return Task.CompletedTask;
    }

    public IReadOnlyList<ThumbnailRequest> RequestsFor(string videoPath) =>
        Requests.Where(request => request.VideoPath == videoPath).ToList();

    private static readonly byte[] MinimalJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x43, 0x00,
        .. Enumerable.Repeat((byte)0x01, 64),
        0xFF, 0xD9
    ];
}

public sealed class StubEncoderAvailability : IEncoderAvailability
{
    public Task<IReadOnlyList<VideoEncoderKind>> GetAvailableEncodersAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VideoEncoderKind>>(
            [VideoEncoderKind.Libx264, VideoEncoderKind.NvencH264]);
}

public sealed class InMemoryConfigStore : IConfigStore
{
    private AppConfig _config = new();

    public AppConfig Load() => _config;

    public void Save(AppConfig config) => _config = config;
}

public sealed class ScriptedDialogService : IDialogService
{
    public Queue<string> ImagesToReturn { get; } = new();

    public int ImagePickerCalls { get; private set; }

    public Task<IReadOnlyList<string>> PickVideoFilesAsync(string? startFolder) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string?> PickFolderAsync(string? startFolder) => Task.FromResult<string?>(null);

    public Task<string?> PickImageFileAsync(string? startFolder)
    {
        ImagePickerCalls++;
        return Task.FromResult(ImagesToReturn.Count > 0 ? ImagesToReturn.Dequeue() : null);
    }

    public Task<string?> SaveLogFileAsync(string suggestedName) => Task.FromResult<string?>(null);

    public Queue<bool> ConfirmAnswers { get; } = new();

    public List<string> ConfirmPrompts { get; } = [];

    public bool DefaultConfirmAnswer { get; set; }

    public Task<bool> ConfirmAsync(string heading, string message)
    {
        ConfirmPrompts.Add($"{heading}|{message}");
        return Task.FromResult(ConfirmAnswers.Count > 0 ? ConfirmAnswers.Dequeue() : DefaultConfirmAnswer);
    }
}
