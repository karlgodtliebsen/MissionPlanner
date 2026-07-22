using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>
/// Defines the contract for a service that handles mission transfers to and from vehicles.
/// </summary>
public interface IMissionTransferService
{
    /// <summary>Uploads a domain mission using the MAVLink mission handshake.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="mission">The mission to upload.</param>
    /// <param name="progress">Optional upload progress.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The transfer result.</returns>
    Task<MissionUploadResult> UploadAsync(VehicleId vehicleId, Mission mission, IProgress<MissionUploadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Uploads already-mapped items for a typed mission plan.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="items">The protocol items to upload.</param>
    /// <param name="missionType">The plan type.</param>
    /// <param name="progress">Optional upload progress.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The transfer result.</returns>
    Task<MissionUploadResult> UploadItemsAsync(
        VehicleId vehicleId,
        IReadOnlyList<MavLinkMissionItem> items,
        MissionPlanType missionType,
        IProgress<MissionUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads a typed mission plan.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="missionType">The plan type.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The downloaded protocol items.</returns>
    Task<MissionDownloadResult> DownloadAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default);

    /// <summary>Downloads a typed mission plan and reports received-item progress.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="missionType">The plan type.</param>
    /// <param name="progress">Optional download progress.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The downloaded protocol items.</returns>
    Task<MissionDownloadResult> DownloadAsync(
        VehicleId vehicleId,
        MissionPlanType missionType,
        IProgress<MissionDownloadProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>Clears a typed mission plan and waits for acknowledgement.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="missionType">The plan type.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The acknowledged clear result.</returns>
    Task<MissionUploadResult> ClearAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default);
}
