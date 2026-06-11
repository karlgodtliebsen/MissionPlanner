using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Options;

using MissionPlanner.Configuration;

namespace MissionPlanner.Views.Connect;

public partial class ConnectPopupViewModel : ObservableObject
{
    private readonly IOptions<ApplicationState> applicationState;
    private readonly IOptions<ApplicationOptions> applicationOptions;

    [ObservableProperty] private string? selectedConnectionType;

    [ObservableProperty] private string? selectedPort;

    [ObservableProperty] private string? selectedBaudRate;

    [ObservableProperty] private bool isConnected;

    /// <summary>
    /// Data for the connect popup, such as available connection types, ports, and baud rates.
    /// </summary>
    public ApplicationOptions Options { get; set; }

    public ConnectPopupViewModel()
    {
        SelectedConnectionType = "Serial";
        SelectedPort = "AUTO";
        SelectedBaudRate = "115200";
    }

    public ConnectPopupViewModel(IOptions<ApplicationState> applicationState, IOptions<ApplicationOptions> applicationOptions) : this()
    {
        this.applicationState = applicationState;
        this.applicationOptions = applicationOptions;
        Options = applicationOptions.Value;

        SelectedConnectionType = applicationState.Value.SelectedConnectionType;
        SelectedPort = applicationState.Value.SelectedPort;
        SelectedBaudRate = applicationState.Value.SelectedBaudRate;
    }


    [RelayCommand]
    private void Connect()
    {
        // TODO: wire up to the comms/connection layer
        IsConnected = !IsConnected;
        applicationState.Value.IsConnected = IsConnected;
    }
}