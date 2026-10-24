using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>A tracker test review tied to a connection, logical axis and reported output limits.</summary>
public sealed record TrackerServoReview(VehicleId Vehicle, CancellationToken Connection, int Axis, string Output, int Minimum, int Maximum, int Trim);

/// <summary>Performs bounded ArduPilot tracker ServoTest commands and requests Stop during cleanup.</summary>
public sealed class AntennaTrackerTestService(IActiveVehicleContext active, IVehicleRegistry vehicles, IVehicleParameterRegistry parameters,
    IMavLinkConnection connection, IMavLinkCommandEncoder encoder, IEventHub events, IVehicleOperationGate operations)
{
    /// <summary>Resolves actual yaw/pitch output assignment and limits on a stopped, disarmed tracker.</summary>
    public TrackerServoReview Review(VehicleId id, int axis)
    {
        _ = Require(id, active.ConnectionCancellationToken, true);
        if (axis is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(axis));
        var values = parameters.GetAllParameters(id);
        var functions = values.Keys.Where(name => name.StartsWith("SERVO", StringComparison.Ordinal) && name.EndsWith("_FUNCTION", StringComparison.Ordinal)).ToArray();
        string prefix;
        if (functions.Length > 0)
        {
            var outputs = functions.Where(name => values[name].Value == (axis == 1 ? 71 : 72)).ToArray();
            if (outputs.Length != 1) throw new InvalidOperationException("Assign exactly one reported output to tracker yaw (71) or pitch (72) before this test.");
            prefix = outputs[0][..^9];
        }
        else prefix = $"RC{axis}";
        int Read(string suffix)
        {
            if (!values.TryGetValue(prefix + suffix, out var parameter) || !float.IsFinite(parameter.Value) || parameter.Value != Math.Truncate(parameter.Value))
                throw new InvalidOperationException("Read the tracker's complete servo limits first.");
            return checked((int)parameter.Value);
        }
        var minimum = Read("_MIN"); var maximum = Read("_MAX"); var trim = Read("_TRIM");
        if (minimum < 800 || maximum > 2200 || minimum >= maximum || trim < minimum || trim > maximum)
            throw new InvalidOperationException("Review tracker output limits and trim before testing (800–2200 μs).");
        return new(id, active.ConnectionCancellationToken, axis, prefix, minimum, maximum, trim);
    }

    /// <summary>Applies an explicitly reviewed PWM for two seconds at most after acknowledgement; cleanup independently requests Stop.</summary>
    public async Task TestAsync(TrackerServoReview review, int pwm, bool confirmed, IProgress<string> status, CancellationToken token)
    {
        if (!confirmed || pwm < review.Minimum || pwm > review.Maximum || Review(review.Vehicle, review.Axis) != review)
            throw new InvalidOperationException("Confirm an unchanged tracker connection/output and PWM within the reported limits.");
        var target = Require(review.Vehicle, review.Connection, true);
        if (!operations.TryAcquire(target.Id, "AntennaTracker servo test", out var lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
        using (lease)
        using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, review.Connection))
        {
            var attempted = false;
            try
            {
                lifetime.Token.ThrowIfCancellationRequested();
                _ = Require(target.Id, review.Connection, true);
                attempted = true;
                status.Report("Requesting ServoTest; this firmware mode enables servo power.");
                var until = DateTimeOffset.UtcNow.AddSeconds(2);
                await CommandAsync(target, 183, [review.Axis, pwm, 0, 0, 0, 0, 0], lifetime.Token).ConfigureAwait(false);
                while (DateTimeOffset.UtcNow < until)
                {
                    _ = Require(target.Id, review.Connection, false);
                    if (active.State!.CustomMode is not (1 or 3)) throw new InvalidOperationException("Tracker mode changed during the test.");
                    await Task.Delay(100, lifetime.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (attempted)
                {
                    try
                    {
                        _ = Require(target.Id, review.Connection, false);
                        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var before = active.State!.LastHeartbeatAt;
                        await CommandAsync(target, 176, [1, 1, 0, 0, 0, 0, 0], stop.Token).ConfigureAwait(false);
                        while (true)
                        {
                            _ = Require(target.Id, review.Connection, false);
                            if (active.State is { CustomMode: 1, IsArmed: false } state && state.LastHeartbeatAt > before) break;
                            await Task.Delay(50, stop.Token).ConfigureAwait(false);
                        }
                        status.Report("Tracker Stop mode and disarmed state confirmed by a new heartbeat. Verify physical motion has stopped.");
                    }
                    catch (Exception ex) { throw new IOException("Could not confirm Tracker Stop after the test. Stop the tracker or isolate servo power immediately.", ex); }
                }
            }
        }
    }

    private VehicleSession Require(VehicleId id, CancellationToken boundary, bool stopped)
    {
        var state = active.State; var now = DateTimeOffset.UtcNow;
        if (!active.IsOnline || active.VehicleId != id || active.ConnectionCancellationToken != boundary || boundary.IsCancellationRequested || state is null ||
            state.Autopilot != 3 || state.Identity.Firmware.Family != FirmwareFamily.AntennaTracker || state.LastHeartbeatAt > now || now - state.LastHeartbeatAt > TimeSpan.FromSeconds(3) ||
            stopped && (state.IsArmed || state.CustomMode != 1))
            throw new InvalidOperationException("Select a fresh ArduPilot AntennaTracker connection; begin tests in Stop mode with servos disarmed.");
        return vehicles.GetRequired(id) ?? throw new InvalidOperationException("Tracker session unavailable.");
    }

    private async Task CommandAsync(VehicleSession target, ushort command, float[] values, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var response = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is CommandAckMessage ack && ack.Command == command && ack.SystemId == target.Id.SystemId && ack.ComponentId == target.Id.ComponentId &&
                Equals(ack.EndPoint, target.EndPoint) && ack.TargetSystemId is null or 0 or 255 && ack.TargetComponentId is null or 0 or 190 && ack.Result != 5) response.TrySetResult(ack.Result);
            return Task.CompletedTask;
        });
        await connection.SendRawAsync(encoder.EncodeCommandLong(target.Id.SystemId, target.Id.ComponentId, command, values), target.EndPoint, timeout.Token).ConfigureAwait(false);
        var result = await response.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        if (result != 0) throw new IOException($"Tracker command {command} rejected (result {result}).");
    }
}
