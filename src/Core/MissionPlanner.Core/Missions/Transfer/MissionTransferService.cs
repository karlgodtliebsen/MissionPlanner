using System.Threading.Channels;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>Coordinates the MAVLink mission upload/download handshake.</summary>
public sealed class MissionTransferService(
    IVehicleRegistry vehicleRegistry,
    IMavLinkConnection connection,
    IMavLinkMissionEncoder encoder,
    IMissionProtocolMapper mapper,
    IEventHub eventHub) : IMissionTransferService
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(3);
    private const int MaxAttempts = 4;

    /// <inheritdoc/>
    public async Task<MissionUploadResult> UploadAsync(VehicleId vehicleId, Mission mission, IProgress<MissionUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        var items = mission.Items.Select(x => mapper.ToProtocol(x, mission.Type)).ToArray();
        var requests = Channel.CreateUnbounded<MavLinkMessage>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (m, _) =>
        {
            if (m.SystemId == vehicleId.SystemId && m.ComponentId == vehicleId.ComponentId && m is MissionRequestIntMessage or MissionAckMessage)
            {
                requests.Writer.TryWrite(m);
            }

            return Task.CompletedTask;
        });

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await connection.SendRawAsync(encoder.EncodeMissionCount(vehicleId.SystemId, vehicleId.ComponentId, checked((ushort)items.Length), (MavMissionType)(byte)mission.Type), session.EndPoint, cancellationToken);
            var sent = new HashSet<ushort>();
            while (true)
            {
                MavLinkMessage incoming;
                try
                {
                    incoming = await ReadWithTimeout(requests.Reader, cancellationToken);
                }
                catch (TimeoutException) { break; }

                if (incoming is MissionAckMessage ack)
                {
                    return new MissionUploadResult(ack.Result == 0, ack.Result, ack.Result == 0 ? null : $"Vehicle rejected mission with result {ack.Result}.");
                }

                var request = (MissionRequestIntMessage)incoming;
                if (request.Sequence >= items.Length)
                {
                    return new MissionUploadResult(false, null, $"Vehicle requested invalid mission sequence {request.Sequence}.");
                }

                await connection.SendRawAsync(encoder.EncodeMissionItemInt(vehicleId.SystemId, vehicleId.ComponentId, items[request.Sequence]), session.EndPoint, cancellationToken);
                sent.Add(request.Sequence);
                progress?.Report(new MissionUploadProgress(sent.Count, items.Length, request.Sequence));
            }
        }

        return new MissionUploadResult(false, null, "Mission upload timed out before MISSION_ACK was received.");
    }

    /// <inheritdoc/>
    public async Task<MissionDownloadResult> DownloadAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        var messages = Channel.CreateUnbounded<MavLinkMessage>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (m, _) =>
        {
            if (m.SystemId == vehicleId.SystemId && m.ComponentId == vehicleId.ComponentId && m is MissionCountMessage or MissionItemIntMessage)
            {
                messages.Writer.TryWrite(m);
            }

            return Task.CompletedTask;
        });
        await connection.SendRawAsync(encoder.EncodeMissionRequestList(vehicleId.SystemId, vehicleId.ComponentId,
            (MavMissionType)(byte)missionType), session.EndPoint, cancellationToken);
        MissionCountMessage count;
        try
        {
            count = (MissionCountMessage)await ReadWithTimeout(messages.Reader, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException) { return new MissionDownloadResult(false, [], "Timed out waiting for MISSION_COUNT."); }

        var result = new MavLinkMissionItem[count.Count];
        for (ushort seq = 0; seq < count.Count; seq++)
        {
            MissionItemIntMessage? item = null;
            for (var attempt = 0; attempt < MaxAttempts && item is null; attempt++)
            {
                await connection.SendRawAsync(encoder.EncodeMissionRequestInt(vehicleId.SystemId, vehicleId.ComponentId, seq,
                    (MavMissionType)(byte)missionType), session.EndPoint, cancellationToken);
                try
                {
                    var m = await ReadWithTimeout(messages.Reader, cancellationToken);
                    if (m is MissionItemIntMessage x && x.Sequence == seq)
                    {
                        item = x;
                    }
                }
                catch (TimeoutException) { }
            }

            if (item is null)
            {
                return new MissionDownloadResult(false, result.Take(seq).ToArray(), $"Timed out downloading mission item {seq}.");
            }

            result[seq] = new MavLinkMissionItem(item.Sequence, item.Frame, item.Command, item.Current != 0,
                item.AutoContinue != 0, item.Param1, item.Param2, item.Param3, item.Param4, item.X, item.Y, item.Z, (MavMissionType)item.MissionType);
        }

        await connection.SendRawAsync(encoder.EncodeMissionAck(vehicleId.SystemId, vehicleId.ComponentId, 0,
            (MavMissionType)(byte)missionType), session.EndPoint, cancellationToken);
        return new MissionDownloadResult(true, result, null);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        await connection.SendRawAsync(encoder.EncodeMissionClearAll(vehicleId.SystemId, vehicleId.ComponentId, (MavMissionType)(byte)missionType), session.EndPoint, cancellationToken);
    }

    private static async Task<MavLinkMessage> ReadWithTimeout(ChannelReader<MavLinkMessage> reader, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StepTimeout);
        try
        {
            return await reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }
}
