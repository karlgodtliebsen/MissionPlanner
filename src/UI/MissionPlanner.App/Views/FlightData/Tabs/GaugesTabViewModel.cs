using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Displays a bounded-rate dashboard projected from promoted vehicle state.</summary>
public sealed class GaugesTabViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ITelemetryFieldCatalog catalog;
    private readonly ITelemetrySnapshotProjector projector;
    private readonly IPlannerSettingsService settings;
    private readonly IDomainEventHub domainEventHub;
    private IDisposable? subscription;
    private bool active;
    private int updatePending;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>Initializes a transient gauges dashboard.</summary>
    public GaugesTabViewModel(IActiveVehicleContext activeVehicle, ITelemetryFieldCatalog catalog,
        IDateTimeProvider dateTimeProvider,
        ITelemetrySnapshotProjector projector, IPlannerSettingsService settings, IDomainEventHub domainEventHub, ILogger<GaugesTabViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.catalog = catalog;
        this.dateTimeProvider = dateTimeProvider;
        this.projector = projector;
        this.settings = settings;
        this.domainEventHub = domainEventHub;
        VerticalSpeed = CreateTile("vertical-speed");
        Speed = CreateTile("ground-speed");
        Altitude = CreateTile("altitude-relative");
        Heading = CreateTile("heading");
        Tiles = [VerticalSpeed, Speed, Altitude, Heading];
    }

    /// <summary>Gets stable dashboard tiles updated in place.</summary>
    public ObservableCollection<GaugeTileViewModel> Tiles
    {
        get;
    }

    /// <summary>Gets the vertical-speed instrument.</summary>
    public GaugeTileViewModel VerticalSpeed
    {
        get;
    }

    /// <summary>Gets the ground-speed instrument.</summary>
    public GaugeTileViewModel Speed
    {
        get;
    }

    /// <summary>Gets the relative-altitude instrument.</summary>
    public GaugeTileViewModel Altitude
    {
        get;
    }

    /// <summary>Gets the heading instrument.</summary>
    public GaugeTileViewModel Heading
    {
        get;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (active)
        {
            return Task.CompletedTask;
        }

        active = true;
        activeVehicle.Changed += OnChanged;
        settings.SettingsChanged += OnSettingsChanged;
        subscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnStateUpdated);
        Update();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnChanged;
        settings.SettingsChanged -= OnSettingsChanged;
        subscription?.Dispose();
        subscription = null;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void OnChanged(EventArgs args)
    {
        Dispatcher.Dispatch(Update);
    }

    private void OnSettingsChanged(EventArgs args)
    {
        Dispatcher.Dispatch(Update);
    }

    private async Task OnStateUpdated(VehicleStateUpdated evt, CancellationToken token)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && Interlocked.Exchange(ref updatePending, 1) == 0)
        {
            await PublishAsync(token);
        }
    }

    private async Task PublishAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1d / Math.Clamp(settings.Current.Telemetry.DisplayRateHz, 1, 30)), token);
            await Dispatcher.DispatchAsync(() =>
            {
                if (active)
                {
                    Update();
                }
            });
        }
        catch (OperationCanceledException)
        {
            Debug.Print("GaugesTabViewModel-PublishAsync-Operation canceled.");
        }
        finally
        {
            Interlocked.Exchange(ref updatePending, 0);
        }
    }
    private void Update()
    {
        var state = activeVehicle.State;
        foreach (var tile in Tiles)
        {
            tile.Update(state is null ? null : projector.Project(tile.Descriptor, state, settings.Current.Units.System, dateTimeProvider.UtcNow));
        }
    }

    private GaugeTileViewModel CreateTile(string key)
    {
        return new(catalog.Fields.Single(field => field.Key == key));
    }
}
