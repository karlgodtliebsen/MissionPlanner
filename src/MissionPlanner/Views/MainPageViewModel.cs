using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.Options;

using MissionPlanner.Configuration;

namespace MissionPlanner.Views;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IOptions<ApplicationState> applicationState;

    [ObservableProperty] private string? selectedConnectionType;
    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private string? selectedBaudRate;
    [ObservableProperty] private bool isConnected;

    /// <summary>
    /// Data for the connect popup, such as available connection types, ports, and baud rates.
    /// </summary>
    public ApplicationState State { get; set; }

    public MainPageViewModel()
    {
        SelectedConnectionType = "Serial";
        SelectedPort = "AUTO";
        SelectedBaudRate = "115200";
    }

    public MainPageViewModel(IOptions<ApplicationState> applicationState) : this()
    {
        this.applicationState = applicationState;
        State = applicationState.Value;

        SelectedConnectionType = State.SelectedConnectionType;
        SelectedPort = State.SelectedPort;
        SelectedBaudRate = State.SelectedBaudRate;
    }
}