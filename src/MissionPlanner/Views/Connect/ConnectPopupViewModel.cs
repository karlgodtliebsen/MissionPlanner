using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Options;

using MissionPlanner.Configuration;

namespace MissionPlanner.Views.Connect;

public partial class ConnectPopupViewModel : ObservableObject
{
    public ApplicationOptions? Options { get; }
    private readonly ApplicationStateService? stateService;

    [ObservableProperty] private string? selectedConnectionType;

    [ObservableProperty] private string? selectedPort;

    [ObservableProperty] private string? selectedBaudRate;

    [ObservableProperty] private bool isConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupViewModel"/> class.
    /// </summary>
    public ConnectPopupViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="stateService"></param>
    public ConnectPopupViewModel(IOptionsMonitor<ApplicationOptions> options, ApplicationStateService stateService) : this()
    {
        Options = options.CurrentValue;
        this.stateService = stateService;

        // Initialize from shared state
        SelectedConnectionType = stateService.SelectedConnectionType;
        SelectedPort = stateService.SelectedPort;
        SelectedBaudRate = stateService.SelectedBaudRate;
        IsConnected = stateService.IsConnected;

        // Subscribe to state changes
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
                    break;
            }
        };
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        switch (args.PropertyName)
        {
            case nameof(ApplicationStateService.SelectedConnectionType):
                stateService?.SelectedConnectionType = SelectedConnectionType!;
                break;
            case nameof(ApplicationStateService.SelectedPort):
                stateService?.SelectedPort = SelectedPort!;
                break;
            case nameof(ApplicationStateService.SelectedBaudRate):
                stateService?.SelectedBaudRate = SelectedBaudRate!;
                break;
            case nameof(ApplicationStateService.IsConnected):
                stateService?.IsConnected = IsConnected;
                break;
        }
    }

    [RelayCommand]
    private void Connect()
    {
        // TODO: wire up to the comms/connection layer
        IsConnected = !IsConnected;
        //// Update shared state - this will propagate to all subscribers including MainPageViewModel
        //stateService?.IsConnected = IsConnected;
    }

    [RelayCommand]
    private void Close()
    {
    }
}