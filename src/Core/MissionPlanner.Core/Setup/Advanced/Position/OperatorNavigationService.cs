using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Advanced.Position;

/// <summary>A reviewed rally plan bound to one vehicle connection.</summary>
public sealed record MovingRallyReview(VehicleSession Session, CancellationToken Connection, RallyPlan Plan);

/// <summary>Separates local GPS preview, guided following and explicitly selected moving-rally updates.</summary>
public sealed class OperatorNavigationService(OperatorPositionInput input, IActiveVehicleContext active, IVehicleRegistry vehicles,
    IMavLinkConnection connection, IMavLinkWireMessageEncoder encoder, IEventHub events, IVehicleOperationGate operations,
    IVehicleCommandService commands, ITrackerHomeService home, IMissionTransferService transfers, IRallyProtocolMapper rallyMapper,
    IRallyConfigurationService rallyWorkspace)
{
    private int running;
    /// <summary>Gets the owned input component; opening it does not enable navigation output.</summary>
    public OperatorPositionInput Input => input;

    /// <summary>Runs at one update per second in an already armed Guided Copter/Plane, then requests Hold on stop when possible.</summary>
    public async Task FollowAsync(VehicleId expectedVehicle, double altitudeAboveHome, double maximumDistance, bool confirmed,
        IProgress<string> status, CancellationToken token)
    {
        if (!confirmed || !double.IsFinite(altitudeAboveHome) || altitudeAboveHome is < 2 or > 1000 || !double.IsFinite(maximumDistance) || maximumDistance is < 5 or > 1000)
            throw new ArgumentException("Confirm a HOME-relative altitude of 2–1000 m and maximum target distance of 5–1000 m.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("Stop the current navigation workflow first.");
        VehicleSession? target = null;
        var boundary = active.ConnectionCancellationToken;
        var sent = false;
        try
        {
            target = RequireVehicle(expectedVehicle);
            RequireGuided(target.State);
            var generation = input.Generation;
            var previous = RequireFix(generation);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, boundary);
            if (!operations.TryAcquire(target.Id, "Follow Me", out var lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
            using (lease)
            {
                while (true)
                {
                    lifetime.Token.ThrowIfCancellationRequested();
                    RequireCaptured(target, boundary);
                    var state = target.State;
                    RequireGuided(state);
                    var fix = RequireFix(generation);
                    CheckJump(previous, fix);
                    if (state.Gps.FixType is not (GpsFixType.Fix3D or GpsFixType.DifferentialGps or GpsFixType.RtkFloat or GpsFixType.RtkFixed) ||
                        state.Gps.ObservedAt is not { } gpsTime || gpsTime > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - gpsTime > TimeSpan.FromSeconds(3) ||
                        state.Gps.LatitudeDegrees is not { } latitude || state.Gps.LongitudeDegrees is not { } longitude ||
                        !new GeoPosition(latitude, longitude).IsValid || Distance(new(latitude, longitude), fix.Position) > maximumDistance)
                        throw new InvalidOperationException("Vehicle GPS is stale or the operator is outside the reviewed maximum distance.");
                    previous = fix;
                    sent = true;
                    await RepositionAsync(target, fix.Position, altitudeAboveHome, lifetime.Token).ConfigureAwait(false);
                    status.Report($"Guided target accepted at {fix.Position.LatitudeDegrees:F7}, {fix.Position.LongitudeDegrees:F7}; {altitudeAboveHome:0.0} m above HOME. Acceptance does not confirm arrival.");
                    await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (sent && target is not null)
            {
                try
                {
                    RequireCaptured(target, boundary);
                    RequireGuided(target.State);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    var result = await commands.HoldAsync(target.Id, timeout.Token).ConfigureAwait(false);
                    status.Report("Follow Me stopped; Hold request result: " + result.Result + ". Verify the vehicle mode and take control if needed.");
                }
                catch (Exception) { status.Report("Follow Me stopped. Hold could not be confirmed on the original Guided connection. The vehicle may retain its last target; use the transmitter or flight controls."); }
            }
            Volatile.Write(ref running, 0);
        }
    }

    /// <summary>Downloads the current rally plan for explicit index selection without replacing local edits.</summary>
    public async Task<MovingRallyReview> ReadRallyAsync(VehicleId expectedVehicle, CancellationToken token)
    {
        var target = RequireVehicle(expectedVehicle);
        if (target.State.Identity.Firmware.Family != FirmwareFamily.ArduPlane) throw new InvalidOperationException("Moving rally updates currently support ArduPlane only.");
        if (rallyWorkspace.GetSnapshot(target.Id).IsDirty) throw new InvalidOperationException("Resolve pending rally edits in the planner before starting moving-rally updates.");
        var boundary = active.ConnectionCancellationToken;
        if (!operations.TryAcquire(target.Id, "Review moving rally point", out var lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
        using (lease)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, boundary))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var plan = await DownloadAsync(target, timeout.Token).ConfigureAwait(false);
            RequireCaptured(target, boundary);
            return new(target, boundary, plan);
        }
    }

    /// <summary>Updates the session-local tracker home; optionally moves only one reviewed existing rally point at most every five seconds.</summary>
    public async Task MoveBaseAsync(MovingRallyReview? review, int selectedIndex, double clearanceMeters, bool confirmed,
        IProgress<string> status, CancellationToken token)
    {
        if (!confirmed || !double.IsFinite(clearanceMeters) || clearanceMeters is < 0 or > 1000) throw new ArgumentException("Confirm moving-base output and a finite clearance from 0 to 1000 m.");
        if (review is not null && (selectedIndex < 0 || selectedIndex >= review.Plan.Points.Count)) throw new ArgumentOutOfRangeException(nameof(selectedIndex), "Select an existing rally point from the downloaded plan.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("Stop the current navigation workflow first.");
        try
        {
            var generation = input.Generation;
            var previous = RequireFix(generation);
            var expectedPlan = review?.Plan;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, review?.Connection ?? CancellationToken.None);
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                var fix = RequireFix(generation);
                CheckJump(previous, fix);
                previous = fix;
                if (review is not null)
                {
                    var target = review.Session;
                    RequireCaptured(target, review.Connection);
                    if (target.State.Identity.Firmware.Family != FirmwareFamily.ArduPlane) throw new InvalidOperationException("Moving rally updates require ArduPlane.");
                    if (rallyWorkspace.GetSnapshot(target.Id).IsDirty) throw new InvalidOperationException("Local rally edits appeared. Moving-rally updates stopped.");
                    if (!operations.TryAcquire(target.Id, "Moving base rally update", out var lease)) throw new InvalidOperationException("Another vehicle operation is active; moving-rally updates stopped.");
                    using (lease)
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(20));
                        var current = await DownloadAsync(target, timeout.Token).ConfigureAwait(false);
                        if (!Equivalent(current, expectedPlan!)) throw new InvalidOperationException("The rally plan changed outside this tool. Download and review it again.");
                        fix = RequireFix(generation);
                        RequireCaptured(target, review.Connection);
                        if (fix.AltitudeMsl is not { } altitude) throw new InvalidOperationException("Moving rally updates require a valid NMEA mean-sea-level altitude.");
                        var points = current.Points.ToArray();
                        points[selectedIndex] = points[selectedIndex] with { Position = fix.Position, Altitude = new(altitude + clearanceMeters, MissionAltitudeReference.MeanSeaLevel) };
                        var proposed = new RallyPlan(points);
                        var result = await transfers.UploadItemsAsync(target.Id, rallyMapper.ToProtocol(proposed), MissionPlanType.RallyPoints, cancellationToken: timeout.Token).ConfigureAwait(false);
                        if (!result.Success) throw new IOException(result.Error ?? "Moving rally upload failed; inspect the vehicle plan.");
                        var verified = await DownloadAsync(target, timeout.Token).ConfigureAwait(false);
                        if (!Equivalent(proposed, verified)) throw new IOException("Moving rally readback did not match. Updates stopped; inspect the vehicle plan.");
                        expectedPlan = verified;
                    }
                }
                home.Set(fix.Position, fix.AltitudeMsl, fix.ObservedAt, "External moving base GPS");
                status.Report(review is null ? "Local tracker home updated from operator GPS. No rally or vehicle HOME writes." : $"Rally point {selectedIndex + 1} updated and read back; all other rally points preserved. Altitude is GPS MSL plus {clearanceMeters:0.0} m.");
                await Task.Delay(TimeSpan.FromSeconds(review is null ? 1 : 5), lifetime.Token).ConfigureAwait(false);
            }
        }
        finally { Volatile.Write(ref running, 0); }
    }

    private async Task<RallyPlan> DownloadAsync(VehicleSession target, CancellationToken token)
    {
        var result = await transfers.DownloadAsync(target.Id, MissionPlanType.RallyPoints, token).ConfigureAwait(false);
        if (!result.Success) throw new IOException(result.Error ?? "Rally download failed.");
        return rallyMapper.FromProtocol(result.Items);
    }

    private async Task RepositionAsync(VehicleSession target, GeoPosition position, double altitude, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var response = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is CommandAckMessage ack && ack.Command == 192 && ack.SystemId == target.Id.SystemId && ack.ComponentId == target.Id.ComponentId &&
                Equals(ack.EndPoint, target.EndPoint) && ack.TargetSystemId is null or 0 or 255 && ack.TargetComponentId is null or 0 or 190 && ack.Result != 5) response.TrySetResult(ack.Result);
            return Task.CompletedTask;
        });
        // Flags=0 never changes mode; NaN yaw preserves the firmware's heading choice.
        var command = new CommandIntMessage(255, 190, target.EndPoint, target.Id.SystemId, target.Id.ComponentId, 6, 192, 0, 0,
            -1, 0, 0, float.NaN, checked((int)Math.Round(position.LatitudeDegrees * 1e7)), checked((int)Math.Round(position.LongitudeDegrees * 1e7)), (float)altitude, DateTimeOffset.UtcNow);
        await connection.SendRawAsync(encoder.Encode(command), target.EndPoint, timeout.Token).ConfigureAwait(false);
        var result = await response.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        if (result != 0) throw new IOException($"The firmware rejected the guided target (result {result}); following stopped.");
    }

    private VehicleSession RequireVehicle(VehicleId expected)
    {
        if (!active.IsOnline || active.VehicleId != expected || active.State?.Autopilot != 3 || active.State.LastHeartbeatAt > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - active.State.LastHeartbeatAt > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("The reviewed ArduPilot target must remain active with a fresh heartbeat.");
        return vehicles.GetRequired(expected) ?? throw new InvalidOperationException("Vehicle session unavailable.");
    }
    private void RequireCaptured(VehicleSession session, CancellationToken boundary)
    {
        if (!ReferenceEquals(RequireVehicle(session.Id), session) || boundary != active.ConnectionCancellationToken || boundary.IsCancellationRequested)
            throw new InvalidOperationException("The target or connection changed. Restart explicitly.");
    }
    private static void RequireGuided(VehicleState state)
    {
        if (!state.IsArmed || !(state.Identity.Firmware.Family == FirmwareFamily.ArduCopter && state.CustomMode == 4 || state.Identity.Firmware.Family == FirmwareFamily.ArduPlane && state.CustomMode == 15))
            throw new InvalidOperationException("Follow Me requires an already armed Copter or Plane in Guided mode. Arming, takeoff and mode selection are separate pilot actions.");
    }
    private OperatorPositionFix RequireFix(long generation)
    {
        if (generation != input.Generation || input.Current is not { } fix || !fix.IsFresh(DateTimeOffset.UtcNow)) throw new InvalidOperationException("Operator GPS is invalid, stale or its source changed. Navigation output stopped.");
        return fix;
    }
    private static void CheckJump(OperatorPositionFix previous, OperatorPositionFix current)
    {
        var seconds = (current.ReceiverTime - previous.ReceiverTime).TotalSeconds;
        if (seconds < 0 || Distance(previous.Position, current.Position) > Math.Max(10, seconds * 30)) throw new InvalidDataException("Operator GPS position jumped or time moved backwards; navigation output stopped.");
    }
    /// <summary>Computes the spherical horizontal distance in metres, including longitude wrap.</summary>
    public static double Distance(GeoPosition first, GeoPosition second)
    {
        if (!first.IsValid || !second.IsValid) throw new ArgumentException("Valid geographic coordinates are required.");
        const double radians = Math.PI / 180;
        var dlat = (second.LatitudeDegrees - first.LatitudeDegrees) * radians;
        var dlon = (second.LongitudeDegrees - first.LongitudeDegrees) * radians;
        var a = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(first.LatitudeDegrees * radians) * Math.Cos(second.LatitudeDegrees * radians) * Math.Pow(Math.Sin(dlon / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
    private static bool Equivalent(RallyPlan first, RallyPlan second) => first.Points.Count == second.Points.Count && first.Points.Zip(second.Points).All(p =>
        Distance(p.First.Position, p.Second.Position) < .03 && p.First.Altitude.Reference == p.Second.Altitude.Reference && Math.Abs(p.First.Altitude.Meters - p.Second.Altitude.Meters) < .01);
}
