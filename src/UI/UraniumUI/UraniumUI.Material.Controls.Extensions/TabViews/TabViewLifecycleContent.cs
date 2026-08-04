namespace UraniumUI.Material.TabViews;

/// <summary>
/// 
/// </summary>
public interface ITabViewLifecycleContent
{
    /// <summary>
    /// 
    /// </summary>
    void Activate();

    /// <summary>
    /// 
    /// </summary>
    void Deactivate();
}

/// <summary>
/// A ContentView that participates in the lifecycle of a TabView.
/// </summary>
public class TabViewLifecycleContent<TViewModel> : ContentView, ITabViewLifecycleContent where TViewModel : class, IDisposable
{
    private TViewModel? viewModel;

    /// <summary>
    /// 
    /// </summary>
    public virtual void Activate()
    {
        viewModel?.Dispose();
        viewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = viewModel;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual void Deactivate()
    {
        viewModel?.Dispose();
        viewModel = null;
    }
}
