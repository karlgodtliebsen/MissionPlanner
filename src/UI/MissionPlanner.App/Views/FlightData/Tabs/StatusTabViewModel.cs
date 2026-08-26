using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents searchable promoted telemetry using the shared descriptor catalog.</summary>
public partial class StatusTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext active;
    private readonly ITelemetryFieldCatalog catalog;
    private readonly ITelemetrySnapshotProjector projector;
    private readonly IPlannerSettingsService settings;
    private readonly IDomainEventHub events;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IDispatcher dispatcher;
    private IDisposable subscription;
    private readonly CancellationTokenSource lifetime = new();
    private int pending;

    /// <summary>Initializes a transient Status tab.</summary>
    public StatusTabViewModel(IActiveVehicleContext active, ITelemetryFieldCatalog catalog, ITelemetrySnapshotProjector projector,
        IPlannerSettingsService settings, IDomainEventHub events, IDateTimeProvider dateTimeProvider, IDispatcher dispatcher,
        ILogger<StatusTabViewModel> logger)
        : base(logger)
    {
        this.active = active;
        this.catalog = catalog;
        this.projector = projector;
        this.settings = settings;
        this.events = events;
        this.dateTimeProvider = dateTimeProvider;
        this.dispatcher = dispatcher;
    }

    /// <summary>Gets stable telemetry rows.</summary>
    public ObservableRangeCollection<StatusTelemetryItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets search text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Gets a versioned JSON snapshot.</summary>
    [ObservableProperty]
    public partial string ExportJson { get; private set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        foreach (var row in Items)
        {
            row.IsVisible = string.IsNullOrWhiteSpace(value)
                            || row.Label.Contains(value, StringComparison.OrdinalIgnoreCase)
                            || row.Category.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
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
        var items = new List<StatusTelemetryItemViewModel>();
        foreach (var descriptor in catalog.Fields.OrderBy(x => x.Category).ThenBy(x => x.Label))
        {
            items.Add(new StatusTelemetryItemViewModel(descriptor));
        }
        Items.ReplaceRange(items);
        active.Changed += OnChanged;
        subscription = events.SubscribeDomainEventAsync<VehicleStateUpdated>(OnUpdated);
        Update();
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
        active.Changed -= OnChanged;
        subscription?.Dispose();
        subscription = null!;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private void OnChanged(EventArgs e)
    {
        dispatcher.Dispatch(Update);
    }

    private async Task OnUpdated(VehicleStateUpdated e, CancellationToken token)
    {
        if (e.VehicleId == active.VehicleId && Interlocked.Exchange(ref pending, 1) == 0)
        {
            await Later(lifetime.Token);
        }
    }

    private Task Later(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1d / Math.Clamp(settings.Current.Telemetry.DisplayRateHz, 1, 30)), Update);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref pending, 0);
        }
        return Task.CompletedTask;
    }

    private void Update()
    {
        var state = active.State;
        var now = dateTimeProvider.UtcNow;
        foreach (var row in Items)
        {
            row.Update(state is null ? null : projector.Project(row.Descriptor, state, settings.Current.Units.System, now));
        }

        ExportJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                capturedAt = now,
                vehicle = active.VehicleId?.ToString(),
                fields = Items.Select(x =>
                    new
                    {
                        x.Descriptor.Key,
                        x.RawValue,
                        x.Value,
                        x.Unit,
                        x.Freshness,
                        x.ObservedAt
                    })
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
