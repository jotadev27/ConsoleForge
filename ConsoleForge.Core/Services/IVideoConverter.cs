using ConsoleForge.Core.Models;

namespace ConsoleForge.Core.Services;

public interface IVideoConverter
{
    Task ConvertAsync(
        ConversionPlan plan,
        IProgress<ConversionProgress>? progress,
        Action<string>? log,
        CancellationToken cancellationToken = default);
}
