using System.IO;
using System.Text.Json;

namespace DotNetFM;

public static class AppStore
{
    private const int CurrentFormatVersion = 2;
    private const uint LegacyMagic = 0x4150_5053;

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);
    private static string _storePath = "";

    public static void Init(string baseDir)
    {
        lock (Gate)
        {
            _storePath = Path.Combine(baseDir, $"settings.{Environment.UserName}.json");

            var entries = CreateDefaults();
            bool importedLegacy = false;

            if (!File.Exists(_storePath))
                importedLegacy = ImportLegacyStore(baseDir, entries);

            foreach (var pair in LoadFromDisk())
                entries[pair.Key] = pair.Value;

            _entries = entries;

            if (importedLegacy)
                SaveToDisk();
        }
    }

    public static string Read(string key)
    {
        lock (Gate)
        {
            if (_entries.TryGetValue(key, out var value))
                return value;
            return "";
        }
    }

    public static int ReadInt(string key, int fallback)
    {
        return int.TryParse(Read(key), out int value) ? value : fallback;
    }

    public static double ReadDouble(string key, double fallback)
    {
        return double.TryParse(Read(key), out double value) ? value : fallback;
    }

    public static bool ReadBool(string key, bool fallback)
    {
        string value = Read(key);
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return fallback;
    }

    public static void Write(string key, string value)
    {
        WriteMany(new Dictionary<string, string> { [key] = value });
    }

    public static void WriteMany(IReadOnlyDictionary<string, string> changes)
    {
        lock (Gate)
        {
            foreach (var pair in changes)
                _entries[pair.Key] = pair.Value;
            SaveToDisk();
        }
    }

    private static Dictionary<string, string> CreateDefaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["tab.iconsize"]                    = "64",
        ["window.left"]                     = "0",
        ["window.top"]                      = "0",
        ["window.width"]                    = "1200",
        ["window.height"]                   = "800",
        ["window.maximized"]                = "0",
        ["tabs.count"]                      = "0",
        ["tabs.active"]                     = "0",
        ["sidebar.mycomputer.collapsed"]    = "0",
        ["sidebar.bookmarks.collapsed"]     = "0",
        ["sidebar.collapsed"]               = "0",
        ["sidebar.width"]                   = "200",
        ["view.showhidden"]                 = "0",
        ["terminal.command"]                = "powershell.exe -NoExit -Command Set-Location -LiteralPath '%V'",
        ["contextmenu.open"]                = "1",
        ["contextmenu.opentab"]             = "1",
        ["contextmenu.cut"]                 = "1",
        ["contextmenu.copy"]                = "1",
        ["contextmenu.paste"]               = "1",
        ["contextmenu.rename"]              = "1",
        ["contextmenu.delete"]              = "1",
        ["contextmenu.restore"]             = "1",
        ["contextmenu.copypath"]            = "1",
        ["contextmenu.new"]                 = "1",
        ["contextmenu.sort"]                = "1",
        ["contextmenu.refresh"]             = "1",
        ["contextmenu.selectall"]           = "1",
        ["contextmenu.hidden"]              = "1",
        ["contextmenu.terminal"]            = "1",
        ["contextmenu.properties"]          = "1",
        ["contextmenu.shell.selection"]     = "1",
        ["contextmenu.shell.background"]    = "0",
        ["contextmenu.extract"]             = "1",
        ["contextmenu.compress"]            = "1",
        ["archives.engine"]                 = "auto",
        ["archives.extract.default"]        = "here",
        ["archives.format"]                 = "zip",
        ["bookmarks.count"]                 = "0",
    };

    private static Dictionary<string, string> LoadFromDisk()
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(_storePath))
            {
                string json = File.ReadAllText(_storePath);
                var payload = JsonSerializer.Deserialize<StoreFile>(json);
                if (payload?.Settings != null)
                {
                    foreach (var pair in payload.Settings)
                        entries[pair.Key] = pair.Value;
                    return entries;
                }
            }
            else
            {
                return entries;
            }
        }
        catch
        {
        }

        QuarantineCorruptStore();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void QuarantineCorruptStore()
    {
        try
        {
            if (File.Exists(_storePath))
                File.Move(_storePath, _storePath + ".corrupt", overwrite: true);
        }
        catch
        {
        }
    }

    private static void SaveToDisk()
    {
        if (string.IsNullOrEmpty(_storePath)) return;

        try
        {
            var payload = new StoreFile
            {
                Version = CurrentFormatVersion,
                Settings = new Dictionary<string, string>(_entries, StringComparer.OrdinalIgnoreCase)
            };

            string json = JsonSerializer.Serialize(payload, JsonOptions);
            string tempPath = _storePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _storePath, overwrite: true);
        }
        catch
        {
        }
    }

    private static bool ImportLegacyStore(string baseDir, Dictionary<string, string> entries)
    {
        string legacyPath = Path.Combine(baseDir, $".{Environment.UserName}.app.store");

        try
        {
            if (!File.Exists(legacyPath)) return false;

            using var stream = File.OpenRead(legacyPath);
            using var reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != LegacyMagic) return false;
            if (reader.ReadUInt32() > 1) return false;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && i < 100_000; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();
                entries[key] = value;
            }

            if (entries.ContainsKey("window.left"))
                entries["window.placed"] = "1";

            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class StoreFile
    {
        public int Version { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
