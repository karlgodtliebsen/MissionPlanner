using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Provides the common lifecycle contract for placeholder Flight Data tabs.</summary>
public abstract class FlightDataTabViewModelBase : ObservableObject, IDisposable
{
    /// <summary>
    /// Gets the active vehicle context.
    /// </summary>
    public IActiveVehicleContext ActiveVehicle { get; }

    /// <summary>Initializes a lifecycle-aware Flight Data tab.</summary>
    protected FlightDataTabViewModelBase(string key, IActiveVehicleContext activeVehicle)
    {
        ActiveVehicle = activeVehicle;
    }


    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
