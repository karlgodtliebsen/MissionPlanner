using System.Globalization;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Downloads, uploads, clears, saves and loads the local geo-fence and rally plans of the active vehicle.</summary>
public interface IVehiclePlanWorkflow : IMissionWorkflow
{
    /// <summary>Downloads the vehicle fence, confirming first when local edits would be replaced.</summary>
    Task DownloadFenceAsync(CancellationToken cancellationToken);

    /// <summary>Validates and uploads the local fence to the vehicle after confirmation.</summary>
    Task UploadFenceAsync(CancellationToken cancellationToken);

    /// <summary>Saves the local fence plan to a file.</summary>
    Task SaveFenceAsync(CancellationToken cancellationToken);

    /// <summary>Loads a fence plan file into the local plan.</summary>
    Task LoadFenceAsync(CancellationToken cancellationToken);

    /// <summary>Clears the local fence plan, or the vehicle fence after confirmation.</summary>
    Task ClearFenceAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the vehicle rally points, confirming first when local edits would be replaced.</summary>
    Task DownloadRallyAsync(CancellationToken cancellationToken);

    /// <summary>Uploads the local rally points to the vehicle after confirmation.</summary>
    Task UploadRallyAsync(CancellationToken cancellationToken);

    /// <summary>Clears the local rally plan, or the vehicle rally points after confirmation.</summary>
    Task ClearRallyAsync(CancellationToken cancellationToken);

    /// <summary>Saves the local rally plan to a file.</summary>
    Task SaveRallyAsync(CancellationToken cancellationToken);

    /// <summary>Loads a rally plan file into the local plan.</summary>
    Task LoadRallyAsync(CancellationToken cancellationToken);

    /// <summary>Asks for a rally altitude, then waits for a map click to place the point.</summary>
    Task SetRallyPointAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IVehiclePlanWorkflow" />
public sealed class VehiclePlanWorkflow : MissionWorkflowBase, IVehiclePlanWorkflow
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFenceConfigurationService fenceService;
    private readonly IRallyConfigurationService rallyService;
    private readonly IFencePlanFileCodec fenceFileCodec;
    private readonly IRallyPlanFileCodec rallyFileCodec;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IUserConfirmationService confirmationService;
    private readonly IDialogService dialogService;
    private readonly IMissionFileInteraction fileInteraction;

    /// <summary>Initializes a new instance of the <see cref="VehiclePlanWorkflow"/> class.</summary>
    public VehiclePlanWorkflow(IActiveVehicleContext activeVehicle, IFenceConfigurationService fenceService, IRallyConfigurationService rallyService, IFencePlanFileCodec fenceFileCodec, IRallyPlanFileCodec rallyFileCodec, IDateTimeProvider dateTimeProvider, IUserConfirmationService confirmationService, IDialogService dialogService, IMissionFileInteraction fileInteraction)
    {
        this.activeVehicle = activeVehicle;
        this.fenceService = fenceService;
        this.rallyService = rallyService;
        this.fenceFileCodec = fenceFileCodec;
        this.rallyFileCodec = rallyFileCodec;
        this.dateTimeProvider = dateTimeProvider;
        this.confirmationService = confirmationService;
        this.dialogService = dialogService;
        this.fileInteraction = fileInteraction;
    }

    /// <inheritdoc />
    public async Task DownloadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Connect a vehicle first.");
            return;
        }

        var snapshot = fenceService.GetSnapshot(vehicleId);
        if (snapshot.IsDirty && !await confirmationService.ConfirmAsync("Replace local fence", "Downloading replaces local fence edits and retains a backup.", "Download", cancellationToken))
        {
            return;
        }

        var report = await fenceService.DownloadAsync(vehicleId, true, FenceProgress(), cancellationToken);
        ShowFenceReport(report);
    }

    /// <inheritdoc />
    public async Task UploadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Connect a vehicle first.");
            return;
        }

        var plan = fenceService.GetSnapshot(vehicleId).LocalPlan;
        var inclusions = plan.Areas.Count(area => area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.CircleInclusion);
        var exclusions = plan.Areas.Count - inclusions;
        if (!await confirmationService.ConfirmAsync("Replace vehicle fence", $"Upload {inclusions} inclusion and {exclusions} exclusion areas{(plan.ReturnPoint is null ? string.Empty : " with a return point")}?", "Validate and upload", cancellationToken))
        {
            return;
        }

        var session = await fenceService.OpenParameterSessionAsync(vehicleId, cancellationToken);
        ShowFenceReport(await fenceService.ApplyAsync(vehicleId, session, FenceProgress(), cancellationToken));
    }

    /// <inheritdoc />
    public async Task SaveFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }

        var path = await fileInteraction.SaveTextAsync("mission-fence.mpfence", fenceFileCodec.Serialize(fenceService.GetSnapshot(vehicleId).LocalPlan), cancellationToken);
        ShowStatus(path is null ? "Fence save cancelled." : $"Fence saved to {path}.");
    }

    /// <inheritdoc />
    public async Task LoadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }

        var current = fenceService.GetSnapshot(vehicleId);
        if (current.IsDirty && !await confirmationService.ConfirmAsync("Replace local fence", "Loading replaces current local edits.", "Load", cancellationToken))
        {
            return;
        }

        if (await fileInteraction.OpenTextAsync("Open MissionPlanner fence", cancellationToken) is not { } file)
        {
            return;
        }

        try
        {
            fenceService.SetLocalPlan(vehicleId, fenceFileCodec.Deserialize(file.Text));
            ShowStatus("Fence loaded locally; upload to apply it.");
        }
        catch (InvalidDataException exception)
        {
            ShowStatus(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task ClearFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }
        var options = dialogService.CreateOptions("Clear Geo-Fence");
        var choice = await dialogService.ChooseAsync(options, ["Clear local plan only", "Clear vehicle fence"], cancellationToken);
        if (choice == "Clear local plan only")
        {
            fenceService.SetLocalPlan(vehicleId, FencePlan.Empty);
            ShowStatus("Local fence cleared; vehicle unchanged.");
        }
        else if (choice == "Clear vehicle fence" && await confirmationService.ConfirmAsync("Clear vehicle fence", "This removes all fence geometry from the vehicle after retaining a backup.", "Clear vehicle", cancellationToken))
        {
            ShowFenceReport(await fenceService.ClearAsync(vehicleId, cancellationToken));
        }
    }

    /// <inheritdoc />
    public async Task DownloadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Connect a vehicle first.");
            return;
        }

        var snapshot = rallyService.GetSnapshot(vehicleId);
        if (snapshot.IsDirty && !await confirmationService.ConfirmAsync("Replace local rally points", "Downloading replaces local rally edits.", "Download", cancellationToken))
        {
            return;
        }

        ShowStatus((await rallyService.DownloadAsync(vehicleId, true, cancellationToken)).Message);
    }

    /// <inheritdoc />
    public async Task UploadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Connect a vehicle first.");
            return;
        }

        var count = rallyService.GetSnapshot(vehicleId).LocalPlan.Points.Count;
        if (await confirmationService.ConfirmAsync("Replace vehicle rally points", $"Upload {count} local rally points as a separate MAVLink rally plan?", "Upload", cancellationToken))
        {
            ShowStatus((await rallyService.UploadAsync(vehicleId, cancellationToken)).Message);
        }
    }

    /// <inheritdoc />
    public async Task ClearRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }
        var options = dialogService.CreateOptions("Clear rally points");

        var choice = await dialogService.ChooseAsync(options, ["Clear local plan only", "Clear vehicle rally points"], cancellationToken);
        if (choice == "Clear local plan only")
        {
            rallyService.SetLocalPlan(vehicleId, RallyPlan.Empty);
            ShowStatus("Local rally points cleared; vehicle unchanged.");
        }
        else if (choice == "Clear vehicle rally points" && await confirmationService.ConfirmAsync("Clear vehicle rally points", "This removes all rally points from the vehicle.", "Clear vehicle", cancellationToken))
        {
            ShowStatus((await rallyService.ClearVehicleAsync(vehicleId, cancellationToken)).Message);
        }
    }

    /// <inheritdoc />
    public async Task SaveRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }

        var path = await fileInteraction.SaveTextAsync("rally-points.mprally", rallyFileCodec.Serialize(rallyService.GetSnapshot(vehicleId).LocalPlan, dateTimeProvider.UtcNow), cancellationToken);
        ShowStatus(path is null ? "Rally save cancelled." : $"Rally points saved to {path}.");
    }

    /// <inheritdoc />
    public async Task LoadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            ShowStatus("Select a vehicle workspace first.");
            return;
        }

        if (await fileInteraction.OpenTextAsync("Open MissionPlanner rally plan", cancellationToken) is not { } file)
        {
            return;
        }

        try
        {
            rallyService.SetLocalPlan(vehicleId, rallyFileCodec.Deserialize(file.Text));
            ShowStatus("Rally points loaded locally; upload to apply them.");
        }
        catch (InvalidDataException exception)
        {
            ShowStatus(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task SetRallyPointAsync(CancellationToken cancellationToken)
    {
        var altitudeText = await dialogService.PromptAsync("Set rally point", "Altitude in metres", DefaultAltitudeMeters.ToString("F0", CultureInfo.CurrentCulture));
        if (!double.TryParse(altitudeText, NumberStyles.Float, CultureInfo.CurrentCulture, out var altitude))
        {
            if (altitudeText is not null)
            {
                ShowStatus("Enter a valid rally altitude.");
            }

            return;
        }
        var options = dialogService.CreateOptions("Rally altitude reference");

        var reference = await dialogService.ChooseAsync(options, ["Relative to home", "Mean sea level", "Terrain"], cancellationToken);
        if (reference is null)
        {
            return;
        }

        Interaction.PendingRallyAltitude = new MissionAltitude(altitude, reference switch
        {
            "Mean sea level" => MissionAltitudeReference.MeanSeaLevel,
            "Terrain" => MissionAltitudeReference.Terrain,
            var _ => MissionAltitudeReference.Home
        });
        Interaction.Begin(MissionMapInteractionMode.SetRallyPoint, "Click the map to add the local rally point.");
    }

    private IProgress<FenceTransferProgress> FenceProgress()
    {
        return new Progress<FenceTransferProgress>(progress =>
            ShowStatus($"{progress.Stage}: {progress.Completed}/{progress.Total}"));
    }

    private void ShowFenceReport(FenceOperationReport report)
    {
        ShowStatus(report.Validation.IsValid
            ? report.Message
            : string.Join(Environment.NewLine, report.Validation.Issues.Select(issue => issue.Message)));
    }
}
