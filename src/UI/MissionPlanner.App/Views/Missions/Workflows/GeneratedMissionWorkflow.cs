using System.Globalization;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Generates circle, text and survey missions, previews them on the map, and applies them to the mission.</summary>
public interface IGeneratedMissionWorkflow : IMissionWorkflow
{
    /// <summary>Gets the generated mission preview currently drawn over the map.</summary>
    IReadOnlyList<GeoPosition> SurveyPreview { get; }

    /// <summary>Generates a waypoint circle around the target position.</summary>
    Task CreateWaypointCircleAsync(CancellationToken cancellationToken);

    /// <summary>Generates a spline circle around the target position.</summary>
    Task CreateSplineCircleAsync(CancellationToken cancellationToken);

    /// <summary>Generates waypoints that draw text at the target position.</summary>
    Task AutoWaypointTextAsync(CancellationToken cancellationToken);

    /// <summary>Generates a circular survey around the target position.</summary>
    Task CreateCircleSurveyAsync(CancellationToken cancellationToken);

    /// <summary>Generates a grid survey over the planning polygon.</summary>
    Task CreateGridSurveyAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IGeneratedMissionWorkflow" />
public sealed class GeneratedMissionWorkflow : MissionWorkflowBase, IGeneratedMissionWorkflow
{
    private IReadOnlyList<GeoPosition> generatedPreview = [];

    /// <inheritdoc />
    public IReadOnlyList<GeoPosition> SurveyPreview => generatedPreview;

    private readonly IAutoWaypointGenerator autoWaypointGenerator;
    private readonly ISurveyMissionGenerator surveyMissionGenerator;
    private readonly IDialogService dialogService;
    private readonly IUserConfirmationService confirmationService;

    /// <summary>Initializes a new instance of the <see cref="GeneratedMissionWorkflow"/> class.</summary>
    public GeneratedMissionWorkflow(IAutoWaypointGenerator autoWaypointGenerator, ISurveyMissionGenerator surveyMissionGenerator, IDialogService dialogService, IUserConfirmationService confirmationService)
    {
        this.autoWaypointGenerator = autoWaypointGenerator;
        this.surveyMissionGenerator = surveyMissionGenerator;
        this.dialogService = dialogService;
        this.confirmationService = confirmationService;
    }

    /// <inheritdoc />
    public async Task CreateWaypointCircleAsync(CancellationToken cancellationToken)
    {
        await CreateCircleAsync(false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateSplineCircleAsync(CancellationToken cancellationToken)
    {
        await CreateCircleAsync(true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AutoWaypointTextAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } origin)
        {
            ShowStatus("Select a map position for the text origin.");
            return;
        }

        var text = await dialogService.PromptAsync("Waypoint text", "Text (supported stroke-font letters/digits, maximum 32)", "HOME");
        var heightText = await dialogService.PromptAsync("Waypoint text", "Character height in metres", "50");
        if (text is null || !double.TryParse(heightText, NumberStyles.Float, CultureInfo.CurrentCulture, out var height))
        {
            return;
        }

        await PreviewAndApplyGeneratedAsync(autoWaypointGenerator.GenerateText(new TextWaypointRequest(text, origin, height, 0, .3, DefaultAltitude())), cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateCircleSurveyAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } center)
        {
            ShowStatus("Select a circle-survey center.");
            return;
        }

        var outerText = await dialogService.PromptAsync("Circle survey", "Outer radius in metres", "200");
        var spacingText = await dialogService.PromptAsync("Circle survey", "Radial spacing in metres", "50");
        if (!double.TryParse(outerText, NumberStyles.Float, CultureInfo.CurrentCulture, out var outer)
            || !double.TryParse(spacingText, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing))
        {
            return;
        }

        var result = surveyMissionGenerator.GenerateCircle(new CircleSurveyRequest(center, spacing, outer, spacing, 24, true, DefaultAltitude()));
        await PreviewAndApplySurveyAsync(result, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateGridSurveyAsync(CancellationToken cancellationToken)
    {
        if (Polygons.Snapshot.Polygon is not { } polygon)
        {
            ShowStatus("Create or load a planning polygon first.");
            return;
        }

        var spacingText = await dialogService.PromptAsync("Grid survey", "Flight-line spacing in metres", "30");
        var angleText = await dialogService.PromptAsync("Grid survey", "Flight-line angle in degrees", "0");
        if (!double.TryParse(spacingText, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing)
            || !double.TryParse(angleText, NumberStyles.Float, CultureInfo.CurrentCulture, out var angle))
        {
            return;
        }

        var cross = await confirmationService.ConfirmAsync("Cross-grid", "Add a perpendicular second grid?", "Cross-grid", cancellationToken);
        await PreviewAndApplySurveyAsync(surveyMissionGenerator.GenerateGrid(new GridSurveyRequest(polygon, angle, spacing, 0, DefaultAltitude(), cross)), cancellationToken);
    }

    private async Task CreateCircleAsync(bool spline, CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } center)
        {
            ShowStatus("Select a map position for the circle center.");
            return;
        }

        var radiusText = await dialogService.PromptAsync("Generate circle", "Radius in metres", "100");
        var countText = await dialogService.PromptAsync("Generate circle", "Number of points (3-1000)", "12");
        if (!double.TryParse(radiusText, NumberStyles.Float, CultureInfo.CurrentCulture, out var radius)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count))
        {
            ShowStatus("Enter a valid radius and point count.");
            return;
        }
        var options = dialogService.CreateOptions("Circle direction");
        var direction = await dialogService.ChooseAsync(options, ["Clockwise", "Counter-clockwise"], cancellationToken);
        if (direction is null)
        {
            return;
        }

        double? endAltitude = null;
        if (spline)
        {
            var endText = await dialogService.PromptAsync("Spline circle", "End altitude in metres (for deterministic helical climb)", DefaultAltitudeMeters.ToString("F0", CultureInfo.CurrentCulture));
            if (!double.TryParse(endText, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed))
            {
                return;
            }

            endAltitude = parsed;
        }

        await PreviewAndApplyGeneratedAsync(autoWaypointGenerator.GenerateCircle(new CircleWaypointRequest(center, radius, count, direction == "Clockwise", 0,
            DefaultAltitude(), endAltitude, spline, false)), cancellationToken);
    }

    private async Task PreviewAndApplyGeneratedAsync(AutoWaypointGenerationResult result, CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            ShowStatus(result.Message);
            return;
        }

        generatedPreview = result.PreviewPositions;
        UpdatePlanningOverlay();
        var options = dialogService.CreateOptions($"Preview: {result.Items.Count} generated mission items");

        var choice = await dialogService.ChooseAsync(options, ["Append to mission", "Replace mission"], cancellationToken);
        if (choice is not ("Append to mission" or "Replace mission"))
        {
            generatedPreview = [];
            UpdatePlanningOverlay();
            return;
        }

        var target = choice == "Replace mission" ? new Mission(MissionId.New(), "Generated mission") : Mission;
        foreach (var item in result.Items)
        {
            target.Add(item);
        }

        generatedPreview = [];
        UpdatePlanningOverlay();
        if (ReferenceEquals(target, Mission))
        {
            OnMissionChanged($"Appended {result.Items.Count} generated mission items.");
        }
        else
        {
            ReplaceMission(target, $"Mission replaced with {result.Items.Count} generated mission items.");
        }
    }

    private async Task PreviewAndApplySurveyAsync(SurveyMissionResult result, CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            ShowStatus(result.Message);
            return;
        }

        if (result.Statistics is { } statistics)
        {
            ShowStatus($"Preview: {statistics.LineCount} legs, {statistics.PointCount} points, {statistics.DistanceMeters:F0} m, {statistics.AreaSquareMeters:F0} m².");
        }

        await PreviewAndApplyGeneratedAsync(new AutoWaypointGenerationResult(true, result.Message, result.Items, result.Preview), cancellationToken);
    }
}
