using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Configuration;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Connect;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using Semi.Avalonia;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace MissionPlanner.AvaloniaUI.App.Views.Common;

/// <summary>
/// ViewModel for the global top bar
/// </summary>
public partial class TopBarViewModel : ViewModelBase
{
    public WindowNotificationManager? NotificationManager
    {
        get; set;
    }

    private readonly ApplicationStateService stateService;
    private readonly IServiceFactory serviceFactory;
    private const string ConnectImage = "avares://MissionPlanner.AvaloniaUI.App/Resources/Images/light_disconnect_icon.png";
    private const string DisConnectImage = "avares://MissionPlanner.AvaloniaUI.App/Resources/Images/light_connect_icon.png";
    private readonly IList<IDisposable> disposables = [];
    private readonly IReplaySessionManager replaySessionManager;
    //private readonly INavigationService navigationService;
    private bool disposed;


    public ObservableCollection<ThemeItem> Themes
    {
        get;
    } =
    [
        new("Default", ThemeVariant.Default),
        new("Light", ThemeVariant.Light),
        new("Dark", ThemeVariant.Dark),
        new("Aquatic", SemiTheme.Aquatic),
        new("Desert", SemiTheme.Desert),
        new("Dusk", SemiTheme.Dusk),
        new("NightSky", SemiTheme.NightSky)
    ];

    partial void OnSelectedThemeChanged(ThemeItem? oldValue, ThemeItem? newValue)
    {
        if (newValue is null)
            return;
        var app = Application.Current;
        if (app is not null)
        {
            app.RequestedThemeVariant = newValue.Theme;
            NotificationManager?.Show(
                new Notification("Theme changed", $"Theme changed to {newValue.Name}"),
                type: NotificationType.Success,
                classes: ["Light"]);
        }
    }


    [ObservableProperty]
    public partial ThemeItem? SelectedTheme
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsConnected
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? Host
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? Port
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? BaudRate
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? VehicleName
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? VehicleId
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? FirmwareIdentity
    {
        get; set;
    }
    [ObservableProperty]
    public partial string? Channel
    {
        get; set;
    }
    [ObservableProperty] public partial string ConnectionStatus { get; set; } = "Disconnected";

    [ObservableProperty]
    public partial bool ShowHost
    {
        get; set;
    }
    [ObservableProperty] public partial bool ShowCom { get; set; } = true;
    [ObservableProperty] public partial bool ShowVehicleName { get; set; } = true;
    [ObservableProperty] public partial string DataSourceMode { get; private set; } = "LIVE / SIMULATION";
    [ObservableProperty]
    public partial bool IsReplayReadOnly
    {
        get; private set;
    }

    [ObservableProperty]
    public partial Bitmap? ConnectionImage
    {
        get; set;
    }

    private Bitmap LoadImage(string image)
    {
        return new Bitmap(AssetLoader.Open(new Uri(image)));
    }

    /// <summary>Gets whether the connection dialog may be opened in the current data-source mode.</summary>
    public bool CanOpenConnection => !IsReplayReadOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="serviceFactory">The service factory.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="replaySessionManager">Application-wide replay safety state.</param>
    /// <param name="logger">The logger instance.</param>
    public TopBarViewModel(
        ApplicationStateService stateService,
        IServiceFactory serviceFactory,
        IDomainEventHub domainEventHub,
        IReplaySessionManager replaySessionManager,
           ILogger<TopBarViewModel> logger
        //  INavigationService navigationService
        ) : base(logger)
    {
        this.stateService = stateService;
        this.serviceFactory = serviceFactory;
        this.replaySessionManager = replaySessionManager;
        // Subscribe to connection events
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
        replaySessionManager.Changed += OnReplayChanged;
        // Subscribe to connection state changes
        stateService.PropertyChanged += OnApplicationStateChanged;
        ApplyReplayState(replaySessionManager.Snapshot);

        SelectedTheme = Themes.FirstOrDefault(t => t.Theme == Application.Current?.RequestedThemeVariant) ?? Themes.First();
        LoadImage(ConnectImage);
        // Initial state
        UpdateConnectionStatus();

    }

    private void OnApplicationStateChanged(object? sender, PropertyChangedEventArgs args)
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
    }

    private void UpdateConnectionStatus()
    {
        Dispatcher.Dispatch(() =>
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
            ConnectionImage = stateService.IsConnected ? LoadImage(ConnectImage) : LoadImage(DisConnectImage);
        });
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        await Dispatcher.DispatchAsync(() => ConnectionStatus = $"Connected: {evt.VehicleId}");
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        await Dispatcher.DispatchAsync(() =>
        {
            ConnectionStatus = $"Disconnected: {evt.VehicleId}";
            FirmwareIdentity = null;
        });
    }

    private async Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken ct)
    {
        var display = VehicleFirmwareDisplayFormatter.Format(evt.VehicleState.Identity.Firmware);
        await Dispatcher.DispatchAsync(() => FirmwareIdentity = display);
    }

    [RelayCommand(CanExecute = nameof(CanOpenConnection))]
    private async Task Connect()
    {
        var view = serviceFactory.Create<ConnectPopupView>();
        var dialogService = serviceFactory.Create<IDialogService>();
        await dialogService.ShowWindowAsync(
            view,
            new DialogOptions
            {
                Title = "Connection",
                Presentation = DialogPresentation.Window,
                Width = 600,
                Height = 500,
                OkText = "Ok",
                ShowCloseButton = false,
            });

    }

    [RelayCommand]
    private Task OpenPreferencesAsync()
    {
        //TODO:  return navigationService.OpenPageAsync("Preferences");
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        stateService.PropertyChanged -= OnApplicationStateChanged;
        replaySessionManager.Changed -= OnReplayChanged;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();
        base.Dispose();
    }

    private void OnReplayChanged(ReplaySessionChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => ApplyReplayState(args.Snapshot));
    }

    private void ApplyReplayState(ReplaySessionSnapshot snapshot)
    {
        Dispatcher.Dispatch(() =>
        {
            IsReplayReadOnly = snapshot.IsTransmissionProhibited;
            DataSourceMode = IsReplayReadOnly ? "REPLAY · READ ONLY" : "LIVE / SIMULATION";
            OnPropertyChanged(nameof(CanOpenConnection));
            ConnectCommand.NotifyCanExecuteChanged();
        });
    }
}
