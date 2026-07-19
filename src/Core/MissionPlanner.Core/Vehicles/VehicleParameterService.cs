using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Service for managing vehicle parameters via MAVLink.
/// Handles parameter requests and updates through the MAVLink protocol.
/// </summary>
public sealed class VehicleParameterService(IMavLinkClient client, IMavLinkParameterEncoder encoder, IVehicleRegistry vehicleRegistry, ILogger<VehicleParameterService> logger)
    : IVehicleParameterService
{
    /// <inheritdoc/>
    public async Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot request parameter list: MAVLink client is not connected");
            return false;
        }

        try
        {
            var packet = encoder.EncodeParamRequestList(vehicleId.SystemId, vehicleId.ComponentId);

            var endpoint = vehicleRegistry.GetRequired(vehicleId).EndPoint;

            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogInformation("📤 Sent PARAM_REQUEST_LIST to {VehicleId}", vehicleId);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send PARAM_REQUEST_LIST to {VehicleId}", vehicleId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot request parameter: MAVLink client is not connected");
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            logger.LogWarning("Parameter name cannot be empty");
            return false;
        }

        if (parameterName.Length > 16)
        {
            logger.LogWarning("Parameter name {ParameterName} exceeds 16 character limit", parameterName);
            return false;
        }

        try
        {
            var packet = encoder.EncodeParamRequestRead(vehicleId.SystemId, vehicleId.ComponentId, parameterName);

            var endpoint = vehicleRegistry.GetRequired(vehicleId).EndPoint;

            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogInformation("📤 Sent PARAM_REQUEST_READ to {VehicleId}: param={ParameterName}", vehicleId, parameterName);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send PARAM_REQUEST_READ to {VehicleId}: param={ParameterName}", vehicleId, parameterName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot request parameter: MAVLink client is not connected");
            return false;
        }

        try
        {
            // Use empty string for paramId and provide the index
            var packet = encoder.EncodeParamRequestRead(vehicleId.SystemId, vehicleId.ComponentId, string.Empty, (short)parameterIndex);

            var endpoint = vehicleRegistry.GetRequired(vehicleId).EndPoint;

            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogDebug("📤 Sent PARAM_REQUEST_READ to {VehicleId}: index={ParameterIndex}", vehicleId, parameterIndex);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send PARAM_REQUEST_READ to {VehicleId}: index={ParameterIndex}", vehicleId, parameterIndex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SetParameterAsync(VehicleId vehicleId, string parameterName, float value, MavParamType paramType, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot set parameter: MAVLink client is not connected");
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            logger.LogWarning("Parameter name cannot be empty");
            return false;
        }

        if (parameterName.Length > 16)
        {
            logger.LogWarning("Parameter name {ParameterName} exceeds 16 character limit", parameterName);
            return false;
        }

        try
        {
            var packet = encoder.EncodeParamSet(vehicleId.SystemId, vehicleId.ComponentId, parameterName, value, paramType);

            var endpoint = vehicleRegistry.GetRequired(vehicleId).EndPoint;

            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogInformation("📤 Sent PARAM_SET to {VehicleId}: param={ParameterName} value={Value} type={Type}", vehicleId, parameterName, value, paramType);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send PARAM_SET to {VehicleId}: param={ParameterName} value={Value}", vehicleId, parameterName, value);
            return false;
        }
    }
}
