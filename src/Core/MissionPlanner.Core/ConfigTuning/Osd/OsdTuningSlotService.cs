using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>A firmware-reported adjustable parameter slot on OSD screen 5 or 6.</summary>
/// <param name="Screen">The OSD screen number, 5 or 6.</param>
/// <param name="Index">The one-based slot, 1 through 9.</param>
/// <param name="Parameter">The assigned parameter identifier.</param>
/// <param name="Type">The OSD parameter presentation type.</param>
/// <param name="Minimum">The allowed minimum for custom numeric slots.</param>
/// <param name="Maximum">The allowed maximum for custom numeric slots.</param>
/// <param name="Increment">The adjustment step for custom numeric slots.</param>
public sealed record OsdTuningSlot(byte Screen, byte Index, string Parameter, byte Type, float Minimum, float Maximum, float Increment);

/// <summary>Correlates OSD slot transactions by request ID, vehicle identity and endpoint.</summary>
public sealed class OsdTuningSlotService(IActiveVehicleContext active, IVehicleRegistry registry,
    IVehicleParameterRegistry parameters, IEventHub events, IMavLinkConnection connection,
    IMavLinkWireMessageEncoder encoder, IVehicleOperationGate operations)
{
    private static int sequence;

    /// <summary>Reads a slot with bounded retries and reports unsupported slots explicitly.</summary>
    public async Task<OsdTuningSlot> ReadAsync(VehicleId vehicleId, byte screen, byte index, CancellationToken cancellationToken)
    {
        ValidateAddress(screen, index);
        var session = Require(vehicleId, false);
        var request = unchecked((uint)Interlocked.Increment(ref sequence));
        var message = new OsdParamShowConfigMessage(255, 190, session.EndPoint, vehicleId.SystemId, vehicleId.ComponentId, request, screen, index, DateTimeOffset.UtcNow);
        var reply = await ExchangeAsync<OsdParamShowConfigReplyMessage>(session, message, value => value.RequestId == request, cancellationToken).ConfigureAwait(false);
        if (reply.Result != 0) throw new NotSupportedException($"OSD {screen}, slot {index} is unavailable (firmware result {reply.Result}).");
        return new(screen, index, reply.ParamId, reply.ConfigType, reply.MinValue, reply.MaxValue, reply.Increment);
    }

    /// <summary>Checks the previous assignment, applies one reviewed change and reads it back.</summary>
    public async Task<OsdTuningSlot> ApplyAsync(VehicleId vehicleId, OsdTuningSlot expected, OsdTuningSlot proposed, bool confirmed, CancellationToken cancellationToken)
    {
        ValidateAddress(proposed.Screen, proposed.Index);
        if (!confirmed || expected.Screen != proposed.Screen || expected.Index != proposed.Index || proposed.Type > 7 ||
            string.IsNullOrWhiteSpace(proposed.Parameter) || proposed.Parameter.Length > 16 || proposed.Parameter.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_') ||
            !float.IsFinite(proposed.Minimum) || !float.IsFinite(proposed.Maximum) || !float.IsFinite(proposed.Increment) ||
            (proposed.Type == 0 && (proposed.Minimum >= proposed.Maximum || proposed.Increment <= 0 || proposed.Increment > proposed.Maximum - proposed.Minimum)))
            throw new ArgumentException("Confirm a reported parameter and valid slot type, range and positive increment.");
        var session = Require(vehicleId, true);
        if (!parameters.GetAllParameters(vehicleId).ContainsKey(proposed.Parameter))
            throw new ArgumentException("The assigned parameter must be reported by this vehicle.");
        if (!operations.TryAcquire(vehicleId, "OSD tuning slot configuration", out var lease))
            throw new InvalidOperationException("Another vehicle operation is in progress.");
        using (lease)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.ConnectionCancellationToken))
        {
            var current = await ReadAsync(vehicleId, proposed.Screen, proposed.Index, linked.Token).ConfigureAwait(false);
            if (current != expected) throw new InvalidOperationException("The OSD slot changed since it was read. Read again before applying.");
            Require(vehicleId, true);
            var request = unchecked((uint)Interlocked.Increment(ref sequence));
            var message = new OsdParamConfigMessage(255, 190, session.EndPoint, vehicleId.SystemId, vehicleId.ComponentId,
                request, proposed.Screen, proposed.Index, proposed.Parameter, proposed.Type, proposed.Minimum, proposed.Maximum, proposed.Increment, DateTimeOffset.UtcNow);
            var reply = await ExchangeAsync<OsdParamConfigReplyMessage>(session, message, value => value.RequestId == request, linked.Token).ConfigureAwait(false);
            if (reply.Result != 0) throw new IOException($"The firmware rejected the OSD slot assignment (result {reply.Result}).");
            var readback = await ReadAsync(vehicleId, proposed.Screen, proposed.Index, linked.Token).ConfigureAwait(false);
            if (readback.Parameter != proposed.Parameter || readback.Type != proposed.Type ||
                (proposed.Type == 0 && (readback.Minimum != proposed.Minimum || readback.Maximum != proposed.Maximum || readback.Increment != proposed.Increment)))
                throw new IOException("OSD slot readback did not match. Read the current assignment before retrying.");
            return readback;
        }
    }

    private async Task<T> ExchangeAsync<T>(VehicleSession session, GeneratedMavLinkMessage request, Func<T, bool> matches, CancellationToken token) where T : MavLinkMessage
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, active.ConnectionCancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(10));
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is T reply && message.SystemId == session.Id.SystemId && message.ComponentId == session.Id.ComponentId && Equals(message.EndPoint, session.EndPoint) && matches(reply))
                completion.TrySetResult(reply);
            return Task.CompletedTask;
        });
        for (var attempt = 0; attempt < 3; attempt++)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            await connection.SendRawAsync(encoder.Encode(request), session.EndPoint, lifetime.Token).ConfigureAwait(false);
            try { return await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), lifetime.Token).ConfigureAwait(false); }
            catch (TimeoutException) when (attempt < 2) { }
        }
        throw new TimeoutException("The vehicle did not answer the OSD slot request.");
    }

    private VehicleSession Require(VehicleId id, bool writing)
    {
        if (active.VehicleId != id || !active.IsOnline || active.State?.Autopilot != 3 || (writing && active.State?.IsArmed != false))
            throw new InvalidOperationException("Select an online ArduPilot vehicle; disarm before changing OSD slots.");
        return registry.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable.");
    }

    private static void ValidateAddress(byte screen, byte index)
    {
        if (screen is not (5 or 6) || index is < 1 or > 9) throw new ArgumentOutOfRangeException(nameof(index), "Choose OSD screen 5 or 6 and slot 1–9.");
    }
}
