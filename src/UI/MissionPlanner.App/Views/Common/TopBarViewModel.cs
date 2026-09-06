using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the global top bar
/// </summary>
public partial class TopBarViewModel : ViewModelBase
{
    public new WindowNotificationManager? NotificationManager
    {
        get; set;
    }

    private readonly ApplicationStateService stateService;
    private readonly IServiceFactory serviceFactory;
    private const string ConnectImage = "avares://MissionPlanner.App/Resources/Images/light_disconnect_icon.png";
    private const string DisConnectImage = "avares://MissionPlanner.App/Resources/Images/light_connect_icon.png";
    private readonly IList<IDisposable> disposables = [];
    private readonly IReplaySessionManager replaySessionManager;
    private readonly INavigationService navigationService;
    private readonly IPlannerSettingsService settingsService;
    private bool changingTheme;
    private bool disposed;


    /// <summary>Gets the themes supported by Avalonia and Semi.</summary>
    public ObservableCollection<ThemeItem> Themes
    {
        get;
    } =
    [
        .. AvaloniaThemeCatalog.Items
    ];

    partial void OnSelectedThemeChanged(ThemeItem? oldValue, ThemeItem? newValue)
    {
        if (newValue is null)
            return;
        AvaloniaThemeCatalog.Apply(newValue);
        if (!changingTheme)
        {
            _ = PersistThemeAsync(newValue);
        }

        NotificationManager?.Show(
            new Notification("Theme changed", $"Theme changed to {newValue.Name}"),
            type: NotificationType.Success,
            classes: ["Light"]);
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
    /// <param name="navigationService">Application route navigation.</param>
    /// <param name="settingsService">The persisted Planner settings service.</param>
    /// <param name="logger">The logger instance.</param>
    public TopBarViewModel(
        ApplicationStateService stateService,
        IServiceFactory serviceFactory,
        IDomainEventHub domainEventHub,
        IReplaySessionManager replaySessionManager,
        INavigationService navigationService,
        IPlannerSettingsService settingsService,
        ILogger<TopBarViewModel> logger) : base(logger)
    {
        this.stateService = stateService;
        this.serviceFactory = serviceFactory;
        this.replaySessionManager = replaySessionManager;
        this.navigationService = navigationService;
        this.settingsService = settingsService;
        // Subscribe to connection events
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
        replaySessionManager.Changed += OnReplayChanged;
        // Subscribe to connection state changes
        stateService.PropertyChanged += OnApplicationStateChanged;
        ApplyReplayState(replaySessionManager.Snapshot);

        AvaloniaThemeCatalog.ThemeChanged += OnThemeChanged;
        SetSelectedTheme(AvaloniaThemeCatalog.Resolve(settingsService.Current.Appearance.ThemeId));
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
        var dialogService = serviceFactory.Create<IDialogService>();
        var options = dialogService.CreateOptions("Connect Vehicle", "Ok", null);
        var viewModel = serviceFactory.Create<ConnectPopupViewModel>();
        var result = await dialogService.ShowOverlayDialogAsync<ConnectPopupView, ConnectPopupViewModel>(viewModel, options);
    }

    [RelayCommand]
    private Task OpenPreferencesAsync()
    {
        return navigationService.NavigateAsync(MissionPlannerRoutes.Preferences);
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
        AvaloniaThemeCatalog.ThemeChanged -= OnThemeChanged;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();
        base.Dispose();
    }

    private async Task PersistThemeAsync(ThemeItem theme)
    {
        settingsService.Current.Appearance = settingsService.Current.Appearance with
        {
            ThemeId = theme.Id
        };
        var result = await settingsService.SaveAsync(settingsService.Current);
        if (!result.Success)
        {
            Logger.LogWarning("Could not persist theme {ThemeId}: {Errors}", theme.Id,
                string.Join(" ", result.Errors.Select(error => error.Message)));
        }
    }

    private void OnThemeChanged(object? sender, ThemeItem theme)
    {
        SetSelectedTheme(theme);
    }

    private void SetSelectedTheme(ThemeItem theme)
    {
        var selected = Themes.First(item => item.Id == theme.Id);
        if (SelectedTheme == selected)
        {
            return;
        }

        changingTheme = true;
        SelectedTheme = selected;
        changingTheme = false;
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
