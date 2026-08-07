using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public static class BitrateOptions
{
    private static readonly int[] AudioLadder = [64, 96, 112, 128, 160, 192, 224, 256, 320];

    public static IReadOnlyList<int> ForVideo(DeviceProfile profile)
    {
        var step = VideoStep(profile);
        var values = new List<int>();

        for (var value = RoundUpTo(profile.MinVideoBitrateKbps, step);
             value <= profile.MaxVideoBitrateKbps;
             value += step)
        {
            values.Add(value);
        }

        return Finalize(values, profile.DefaultVideoBitrateKbps, profile.MinVideoBitrateKbps, profile.MaxVideoBitrateKbps);
    }

    public static IReadOnlyList<int> ForAudio(DeviceProfile profile)
    {
        var values = AudioLadder
            .Where(value => value >= profile.MinAudioBitrateKbps && value <= profile.MaxAudioBitrateKbps)
            .ToList();

        return Finalize(values, profile.DefaultAudioBitrateKbps, profile.MinAudioBitrateKbps, profile.MaxAudioBitrateKbps);
    }

    private static int VideoStep(DeviceProfile profile)
    {
        var span = profile.MaxVideoBitrateKbps - profile.MinVideoBitrateKbps;
        return span >= 6000 ? 1000 : span >= 2000 ? 500 : 250;
    }

    private static int RoundUpTo(int value, int step) =>
        value % step == 0 ? value : (value / step + 1) * step;

    private static IReadOnlyList<int> Finalize(List<int> values, int required, int min, int max)
    {
        if (required >= min && required <= max && !values.Contains(required)) values.Add(required);
        if (values.Count == 0) values.Add(Math.Clamp(required, min, max));

        values.Sort();
        return values;
    }
}
