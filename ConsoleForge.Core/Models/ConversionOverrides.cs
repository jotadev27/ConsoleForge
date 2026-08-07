using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Models;

public sealed record ConversionOverrides
{
    public VideoResolution? Resolution { get; init; }
    public int? VideoBitrateKbps { get; init; }
    public int? AudioBitrateKbps { get; init; }
    public int? AudioChannels { get; init; }
    public int? MaxFrameRate { get; init; }
    public bool AllowUpscale { get; init; }

    public static readonly ConversionOverrides None = new();

    public bool IsEmpty =>
        Resolution is null &&
        VideoBitrateKbps is null &&
        AudioBitrateKbps is null &&
        AudioChannels is null &&
        MaxFrameRate is null &&
        !AllowUpscale;
}
