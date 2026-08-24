using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class Esp8266SetupViewModel(ILogger<Esp8266SetupViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
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
    public string Target => $"MAVLink UDP bridge component {UdpBridgeComponentId}";
    [RelayCommand]
    private void Validate()
    {
        _ = PackedParameterStringCodec.Encode(Ssid);
        _ = PackedParameterStringCodec.Encode(Password);
        StatusMessage = ComponentDetected ? "Settings are valid and ready for explicit component-targeted Apply." : "Waiting for UDP bridge component discovery.";
    }
    public override void Dispose()
    {
        Password = string.Empty;
        StationPassword = string.Empty;
        base.Dispose();
    }
}
