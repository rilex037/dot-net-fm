using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace DotNetFM;

public sealed class ClipboardService
{
    private const string PreferredDropEffectFormat = "Preferred DropEffect";
    private const int DropeffectCopy = 1;
    private const int DropeffectMove = 2;

    public bool IsCut => ReadPreferredDropEffect() == DropeffectMove;

    public void Copy(IReadOnlyList<string> paths) => SetFileDropList(paths, DropeffectCopy);

    public void Cut(IReadOnlyList<string> paths) => SetFileDropList(paths, DropeffectMove);

    public void Clear()
    {
        try { Clipboard.Clear(); }
        catch { }
    }

    public IReadOnlyList<string>? TryGetFileDropList()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList()) return null;

            var raw = Clipboard.GetFileDropList();
            if (raw.Count == 0) return null;

            var result = new string[raw.Count];
            raw.CopyTo(result, 0);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static void SetFileDropList(IReadOnlyList<string> paths, int dropEffect)
    {
        if (paths.Count == 0) return;

        try
        {
            var data = new DataObject();
            var collection = new StringCollection();
            foreach (var path in paths)
                collection.Add(path);
            data.SetFileDropList(collection);

            var effectStream = new MemoryStream();
            effectStream.WriteByte((byte)(dropEffect & 0xFF));
            effectStream.WriteByte((byte)((dropEffect >> 8) & 0xFF));
            effectStream.WriteByte((byte)((dropEffect >> 16) & 0xFF));
            effectStream.WriteByte((byte)((dropEffect >> 24) & 0xFF));
            effectStream.Position = 0;
            data.SetData(PreferredDropEffectFormat, effectStream);

            Clipboard.SetDataObject(data, true);
        }
        catch
        {
        }
    }

    private static int ReadPreferredDropEffect()
    {
        try
        {
            if (!Clipboard.ContainsData(PreferredDropEffectFormat)) return DropeffectCopy;
            return ReadEffect(Clipboard.GetData(PreferredDropEffectFormat));
        }
        catch
        {
            return DropeffectCopy;
        }
    }

    private static int ReadEffect(object? data)
    {
        if (data is MemoryStream stream)
        {
            if (stream.CanSeek) stream.Position = 0;
            var buffer = new byte[4];
            int read = stream.Read(buffer, 0, 4);
            if (read == 4) return BitConverter.ToInt32(buffer, 0);
        }

        if (data is byte[] bytes && bytes.Length >= 4)
            return BitConverter.ToInt32(bytes, 0);

        return DropeffectCopy;
    }
}
