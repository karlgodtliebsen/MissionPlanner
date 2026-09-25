using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
/// <summary>
/// ViewModel for configuring the ESP8266 optional hardware.
/// </summary>
/// <param name="logger"></param>
/// <param name="navigation">The application navigation service.</param>
public sealed partial class Esp8266SetupViewModel(ILogger<Esp8266SetupViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private void Refresh()
    {
        try
        {
            Validate();
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    /// <summary>Opens the shared Full Parameters workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    /// <summary>
    /// The component ID for the UDP bridge.
    /// </summary>
    public const byte UdpBridgeComponentId = 240;
    [ObservableProperty] public partial string Ssid { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string StationSsid { get; set; } = string.Empty;
    [ObservableProperty] public partial string StationPassword { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool ComponentDetected
    {
        get; set;
    }
    /// <summary>
    /// The target description for the UDP bridge component.
    /// </summary>
    public string Target => $"MAVLink UDP bridge component {UdpBridgeComponentId}";

    [RelayCommand]
    private void Validate()
    {
        _ = PackedParameterStringCodec.Encode(Ssid);
        _ = PackedParameterStringCodec.Encode(Password);
        var msg = ComponentDetected ? "Settings are valid and ready for explicit component-targeted Apply." : "Waiting for UDP bridge component discovery.";
        SetMessages(msg, null);
    }
}

