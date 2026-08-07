using System.Globalization;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.FFmpeg;

public static class FFmpegArgumentBuilder
{
    public static IReadOnlyList<string> Build(ConversionPlan plan)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i", plan.InputPath,
            "-map", "0:v:0",
            "-map", "0:a:0?",
            "-sn",
            "-dn",
            "-map_metadata", "-1"
        };

        arguments.AddRange(VideoArguments(plan));
        arguments.AddRange(FilterArguments(plan));
        arguments.AddRange(AudioArguments(plan));

        if (plan.Profile.RequiresFastStart)
        {
            arguments.AddRange(["-movflags", "+faststart"]);
        }

        arguments.AddRange(["-f", "mp4", "-progress", "pipe:1", "-nostats", plan.OutputPath]);

        return arguments;
    }

    private static IEnumerable<string> VideoArguments(ConversionPlan plan)
    {
        var profile = plan.Profile;
        var bitrate = Kbps(plan.VideoBitrateKbps);
        var maxRate = Kbps(BitrateCalculator.MaxRateFor(plan.VideoBitrateKbps));
        var bufferSize = Kbps(BitrateCalculator.BufferSizeFor(plan.VideoBitrateKbps));

        List<string> arguments = plan.Encoder == VideoEncoderKind.NvencH264
            ?
            [
                "-c:v", "h264_nvenc",
                "-preset", "p6",
                "-tune", "hq",
                "-rc", "vbr"
            ]
            :
            [
                "-c:v", "libx264",
                "-preset", "slow"
            ];

        arguments.AddRange(
        [
            "-b:v", bitrate,
            "-maxrate", maxRate,
            "-bufsize", bufferSize,
            "-profile:v", profile.H264Profile,
            "-level", profile.H264Level,
            "-pix_fmt", "yuv420p",
            "-g", GopSize(plan)
        ]);

        arguments.AddRange(DecoderConstraintArguments(profile));

        return arguments;
    }

    private static IEnumerable<string> DecoderConstraintArguments(DeviceProfile profile)
    {
        if (profile.MaxBFrames is { } bFrames)
        {
            yield return "-bf";
            yield return bFrames.ToString(CultureInfo.InvariantCulture);
        }

        if (profile.MaxReferenceFrames is { } referenceFrames)
        {
            yield return "-refs";
            yield return referenceFrames.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<string> FilterArguments(ConversionPlan plan)
    {
        var filters = new List<string>
        {
            $"scale={plan.OutputResolution.Width}:{plan.OutputResolution.Height}:flags=lanczos",
            "setsar=1"
        };

        if (plan.FrameRateCapRequired)
        {
            filters.Add($"fps={plan.MaxFrameRate}");
        }

        return ["-vf", string.Join(',', filters)];
    }

    private static IEnumerable<string> AudioArguments(ConversionPlan plan) =>
    [
        "-c:a", "aac",
        "-b:a", Kbps(plan.AudioBitrateKbps),
        "-ac", plan.AudioChannels.ToString(CultureInfo.InvariantCulture),
        "-ar", plan.Profile.AudioSampleRate.ToString(CultureInfo.InvariantCulture)
    ];

    private static string GopSize(ConversionPlan plan) =>
        (plan.MaxFrameRate * 2).ToString(CultureInfo.InvariantCulture);

    private static string Kbps(int value) =>
        value.ToString(CultureInfo.InvariantCulture) + "k";

    public static string ToDisplayString(string executable, IReadOnlyList<string> arguments) =>
        Quote(executable) + " " + string.Join(' ', arguments.Select(Quote));

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) ? "\"" + value + "\"" : value;
}
