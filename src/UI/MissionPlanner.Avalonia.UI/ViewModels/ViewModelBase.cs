using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.Avalonia.UI.ViewModels;

/// <summary>
/// Base class for all view models, providing property change notification and disposal support.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    /// <inheritdoc/>
    public abstract void Dispose();
}
