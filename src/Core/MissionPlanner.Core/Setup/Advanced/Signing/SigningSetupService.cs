using System.Security.Cryptography;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Signing;

namespace MissionPlanner.Core.Setup.Advanced.Signing;

/// <summary>Controls the bounded wait for vehicle signing evidence.</summary>
public sealed class SigningSetupOptions
{
    /// <summary>Gets or sets a positive confirmation timeout of at most one minute.</summary>
    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>Configures signing using SETUP_SIGNING and a verified new signed exchange, not a write-only success.</summary>
public sealed class SigningSetupService(IVehicleConnectionSession connection, IActiveVehicleContext active,
    IVehicleRegistry vehicles, IVehicleOperationGate operations, IMavLinkWireMessageEncoder encoder,
    SigningKeyRepository repository, TimeProvider clock, SigningSetupOptions options)
{
    /// <summary>Gets current connection signing evidence, or an explicit disconnected state.</summary>
    public MavLinkSigningSnapshot Snapshot()
    {
        if (!active.IsOnline)
        {
            return new("Unavailable: connect a vehicle", "No key", 0, 0, 0, 0, 0, 0);
        }
        return connection.Connection.Signing?.Snapshot()
            ?? new("Unavailable: connection has no signing support", "No key", 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Performs an explicitly confirmed setup; cancellation or timeout may leave vehicle configuration changed.</summary>
    public async Task ConfigureAsync(byte[] key, byte linkId, bool confirmed, CancellationToken token)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("Signing setup requires explicit confirmation.");
        }
        if (!active.IsOnline || active.VehicleId is not { } id)
        {
            throw new InvalidOperationException("Connect and select a vehicle first.");
        }
        if (!operations.TryAcquire(id, "MAVLink signing setup", out var lease))
        {
            throw new InvalidOperationException("Another vehicle operation is running.");
        }
        if (options.ConfirmationTimeout <= TimeSpan.Zero || options.ConfirmationTimeout > TimeSpan.FromMinutes(1))
        {
            lease?.Dispose();
            throw new InvalidOperationException("Signing confirmation timeout must be positive and at most one minute.");
        }
        using (lease)
        using (var timeout = new CancellationTokenSource(options.ConfirmationTimeout, clock))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, active.ConnectionCancellationToken, timeout.Token))
        {
            var transport = connection.Connection;
            var signing = transport.Signing ?? throw new NotSupportedException("This connection does not support signing.");
            var target = vehicles.GetRequired(id) ?? throw new InvalidOperationException("The selected vehicle is no longer available.");
            var secret = key.ToArray();
            byte[]? packet = null;
            var staged = false;
            try
            {
                _ = MavLinkSigningCodec.Fingerprint(secret);
                var saved = await repository.PrepareAsync(secret, linkId, MavLinkSigningCodec.Timestamp(clock.GetUtcNow()), linked.Token).ConfigureAwait(false);
                if (saved.Floor == MavLinkSigningCodec.MaximumTimestamp)
                {
                    throw new InvalidOperationException("Signing timestamp exhausted.");
                }
                var timestamp = saved.Floor + 1;
                var confirmation = signing.BeginSetup(secret, linkId, id.SystemId, id.ComponentId, timestamp);
                staged = true;
                packet = encoder.Encode(new SetupSigningMessage(255, 190, target.EndPoint, id.SystemId, id.ComponentId, secret, timestamp, clock.GetUtcNow()));
                await transport.SendRawAsync(packet, target.EndPoint, linked.Token).ConfigureAwait(false);
                await confirmation.WaitAsync(linked.Token).ConfigureAwait(false);
                await signing.CommitSetupAsync((upper, cancellation) => repository.ReserveAsync(saved.Id, upper, cancellation), linked.Token).ConfigureAwait(false);
                staged = false;
            }
            finally
            {
                if (staged)
                {
                    signing.CancelSetup();
                }
                CryptographicOperations.ZeroMemory(secret);
                if (packet is not null)
                {
                    CryptographicOperations.ZeroMemory(packet);
                }
            }
        }
    }
}
