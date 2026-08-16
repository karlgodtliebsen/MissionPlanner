using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Latest connection-scoped parameter loading status for a vehicle.</summary>
public sealed record ParameterLoadStatus(
    VehicleId VehicleId,
    ParameterLoadState State,
    int ReceivedCount,
    int TotalCount,
    int PercentComplete,
    string Message,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Gets whether parameter loading is still active.</summary>
    public bool IsInProgress => State is ParameterLoadState.Starting or ParameterLoadState.Downloading;
}
