using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// A base class for views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public partial class UserControlViewBase<TViewModel> : UserControl where TViewModel : ViewModelBase
{
    /// <summary>
    /// The logger instance used for logging within the UserControlViewBase class. 
    /// </summary>
    protected ILogger Logger;

    /// <summary>The view model associated with this View.</summary>
    protected TViewModel ViewModel
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public UserControlViewBase()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        Logger = ServiceHelper.GetRequiredService<ILogger<TViewModel>>();
        DataContext = ViewModel;
    }
    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        NotificationHelper.SetupManagers(this, ViewModel);
        ViewModel?.ActivateAsync().SafeFireAndForget();
    }


    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ViewModel?.DeactivateAsync().SafeFireAndForget();
        base.OnUnloaded(e);
    }
}


/// <inheritdoc/>
public partial class UserControlViewBase : UserControl
{
    /// <summary>
    /// The logger instance used for logging within the UserControlViewBase class. 
    /// </summary>
    protected ILogger Logger;

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is not ViewModelBase viewModel)
        {
            return;
        }

        NotificationHelper.SetupManagers(this, viewModel);
    }



    /// <inheritdoc />
    public UserControlViewBase()
    {
        Logger = ServiceHelper.GetRequiredService<ILogger<UserControlViewBase>>();
    }
}
