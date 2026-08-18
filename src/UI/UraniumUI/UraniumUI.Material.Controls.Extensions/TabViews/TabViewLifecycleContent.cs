namespace UraniumUI.Material.TabViews;

/// <summary>
/// A ContentView that participates in the lifecycle of a TabView.
/// </summary>
public class TabViewLifecycleContent<TViewModel> : ContentView, ITabViewLifecycleContent where TViewModel : class, IDisposable
{
    /// <summary>
    /// The view model associated with this ContentView.
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// 
    /// </summary>
    public virtual void Activate()
    {
        if (ViewModel is not null)
        {
            return;
        }

        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual void Deactivate()
    {
        if (Dispatcher.IsDispatchRequired)
        {
            Dispatcher.Dispatch(Deactivate);
            return;
        }

        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        ViewModel = null; // Claim it before invoking binding/disposal code.
        BindingContext = null;
        viewModel.Dispose();
    }
}
