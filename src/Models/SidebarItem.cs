using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DotNetFM;

public class SidebarItem
{
    public Dictionary<string, string> SidebarIcons { get; set; } = new();

    public class Item : INotifyPropertyChanged
    {
        private string _name = "";
        private string _iconPath = "";
        private string _path = "";
        private bool _isHint;

        public object? Tag { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("Icon")]
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

        public bool IsHint
        {
            get => _isHint;
            set { _isHint = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
