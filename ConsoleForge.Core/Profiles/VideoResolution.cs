namespace ConsoleForge.Core.Profiles;

public readonly record struct VideoResolution(int Width, int Height)
{
    public int PixelCount => Width * Height;

    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;

    public bool FitsInside(VideoResolution bounds) =>
        Width <= bounds.Width && Height <= bounds.Height;

    public override string ToString() => $"{Width}x{Height}";
}
