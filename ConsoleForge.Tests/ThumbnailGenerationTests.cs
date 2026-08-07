using System.Diagnostics;
using System.Globalization;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.FFmpeg;

namespace ConsoleForge.Tests;

public class ThumbnailGenerationTests : IDisposable
{
    private readonly FFmpegLocator _locator = new();
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-thumb-" + Guid.NewGuid().ToString("N"));

    public ThumbnailGenerationTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(640, 480)]
    [InlineData(1080, 1920)]
    public async Task AutoFrameThumbnailIsExactlyTheProfileSize(int width, int height)
    {
        if (!_locator.IsAvailable) return;

        var video = await CreateVideoAsync(width, height);
        var output = Path.Combine(_workspace, $"auto_{width}x{height}.THM");

        await new FFmpegThumbnailService(_locator).CreateAsync(new ThumbnailRequest
        {
            OutputPath = output,
            Size = DeviceProfileCatalog.Psp.ThumbnailResolution!.Value,
            VideoPath = video,
            FramePosition = TimeSpan.FromSeconds(0.2)
        });

        await AssertIsThumbnailSizedAsync(output);
    }

    [Theory]
    [InlineData(1000, 200)]
    [InlineData(200, 1000)]
    [InlineData(160, 120)]
    public async Task CustomCoverIsLetterboxedNeverStretched(int width, int height)
    {
        if (!_locator.IsAvailable) return;

        var image = await CreateImageAsync(width, height);
        var output = Path.Combine(_workspace, $"custom_{width}x{height}.THM");

        await new FFmpegThumbnailService(_locator).CreateAsync(new ThumbnailRequest
        {
            OutputPath = output,
            Size = DeviceProfileCatalog.Psp.ThumbnailResolution!.Value,
            VideoPath = "unused-when-custom-image-is-set",
            FramePosition = TimeSpan.Zero,
            CustomImagePath = image
        });

        await AssertIsThumbnailSizedAsync(output);
    }

    [Fact]
    public void OnlyPspDeclaresThumbnailGeneration()
    {
        Assert.True(DeviceProfileCatalog.Psp.GeneratesThumbnail);
        Assert.Equal(".THM", DeviceProfileCatalog.Psp.ThumbnailExtension);
        Assert.Equal(new VideoResolution(160, 120), DeviceProfileCatalog.Psp.ThumbnailResolution);

        Assert.False(DeviceProfileCatalog.Ps3.GeneratesThumbnail);
        Assert.False(DeviceProfileCatalog.Vita.GeneratesThumbnail);
    }

    private async Task AssertIsThumbnailSizedAsync(string path)
    {
        Assert.True(File.Exists(path), $"{path} was not created");

        var expected = DeviceProfileCatalog.Psp.ThumbnailResolution!.Value;
        var probed = await ProbeSizeAsync(path);

        Assert.Equal(expected.ToString(), probed);
    }

    private async Task<string> ProbeSizeAsync(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _locator.RequireFFprobe(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
                 {
                     "-v", "quiet", "-select_streams", "v:0",
                     "-show_entries", "stream=width,height", "-of", "csv=p=0:s=x", path
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Trim();
    }

    private async Task<string> CreateVideoAsync(int width, int height)
    {
        var path = Path.Combine(_workspace, $"video_{width}x{height}.mkv");

        await RunAsync(_locator.RequireFFmpeg(),
        [
            "-v", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:rate=30:duration=1",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", path
        ]);

        return path;
    }

    private async Task<string> CreateImageAsync(int width, int height)
    {
        var path = Path.Combine(_workspace, $"image_{width}x{height}.png");

        await RunAsync(_locator.RequireFFmpeg(),
        [
            "-v", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:duration=1",
            "-frames:v", "1", path
        ]);

        return path;
    }

    private static async Task RunAsync(string executable, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
    }
}
