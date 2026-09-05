using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Utilities;

public partial class ContentViewBase<TViewModel> : ContentPage where TViewModel : ViewModelBase
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
    protected ILogger Logger;


    /// <inheritdoc />
    public ContentViewBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<UserControlViewBase>>();

    }
}
