using System.Globalization;
using ConsoleForge.Core.Models;

namespace ConsoleForge.Infrastructure.FFmpeg;

internal sealed class FFmpegProgressParser
{
    private readonly TimeSpan _total;
    private TimeSpan _position;
    private long _frame;
    private double _fps;
    private double _speed;

    public FFmpegProgressParser(TimeSpan total) => _total = total;

    public bool TryConsume(string line, out ConversionProgress progress)
    {
        progress = default;

        var separator = line.IndexOf('=');
        if (separator <= 0) return false;

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();

        switch (key)
        {
            case "out_time_us" or "out_time_ms":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds)
                    && microseconds >= 0)
                {
                    _position = TimeSpan.FromMilliseconds(microseconds / 1000.0);
                }
                return false;

            case "frame":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame))
                {
                    _frame = frame;
                }
                return false;

            case "fps":
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps))
                {
                    _fps = fps;
                }
                return false;

            case "speed":
                if (double.TryParse(value.TrimEnd('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                {
                    _speed = speed;
                }
                return false;

            case "progress":
                progress = Snapshot(completed: value == "end");
                return true;

            default:
                return false;
        }
    }

    private ConversionProgress Snapshot(bool completed) => new(
        completed && _total > TimeSpan.Zero ? _total : _position,
        _total,
        _frame,
        _fps,
        _speed);
}
