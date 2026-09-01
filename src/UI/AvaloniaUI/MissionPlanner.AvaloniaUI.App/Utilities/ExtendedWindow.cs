using AsyncAwaitBestPractices;
using Avalonia.Interactivity;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>Associates a content page with a view model and serializes its lifecycle.</summary>
public class ExtendedWindow<TViewModel> : UrsaWindow, IDisposable
    where TViewModel : ViewModelBase, IDisposable//, IActivationLifeCycle
{
    //private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    //private LifecycleState lifecycleState;
    //private long lifecycleVersion;
    private bool disposed;

    /// <summary>The view model associated with this Window.</summary>
    protected TViewModel? ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ViewModel?.ActivateAsync().SafeFireAndForget();
    }


    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ViewModel?.DeactivateAsync().SafeFireAndForget();
        base.OnUnloaded(e);
    }

    /// <summary>Initializes the page, optionally using a keyed view model.</summary>
    protected ExtendedWindow(string? key = null)
    {
        ViewModel = key is not null
            ? ServiceHelper.GetRequiredKeyedService<TViewModel>(key)
            : ServiceHelper.GetRequiredService<TViewModel>();
        DataContext = ViewModel;
    }

    /// <summary>Initializes the page.</summary>
    protected ExtendedWindow()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        DataContext = ViewModel;
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
        DataContext = null;
    }


}
