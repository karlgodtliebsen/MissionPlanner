using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Parameters.Metadata;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service for accessing parameter metadata for vehicles.
/// Maps MAVLink vehicle types to ArduPilot firmware types and retrieves metadata.
/// </summary>
public sealed class VehicleParameterMetadataService(
    IVehicleRegistry vehicleRegistry,
    IParameterMetadataRepository metadataRepository,
    ILogger<VehicleParameterMetadataService> logger)
    : IVehicleParameterMetadataService
{
    /// <inheritdoc/>
    public async Task<ParameterMetadata?> GetMetadataAsync(
        VehicleId vehicleId,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        var vehicleType = GetVehicleType(vehicleId);
        if (!vehicleType.HasValue)
        {
            logger.LogWarning("Vehicle {VehicleId} not found in registry", vehicleId);
            return null;
        }

        return await metadataRepository.GetMetadataAsync(
            vehicleType.Value,
            parameterName,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ParameterMetadata?> GetMetadataAsync(
        VehicleType vehicleType,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        return metadataRepository.GetMetadataAsync(vehicleType, parameterName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(
        VehicleId vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicleType = GetVehicleType(vehicleId);
        if (!vehicleType.HasValue)
        {
            logger.LogWarning("Vehicle {VehicleId} not found in registry", vehicleId);
            return new Dictionary<string, ParameterMetadata>();
        }

        return await metadataRepository.GetAllMetadataAsync(vehicleType.Value, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(
        VehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        return metadataRepository.GetAllMetadataAsync(vehicleType, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RefreshMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        return metadataRepository.RefreshAsync(vehicleType, cancellationToken);
    }

    private VehicleType? GetVehicleType(VehicleId vehicleId)
    {
        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        if (vehicle == null)
        {
            return null;
        }

        // Map MAVLink vehicle type to ArduPilot firmware type
        // MAV_TYPE enumeration values
        return vehicle.State.VehicleType switch
        {
            2 => VehicleType.ArduCopter,     // MAV_TYPE_QUADROTOR and other multirotor types
            3 => VehicleType.ArduCopter,     // MAV_TYPE_HELICOPTER
            13 => VehicleType.ArduCopter,    // MAV_TYPE_HEXAROTOR
            14 => VehicleType.ArduCopter,    // MAV_TYPE_OCTOROTOR
            15 => VehicleType.ArduCopter,    // MAV_TYPE_TRICOPTER
            1 => VehicleType.ArduPlane,      // MAV_TYPE_FIXED_WING
            10 => VehicleType.Rover,         // MAV_TYPE_GROUND_ROVER
            12 => VehicleType.ArduSub,       // MAV_TYPE_SUBMARINE
            4 => VehicleType.AntennaTracker, // MAV_TYPE_ANTENNA_TRACKER
            _ => VehicleType.ArduCopter      // Default fallback
        };
    }
}
