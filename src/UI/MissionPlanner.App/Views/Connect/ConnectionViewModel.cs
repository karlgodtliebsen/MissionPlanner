using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Connect;

public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty] public partial string? SelectedConnectionType { get; set; }
    [ObservableProperty] public partial string? SelectedPort { get; set; }
    [ObservableProperty] public partial string? SelectedBaudRate { get; set; }
    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = NotConnectedImage;
    [ObservableProperty] public partial string? IsConnectedText { get; set; } = NotConnectedText;


    private const string? ConnectedImage = "Resources/Images/light_disconnect_icon.png";
    private const string? NotConnectedImage = "Resources/Images/light_connect_icon.png";

    private const string? ConnectedText = "Connected";
    private const string? NotConnectedText = "Connect";


    /// <summary>
    /// Data for the connect popup, such as available connection types, ports, and baud rates.
    /// </summary>
    public ConnectionViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionViewModel"/> class with the specified application state.
    /// </summary>
    /// <param name="stateService"></param>
    public ConnectionViewModel(ApplicationStateService stateService) : this()
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
                    IsConnectedText = IsConnected ? ConnectedText : NotConnectedText;
                    break;
            }
        };
    }
}
