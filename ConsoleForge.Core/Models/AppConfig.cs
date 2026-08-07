using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Models;

public sealed class AppConfig
{
    public TargetDevice LastDevice { get; set; } = TargetDevice.Psp;
    public Dictionary<TargetDevice, VideoEncoderKind> EncoderByDevice { get; set; } = [];
    public string? OutputRoot { get; set; }
    public string? LastInputFolder { get; set; }
    public bool AdvancedMode { get; set; }
    public string? FFmpegPath { get; set; }
    public string? FFprobePath { get; set; }

    public VideoEncoderKind? GetEncoder(TargetDevice device) =>
        EncoderByDevice.TryGetValue(device, out var encoder) ? encoder : null;

    public void SetEncoder(TargetDevice device, VideoEncoderKind encoder) =>
        EncoderByDevice[device] = encoder;
}
