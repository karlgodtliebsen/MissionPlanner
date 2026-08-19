using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class DroneCanUavCanViewModel(IDroneCanService service) : OptionalHardwareBaseViewModel
{
    public IReadOnlyList<DroneCanTransportKind> TransportKinds { get; } = Enum.GetValues<DroneCanTransportKind>();
    public ObservableCollection<DroneCanNode> Nodes { get; } = [];
    [ObservableProperty] public partial DroneCanTransportKind TransportKind { get; set; }
    [ObservableProperty] public partial DroneCanNode? SelectedNode { get; set; }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try { IsBusy = true; ErrorMessage = string.Empty; await service.ConnectAsync(TransportKind, CancellationToken.None); await RefreshAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var nodes = await service.DiscoverAsync(CancellationToken.None);
            await Dispatcher.DispatchAsync(() => { Nodes.Clear(); foreach (var node in nodes) Nodes.Add(node); });
            StatusMessage = $"{Nodes.Count} DroneCAN v0 node(s) discovered.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
    public override void Dispose() { _ = service.DisposeAsync(); base.Dispose(); }
}

public sealed partial class CubeIdUpdateViewModel : OptionalHardwareBaseViewModel
{
    [ObservableProperty] public partial string FirmwarePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirmwareSummary { get; set; } = "Select a local .bin image. Updating remains disabled until a CubeID component is detected.";
    [ObservableProperty] public partial bool TargetDetected { get; set; }
    [RelayCommand]
    private async Task InspectAsync()
    {
        try
        {
            var data = await File.ReadAllBytesAsync(FirmwarePath);
            if (!FirmwarePath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("CubeID firmware must be a .bin file.");
            FirmwareSummary = $"{Path.GetFileName(FirmwarePath)} — {data.Length:N0} bytes — CRC32 {CubeFirmwareCodec.Crc32(data):X8} — {CubeFirmwareCodec.Chunk(data).Count} chunks";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}

public sealed partial class Esp8266SetupViewModel : OptionalHardwareBaseViewModel
{
    public const byte UdpBridgeComponentId = 240;
    [ObservableProperty] public partial string Ssid { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string StationSsid { get; set; } = string.Empty;
    [ObservableProperty] public partial string StationPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ComponentDetected { get; set; }
    public string Target => $"MAVLink UDP bridge component {UdpBridgeComponentId}";
    [RelayCommand]
    private void Validate()
    {
        _ = PackedParameterStringCodec.Encode(Ssid);
        _ = PackedParameterStringCodec.Encode(Password);
        StatusMessage = ComponentDetected ? "Settings are valid and ready for explicit component-targeted Apply." : "Waiting for UDP bridge component discovery.";
    }
    public override void Dispose() { Password = string.Empty; StationPassword = string.Empty; base.Dispose(); }
}
