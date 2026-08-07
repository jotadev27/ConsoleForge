using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.Storage;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configPath;

    public JsonConfigStore(string? configPath = null) => _configPath = configPath ?? DefaultConfigPath();

    public string ConfigPath => _configPath;

    public static string DefaultConfigPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "ConsoleForge", "config.json");
        }

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
        {
            configHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(configHome, "consoleforge", "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return new AppConfig();
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_configPath, JsonSerializer.Serialize(config, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
