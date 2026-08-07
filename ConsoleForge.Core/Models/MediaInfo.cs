using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Models;

public sealed record MediaInfo
{
    public required string FilePath { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ContainerFormat { get; init; }
    public required TimeSpan Duration { get; init; }

    public string? VideoCodec { get; init; }
    public VideoResolution? Resolution { get; init; }
    public double? FrameRate { get; init; }
    public string? PixelFormat { get; init; }

    public string? AudioCodec { get; init; }
    public int? AudioChannels { get; init; }
    public int? AudioSampleRate { get; init; }

    public bool HasVideo => VideoCodec is not null && Resolution is not null;
    public bool HasAudio => AudioCodec is not null;

    public string SourceSummary
    {
        get
        {
            var parts = new List<string> { ContainerFormat.ToUpperInvariant() };
            if (HasVideo)
            {
                parts.Add(VideoCodec!);
                parts.Add(Resolution!.Value.ToString());
                if (FrameRate is > 0) parts.Add($"{FrameRate.Value:0.##}fps");
            }
            if (HasAudio)
            {
                parts.Add($"{AudioChannels ?? 0}ch");
            }
            return string.Join(' ', parts);
        }
    }
}
