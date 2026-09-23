using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

public sealed partial class VehicleLiveDiagnostics
{
    /// <inheritdoc />
    public IReadOnlyList<VehicleRawDiagnostic> GetRaw(VehicleId vehicleId, string? nameOrId = null, byte? system = null, byte? component = null)
    {
        lock (sync)
        {
            return Get(vehicleId).Raw.Reverse().Where(item =>
                (string.IsNullOrWhiteSpace(nameOrId) || item.Name.Contains(nameOrId, StringComparison.OrdinalIgnoreCase) || item.MessageId.ToString() == nameOrId) &&
                (system is null || item.SystemId == system) && (component is null || item.ComponentId == component)).Take(200).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetOutputs(VehicleId vehicleId)
    {
        var snapshot = GetSnapshot(vehicleId);
        var outputs = snapshot.State?.Radio.ServoOutputsRaw;
        var values = new List<string>();
        if (outputs is null)
        {
            values.Add("No SERVO_OUTPUT_RAW received.");
            values.Add("Physical motor movement remains unknown.");
        }
        else
        {
            var observedAt = snapshot.State!.Radio.ServoObservedAt;
            var age = observedAt is { } at ? clock.GetUtcNow() - at : (TimeSpan?)null;
            var stale = snapshot.Disconnected || snapshot.State.ConnectionState != VehicleConnectionState.Online ||
                age is null || age < TimeSpan.Zero || age > options.OutputSampleLifetime;
            values.Add(stale
                ? "SERVO_OUTPUT_RAW was received previously, but the latest sample is stale."
                : "SERVO_OUTPUT_RAW received. Flight controller output is observed.");
            values.Add("Physical motor movement remains unknown.");
            values.Add(age is { } sampleAge
                ? $"Output sample age: {Math.Max(0, sampleAge.TotalSeconds):F1} s"
                : "Output sample age: unavailable");
            var bank = snapshot.State!.Radio.ServoOutputPort ?? 0;
            for (var index = 0; index < Math.Min(outputs.Count, 32); index++)
            {
                var number = index + 1;
                var function = bank == 0 ? parameters?.GetParameter(vehicleId, $"SERVO{number}_FUNCTION")?.Value : null;
                var role = function is >= 33 and <= 40 ? $"Motor{function - 32}" : $"Function {function?.ToString() ?? "unavailable"}";
                values.Add($"Bank {bank} output {number}: {outputs[index]} · {role} · SERVO{number}_FUNCTION={function?.ToString() ?? "unavailable"}");
            }
        }
        var protocol = parameters?.GetParameter(vehicleId, "MOT_PWM_TYPE")?.Value;
        values.Add($"Output protocol: {(protocol is { } value ? MissionPlanner.Core.Setup.OptionalHardware.Motor.MotorOutputDiagnostics.Protocol(value) : "unavailable")} · ESC RPM: unavailable");
        values.Add($"Output sample time: {snapshot.State?.Radio.ServoObservedAt:O}");
        values.AddRange(GetRecentEvents(vehicleId, 100).Where(item => item.Category == "Motor" ||
            item.Category.StartsWith("Command", StringComparison.Ordinal) && item.Message.Contains("209"))
            .Take(8).Select(item => $"{item.At:HH:mm:ss.fff} {item.Category}: {item.Message}"));
        return values;
    }
}
