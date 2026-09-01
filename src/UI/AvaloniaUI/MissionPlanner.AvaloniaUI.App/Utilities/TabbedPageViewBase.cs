using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// A base class for views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class TabbedPageViewBase<TViewModel> : TabbedPage where TViewModel : ViewModelBase
{
    /// <summary>
    /// The logger instance used for logging within the TabbedPageViewBase class. 
    /// </summary>
    protected ILogger Logger;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public TabbedPageViewBase()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }

    /// <inheritdoc/>
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
public partial class TabbedPageViewBase : TabbedPage
{
    /// <summary>
    /// The logger instance used for logging within the TabbedPageViewBase class. 
    /// </summary>
    protected ILogger Logger;
    /// <summary>
    /// 
    /// </summary>
    public TabbedPageViewBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<TabbedPageViewBase>>();
    }
}
