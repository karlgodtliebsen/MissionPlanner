using Avalonia.Controls;

namespace MissionPlanner.Avalonia.UI.Utilities;

/// <summary>
/// A base class for views that are associated with a specific view model.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
public abstract class ViewBase<TViewModel> : UserControl where TViewModel : class
{
    /// <inheritdoc />
    protected ViewBase()
    {
        DataContext = ServiceHelper.GetRequiredService<TViewModel>();
    }

    /// <summary>
    /// Gets the view model associated with this view.
    /// </summary>
    protected TViewModel ViewModel => (TViewModel)DataContext!;
}