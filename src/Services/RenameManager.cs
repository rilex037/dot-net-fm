using System.Windows.Threading;

namespace DotNetFM;

public sealed class RenameManager
{
    private readonly DispatcherTimer _timer;
    private FolderItem? _pendingItem;

    public FolderItem? ActiveEditingItem { get; private set; }

    public event Action<FolderItem>? RenameReady;

    public RenameManager()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;
    }

    public void StartPending(FolderItem item)
    {
        CancelPending();
        _pendingItem = item;
        _timer.Start();
    }

    public void CancelPending()
    {
        _timer.Stop();
        _pendingItem = null;
    }

    public void BeginRename(FolderItem item)
    {
        ActiveEditingItem = item;
        item.EditName = item.Name;
        item.IsEditing = true;
    }

    public bool CommitActive(Action<FolderItem>? onCommitted)
    {
        if (ActiveEditingItem == null || !ActiveEditingItem.IsEditing)
        {
            ClearActive();
            return false;
        }

        onCommitted?.Invoke(ActiveEditingItem);
        ClearActive();
        return true;
    }

    public void ClearActive()
    {
        if (ActiveEditingItem != null)
        {
            ActiveEditingItem.IsEditing = false;
            ActiveEditingItem = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_pendingItem != null && _pendingItem.IsSelected)
        {
            RenameReady?.Invoke(_pendingItem);
        }

        _pendingItem = null;
    }
}
