using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Presentation state for one firmware operation.</summary>
public sealed partial class FirmwareProgressViewModel : ObservableObject
{
    [ObservableProperty] public partial string Stage { get; set; } = "Ready";
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool HasPercentage { get; set; }
    [ObservableProperty] public partial bool IsPowerCritical { get; set; }
    [ObservableProperty] public partial string? TechnicalDetail { get; set; }
}
