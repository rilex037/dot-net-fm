using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace DotNetFM;

public sealed class FolderItem : INotifyPropertyChanged, IDisposable
{
    private string _name = "";
    private string _itemCount = "";
    private string _fullPath = "";
    private BitmapSource? _nativeIcon;
    private bool _isFolder = true;
    private bool _isEditing;
    private string _editName = "";
    private bool _isSelected;
    private DateTime _sortDate;
    private long _sortSize;
    private string _sortType = "";

    private CancellationTokenSource? _iconCts;
    private bool _disposed;

    public IIconProvider? IconProvider { get; set; }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string ItemCount
    {
        get => _itemCount;
        set { _itemCount = value; OnPropertyChanged(); }
    }

    public string FullPath
    {
        get => _fullPath;
        set { _fullPath = value; OnPropertyChanged(); }
    }

    public BitmapSource? NativeIcon
    {
        get => _nativeIcon;
        set { _nativeIcon = value; OnPropertyChanged(); }
    }

    public bool IsFolder
    {
        get => _isFolder;
        set { _isFolder = value; OnPropertyChanged(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public string EditName
    {
        get => _editName;
        set { _editName = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public DateTime SortDate
    {
        get => _sortDate;
        set { _sortDate = value; }
    }

    public long SortSize
    {
        get => _sortSize;
        set { _sortSize = value; }
    }

    public string SortType
    {
        get => _sortType;
        set { _sortType = value; }
    }

    public void CancelIconLoad()
    {
        _iconCts?.Cancel();
        _iconCts?.Dispose();
        _iconCts = null;
    }

    public async void LoadIconAsync(CancellationToken navigationToken = default, int requestedSize = 256)
    {
        if (_disposed || navigationToken.IsCancellationRequested || IconProvider == null) return;

        _iconCts?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(navigationToken);
        _iconCts = cts;
        var token = cts.Token;

        try
        {
            var icon = await IconProvider.GetThumbnailAsync(FullPath, requestedSize, token).ConfigureAwait(false);

            if (token.IsCancellationRequested || _disposed) return;
            if (icon != null)
                NativeIcon = icon;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_iconCts, cts))
                _iconCts = null;
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _iconCts?.Cancel();
        _iconCts?.Dispose();
        _iconCts = null;

        _nativeIcon = null;
        OnPropertyChanged(nameof(NativeIcon));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
