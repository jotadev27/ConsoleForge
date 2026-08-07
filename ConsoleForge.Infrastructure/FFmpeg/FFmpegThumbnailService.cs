using System.Globalization;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.FFmpeg;

public sealed class FFmpegThumbnailService : IThumbnailService
{
    private readonly FFmpegLocator _locator;

    public FFmpegThumbnailService(FFmpegLocator locator) => _locator = locator;

    public async Task CreateAsync(ThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var arguments = request.UsesCustomImage
            ? BuildFromImage(request)
            : BuildFromVideo(request);

        var (exitCode, _, standardError) = await ProcessRunner
            .RunAsync(_locator.RequireFFmpeg(), arguments, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0 || !File.Exists(request.OutputPath))
        {
            throw new InvalidOperationException(
                $"Thumbnail generation failed (exit {exitCode}). {standardError.Trim()}");
        }
    }

    private static IReadOnlyList<string> BuildFromVideo(ThumbnailRequest request) =>
    [
        "-hide_banner", "-nostdin", "-y",
        "-ss", request.FramePosition.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", request.VideoPath,
        "-frames:v", "1",
        "-vf", LetterboxFilter(request.Size),
        "-f", "mjpeg",
        "-q:v", "3",
        request.OutputPath
    ];

    private static IReadOnlyList<string> BuildFromImage(ThumbnailRequest request) =>
    [
        "-hide_banner", "-nostdin", "-y",
        "-i", request.CustomImagePath!,
        "-frames:v", "1",
        "-vf", LetterboxFilter(request.Size),
        "-f", "mjpeg",
        "-q:v", "3",
        request.OutputPath
    ];

    public static string LetterboxFilter(VideoResolution size)
    {
        var width = size.Width.ToString(CultureInfo.InvariantCulture);
        var height = size.Height.ToString(CultureInfo.InvariantCulture);

        return $"scale={width}:{height}:force_original_aspect_ratio=decrease,"
             + $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black,setsar=1";
    }
}
