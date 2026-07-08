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
    private CancellationTokenSource ctsProgress = new();

    private readonly ILogger<FullParametersListTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];

    private IDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>();

    /// <summary>
    /// Gets the collection of vehicle parameters.
    /// </summary>
    public ObservableCollection<VehicleParameter> Parameters { get; set; } = [];

    [ObservableProperty] public partial string ProgressMessage { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }


    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabViewModel"/> class.
    /// </summary>
    /// <param name="session">The vehicle connection session.</param>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="cts">The cancellation token source.</param>
    /// <param name="logger">The logger.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession session,
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

        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    //private async Task VehicleParameterReceived(VehicleParameterReceived message, CancellationToken cancellationToken)
    //{
    //    await Task.Run(async () => await LoadAsync(message.VehicleId, cancellationToken), cancellationToken);
    //}


    private Task VehicleDisconnected(VehicleDisconnected vehicle, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(() =>
        {
            Parameters.Clear();
            Progress = 0;
            IsLoading = false;
        });
        return Task.CompletedTask;
    }

    private async Task VehicleRegistered(VehicleConnected vehicle, CancellationToken cancellationToken)
    {
        await Task.Run(async () => await LoadAsync(vehicle.VehicleId, cancellationToken), cancellationToken);
    }

    [RelayCommand]
    private async Task LoadParameters()
    {
        var vehicles = vehicleRegistry.Vehicles;
        var vehicle = vehicles.FirstOrDefault();
        if (vehicle is not null)
        {
            await Task.Run(async () => await LoadAsync(vehicle.Id, cts.Token), cts.Token);
        }
    }

    [RelayCommand]
    private async Task CancelLoad()
    {
        await ctsProgress.CancelAsync();
        ctsProgress = new CancellationTokenSource();
        Progress = 0;
        IsLoading = false;
    }

    // MainThread.BeginInvokeOnMainThread(async () =>
    private async Task LoadAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        IsLoading = true;
        Parameters.Clear();
        parameters.Clear();
        ProgressMessage = "Loading parameters...";
        Progress = 0;
        IProgress<ParameterStreamProgress>? progress = new Progress<ParameterStreamProgress>(p =>
        {
            Progress = (double)p.ReceivedCount / p.TotalCount;

            ProgressMessage = $"Loading parameters... {p.ReceivedCount}/{p.TotalCount}";
        });

        // Stream all parameters with progress tracking
        var vehicleParameterStreamService = session.ParameterStreamService;
        var result = await vehicleParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: ctsProgress.Token);

        if (result.Success)
        {
            parameters = new Dictionary<string, VehicleParameter>(result.Parameters);
            Parameters.Clear();
            foreach (var parameter in parameters.Values)
            {
                Parameters.Add(parameter);
            }
        }

        Progress = 0;
        IsLoading = false;
    }

    //using (await DialogService.DisplayProgressCancellableAsync("Loading", "Work in progress, please wait...", tokenSource: tokenSource))
    //{
    //    try
    //    {
    //        // Long operation...
    //        await Task.Delay((int) sliderForProgress.Value, tokenSource.Token);
    //    }
    //    catch (TaskCanceledException)
    //    {
    //    }
    //}

    ////if (parameters.Count != Parameters.Count)
    //{
    //    dispatcher.Dispatch(() =>
    //    {
    //        Parameters.Clear();
    //        foreach (var parameter in parameters.Values)
    //        {
    //            Parameters.Add(parameter);
    //        }

    //        //ParametersCount = Parameters.Count;
    //    });
    //}
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
