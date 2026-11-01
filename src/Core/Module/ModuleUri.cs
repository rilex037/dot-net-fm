namespace DotNetFM;

public readonly record struct ModuleUri : IEquatable<ModuleUri>
{
    public string Prefix { get; }

    public string Path { get; }

    public string FullUri => string.IsNullOrEmpty(Prefix) ? Path : $"{Prefix}://{Path}";

    public ModuleUri(string prefix, string path)
    {
        Prefix = prefix ?? "";
        Path = path ?? "";
    }

    public static ModuleUri Parse(string uri)
    {
        if (TryParse(uri, out var result))
            return result;

        return new ModuleUri("", uri);
    }

    public static bool TryParse(string uri, out ModuleUri result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(uri))
            return false;

        int schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            string prefix = uri[..schemeEnd];
            string path = uri[(schemeEnd + 3)..];
            result = new ModuleUri(prefix, path);
            return true;
        }

        result = new ModuleUri("", uri);
        return true;
    }

    public static ModuleUri Create(string prefix, string path)
        => new(prefix, path);

    public bool IsEmpty => string.IsNullOrEmpty(Prefix) && string.IsNullOrEmpty(Path);

    public override string ToString() => FullUri;

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
