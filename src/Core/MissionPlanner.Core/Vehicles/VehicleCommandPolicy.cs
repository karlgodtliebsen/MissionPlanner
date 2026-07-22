using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Applies connection, identity, flight-state, and telemetry-freshness safety rules to vehicle actions.
/// </summary>
public sealed class VehicleCommandPolicy(IDateTimeProvider clock) : IVehicleCommandPolicy
{
    private static readonly TimeSpan maximumHeartbeatAge = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan maximumActionTelemetryAge = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public VehicleCommandDecision Evaluate(VehicleState state, VehicleAction action)
    {
        var common = ValidateCommon(state);
        if (common is not null)
        {
            return common;
        }

        return action switch
        {
            VehicleAction.Arm => ValidateArmAction(state),
            VehicleAction.Disarm => !state.IsArmed
                ? VehicleCommandDecision.Deny("Vehicle is already disarmed.")
                : state.Flight.LandedState == VehicleLandedState.OnGround
                    ? VehicleCommandDecision.Allow()
                    : VehicleCommandDecision.Allow(true, "Disarming a vehicle that is not confirmed on the ground can stop its motors in flight."),
            VehicleAction.SetMode => VehicleCommandDecision.Allow(),
            VehicleAction.Takeoff => ValidateTakeoff(state),
            VehicleAction.Land or VehicleAction.ReturnToLaunch or VehicleAction.Hold => ValidateInFlightAction(state),
            VehicleAction.RebootAutopilot => ValidateReboot(state),
            VehicleAction.SetHomeHere => ValidateSetHome(state),
            VehicleAction.ExpertCommand => VehicleCommandDecision.Allow(true, "Expert commands can change safety-critical vehicle behavior."),
            _ => VehicleCommandDecision.Deny("The action is not supported.")
        };
    }

    /// <inheritdoc />
    public VehicleCommandResponse? ValidateArm(VehicleState state) => ToResponse(state, Evaluate(state, VehicleAction.Arm));

    /// <inheritdoc />
    public VehicleCommandResponse? ValidateDisarm(VehicleState state) => ToResponse(state, Evaluate(state, VehicleAction.Disarm));

    /// <inheritdoc />
    public VehicleCommandResponse? ValidateSetMode(VehicleState state, VehicleMode mode)
    {
        var decision = Evaluate(state, VehicleAction.SetMode);
        if (decision.IsAllowed && mode == VehicleMode.Guided && !state.IsArmed)
        {
            decision = VehicleCommandDecision.Deny("Guided mode requires an armed vehicle.");
        }

        return ToResponse(state, decision);
    }

    private VehicleCommandDecision? ValidateCommon(VehicleState state)
    {
        if (state.ConnectionState != VehicleConnectionState.Online)
        {
            return VehicleCommandDecision.Deny("Vehicle is not online.");
        }

        if (clock.UtcNow - state.LastHeartbeatAt > maximumHeartbeatAge)
        {
            return VehicleCommandDecision.Deny("Vehicle heartbeat is stale.");
        }

        return state.Identity.Firmware.Family is FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane or FirmwareFamily.Rover or FirmwareFamily.ArduSub
            ? null
            : VehicleCommandDecision.Deny("The connected firmware family does not support this action.");
    }

    private VehicleCommandDecision ValidateTakeoff(VehicleState state)
    {
        if (state.Identity.Firmware.Family is not (FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane))
        {
            return VehicleCommandDecision.Deny("Automatic takeoff is only available for Copter and Plane vehicles.");
        }

        if (!state.IsArmed)
        {
            return VehicleCommandDecision.Deny("Vehicle must be armed before takeoff.");
        }

        if (state.Flight.LandedState != VehicleLandedState.OnGround || state.Flight.IsStale(clock.UtcNow, maximumActionTelemetryAge))
        {
            return VehicleCommandDecision.Deny("Fresh telemetry must confirm that the vehicle is on the ground.");
        }

        return HasFreshPosition(state)
            ? VehicleCommandDecision.Allow(true, "Confirm automatic takeoff.")
            : VehicleCommandDecision.Deny("Takeoff requires a fresh 3D GPS position.");
    }

    private VehicleCommandDecision ValidateArmAction(VehicleState state)
    {
        if (state.IsArmed)
        {
            return VehicleCommandDecision.Deny("Vehicle is already armed.");
        }

        if (state.Flight.IsStale(clock.UtcNow, maximumActionTelemetryAge))
        {
            return VehicleCommandDecision.Deny("Fresh flight-state telemetry is required before arming.");
        }

        return state.Flight.LandedState == VehicleLandedState.OnGround
            ? VehicleCommandDecision.Allow()
            : VehicleCommandDecision.Deny("The vehicle must be confirmed on the ground before arming.");
    }

    private VehicleCommandDecision ValidateInFlightAction(VehicleState state)
    {
        if (!state.IsArmed)
        {
            return VehicleCommandDecision.Deny("Vehicle must be armed.");
        }

        if (state.Flight.IsStale(clock.UtcNow, maximumActionTelemetryAge))
        {
            return VehicleCommandDecision.Deny("Fresh flight-state telemetry is required.");
        }

        return state.Flight.LandedState == VehicleLandedState.OnGround
            ? VehicleCommandDecision.Deny("Vehicle is confirmed on the ground.")
            : VehicleCommandDecision.Allow();
    }

    private static VehicleCommandDecision ValidateReboot(VehicleState state)
    {
        if (state.IsArmed || state.Flight.LandedState is VehicleLandedState.InAir or VehicleLandedState.TakingOff or VehicleLandedState.Landing)
        {
            return VehicleCommandDecision.Deny("Autopilot reboot is blocked while the vehicle is armed or airborne.");
        }

        return VehicleCommandDecision.Allow(true, "Rebooting the autopilot interrupts telemetry and control until it restarts.");
    }

    private VehicleCommandDecision ValidateSetHome(VehicleState state) => HasFreshPosition(state)
        ? VehicleCommandDecision.Allow(true, "Changing home affects RTL and other recovery behavior.")
        : VehicleCommandDecision.Deny("Setting home requires a fresh 3D GPS position.");

    private bool HasFreshPosition(VehicleState state) =>
        state.Gps.FixType >= GpsFixType.Fix3D &&
        !state.Gps.IsStale(clock.UtcNow, maximumActionTelemetryAge) &&
        state.Position.LatitudeDegrees is not null &&
        state.Position.LongitudeDegrees is not null &&
        state.Position.ObservedAt is { } observedAt &&
        clock.UtcNow - observedAt <= maximumActionTelemetryAge;

    private VehicleCommandResponse? ToResponse(VehicleState state, VehicleCommandDecision decision) => decision.IsAllowed
        ? null
        : new VehicleCommandResponse(state.VehicleId, VehicleCommandResult.Denied, clock.UtcNow, decision.Reason);
}
