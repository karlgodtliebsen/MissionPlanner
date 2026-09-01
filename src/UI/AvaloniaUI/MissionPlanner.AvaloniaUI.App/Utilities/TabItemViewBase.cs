using AsyncAwaitBestPractices;
using Avalonia.Interactivity;

namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>
/// A base class for tab item views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public class TabItemViewBase<TViewModel> : UserControlViewBase<TViewModel> where TViewModel : ViewModelBase
{
    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ViewModel.ActivateAsync().SafeFireAndForget();
    }


    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ViewModel.DeactivateAsync().SafeFireAndForget();
        base.OnUnloaded(e);
    }
}
/// <summary>
/// A base class for tab item views that are associated with a specific view model.
/// </summary>
public class TabItemViewBase : UserControlViewBase
{
}
