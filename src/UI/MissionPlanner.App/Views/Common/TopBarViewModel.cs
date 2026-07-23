using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using UraniumUI.Dialogs;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the global top bar
/// </summary>
public partial class TopBarViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<TopBarViewModel> logger;
    private readonly ApplicationStateService stateService;
    private readonly IServiceFactory serviceFactory;
    private readonly IDispatcher dispatcher;
    private const string? ConnectImage = "Resources/Images/x_light_disconnect_icon_x.png";
    private const string? DisConnectImage = "Resources/Images/x_light_connect_icon_x.png";
    private readonly IList<IDisposable> disposables = [];
    private readonly IReplaySessionManager replaySessionManager;

    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string? Host { get; set; }
    [ObservableProperty] public partial string? Port { get; set; }
    [ObservableProperty] public partial string? BaudRate { get; set; }
    [ObservableProperty] public partial string? VehicleName { get; set; }
    [ObservableProperty] public partial string? VehicleId { get; set; }
    [ObservableProperty] public partial string? FirmwareIdentity { get; set; }
    [ObservableProperty] public partial string? Channel { get; set; }
    [ObservableProperty] public partial string ConnectionStatus { get; set; } = "Disconnected";
    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = ConnectImage;
    [ObservableProperty] public partial string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] public partial bool ShowHost { get; set; }
    [ObservableProperty] public partial bool ShowCom { get; set; } = true;
    [ObservableProperty] public partial bool ShowVehicleName { get; set; } = true;
    [ObservableProperty] public partial string DataSourceMode { get; private set; } = "LIVE / SIMULATION";
    [ObservableProperty] public partial bool IsReplayReadOnly { get; private set; }

    /// <summary>Gets whether the connection dialog may be opened in the current data-source mode.</summary>
    public bool CanOpenConnection => !IsReplayReadOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="serviceFactory">The service factory.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="replaySessionManager">Application-wide replay safety state.</param>
    public TopBarViewModel(
        ApplicationStateService stateService,
        IServiceFactory serviceFactory,
        IDispatcher dispatcher,
        IDomainEventHub domainEventHub,
        ILogger<TopBarViewModel> logger,
        IReplaySessionManager replaySessionManager)
    {
        this.logger = logger;
        this.stateService = stateService;
        this.serviceFactory = serviceFactory;
        this.dispatcher = dispatcher;
        this.replaySessionManager = replaySessionManager;
        // Subscribe to connection events
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
        replaySessionManager.Changed += OnReplayChanged;
        ApplyReplayState(replaySessionManager.Snapshot);

        // Initial state
        UpdateConnectionStatus();

        // Subscribe to connection state changes
        stateService.PropertyChanged += (s, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ApplicationStateService.IsConnected):

                    if (IsConnected != stateService.IsConnected)
                    {
                        IsConnected = stateService.IsConnected;
                        UpdateConnectionStatus();
                    }

                    break;
                case nameof(ApplicationStateService.VehicleId):

                    if (VehicleId != stateService.VehicleId.ToString())
                    {
                        VehicleId = stateService.VehicleId.ToString();
                        UpdateConnectionStatus();
                    }

                    break;
                case nameof(ApplicationStateService.VehicleName):

                    if (VehicleName != stateService.VehicleName)
                    {
                        VehicleName = stateService.VehicleName;
                        UpdateConnectionStatus();
                    }

                    break;
            }
        };
    }


    private void UpdateConnectionStatus()
    {
        IsConnected = stateService.IsConnected;
        Channel = stateService.SelectedChannel;
        ShowHost = Channel is "TCP" or "UDP" or "UDPCI";
        ShowCom = !ShowHost;
        ShowVehicleName = !string.IsNullOrEmpty(stateService.VehicleName);
        Host = ShowHost ? stateService.SelectedHost : null;
        Port = ShowHost ? stateService.SelectedPort : null;

        BaudRate = ShowCom ? stateService.SelectedBaudRate : null;
        VehicleName = ShowVehicleName ? stateService.VehicleName : null;
        VehicleId = stateService.VehicleId.ToString();

        ConnectionStatus = stateService.IsConnected ? "Connected" : "Disconnected";
        IsConnectedImage = stateService.IsConnected ? ConnectImage : DisConnectImage;
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(async () => ConnectionStatus = $"Connected: {evt.VehicleId}");
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(async () =>
        {
            ConnectionStatus = $"Disconnected: {evt.VehicleId}";
            FirmwareIdentity = null;
        });
    }

    private async Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken ct)
    {
        var display = VehicleFirmwareDisplayFormatter.Format(evt.VehicleState.Identity.Firmware);
        await dispatcher.DispatchAsync(async () => FirmwareIdentity = display);
    }

    [RelayCommand(CanExecute = nameof(CanOpenConnection))]
    private async Task Connect()
    {
        var view = serviceFactory.Create<ConnectPopupView>();
        var dialogService = serviceFactory.Create<IDialogService>();
        await dialogService.DisplayViewAsync("Connection", view, "Close");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        replaySessionManager.Changed -= OnReplayChanged;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    private void OnReplayChanged(object? sender, ReplaySessionChangedEventArgs args) =>
        dispatcher.Dispatch(() => ApplyReplayState(args.Snapshot));

    private void ApplyReplayState(ReplaySessionSnapshot snapshot)
    {
        IsReplayReadOnly = snapshot.IsTransmissionProhibited;
        DataSourceMode = IsReplayReadOnly ? "REPLAY · READ ONLY" : "LIVE / SIMULATION";
        OnPropertyChanged(nameof(CanOpenConnection));
        ConnectCommand.NotifyCanExecuteChanged();
    }
}
