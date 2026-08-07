namespace ConsoleForge.Infrastructure.Storage;

public static class MediaFileScanner
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".ts", ".m2ts", ".mts", ".vob", ".ogv", ".3gp", ".rmvb", ".divx"
    };

    public static bool IsVideoFile(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static IReadOnlyList<string> Scan(string folderPath, bool recursive = true)
    {
        if (!Directory.Exists(folderPath)) return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        return Directory.EnumerateFiles(folderPath, "*", options)
            .Where(IsVideoFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
