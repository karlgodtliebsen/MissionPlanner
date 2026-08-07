using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a content view that is associated with a specific view model type.
/// </summary>
/// <typeparam name="TViewModel"></typeparam>
public class ExtendedContentView<TViewModel> : ContentView where TViewModel : class, IDisposable
{
    /// <summary>
    ///  
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentView{TViewModel}"/> class.
    /// </summary>
    protected ExtendedContentView()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
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
