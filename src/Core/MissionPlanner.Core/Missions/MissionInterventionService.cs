using System.Threading.Channels;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Core.Missions;

/// <inheritdoc />
public sealed class MissionInterventionService(
    IVehicleRegistry registry,
    IVehicleConnectionSession connectionSession,
    IMavLinkCommandEncoder commandEncoder,
    IMavLinkMissionEncoder missionEncoder,
    ICommandAckTracker ackTracker,
    IVehicleOperationGate operationGate,
    IEventHub eventHub,
    IOnboardMissionSnapshotStore snapshots,
    IVehicleParameterRegistry parameters,
    ISimulationVehicleChannelRegistry? simulationChannels = null) : IMissionInterventionService
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TelemetryTimeout = TimeSpan.FromSeconds(3);

    public VehicleCommandDecision Evaluate(VehicleState state, VehicleAction action, ushort? sequence = null)
    {
        if (state.ConnectionState != VehicleConnectionState.Online)
        {
            return VehicleCommandDecision.Deny("Vehicle is not online.");
        }
        return action switch
        {
            VehicleAction.SetCurrentMissionItem => sequence is { } requested && IsKnownSequence(state, requested)
                ? VehicleCommandDecision.Allow()
                : VehicleCommandDecision.Deny("Select a canonical sequence known to exist onboard."),
            VehicleAction.RestartMission => HasKnownMission(state)
                ? VehicleCommandDecision.Allow(true, "Restart resets mission execution state and jump counters without arming or changing mode.")
                : VehicleCommandDecision.Deny("A mission must be known on the vehicle."),
            VehicleAction.ResumeMission => state.Navigation.MissionState == MissionState.Paused || state.Navigation.MissionMode == VehicleMissionMode.Suspended
                ? VehicleCommandDecision.Allow()
                : VehicleCommandDecision.Deny("Mission telemetry does not confirm a paused or suspended mission."),
            VehicleAction.AbortLanding => EvaluateAbortLanding(state),
            _ => VehicleCommandDecision.Deny("This is not a mission intervention action.")
        };
    }

    public Task<MissionInterventionResult> SetCurrentMissionItemAsync(VehicleId vehicleId, ushort sequence, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null)
        {
            return Task.FromResult(Denied(vehicleId, "Vehicle is not connected."));
        }
        if (!IsKnownSequence(state, sequence))
        {
            return Task.FromResult(Denied(vehicleId, "The requested canonical mission sequence is not known to exist onboard."));
        }
        return ExecuteMissionCurrentCommandAsync(vehicleId, "set current mission item", sequence, false, allowLegacyFallback: true, cancellationToken);
    }

    public Task<MissionInterventionResult> RestartMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null || !HasKnownMission(state))
        {
            return Task.FromResult(Denied(vehicleId, "A mission must be known on the connected vehicle."));
        }
        return ExecuteMissionCurrentCommandAsync(vehicleId, "restart mission", 0, true, allowLegacyFallback: false, cancellationToken);
    }

    public Task<MissionInterventionResult> ResumeMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null)
        {
            return Task.FromResult(Denied(vehicleId, "Vehicle is not connected."));
        }
        if (state.Navigation.MissionState != MissionState.Paused && state.Navigation.MissionMode != VehicleMissionMode.Suspended)
        {
            return Task.FromResult(Denied(vehicleId, "Mission telemetry does not confirm a paused or suspended mission."));
        }
        return ExecuteCommandAsync(vehicleId, "resume mission", (ushort)MavCmd.DoPauseContinue, [1, 0, 0, 0, 0, 0, 0],
            message => message.MissionState == (byte)MissionState.Active || message.MissionMode == (byte)VehicleMissionMode.Mission,
            acceptedWithoutTelemetryIsValid: true, cancellationToken);
    }

    public Task<MissionInterventionResult> AbortLandingAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null || state.ConnectionState != VehicleConnectionState.Online)
        {
            return Task.FromResult(Denied(vehicleId, "Vehicle is not online."));
        }
        if (state.Identity.Firmware.Family != FirmwareFamily.ArduPlane)
        {
            return Task.FromResult(Denied(vehicleId, "Abort Landing is available only for Plane."));
        }
        if (state.CustomMode != 10)
        {
            return Task.FromResult(Denied(vehicleId, "Plane must be in AUTO mode."));
        }
        if (state.Navigation.MissionState != MissionState.Active)
        {
            return Task.FromResult(Denied(vehicleId, "Mission execution is not confirmed active."));
        }
        if (!snapshots.TryGetCurrentItem(state, out var current) || current?.Command != (ushort)MavCmd.NavLand)
        {
            return Task.FromResult(Denied(vehicleId, "A verified current NAV_LAND mission item is required."));
        }
        if (parameters.GetParameter(vehicleId, "LAND_ABORT_THR")?.Value != 1)
        {
            return Task.FromResult(Denied(vehicleId, "LAND_ABORT_THR is missing or disabled."));
        }
        return ExecuteCommandAsync(vehicleId, "abort landing", (ushort)MavCmd.DoGoAround, [0, 0, 0, 0, 0, 0, 0],
            telemetryPredicate: null, acceptedWithoutTelemetryIsValid: true, cancellationToken);
    }

    private VehicleCommandDecision EvaluateAbortLanding(VehicleState state)
    {
        if (state.Identity.Firmware.Family != FirmwareFamily.ArduPlane) return VehicleCommandDecision.Deny("Abort Landing is Plane-only.");
        if (state.CustomMode != 10) return VehicleCommandDecision.Deny("AUTO mode is required.");
        if (state.Navigation.MissionState != MissionState.Active) return VehicleCommandDecision.Deny("Mission execution is not confirmed active.");
        if (!snapshots.TryGetCurrentItem(state, out var current) || current?.Command != (ushort)MavCmd.NavLand)
            return VehicleCommandDecision.Deny("The current landing item is not verified.");
        return parameters.GetParameter(state.VehicleId, "LAND_ABORT_THR")?.Value == 1
            ? VehicleCommandDecision.Allow(true, "Abort the active Plane landing approach.")
            : VehicleCommandDecision.Deny("Landing abort is disabled or unknown (LAND_ABORT_THR).");
    }

    private async Task<MissionInterventionResult> ExecuteMissionCurrentCommandAsync(
        VehicleId vehicleId, string name, ushort sequence, bool reset, bool allowLegacyFallback, CancellationToken cancellationToken)
    {
        if (!operationGate.TryAcquire(vehicleId, name, out var operationLease))
        {
            return new MissionInterventionResult(vehicleId, MissionInterventionStatus.Busy, $"Another operation is pending ({operationGate.GetCurrentOperation(vehicleId)}).");
        }
        using (operationLease)
        {
            var result = await ExecuteCommandAsync(vehicleId, name, (ushort)MavCmd.DoSetMissionCurrent,
                [sequence, reset ? 1 : 0, 0, 0, 0, 0, 0], message => message.Sequence == sequence,
                acceptedWithoutTelemetryIsValid: true, cancellationToken, acquireLease: false).ConfigureAwait(false);
            if (result.Status != MissionInterventionStatus.Unsupported || !allowLegacyFallback)
            {
                return result;
            }

            return await ExecuteLegacySetCurrentAsync(vehicleId, sequence, cancellationToken, acquireLease: false).ConfigureAwait(false);
        }
    }

    private async Task<MissionInterventionResult> ExecuteCommandAsync(
        VehicleId vehicleId,
        string name,
        ushort command,
        IReadOnlyList<float> commandParameters,
        Func<MissionCurrentMessage, bool>? telemetryPredicate,
        bool acceptedWithoutTelemetryIsValid,
        CancellationToken cancellationToken,
        bool acquireLease = true)
    {
        var session = registry.GetRequired(vehicleId);
        if (session is null)
        {
            return Denied(vehicleId, "Vehicle is not connected.");
        }
        IDisposable? lease = null;
        if (acquireLease && !operationGate.TryAcquire(vehicleId, name, out lease))
        {
            return new MissionInterventionResult(vehicleId, MissionInterventionStatus.Busy, $"Another operation is pending ({operationGate.GetCurrentOperation(vehicleId)}).");
        }

        using (lease)
        using (var telemetry = SubscribeMissionCurrent(vehicleId))
        using (var ackLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var ackWait = ackTracker.WaitForAckAsync(vehicleId, command, AckTimeout, ackLifetime.Token);
            try
            {
                var packet = commandEncoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, command, commandParameters);
                await GetConnection(vehicleId).SendRawAsync(packet, session.EndPoint, cancellationToken).ConfigureAwait(false);
                var ack = await ackWait.ConfigureAwait(false);
                var mapped = MapAck(vehicleId, ack.Result);
                if (mapped.Status != MissionInterventionStatus.AcceptedButNotTelemetryConfirmed)
                {
                    return mapped;
                }
                if (telemetryPredicate is null)
                {
                    return mapped;
                }
                var confirmed = await WaitForTelemetryAsync(telemetry.Reader, telemetryPredicate, cancellationToken).ConfigureAwait(false);
                return confirmed
                    ? new MissionInterventionResult(vehicleId, MissionInterventionStatus.TelemetryConfirmed, "Command ACK accepted and post-request mission telemetry confirmed the result.")
                    : new MissionInterventionResult(vehicleId, MissionInterventionStatus.AcceptedButNotTelemetryConfirmed,
                        acceptedWithoutTelemetryIsValid ? "Command ACK accepted; matching mission telemetry was not observed." : "Mission telemetry confirmation timed out.");
            }
            catch (TimeoutException)
            {
                return new MissionInterventionResult(vehicleId, MissionInterventionStatus.Timeout, "Command acknowledgement timed out.");
            }
            finally
            {
                await ackLifetime.CancelAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<MissionInterventionResult> ExecuteLegacySetCurrentAsync(VehicleId vehicleId, ushort sequence, CancellationToken cancellationToken, bool acquireLease = true)
    {
        var session = registry.GetRequired(vehicleId)!;
        IDisposable? lease = null;
        if (acquireLease && !operationGate.TryAcquire(vehicleId, "legacy set current mission item", out lease))
        {
            return new MissionInterventionResult(vehicleId, MissionInterventionStatus.Busy, "Another mission operation is pending.");
        }
        using (lease)
        using (var telemetry = SubscribeMissionCurrent(vehicleId))
        {
            await GetConnection(vehicleId).SendRawAsync(missionEncoder.EncodeMissionSetCurrent(vehicleId.SystemId, vehicleId.ComponentId, sequence), session.EndPoint, cancellationToken).ConfigureAwait(false);
            return await WaitForTelemetryAsync(telemetry.Reader, message => message.Sequence == sequence, cancellationToken).ConfigureAwait(false)
                ? new MissionInterventionResult(vehicleId, MissionInterventionStatus.FallbackTelemetryConfirmed, "Legacy fallback was confirmed by post-request MISSION_CURRENT telemetry; no command ACK exists for this path.")
                : new MissionInterventionResult(vehicleId, MissionInterventionStatus.Timeout, "Legacy fallback was not confirmed by mission telemetry.");
        }
    }

    private MissionTelemetrySubscription SubscribeMissionCurrent(VehicleId vehicleId)
    {
        var channel = Channel.CreateUnbounded<MissionCurrentMessage>();
        var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is MissionCurrentMessage current && current.SystemId == vehicleId.SystemId && current.ComponentId == vehicleId.ComponentId)
            {
                channel.Writer.TryWrite(current);
            }
            return Task.CompletedTask;
        });
        return new MissionTelemetrySubscription(channel.Reader, subscription);
    }

    private static async Task<bool> WaitForTelemetryAsync(ChannelReader<MissionCurrentMessage> reader, Func<MissionCurrentMessage, bool> predicate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TelemetryTimeout);
        try
        {
            while (await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    if (predicate(message)) return true;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        return false;
    }

    private bool IsKnownSequence(VehicleState state, ushort sequence)
    {
        if (snapshots.GetFreshness(state) == MissionSnapshotFreshness.VerifiedCurrent)
        {
            return snapshots.Get(state.VehicleId)!.Items.Any(item => item.Sequence == sequence);
        }
        return state.Navigation.MissionItemCount is { } total && sequence < total;
    }

    private bool HasKnownMission(VehicleState state) =>
        snapshots.GetFreshness(state) == MissionSnapshotFreshness.VerifiedCurrent
            ? snapshots.Get(state.VehicleId)!.Items.Count > 0
            : state.Navigation.MissionItemCount is > 0;

    private IMavLinkConnection GetConnection(VehicleId vehicleId) =>
        simulationChannels?.Find(vehicleId)?.ConnectionSession.Connection ?? connectionSession.Connection;

    private static MissionInterventionResult MapAck(VehicleId vehicleId, byte result) => result switch
    {
        0 => new(vehicleId, MissionInterventionStatus.AcceptedButNotTelemetryConfirmed, "Command ACK accepted."),
        2 => new(vehicleId, MissionInterventionStatus.Denied, "Command was denied by the vehicle."),
        3 => new(vehicleId, MissionInterventionStatus.Unsupported, "Command is unsupported by the vehicle."),
        _ => new(vehicleId, MissionInterventionStatus.Failed, $"Vehicle rejected the command with MAV_RESULT {result}.")
    };

    private static MissionInterventionResult Denied(VehicleId vehicleId, string message) => new(vehicleId, MissionInterventionStatus.Denied, message);

    private sealed record MissionTelemetrySubscription(ChannelReader<MissionCurrentMessage> Reader, IDisposable Lifetime) : IDisposable
    {
        public void Dispose() => Lifetime.Dispose();
    }
}
