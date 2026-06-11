using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.Configuration;

/// <summary>
/// Singleton service for managing shared application state across the application.
/// </summary>
public partial class ApplicationStateService : ObservableObject
{
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private string selectedPort = "AUTO";
    [ObservableProperty] private string selectedBaudRate = "115200";
    [ObservableProperty] private string selectedConnectionType = "Serial";

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
