using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public static class ConversionPlanner
{
    public static ConversionPlan Create(
        MediaInfo source,
        DeviceProfile profile,
        VideoEncoderKind encoder,
        string outputRoot,
        ConversionOverrides? overrides = null,
        string? customCoverPath = null,
        string? outputBaseName = null) =>
        Build(
            source, profile, encoder,
            OutputPathResolver.Resolve(source.FilePath, outputRoot, profile, outputBaseName),
            overrides, customCoverPath);

    public static ConversionPlan Preview(
        MediaInfo source,
        DeviceProfile profile,
        VideoEncoderKind encoder,
        ConversionOverrides? overrides = null) =>
        Build(source, profile, encoder, string.Empty, overrides, customCoverPath: null);

    private static ConversionPlan Build(
        MediaInfo source,
        DeviceProfile profile,
        VideoEncoderKind encoder,
        string outputPath,
        ConversionOverrides? overrides,
        string? customCoverPath)
    {
        overrides ??= ConversionOverrides.None;

        var bounds = overrides.Resolution ?? profile.MaxResolution;
        var sourceResolution = source.Resolution ?? bounds;

        var outputResolution = overrides.Resolution is { } forced && overrides.AllowUpscale
            ? ResolutionCalculator.Align(forced, forced, profile.DimensionAlignment)
            : ResolutionCalculator.Fit(
                sourceResolution, bounds, overrides.AllowUpscale, profile.DimensionAlignment);

        var frameRateCap = overrides.MaxFrameRate ?? profile.MaxFrameRate;
        var audioChannels = Math.Min(
            overrides.AudioChannels ?? profile.MaxAudioChannels,
            source.AudioChannels ?? profile.MaxAudioChannels);

        return new ConversionPlan
        {
            InputPath = source.FilePath,
            OutputPath = outputPath,
            ThumbnailPath = profile.GeneratesThumbnail && outputPath.Length > 0
                ? Path.ChangeExtension(outputPath, profile.ThumbnailExtension)
                : null,
            CustomCoverPath = customCoverPath,
            Profile = profile,
            Encoder = encoder,
            OutputResolution = outputResolution,
            VideoBitrateKbps = overrides.VideoBitrateKbps ?? BitrateCalculator.ForResolution(profile, outputResolution),
            AudioBitrateKbps = overrides.AudioBitrateKbps ?? profile.DefaultAudioBitrateKbps,
            AudioChannels = Math.Max(1, audioChannels),
            MaxFrameRate = frameRateCap,
            ScalingRequired = outputResolution != sourceResolution,
            FrameRateCapRequired = source.FrameRate is { } fps && fps > frameRateCap + 0.01,
            SourceDuration = source.Duration
        };
    }
}
