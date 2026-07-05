using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Configuration;

/// <summary>
/// Singleton service for managing shared application state across the application.
/// </summary>
public partial class ApplicationStateService : ObservableObject
{
    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string SelectedPort { get; set; } = "AUTO";
    [ObservableProperty] public partial string SelectedBaudRate { get; set; } = "115200";
    [ObservableProperty] public partial string SelectedConnectionType { get; set; } = "Serial";

    /// <summary>
    /// Initializes the service with values from ApplicationState.
    /// </summary>
    public void Initialize(ApplicationState state)
    {
        IsConnected = state.IsConnected;
        SelectedPort = state.SelectedPort;
        SelectedBaudRate = state.SelectedBaudRate;
        SelectedConnectionType = state.SelectedConnectionType;
    }
}
