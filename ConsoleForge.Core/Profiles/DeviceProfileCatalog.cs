namespace ConsoleForge.Core.Profiles;

public static class DeviceProfileCatalog
{
    public static readonly DeviceProfile Ps3 = new()
    {
        Device = TargetDevice.Ps3,
        DisplayName = "PlayStation 3",
        ShortName = "PS3",
        CompatibilityNote = "XMB video player, HEN optional. Full HD over HDMI.",
        MaxResolution = new VideoResolution(1920, 1080),
        SelectableResolutions =
        [
            new VideoResolution(1920, 1080),
            new VideoResolution(1280, 720),
            new VideoResolution(854, 480)
        ],
        DimensionAlignment = 2,
        H264Profile = "high",
        H264Level = "4.1",
        MaxFrameRate = 60,
        DefaultVideoBitrateKbps = 16000,
        MinVideoBitrateKbps = 10000,
        MaxVideoBitrateKbps = 16000,
        DefaultAudioBitrateKbps = 192,
        MinAudioBitrateKbps = 96,
        MaxAudioBitrateKbps = 320,
        MaxAudioChannels = 2,
        AudioSampleRate = 48000,
        RequiresFastStart = true,
        OutputSubfolder = "VIDEO",
        MaxFileNameLength = 64
    };

    public static readonly DeviceProfile Psp = new()
    {
        Device = TargetDevice.Psp,
        DisplayName = "PlayStation Portable",
        ShortName = "PSP",
        CompatibilityNote = "CFW required. Baseline only, 480x272 panel ceiling.",
        MaxResolution = new VideoResolution(480, 272),
        SelectableResolutions =
        [
            new VideoResolution(480, 272),
            new VideoResolution(320, 240)
        ],
        DimensionAlignment = 16,
        H264Profile = "baseline",
        H264Level = "3.0",
        MaxFrameRate = 30,
        MaxBFrames = 0,
        MaxReferenceFrames = 1,
        PreferredEncoder = VideoEncoderKind.Libx264,
        PreferredEncoderReason =
            "libx264 by default: NVENC rate control is tuned for higher bitrates and loses "
            + "detail at 480x272. Pick NVENC manually if you prefer speed.",
        DefaultVideoBitrateKbps = 1500,
        MinVideoBitrateKbps = 1000,
        MaxVideoBitrateKbps = 2500,
        DefaultAudioBitrateKbps = 128,
        MinAudioBitrateKbps = 64,
        MaxAudioBitrateKbps = 192,
        MaxAudioChannels = 2,
        AudioSampleRate = 44100,
        ThumbnailResolution = new VideoResolution(160, 120),
        ThumbnailExtension = ".THM",
        RequiresFastStart = true,
        OutputSubfolder = "VIDEO",
        MaxFileNameLength = 54
    };

    public static readonly DeviceProfile Vita = new()
    {
        Device = TargetDevice.Vita,
        DisplayName = "PlayStation Vita",
        ShortName = "VITA",
        CompatibilityNote = "HENkaku/Ensō. 1280x720 needs a homebrew player.",
        MaxResolution = new VideoResolution(960, 544),
        SelectableResolutions =
        [
            new VideoResolution(960, 544),
            new VideoResolution(1280, 720),
            new VideoResolution(640, 368)
        ],
        DimensionAlignment = 16,
        H264Profile = "high",
        H264Level = "3.1",
        MaxFrameRate = 30,
        DefaultVideoBitrateKbps = 6000,
        MinVideoBitrateKbps = 4000,
        MaxVideoBitrateKbps = 8000,
        DefaultAudioBitrateKbps = 192,
        MinAudioBitrateKbps = 96,
        MaxAudioBitrateKbps = 320,
        MaxAudioChannels = 2,
        AudioSampleRate = 48000,
        RequiresFastStart = true,
        OutputSubfolder = Path.Combine("PS Vita", "VIDEO"),
        MaxFileNameLength = 64
    };

    public static readonly IReadOnlyList<DeviceProfile> All = [Ps3, Psp, Vita];

    public static DeviceProfile For(TargetDevice device) => device switch
    {
        TargetDevice.Ps3 => Ps3,
        TargetDevice.Psp => Psp,
        TargetDevice.Vita => Vita,
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Unknown target device.")
    };
}
