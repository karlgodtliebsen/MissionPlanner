using System.Collections.Specialized;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Application diagnostics content hosted by Logs.</summary>
public partial class ApplicationLogsView : UserControlViewBase<ApplicationLogsViewModel>
{
    /// <summary>Initializes application diagnostics content.</summary>
    public ApplicationLogsView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            ViewModel.Entries.CollectionChanged -= EntriesChanged;
            ViewModel.Entries.CollectionChanged += EntriesChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            ViewModel.Entries.CollectionChanged -= EntriesChanged;
        }

        base.OnUnloaded(e);
    }

    private void EntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.FollowTail && ViewModel.IsLive)
        {
            Dispatcher.UIThread.Post(() => EventsGrid.ScrollToEnd(), DispatcherPriority.Background);
        }
    }
}
