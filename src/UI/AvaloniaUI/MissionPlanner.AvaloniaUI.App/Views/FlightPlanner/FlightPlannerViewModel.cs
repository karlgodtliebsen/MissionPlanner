using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightPlanner;

/// <summary>
/// View model for the Plan screen. Composes the shared <see cref="MissionMapViewModel"/> (map,
/// mission editing, file load/save) and adds vehicle transfer: Read, Write and Write Fast.
/// </summary>
public partial class FlightPlannerViewModel : ViewModelBase
{
    private readonly IDialogService dialogService;

    private readonly IDomainFactory domainFactory;
    private readonly IDomainEventHub domainEventHub;
    private readonly IMissionTransferService transferService;
    private readonly IMissionProtocolMapper protocolMapper;
    private readonly IMissionValidator validator;
    private readonly IVehicleRegistry vehicleRegistry;

    private readonly IList<IDisposable> disposables = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerViewModel"/> class.
    /// </summary>
    public FlightPlannerViewModel(
        FlightPlannerMissionMapViewModel map,
        IDialogService dialogService,
        IDomainFactory domainFactory,
        IDomainEventHub domainEventHub,
        IMissionTransferService transferService,
        IMissionProtocolMapper protocolMapper,
        IMissionValidator validator,
        IVehicleRegistry vehicleRegistry,
        ILogger<FlightPlannerViewModel> logger) : base(logger)
    {
        Map = map;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.domainEventHub = domainEventHub;
        this.transferService = transferService;
        this.protocolMapper = protocolMapper;
        this.validator = validator;
        this.vehicleRegistry = vehicleRegistry;
    }

    /// <summary>The shared mission map editor (same instance as the FlightData map).</summary>
    public MissionMapViewModel Map
    {
        get; private set;
    }

    /// <summary>Progress/result text for the last vehicle transfer.</summary>
    [ObservableProperty]
    public partial string? TransferStatus
    {
        get; set;
    }

    [RelayCommand]
    private async Task ReadFromVehicleAsync()
    {
        if (CurrentVehicleId() is not { } vehicleId)
        {
            TransferStatus = "No vehicle connected.";
            return;
        }

        SetBusy();
        try
        {
            TransferStatus = "Reading mission from vehicle...";
            var result = await transferService.DownloadAsync(vehicleId);
            if (!result.Success)
            {
                TransferStatus = $"Read failed: {result.Error}";
                return;
            }

            var mission = new Mission(MissionId.New(), "Vehicle Mission");
            GeoPosition? home = null;
            var skipped = 0;
            foreach (var protocolItem in result.Items)
            {
                // Sequence 0 is the home position by ArduPilot convention.
                if (protocolItem.Sequence == 0 && protocolItem.Command == (ushort)MissionCommand.Waypoint)
                {
                    var position = new GeoPosition(protocolItem.X / 1e7, protocolItem.Y / 1e7);
                    home = position is { IsValid: true } && (protocolItem.X != 0 || protocolItem.Y != 0) ? position : null;
                    continue;
                }

                try
                {
                    mission.Add(protocolMapper.FromProtocol(protocolItem));
                }
                catch (NotSupportedException)
                {
                    skipped++;
                }
            }

            if (home is not null)
            {
                Map.HomePosition = home;
            }

            Map.ReplaceMission(mission, $"Read {mission.Items.Count} items from vehicle.");
            TransferStatus = skipped == 0
                ? $"Read {mission.Items.Count} items."
                : $"Read {mission.Items.Count} items; skipped {skipped} unsupported.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Mission read failed");
            TransferStatus = $"Read failed: {ex.Message}";
        }
        finally
        {
            ResetBusy();
        }
    }


    [RelayCommand]
    private async Task EditAsync(CancellationToken cancellationToken)
    {
        await ShowHideEditAsync(new EditorDisplayEvent("EditorOpen"), cancellationToken);
    }

    [RelayCommand]
    private async Task CloseAsync(CancellationToken cancellationToken)
    {
        await ShowHideEditAsync(new EditorDisplayEvent("EditorClose"), cancellationToken);
    }

    private async Task ShowHideEditAsync(EditorDisplayEvent e, CancellationToken cancellationToken)
    {
        if (e.Name == "EditorOpen")
        {
            cancellationToken.ThrowIfCancellationRequested();
            //TODO: Must be migrated to use a Drawer
            throw new NotImplementedException();

            //var options = AvaloniaDialogService.CreateDialogOptions("Connect Vehicle", "Ok", null);
            //var viewModel = domainFactory.Create<MissionMapViewModel>();

            //var result = await dialogService.ShowOverlayDialogAsync<MissionItemListViewPage, MissionMapViewModel>(viewModel, options, cancellationToken: cancellationToken);

            //var view = domainFactory.Create<MissionItemListViewPage, MissionMapViewModel>(Map);
            ////await dialogService.DisplayViewAsync("Mission editor", pageView, "Close", 1100, 760);

            //await dialogService.ShowWindowAsync(
            //    view,
            //    new DialogOptions
            //    {
            //        Title = "Connection",
            //        Presentation = DialogPresentation.Window,
            //        Width = 600,
            //        Height = 500,
            //        OkText = "Ok",
            //        CloseText = "Cancel"
            //    });

        }
        else if (e.Name == "EditorClose")
        {
            // await dialogService.CloseAsync(true, cancellationToken);
        }
    }


    [RelayCommand]
    private void ClearMission()
    {
        Map.ClearMissionData();
    }

    [RelayCommand]
    private async Task WriteToVehicleAsync()
    {
        await UploadAsync(true);
    }

    /// <summary>Uploads without validation, mirroring the old "Write Fast" behavior of skipping verification.</summary>
    [RelayCommand]
    private async Task WriteToVehicleFastAsync()
    {
        await UploadAsync(false);
    }

    private async Task UploadAsync(bool validate)
    {
        if (CurrentVehicleId() is not { } vehicleId)
        {
            TransferStatus = "No vehicle connected.";
            return;
        }

        if (Map.Mission.Items.Count == 0)
        {
            TransferStatus = "Mission is empty.";
            return;
        }

        if (validate)
        {
            var validation = validator.Validate(Map.Mission);
            if (!validation.IsValid)
            {
                var first = validation.Issues.First(x => x.Severity == MissionValidationSeverity.Error);
                TransferStatus = $"Validation failed: {first.Message}";
                return;
            }
        }

        SetBusy();
        try
        {
            var progress = new Progress<MissionUploadProgress>(p =>
                TransferStatus = $"Uploading {p.SentItems}/{p.TotalItems}...");
            var result = await transferService.UploadAsync(vehicleId, Map.Mission, validate ? progress : null);
            TransferStatus = result.Success
                ? $"Wrote {Map.Mission.Items.Count} items to vehicle."
                : $"Write failed: {result.Error}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Mission write failed");
            TransferStatus = $"Write failed: {ex.Message}";
        }
        finally
        {
            ResetBusy();
        }
    }

    private VehicleId? CurrentVehicleId()
    {
        return vehicleRegistry.Vehicles.FirstOrDefault()?.Id;
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
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<EditorDisplayEvent>(ShowHideEditAsync));
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
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();
    }
}
