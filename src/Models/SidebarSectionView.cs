using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetFM;

public class SidebarSectionView : INotifyPropertyChanged
{
    private bool _isCollapsed;

    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public ObservableCollection<SidebarItem.Item> Items { get; set; } = new();

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            OnPropertyChanged();

            if (!string.IsNullOrEmpty(Id))
                AppStore.Write($"sidebar.{Id}.collapsed", value ? "1" : "0");
        }
    }

    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    public void RestoreCollapsedState()
    {
        if (string.IsNullOrEmpty(Id)) return;

        _isCollapsed = AppStore.Read($"sidebar.{Id}.collapsed") == "1";
        OnPropertyChanged(nameof(IsCollapsed));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
