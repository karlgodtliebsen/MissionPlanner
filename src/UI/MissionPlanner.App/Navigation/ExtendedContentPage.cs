using System.Diagnostics;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Views.Common;
using UraniumUI.Material.TabViews;
using UraniumUI.Pages;

namespace MissionPlanner.App.Navigation;

/// <summary>Associates a content page with a view model and serializes its lifecycle.</summary>
public class ExtendedContentPage<TViewModel> : UraniumContentPage, IDisposable
    where TViewModel : class, IDisposable, IActivationLifeCycle
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private LifecycleState lifecycleState;
    private long lifecycleVersion;
    private bool disposed;

    /// <summary>The view model associated with this page.</summary>
    protected TViewModel? ViewModel;

    /// <summary>Initializes the page, optionally using a keyed view model.</summary>
    protected ExtendedContentPage(string? key = null)
    {
        ViewModel = key is not null
            ? ServiceHelper.GetRequiredKeyedService<TViewModel>(key)
            : ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
        SetupStatusBar();
    }

    private void SetupStatusBar()
    {
        if (Content is Grid grid)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var statusBarView = new StatusBarView();
            Grid.SetRow(statusBarView, grid.RowDefinitions.Count - 1);
            Grid.SetColumnSpan(statusBarView, grid.ColumnDefinitions.Count);
            grid.Children.Add(statusBarView);


            var notificationView = new NotificationView
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End
            };

            Grid.SetRow(notificationView, 0);
            Grid.SetColumnSpan(notificationView, grid.ColumnDefinitions.Count);
            grid.Children.Add(notificationView);


        }
    }

    /// <summary>Initializes the page.</summary>
    protected ExtendedContentPage()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
        SetupStatusBar();
    }

    /// <inheritdoc />
    protected override async void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            await RunNavigationTransitionAsync(DeactivateAsync).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            await RunNavigationTransitionAsync(ActivateAsync).ConfigureAwait(true);
        }
    }

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
        BindingContext = null;
    }

    /// <summary>Runs the complete serialized activation transition.</summary>
    protected async Task ActivateAsync()
    {
        long transitionVersion;
        await lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (disposed || lifecycleState is LifecycleState.Activating or LifecycleState.Active or
                LifecycleState.Deactivating or LifecycleState.Faulted)
            {
                return;
            }

            lifecycleState = LifecycleState.Activating;
            transitionVersion = ++lifecycleVersion;
        }
        finally
        {
            lifecycleGate.Release();
        }

        try
        {
            await Task.Yield();
            await OnActivateAsync().ConfigureAwait(true);

            await lifecycleGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (!disposed && lifecycleState == LifecycleState.Activating && lifecycleVersion == transitionVersion)
                {
                    lifecycleState = LifecycleState.Active;
                }
            }
            finally
            {
                lifecycleGate.Release();
            }
        }
        catch
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (!disposed && lifecycleState == LifecycleState.Activating && lifecycleVersion == transitionVersion)
                {
                    lifecycleState = LifecycleState.Faulted;
                }
            }
            finally
            {
                lifecycleGate.Release();
            }

            throw;
        }
    }

    /// <summary>Runs the complete serialized deactivation transition.</summary>
    protected async Task DeactivateAsync()
    {
        var preserveFault = false;
        long transitionVersion;
        await lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (disposed || lifecycleState is LifecycleState.Inactive or LifecycleState.Deactivating)
            {
                return;
            }

            preserveFault = lifecycleState == LifecycleState.Faulted;
            lifecycleState = LifecycleState.Deactivating;
            transitionVersion = ++lifecycleVersion;
        }
        finally
        {
            lifecycleGate.Release();
        }


        try
        {
            // Do not wait for an in-flight activation to finish. Deactivation is
            // responsible for cancelling it as soon as navigation starts.
            await OnDeactivateAsync().ConfigureAwait(true);

            await lifecycleGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (!disposed && lifecycleState == LifecycleState.Deactivating && lifecycleVersion == transitionVersion)
                {
                    lifecycleState = preserveFault ? LifecycleState.Faulted : LifecycleState.Inactive;
                }
            }
            finally
            {
                lifecycleGate.Release();
            }
        }
        catch
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (!disposed)
                {
                    lifecycleState = LifecycleState.Faulted;
                }
            }
            finally
            {
                lifecycleGate.Release();
            }

            throw;
        }
    }

    /// <summary>Performs page-specific activation under the lifecycle gate.</summary>
    protected virtual Task OnActivateAsync()
    {
        return ViewModel?.ActivateAsync() ?? Task.CompletedTask;
    }

    /// <summary>Performs page-specific deactivation under the lifecycle gate.</summary>
    protected virtual Task OnDeactivateAsync()
    {
        return ViewModel?.DeactivateAsync() ?? Task.CompletedTask;
    }

    private static async Task RunNavigationTransitionAsync(Func<Task> transition)
    {
        try
        {
            await transition().ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            Debug.WriteLine($"Page lifecycle transition was cancelled: {exception}");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Fatal page lifecycle transition failure: {exception}");
        }
    }

    private enum LifecycleState
    {
        Inactive, Activating, Active, Deactivating, Faulted
    }
}
