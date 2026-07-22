using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Configuration.VendorDevices;

/// <summary>Correlates generated MAVLink device-operation requests and replies.</summary>
public sealed class DeviceOperationClient : IDeviceOperationClient
{
    private const byte SourceSystemId = 255;
    private const byte SourceComponentId = 190;
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IMavLinkConnection connection;
    private readonly IMavLinkWireMessageEncoder encoder;
    private readonly IEventHub eventHub;
    private readonly ILogger<DeviceOperationClient> logger;
    private int requestId;

    /// <summary>Initializes the device-operation client.</summary>
    /// <param name="vehicleRegistry">The connected-vehicle registry.</param>
    /// <param name="connection">The MAVLink connection.</param>
    /// <param name="encoder">The generated-message encoder.</param>
    /// <param name="eventHub">The inbound message hub.</param>
    /// <param name="logger">The logger.</param>
    public DeviceOperationClient(
        IVehicleRegistry vehicleRegistry,
        IMavLinkConnection connection,
        IMavLinkWireMessageEncoder encoder,
        IEventHub eventHub,
        ILogger<DeviceOperationClient> logger)
    {
        this.vehicleRegistry = vehicleRegistry;
        this.connection = connection;
        this.encoder = encoder;
        this.eventHub = eventHub;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<DeviceOperationResult> ReadAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        byte count,
        CancellationToken cancellationToken = default)
    {
        if (count is 0 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "DEVICE_OP_READ supports 1 to 128 bytes.");
        }

        return ExecuteReadAsync(vehicleId, busType, bus, address, registerStart, count, cancellationToken);
    }

    /// <inheritdoc />
    public Task<DeviceOperationResult> WriteAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.Length is 0 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "DEVICE_OP_WRITE supports 1 to 128 bytes.");
        }

        return ExecuteWriteAsync(vehicleId, busType, bus, address, registerStart, data, cancellationToken);
    }

    private async Task<DeviceOperationResult> ExecuteReadAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        byte count,
        CancellationToken cancellationToken)
    {
        var session = vehicleRegistry.GetRequired(vehicleId)
            ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        var id = NextRequestId();
        var reply = new TaskCompletionSource<DeviceOpReadReplyMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is DeviceOpReadReplyMessage candidate && candidate.RequestId == id &&
                candidate.SystemId == vehicleId.SystemId && candidate.ComponentId == vehicleId.ComponentId &&
                candidate.EndPoint.Equals(session.EndPoint))
            {
                reply.TrySetResult(candidate);
            }

            return Task.CompletedTask;
        });
        var request = new DeviceOpReadMessage(
            SourceSystemId,
            SourceComponentId,
            session.EndPoint,
            vehicleId.SystemId,
            vehicleId.ComponentId,
            id,
            (byte)busType,
            bus,
            address,
            string.Empty,
            registerStart,
            count,
            0,
            DateTimeOffset.UtcNow);
        await connection.SendRawAsync(encoder.Encode(request), session.EndPoint, cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await reply.Task.WaitAsync(ReplyTimeout, cancellationToken).ConfigureAwait(false);
            var returnedCount = Math.Min(response.Count, checked((byte)response.Data.Length));
            return new DeviceOperationResult(response.Result, response.Regstart, response.Data[..returnedCount]);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "DEVICE_OP_READ timed out for {VehicleId}; Bus={Bus}; Address={Address}; Register={Register}.",
                vehicleId,
                bus,
                address,
                registerStart);
            throw;
        }
    }

    private async Task<DeviceOperationResult> ExecuteWriteAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        var session = vehicleRegistry.GetRequired(vehicleId)
            ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        var id = NextRequestId();
        var reply = new TaskCompletionSource<DeviceOpWriteReplyMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is DeviceOpWriteReplyMessage candidate && candidate.RequestId == id &&
                candidate.SystemId == vehicleId.SystemId && candidate.ComponentId == vehicleId.ComponentId &&
                candidate.EndPoint.Equals(session.EndPoint))
            {
                reply.TrySetResult(candidate);
            }

            return Task.CompletedTask;
        });
        var request = new DeviceOpWriteMessage(
            SourceSystemId,
            SourceComponentId,
            session.EndPoint,
            vehicleId.SystemId,
            vehicleId.ComponentId,
            id,
            (byte)busType,
            bus,
            address,
            string.Empty,
            registerStart,
            checked((byte)data.Length),
            data.ToArray(),
            0,
            DateTimeOffset.UtcNow);
        await connection.SendRawAsync(encoder.Encode(request), session.EndPoint, cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await reply.Task.WaitAsync(ReplyTimeout, cancellationToken).ConfigureAwait(false);
            return new DeviceOperationResult(response.Result, registerStart, []);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "DEVICE_OP_WRITE timed out for {VehicleId}; Bus={Bus}; Address={Address}; Register={Register}.",
                vehicleId,
                bus,
                address,
                registerStart);
            throw;
        }
    }

    private uint NextRequestId() => unchecked((uint)Interlocked.Increment(ref requestId));
}
