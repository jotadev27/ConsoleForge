using ConsoleForge.Core.Models;

namespace ConsoleForge.Core.Services;

public interface IProbeService
{
    Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}
