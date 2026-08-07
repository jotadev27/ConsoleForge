namespace ConsoleForge.Core.Models;

public readonly record struct ConversionProgress(
    TimeSpan Position,
    TimeSpan Total,
    long Frame,
    double Fps,
    double Speed)
{
    public double Percent => Total.TotalSeconds <= 0
        ? 0
        : Math.Clamp(Position.TotalSeconds / Total.TotalSeconds * 100.0, 0, 100);

    public TimeSpan? Remaining
    {
        get
        {
            if (Speed <= 0 || Total.TotalSeconds <= 0) return null;
            var secondsLeft = (Total.TotalSeconds - Position.TotalSeconds) / Speed;
            return secondsLeft < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(secondsLeft);
        }
    }
}
