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
            var recent = entry.LastArmAttemptAt is { } attempt && now - attempt <= options.ReasonLifetime;
            var stage = lost ? ArmingDiagnosticStage.Unknown :
                armed ? recent && entry.DisarmRequested ? ArmingDiagnosticStage.DisarmRequested : ArmingDiagnosticStage.Armed :
                recent && !entry.DisarmRequested ? entry.LastArmAck is 1 or 2 or 3 or 4 or 6
                    ? ArmingDiagnosticStage.ArmRejected : ArmingDiagnosticStage.ArmRequested :
                reasons.Count > 0 || ready == false ? ArmingDiagnosticStage.DisarmedNotReady :
                ready == true ? ArmingDiagnosticStage.DisarmedReady : ArmingDiagnosticStage.Unknown;
            if (stage == ArmingDiagnosticStage.ArmRequested)
            {
                summary = "ARM REQUESTED / AWAITING HEARTBEAT";
            }
            if (stage == ArmingDiagnosticStage.ArmRejected)
            {
                summary = "ARM REQUEST REJECTED";
            }
            if (stage == ArmingDiagnosticStage.DisarmRequested)
            {
                summary = "DISARM REQUESTED / AWAITING HEARTBEAT";
            }
            var liveRc = state is not null && state.Radio.ChannelCount > 0 && state.Radio.RssiPercent != 0 &&
                !state.Radio.IsStale(now, options.OutputSampleLifetime);
            var guidance = !lost && !armed && !recent && ready == true && liveRc
                ? "No recent arm request was observed. Check the assigned Arm/Disarm RC option, transmitter switch mapping, and stick arming configuration.\n" +
                  MissionPlanner.Core.Setup.MandatoryHardware.RadioArmingConfiguration.Describe(name => parameters?.GetParameter(vehicleId, name)?.Value)
                : null;
            return new(summary, armed, ready, reasons.Distinct().ToArray(),
                entry.LastArmFailure ?? state?.Arming.LastArmFailure, entry.LastArmAttemptAt, entry.LastArmResult)
            {
                Stage = stage, LastArmCommandSource = entry.LastArmCommandSource, LastArmAck = entry.LastArmAck,
                LastPreArmReason = entry.LastPreArmReason, LastPreArmReasonAt = entry.LastPreArmReasonAt,
                Guidance = guidance
            };
        }
    }

    private void ObserveArmingInput(Entry entry, VehicleState? previous, VehicleState state, DateTimeOffset now)
    {
        float? Parameter(string name) => parameters?.GetParameter(state.VehicleId, name)?.Value;
        var radio = state.Radio;
        if (previous is not null && radio.ObservedAt != previous.Radio.ObservedAt &&
            !radio.IsStale(now, options.OutputSampleLifetime) && !previous.Radio.IsStale(now, options.OutputSampleLifetime))
        {
            var count = Math.Min(Math.Min(radio.ChannelsRaw.Count, previous.Radio.ChannelsRaw.Count), radio.ChannelCount ?? 0);
            for (var channel = 1; channel <= count; channel++)
            {
                var current = radio.ChannelsRaw[channel - 1];
                var old = previous.Radio.ChannelsRaw[channel - 1];
                if (Parameter($"RC{channel}_OPTION") == 153 && old is >= 800 and <= 2200 && current is >= 800 and <= 2200 &&
                    (old <= 1200 && current >= 1800 || old >= 1800 && current <= 1200))
                {
                    RecordCandidate($"RC auxiliary switch RC{channel} (inferred from configured function and movement)", current <= 1200);
                }
            }
            if (!state.IsArmed && Parameter("ARMING_RUDDER") is > 0 && Parameter("RCMAP_THROTTLE") is { } throttle &&
                Parameter("RCMAP_YAW") is { } yaw && throttle >= 1 && throttle <= count && yaw >= 1 && yaw <= count)
            {
                var t = radio.ChannelsRaw[(int)throttle - 1];
                var y = radio.ChannelsRaw[(int)yaw - 1];
                var oldY = previous.Radio.ChannelsRaw[(int)yaw - 1];
                var reversed = Parameter($"RC{(int)yaw}_REVERSED") == -1;
                if (t is >= 800 and <= 1100 && y is >= 800 and <= 2200 &&
                    (reversed ? y <= 1100 && oldY > 1100 : y >= 1900 && oldY < 1900))
                {
                    RecordCandidate("Rudder/stick arming gesture (inferred; hold time not confirmed)", false);
                }
            }
        }
        if (state.IsArmed && previous?.IsArmed == false)
        {
            entry.LastArmResult = "Armed heartbeat observed";
            if (entry.LastArmAttemptAt is null || now - entry.LastArmAttemptAt > options.ReasonLifetime)
            {
                entry.LastArmAttemptAt = now;
                entry.LastArmCommandSource = "Unknown source; armed heartbeat observed";
                entry.LastArmAck = null;
            }
            entry.DisarmRequested = false;
        }
        if (!state.IsArmed && previous?.IsArmed == true)
        {
            entry.LastArmResult = "Disarmed heartbeat observed";
            entry.DisarmRequested = true;
        }
        void RecordCandidate(string source, bool disarm)
        {
            entry.LastArmAttemptAt = now;
            entry.LastArmCommandSource = source;
            entry.LastArmAck = null;
            entry.ArmCorrelation = null;
            entry.DisarmRequested = disarm;
            entry.LastArmResult = "RC request candidate observed; heartbeat confirmation required";
            Add(entry, new(state.VehicleId, now, "Arming", source));
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
            if (command.CommandId == 400)
            {
                if (command.Stage == "TX")
                {
                    entry.LastArmAttemptAt = command.At;
                    entry.DisarmRequested = !command.IsArmRequest;
                    entry.LastArmCommandSource = "MAVLink arm/disarm command";
                    entry.LastArmAck = null;
                    entry.ArmCorrelation = command.CorrelationId;
                    entry.LastArmResult = "Awaiting acknowledgement and heartbeat confirmation";
                }
                else if (entry.ArmCorrelation == command.CorrelationId)
                {
                    entry.LastArmResult = command.Detail;
                    entry.LastArmAck = command.CommandAck ?? entry.LastArmAck;
                }
            }
        }
    }
}
