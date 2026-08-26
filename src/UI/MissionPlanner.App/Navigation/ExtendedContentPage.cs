using System.Diagnostics;
using MissionPlanner.App.Helpers;
using UraniumUI.Material.TabViews;
using UraniumUI.Pages;

namespace MissionPlanner.App.Navigation;

/// <summary>Associates a content page with a view model and serializes its lifecycle.</summary>
public class ExtendedContentPage<TViewModel> : UraniumContentPage, IDisposable
    where TViewModel : class, IDisposable, IActivationLifeCycle
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private LifecycleState lifecycleState;
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
    }

    /// <summary>Initializes the page.</summary>
    protected ExtendedContentPage()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
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
        // Navigation-type behavior is intentionally unchanged pending verification.
        if (args.NavigationType is not (NavigationType.Replace or NavigationType.Remove))
        {
            return;
        }

        await RunNavigationTransitionAsync(ActivateAsync).ConfigureAwait(true);
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
        await lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (disposed || lifecycleState is LifecycleState.Active or LifecycleState.Faulted)
            {
                return;
            }

            lifecycleState = LifecycleState.Activating;
            try
            {
                await Task.Yield();
                await OnActivateAsync().ConfigureAwait(true);
                lifecycleState = LifecycleState.Active;
                await Task.Yield();
            }
            catch
            {
                lifecycleState = LifecycleState.Faulted;
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    /// <summary>Runs the complete serialized deactivation transition.</summary>
    protected async Task DeactivateAsync()
    {
        await lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (disposed || lifecycleState is LifecycleState.Inactive or LifecycleState.Deactivating)
            {
                return;
            }

            var preserveFault = lifecycleState == LifecycleState.Faulted;
            lifecycleState = LifecycleState.Deactivating;
            try
            {
                await OnDeactivateAsync().ConfigureAwait(true);
                lifecycleState = preserveFault ? LifecycleState.Faulted : LifecycleState.Inactive;
            }
            catch
            {
                lifecycleState = LifecycleState.Faulted;
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
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
