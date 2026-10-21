using System.Globalization;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Draws, edits, saves and imports the planning polygon and imported geospatial overlays.</summary>
public interface IPolygonPlanningWorkflow : IMissionWorkflow
{
    /// <summary>Gets the imported overlay features currently drawn over the map.</summary>
    IReadOnlyList<ImportedPlanningOverlay> ImportedOverlays { get; }

    /// <summary>Stores the drawn vertices as the planning polygon.</summary>
    void FinishPolygon();

    /// <summary>Cancels polygon drawing.</summary>
    void CancelPolygon();

    /// <summary>Clears the planning polygon.</summary>
    void ClearPolygon();

    /// <summary>Creates the planning polygon from the mission waypoints.</summary>
    void PolygonFromWaypoints();

    /// <summary>Previews and, after confirmation, applies a signed offset to the polygon.</summary>
    Task OffsetPolygonAsync(CancellationToken cancellationToken);

    /// <summary>Reports the polygon area in several units.</summary>
    void ShowPolygonArea();

    /// <summary>Saves the planning polygon to a file.</summary>
    Task SavePolygonAsync(CancellationToken cancellationToken);

    /// <summary>Loads a planning polygon file.</summary>
    Task LoadPolygonAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the imported overlay with the features of a KML/KMZ file.</summary>
    Task KmlOverlayAsync(CancellationToken cancellationToken);

    /// <summary>Clears the imported overlay.</summary>
    void ClearImportedOverlay();

    /// <summary>Imports a KML/KMZ file as waypoints or a polygon.</summary>
    Task LoadKmlFileAsync(CancellationToken cancellationToken);

    /// <summary>Imports a shapefile as waypoints or a polygon.</summary>
    Task LoadShapefileAsync(CancellationToken cancellationToken);

    /// <summary>Uses a polygon from a shapefile as the planning polygon.</summary>
    Task PolygonFromShapefileAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IPolygonPlanningWorkflow" />
public sealed class PolygonPlanningWorkflow : MissionWorkflowBase, IPolygonPlanningWorkflow
{
    private IReadOnlyList<ImportedPlanningOverlay> importedOverlays = [];

    /// <inheritdoc />
    public IReadOnlyList<ImportedPlanningOverlay> ImportedOverlays => importedOverlays;

    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IUserConfirmationService confirmationService;
    private readonly IDialogService dialogService;
    private readonly IMissionFileInteraction fileInteraction;

    /// <summary>Initializes a new instance of the <see cref="PolygonPlanningWorkflow"/> class.</summary>
    public PolygonPlanningWorkflow(IDateTimeProvider dateTimeProvider, IUserConfirmationService confirmationService, IDialogService dialogService, IMissionFileInteraction fileInteraction)
    {
        this.dateTimeProvider = dateTimeProvider;
        this.confirmationService = confirmationService;
        this.dialogService = dialogService;
        this.fileInteraction = fileInteraction;
    }

    /// <inheritdoc />
    public void FinishPolygon()
    {
        if (Interaction.State.Mode != MissionMapInteractionMode.DrawPolygon)
        {
            ShowStatus("Polygon drawing is not active.");
            return;
        }

        var result = Polygons.Set("Planning polygon", Interaction.State.Positions);
        if (!result.Succeeded)
        {
            ShowStatus(result.Message);
            return;
        }

        Interaction.Complete();
        ShowStatus(result.Message);
    }

    /// <inheritdoc />
    public void CancelPolygon()
    {
        Interaction.Cancel();
        ShowStatus("Polygon drawing cancelled.");
    }

    /// <inheritdoc />
    public void ClearPolygon()
    {
        Polygons.Clear();
        Interaction.Cancel();
        ShowStatus("Planning polygon cleared.");
    }

    /// <inheritdoc />
    public void PolygonFromWaypoints()
    {
        var result = Polygons.FromMission(Mission);
        ShowStatus(result.Message);
    }

    /// <inheritdoc />
    public async Task OffsetPolygonAsync(CancellationToken cancellationToken)
    {
        var text = await dialogService.PromptAsync("Offset polygon", "Signed offset distance in metres (positive outward)", "10");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var metres))
        {
            if (text is not null)
            {
                ShowStatus("Enter a valid offset distance.");
            }

            return;
        }

        var result = Polygons.PreviewOffset(metres);
        if (!result.Succeeded || result.Preview is null)
        {
            ShowStatus(result.Message);
            return;
        }

        if (await confirmationService.ConfirmAsync("Apply polygon offset", $"Replace the polygon with the {metres:F1} m offset preview?", "Apply", cancellationToken))
        {
            ShowStatus(Polygons.ApplyPreview(result.Preview).Message);
        }
    }

    /// <inheritdoc />
    public void ShowPolygonArea()
    {
        var area = Polygons.CalculateArea();
        ShowStatus(area is null ? "Create a polygon first." : $"Area: {area.SquareMeters:F1} m² • {area.Hectares:F3} ha • {area.SquareKilometers:F4} km² • {area.Acres:F3} acres • {area.SquareFeet:F0} ft²");
    }

    /// <inheritdoc />
    public async Task SavePolygonAsync(CancellationToken cancellationToken)
    {
        if (Polygons.Snapshot.Polygon is null)
        {
            ShowStatus("Create a polygon first.");
            return;
        }

        var path = await fileInteraction.SaveTextAsync("planning-polygon.mppolygon", Polygons.Serialize(dateTimeProvider.UtcNow), cancellationToken);
        ShowStatus(path is null ? "Polygon save cancelled." : $"Polygon saved to {path}.");
    }

    /// <inheritdoc />
    public async Task LoadPolygonAsync(CancellationToken cancellationToken)
    {
        if (await fileInteraction.OpenTextAsync("Open MissionPlanner polygon", cancellationToken) is { } file)
        {
            ShowStatus(Polygons.Deserialize(file.Text).Message);
        }
    }

    /// <inheritdoc />
    public async Task KmlOverlayAsync(CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync("Open KML overlay", false, cancellationToken);
        if (imported is null)
        {
            return;
        }

        importedOverlays = imported.Features.Where(feature => feature.Positions.Count > 0)
            .Select(feature => new ImportedPlanningOverlay(feature.Name, feature.Positions, feature.Kind == GeospatialGeometryKind.Polygon)).ToArray();
        UpdatePlanningOverlay();
        ShowStatus($"Overlay replaced with {importedOverlays.Count} imported features.");
    }

    /// <inheritdoc />
    public void ClearImportedOverlay()
    {
        importedOverlays = [];
        UpdatePlanningOverlay();
        ShowStatus("Imported overlay cleared.");
    }

    /// <inheritdoc />
    public async Task LoadKmlFileAsync(CancellationToken cancellationToken)
    {
        await ImportMissionGeometryAsync(false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LoadShapefileAsync(CancellationToken cancellationToken)
    {
        await ImportMissionGeometryAsync(true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PolygonFromShapefileAsync(CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync("Open polygon shapefile", true, cancellationToken);
        if (imported is null)
        {
            return;
        }

        var polygons = imported.Features.Where(feature => feature.Kind == GeospatialGeometryKind.Polygon).ToArray();
        if (polygons.Length == 0)
        {
            ShowStatus("The shapefile contains no polygon geometry.");
            return;
        }
        var options = dialogService.CreateOptions("Choose planning polygon");

        var selectedName = polygons.Length == 1
            ? polygons[0].Name
            : await dialogService.ChooseAsync(options, polygons.Select(feature => feature.Name).ToArray(), cancellationToken);
        var selected = polygons.FirstOrDefault(feature => feature.Name == selectedName);
        if (selected is not null)
        {
            ShowStatus(Polygons.Set(selected.Name, selected.Positions).Message);
        }
    }

    private async Task ImportMissionGeometryAsync(bool shapefile, CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync(shapefile ? "Open shapefile" : "Open KML/KMZ file", shapefile, cancellationToken);
        if (imported is null)
        {
            return;
        }

        var preview = imported.Preview;
        var message = $"{preview.Points} points, {preview.LineStrings} lines, " +
                      $"{preview.Polygons} polygons, " +
                      $"{preview.MissionCandidates} waypoint candidates, " +
                      $"{preview.Unsupported} unsupported";

        var options = dialogService.CreateOptions("Choose planning polygon");
        options.Title = message;
        var choice = await dialogService.ChooseAsync(
            options,
            ["Append waypoints", "Replace mission", "Use first polygon as planning polygon"], cancellationToken);

        if (choice == "Use first polygon as planning polygon")
        {
            var polygon = imported.Features.FirstOrDefault(feature => feature.Kind == GeospatialGeometryKind.Polygon);
            ShowStatus(polygon is null ? "No polygon geometry was found." : Polygons.Set(polygon.Name, polygon.Positions).Message);
            return;
        }

        if (choice is not ("Append waypoints" or "Replace mission"))
        {
            return;
        }

        var candidates = imported.Features.Where(feature => feature.Kind is GeospatialGeometryKind.Point or GeospatialGeometryKind.LineString)
            .SelectMany(feature => feature.Positions.Select(position => (Position: position, feature.AltitudeMeters))).ToArray();
        if (candidates.Length == 0)
        {
            ShowStatus("No point or line geometry can be imported as waypoints.");
            return;
        }

        var target = choice == "Replace mission" ? new Mission(MissionId.New(), "Imported mission") : Mission;
        foreach (var candidate in candidates)
        {
            target.Add(new WaypointMissionItem(MissionItemId.New(), 0, candidate.Position,
                new MissionAltitude(candidate.AltitudeMeters ?? DefaultAltitudeMeters, MissionAltitudeReference.Home), TimeSpan.Zero, WaypointRadiusMeters));
        }

        if (!ReferenceEquals(target, Mission))
        {
            ReplaceMission(target, $"Mission replaced with {candidates.Length} imported waypoints.");
        }
        else
        {
            OnMissionChanged($"Appended {candidates.Length} imported waypoints.");
        }
    }

    private async Task<GeospatialImportResult?> OpenGeospatialAsync(string title, bool includeCompanions, CancellationToken cancellationToken)
    {
        var result = await fileInteraction.ImportGeospatialAsync(title, includeCompanions, cancellationToken);
        if (result.Message is not null)
        {
            ShowStatus(result.Message);
        }

        return result.Imported;
    }
}
