using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.Dashboard;

/// <summary>
/// Represents the view model for the dashboard page.
/// </summary>
public partial class DashboardPageViewModel : ObservableObject
{
    /*
     private readonly IMavLinkConnection connection = null!;
       private readonly IVehicleConnectionMonitor monitor;
       private readonly IVehicleCommandService vehicleCommandService;
       private readonly IVehicleMessagePump messagePump = null!;
       private readonly IVehicleService vehicleService;
       private readonly InitializeSitl initializeSitl = null!;
       private readonly CancellationTokenSource cancellationTokenSource;
       private readonly ILogger<DashboardPageViewModel> logger;

       [RelayCommand]
       private void Reset()
       {
           initializeSitl.Initialize(cancellationTokenSource.Token).WaitAsync(cancellationTokenSource.Token);
       }

       [RelayCommand]
       private async Task Arm()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.ArmAsync(vehicleState.VehicleId, cancellationTokenSource.Token);
       }

       [RelayCommand]
       private async Task Disarm()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.DisarmAsync(vehicleState.VehicleId, cancellationTokenSource.Token);
       }

       [RelayCommand]
       private async Task Land()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleCommandService.LandAsync(vehicleState, cancellationTokenSource.Token).ConfigureAwait(false);
       }

       [RelayCommand]
       private async Task Rtl()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.SetModeAsync(vehicleState.VehicleId, VehicleMode.Rtl, cancellationTokenSource.Token);
       }

       [RelayCommand]
       private async Task Loiter()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.SetModeAsync(vehicleState.VehicleId, VehicleMode.Loiter, cancellationTokenSource.Token);
       }


       [RelayCommand]
       private async Task Stabilize()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.SetModeAsync(vehicleState.VehicleId, VehicleMode.Stabilize, cancellationTokenSource.Token);
       }

       [RelayCommand]
       private async Task Guided()
       {
           var vehicles = vehicleService.GetVehicles();
           foreach (var vehicleState in vehicles) await vehicleService.SetModeAsync(vehicleState.VehicleId, VehicleMode.Guided, cancellationTokenSource.Token).ConfigureAwait(false);
       }

       /// <summary>
       /// Initializes a new instance of the <see cref="DashboardPageViewModel"/> class with the specified SITL initializer.
       /// </summary>
       /// <param name="initializeSitl">The SITL initializer to be used by the view model.</param>
       /// <param name="connection">The MAVLink connection to be used by the view model.</param>
       /// <param name="monitor">The vehicle connection monitor to be used by the view model.</param>
       /// <param name="vehicleCommandService">The vehicle command service to be used by the view model.</param>
       /// <param name="messagePump">The vehicle message pump to be used by the view model.</param>
       /// <param name="vehicleService">The vehicle service to be used by the view model.</param>
       /// <param name="cancellationTokenSource">The cancellation token source to be used by the view model.</param>
       /// <param name="logger">The logger to be used by the view model.</param>
       public DashboardPageViewModel(IMavLinkConnection connection, IVehicleConnectionMonitor monitor, IVehicleCommandService vehicleCommandService, IVehicleMessagePump messagePump, IVehicleService vehicleService,
           CancellationTokenSource cancellationTokenSource, ILogger<DashboardPageViewModel> logger)
       {
           this.connection = connection;
           this.monitor = monitor;
           this.vehicleCommandService = vehicleCommandService;
           this.messagePump = messagePump;
           this.vehicleService = vehicleService;
           this.initializeSitl = initializeSitl;
           this.cancellationTokenSource = cancellationTokenSource;
           this.logger = logger;
           SetupSubscriptions();
       }


       /// <summary>
       /// Sets up the subscriptions for domain events.
       /// </summary>
       private void SetupSubscriptions()
       {
           var cancellationToken = cancellationTokenSource.Token;
           var conn = connection;
           var mp = messagePump;
           _ = Task.Run(() => conn.StartAsync(cancellationToken), cancellationToken);
           _ = Task.Run(() => mp.StartAsync(cancellationToken), cancellationToken);

           _ = Task.Run(async () =>
           {
               while (!cancellationToken.IsCancellationRequested)
               {
                   monitor.UpdateConnectionStates();
                   await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
               }
           }, cancellationToken);


           //eventHub.SubscribeDomainEvent<VehicleArmed>((m) => OnVehicleArmed((VehicleArmed)m));
           //eventHub.SubscribeDomainEvent<VehicleDisarmed>((m) => OnVehicleDisarmed((VehicleDisarmed)m));
           //eventHub.SubscribeDomainEvent<VehicleConnectionStateChanged>((m) => OnVehicleConnectionStateChanged((VehicleConnectionStateChanged)m));
           //eventHub.SubscribeDomainEvent<VehicleModeChanged>((m) => OnVehicleModeChanged((VehicleModeChanged)m));
           //eventHub.SubscribeDomainEvent<VehicleRegistered>((m) => OnVehicleRegistered((VehicleRegistered)m));
           //eventHub.SubscribeDomainEvent<VehicleStateUpdated>((m) => OnVehicleStateUpdated((VehicleStateUpdated)m));
           //eventHub.SubscribeDomainEvent<VehicleStatusMessageReceived>((m) => OnVehicleStatusMessageReceived((VehicleStatusMessageReceived)m));
       }
       */
}