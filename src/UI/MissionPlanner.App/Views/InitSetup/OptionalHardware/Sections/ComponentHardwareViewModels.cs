using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class DroneCanUavCanViewModel(IDroneCanService service, ILogger<DroneCanUavCanViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
    public IReadOnlyList<DroneCanTransportKind> TransportKinds { get; } = Enum.GetValues<DroneCanTransportKind>();
    public ObservableCollection<DroneCanNode> Nodes { get; } = [];
    [ObservableProperty]
    public partial DroneCanTransportKind TransportKind
    {
        get; set;
    }
    [ObservableProperty]
    public partial DroneCanNode? SelectedNode
    {
        get; set;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await service.ConnectAsync(TransportKind, CancellationToken.None);
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var nodes = await service.DiscoverAsync(CancellationToken.None);
            await Dispatcher.DispatchAsync(() => { Nodes.Clear(); foreach (var node in nodes) { Nodes.Add(node); } });
            StatusMessage = $"{Nodes.Count} DroneCAN v0 node(s) discovered.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
    public override void Dispose()
    {
        _ = service.DisposeAsync();
        base.Dispose();
    }
}

