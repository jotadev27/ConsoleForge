using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public interface IEncoderAvailability
{
    Task<IReadOnlyList<VideoEncoderKind>> GetAvailableEncodersAsync(CancellationToken cancellationToken = default);
}
