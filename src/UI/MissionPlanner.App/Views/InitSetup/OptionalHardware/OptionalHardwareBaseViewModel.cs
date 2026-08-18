using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string Error { get; set; }

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
