using System.IO;

namespace DotNetFM;

public static class IconProvider
{
    private static string BasePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");

    public static string GetFullPath(string iconFileName) =>
        Path.Combine(BasePath, iconFileName);
}
