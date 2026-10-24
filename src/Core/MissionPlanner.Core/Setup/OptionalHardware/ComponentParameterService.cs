using System.Threading.Channels;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Reads and verifies raw component parameters without converting or logging credential payloads.</summary>
public sealed class ComponentParameterService(IActiveVehicleContext active, IVehicleRegistry registry,
    IEventHub events, IMavLinkConnection connection, IMavLinkParameterEncoder parameters,
    IMavLinkCommandEncoder commands, IVehicleOperationGate operations) : IComponentParameterService
{
    private readonly SemaphoreSlim serial = new(1, 1);
    private Dictionary<string, ParamValueMessage> snapshot = [];
    private VehicleSession? loadedSession;
    private ComponentTarget? loadedTarget;
    private CancellationToken loadedConnection;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, float>> ReadAsync(ComponentTarget target, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            snapshot.Clear();
            loadedTarget = null;
            var session = RequireSession(target);
            var boundary = active.ConnectionCancellationToken;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var inbox = CreateInbox(target, session, out var subscription);
            using (subscription)
            {
                var received = new Dictionary<string, ParamValueMessage>(StringComparer.Ordinal);
                var indices = new HashSet<ushort>();
                ushort expected = 0;
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    await connection.SendRawAsync(parameters.EncodeParamRequestList(target.SystemId, target.ComponentId), session.EndPoint, timeout.Token).ConfigureAwait(false);
                    using var round = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                    round.CancelAfter(TimeSpan.FromSeconds(3));
                    try
                    {
                        while (true)
                        {
                            if (await inbox.Reader.ReadAsync(round.Token).ConfigureAwait(false) is not ParamValueMessage value || value.ParamIndex == ushort.MaxValue)
                            {
                                continue;
                            }
                            if (value.ParamCount is 0 or > 512 || value.ParamIndex >= value.ParamCount)
                            {
                                throw new InvalidDataException("The component reported an invalid parameter inventory.");
                            }
                            if (expected != 0 && expected != value.ParamCount)
                            {
                                throw new InvalidDataException("The component parameter inventory changed while reading. Read again.");
                            }
                            expected = value.ParamCount;
                            received[value.ParamId] = value;
                            indices.Add(value.ParamIndex);
                            if (indices.Count == expected && received.Count == expected)
                            {
                                snapshot = received;
                                loadedSession = session;
                                loadedTarget = target;
                                loadedConnection = boundary;
                                return received.ToDictionary(pair => pair.Key, pair => pair.Value.ParamValue);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!timeout.IsCancellationRequested) { }
                }
                throw new TimeoutException("The component parameter list was incomplete. No settings were staged.");
            }
        }
        finally { serial.Release(); }
    }

    /// <inheritdoc />
    public Task WriteAsync(ComponentTarget target, string name, float value, CancellationToken cancellationToken) =>
        ApplyAsync(target, new Dictionary<string, float> { [name] = value }, false, cancellationToken);

    /// <summary>Verifies every staged raw value, then optionally persists settings on the ESP8266 bridge.</summary>
    public async Task ApplyAsync(ComponentTarget target, IReadOnlyDictionary<string, float> changes, bool persist, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = RequireLoaded(target);
            foreach (var name in changes.Keys)
            {
                if (!snapshot.ContainsKey(name) || name is "SW_VER" or "WIFI_IPADDRESS" or "_HASH_CHECK")
                {
                    throw new InvalidOperationException("Only reported writable bridge parameters may be changed.");
                }
            }
            using var lease = Acquire(session);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, loadedConnection);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var inbox = CreateInbox(target, session, out var subscription);
            using (subscription)
            {
                foreach (var (name, value) in changes)
                {
                    RequireLoaded(target);
                    while (inbox.Reader.TryRead(out _)) { }
                    using var replyTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                    replyTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await connection.SendRawAsync(parameters.EncodeParamSet(target.SystemId, target.ComponentId, name, value, snapshot[name].ParamType), session.EndPoint, replyTimeout.Token).ConfigureAwait(false);
                    while (true)
                    {
                        if (await inbox.Reader.ReadAsync(replyTimeout.Token).ConfigureAwait(false) is not ParamValueMessage reply || reply.ParamId != name)
                        {
                            continue;
                        }
                        if (reply.ParamType != snapshot[name].ParamType || BitConverter.SingleToInt32Bits(reply.ParamValue) != BitConverter.SingleToInt32Bits(value))
                        {
                            throw new IOException("Bridge parameter readback did not match. Earlier settings may have changed; read again.");
                        }
                        snapshot[name] = reply;
                        break;
                    }
                }
                if (persist)
                {
                    RequireLoaded(target);
                    await CommandAsync(target, session, inbox, 245, [1, 0, 0, 0, 0, 0, 0], timeout.Token).ConfigureAwait(false);
                }
            }
        }
        finally { serial.Release(); }
    }

    /// <summary>Requests an acknowledged bridge reboot or restores and persists its defaults.</summary>
    public async Task MaintainAsync(ComponentTarget target, bool restoreDefaults, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = RequireLoaded(target);
            using var lease = Acquire(session);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, loadedConnection);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var inbox = CreateInbox(target, session, out var subscription);
            using (subscription)
            {
                if (restoreDefaults)
                {
                    await CommandAsync(target, session, inbox, 245, [2, 0, 0, 0, 0, 0, 0], timeout.Token).ConfigureAwait(false);
                    RequireLoaded(target);
                    await CommandAsync(target, session, inbox, 245, [1, 0, 0, 0, 0, 0, 0], timeout.Token).ConfigureAwait(false);
                }
                else
                {
                    await CommandAsync(target, session, inbox, 246, [0, 1, 0, 0, 0, 0, 0], timeout.Token).ConfigureAwait(false);
                }
                loadedTarget = null;
                snapshot.Clear();
            }
        }
        finally { serial.Release(); }
    }

    private async Task CommandAsync(ComponentTarget target, VehicleSession session, Channel<MavLinkMessage> inbox,
        ushort command, float[] values, CancellationToken token)
    {
        while (inbox.Reader.TryRead(out _)) { }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await connection.SendRawAsync(commands.EncodeCommandLong(target.SystemId, target.ComponentId, command, values), session.EndPoint, timeout.Token).ConfigureAwait(false);
        while (true)
        {
            if (await inbox.Reader.ReadAsync(timeout.Token).ConfigureAwait(false) is not CommandAckMessage ack || ack.Command != command || ack.Result == 5)
            {
                continue;
            }
            if (ack.Result != 0)
            {
                throw new IOException($"The bridge rejected command {command} (result {ack.Result}).");
            }
            return;
        }
    }

    private Channel<MavLinkMessage> CreateInbox(ComponentTarget target, VehicleSession session, out IDisposable subscription)
    {
        var inbox = Channel.CreateBounded<MavLinkMessage>(new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.DropOldest });
        subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message.SystemId == target.SystemId && message.ComponentId == target.ComponentId && Equals(message.EndPoint, session.EndPoint) &&
                (message is ParamValueMessage || message is CommandAckMessage { TargetSystemId: null or 0 or 255, TargetComponentId: null or 0 or 190 }))
            {
                inbox.Writer.TryWrite(message);
            }
            return Task.CompletedTask;
        });
        return inbox;
    }

    private VehicleSession RequireSession(ComponentTarget target)
    {
        if (target.ComponentId != 240 || !active.IsOnline || active.VehicleId is not { } id || id.SystemId != target.SystemId)
        {
            throw new InvalidOperationException("Connect and select the vehicle carrying the ESP8266 bridge first.");
        }
        return registry.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable.");
    }

    private VehicleSession RequireLoaded(ComponentTarget target)
    {
        var session = RequireSession(target);
        if (loadedTarget != target || !ReferenceEquals(loadedSession, session) || loadedConnection != active.ConnectionCancellationToken || loadedConnection.IsCancellationRequested)
        {
            throw new InvalidOperationException("Read the bridge again after a target or connection change.");
        }
        if (active.State?.IsArmed != false)
        {
            throw new InvalidOperationException("Disarm before changing the telemetry bridge.");
        }
        return session;
    }

    private IDisposable Acquire(VehicleSession session)
    {
        if (!operations.TryAcquire(session.Id, "ESP8266 bridge configuration", out var lease))
        {
            throw new InvalidOperationException("Another vehicle operation is in progress.");
        }
        return lease!;
    }
}
