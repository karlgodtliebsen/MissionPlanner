using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
