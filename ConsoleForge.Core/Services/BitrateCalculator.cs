using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public static class BitrateCalculator
{
    private const int AbsoluteFloorKbps = 300;
    private const double NearNativePixelRatio = 0.7;

    public static int ForResolution(DeviceProfile profile, VideoResolution output)
    {
        var referencePixels = profile.MaxResolution.PixelCount;
        if (referencePixels <= 0 || output.PixelCount <= 0)
        {
            return profile.DefaultVideoBitrateKbps;
        }

        var pixelRatio = (double)output.PixelCount / referencePixels;
        var scaled = (int)Math.Round(profile.DefaultVideoBitrateKbps * Math.Sqrt(pixelRatio) / 100.0) * 100;

        var floor = pixelRatio >= NearNativePixelRatio ? profile.MinVideoBitrateKbps : AbsoluteFloorKbps;

        return Math.Clamp(scaled, floor, profile.MaxVideoBitrateKbps);
    }

    public static int MaxRateFor(int targetKbps) => (int)Math.Round(targetKbps * 1.25);

    public static int BufferSizeFor(int targetKbps) => targetKbps * 2;
}
