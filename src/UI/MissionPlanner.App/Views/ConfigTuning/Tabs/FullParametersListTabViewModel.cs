using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the full list of parameters for a vehicle.
/// </summary>
public partial class FullParametersListTabViewModel : /*BindableObject*/ObservableObject, IDisposable
{
    // private readonly IVehicleConnectionService vehicleConnectionService;
    private readonly IVehicleConnectionSession session;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDispatcher dispatcher;
    private readonly CancellationTokenSource cts;

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    private IDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>();

    /// <summary>
    /// Gets the collection of vehicle parameters.
    /// </summary>
    public ObservableCollection<VehicleParameter> Parameters { get; set; } = [];

    [ObservableProperty] public partial int ParametersCount { get; set; }


    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabViewModel"/> class.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="vehicleRegistry"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="dispatcher"></param>
    /// <param name="cts"></param>
    /// <param name="logger"></param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession session,
        //IVehicleParameterService vehicleParameterService,
        //IVehicleParameterRegistry vehicleParameterRegistry,
        IVehicleRegistry vehicleRegistry,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        CancellationTokenSource cts,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.session = session;
        this.vehicleRegistry = vehicleRegistry;
        this.dispatcher = dispatcher;
        this.cts = cts;
        this.logger = logger;
        var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(VehicleRegistered);
        eventSubscriptions.Add(eventSubscription);

        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(VehicleDisconnected);
        eventSubscriptions.Add(eventSubscription);

        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(VehicleParameterReceived);
        eventSubscriptions.Add(eventSubscription);

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    private async Task VehicleParameterReceived(VehicleParameterReceived message, CancellationToken cancellationToken)
    {
        await Task.Run(async () => await LoadAsync(message.VehicleId, cancellationToken), cancellationToken);
    }


    private Task VehicleDisconnected(VehicleDisconnected vehicle, CancellationToken arg2)
    {
        dispatcher.Dispatch(() =>
        {
            Parameters.Clear();
            ParametersCount = Parameters.Count;
        });
        return Task.CompletedTask;
    }

    private async Task VehicleRegistered(VehicleConnected vehicle, CancellationToken ct)
    {
        await Task.Run(async () => await LoadAsync(vehicle.VehicleId, ct), cts.Token);
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            //await LoadAsync(vehicle.Id, CancellationToken.None);
            await Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    // MainThread.BeginInvokeOnMainThread(async () =>
    private async Task LoadAsync(VehicleId vehicleId, CancellationToken ct)
    {
        await session.ParameterService.RequestParameterListAsync(vehicleId, ct);
        parameters = new Dictionary<string, VehicleParameter>(session.ParameterRegistry.GetAllParameters(vehicleId));
        //if (parameters.Count != Parameters.Count)
        {
            dispatcher.Dispatch(() =>
            {
                Parameters.Clear();
                foreach (var parameter in parameters.Values)
                {
                    Parameters.Add(parameter);
                }

                ParametersCount = Parameters.Count;
            });
            //await Task.Delay(50, ct);
            //await LoadAsync(vehicleId, ct);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in eventSubscriptions)
        {
            disposable.Dispose();
        }

        eventSubscriptions.Clear();
    }
}
