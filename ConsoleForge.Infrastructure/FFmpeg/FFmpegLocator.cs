using System.Runtime.InteropServices;

namespace ConsoleForge.Infrastructure.FFmpeg;

public sealed class FFmpegLocator
{
    private readonly string? _configuredFFmpeg;
    private readonly string? _configuredFFprobe;

    public FFmpegLocator(string? configuredFFmpeg = null, string? configuredFFprobe = null)
    {
        _configuredFFmpeg = configuredFFmpeg;
        _configuredFFprobe = configuredFFprobe;
    }

    public string? FFmpegPath => Resolve("ffmpeg", _configuredFFmpeg);

    public string? FFprobePath => Resolve("ffprobe", _configuredFFprobe);

    public bool IsAvailable => FFmpegPath is not null && FFprobePath is not null;

    public string RequireFFmpeg() => FFmpegPath
        ?? throw new FileNotFoundException("ffmpeg was not found on PATH. Set an explicit path in the configuration.");

    public string RequireFFprobe() => FFprobePath
        ?? throw new FileNotFoundException("ffprobe was not found on PATH. Set an explicit path in the configuration.");

    private static string? Resolve(string toolName, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var executable = isWindows ? toolName + ".exe" : toolName;

        var alongsideApp = Path.Combine(AppContext.BaseDirectory, executable);
        if (File.Exists(alongsideApp)) return alongsideApp;

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), executable);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
            }
        }

        foreach (var directory in FallbackDirectories(isWindows))
        {
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> FallbackDirectories(bool isWindows)
    {
        if (isWindows)
        {
            yield return @"C:\ffmpeg\bin";
            yield return @"C:\Program Files\ffmpeg\bin";
        }
        else
        {
            yield return "/usr/bin";
            yield return "/usr/local/bin";
            yield return "/opt/homebrew/bin";
            yield return "/var/lib/flatpak/exports/bin";
        }
    }
}
