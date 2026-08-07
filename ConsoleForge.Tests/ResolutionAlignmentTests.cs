using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Tests;

public class ResolutionAlignmentTests
{
    public static TheoryData<int, int> SourceResolutions()
    {
        var data = new TheoryData<int, int>();
        foreach (var (width, height) in new[]
                 {
                     (1920, 1080), (3840, 2160), (1280, 720), (1440, 1080), (720, 576),
                     (720, 480), (640, 480), (352, 288), (2560, 1080), (1920, 816),
                     (1080, 1920), (480, 640), (854, 480), (1024, 768), (960, 544),
                     (480, 272), (320, 240), (1998, 1080), (4096, 1716), (176, 144)
                 })
        {
            data.Add(width, height);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SourceResolutions))]
    public void PspOutputIsAlwaysMacroblockAligned(int width, int height)
    {
        AssertAligned(DeviceProfileCatalog.Psp, width, height);
    }

    [Theory]
    [MemberData(nameof(SourceResolutions))]
    public void VitaOutputIsAlwaysMacroblockAligned(int width, int height)
    {
        AssertAligned(DeviceProfileCatalog.Vita, width, height);
    }

    [Theory]
    [MemberData(nameof(SourceResolutions))]
    public void Ps3OutputStaysEvenAndUnconstrainedBy16(int width, int height)
    {
        var plan = Plan(DeviceProfileCatalog.Ps3, width, height);

        Assert.Equal(0, plan.OutputResolution.Width % 2);
        Assert.Equal(0, plan.OutputResolution.Height % 2);
        Assert.True(plan.OutputResolution.FitsInside(DeviceProfileCatalog.Ps3.MaxResolution));
    }

    [Fact]
    public void SixteenNineSourceFillsPspPanelInsteadOfLandingOn270()
    {
        var plan = Plan(DeviceProfileCatalog.Psp, 1920, 1080);

        Assert.Equal(new VideoResolution(480, 272), plan.OutputResolution);
    }

    [Fact]
    public void SixteenNineSourceFillsVitaPanelInsteadOfLandingOn540()
    {
        var plan = Plan(DeviceProfileCatalog.Vita, 1920, 1080);

        Assert.Equal(new VideoResolution(960, 544), plan.OutputResolution);
    }

    [Fact]
    public void Ps3KeepsExact1080pForA1080pSource()
    {
        var plan = Plan(DeviceProfileCatalog.Ps3, 1920, 1080);

        Assert.Equal(new VideoResolution(1920, 1080), plan.OutputResolution);
    }

    [Theory]
    [MemberData(nameof(SourceResolutions))]
    public void AdvancedResolutionOverridesStayAligned(int width, int height)
    {
        foreach (var profile in new[] { DeviceProfileCatalog.Psp, DeviceProfileCatalog.Vita })
        {
            foreach (var selectable in profile.SelectableResolutions)
            {
                foreach (var allowUpscale in new[] { false, true })
                {
                    var plan = Plan(profile, width, height, new ConversionOverrides
                    {
                        Resolution = selectable,
                        AllowUpscale = allowUpscale
                    });

                    Assert.Equal(0, plan.OutputResolution.Width % profile.DimensionAlignment);
                    Assert.Equal(0, plan.OutputResolution.Height % profile.DimensionAlignment);
                }
            }
        }
    }

    [Fact]
    public void AlignmentNeverDegeneratesToZero()
    {
        var tiny = ResolutionCalculator.Fit(
            new VideoResolution(8, 4), new VideoResolution(480, 272), allowUpscale: false, alignment: 16);

        Assert.True(tiny.Width >= 16);
        Assert.True(tiny.Height >= 16);
    }

    private static void AssertAligned(DeviceProfile profile, int width, int height)
    {
        var plan = Plan(profile, width, height);
        var step = profile.DimensionAlignment;

        Assert.Equal(0, plan.OutputResolution.Width % step);
        Assert.Equal(0, plan.OutputResolution.Height % step);
        Assert.True(
            plan.OutputResolution.FitsInside(profile.MaxResolution),
            $"{plan.OutputResolution} exceeds {profile.MaxResolution}");
    }

    private static ConversionPlan Plan(
        DeviceProfile profile, int width, int height, ConversionOverrides? overrides = null) =>
        ConversionPlanner.Preview(
            new MediaInfo
            {
                FilePath = "/tmp/source.mkv",
                FileSizeBytes = 1,
                ContainerFormat = "matroska",
                Duration = TimeSpan.FromMinutes(1),
                VideoCodec = "h264",
                Resolution = new VideoResolution(width, height),
                FrameRate = 30,
                AudioCodec = "aac",
                AudioChannels = 2
            },
            profile,
            VideoEncoderKind.Libx264,
            overrides);
}
