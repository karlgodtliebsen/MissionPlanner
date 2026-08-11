using MissionPlanner.Core.Commands;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Component-targeted camera and gimbal protocol workflow.</summary>
public sealed class PayloadProtocolService(
    IVehicleComponentRegistry components,
    IVehicleRegistry vehicles,
    IVehicleConnectionSession connectionSession,
    IMavLinkCommandEncoder encoder,
    ICommandAckTracker acknowledgements,
    IVehicleOperationGate operationGate,
    IReplaySessionManager? replay = null) : ICameraProtocolService, IGimbalProtocolService
{
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public IReadOnlyList<CameraComponentState> GetCameras(byte systemId)
    {
        return components.GetComponents(systemId)
            .Where(item => item.MavType == (byte)MavType.Camera)
            .Select(item => new CameraComponentState(new PayloadComponentSelection(item.Key, "Camera", item.LastSeen), new CameraCapabilities(true, true, false, false))).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<GimbalComponentState> GetGimbals(byte systemId)
    {
        return components.GetComponents(systemId)
            .Where(item => item.MavType == (byte)MavType.Gimbal)
            .Select(item => new GimbalComponentState(new PayloadComponentSelection(item.Key, "Gimbal", item.LastSeen), new GimbalCapabilities(true, true, false))).ToArray();
    }

    /// <inheritdoc />
    public async Task<CameraOperationResult> CaptureImageAsync(VehicleId autopilot, byte componentId, CancellationToken cancellationToken)
    {
        var result = await SendAsync(autopilot, componentId, (ushort)MavCmd.ImageStartCapture,
            [0, 0, 1, 0, 0, 0, 0], cancellationToken).ConfigureAwait(false);
        return new CameraOperationResult(result.Accepted, result.Summary);
    }

    /// <inheritdoc />
    public async Task<CameraOperationResult> SetVideoAsync(VehicleId autopilot, byte componentId, bool start, CancellationToken cancellationToken)
    {
        var result = await SendAsync(autopilot, componentId, (ushort)(start ? MavCmd.VideoStartCapture : MavCmd.VideoStopCapture),
            [0, 0, 0, 0, 0, 0, 0], cancellationToken).ConfigureAwait(false);
        return new CameraOperationResult(result.Accepted, result.Summary);
    }

    /// <inheritdoc />
    public async Task<GimbalOperationResult> SetPitchYawAsync(VehicleId autopilot, byte componentId, float pitchDegrees,
        float yawDegrees, bool yawLock, CancellationToken cancellationToken)
    {
        if (!float.IsFinite(pitchDegrees) || pitchDegrees is < -90 or > 90 || !float.IsFinite(yawDegrees) || yawDegrees is < -180 or > 180)
        {
            return new GimbalOperationResult(false, "Pitch must be -90..90 and yaw -180..180 degrees.");
        }

        var flags = yawLock ? 16f : 0f;
        var result = await SendAsync(autopilot, componentId, (ushort)MavCmd.DoGimbalManagerPitchyaw,
            [pitchDegrees, yawDegrees, float.NaN, float.NaN, flags, 0, 0], cancellationToken).ConfigureAwait(false);
        return new GimbalOperationResult(result.Accepted, result.Summary);
    }

    private async Task<(bool Accepted, string Summary)> SendAsync(VehicleId autopilot, byte componentId, ushort command,
        IReadOnlyList<float> parameters, CancellationToken cancellationToken)
    {
        if (replay is not null && replay.Snapshot.State != ReplaySessionState.Unloaded)
        {
            return (false, "Payload writes are blocked during replay.");
        }

        var session = vehicles.GetRequired(autopilot);
        if (session is null)
        {
            return (false, "The active vehicle is unavailable.");
        }

        var target = new VehicleId(autopilot.SystemId, componentId);
        if (!operationGate.TryAcquire(autopilot, $"payload component {componentId}", out var lease))
        {
            return (false, "Another vehicle operation is active.");
        }

        using (lease)
        using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var wait = acknowledgements.WaitForAckAsync(target, command, timeout, lifetime.Token);
            try
            {
                await connectionSession.Connection.SendRawAsync(encoder.EncodeCommandLong(target.SystemId, target.ComponentId, command, parameters), session.EndPoint, lifetime.Token).ConfigureAwait(false);
                var ack = await wait.ConfigureAwait(false);
                return (ack.Result == 0, $"MAVLink ACK result {ack.Result}; observed payload state is not implied.");
            }
            catch (TimeoutException) { return (false, "No payload command acknowledgement was received."); }
            finally { await lifetime.CancelAsync().ConfigureAwait(false); }
        }
    }
}
