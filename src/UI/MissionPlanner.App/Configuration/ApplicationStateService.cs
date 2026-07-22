using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Configuration;

/// <summary>
/// Singleton service for managing shared application state across the application.
/// </summary>
public partial class ApplicationStateService : ObservableObject, IDisposable
{
    private readonly IList<IDisposable> disposables = [];
    private readonly IVehicleRegistry vehicleRegistry;

    /// <summary>
    /// Singleton service for managing shared application state across the application.
    /// </summary>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="vehicleRegistry">The connected vehicle registry.</param>
    public ApplicationStateService(IDomainEventHub domainEventHub, IVehicleRegistry vehicleRegistry)
    {
        this.vehicleRegistry = vehicleRegistry;
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
    }

    private Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    private Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        VehicleId = evt.VehicleId;
        VehicleName = vehicleRegistry.GetRequired(evt.VehicleId)?.State.DisplayName ?? $"{evt.VehicleId.SystemId}:Unknown";
        IsConnected = true;
        return Task.CompletedTask;
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken ct)
    {
        if (VehicleId == evt.VehicleId)
        {
            VehicleName = evt.VehicleState.DisplayName;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
    }

    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string SelectedChannel { get; set; } = "AUTO";
    [ObservableProperty] public partial string SelectedBaudRate { get; set; } = "115200";
    [ObservableProperty] public partial string SelectedPort { get; set; } = "14550";
    [ObservableProperty] public partial string SelectedHost { get; set; } = "127.0.0.1";
    [ObservableProperty] public partial string? VehicleName { get; set; }
    [ObservableProperty] public partial VehicleId? VehicleId { get; set; }

    /// <summary>
    /// Initializes the service with values from ApplicationState.
    /// </summary>
    public void Initialize(ApplicationState state)
    {
        IsConnected = state.IsConnected;
        SelectedChannel = state.SelectedChannel;
        SelectedBaudRate = state.SelectedBaudRate;
        SelectedPort = state.SelectedPort;
        SelectedHost = state.SelectedHost;
        VehicleName = null;
        VehicleId = null;
    }
}
