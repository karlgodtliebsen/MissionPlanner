using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MissionPlanner.App.Utilities;

/// <summary>Base class for content pages that resolve their view model from the service container.</summary>
public partial class ContentViewBase<TViewModel> : ContentPage where TViewModel : ViewModelBase
{
    /// <summary>The logger of the page.</summary>
    protected ILogger Logger = NullLogger.Instance;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    } = null!;

    /// <inheritdoc />
    public ContentViewBase()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }

    /// <summary>Activates the view model when the page is loaded.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ViewModel?.ActivateAsync().SafeFireAndForget();
    }


    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ViewModel?.DeactivateAsync().SafeFireAndForget();
        base.OnUnloaded(e);
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
    protected ILogger Logger = NullLogger.Instance;


    /// <inheritdoc />
    public ContentViewBase()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        Logger = ServiceHelper.GetRequiredService<ILogger<UserControlViewBase>>();

    }
}
