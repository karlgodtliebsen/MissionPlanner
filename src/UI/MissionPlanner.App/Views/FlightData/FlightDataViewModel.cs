using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.Missions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Coordinates the Flight Data page, its active tab, and active-vehicle status presentation.
/// </summary>
public partial class FlightDataViewModel : ViewModelBase
{
    /// <summary>Shared readiness summary and diagnostic navigation.</summary>
    public Views.Diagnostics.LiveTelemetryInspectorViewModel? Diagnostics { get; }

    /// <summary>The shared mission map editor (same instance as the FlightData map).</summary>
    public MissionMapViewModel Map
    {
        get; private set;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await Map.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        await Map.DeactivateAsync();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataViewModel"/> class.
    /// </summary>
    /// <param name="map">The shared mission map editor.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="diagnostics">Shared active-vehicle readiness presentation.</param>
    public FlightDataViewModel(FlightDataMissionMapViewModel map, ILogger<FlightDataViewModel> logger,
        Views.Diagnostics.LiveTelemetryInspectorViewModel? diagnostics = null) : base(logger)
    {
        Map = map;
        Diagnostics = diagnostics;
        Logger.LogTrace("FlightDataViewModel initialized.");
    }

}
