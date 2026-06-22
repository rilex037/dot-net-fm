using System.IO;

namespace DotNetFM;

/// <summary>
/// Simple binary key-value store for persistent app state.
/// Two files per app: .default.app.store (shipped defaults, read-only)
/// and .{username}.app.store (per-user, read/write).
/// API: Read(key) → string, Write(key, value) → void.
/// </summary>
public static class AppStore
{
    private const uint Magic = 0x4150_5053; // "APPS"
    private const uint Version = 1;

    private static Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);
    private static string _userStorePath = "";
    private static string _defaultStorePath = "";
    private static bool _initialized;

    /// <summary>
    /// Initializes the store. Finds or creates the per-user file from defaults.
    /// Must be called once at startup before any Read/Write calls.
    /// </summary>
    public static void Init(string baseDir)
    {
        _defaultStorePath = Path.Combine(baseDir, ".default.app.store");

        string username = Environment.UserName;
        _userStorePath = Path.Combine(baseDir, $".{username}.app.store");

        // Bootstrap default store if missing
        if (!File.Exists(_defaultStorePath))
            WriteStore(_defaultStorePath, CreateDefaults());

        // Bootstrap user store from default
        if (!File.Exists(_userStorePath))
            File.Copy(_defaultStorePath, _userStorePath);

        _entries = ReadStore(_userStorePath);
        _initialized = true;
    }

    /// <summary>Reads a value by key. Throws if key not found — defaults live in .default.app.store only.</summary>
    public static string Read(string key)
    {
        EnsureInitialized();
        if (!_entries.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"AppStore key not found: '{key}'. Ensure it exists in .default.app.store.");
        return value;
    }

    /// <summary>Writes (upserts) a key-value pair and persists to disk.</summary>
    public static void Write(string key, string value)
    {
        EnsureInitialized();
        _entries[key] = value;
        WriteStore(_userStorePath, _entries);
    }

    // ── Default values ─────────────────────────────────────────────

    private static Dictionary<string, string> CreateDefaults()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tab.iconsize"]                    = "64",
            ["window.left"]                     = "0",
            ["window.top"]                      = "0",
            ["window.width"]                    = "1200",
            ["window.height"]                   = "800",
            ["tabs.count"]                      = "0",
            ["tabs.active"]                     = "0",
            ["sidebar.mycomputer.collapsed"]    = "0",
        };
    }

    // ── Binary I/O ─────────────────────────────────────────────────

    private static Dictionary<string, string> ReadStore(string path)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
            return entries; // corrupt or wrong format — return empty

        uint version = reader.ReadUInt32();
        if (version > Version)
            return entries; // future version — skip gracefully

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            entries[key] = value;
        }

        return entries;
    }

    private static void WriteStore(string path, Dictionary<string, string> entries)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(entries.Count);

        foreach (var kvp in entries)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
    }

    // ── Guard ──────────────────────────────────────────────────────

    private static void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "AppStore not initialized. Call AppStore.Init(baseDir) at startup.");
    }
}