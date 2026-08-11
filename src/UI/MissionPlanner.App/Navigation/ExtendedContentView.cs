using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a content view that is associated with a specific view model type.
/// Used to Enforce Cleanup when Navigating away from a view. The view model is automatically disposed when the view is disposed.
/// </summary>
/// <typeparam name="TViewModel"></typeparam>
public class ExtendedContentView<TViewModel> : ContentView where TViewModel : class, IDisposable
{
    /// <summary>
    ///  
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// 
    /// </summary>
    protected ExtendedContentView()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentView{TViewModel}"/> class.
    /// </summary>
    protected ExtendedContentView(string key)
    {
        ViewModel = ServiceHelper.GetRequiredKeyedService<TViewModel>(key);
        BindingContext = ViewModel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentView{TViewModel}"/> class with the specified view model.
    /// </summary>
    /// <param name="viewModel">The view model to associate with the view.</param>
    protected ExtendedContentView(TViewModel viewModel)
    {
        ViewModel = viewModel;
        BindingContext = ViewModel;
    }

    /// <summary>
    /// Disposes the resources used by the view and its associated view model.     
    /// </summary>
    public virtual void Dispose()
    {
        BindingContext = null;
        ViewModel?.Dispose();
        ViewModel = null;
    }
}
