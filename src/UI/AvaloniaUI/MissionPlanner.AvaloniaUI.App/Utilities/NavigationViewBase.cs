using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// A base class for views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class NavigationViewBase<TViewModel> : NavigationPage where TViewModel : class
{
    /// <summary>
    /// The logger instance used for logging within the NavigationViewBase class. 
    /// </summary>
    protected ILogger Logger;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public NavigationViewBase()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }
}

public partial class NavigationViewBase : NavigationPage
{
    /// <summary>
    /// The logger instance used for logging within the NavigationViewBase class. 
    /// </summary>
    protected ILogger Logger;


    /// <inheritdoc />
    public NavigationViewBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<NavigationViewBase>>();

    }
}
