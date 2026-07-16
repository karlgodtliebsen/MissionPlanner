using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service for sending MAVLink commands to vehicles.
/// Wraps packet building and transmission via MAVLink client.
/// </summary>
public class MavLinkCommandService(
    IMavLinkClient client,
    ILogger<MavLinkCommandService> logger)
    : IMavLinkCommandService
{
    private byte sequenceNumber = 0;

    /// <inheritdoc/>
    public async Task<bool> RequestDataStreamAsync(
        VehicleId vehicleId,
        MavDataStream streamId,
        int rateHz,
        bool start = true,
        CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot send REQUEST_DATA_STREAM: MAVLink client is not connected");
            return false;
        }

        if (rateHz < 0 || rateHz > 50)
        {
            logger.LogWarning("Invalid data stream rate {RateHz} Hz (must be 0-50)", rateHz);
            return false;
        }

        try
        {
            // Build REQUEST_DATA_STREAM packet
            var packet = MavLinkPacketBuilder.BuildRequestDataStreamPacket(
                vehicleId.SystemId,
                vehicleId.ComponentId,
                (byte)streamId,
                (ushort)rateHz,
                start ? (byte)1 : (byte)0,
                sequenceNumber++);

            // Create endpoint for this vehicle
            // Note: For serial/TCP/UDP, the transport layer handles routing
            // We use a generic endpoint here since the client is already bound to the transport
            var endpoint = new TransportEndPoint("mavlink", "unknown", 0);

            // Send packet
            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogInformation(
                "📤 Sent REQUEST_DATA_STREAM to {VehicleId}: stream={StreamName}({StreamId}) rate={RateHz}Hz {Action}",
                vehicleId,
                streamId,
                (byte)streamId,
                rateHz,
                start ? "START" : "STOP");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send REQUEST_DATA_STREAM to {VehicleId}: stream={StreamId} rate={RateHz}Hz",
                vehicleId, streamId, rateHz);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RequestHomePositionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Cannot send MAV_CMD_GET_HOME_POSITION: MAVLink client is not connected");
            return false;
        }

        try
        {
            const ushort GetHomePositionCommand = 410; // MAV_CMD_GET_HOME_POSITION

            var packet = MavLinkPacketBuilder.BuildCommandLongPacket(
                vehicleId.SystemId,
                vehicleId.ComponentId,
                GetHomePositionCommand,
                sequenceNumber: sequenceNumber++);

            var endpoint = new TransportEndPoint("mavlink", "unknown", 0);
            await client.SendAsync(packet, endpoint, cancellationToken);

            logger.LogInformation("📤 Sent MAV_CMD_GET_HOME_POSITION to {VehicleId}", vehicleId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send MAV_CMD_GET_HOME_POSITION to {VehicleId}", vehicleId);
            return false;
        }
    }
}
