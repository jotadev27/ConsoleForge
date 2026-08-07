using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.FFmpeg;

namespace ConsoleForge.Tests;

public class PspAudioResampleTests : IDisposable
{
    private const double ClipSeconds = 4.0;
    private const double DurationToleranceSeconds = 0.12;

    private readonly FFmpegLocator _locator = new();
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-audio-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(44100, "aac")]
    [InlineData(48000, "aac")]
    [InlineData(48000, "ac3")]
    [InlineData(32000, "aac")]
    [InlineData(22050, "aac")]
    public async Task PspAudioMatchesProfileRateAndStaysInSyncWithVideo(int sourceRate, string sourceCodec)
    {
        if (!_locator.IsAvailable) return;

        Directory.CreateDirectory(_workspace);
        var source = await CreateSourceClipAsync(sourceRate, sourceCodec);

        var info = await new FFprobeService(_locator).ProbeAsync(source);
        Assert.Equal(sourceRate, info.AudioSampleRate);

        var plan = ConversionPlanner.Create(
            info, DeviceProfileCatalog.Psp, VideoEncoderKind.Libx264, Path.Combine(_workspace, "out"));

        await new FFmpegVideoConverter(_locator).ConvertAsync(plan, null, null, CancellationToken.None);

        var audio = await ProbeAsync(plan.OutputPath, "a:0");
        var video = await ProbeAsync(plan.OutputPath, "v:0");

        var declaredRate = int.Parse(
            audio.GetProperty("sample_rate").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(DeviceProfileCatalog.Psp.AudioSampleRate, declaredRate);

        var audioDuration = ReadDouble(audio, "duration");
        var videoDuration = ReadDouble(video, "duration");

        Assert.InRange(audioDuration, 0.1, ClipSeconds + 1.0);
        Assert.True(
            Math.Abs(audioDuration - videoDuration) < DurationToleranceSeconds,
            $"audio {audioDuration:0.###}s vs video {videoDuration:0.###}s — container rate and payload disagree");

        var decodedSamples = await CountDecodedSampleFramesAsync(plan.OutputPath);
        var impliedDuration = (double)decodedSamples / declaredRate;

        Assert.True(
            Math.Abs(impliedDuration - videoDuration) < DurationToleranceSeconds,
            $"{decodedSamples} decoded sample frames at {declaredRate}Hz implies {impliedDuration:0.###}s "
            + $"but video is {videoDuration:0.###}s — payload was not resampled");
    }

    private async Task<long> CountDecodedSampleFramesAsync(string path)
    {
        var pcmPath = Path.Combine(_workspace, Guid.NewGuid().ToString("N") + ".raw");

        var exitCode = await RunAsync(_locator.RequireFFmpeg(),
        [
            "-v", "error", "-y",
            "-i", path,
            "-map", "0:a:0",
            "-f", "s16le", "-acodec", "pcm_s16le",
            pcmPath
        ]);

        Assert.Equal(0, exitCode);

        const int bytesPerStereoFrame = 4;
        return new FileInfo(pcmPath).Length / bytesPerStereoFrame;
    }

    private async Task<string> CreateSourceClipAsync(int sampleRate, string audioCodec)
    {
        var path = Path.Combine(_workspace, $"source_{audioCodec}_{sampleRate}.mkv");

        var exitCode = await RunAsync(_locator.RequireFFmpeg(),
        [
            "-v", "error", "-y",
            "-f", "lavfi", "-i",
            $"testsrc2=size=1280x720:rate=30:duration={ClipSeconds.ToString(CultureInfo.InvariantCulture)}",
            "-f", "lavfi", "-i",
            $"sine=frequency=440:sample_rate={sampleRate}:duration={ClipSeconds.ToString(CultureInfo.InvariantCulture)}",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-c:a", audioCodec, "-ac", "2", "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
            path
        ]);

        Assert.Equal(0, exitCode);
        return path;
    }

    private async Task<JsonElement> ProbeAsync(string path, string streamSelector)
    {
        string[] arguments =
        [
            "-v", "quiet", "-select_streams", streamSelector,
            "-show_entries", "stream=sample_rate,duration,codec_name",
            "-of", "json", path
        ];

        var startInfo = new ProcessStartInfo
        {
            FileName = _locator.RequireFFprobe(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("streams").EnumerateArray().Single().Clone();
    }

    private static double ReadDouble(JsonElement element, string property) =>
        double.Parse(element.GetProperty(property).GetString()!, CultureInfo.InvariantCulture);

    private static async Task<int> RunAsync(string executable, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
