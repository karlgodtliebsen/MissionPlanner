using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

/// <summary>Supplies live progress text and owner-controlled cancellation for an overlay.</summary>
public sealed partial class ProgressDialogViewModel : DialogViewModelBase
{
    private readonly Func<string> messageProvider;
    private readonly Action? requestCancellation;
    private readonly DispatcherTimer timer;
    private bool disposed;

    /// <summary>Initializes the progress message and optional deferred-cancellation action.</summary>
    /// <param name="messageProvider">Supplies the current operation message.</param>
    /// <param name="requestCancellation">Requests cancellation without dismissing ongoing work.</param>
    public ProgressDialogViewModel(Func<string> messageProvider, Action? requestCancellation = null)
    {
        ArgumentNullException.ThrowIfNull(messageProvider);
        this.messageProvider = messageProvider;
        this.requestCancellation = requestCancellation;
        Message = messageProvider();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Message = this.messageProvider());
        timer.Start();
    }

    /// <summary>Gets the latest progress message.</summary>
    [ObservableProperty]
    public partial string Message
    {
        get;
        private set;
    }

    /// <summary>Gets whether the operation exposes an explicit cancellation request.</summary>
    public bool CanRequestCancellation => requestCancellation is not null;

    /// <summary>Requests deferred cancellation, or closes ordinary progress.</summary>
    public override void Close()
    {
        if (disposed)
        {
            return;
        }
        if (requestCancellation is not null)
        {
            requestCancellation();
            return;
        }
        base.Close();
    }

    /// <summary>Requests cancellation while retaining progress until its owner completes.</summary>
    public override void Cancel()
    {
        Close();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        timer.Stop();
        base.Dispose();
    }
}
