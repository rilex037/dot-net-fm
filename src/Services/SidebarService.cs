using System.IO;
using System.Text.Json;

namespace DotNetFM;

/// <summary>
/// Loads, saves, and resolves sidebar configuration from a JSON file.
/// Supports %ENV_VAR% path resolution and special folder tokens.
/// </summary>
public static class SidebarService
{
    private const string ConfigFileName = "sidebar-config.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);

    /// <summary>
    /// Loads config from the JSON file on disk.
    /// Returns an empty config if the file is missing or corrupted.
    /// </summary>
    public static SidebarItem Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<SidebarItem>(json, JsonOptions);
                if (config != null)
                    return config;
            }
            catch
            {
                // Corrupted file — return empty
            }
        }

        return new SidebarItem();
    }

    /// <summary>
    /// Persists the current config to disk.
    /// </summary>
    public static void Save(SidebarItem config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
