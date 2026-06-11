using CommunityToolkit.Mvvm.ComponentModel;

using MissionPlanner.Configuration;

namespace MissionPlanner.Views;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty] private string? selectedConnectionType;
    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private string? selectedBaudRate;
    [ObservableProperty] private bool isConnected = false;

    //[ObservableProperty] private readonly string? isConnectedImage = "Resources/Images/light_connect_icon.png"; //disconnect
    [ObservableProperty] private string? isConnectedImage = NotConnectedImage;


    private const string? ConnectedImage = "Resources/Images/light_disconnect_icon.png";
    private const string? NotConnectedImage = "Resources/Images/light_connect_icon.png";


    /// <summary>
    /// Data for the connect popup, such as available connection types, ports, and baud rates.
    /// </summary>
    public MainPageViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPageViewModel"/> class with the specified application state.
    /// </summary>
    /// <param name="stateService"></param>
    public MainPageViewModel(ApplicationStateService stateService) : this()
    {
        // Initialize from shared state
        SelectedConnectionType = stateService.SelectedConnectionType;
        SelectedPort = stateService.SelectedPort;
        SelectedBaudRate = stateService.SelectedBaudRate;
        IsConnected = stateService.IsConnected;

        // Subscribe to state changes - this will update the statusbar when values change
        stateService.PropertyChanged += (sender, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ApplicationStateService.SelectedConnectionType):
                    SelectedConnectionType = stateService.SelectedConnectionType;
                    break;
                case nameof(ApplicationStateService.SelectedPort):
                    SelectedPort = stateService.SelectedPort;
                    break;
                case nameof(ApplicationStateService.SelectedBaudRate):
                    SelectedBaudRate = stateService.SelectedBaudRate;
                    break;
                case nameof(ApplicationStateService.IsConnected):
                    IsConnected = stateService.IsConnected;
                    IsConnectedImage = IsConnected ? ConnectedImage : NotConnectedImage;
                    break;
            }
        };
    }
}