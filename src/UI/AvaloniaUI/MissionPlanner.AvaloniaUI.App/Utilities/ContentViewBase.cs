using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

public partial class ContentViewBase<TViewModel> : ContentPage where TViewModel : class
{
    protected ILogger Logger;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public ContentViewBase()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }
}



/// <summary>
/// A base class for views that are not associated with a specific view model.
/// </summary>
public partial class ContentViewBase : ContentPage
{
    /// <summary>
    /// The logger instance used for logging within the ContentViewBase class. 
    /// </summary>
    protected ILogger Logger;


    /// <inheritdoc />
    public ContentViewBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<UserControlViewBase>>();

    }
}
