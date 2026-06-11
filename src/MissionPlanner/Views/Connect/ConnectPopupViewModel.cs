using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.Views.Connect;

public partial class ConnectPopupViewModel : ObservableObject
{
    [ObservableProperty] private string? selectedConnectionType;

    [ObservableProperty] private string? selectedPort;

    [ObservableProperty] private string? selectedBaudRate;

    [ObservableProperty] private bool isConnected;

    public List<string> ConnectionTypes { get; } =
    [
        "Serial",
        "TCP",
        "UDP",
        "UDP Client"
    ];

    public List<string> Ports { get; } =
    [
        "COM1",
        "COM2",
        "COM3",
        "AUTO"
    ];

    public List<string> BaudRates { get; } =
    [
        "4800",
        "9600",
        "19200",
        "38400",
        "57600",
        "115200",
        "230400",
        "460800",
        "921600"
    ];

    public ConnectPopupViewModel()
    {
        SelectedConnectionType = ConnectionTypes[0];
        SelectedPort = Ports[^1];
        SelectedBaudRate = "115200";
    }

    [RelayCommand]
    private void Connect()
    {
        // TODO: wire up to the comms/connection layer
        IsConnected = !IsConnected;
    }
}