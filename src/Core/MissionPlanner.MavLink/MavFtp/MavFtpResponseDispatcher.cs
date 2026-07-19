using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// 
/// </summary>
public sealed class MavFtpResponseDispatcher : IMavFtpResponseDispatcher
{
    private readonly ConcurrentDictionary<long, MavFtpResponseRegistration> registrations = new();
    private readonly IMavFtpPacketCodec codec;
    private readonly ILogger<MavFtpResponseDispatcher> logger;
    private readonly IDisposable subscription;
    private readonly int responseQueueCapacity;
    private long nextId;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventHub"></param>
    /// <param name="codec"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public MavFtpResponseDispatcher(IEventHub eventHub, IMavFtpPacketCodec codec, IOptions<MavFtpOptions> options, ILogger<MavFtpResponseDispatcher> logger)
    {
        this.codec = codec;
        this.logger = logger;
        responseQueueCapacity = options.Value.ResponseQueueCapacity;
        subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, DispatchAsync);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="requestSequence"></param>
    /// <param name="requestedOpcode"></param>
    /// <param name="session"></param>
    /// <param name="multipleResponses"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public MavFtpResponseRegistration Register(MavFtpTarget target, ushort requestSequence, MavFtpOpcode requestedOpcode, byte? session = null, bool multipleResponses = false)
    {
        var id = Interlocked.Increment(ref nextId);
        var registration = new MavFtpResponseRegistration(target, requestSequence, requestedOpcode, session, multipleResponses, responseQueueCapacity, () => registrations.TryRemove(id, out var _));
        return !registrations.TryAdd(id, registration)
            ? throw new InvalidOperationException("Unable to register MAVFTP response operation.")
            : registration;
    }

    private Task DispatchAsync(MavLinkMessage message, CancellationToken _)
    {
        if (message is not FileTransferProtocolMessage ftp)
        {
            return Task.CompletedTask;
        }

        MavFtpPacket packet;
        try
        {
            packet = codec.Decode(ftp.Payload);
        }
        catch (MavFtpProtocolException ex)
        {
            logger.LogWarning(ex, "Malformed MAVFTP response from {SystemId}/{ComponentId}.", ftp.SystemId, ftp.ComponentId);
            return Task.CompletedTask;
        }

        foreach (var pair in registrations)
        {
            var item = pair.Value;
            if (ftp.SystemId != item.Target.SystemId || ftp.ComponentId != item.Target.ComponentId || !ftp.EndPoint.Equals(item.Target.EndPoint) ||
                packet.RequestedOpcode != item.RequestedOpcode || (item.Session.HasValue && packet.Session != item.Session.Value))
            {
                continue;
            }

            if (!item.MultipleResponses && packet.Sequence != MavFtpSequence.FirstResponse(item.RequestSequence))
            {
                continue;
            }

            if (item.MultipleResponses && !MavFtpSequence.IsInResponseWindow(item.RequestSequence, packet.Sequence))
            {
                continue;
            }

            if (!item.TryWrite(packet))
            {
                var exception = new MavFtpProtocolException(
                    $"MAVFTP response queue overflow for {item.Target}, request sequence {item.RequestSequence}.");
                logger.LogError(exception,
                    "MAVFTP response queue overflow for {Target}, request sequence {RequestSequence}.",
                    item.Target, item.RequestSequence);
                item.Fail(exception);
            }

            break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        subscription.Dispose();
        foreach (var registration in registrations.Values)
        {
            registration.Dispose();
        }

        registrations.Clear();
    }
}
