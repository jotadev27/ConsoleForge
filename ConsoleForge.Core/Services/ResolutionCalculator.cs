using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public static class ResolutionCalculator
{
    public const int MinimumAlignment = 2;

    public static VideoResolution Fit(
        VideoResolution source,
        VideoResolution bounds,
        bool allowUpscale = false,
        int alignment = MinimumAlignment)
    {
        if (source.Width <= 0 || source.Height <= 0) return Align(bounds, bounds, alignment);
        if (!allowUpscale && source.FitsInside(bounds)) return Align(source, bounds, alignment);

        var scale = Math.Min((double)bounds.Width / source.Width, (double)bounds.Height / source.Height);
        var scaled = new VideoResolution(
            (int)Math.Round(source.Width * scale),
            (int)Math.Round(source.Height * scale));

        return Align(scaled, bounds, alignment);
    }

    public static VideoResolution Align(VideoResolution value, VideoResolution bounds, int alignment)
    {
        var step = Math.Max(MinimumAlignment, alignment);
        return new VideoResolution(
            AlignDimension(value.Width, bounds.Width, step),
            AlignDimension(value.Height, bounds.Height, step));
    }

    private static int AlignDimension(int value, int max, int step)
    {
        var aligned = (int)Math.Round((double)value / step, MidpointRounding.AwayFromZero) * step;
        var ceiling = max - (max % step);

        if (ceiling >= step && aligned > ceiling) aligned = ceiling;

        return Math.Max(step, aligned);
    }
}
