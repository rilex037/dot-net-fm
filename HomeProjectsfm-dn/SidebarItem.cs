using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FmDn;

public class SidebarItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _iconPath = "";
    private string _path = "";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string IconPath
    {
        get => _iconPath;
        set { _iconPath = value; OnPropertyChanged(); }
    }

    public string Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
