using Avalonia.Controls;

namespace MissionPlanner.Avalonia.UI.Utilities;

/// <summary>Associates a content page with a view model and serializes its lifecycle.</summary>
public class ExtendedWindow<TViewModel> : Window, IDisposable
    where TViewModel : class, IDisposable//, IActivationLifeCycle
{
    //private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    //private LifecycleState lifecycleState;
    //private long lifecycleVersion;
    private bool disposed;

    /// <summary>The view model associated with this page.</summary>
    protected TViewModel? ViewModel;

    /// <summary>Initializes the page, optionally using a keyed view model.</summary>
    protected ExtendedWindow(string? key = null)
    {
        //ViewModel = key is not null
        //    ? ServiceHelper.GetRequiredKeyedService<TViewModel>(key)
        //    : ServiceHelper.GetRequiredService<TViewModel>();
        //BindingContext = ViewModel;
        SetupStatusBar();
    }

    private void SetupStatusBar()
    {
        if (Content is Grid grid)
        {
            //grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            //grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            //var statusBarView = new StatusBarView();
            //Grid.SetRow(statusBarView, grid.RowDefinitions.Count - 1);
            //Grid.SetColumnSpan(statusBarView, grid.ColumnDefinitions.Count);
            //grid.Children.Add(statusBarView);


            //var notificationView = new NotificationView
            //{
            //    HorizontalOptions = LayoutOptions.End,
            //    VerticalOptions = LayoutOptions.End
            //};

            //Grid.SetRow(notificationView, 0);
            //Grid.SetColumnSpan(notificationView, grid.ColumnDefinitions.Count);
            //grid.Children.Add(notificationView);
        }
    }

    /// <summary>Initializes the page.</summary>
    protected ExtendedWindow()
    {
        //ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        //BindingContext = ViewModel;
        SetupStatusBar();
    }

    ///// <inheritdoc />
    //protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    //{
    //    base.OnNavigatingFrom(args);
    //    if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
    //    {
    //        await RunNavigationTransitionAsync(DeactivateAsync).ConfigureAwait(true);
    //    }
    //}

    ///// <inheritdoc />
    //protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    //{
    //    base.OnNavigatedTo(args);
    //    if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
    //    {
    //        await RunNavigationTransitionAsync(ActivateAsync).ConfigureAwait(true);
    //    }
    //}

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ViewModel?.Dispose();
        ViewModel = null;
        // BindingContext = null;
    }


}