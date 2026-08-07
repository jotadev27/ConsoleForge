using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Tests;

public class EncoderSelectionTests
{
    private static readonly VideoEncoderKind[] NvencAvailable =
        [VideoEncoderKind.Libx264, VideoEncoderKind.NvencH264];

    private static readonly VideoEncoderKind[] CpuOnly = [VideoEncoderKind.Libx264];

    [Fact]
    public void PspDefaultsToLibx264EvenWhenNvencIsAvailable()
    {
        Assert.Equal(
            VideoEncoderKind.Libx264,
            EncoderSelector.SelectDefault(DeviceProfileCatalog.Psp, NvencAvailable));
    }

    [Fact]
    public void Ps3AndVitaDefaultToNvencWhenAvailable()
    {
        foreach (var profile in new[] { DeviceProfileCatalog.Ps3, DeviceProfileCatalog.Vita })
        {
            Assert.Equal(
                VideoEncoderKind.NvencH264,
                EncoderSelector.SelectDefault(profile, NvencAvailable));
        }
    }

    [Fact]
    public void EveryProfileFallsBackToLibx264WithoutNvenc()
    {
        foreach (var profile in DeviceProfileCatalog.All)
        {
            Assert.Equal(VideoEncoderKind.Libx264, EncoderSelector.SelectDefault(profile, CpuOnly));
        }
    }

    [Fact]
    public void SelectionIsSafeWhenNoEncoderWasDetected()
    {
        Assert.Equal(VideoEncoderKind.Libx264, EncoderSelector.SelectDefault(DeviceProfileCatalog.Psp, []));
    }

    [Fact]
    public void OnlyPspPinsAnEncoder()
    {
        Assert.Equal(VideoEncoderKind.Libx264, DeviceProfileCatalog.Psp.PreferredEncoder);
        Assert.NotNull(DeviceProfileCatalog.Psp.PreferredEncoderReason);

        Assert.Null(DeviceProfileCatalog.Ps3.PreferredEncoder);
        Assert.Null(DeviceProfileCatalog.Vita.PreferredEncoder);
    }

    [Fact]
    public void NvencStaysSelectableForPspAsAManualChoice()
    {
        Assert.Contains(VideoEncoderKind.NvencH264, NvencAvailable);

        var plan = PlanWith(DeviceProfileCatalog.Psp, VideoEncoderKind.NvencH264);

        Assert.Equal(VideoEncoderKind.NvencH264, plan.Encoder);
    }

    [Fact]
    public void PspPlanUsesLibx264WhenBuiltFromTheDefaultSelection()
    {
        var encoder = EncoderSelector.SelectDefault(DeviceProfileCatalog.Psp, NvencAvailable);
        var plan = PlanWith(DeviceProfileCatalog.Psp, encoder);

        Assert.Equal(VideoEncoderKind.Libx264, plan.Encoder);
    }

    [Fact]
    public void Ps3PlanUsesNvencWhenBuiltFromTheDefaultSelection()
    {
        var encoder = EncoderSelector.SelectDefault(DeviceProfileCatalog.Ps3, NvencAvailable);
        var plan = PlanWith(DeviceProfileCatalog.Ps3, encoder);

        Assert.Equal(VideoEncoderKind.NvencH264, plan.Encoder);
    }

    private static ConversionPlan PlanWith(DeviceProfile profile, VideoEncoderKind encoder) =>
        ConversionPlanner.Preview(
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
            encoder);
}
