using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Models;

public sealed record ConversionPlan
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required DeviceProfile Profile { get; init; }
    public required VideoEncoderKind Encoder { get; init; }

    public required VideoResolution OutputResolution { get; init; }
    public required int VideoBitrateKbps { get; init; }
    public required int AudioBitrateKbps { get; init; }
    public required int AudioChannels { get; init; }
    public required int MaxFrameRate { get; init; }

    public string? ThumbnailPath { get; init; }
    public string? CustomCoverPath { get; init; }

    public required bool ScalingRequired { get; init; }
    public required bool FrameRateCapRequired { get; init; }
    public required TimeSpan SourceDuration { get; init; }

    public long EstimatedOutputBytes =>
        (long)(SourceDuration.TotalSeconds * (VideoBitrateKbps + AudioBitrateKbps) * 1000 / 8);

    public string PlanSummary =>
        $"{OutputResolution} {VideoBitrateKbps / 1000.0:0.0}Mbps {MaxFrameRate}fps AAC{AudioBitrateKbps}k";
}
