using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.Core.DomainEvents;
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
    private readonly IDomainEventHub? eventHub;
    private readonly IDisposable? eventSubscription;
    private const string? ConnectImage = "Resources/Images/light_disconnect_icon.png";
    private const string? DisConnectImage = "Resources/Images/light_connect_icon.png";

    [ObservableProperty] public partial string ConnectionStatus { get; set; } = "Disconnected";
    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = ConnectImage;
    [ObservableProperty] public partial string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="serviceFactory"></param>
    /// <param name="eventHub">The domain event hub.</param>
    /// <param name="logger">The logger instance.</param>
    public TopBarViewModel(ApplicationStateService stateService, IServiceFactory serviceFactory, IDomainEventHub eventHub, ILogger<TopBarViewModel> logger)
    {
        this.logger = logger;
        this.stateService = stateService;
        this.serviceFactory = serviceFactory;
        this.eventHub = eventHub;
        //    this.dialogService = dialogService;

        // Subscribe to connection state changes
        stateService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ApplicationStateService.IsConnected))
            {
                UpdateConnectionStatus();
            }
        };

        // Subscribe to vehicle connection events
        eventSubscription = eventHub.SubscribeDomainEvent<VehicleConnected>(OnVehicleConnected);

        // Initial state
        UpdateConnectionStatus();
    }


    private void UpdateConnectionStatus()
    {
        ConnectionStatus = stateService.IsConnected ? "Connected" : "Disconnected";
        IsConnectedImage = stateService.IsConnected ? ConnectImage : DisConnectImage;
    }

    private void OnVehicleConnected(VehicleConnected evt)
    {
        MainThread.BeginInvokeOnMainThread(() => ConnectionStatus = $"Connected: {evt.VehicleId}");
    }


    [RelayCommand]
    private async Task Connect()
    {
        var view = serviceFactory.Create<ConnectPopupView>();
        var dialogService = serviceFactory.Create<IDialogService>();
        await dialogService.DisplayViewAsync("Connection", view, "Close");
    }
}
