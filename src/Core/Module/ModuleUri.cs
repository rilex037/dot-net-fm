namespace dot_net_fm;

/// <summary>
/// Type-safe URI that pairs a module prefix with a path.
/// Examples: "windows://C:\Users\Admin\Documents", "ftp://192.168.1.50/home/user"
/// 
/// The address bar shows only the human-readable path (without prefix).
/// Internally, bookmarks and routing use the full ModuleUri.
/// </summary>
public readonly record struct ModuleUri : IEquatable<ModuleUri>
{
    /// <summary>The module's unique prefix (e.g., "windows", "ftp", "onedrive").</summary>
    public string Prefix { get; }

    /// <summary>The path within the module (e.g., "C:\Users\Admin\Documents", "/home/user").</summary>
    public string Path { get; }

    /// <summary>The full URI string including prefix (e.g., "windows://C:\Users\Admin\Documents").</summary>
    public string FullUri => string.IsNullOrEmpty(Prefix) ? Path : $"{Prefix}://{Path}";

    public ModuleUri(string prefix, string path)
    {
        Prefix = prefix ?? "";
        Path = path ?? "";
    }

    /// <summary>
    /// Parses a full URI string into a ModuleUri.
    /// "windows://C:\Users" → ("windows", @"C:\Users")
    /// "C:\Users" → ("", "C:\Users") — no prefix means default module
    /// </summary>
    public static ModuleUri Parse(string uri)
    {
        if (TryParse(uri, out var result))
            return result;

        return new ModuleUri("", uri);
    }

    /// <summary>
    /// Tries to parse a URI string into a ModuleUri.
    /// Returns false if the format is invalid.
    /// </summary>
    public static bool TryParse(string uri, out ModuleUri result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(uri))
            return false;

        // Check for "prefix://" format
        int schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            string prefix = uri[..schemeEnd];
            string path = uri[(schemeEnd + 3)..];
            result = new ModuleUri(prefix, path);
            return true;
        }

        // No prefix — treat as a bare path
        result = new ModuleUri("", uri);
        return true;
    }

    /// <summary>
    /// Creates a ModuleUri with the given prefix and path.
    /// </summary>
    public static ModuleUri Create(string prefix, string path)
        => new(prefix, path);

    public bool IsEmpty => string.IsNullOrEmpty(Prefix) && string.IsNullOrEmpty(Path);

    public override string ToString() => FullUri;

    // Equality is based on Prefix + Path (case-insensitive for paths)
    public bool Equals(ModuleUri other)
    {
        return string.Equals(Prefix, other.Prefix, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(Prefix),
            StringComparer.OrdinalIgnoreCase.GetHashCode(Path));
    }

}