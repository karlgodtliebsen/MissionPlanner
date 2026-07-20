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
        logger.LogInformation("MAVFTP response dispatcher subscribed to {Topic}.", MavLinkEventTopics.ReceivedMessage);
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
        if (!registrations.TryAdd(id, registration))
        {
            throw new InvalidOperationException("Unable to register MAVFTP response operation.");
        }

        logger.LogDebug(
            "Registered MAVFTP request. Target={Target}, Sequence={Sequence}, Opcode={Opcode}, Session={Session}, MultipleResponses={MultipleResponses}.",
            target, requestSequence, requestedOpcode, session, multipleResponses);

        return registration;
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

        logger.LogDebug(
            "Received MAVFTP response. Source={SystemId}:{ComponentId}, Endpoint={Endpoint}, Sequence={Sequence}, Session={Session}, Opcode={Opcode}, RequestedOpcode={RequestedOpcode}, Registrations={RegistrationCount}.",
            ftp.SystemId, ftp.ComponentId, ftp.EndPoint, packet.Sequence, packet.Session, packet.Opcode, packet.RequestedOpcode, registrations.Count);

        foreach (var pair in registrations)
        {
            var item = pair.Value;

            if (ftp.SystemId != item.Target.SystemId)
            {
                logger.LogTrace(
                    "Rejected MAVFTP response for registration {RegistrationId}: system mismatch. Response={ResponseSystemId}, Target={TargetSystemId}.",
                    pair.Key, ftp.SystemId, item.Target.SystemId);
                continue;
            }

            // Do not require the MAVLink source component to equal the vehicle component.
            // ArduPilot may route MAVFTP through a dedicated component while the FTP
            // packet itself still targets the connected vehicle.
            if (!ftp.EndPoint.Equals(item.Target.EndPoint))
            {
                logger.LogTrace(
                    "Rejected MAVFTP response for registration {RegistrationId}: endpoint mismatch. Response={ResponseEndpoint}, Target={TargetEndpoint}.",
                    pair.Key, ftp.EndPoint, item.Target.EndPoint);
                continue;
            }

            if (packet.RequestedOpcode != item.RequestedOpcode)
            {
                logger.LogTrace(
                    "Rejected MAVFTP response for registration {RegistrationId}: opcode mismatch. Response={ResponseOpcode}, Target={TargetOpcode}.",
                    pair.Key, packet.RequestedOpcode, item.RequestedOpcode);
                continue;
            }

            if (item.Session.HasValue && packet.Session != item.Session.Value)
            {
                logger.LogTrace(
                    "Rejected MAVFTP response for registration {RegistrationId}: session mismatch. Response={ResponseSession}, Target={TargetSession}.",
                    pair.Key, packet.Session, item.Session.Value);
                continue;
            }

            if (!item.MultipleResponses && !MavFtpSequence.MatchesSingleResponse(item.RequestSequence, packet.Sequence))
            {
                logger.LogTrace(
                    "Rejected MAVFTP response for registration {RegistrationId}: sequence mismatch. Response={ResponseSequence}, Request={RequestSequence}.",
                    pair.Key, packet.Sequence, item.RequestSequence);
                continue;
            }

            if (item.MultipleResponses && !MavFtpSequence.IsInResponseWindow(item.RequestSequence, packet.Sequence))
            {
                logger.LogTrace(
                    "Rejected MAVFTP burst response for registration {RegistrationId}: sequence outside response window. Response={ResponseSequence}, Request={RequestSequence}.",
                    pair.Key, packet.Sequence, item.RequestSequence);
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
            else
            {
                logger.LogDebug(
                    "Matched MAVFTP response to registration {RegistrationId}. Target={Target}, Sequence={Sequence}, RequestedOpcode={RequestedOpcode}.",
                    pair.Key, item.Target, packet.Sequence, packet.RequestedOpcode);
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
