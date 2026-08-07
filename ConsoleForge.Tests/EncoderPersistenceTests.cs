using ConsoleForge.Core.Models;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Infrastructure.Storage;

namespace ConsoleForge.Tests;

public class EncoderPersistenceTests : IDisposable
{
    private static readonly VideoEncoderKind[] NvencAvailable =
        [VideoEncoderKind.Libx264, VideoEncoderKind.NvencH264];

    private readonly string _configPath = Path.Combine(
        Path.GetTempPath(), "consoleforge-config-" + Guid.NewGuid().ToString("N"), "config.json");

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ManualPspChoiceDoesNotLeakIntoPs3OrVita()
    {
        var config = new AppConfig();
        config.SetEncoder(TargetDevice.Psp, VideoEncoderKind.NvencH264);

        Assert.Equal(
            VideoEncoderKind.NvencH264,
            Resolve(config, DeviceProfileCatalog.Psp).Encoder);

        Assert.Null(config.GetEncoder(TargetDevice.Ps3));
        Assert.Null(config.GetEncoder(TargetDevice.Vita));

        Assert.False(Resolve(config, DeviceProfileCatalog.Ps3).IsUserChoice);
        Assert.False(Resolve(config, DeviceProfileCatalog.Vita).IsUserChoice);
    }

    [Fact]
    public void ManualPs3ChoiceDoesNotLeakIntoPsp()
    {
        var config = new AppConfig();
        config.SetEncoder(TargetDevice.Ps3, VideoEncoderKind.Libx264);

        var psp = Resolve(config, DeviceProfileCatalog.Psp);

        Assert.Equal(VideoEncoderKind.Libx264, psp.Encoder);
        Assert.False(psp.IsUserChoice);
    }

    [Fact]
    public void ChoicesSurviveAConfigRestart()
    {
        var store = new JsonConfigStore(_configPath);

        var saved = store.Load();
        saved.SetEncoder(TargetDevice.Psp, VideoEncoderKind.NvencH264);
        saved.SetEncoder(TargetDevice.Vita, VideoEncoderKind.Libx264);
        store.Save(saved);

        var reloaded = new JsonConfigStore(_configPath).Load();

        Assert.Equal(VideoEncoderKind.NvencH264, reloaded.GetEncoder(TargetDevice.Psp));
        Assert.Equal(VideoEncoderKind.Libx264, reloaded.GetEncoder(TargetDevice.Vita));
        Assert.Null(reloaded.GetEncoder(TargetDevice.Ps3));

        var psp = Resolve(reloaded, DeviceProfileCatalog.Psp);
        Assert.Equal(VideoEncoderKind.NvencH264, psp.Encoder);
        Assert.True(psp.IsUserChoice);

        var ps3 = Resolve(reloaded, DeviceProfileCatalog.Ps3);
        Assert.Equal(VideoEncoderKind.NvencH264, ps3.Encoder);
        Assert.False(ps3.IsUserChoice);
    }

    [Fact]
    public void PersistedKeysAreReadableDeviceNames()
    {
        var store = new JsonConfigStore(_configPath);
        var config = store.Load();
        config.SetEncoder(TargetDevice.Psp, VideoEncoderKind.NvencH264);
        store.Save(config);

        var json = File.ReadAllText(_configPath);

        Assert.Contains("\"Psp\"", json);
        Assert.Contains("NvencH264", json);
    }

    [Fact]
    public void WithoutASavedChoiceEveryProfileFallsBackToItsDefault()
    {
        var config = new AppConfig();

        foreach (var profile in DeviceProfileCatalog.All)
        {
            var resolution = Resolve(config, profile);

            Assert.False(resolution.IsUserChoice);
            Assert.Equal(EncoderSelector.SelectDefault(profile, NvencAvailable), resolution.Encoder);
        }

        Assert.Equal(VideoEncoderKind.Libx264, Resolve(config, DeviceProfileCatalog.Psp).Encoder);
        Assert.Equal(VideoEncoderKind.NvencH264, Resolve(config, DeviceProfileCatalog.Ps3).Encoder);
        Assert.Equal(VideoEncoderKind.NvencH264, Resolve(config, DeviceProfileCatalog.Vita).Encoder);
    }

    [Fact]
    public void SavedChoiceIsIgnoredWhenThatEncoderIsNoLongerInstalled()
    {
        var config = new AppConfig();
        config.SetEncoder(TargetDevice.Ps3, VideoEncoderKind.NvencH264);

        var resolution = EncoderSelector.Resolve(
            DeviceProfileCatalog.Ps3, [VideoEncoderKind.Libx264], config.GetEncoder(TargetDevice.Ps3));

        Assert.Equal(VideoEncoderKind.Libx264, resolution.Encoder);
        Assert.False(resolution.IsUserChoice);
    }

    [Fact]
    public void ReasonIsSuppressedOnlyWhenTheUserHasChosenForThatProfile()
    {
        var config = new AppConfig();
        Assert.False(Resolve(config, DeviceProfileCatalog.Psp).IsUserChoice);

        config.SetEncoder(TargetDevice.Psp, VideoEncoderKind.NvencH264);
        Assert.True(Resolve(config, DeviceProfileCatalog.Psp).IsUserChoice);
    }

    private static EncoderResolution Resolve(AppConfig config, DeviceProfile profile) =>
        EncoderSelector.Resolve(profile, NvencAvailable, config.GetEncoder(profile.Device));
}
