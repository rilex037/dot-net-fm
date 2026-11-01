using System.IO;
using System.Text.Json;

namespace DotNetFM;

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
            }
        }

        return new SidebarItem();
    }

    public static void Save(SidebarItem config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
