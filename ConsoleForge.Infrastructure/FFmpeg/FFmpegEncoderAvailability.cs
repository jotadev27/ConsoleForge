using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.FFmpeg;

public sealed class FFmpegEncoderAvailability : IEncoderAvailability
{
    private readonly FFmpegLocator _locator;
    private IReadOnlyList<VideoEncoderKind>? _cached;

    public FFmpegEncoderAvailability(FFmpegLocator locator) => _locator = locator;

    public async Task<IReadOnlyList<VideoEncoderKind>> GetAvailableEncodersAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cached is not null) return _cached;

        var available = new List<VideoEncoderKind>();

        if (_locator.FFmpegPath is { } executable)
        {
            try
            {
                var (exitCode, standardOutput, _) = await ProcessRunner
                    .RunAsync(executable, ["-hide_banner", "-encoders"], cancellationToken)
                    .ConfigureAwait(false);

                if (exitCode == 0)
                {
                    if (standardOutput.Contains("libx264", StringComparison.Ordinal))
                    {
                        available.Add(VideoEncoderKind.Libx264);
                    }
                    if (standardOutput.Contains("h264_nvenc", StringComparison.Ordinal))
                    {
                        available.Add(VideoEncoderKind.NvencH264);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                available.Clear();
            }
        }

        if (available.Count == 0) available.Add(VideoEncoderKind.Libx264);

        _cached = available;
        return _cached;
    }
}
