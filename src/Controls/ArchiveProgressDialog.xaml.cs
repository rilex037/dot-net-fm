using System.Windows;
using System.Windows.Input;

namespace DotNetFM;

public sealed record ArchiveOutcome(bool Succeeded, bool Cancelled, string? ErrorMessage);

public partial class ArchiveProgressDialog : Window
{
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _done;
    private bool _shown;

    public CancellationToken Token => _cts.Token;

    private ArchiveProgressDialog(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
        StatusText.Text = "Preparing…";
        Closing += (_, e) =>
        {
            if (!_done)
            {
                e.Cancel = true;
                RequestCancel();
            }
        };
    }

    public static ArchiveOutcome Run(Window? owner, string title,
        Func<IProgress<ArchiveProgress>, CancellationToken, Task> work)
    {
        var dialog = new ArchiveProgressDialog(title);
        if (owner != null) dialog.Owner = owner;

        var progress = new Progress<ArchiveProgress>(dialog.Report);
        var task = Task.Run(() => work(progress, dialog.Token));

        task.ContinueWith(
            _ => dialog.SignalDone(),
            TaskScheduler.FromCurrentSynchronizationContext());

        dialog.ShowDialog();
        dialog.DisposeCts();

        if (task.IsCanceled) return new ArchiveOutcome(false, true, null);
        if (task.IsFaulted) return new ArchiveOutcome(false, false, Flatten(task.Exception));
        return new ArchiveOutcome(true, false, null);
    }

    private void SignalDone()
    {
        _done = true;
        if (_shown) Close();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _shown = true;
        if (_done) Close();
    }

    private void Report(ArchiveProgress value)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Report(value));
            return;
        }

        if (value.CurrentItem.Length > 0)
            StatusText.Text = value.CurrentItem;

        if (value.Indeterminate || value.Total <= 0)
        {
            Bar.IsIndeterminate = true;
        }
        else
        {
            Bar.IsIndeterminate = false;
            Bar.Maximum = value.Total;
            Bar.Value = Math.Min(value.Completed, value.Total);
        }
    }

    private void RequestCancel()
    {
        try { _cts.Cancel(); }
        catch { }

        CancelButton.IsEnabled = false;
        CancelButton.Content = "Cancelling…";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => RequestCancel();

    private void DisposeCts()
    {
        try { _cts.Dispose(); }
        catch { }
    }

    private static string Flatten(Exception? exception)
    {
        if (exception == null) return "";
        if (exception is AggregateException aggregate)
        {
            var messages = aggregate.InnerExceptions.Select(Flatten).Where(m => m.Length > 0).ToArray();
            return messages.Length > 0 ? string.Join("\n", messages) : aggregate.Message;
        }
        return exception.Message;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
