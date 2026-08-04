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
    protected TViewModel? ViewModel;

    /// <summary>
    /// 
    /// </summary>
    public virtual void Activate()
    {
        ViewModel?.Dispose();
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual void Deactivate()
    {
        ViewModel?.Dispose();
        ViewModel = null;
    }
}
