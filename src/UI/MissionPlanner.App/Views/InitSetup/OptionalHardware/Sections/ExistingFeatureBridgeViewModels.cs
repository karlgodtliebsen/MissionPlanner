using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Links Optional Hardware to the authoritative Onboard OSD configuration workspace.</summary>
public sealed partial class OnboardOsdBridgeViewModel(INavigationService navigation) : ObservableObject, IDisposable
{
    /// <summary>Opens the existing OSD editor.</summary>
    [RelayCommand]
    private Task OpenOnboardOsdAsync() => navigation.OpenSubViewAsync("Config", "Onboard OSD");

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>Provides metadata-backed camera/gimbal setup and links to existing live payload control.</summary>
public sealed partial class CameraGimbalViewModel(
    IActiveVehicleContext activeVehicle,
    IOptionalHardwareService service,
    INavigationService navigation)
    : ParameterHardwareViewModel("camera-gimbal", activeVehicle, service)
{
    /// <summary>Opens the existing Flight Data workspace that owns live payload control.</summary>
    [RelayCommand]
    private Task OpenPayloadControlAsync() => navigation.OpenPageAsync("Flight Data");
}
