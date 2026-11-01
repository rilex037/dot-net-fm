using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace DotNetFM;

public partial class MenuBar : UserControl
{
    public event Action? AddBookmarkRequested;
    public event Action? ManageBookmarksRequested;
    public event Action? BookmarksSectionToggleRequested;
    public event Action? HiddenFilesToggleRequested;
    public event Action? SettingsRequested;

    public MenuBar()
    {
        InitializeComponent();
    }

    public void SetBookmarksSectionVisible(bool visible) => BookmarksSectionToggle.IsChecked = visible;

    public void SetHiddenFilesShown(bool shown) => HiddenFilesToggle.IsChecked = shown;

    private void BookmarksSectionToggle_Click(object sender, RoutedEventArgs e)
    {
        BookmarksSectionToggleRequested?.Invoke();
    }

    private void HiddenFilesToggle_Click(object sender, RoutedEventArgs e)
    {
        HiddenFilesToggleRequested?.Invoke();
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        AddBookmarkRequested?.Invoke();
    }

    private void ManageBookmarks_Click(object sender, RoutedEventArgs e)
    {
        ManageBookmarksRequested?.Invoke();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version?.ToString(3) ?? "0.1.0";

        CustomDialog.Show(
            "About DotNetFM",
            $"DotNetFM File Manager\nVersion {versionText}-alpha\n\nA lightweight file manager built with WPF.",
            DialogIcon.Info);
    }
}
