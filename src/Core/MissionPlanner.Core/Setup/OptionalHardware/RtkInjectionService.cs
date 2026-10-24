using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Forwards fresh RTCM frames to an explicit set of endpoints captured at start, without queuing stale corrections.</summary>
public sealed class RtkInjectionService(IRtkCorrectionSourceFactory sources, IActiveVehicleContext active,
    IVehicleRegistry registry, IMavLinkConnection connection, IMavLinkWireMessageEncoder encoder) : IRtkInjectionService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private Task? worker;
    private bool disposed;
    private byte sequence;

    /// <inheritdoc />
    public RtkInjectionSnapshot Current { get; private set; } = RtkInjectionSnapshot.Idle;

    /// <inheritdoc />
    public event EventHandler<RtkInjectionSnapshot>? Changed;

    /// <inheritdoc />
    public async Task StartAsync(RtkSourceOptions options, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker is { IsCompleted: false })
            {
                throw new InvalidOperationException("Stop the current correction source first.");
            }
            if (!active.IsOnline || active.VehicleId is not { } id)
            {
                throw new InvalidOperationException("Connect and select a correction target first.");
            }
            var targets = registry.Vehicles.Where(vehicle => vehicle.State.Connection.State == VehicleConnectionState.Online &&
                (options.AllConnectedVehicles || vehicle.Id == id)).ToArray();
            if (targets.Length == 0)
            {
                throw new InvalidOperationException("No connected correction targets were found.");
            }
            lifetime?.Dispose();
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.ConnectionCancellationToken);
            var source = await sources.OpenAsync(options, lifetime.Token).ConfigureAwait(false);
            sequence = 0;
            Publish(RtkInjectionSnapshot.Idle with
            {
                IsRunning = true,
                SourceStatus = $"{options.Kind} correction source connected",
                TargetStatus = $"Targets captured at start: {string.Join(", ", targets.Select(target => target.Id))}"
            });
            worker = RunAsync(source, targets, lifetime.Token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        lifetime?.Cancel();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lifetime?.Cancel();
            if (worker is not null)
            {
                await worker.ConfigureAwait(false);
            }
            lifetime?.Dispose();
            lifetime = null;
            worker = null;
            Publish(Current with { IsRunning = false, SourceStatus = "Correction source stopped" });
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunAsync(IRtkCorrectionSource source, VehicleSession[] targets, CancellationToken token)
    {
        try
        {
            await using (source.ConfigureAwait(false))
            {
                var framer = new Rtcm3Framer();
                var buffer = new byte[4096];
                while (!token.IsCancellationRequested)
                {
                    var count = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (count == 0)
                    {
                        Publish(Current with { SourceStatus = "Correction source closed" });
                        break;
                    }
                    foreach (var frame in framer.Push(buffer.AsSpan(0, count)))
                    {
                        await ForwardAsync(frame, targets, token).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Publish(Current with { SourceStatus = "Correction source cancelled or disconnected" });
        }
        catch (Exception exception)
        {
            // Do not expose source URLs or credentials in status or logs.
            Publish(Current with { SourceStatus = $"Correction source failed ({exception.GetType().Name}); reconnect explicitly" });
        }
        finally
        {
            Publish(Current with { IsRunning = false });
        }
    }

    private async Task ForwardAsync(byte[] frame, VehicleSession[] targets, CancellationToken token)
    {
        var types = Current.MessageTypes.ToDictionary();
        var type = Rtcm3Framer.MessageType(frame);
        types[type] = types.GetValueOrDefault(type) + 1;
        var snapshot = Current with { FramesSeen = Current.FramesSeen + 1, LastCorrection = DateTimeOffset.UtcNow, MessageTypes = types };
        if (frame.Length > 720)
        {
            Publish(snapshot with { TargetStatus = $"RTCM {type} exceeds GPS_RTCM_DATA's 720-byte limit; frame omitted" });
            return;
        }
        var endpoints = targets.Where(target => registry.Vehicles.Contains(target) && target.State.Connection.State == VehicleConnectionState.Online)
            .Select(target => target.EndPoint).Distinct().ToArray();
        var fragments = GpsRtcmFragmenter.Fragment(frame, sequence++);
        var sent = 0;
        foreach (var endpoint in endpoints)
        {
            foreach (var fragment in fragments)
            {
                token.ThrowIfCancellationRequested();
                var payload = new byte[GpsRtcmFragmenter.PayloadSize];
                fragment.Data.CopyTo(payload, 0);
                var message = new GpsRtcmDataMessage(255, 190, new TransportEndPoint("internal", "rtk"),
                    fragment.Flags, checked((byte)fragment.Data.Length), payload, DateTimeOffset.UtcNow);
                await connection.SendRawAsync(encoder.Encode(message), endpoint, token).ConfigureAwait(false);
                sent++;
            }
        }
        Publish(snapshot with { PacketsSent = snapshot.PacketsSent + sent,
            TargetStatus = endpoints.Length == 0 ? "No original targets remain connected; corrections discarded" : $"Sending to {endpoints.Length} captured endpoint(s)" });
    }

    private void Publish(RtkInjectionSnapshot snapshot)
    {
        Current = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        lifetime?.Cancel();
    }
}
