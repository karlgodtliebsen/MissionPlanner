
namespace UraniumUI.Material.TabViews;

/// <summary>
/// A ContentView that participates in the lifecycle of a TabView.
/// </summary>
public class TabViewLifecycleContent<TViewModel> : ContentView, IActivationLifeCycle, IDisposable
    where TViewModel : class, IDisposable, IActivationLifeCycle
{
    private bool disposed;
    /// <summary>
    /// The view model associated with this ContentView.
    /// </summary>
    protected TViewModel? ViewModel;

    /// <inheritdoc />
    public TabViewLifecycleContent()
    {
        ViewModel = ServiceProviderHelper.GetRequiredService<TViewModel>();
        ArgumentNullException.ThrowIfNull(ViewModel);
        BindingContext = ViewModel;
    }

    /// <summary>
    /// Activates the view model associated with this ContentView.
    /// </summary>
    public virtual async Task ActivateAsync()
    {
        ArgumentNullException.ThrowIfNull(ViewModel);
        await ViewModel.ActivateAsync();
    }

    /// <summary>
    /// Deactivates the view model associated with this ContentView.
    /// </summary>
    public virtual async Task DeactivateAsync()
    {
        ArgumentNullException.ThrowIfNull(ViewModel);
        await ViewModel.DeactivateAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        BindingContext = null;
        ViewModel?.Dispose();
        ViewModel = null;
    }
}
