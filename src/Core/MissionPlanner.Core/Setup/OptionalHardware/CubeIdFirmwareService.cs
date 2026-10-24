using System.Threading.Channels;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Runs the CubeID offset-request firmware protocol against a freshly detected component.</summary>
public sealed class CubeIdFirmwareService(IActiveVehicleContext active, IVehicleRegistry registry,
    IEventHub events, IMavLinkConnection connection, IMavLinkWireMessageEncoder encoder, IVehicleOperationGate operations)
{
    /// <summary>The maximum locally accepted firmware size, also bounded by the 16-bit chunk sequence.</summary>
    public const int MaximumImageBytes = 4 * 1024 * 1024;
    private VehicleSession? detectedSession;
    private CancellationToken detectedConnection;
    private DateTimeOffset detectedAt;

    /// <summary>Waits for a new heartbeat from ODID transmitter component 236 on the active link.</summary>
    public async Task<ComponentTarget> DetectAsync(CancellationToken cancellationToken)
    {
        detectedSession = null;
        var session = RequireSession();
        var boundary = active.ConnectionCancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var completion = new TaskCompletionSource<HeartbeatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is HeartbeatMessage heartbeat && message.SystemId == session.Id.SystemId && message.ComponentId == 236 && Equals(message.EndPoint, session.EndPoint))
                completion.TrySetResult(heartbeat);
            return Task.CompletedTask;
        });
        await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        timeout.Token.ThrowIfCancellationRequested();
        detectedSession = session;
        detectedConnection = boundary;
        detectedAt = DateTimeOffset.UtcNow;
        return new(session.Id.SystemId, 236);
    }

    /// <summary>Transfers an explicitly confirmed local image; final acknowledgement does not verify a subsequent boot.</summary>
    public async Task TransferAsync(ComponentTarget target, CubeFirmwareImage image, bool confirmed,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var session = RequireSession();
        if (!confirmed || target != new ComponentTarget(session.Id.SystemId, 236) || !ReferenceEquals(session, detectedSession) ||
            detectedConnection != active.ConnectionCancellationToken || detectedConnection.IsCancellationRequested || DateTimeOffset.UtcNow - detectedAt > TimeSpan.FromSeconds(30))
            throw new InvalidOperationException("Detect the CubeID on this connection again and confirm its image before updating.");
        if (image.Data.Length is 0 or > MaximumImageBytes || !image.Name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a non-empty CubeID .bin image of at most 4 MiB.");
        if (!operations.TryAcquire(session.Id, "CubeID firmware transfer", out var lease))
            throw new InvalidOperationException("Another vehicle operation is in progress.");
        using (lease)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, detectedConnection))
        {
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var data = image.Data.ToArray();
            var inbox = Channel.CreateBounded<uint>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest });
            using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
            {
                if (message is CubepilotFirmwareUpdateRespMessage response && message.SystemId == target.SystemId && message.ComponentId == target.ComponentId &&
                    Equals(message.EndPoint, session.EndPoint) && response.TargetSystem is 0 or 255 && response.TargetComponent is 0 or 190)
                    inbox.Writer.TryWrite(response.Offset);
                return Task.CompletedTask;
            });
            GeneratedMavLinkMessage packet = new CubepilotFirmwareUpdateStartMessage(255, 190, session.EndPoint,
                target.SystemId, target.ComponentId, (uint)data.Length, CubeFirmwareCodec.Crc32(data), DateTimeOffset.UtcNow);
            uint sentEnd = 0;
            uint lastOffset = 0;
            var retries = 0;
            while (true)
            {
                RequireSession();
                timeout.Token.ThrowIfCancellationRequested();
                await connection.SendRawAsync(encoder.Encode(packet), session.EndPoint, timeout.Token).ConfigureAwait(false);
                uint offset;
                using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                responseTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                try { offset = await inbox.Reader.ReadAsync(responseTimeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!timeout.IsCancellationRequested)
                {
                    if (++retries > 5) throw new TimeoutException("CubeID stopped requesting firmware data. Transfer is incomplete; reconnect using its recovery procedure.");
                    continue;
                }
                if (offset > data.Length || offset > sentEnd || (offset != data.Length && offset % CubeFirmwareCodec.ChunkSize != 0))
                    throw new InvalidDataException("CubeID requested an invalid firmware offset; transfer stopped.");
                if (offset == data.Length)
                {
                    progress?.Report(100);
                    return;
                }
                if (offset <= lastOffset && sentEnd > 0)
                {
                    if (++retries > 5) throw new IOException("CubeID repeatedly requested the same firmware data without progress.");
                }
                else { retries = 0; }
                lastOffset = offset;
                var count = Math.Min(CubeFirmwareCodec.ChunkSize, data.Length - (int)offset);
                var payload = new byte[253];
                data.AsSpan((int)offset, count).CopyTo(payload);
                packet = new EncapsulatedDataMessage(255, 190, session.EndPoint, checked((ushort)(offset / CubeFirmwareCodec.ChunkSize)), payload, DateTimeOffset.UtcNow);
                sentEnd = offset + (uint)count;
                progress?.Report(100d * offset / data.Length);
            }
        }
    }

    private VehicleSession RequireSession()
    {
        if (!active.IsOnline || active.VehicleId is not { } id || active.State?.IsArmed != false)
            throw new InvalidOperationException("Connect and select a disarmed vehicle before using the CubeID updater.");
        return registry.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable.");
    }
}
