using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ConsoleForge.Core.Profiles;
using ConsoleForge.UI.Models;

namespace ConsoleForge.UI.Converters;

public static class ForgeConverters
{
    public static readonly IValueConverter StatusToBrush = new StatusToBrushConverter();
    public static readonly IValueConverter EncoderName = new EncoderNameConverter();
    public static readonly IValueConverter Not = new NotConverter();
}

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#798187"));
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#C8551A"));
    private static readonly IBrush Success = new SolidColorBrush(Color.Parse("#6F9E4C"));
    private static readonly IBrush Error = new SolidColorBrush(Color.Parse("#B8483B"));
    private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#C9A227"));
    private static readonly IBrush Text = new SolidColorBrush(Color.Parse("#C9CED2"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        QueueItemStatus.Ready => Text,
        QueueItemStatus.Converting or QueueItemStatus.Probing => Accent,
        QueueItemStatus.Completed => Success,
        QueueItemStatus.Failed or QueueItemStatus.Unsupported => Error,
        QueueItemStatus.Cancelled => Warning,
        _ => Dim
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EncoderNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        VideoEncoderKind.Libx264 => "libx264  (CPU, any PC)",
        VideoEncoderKind.NvencH264 => "h264_nvenc  (NVIDIA GPU)",
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;
}
