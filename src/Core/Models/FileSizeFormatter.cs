namespace DotNetFM;

public static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        double size = bytes;
        int unit = 0;
        do
        {
            size /= 1024;
            unit++;
        } while (size >= 1024 && unit < Units.Length - 1);

        return $"{size:0.#} {Units[unit]}";
    }
}
