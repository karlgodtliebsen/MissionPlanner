namespace MissionPlanner.App.Utilities;

/// <summary>
/// A base class for tab item views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public class TabItemViewBase<TViewModel> : UserControlViewBase<TViewModel> where TViewModel : ViewModelBase
{

    // UserControlViewBase owns activation and deactivation. Calling them here too
    // duplicates telemetry subscriptions every time a tab moves between windows.
}
/// <summary>
/// A base class for tab item views that are associated with a specific view model.
/// </summary>
public class TabItemViewBase : UserControlViewBase
{
}
