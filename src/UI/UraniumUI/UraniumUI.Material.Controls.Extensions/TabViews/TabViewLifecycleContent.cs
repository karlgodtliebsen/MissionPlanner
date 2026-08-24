namespace UraniumUI.Material.TabViews;

/// <summary>
/// A ContentView that participates in the lifecycle of a TabView.
/// </summary>
public class TabViewLifecycleContent<TViewModel> : ContentView, IActivationLifeCycle
    where TViewModel : class, IDisposable, IActivationLifeCycle
{
    /// <summary>
    /// The view model associated with this ContentView.
    /// </summary>
    protected TViewModel? ViewModel;

    /// <inheritdoc />
    public TabViewLifecycleContent()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual async Task ActivateAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ActivateAsync();
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual async Task DeactivateAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.DeactivateAsync();
    }
}
