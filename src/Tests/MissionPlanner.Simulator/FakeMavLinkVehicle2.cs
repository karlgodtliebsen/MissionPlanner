using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MissionPlanner.Core.Models;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Simulator;

/// <summary>
/// A simple MAVLink vehicle simulator that sends fake MAVLink messages over UDP.
/// </summary>
public sealed class FakeMavLinkVehicle2 : IAsyncDisposable
{
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkCrcExtraProvider crcExtraProvider;
    private readonly UdpClient udpClient;
    private readonly IPEndPoint targetEndpoint;
    private readonly TimeSpan heartbeatInterval;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? workerTask = null;
    private Task? receiveTask = null;
    private byte sequence;
    private VehicleState state;
    private readonly bool acknowledgeCommands;
    private readonly byte commandAckResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeMavLinkVehicle2"/> class.
    /// </summary>
    /// <param name="frameParser">The MAVLink frame parser.</param>
    /// <param name="crcExtraProvider"></param>
    /// <param name="targetIp">The IP address of the target endpoint.</param>
    /// <param name="targetPort">The port of the target endpoint.</param>
    /// <param name="localPort">The local port to bind the UDP client to.</param>
    /// <param name="heartbeatInterval">The interval at which heartbeat messages are sent.</param>
    /// <param name="acknowledgeCommands">Whether to acknowledge commands.</param>
    /// <param name="commandAckResult"></param>
    public FakeMavLinkVehicle2(IMavLinkFrameParser frameParser, IMavLinkCrcExtraProvider crcExtraProvider, string targetIp, int targetPort, int localPort,
        TimeSpan? heartbeatInterval = null, bool acknowledgeCommands = true, byte commandAckResult = 0)
    {
        this.frameParser = frameParser;
        this.crcExtraProvider = crcExtraProvider;
        this.acknowledgeCommands = acknowledgeCommands;
        this.commandAckResult = commandAckResult;
        targetEndpoint = new IPEndPoint(IPAddress.Parse(targetIp), targetPort);

        udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, localPort));

        this.heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(1);

        state = new VehicleState(
            new VehicleId(1, 1),
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            DateTimeOffset.UtcNow,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    /// <summary>
    /// Starts the fake MAVLink vehicle simulator.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        workerTask = Task.Run(() => SendLoopAsync(cancellationTokenSource.Token), CancellationToken.None);
        receiveTask = Task.Run(() => ReceiveCommandLoopAsync(cancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task ReceiveCommandLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);

            var frames = frameParser.Parse(result.Buffer, new TransportEndPoint("udp", result.RemoteEndPoint), DateTimeOffset.UtcNow);

            foreach (var frame in frames)
            {
                if (frame.MessageId == MessageIds.CommandLong)
                {
                    await HandleCommandLongAsync(frame, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task HandleArmDisarmAsync(MavLinkFrame frame, ushort command, CancellationToken cancellationToken)
    {
        var isArmed = Math.Abs(ReadFloat(frame.Payload.Span[0..4]) - 1.0f) < 0.001;

        const byte mavModeFlagSafetyArmed = 0b1000_0000;

        var newBaseMode = isArmed
            ? (byte)(state.BaseMode | mavModeFlagSafetyArmed)
            : (byte)(state.BaseMode & ~mavModeFlagSafetyArmed);

        state = VehicleStateFactory.CreateFromLegacyState(state, newBaseMode, isArmed);
        if (!acknowledgeCommands)
        {
            return;
        }

        var ack = CreateCommandAckV2(command, commandAckResult);

        await udpClient.SendAsync(ack, targetEndpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleSetModeAsync(MavLinkFrame frame, ushort command, CancellationToken cancellationToken)
    {
        var customMode = (byte)ReadFloat(frame.Payload.Span[4..8]);
        var mode = ArduCopterModeMapper.ToVehicleMode(customMode);

        state = VehicleStateFactory.CreateFromLegacyState(state, customMode, mode);

        if (!acknowledgeCommands)
        {
            return;
        }

        var ack = CreateCommandAckV2(command, commandAckResult);

        await udpClient.SendAsync(ack, targetEndpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleCommandLongAsync(MavLinkFrame frame, CancellationToken cancellationToken)
    {
        var command = BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[28..30]);
        if (command == MavLinkCommandIds.ComponentArmDisarm)
        {
            await HandleArmDisarmAsync(frame, command, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command == MavLinkCommandIds.DoSetMode)
        {
            await HandleSetModeAsync(frame, command, cancellationToken).ConfigureAwait(false);

            return;
        }
    }

    private static float ReadFloat(ReadOnlySpan<byte> value)
    {
        var raw = BinaryPrimitives.ReadInt32LittleEndian(value);
        return BitConverter.Int32BitsToSingle(raw);
    }

    private byte[] CreateCommandAckV2(ushort command, byte result, byte systemId = 1, byte componentId = 1)
    {
        Span<byte> payload = stackalloc byte[10];

        // MAVLink COMMAND_ACK payload
        //
        // uint16 command
        // uint8 result
        // uint8 progress
        // int32 result_param2
        // uint8 target_system
        // uint8 target_component

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload[0..2],
            command);

        payload[2] = result; // MAV_RESULT_ACCEPTED = 0
        payload[3] = 0; // progress

        BinaryPrimitives.WriteInt32LittleEndian(
            payload[4..8],
            0);

        payload[8] = 255; // target system (GCS)
        payload[9] = 190; // target component (GCS)

        return BuildV2Packet(
            systemId,
            componentId,
            MessageIds.CommandAck,
            payload);
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var heartbeat = MavLinkKnownFrames.CreateHeartbeatV2(
                crcExtraProvider,
                sequence++,
                state.VehicleId.SystemId,
                state.VehicleId.ComponentId,
                state.CustomMode,
                state.VehicleType,
                state.Autopilot,
                state.BaseMode,
                state.SystemStatus,
                state.MavLinkVersion);

            await udpClient.SendAsync(heartbeat, targetEndpoint, cancellationToken).ConfigureAwait(false);
            await Task.Delay(heartbeatInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private byte[] BuildV2Packet(byte systemId, byte componentId, uint messageId, ReadOnlySpan<byte> payload)
    {
        var packetLength = 10 + payload.Length + 2;

        var packet = new byte[packetLength];

        packet[0] = 0xFD;
        packet[1] = (byte)payload.Length;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = sequence++;

        packet[5] = systemId;
        packet[6] = componentId;

        packet[7] = (byte)(messageId & 0xFF);
        packet[8] = (byte)((messageId >> 8) & 0xFF);
        packet[9] = (byte)((messageId >> 16) & 0xFF);

        payload.CopyTo(packet.AsSpan(10));

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
        {
            throw new InvalidOperationException($"No CRC extra registered for message {messageId}");
        }

        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), crcExtra);

        var crcOffset = 10 + payload.Length;

        packet[crcOffset] = (byte)(crc & 0xFF);
        packet[crcOffset + 1] = (byte)(crc >> 8);

        return packet;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (cancellationTokenSource is not null)
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

            if (workerTask is not null)
            {
                try
                {
                    await workerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }

            if (receiveTask is not null)
            {
                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }

            cancellationTokenSource.Dispose();
        }

        udpClient.Dispose();
    }
}
