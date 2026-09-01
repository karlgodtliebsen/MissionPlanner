using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;

public sealed partial class ProgressDialogViewModel : ObservableObject, IDisposable
{
    private readonly Func<string> messageProvider;
    private readonly DispatcherTimer timer;

    public ProgressDialogViewModel(Func<string> messageProvider)
    {
        this.messageProvider = messageProvider;
        Message = messageProvider();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Message = messageProvider());
        timer.Start();
    }

    [ObservableProperty]
    public partial string Message
    {
        get; private set;
    }

    public void Dispose()
    {
        timer.Stop();
    }
}
