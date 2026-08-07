namespace ConsoleForge.Core.Profiles;

public sealed record DeviceProfile
{
    public required TargetDevice Device { get; init; }
    public required string DisplayName { get; init; }
    public required string ShortName { get; init; }
    public required string CompatibilityNote { get; init; }

    public required VideoResolution MaxResolution { get; init; }
    public required IReadOnlyList<VideoResolution> SelectableResolutions { get; init; }
    public required int DimensionAlignment { get; init; }

    public required string H264Profile { get; init; }
    public required string H264Level { get; init; }
    public required int MaxFrameRate { get; init; }

    public int? MaxBFrames { get; init; }
    public int? MaxReferenceFrames { get; init; }

    public VideoEncoderKind? PreferredEncoder { get; init; }
    public string? PreferredEncoderReason { get; init; }

    public required int DefaultVideoBitrateKbps { get; init; }
    public required int MinVideoBitrateKbps { get; init; }
    public required int MaxVideoBitrateKbps { get; init; }

    public required int DefaultAudioBitrateKbps { get; init; }
    public required int MinAudioBitrateKbps { get; init; }
    public required int MaxAudioBitrateKbps { get; init; }
    public required int MaxAudioChannels { get; init; }
    public required int AudioSampleRate { get; init; }

    public VideoResolution? ThumbnailResolution { get; init; }
    public string? ThumbnailExtension { get; init; }

    public bool GeneratesThumbnail => ThumbnailResolution is not null && ThumbnailExtension is not null;

    public required bool RequiresFastStart { get; init; }
    public required string OutputSubfolder { get; init; }
    public required int MaxFileNameLength { get; init; }
}
