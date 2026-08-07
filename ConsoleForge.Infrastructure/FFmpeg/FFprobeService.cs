using System.Globalization;
using System.Text.Json;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.FFmpeg;

public sealed class FFprobeService : IProbeService
{
    private readonly FFmpegLocator _locator;

    public FFprobeService(FFmpegLocator locator) => _locator = locator;

    public async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Input file does not exist.", filePath);
        }

        string[] arguments =
        [
            "-v", "quiet",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            filePath
        ];

        var (exitCode, standardOutput, standardError) =
            await ProcessRunner.RunAsync(_locator.RequireFFprobe(), arguments, cancellationToken).ConfigureAwait(false);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new InvalidOperationException(
                $"ffprobe failed for '{Path.GetFileName(filePath)}' (exit {exitCode}). {standardError.Trim()}");
        }

        using var document = JsonDocument.Parse(standardOutput);
        var root = document.RootElement;

        var format = root.TryGetProperty("format", out var formatElement) ? formatElement : default;
        var streams = root.TryGetProperty("streams", out var streamsElement)
            ? streamsElement.EnumerateArray().ToList()
            : [];

        var videoStream = streams.FirstOrDefault(s => CodecType(s) == "video" && !IsAttachedPicture(s));
        var audioStream = streams.FirstOrDefault(s => CodecType(s) == "audio");

        var fileInfo = new FileInfo(filePath);

        var info = new MediaInfo
        {
            FilePath = filePath,
            FileSizeBytes = fileInfo.Length,
            ContainerFormat = ReadString(format, "format_name")?.Split(',').First() ?? "unknown",
            Duration = ReadDuration(format, videoStream),
            VideoCodec = ReadString(videoStream, "codec_name"),
            Resolution = ReadResolution(videoStream),
            FrameRate = ReadFrameRate(videoStream),
            PixelFormat = ReadString(videoStream, "pix_fmt"),
            AudioCodec = ReadString(audioStream, "codec_name"),
            AudioChannels = ReadInt(audioStream, "channels"),
            AudioSampleRate = ReadIntFromString(audioStream, "sample_rate")
        };

        if (!info.HasVideo)
        {
            throw new InvalidOperationException(
                $"'{Path.GetFileName(filePath)}' has no decodable video stream.");
        }

        return info;
    }

    private static string? CodecType(JsonElement stream) => ReadString(stream, "codec_type");

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.ValueKind == JsonValueKind.Object &&
        stream.TryGetProperty("disposition", out var disposition) &&
        disposition.TryGetProperty("attached_pic", out var attached) &&
        attached.ValueKind == JsonValueKind.Number &&
        attached.GetInt32() == 1;

    private static TimeSpan ReadDuration(JsonElement format, JsonElement videoStream)
    {
        var seconds = ReadDoubleFromString(format, "duration") ?? ReadDoubleFromString(videoStream, "duration");
        return seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : TimeSpan.Zero;
    }

    private static VideoResolution? ReadResolution(JsonElement stream)
    {
        var width = ReadInt(stream, "width");
        var height = ReadInt(stream, "height");
        return width is > 0 && height is > 0 ? new VideoResolution(width.Value, height.Value) : null;
    }

    private static double? ReadFrameRate(JsonElement stream)
    {
        var raw = ReadString(stream, "avg_frame_rate") ?? ReadString(stream, "r_frame_rate");
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var parts = raw.Split('/');
        if (parts.Length != 2) return null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)) return null;

        return denominator <= 0 ? null : numerator / denominator;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static int? ReadIntFromString(JsonElement element, string property) =>
        int.TryParse(ReadString(element, property), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double? ReadDoubleFromString(JsonElement element, string property) =>
        double.TryParse(ReadString(element, property), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
