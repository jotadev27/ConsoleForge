using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public interface IThumbnailService
{
    Task CreateAsync(ThumbnailRequest request, CancellationToken cancellationToken = default);
}

public sealed record ThumbnailRequest
{
    public required string OutputPath { get; init; }
    public required VideoResolution Size { get; init; }
    public required string VideoPath { get; init; }
    public required TimeSpan FramePosition { get; init; }
    public string? CustomImagePath { get; init; }

    public bool UsesCustomImage => !string.IsNullOrWhiteSpace(CustomImagePath);

    public static ThumbnailRequest? ForPlan(ConversionPlan plan) =>
        plan.ThumbnailPath is { } thumbnailPath && plan.Profile.ThumbnailResolution is { } size
            ? new ThumbnailRequest
            {
                OutputPath = thumbnailPath,
                Size = size,
                VideoPath = plan.InputPath,
                FramePosition = DefaultFramePosition(plan.SourceDuration),
                CustomImagePath = plan.CustomCoverPath
            }
            : null;

    public static TimeSpan DefaultFramePosition(TimeSpan duration) =>
        duration > TimeSpan.Zero ? duration * 0.10 : TimeSpan.Zero;
}
