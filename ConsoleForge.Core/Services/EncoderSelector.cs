using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public readonly record struct EncoderResolution(VideoEncoderKind Encoder, bool IsUserChoice);

public static class EncoderSelector
{
    private static readonly VideoEncoderKind[] FastestFirst =
        [VideoEncoderKind.NvencH264, VideoEncoderKind.Libx264];

    public static EncoderResolution Resolve(
        DeviceProfile profile,
        IReadOnlyList<VideoEncoderKind> availableEncoders,
        VideoEncoderKind? userChoice)
    {
        if (userChoice is { } chosen && availableEncoders.Contains(chosen))
        {
            return new EncoderResolution(chosen, IsUserChoice: true);
        }

        return new EncoderResolution(SelectDefault(profile, availableEncoders), IsUserChoice: false);
    }

    public static VideoEncoderKind SelectDefault(
        DeviceProfile profile,
        IReadOnlyList<VideoEncoderKind> availableEncoders)
    {
        if (profile.PreferredEncoder is { } preferred && availableEncoders.Contains(preferred))
        {
            return preferred;
        }

        foreach (var candidate in FastestFirst)
        {
            if (availableEncoders.Contains(candidate)) return candidate;
        }

        return VideoEncoderKind.Libx264;
    }
}
