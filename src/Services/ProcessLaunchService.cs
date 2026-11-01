using System.Diagnostics;

namespace DotNetFM;

public sealed class ProcessLaunchService
{
    public readonly record struct LaunchResult(bool Success, string? ErrorMessage = null);

    public LaunchResult OpenWithShell(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };

            string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                startInfo.WorkingDirectory = dir;

            Process.Start(startInfo);
            return new LaunchResult(true);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Could not open '{System.IO.Path.GetFileName(path)}': {ex.Message}");
        }
    }
}
