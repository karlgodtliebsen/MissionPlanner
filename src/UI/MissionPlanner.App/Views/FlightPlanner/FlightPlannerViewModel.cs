using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// View model for the Plan screen. Composes the shared <see cref="MissionMapViewModel"/> (map,
/// mission editing, file load/save) and adds vehicle transfer: Read, Write and Write Fast.
/// </summary>
public partial class FlightPlannerViewModel : ObservableObject
{
    private readonly IMissionTransferService transferService;
    private readonly IMissionProtocolMapper protocolMapper;
    private readonly IMissionValidator validator;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly ILogger<FlightPlannerViewModel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerViewModel"/> class.
    /// </summary>
    public FlightPlannerViewModel(
        MissionMapViewModel map,
        IMissionTransferService transferService,
        IMissionProtocolMapper protocolMapper,
        IMissionValidator validator,
        IVehicleRegistry vehicleRegistry,
        ILogger<FlightPlannerViewModel> logger)
    {
        Map = map;
        this.transferService = transferService;
        this.protocolMapper = protocolMapper;
        this.validator = validator;
        this.vehicleRegistry = vehicleRegistry;
        this.logger = logger;
    }

    /// <summary>The shared mission map editor (same instance as the FlightData map).</summary>
    public MissionMapViewModel Map { get; }

    /// <summary>True while a vehicle transfer is running; disables the transfer buttons.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Progress/result text for the last vehicle transfer.</summary>
    [ObservableProperty]
    public partial string? TransferStatus { get; set; }

    [RelayCommand]
    private async Task ReadFromVehicleAsync()
    {
        if (CurrentVehicleId() is not { } vehicleId)
        {
            TransferStatus = "No vehicle connected.";
            return;
        }

        IsBusy = true;
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
            logger.LogError(ex, "Mission read failed");
            TransferStatus = $"Read failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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

        IsBusy = true;
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
            logger.LogError(ex, "Mission write failed");
            TransferStatus = $"Write failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private VehicleId? CurrentVehicleId()
    {
        return vehicleRegistry.Vehicles.FirstOrDefault()?.Id;
    }
}
