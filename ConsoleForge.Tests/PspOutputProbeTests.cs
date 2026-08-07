using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.FFmpeg;

namespace ConsoleForge.Tests;

public class PspOutputProbeTests : IDisposable
{
    private static readonly string[] AcceptedBaselineNames = ["Constrained Baseline", "Baseline"];

    private readonly FFmpegLocator _locator = new();
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-probe-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(1920, 1080, 60)]
    [InlineData(640, 480, 30)]
    [InlineData(1080, 1920, 25)]
    public async Task PspOutputIsBaselineWithoutBFrames(int width, int height, int frameRate)
    {
        if (!_locator.IsAvailable) return;

        Directory.CreateDirectory(_workspace);
        var source = await CreateSourceClipAsync(width, height, frameRate);

        var probeService = new FFprobeService(_locator);
        var info = await probeService.ProbeAsync(source);

        var plan = ConversionPlanner.Create(
            info, DeviceProfileCatalog.Psp, VideoEncoderKind.Libx264, Path.Combine(_workspace, "out"));

        await new FFmpegVideoConverter(_locator).ConvertAsync(plan, null, null, CancellationToken.None);

        var video = await ProbeStreamAsync(plan.OutputPath, "v:0");
        var audio = await ProbeStreamAsync(plan.OutputPath, "a:0");

        Assert.Equal("h264", video.GetProperty("codec_name").GetString());
        Assert.Contains(video.GetProperty("profile").GetString(), AcceptedBaselineNames);
        Assert.Equal(0, video.GetProperty("has_b_frames").GetInt32());

        if (video.TryGetProperty("refs", out var referenceFrames))
        {
            Assert.Equal(1, referenceFrames.GetInt32());
        }

        Assert.Equal(0, video.GetProperty("width").GetInt32() % DeviceProfileCatalog.Psp.DimensionAlignment);
        Assert.Equal(0, video.GetProperty("height").GetInt32() % DeviceProfileCatalog.Psp.DimensionAlignment);

        Assert.Equal("aac", audio.GetProperty("codec_name").GetString());
        Assert.Equal(
            DeviceProfileCatalog.Psp.AudioSampleRate.ToString(CultureInfo.InvariantCulture),
            audio.GetProperty("sample_rate").GetString());
    }

    private async Task<string> CreateSourceClipAsync(int width, int height, int frameRate)
    {
        var path = Path.Combine(_workspace, $"source_{width}x{height}.mkv");

        var exitCode = await RunAsync(_locator.RequireFFmpeg(),
        [
            "-v", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:rate={frameRate}:duration=2",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-ac", "2", "-ar", "48000",
            path
        ]);

        Assert.Equal(0, exitCode);
        return path;
    }

    private async Task<JsonElement> ProbeStreamAsync(string path, string streamSelector)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _locator.RequireFFprobe(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in new[]
                 {
                     "-v", "quiet", "-select_streams", streamSelector,
                     "-show_entries",
                     "stream=codec_name,profile,has_b_frames,refs,width,height,sample_rate",
                     "-of", "json", path
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("streams").EnumerateArray().Single().Clone();
    }

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
