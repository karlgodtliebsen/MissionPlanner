using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.Vehicles.Abstractions;
using BaseViewModel = MissionPlanner.App.Helpers.BaseViewModel;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents discovered component-scoped transponder and nearby traffic state.</summary>
public partial class TransponderTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleComponentRegistry registry;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<TransponderTabViewModel> logger;

    /// <summary>Initializes a transient transponder view model.</summary>
    public TransponderTabViewModel(IActiveVehicleContext activeVehicle, IVehicleComponentRegistry registry, IDispatcher dispatcher,
        ILogger<TransponderTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.registry = registry;
        this.dispatcher = dispatcher;
        this.logger = logger;
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
        dispatcher.Dispatch(Refresh);
    }
    private void OnChanged()
    {
        dispatcher.Dispatch(Refresh);
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
