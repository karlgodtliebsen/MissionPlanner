using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs;

/// <summary>Presents discovered component-scoped transponder and nearby traffic state.</summary>
public partial class TransponderTabViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleComponentRegistry registry;

    /// <summary>Initializes a transient transponder view model.</summary>
    public TransponderTabViewModel(IActiveVehicleContext activeVehicle, IVehicleComponentRegistry registry, ILogger<TransponderTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.registry = registry;
    }

    /// <summary>Gets discovered transponders.</summary>
    public ObservableRangeCollection<TransponderComponentState> Components { get; } = [];

    /// <summary>Gets bounded nearby traffic.</summary>
    public ObservableRangeCollection<AdsbTrafficTrack> Traffic { get; } = [];


    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        SetMessages("No transponder discovered");
        registry.Changed += OnChanged;
        activeVehicle.Changed += OnChanged;
        Refresh();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        registry.Changed -= OnChanged;
        activeVehicle.Changed -= OnChanged;
    }

    private void OnChanged(EventArgs args)
    {
        Dispatcher.Dispatch(Refresh);
    }
    private void OnChanged()
    {
        Dispatcher.Dispatch(Refresh);
    }
    private void Refresh()
    {
        var systemId = activeVehicle.VehicleId?.SystemId;
        if (systemId is null)
        {
            SetMessages("No active vehicle");
            return;
        }

        Replace(Components, registry.GetTransponders(systemId.Value));
        Replace(Traffic, registry.GetTraffic(systemId.Value, DateTimeOffset.UtcNow));
        SetMessages(Components.Count == 0 ? "No supported uAvionix transponder discovered" : $"{Components.Count} transponder component(s)");
    }

    private static void Replace<T>(ObservableRangeCollection<T> target, IEnumerable<T> values)
    {
        target.ReplaceRange(values);
    }
}

