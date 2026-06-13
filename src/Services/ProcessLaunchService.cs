using System;
using System.Diagnostics;

namespace dot_net_fm;

/// <summary>
/// Handles launching files/processes via shell execution.
/// Returns a result instead of showing UI dialogs.
/// </summary>
public sealed class ProcessLaunchService
{
    /// <summary>Result of a launch operation.</summary>
    public readonly record struct LaunchResult(bool Success, string? ErrorMessage = null);

    /// <summary>
    /// Opens a file or folder using the shell default handler.
    /// For folders, the caller should handle navigation separately;
    /// this method always launches via shell.
    /// </summary>
    public LaunchResult OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return new LaunchResult(true);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Could not open '{System.IO.Path.GetFileName(path)}': {ex.Message}");
        }
    }
}