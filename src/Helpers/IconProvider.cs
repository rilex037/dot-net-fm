using System.IO;

namespace DotNetFM;

/// <summary>
/// Resolves file paths for icons stored under Assets/Icons.
/// This is the generic base — it has no knowledge of sidebar or dialog icon mappings.
/// </summary>
public static class IconProvider
{
    private static string BasePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");

    /// <summary>
    /// Returns the full path to any icon file under Assets/Icons.
    /// Example: <c>GetFullPath("sidebar-home.svg")</c> →
    ///          <c>".../Assets/Icons/sidebar-home.svg"</c>
    /// </summary>
    public static string GetFullPath(string iconFileName) =>
        Path.Combine(BasePath, iconFileName);
}