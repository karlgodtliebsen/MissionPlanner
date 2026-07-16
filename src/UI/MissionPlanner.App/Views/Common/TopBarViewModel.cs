using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using UraniumUI.Dialogs;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the global top bar
/// </summary>
public partial class TopBarViewModel : ObservableObject
{
    private readonly ILogger<TopBarViewModel> logger;
    private readonly ApplicationStateService stateService;
    private readonly IServiceFactory serviceFactory;
    private readonly IDomainEventHub? domainEventHub;
    private readonly IDisposable? eventSubscription;
    private const string? ConnectImage = "Resources/Images/x_light_disconnect_icon_x.png";
    private const string? DisConnectImage = "Resources/Images/x_light_connect_icon_x.png";


    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string? Host { get; set; }
    [ObservableProperty] public partial string? Port { get; set; }
    [ObservableProperty] public partial string? BaudRate { get; set; }
    [ObservableProperty] public partial string? VehicleName { get; set; }
    [ObservableProperty] public partial string? Channel { get; set; }
    [ObservableProperty] public partial string ConnectionStatus { get; set; } = "Disconnected";
    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = ConnectImage;
    [ObservableProperty] public partial string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] public partial bool ShowHost { get; set; }
    [ObservableProperty] public partial bool ShowCom { get; set; } = true;
    [ObservableProperty] public partial bool ShowVehicle { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="serviceFactory"></param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="logger">The logger instance.</param>
    public TopBarViewModel(ApplicationStateService stateService, IServiceFactory serviceFactory, IDomainEventHub domainEventHub, ILogger<TopBarViewModel> logger)
    {
        this.logger = logger;
        this.stateService = stateService;
        this.serviceFactory = serviceFactory;
        this.domainEventHub = domainEventHub;
        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected);

        //    this.dialogService = dialogService;

        // Subscribe to connection state changes
        stateService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ApplicationStateService.IsConnected))
            {
                UpdateConnectionStatus();
            }
        };

        //// Subscribe to vehicle connection events
        //eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected);

        // Initial state
        UpdateConnectionStatus();
    }


    private void UpdateConnectionStatus()
    {
        IsConnected = stateService.IsConnected;
        Channel = stateService.SelectedChannel;
        ShowHost = Channel is "TCP" or "UDP" or "UDPCI";
        ShowCom = !ShowHost;
        ShowVehicle = !string.IsNullOrEmpty(stateService.VehicleName);
        Host = ShowHost ? stateService.SelectedHost : null;
        Port = ShowHost ? stateService.SelectedPort : null;

        BaudRate = ShowCom ? stateService.SelectedBaudRate : null;
        VehicleName = ShowVehicle ? stateService.VehicleName : null;

        ConnectionStatus = stateService.IsConnected ? "Connected" : "Disconnected";
        IsConnectedImage = stateService.IsConnected ? ConnectImage : DisConnectImage;
    }

    private Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        MainThread.BeginInvokeOnMainThread(() => ConnectionStatus = $"Connected: {evt.VehicleId}");
        return Task.CompletedTask;
    }


    [RelayCommand]
    private async Task Connect()
    {
        var view = serviceFactory.Create<ConnectPopupView>();
        var dialogService = serviceFactory.Create<IDialogService>();
        await dialogService.DisplayViewAsync("Connection", view, "Close");
    }
}
