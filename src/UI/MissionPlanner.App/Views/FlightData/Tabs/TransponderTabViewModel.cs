using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents discovered component-scoped transponder and nearby traffic state.</summary>
public partial class TransponderTabViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleComponentRegistry registry;
    private readonly IDispatcher dispatcher;

    /// <summary>Initializes a transient transponder view model.</summary>
    public TransponderTabViewModel(IActiveVehicleContext activeVehicle, IVehicleComponentRegistry registry, IDispatcher dispatcher)
    {
        this.activeVehicle = activeVehicle;
        this.registry = registry;
        this.dispatcher = dispatcher;
        registry.Changed += OnChanged;
        activeVehicle.Changed += OnChanged;
        Refresh();
    }

    /// <summary>Gets discovered transponders.</summary>
    public ObservableRangeCollection<TransponderComponentState> Components
    {
        get;
    } = [];

    /// <summary>Gets bounded nearby traffic.</summary>
    public ObservableRangeCollection<AdsbTrafficTrack> Traffic
    {
        get;
    } = [];

    /// <summary>Gets the explicit support/discovery state.</summary>
    [ObservableProperty]
    public partial string Status
    {
        get;
        private set;
    } = "No transponder discovered";

    /// <inheritdoc />
    public void Dispose()
    {
        registry.Changed -= OnChanged;
        activeVehicle.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(Refresh);
    }

    private void Refresh()
    {
        var systemId = activeVehicle.VehicleId?.SystemId;
        if (systemId is null)
        {
            Status = "No active vehicle";
            return;
        }

        Replace(Components, registry.GetTransponders(systemId.Value));
        Replace(Traffic, registry.GetTraffic(systemId.Value, DateTimeOffset.UtcNow));
        Status = Components.Count == 0 ? "No supported uAvionix transponder discovered" : $"{Components.Count} transponder component(s)";
    }

    private static void Replace<T>(ObservableRangeCollection<T> target, IEnumerable<T> values)
    {
        target.ReplaceRange(values);
    }
}
