using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Provides the common lifecycle contract for placeholder Flight Data tabs.</summary>
public abstract class FlightDataTabViewModelBase : ObservableObject, IFlightDataTabLifecycle, IDisposable
{
    private readonly FlightDataTabLifecycle lifecycle;

    /// <summary>Initializes a lifecycle-aware Flight Data tab.</summary>
    protected FlightDataTabViewModelBase(string key, IActiveVehicleContext activeVehicle)
    {
        lifecycle = new FlightDataTabLifecycle(key, activeVehicle);
    }

    /// <inheritdoc />
    public string Key => lifecycle.Key;

    /// <inheritdoc />
    public bool IsActive => lifecycle.IsActive;

    /// <inheritdoc />
    public bool IsInitialized => lifecycle.IsInitialized;

    /// <inheritdoc />
    public Task ActivateAsync(CancellationToken cancellationToken = default) => lifecycle.ActivateAsync(cancellationToken);

    /// <inheritdoc />
    public Task DeactivateAsync() => lifecycle.DeactivateAsync();

    /// <inheritdoc />
    public void Dispose() => lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
