using System.Text;
using ConsoleForge.Core.Profiles;

namespace ConsoleForge.Core.Services;

public static class OutputPathResolver
{
    public static string Resolve(
        string inputPath, string outputRoot, DeviceProfile profile, string? overrideBaseName = null)
    {
        var directory = Path.Combine(outputRoot, profile.OutputSubfolder);
        var requested = string.IsNullOrWhiteSpace(overrideBaseName)
            ? Path.GetFileNameWithoutExtension(inputPath)
            : overrideBaseName;

        var baseName = SanitizeFileName(requested, profile.MaxFileNameLength);
        return EnsureUnique(Path.Combine(directory, baseName + ".mp4"));
    }

    private static readonly char[] PortablyInvalidCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static bool IsValidFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Length > 255) return false;
        if (name.Trim() != name) return false;
        if (name is "." or "..") return false;
        if (name.EndsWith('.')) return false;

        if (name.IndexOfAny(PortablyInvalidCharacters) >= 0) return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        if (name.Any(char.IsControl)) return false;

        return SanitizeFileName(name, 255).Length > 0;
    }

    public static string SanitizeFileName(string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "video";

        var builder = new StringBuilder(name.Length);
        var lastWasSeparator = false;

        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) && character < 128)
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (character is '-' or '_' or '.' or ' ')
            {
                if (!lastWasSeparator && builder.Length > 0) builder.Append('_');
                lastWasSeparator = true;
            }
        }

        var sanitized = builder.ToString().Trim('_', '.');
        if (sanitized.Length == 0) sanitized = "video";
        if (sanitized.Length > maxLength) sanitized = sanitized[..maxLength].TrimEnd('_', '.');

        return sanitized;
    }

    private static string EnsureUnique(string candidate)
    {
        if (!File.Exists(candidate)) return candidate;

        var directory = Path.GetDirectoryName(candidate) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(candidate);
        var extension = Path.GetExtension(candidate);

        for (var index = 2; index < 10000; index++)
        {
            var next = Path.Combine(directory, $"{baseName}_{index}{extension}");
            if (!File.Exists(next)) return next;
        }

        return Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }
}
