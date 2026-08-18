using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Gets the dispatcher for the current context.
    /// </summary>
    public required IDispatcher Dispatcher = ServiceHelper.GetRequiredService<IDispatcher>();

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string ErrorMessage { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; }


    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
