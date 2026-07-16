using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Configuration;

/// <summary>
/// Singleton service for managing shared application state across the application.
/// </summary>
public partial class ApplicationStateService : ObservableObject
{
    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string SelectedChannel { get; set; } = "AUTO";
    [ObservableProperty] public partial string SelectedBaudRate { get; set; } = "115200";
    [ObservableProperty] public partial string SelectedPort { get; set; } = "14550";
    [ObservableProperty] public partial string SelectedHost { get; set; } = "127.0.0.1";
    [ObservableProperty] public partial string? VehicleName { get; set; }
    [ObservableProperty] public partial VehicleId? VehicleId { get; set; }

    /// <summary>
    /// Initializes the service with values from ApplicationState.
    /// </summary>
    public void Initialize(ApplicationState state)
    {
        IsConnected = state.IsConnected;
        SelectedChannel = state.SelectedChannel;
        SelectedBaudRate = state.SelectedBaudRate;
        SelectedPort = state.SelectedPort;
        SelectedHost = state.SelectedHost;
        VehicleName = null;
        VehicleId = null;
    }
}
