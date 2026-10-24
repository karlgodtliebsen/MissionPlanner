using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Links Optional Hardware to the authoritative Onboard OSD configuration workspace.</summary>
public sealed partial class OnboardOsdBridgeViewModel(INavigationService navigation, ILogger<OnboardOsdBridgeViewModel> logger, IActiveVehicleContext vehicle, IOptionalHardwareService hardware)
    : ParameterHardwareViewModel("external-osd", vehicle, hardware, logger, navigation)
{
    /// <summary>Gets or sets the explicitly selected MAVLink stream channel.</summary>
    [ObservableProperty]
    public partial int StreamChannel { get; set; }

    [RelayCommand]
    private void StageExternalOsdPreset()
    {
        var prefix = $"SR{StreamChannel}_";
        var selected = Settings.Where(setting => setting.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        foreach (var setting in selected)
        {
            setting.Editor.NumericValue = 2;
        }
        SetMessages($"Staged the historical 2 Hz external-OSD preset for {selected.Length} reported {prefix} settings. Review and apply the individual rows. No vehicle writes have occurred.");
    }

    /// <summary>Opens the existing OSD editor.</summary>
    [RelayCommand]
    private Task OpenOnboardOsdAsync()
    {
        return Navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationOnboardOsd);
    }
}
