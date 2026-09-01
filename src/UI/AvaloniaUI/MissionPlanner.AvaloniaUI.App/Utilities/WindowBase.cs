using Microsoft.Extensions.Logging;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// A base class for views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class WindowBase<TViewModel> : UrsaWindow where TViewModel : class
{
    /// <summary>
    /// The logger instance used for logging within the TabViewLifecycleContent class. 
    /// </summary>
    protected ILogger Logger;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public WindowBase()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }
}

public partial class WindowBase : UrsaWindow
{
    /// <summary>
    /// The logger instance used for logging within the TabViewLifecycleContent class. 
    /// </summary>
    protected ILogger Logger;

    /// <inheritdoc />
    public WindowBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<UserControlViewBase>>();
    }
}
