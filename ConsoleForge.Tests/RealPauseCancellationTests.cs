using System.Diagnostics;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.FFmpeg;

namespace ConsoleForge.Tests;

public class RealPauseCancellationTests : IDisposable
{
    private readonly FFmpegLocator _locator = new();
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-realpause-" + Guid.NewGuid().ToString("N"));

    public RealPauseCancellationTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CancellingRealFfmpegAlwaysSurfacesAsOperationCanceled()
    {
        if (!_locator.IsAvailable) return;

        var source = await CreateLongSourceAsync();
        var info = await new FFprobeService(_locator).ProbeAsync(source);

        var plan = ConversionPlanner.Create(
            info, DeviceProfileCatalog.Psp, VideoEncoderKind.Libx264, Path.Combine(_workspace, "out"));

        using var cancellation = new CancellationTokenSource();
        var converter = new FFmpegVideoConverter(_locator);

        var conversion = converter.ConvertAsync(plan, null, null, cancellation.Token);

        await WaitForAsync(() => File.Exists(plan.OutputPath));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion);

        Assert.False(File.Exists(plan.OutputPath), "the partial output must be deleted");
    }

    [Fact]
    public async Task CancellingLateStillSurfacesAsOperationCanceledNotAnExitCodeFailure()
    {
        if (!_locator.IsAvailable) return;

        var source = await CreateLongSourceAsync();
        var info = await new FFprobeService(_locator).ProbeAsync(source);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var plan = ConversionPlanner.Create(
                info, DeviceProfileCatalog.Psp, VideoEncoderKind.Libx264,
                Path.Combine(_workspace, $"out{attempt}"));

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250 + attempt * 150));
            var converter = new FFmpegVideoConverter(_locator);

            var error = await Record.ExceptionAsync(
                () => converter.ConvertAsync(plan, null, null, cancellation.Token));

            Assert.True(
                error is OperationCanceledException,
                $"attempt {attempt} surfaced {error?.GetType().Name ?? "success"} instead of a cancellation: "
                + error?.Message);
        }
    }

    private async Task<string> CreateLongSourceAsync()
    {
        var path = Path.Combine(_workspace, "long.mkv");

        var startInfo = new ProcessStartInfo
        {
            FileName = _locator.RequireFFmpeg(),
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
                 {
                     "-v", "error", "-y",
                     "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=30:duration=25",
                     "-f", "lavfi", "-i", "sine=frequency=440:duration=25",
                     "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
                     "-c:a", "aac", "-ac", "2", path
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);

        return path;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 300 && !condition(); attempt++) await Task.Delay(10);
    }
}
