using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

public sealed partial class VehicleLiveDiagnostics
{
    /// <inheritdoc />
    public VehicleArmingDiagnostic GetArming(VehicleId vehicleId)
    {
        lock (sync)
        {
            var entry = Get(vehicleId);
            var state = entry.State;
            var now = clock.GetUtcNow();
            foreach (var expired in entry.Reasons.Where(pair => now - pair.Value > options.ReasonLifetime).Select(pair => pair.Key).ToArray())
            {
                entry.Reasons.Remove(expired);
            }
            var reasons = entry.Reasons.Keys.ToList();
            var armed = state?.IsArmed == true;
            bool? ready = null;
            if (state is not null)
            {
                const uint preArm = 1u << 28;
                if (state.Health.SystemObservedAt is { } observed && now - observed <= options.ReasonLifetime &&
                    state.Health.SensorsPresent is { } present && (present & preArm) != 0 &&
                    state.Health.SensorsEnabled is { } enabled && (enabled & preArm) != 0 &&
                    state.Health.SensorsHealthy is { } health)
                {
                    ready = (health & preArm) != 0;
                }
                if (ready == true || armed)
                {
                    entry.Reasons.Clear();
                    reasons.Clear();
                }
                else
                {
                    if (SensorFailed(state, 1u << 16, now))
                    {
                        reasons.Add("RC receiver unhealthy");
                    }
                    if (SensorFailed(state, 1u << 25, now))
                    {
                        reasons.Add("Battery unhealthy");
                    }
                    if (state.OnboardLogging.AffectsArming && state.OnboardLogging.Healthy == false)
                    {
                        reasons.Add(state.OnboardLogging.LatestMessage ?? "Onboard logging failed");
                    }
                    if (ready == false && reasons.Count == 0)
                    {
                        reasons.Add("Flight controller reports pre-arm checks not ready");
                    }
                }
            }
            var lost = entry.Disconnected || state?.Connection.State is VehicleConnectionState.Offline;
            var summary = lost ? "CONNECTION LOST" : armed ? "ARMED" :
                reasons.Count > 0 || ready == false ? "DISARMED / NOT READY" :
                ready == true ? "DISARMED / READY" : "ARMING UNKNOWN";
            return new(summary, armed, ready, reasons.Distinct().ToArray(),
                entry.LastArmFailure ?? state?.Arming.LastArmFailure, entry.LastArmAttemptAt, entry.LastArmResult);
        }
    }

    private bool SensorFailed(VehicleState state, uint bit, DateTimeOffset now)
    {
        return state.Health.SystemObservedAt is { } observed && now - observed <= options.ReasonLifetime &&
            state.Health.SensorsEnabled is { } enabled && (enabled & bit) != 0 &&
            state.Health.SensorsPresent is { } present && (present & bit) != 0 &&
            state.Health.SensorsHealthy is { } healthy && (healthy & bit) == 0;
    }

    private void AcceptCommand(VehicleCommandDiagnostic command)
    {
        lock (sync)
        {
            var entry = Get(command.VehicleId);
            Add(entry, new(command.VehicleId, command.At, $"Command {command.Stage}", command.Detail, command.CorrelationId));
            if (command.CommandId == 209)
            {
                entry.MotorCorrelation = command.CorrelationId;
            }
            if (command.CommandId == 400 && command.IsArmRequest)
            {
                if (command.Stage == "TX")
                {
                    entry.LastArmAttemptAt = command.At;
                    entry.ArmCorrelation = command.CorrelationId;
                    entry.LastArmResult = "Awaiting acknowledgement and armed heartbeat";
                }
                else if (entry.ArmCorrelation == command.CorrelationId)
                {
                    entry.LastArmResult = command.Detail;
                }
            }
        }
    }
}
