using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Displays a bounded-rate dashboard projected from promoted vehicle state.</summary>
public sealed class GaugesTabViewModel : IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ITelemetryFieldCatalog catalog;
    private readonly ITelemetrySnapshotProjector projector;
    private readonly IPlannerSettingsService settings;
    private readonly IDispatcher dispatcher;
    private readonly IDisposable subscription;
    private readonly CancellationTokenSource lifetime = new();
    private int updatePending;

    /// <summary>Initializes a transient gauges dashboard.</summary>
    public GaugesTabViewModel(IActiveVehicleContext activeVehicle, ITelemetryFieldCatalog catalog,
        ITelemetrySnapshotProjector projector, IPlannerSettingsService settings, IDomainEventHub events, IDispatcher dispatcher)
    {
        this.activeVehicle = activeVehicle; this.catalog = catalog; this.projector = projector; this.settings = settings; this.dispatcher = dispatcher;
        VerticalSpeed = CreateTile("vertical-speed");
        Speed = CreateTile("ground-speed");
        Altitude = CreateTile("altitude-relative");
        Heading = CreateTile("heading");
        Tiles = [VerticalSpeed, Speed, Altitude, Heading];
        activeVehicle.Changed += OnChanged;
        settings.SettingsChanged += OnSettingsChanged;
        subscription = events.SubscribeDomainEventAsync<VehicleStateUpdated>(OnStateUpdated);
        Update();
    }

    /// <summary>Gets stable dashboard tiles updated in place.</summary>
    public ObservableCollection<GaugeTileViewModel> Tiles { get; }

    /// <summary>Gets the vertical-speed instrument.</summary>
    public GaugeTileViewModel VerticalSpeed { get; }

    /// <summary>Gets the ground-speed instrument.</summary>
    public GaugeTileViewModel Speed { get; }

    /// <summary>Gets the relative-altitude instrument.</summary>
    public GaugeTileViewModel Altitude { get; }

    /// <summary>Gets the heading instrument.</summary>
    public GaugeTileViewModel Heading { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        activeVehicle.Changed -= OnChanged; settings.SettingsChanged -= OnSettingsChanged; subscription.Dispose(); lifetime.Cancel(); lifetime.Dispose();
    }

    private void OnChanged(object? sender, EventArgs args) => dispatcher.Dispatch(Update);
    private void OnSettingsChanged(object? sender, EventArgs args) => dispatcher.Dispatch(Update);
    private Task OnStateUpdated(VehicleStateUpdated evt, CancellationToken token)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && Interlocked.Exchange(ref updatePending, 1) == 0) _ = PublishAsync(lifetime.Token);
        return Task.CompletedTask;
    }
    private async Task PublishAsync(CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(1d / Math.Clamp(settings.Current.Telemetry.DisplayRateHz, 1, 30)), token); dispatcher.Dispatch(Update); }
        catch (OperationCanceledException) { }
        finally { Interlocked.Exchange(ref updatePending, 0); }
    }
    private void Update()
    {
        var state = activeVehicle.State;
        foreach (var tile in Tiles) tile.Update(state is null ? null : projector.Project(tile.Descriptor, state, settings.Current.Units.System, DateTimeOffset.UtcNow));
    }

    private GaugeTileViewModel CreateTile(string key) =>
        new(catalog.Fields.Single(field => field.Key == key));
}

/// <summary>Provides one stable bindable gauge tile.</summary>
public partial class GaugeTileViewModel(TelemetryFieldDescriptor descriptor) : ObservableObject
{
    /// <summary>Gets the field descriptor.</summary>
    public TelemetryFieldDescriptor Descriptor { get; } = descriptor;
    /// <summary>Gets the label.</summary>
    public string Label => Descriptor.Label;
    /// <summary>Gets the formatted reading.</summary>
    [ObservableProperty] public partial string Value { get; private set; } = "Unavailable";
    /// <summary>Gets the formatted unit.</summary>
    [ObservableProperty] public partial string Unit { get; private set; } = string.Empty;
    /// <summary>Gets the explicit freshness label.</summary>
    [ObservableProperty] public partial string Freshness { get; private set; } = "Unavailable";
    /// <summary>Gets the numeric reading used to position an analog needle.</summary>
    [ObservableProperty] public partial double? NumericValue { get; private set; }
    /// <summary>Updates this object without replacing it.</summary>
    public void Update(TelemetryValueSnapshot? snapshot)
    {
        Value = snapshot?.DisplayValue ?? "Unavailable";
        Unit = snapshot?.Unit ?? string.Empty;
        Freshness = snapshot?.Freshness.ToString() ?? "Unavailable";
        NumericValue = snapshot is not null && double.TryParse(
            snapshot.DisplayValue,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out var numericValue)
                ? numericValue
                : null;
    }
}
