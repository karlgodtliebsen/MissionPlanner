using System.Globalization;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Prefetch;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Prefetches map tiles, manages points of interest and tracker home, converts UTM coordinates, and rotates the map.</summary>
public interface IMapToolsWorkflow : IMissionWorkflow
{
    /// <summary>Warms the online tile cache around the target position after confirmation.</summary>
    Task PrefetchVisibleAsync(CancellationToken cancellationToken);

    /// <summary>Warms the online tile cache along the mission route after confirmation.</summary>
    Task PrefetchWaypointPathAsync(CancellationToken cancellationToken);

    /// <summary>Saves a local point of interest at the target position.</summary>
    Task AddPoiAsync(CancellationToken cancellationToken);

    /// <summary>Edits the point of interest nearest the target position.</summary>
    Task EditPoiAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the point of interest nearest the target position after confirmation.</summary>
    Task DeletePoiAsync(CancellationToken cancellationToken);

    /// <summary>Stores the target position as the local tracker home.</summary>
    Task SetTrackerHomeAsync(CancellationToken cancellationToken);

    /// <summary>Converts a typed UTM coordinate and offers to add a waypoint or center the map.</summary>
    Task EnterUtmCoordinateAsync(CancellationToken cancellationToken);

    /// <summary>Rotates the map to a typed bearing for this session.</summary>
    Task RotateMapAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IMapToolsWorkflow" />
public sealed class MapToolsWorkflow : MissionWorkflowBase, IMapToolsWorkflow
{
    private readonly IMapTilePrefetchService mapTilePrefetchService;
    private readonly IPoiService poiService;
    private readonly ITrackerHomeService trackerHomeService;
    private readonly IGeodeticCoordinateConverter geodeticConverter;
    private readonly IDialogService dialogService;
    private readonly IUserConfirmationService confirmationService;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>Initializes a new instance of the <see cref="MapToolsWorkflow"/> class.</summary>
    public MapToolsWorkflow(IMapTilePrefetchService mapTilePrefetchService, IPoiService poiService, ITrackerHomeService trackerHomeService, IGeodeticCoordinateConverter geodeticConverter, IDialogService dialogService, IUserConfirmationService confirmationService, IDateTimeProvider dateTimeProvider)
    {
        this.mapTilePrefetchService = mapTilePrefetchService;
        this.poiService = poiService;
        this.trackerHomeService = trackerHomeService;
        this.geodeticConverter = geodeticConverter;
        this.dialogService = dialogService;
        this.confirmationService = confirmationService;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task PrefetchVisibleAsync(CancellationToken cancellationToken)
    {
        var center = TargetPosition() ?? HomePosition;
        if (center is null)
        {
            ShowStatus("Select a map position before visible-area prefetch.");
            return;
        }

        await RunPrefetchAsync([new MapPrefetchBounds(center.Value.LatitudeDegrees - .02, center.Value.LongitudeDegrees - .03, center.Value.LatitudeDegrees + .02, center.Value.LongitudeDegrees + .03)], cancellationToken);
    }

    /// <inheritdoc />
    public async Task PrefetchWaypointPathAsync(CancellationToken cancellationToken)
    {
        var positions = Mission.Items.Select(Core.Missions.MissionItemGeometry.PositionOf).OfType<GeoPosition>().ToArray();
        if (positions.Length == 0)
        {
            ShowStatus("The mission has no positioned route to prefetch.");
            return;
        }

        const double corridor = .005;
        var areas = positions.Zip(positions.Skip(1), (a, b) => new MapPrefetchBounds(Math.Min(a.LatitudeDegrees, b.LatitudeDegrees) - corridor,
            Math.Min(a.LongitudeDegrees, b.LongitudeDegrees) - corridor, Math.Max(a.LatitudeDegrees, b.LatitudeDegrees) + corridor,
            Math.Max(a.LongitudeDegrees, b.LongitudeDegrees) + corridor)).ToArray();
        if (areas.Length == 0)
        {
            areas = [new MapPrefetchBounds(positions[0].LatitudeDegrees - corridor, positions[0].LongitudeDegrees - corridor, positions[0].LatitudeDegrees + corridor, positions[0].LongitudeDegrees + corridor)];
        }

        await RunPrefetchAsync(areas, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddPoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("Select a map position for the POI.");
            return;
        }
        var options = AvaloniaDialogService.CreateDialogOptions("Add point of interest", "Ok", null);
        var name = await dialogService.PromptAsync(options, $"POI {poiService.Snapshot.Items.Count + 1}", cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var description = await dialogService.PromptAsync(options, "Optional description", string.Empty, cancellationToken);
        await poiService.AddAsync(name, position, PointerAltitude, description, null, cancellationToken);
        ShowStatus($"Local POI '{name}' saved.");
    }

    /// <inheritdoc />
    public async Task EditPoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position || poiService.FindNearest(position) is not { } item)
        {
            ShowStatus("No POI is available to edit.");
            return;
        }
        var options = AvaloniaDialogService.CreateDialogOptions("Edit nearest POI", "Ok", null);
        var name = await dialogService.PromptAsync(options, $"Name ({item.Name})", item.Name, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        var description = await dialogService.PromptAsync(options, "Description", item.Description ?? string.Empty, cancellationToken: cancellationToken);
        await poiService.UpdateAsync(item with
        {
            Name = name,
            Description = description
        }, cancellationToken);
        ShowStatus($"Local POI '{name}' updated.");
    }

    /// <inheritdoc />
    public async Task DeletePoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position || poiService.FindNearest(position) is not { } item)
        {
            ShowStatus("No POI is available to delete.");
            return;
        }

        if (!await confirmationService.ConfirmAsync("Delete nearest POI", $"Delete local POI '{item.Name}'?", "Delete", cancellationToken))
        {
            return;
        }

        await poiService.DeleteAsync(item.Id, cancellationToken);
        ShowStatus($"Local POI '{item.Name}' deleted.");
    }

    /// <inheritdoc />
    public async Task SetTrackerHomeAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("Select a map position for tracker home.");
            return;
        }

        var options = AvaloniaDialogService.CreateDialogOptions("Tracker home", "Ok", null);
        var text = await dialogService.PromptAsync(options, "Optional altitude in metres", PointerAltitude?.ToString("F1", CultureInfo.CurrentCulture) ?? string.Empty, cancellationToken: cancellationToken);
        double? altitude = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed))
            {
                ShowStatus("Enter a valid altitude or leave it empty.");
                return;
            }

            altitude = parsed;
        }

        trackerHomeService.Set(position, altitude, dateTimeProvider.UtcNow, "Mission map context");
        ShowStatus("Local tracker home updated. No antenna-tracker hardware command was sent.");
    }

    /// <inheritdoc />
    public async Task EnterUtmCoordinateAsync(CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions("Enter UTM coordinate");
        var text = await dialogService.PromptAsync(options, "Format: zone+hemisphere easting northing (example: 32N 500000 6170000)", "32N 500000 6170000", cancellationToken: cancellationToken);
        if (text is null)
        {
            return;
        }

        GeographicCoordinate geographic;
        try
        {
            geographic = geodeticConverter.ToGeographic(geodeticConverter.ParseUtm(text));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            ShowStatus(exception.Message);
            return;
        }

        var position = new GeoPosition(geographic.Latitude, geographic.Longitude);
        Host.SetMapContext(position, MissionMapContextSource.CoordinateEntry);

        options = dialogService.CreateOptions($"Converted to {geographic.Latitude:F7}, {geographic.Longitude:F7}");

        var choice = await dialogService.ChooseAsync(options, ["Add waypoint here", "Center map here"], cancellationToken);
        if (choice == "Add waypoint here")
        {
            AddWaypoint(position, "Waypoint added from UTM coordinate.");
        }
        else if (choice == "Center map here")
        {
            Host.RequestMapCenter(position);
            ShowStatus("Map centered on converted UTM coordinate.");
        }
    }

    /// <inheritdoc />
    public async Task RotateMapAsync(CancellationToken cancellationToken)
    {
        var text = await dialogService.PromptAsync("Rotate map", "Bearing degrees (0-359; 0 resets north)", "0");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var angle) || !double.IsFinite(angle))
        {
            if (text is not null)
            {
                ShowStatus("Enter a valid bearing.");
            }

            return;
        }

        angle = ((angle % 360) + 360) % 360;
        Host.RequestMapRotation(angle);
        ShowStatus($"Map rotation set to {angle:F0}° for this session.");
    }

    private async Task RunPrefetchAsync(IReadOnlyList<MapPrefetchBounds> areas, CancellationToken cancellationToken)
    {
        var request = new MapPrefetchRequest(SelectedSourceId, areas, 12, 15);
        var estimate = await mapTilePrefetchService.EstimateAsync(request, cancellationToken);
        if (!estimate.IsAllowed)
        {
            ShowStatus(estimate.Message);
            return;
        }

        if (!await confirmationService.ConfirmAsync("Warm online map cache", $"Fetch {estimate.TileCount} tiles at zoom {estimate.MinimumZoom}-{estimate.MaximumZoom}? This does not create an offline pack.", "Start", cancellationToken))
        {
            return;
        }

        var progress = new Progress<(int Completed, int Total)>(value => ShowStatus($"Prefetch: {value.Completed}/{value.Total}"));
        ShowStatus((await mapTilePrefetchService.PrefetchAsync(request, progress, cancellationToken)).Message);
    }
}
