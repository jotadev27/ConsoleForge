using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.FFmpeg;

namespace ConsoleForge.Tests;

public class PspDecoderConstraintTests
{
    [Fact]
    public void PspTargetsBaselineProfile()
    {
        Assert.Equal("baseline", DeviceProfileCatalog.Psp.H264Profile);
    }

    [Fact]
    public void PspForbidsBFramesAndExtraReferenceFrames()
    {
        Assert.Equal(0, DeviceProfileCatalog.Psp.MaxBFrames);
        Assert.Equal(1, DeviceProfileCatalog.Psp.MaxReferenceFrames);
    }

    [Fact]
    public void Ps3AndVitaKeepHighProfileAndUnconstrainedEncoding()
    {
        foreach (var profile in new[] { DeviceProfileCatalog.Ps3, DeviceProfileCatalog.Vita })
        {
            Assert.Equal("high", profile.H264Profile);
            Assert.Null(profile.MaxBFrames);
            Assert.Null(profile.MaxReferenceFrames);
        }
    }

    [Theory]
    [InlineData(VideoEncoderKind.Libx264)]
    [InlineData(VideoEncoderKind.NvencH264)]
    public void PspArgumentsPinBaselineNoBFramesAndOneReference(VideoEncoderKind encoder)
    {
        var arguments = Arguments(DeviceProfileCatalog.Psp, encoder);

        AssertOptionValue(arguments, "-profile:v", "baseline");
        AssertOptionValue(arguments, "-bf", "0");
        AssertOptionValue(arguments, "-refs", "1");
    }

    [Theory]
    [InlineData(VideoEncoderKind.Libx264)]
    [InlineData(VideoEncoderKind.NvencH264)]
    public void Ps3AndVitaArgumentsOmitDecoderConstraints(VideoEncoderKind encoder)
    {
        foreach (var profile in new[] { DeviceProfileCatalog.Ps3, DeviceProfileCatalog.Vita })
        {
            var arguments = Arguments(profile, encoder);

            AssertOptionValue(arguments, "-profile:v", "high");
            Assert.DoesNotContain("-bf", arguments);
            Assert.DoesNotContain("-refs", arguments);
        }
    }

    private static void AssertOptionValue(IReadOnlyList<string> arguments, string option, string expected)
    {
        var index = arguments.ToList().IndexOf(option);

        Assert.True(index >= 0, $"expected '{option}' in: {string.Join(' ', arguments)}");
        Assert.True(index + 1 < arguments.Count, $"'{option}' has no value");
        Assert.Equal(expected, arguments[index + 1]);
    }

    private static IReadOnlyList<string> Arguments(DeviceProfile profile, VideoEncoderKind encoder)
    {
        var plan = ConversionPlanner.Create(
            new MediaInfo
            {
                FilePath = "/tmp/source.mkv",
                FileSizeBytes = 1,
                ContainerFormat = "matroska",
                Duration = TimeSpan.FromMinutes(1),
                VideoCodec = "h264",
                Resolution = new VideoResolution(1920, 1080),
                FrameRate = 30,
                AudioCodec = "aac",
                AudioChannels = 2
            },
            profile,
            encoder,
            Path.Combine(Path.GetTempPath(), "consoleforge-tests"));

        return FFmpegArgumentBuilder.Build(plan);
    }
}
