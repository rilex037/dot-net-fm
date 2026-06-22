using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetFM;

/// <summary>
/// View model for a sidebar section rendered by SidebarPanel.
/// Each section has a title, a list of navigable items, and can be collapsed/expanded.
/// </summary>
public class SidebarSectionView : INotifyPropertyChanged
{
    private bool _isCollapsed;

    /// <summary>Stable identifier matching <see cref="SidebarSection.Id"/> for persisting UI state.</summary>
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
    public ObservableCollection<SidebarItem.Item> Items { get; set; } = new();

    /// <summary>
    /// Whether the section items are collapsed (hidden).
    /// Setting this property persists the state via AppStore when an <see cref="Id"/> is assigned.
    /// </summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            OnPropertyChanged();

            // Persist collapsed state to user store.
            if (!string.IsNullOrEmpty(Id))
                AppStore.Write($"sidebar.{Id}.collapsed", value ? "1" : "0");
        }
    }

    /// <summary>Toggle between collapsed and expanded state.</summary>
    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    /// <summary>
    /// Reads the persisted collapsed state for this section from AppStore.
    /// Defaults to <c>false</c> (expanded) if no saved state exists.
    /// </summary>
    public void RestoreCollapsedState()
    {
        if (string.IsNullOrEmpty(Id)) return;

        try
        {
            string val = AppStore.Read($"sidebar.{Id}.collapsed");
            _isCollapsed = val == "1";
            OnPropertyChanged(nameof(IsCollapsed));
        }
        catch (KeyNotFoundException)
        {
            // No saved state — leave default (expanded).
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
