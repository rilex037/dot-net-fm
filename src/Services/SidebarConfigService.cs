using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace dot_net_fm;

/// <summary>
/// Loads, saves, and resolves sidebar configuration from a JSON file.
/// Supports %ENV_VAR% path resolution and special folder tokens.
/// </summary>
public static class SidebarConfigService
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
    public static SidebarItemConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<SidebarItemConfig>(json, JsonOptions);
                if (config != null)
                    return config;
            }
            catch
            {
                // Corrupted file — return empty
            }
        }

        return new SidebarItemConfig();
    }

    /// <summary>
    /// Persists the current config to disk.
    /// </summary>
    public static void Save(SidebarItemConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Resolves a path string that may contain %ENV_VAR% tokens or special names.
    /// </summary>
    public static string ResolvePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return "";

        // Expand %ENV_VAR% patterns
        string result = rawPath;
        int start = 0;
        while (true)
        {
            int open = result.IndexOf('%', start);
            if (open < 0) break;
            int close = result.IndexOf('%', open + 1);
            if (close < 0) break;

            string varName = result.Substring(open + 1, close - open - 1);
            string? envValue = Environment.GetEnvironmentVariable(varName);
            if (envValue != null)
            {
                result = result.Substring(0, open) + envValue + result.Substring(close + 1);
                start = open + envValue.Length;
            }
            else
            {
                start = close + 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a bookmark and saves the config.
    /// </summary>
    public static void AddBookmark(SidebarItemConfig config, string name, string path, string icon = "Home")
    {
        config.Bookmarks.Add(new SidebarItem { Name = name, Path = path, IconPath = icon });
        Save(config);
    }

    /// <summary>
    /// Removes a bookmark by name and saves the config.
    /// </summary>
    public static bool RemoveBookmark(SidebarItemConfig config, string name)
    {
        int removed = config.Bookmarks.RemoveAll(b =>
            b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            Save(config);
        return removed > 0;
    }

}
