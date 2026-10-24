using System.Threading.Channels;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Captures one disarmed ArduPilot connection and renews its CAN_FORWARD lease until explicitly disconnected.</summary>
public sealed class MavLinkCanFrameLink(IActiveVehicleContext active, IVehicleRegistry registry, IEventHub events,
    IMavLinkConnection connection, IMavLinkWireMessageEncoder wire, IMavLinkCommandEncoder commands,
    IVehicleOperationGate operations, DroneCanConnectionOptions options) : IDroneCanFrameLink
{
    private readonly Channel<DroneCanFrame> frames = Channel.CreateBounded<DroneCanFrame>(2048);
    private readonly CancellationTokenSource lifetime = new();
    private VehicleSession? target;
    private CancellationToken boundary;
    private IDisposable? lease;
    private IDisposable? subscription;
    private Task? renewal;
    private Exception? failure;
    private bool forwardingRequested;
    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken token)
    {
        options.Validate(DroneCanTransportKind.MavLinkTunnel);
        if (!active.IsOnline || active.VehicleId is not { } id) throw new InvalidOperationException("Connect an ArduPilot vehicle first.");
        target = registry.GetRequired(id); boundary = active.ConnectionCancellationToken; Validate();
        if (!operations.TryAcquire(id, "DroneCAN maintenance tunnel", out lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
        var captured = target!;
        var ack = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message.SystemId != id.SystemId || message.ComponentId != id.ComponentId || !Equals(message.EndPoint, captured.EndPoint)) return Task.CompletedTask;
            if (message is CommandAckMessage { Command: 32000, TargetSystemId: null or 0 or 255, TargetComponentId: null or 0 or 190 } response && response.Result != 5) ack.TrySetResult(response.Result);
            if (message is CanFrameMessage { TargetSystem: 0 or 255, TargetComponent: 0 or 190, Len: <= 8 } frame && frame.Bus == options.Bus - 1 &&
                (frame.Id & 0xE0000000) == 0x80000000 && frame.Data.Length >= frame.Len &&
                !frames.Writer.TryWrite(new(frame.Id & 0x1FFFFFFF, frame.Data[..frame.Len])))
            { failure = new IOException("CAN tunnel receive overflow; reconnect on a faster MAVLink link."); frames.Writer.TryComplete(failure); lifetime.Cancel(); }
            return Task.CompletedTask;
        });
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, boundary); timeout.CancelAfter(TimeSpan.FromSeconds(3));
            forwardingRequested = true;
            await ForwardAsync(options.Bus, timeout.Token).ConfigureAwait(false);
            var result = await ack.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            if (result != 0) throw new IOException($"The autopilot rejected CAN forwarding (result {result}).");
            await connection.SendRawAsync(wire.Encode(new CanFilterModifyMessage(255, 190, captured.EndPoint, id.SystemId, id.ComponentId,
                options.Bus, 0, 0, [], DateTimeOffset.UtcNow)), captured.EndPoint, timeout.Token).ConfigureAwait(false);
            renewal = RenewAsync(lifetime.Token);
        }
        catch { await DisposeAsync().ConfigureAwait(false); throw; }
    }
    /// <inheritdoc />
    public void Validate()
    {
        if (failure is not null) throw new IOException("The CAN tunnel failed.", failure);
        var now = DateTimeOffset.UtcNow;
        if (target is null || lifetime.IsCancellationRequested || boundary.IsCancellationRequested || boundary != active.ConnectionCancellationToken || !active.IsOnline || active.VehicleId != target.Id ||
            !ReferenceEquals(registry.GetRequired(target.Id), target) || active.State is not { IsArmed: false, Autopilot: 3 } state || state.LastHeartbeatAt > now || now - state.LastHeartbeatAt > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("DroneCAN requires the same connected, freshly reporting, disarmed ArduPilot vehicle.");
    }
    /// <inheritdoc />
    public async Task<DroneCanFrame> ReadAsync(CancellationToken token) => await frames.Reader.ReadAsync(token).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task SendAsync(DroneCanFrame frame, CancellationToken token)
    {
        Validate();
        if (frame.Data.Length > 8 || frame.Identifier > 0x1FFFFFFF) throw new ArgumentException("Classic extended CAN frames are required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, boundary); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await connection.SendRawAsync(wire.Encode(new CanFrameMessage(255, 190, target!.EndPoint, target.Id.SystemId, target.Id.ComponentId,
            (byte)(options.Bus - 1), (byte)frame.Data.Length, frame.Identifier | 0x80000000, frame.Data, DateTimeOffset.UtcNow)), target.EndPoint, timeout.Token).ConfigureAwait(false);
    }
    private Task ForwardAsync(byte bus, CancellationToken token) => connection.SendRawAsync(commands.EncodeCommandLong(target!.Id.SystemId, target.Id.ComponentId, 32000, [bus, 0, 0, 0, 0, 0, 0]), target.EndPoint, token).AsTask();
    private async Task RenewAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(1000, token).ConfigureAwait(false); Validate();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, boundary); timeout.CancelAfter(TimeSpan.FromSeconds(2));
                await ForwardAsync(options.Bus, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; frames.Writer.TryComplete(ex); }
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (renewal is not null) await renewal.ConfigureAwait(false);
        subscription?.Dispose(); subscription = null;
        try
        {
            if (forwardingRequested && target is not null && !boundary.IsCancellationRequested && boundary == active.ConnectionCancellationToken)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await ForwardAsync(0, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException) { }
        finally { lease?.Dispose(); lease = null; target = null; forwardingRequested = false; frames.Writer.TryComplete(); }
    }
}
