using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Displays a bounded-rate dashboard projected from promoted vehicle state.</summary>
public sealed class GaugesTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ITelemetryFieldCatalog catalog;
    private readonly ITelemetrySnapshotProjector projector;
    private readonly IPlannerSettingsService settings;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDispatcher dispatcher;
    private IDisposable subscription;
    private CancellationTokenSource lifetime = new();
    private int updatePending;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>Initializes a transient gauges dashboard.</summary>
    public GaugesTabViewModel(IActiveVehicleContext activeVehicle, ITelemetryFieldCatalog catalog,
        IDateTimeProvider dateTimeProvider,
        ITelemetrySnapshotProjector projector, IPlannerSettingsService settings, IDomainEventHub domainEventHub, IDispatcher dispatcher, ILogger<GaugesTabViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.catalog = catalog;
        this.dateTimeProvider = dateTimeProvider;
        this.projector = projector;
        this.settings = settings;
        this.domainEventHub = domainEventHub;
        this.dispatcher = dispatcher;
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
        DeactivateAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        lifetime = new();
        activeVehicle.Changed += OnChanged;
        settings.SettingsChanged += OnSettingsChanged;
        subscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnStateUpdated);
        Update();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnChanged;
        settings.SettingsChanged -= OnSettingsChanged;
        subscription.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
        return Task.CompletedTask;
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(Update);
    }

    private void OnSettingsChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(Update);
    }

    private async Task OnStateUpdated(VehicleStateUpdated evt, CancellationToken token)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && Interlocked.Exchange(ref updatePending, 1) == 0)
        {
            await PublishAsync(lifetime.Token);
        }
    }

    private async Task PublishAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1d / Math.Clamp(settings.Current.Telemetry.DisplayRateHz, 1, 30)), token);
            dispatcher.Dispatch(Update);
        }
        catch (OperationCanceledException)
        {
            Debug.Print("Operation canceled.");
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
