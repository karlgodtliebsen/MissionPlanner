using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>RC channel evidence and configured calibration, without inferring input activity from channel count.</summary>
/// <param name="Channel">One-based input number.</param>
/// <param name="Name">Known configured role or unknown mapping.</param>
/// <param name="Value">Current PWM input, absent for invalid sentinel values.</param>
/// <param name="Minimum">Configured minimum.</param>
/// <param name="Trim">Configured neutral.</param>
/// <param name="Maximum">Configured maximum.</param>
/// <param name="Assigned">Whether a primary or AUX function is configured.</param>
public sealed record VehicleDiagnosticChannel(int Channel, string Name, ushort? Value,
    float? Minimum, float? Trim, float? Maximum, bool Assigned)
{
    /// <summary>Normalized display position using configured limits or a visual-only 1000–2000 scale.</summary>
    public double Position => Value is { } value ? Math.Clamp((value - (Minimum ?? 1000)) /
        Math.Max(1, (Maximum ?? 2000) - (Minimum ?? 1000)) * 100, 0, 100) : 0;

    /// <summary>Readable calibration evidence.</summary>
    public string Calibration => $"Min {Minimum?.ToString() ?? "?"} · Now {Value?.ToString() ?? "unavailable"} · Trim {Trim?.ToString() ?? "?"} · Max {Maximum?.ToString() ?? "?"}";

    /// <summary>Unassigned inputs are displayed less prominently.</summary>
    public double Opacity => Assigned ? 1 : 0.55;
}

/// <summary>Projects domain evidence and downloaded parameters into diagnostic panels.</summary>
public static class VehicleDiagnosticPanels
{
    /// <summary>Returns current RC input values with known primary and AUX mappings.</summary>
    public static IReadOnlyList<VehicleDiagnosticChannel> Rc(VehicleState? state, Func<string, float?> parameter)
    {
        if (state is null)
        {
            return [];
        }
        var roles = new[] { ("RCMAP_ROLL", "Roll"), ("RCMAP_PITCH", "Pitch"), ("RCMAP_THROTTLE", "Throttle"), ("RCMAP_YAW", "Yaw") };
        var rows = new List<VehicleDiagnosticChannel>();
        for (var index = 0; index < Math.Min(32, state.Radio.ChannelsRaw.Count); index++)
        {
            var channel = index + 1;
            var names = roles.Where(role => parameter(role.Item1) == channel).Select(role => role.Item2).ToList();
            if (parameter("FLTMODE_CH") == channel || parameter("MODE_CH") == channel)
            {
                names.Add("Mode");
            }
            var option = parameter($"RC{channel}_OPTION");
            if (option is > 0)
            {
                names.Add(option == 153 ? "Arm/Disarm" : $"AUX option {option}");
            }
            var value = state.Radio.ChannelsRaw[index];
            rows.Add(new(channel, names.Count == 0 ? $"RC{channel} · unassigned / mapping unavailable" : $"RC{channel} · {string.Join(", ", names)}",
                value is 0 or ushort.MaxValue ? null : value, parameter($"RC{channel}_MIN"),
                parameter($"RC{channel}_TRIM"), parameter($"RC{channel}_MAX"), names.Count > 0));
        }
        return rows;
    }

    /// <summary>Describes supported sensor health while distinguishing optional disabled sensors.</summary>
    public static IReadOnlyList<string> Sensors(VehicleState state, DateTimeOffset now)
    {
        var values = new List<string>();
        foreach (var (bit, label) in new[] { (0, "Gyro"), (1, "Accelerometer"), (2, "Compass"), (3, "Barometer"), (5, "GPS"), (6, "Optical flow"), (8, "Range sensor") })
        {
            var mask = 1u << bit;
            string Flag(uint? value) => value is null ? "unknown" : (value & mask) != 0 ? "yes" : "no";
            var enabled = state.Health.SensorsEnabled;
            var status = enabled is null ? "unknown" : (enabled & mask) == 0 ? "disabled / optional" :
                state.Health.SensorsHealthy is null ? "unknown" : (state.Health.SensorsHealthy & mask) != 0 ? "healthy" : "unhealthy";
            values.Add($"{label} · Detected {Flag(state.Health.SensorsPresent)} · Enabled {Flag(enabled)} · {status}");
        }
        values.Add($"EKF: {state.Health.EkfHealthy?.ToString() ?? "unavailable"} · flags {state.Health.EkfFlags?.ToString() ?? "unavailable"}");
        values.Add($"GPS: {state.Gps.FixType} · satellites {state.Gps.SatellitesVisible?.ToString() ?? "unavailable"}");
        values.Add($"Vibration: X {state.Vibration.X} / Y {state.Vibration.Y} / Z {state.Vibration.Z}");
        values.Add($"Range sensors: {state.Range.Sensors.Count}");
        values.Add($"Onboard logging: {state.OnboardLogging.DisplayState} · {state.OnboardLogging.LatestMessage}");
        values.Add($"System health age: {(state.Health.SystemObservedAt is { } at ? $"{(now - at).TotalSeconds:F1} s" : "unavailable")}");
        return values;
    }

    /// <summary>Shows connection and power evidence; absent values remain explicitly unavailable.</summary>
    public static IReadOnlyList<string> Describe(string panel, VehicleLiveDiagnosticSnapshot snapshot,
        VehicleArmingDiagnostic arming, DateTimeOffset now)
    {
        var state = snapshot.State;
        if (state is null)
        {
            return ["No telemetry available for this vehicle."];
        }
        string Value(object? value, string unit = "") => value is null ? "unavailable" : $"{(value is IFormattable formatted ? formatted.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value)}{unit}";
        string Age(DateTimeOffset? at) => at is null ? "unavailable" : $"{Math.Max(0, (now - at.Value).TotalSeconds):F1} s";
        return panel switch
        {
            "Status" => new[]
            {
                arming.Summary,
                $"Connection: {(snapshot.Disconnected ? "Disconnected" : state.Connection.State)}",
                $"Transport: {Value(snapshot.Transport)} · Endpoint: {Value(snapshot.Endpoint)}",
                $"Last valid packet: {Age(state.Connection.LastPacketAt)} · Heartbeat: {Age(state.Connection.LastHeartbeatAt)}",
                $"Link drop rate: {Value(state.Health.CommunicationDropRatePercent, "%")} · RX errors: {Value(state.Radio.ReceiveErrors)}",
                $"Mode: {state.Flight.Mode} · System status: {state.Flight.SystemStatus}",
                $"Firmware: {state.Identity.Firmware.Family} {state.Identity.Firmware.FlightVersion} · {state.Identity.Firmware.FlightGitHash}",
                $"Vehicle: {state.DisplayName} · Board: {state.Identity.Firmware.BoardVersion} · VID/PID: {state.Identity.Firmware.VendorId}/{state.Identity.Firmware.ProductId} · UID: {state.Identity.Firmware.HardwareUid2 ?? state.Identity.Firmware.HardwareUid?.ToString() ?? "unavailable"}",
                $"Last arm attempt: {Value(arming.LastArmAttemptAt)} · {Value(arming.LastArmResult)}",
                $"Last arm failure: {Value(arming.LastArmFailure)}"
            }.Concat(arming.Reasons.Select(reason => $"Why not armed? {reason}")).ToArray(),
            "Power" => new[]
            {
                $"Battery: {Value(state.Power.BatteryVoltageVolts, " V")}",
                $"Current: {Value(state.Power.BatteryCurrentAmps, " A")}",
                $"Remaining: {Value(state.Power.BatteryRemainingPercent, "%")}",
                $"Consumed: {Value(state.Power.BatteryConsumedMah, " mAh")} / {Value(state.Power.BatteryConsumedWh, " Wh")}",
                $"Controller: {Value(state.Power.ControllerVoltageVolts, " V")} · Servo rail: {Value(state.Power.ServoVoltageVolts, " V")}",
                $"Battery failsafe evidence: {string.Join("; ", arming.Reasons.Where(reason => reason.Contains("battery", StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty("No active report; unknown if not reported"))}",
                $"Power sample age: {Age(state.Power.ObservedAt)}"
            },
            "RC" => new[]
            {
                $"RC sample age: {Age(state.Radio.ObservedAt)} · RSSI: {Value(state.Radio.RssiPercent, "%")}",
                $"Advertised inputs: {Value(state.Radio.ChannelCount)} · link quality: unavailable",
                "Only configured roles are highlighted. Advertised channels need not all move."
            },
            "Sensors" => Sensors(state, now),
            _ => [$"{panel}: no evidence available"]
        };
    }
}
