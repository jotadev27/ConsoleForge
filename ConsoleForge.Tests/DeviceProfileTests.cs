using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Tests;

public class DeviceProfileTests
{
    [Fact]
    public void PspStaysWithinMediaEngineComfortLimits()
    {
        var psp = DeviceProfileCatalog.Psp;

        Assert.Equal(44100, psp.AudioSampleRate);
        Assert.Equal(128, psp.DefaultAudioBitrateKbps);
        Assert.Equal(1500, psp.DefaultVideoBitrateKbps);
    }

    [Fact]
    public void Ps3AndVitaKeep48000HzAudio()
    {
        Assert.Equal(48000, DeviceProfileCatalog.Ps3.AudioSampleRate);
        Assert.Equal(48000, DeviceProfileCatalog.Vita.AudioSampleRate);
    }

    [Fact]
    public void Ps3AndVitaKeepTheirHigherBitrateBudgets()
    {
        Assert.Equal(16000, DeviceProfileCatalog.Ps3.DefaultVideoBitrateKbps);
        Assert.Equal(6000, DeviceProfileCatalog.Vita.DefaultVideoBitrateKbps);
        Assert.Equal(192, DeviceProfileCatalog.Ps3.DefaultAudioBitrateKbps);
        Assert.Equal(192, DeviceProfileCatalog.Vita.DefaultAudioBitrateKbps);
    }

    [Fact]
    public void HandheldProfilesRequireMacroblockAlignment()
    {
        Assert.Equal(16, DeviceProfileCatalog.Psp.DimensionAlignment);
        Assert.Equal(16, DeviceProfileCatalog.Vita.DimensionAlignment);
    }

    [Fact]
    public void Ps3KeepsEvenAlignmentBecause1080IsNotAMultipleOf16()
    {
        Assert.Equal(2, DeviceProfileCatalog.Ps3.DimensionAlignment);
        Assert.NotEqual(0, DeviceProfileCatalog.Ps3.MaxResolution.Height % 16);
    }

    [Theory]
    [MemberData(nameof(AlignedProfiles))]
    public void AlignedProfilesDeclareAlignedResolutions(DeviceProfile profile)
    {
        var step = profile.DimensionAlignment;

        Assert.Equal(0, profile.MaxResolution.Width % step);
        Assert.Equal(0, profile.MaxResolution.Height % step);

        foreach (var resolution in profile.SelectableResolutions)
        {
            Assert.Equal(0, resolution.Width % step);
            Assert.Equal(0, resolution.Height % step);
        }
    }

    public static TheoryData<DeviceProfile> AlignedProfiles =>
        new() { DeviceProfileCatalog.Psp, DeviceProfileCatalog.Vita };
}
