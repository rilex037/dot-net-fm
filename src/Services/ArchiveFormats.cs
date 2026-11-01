using System.IO;

namespace DotNetFM;

public enum ArchiveExtractMode
{
    Here,
    Subfolder,
    PickFolder
}

public enum ArchiveFormat
{
    Zip,
    SevenZip,
    TarGz,
    Rar
}

public static class ArchiveFormats
{
    private static readonly string[] CompoundExtensions = [".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst"];

    private static readonly HashSet<string> BuiltinExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".tar", ".gz", ".tgz"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".7z", ".rar", ".cab", ".iso", ".wim", ".lzh", ".arj",
        ".tar", ".gz", ".bz2", ".xz", ".zst", ".tgz", ".tbz2", ".txz", ".tzst"
    };

    public static bool IsArchive(string path)
    {
        string name = GetFileNameOnly(path);
        if (HasCompoundExtension(name)) return true;
        string ext = Path.GetExtension(name);
        return ext.Length > 0 && ArchiveExtensions.Contains(ext);
    }

    public static bool IsBuiltin(string path)
    {
        string name = GetFileNameOnly(path);
        if (HasCompoundExtension(name))
            return name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

        string ext = Path.GetExtension(name);
        return ext.Length > 0 && BuiltinExtensions.Contains(ext);
    }

    public static string GetBaseName(string path)
    {
        string name = GetFileNameOnly(path);

        foreach (string compound in CompoundExtensions)
        {
            if (name.EndsWith(compound, StringComparison.OrdinalIgnoreCase) && name.Length > compound.Length)
                return name[..^compound.Length];
        }

        string ext = Path.GetExtension(name);
        return ext.Length > 0 && ext.Length < name.Length ? name[..^ext.Length] : name;
    }

    private static string GetFileNameOnly(string path)
    {
        int cut = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        return cut >= 0 ? path[(cut + 1)..] : path;
    }

    private static bool HasCompoundExtension(string name)
    {
        foreach (string compound in CompoundExtensions)
        {
            if (name.EndsWith(compound, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
