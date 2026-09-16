namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Separates compass configuration intent, reported detection, and health evidence.</summary>
/// <param name="Configured">Whether a compass is enabled for use.</param>
/// <param name="Detected">Whether a nonzero device ID was reported.</param>
/// <param name="Required">Whether any configured EKF3 yaw source requires a compass.</param>
/// <param name="Healthy">Known subsystem health; null when telemetry/configuration is incomplete.</param>
/// <param name="Evidence">Raw parameter values and per-slot decoded identity evidence.</param>
public sealed record CompassDiagnostics(bool? Configured, bool? Detected, bool? Required, bool? Healthy, IReadOnlyList<string> Evidence)
{
    /// <summary>Gets a readable summary without replacing unknown values with optimistic defaults.</summary>
    public string Summary => $"Configured: {Label(Configured)} · Detected: {Label(Detected)} · Required by configured EKF yaw sources: {Label(Required)} · " +
        $"Healthy: {Label(Healthy)} · Status: {(Healthy is true ? "OK" : Healthy is false ? "Error" : "Unknown")}";

    /// <summary>Builds read-only diagnostics from downloaded parameters and aggregate magnetometer health.</summary>
    /// <param name="parameters">Current parameter values, retaining missing entries as unknown.</param>
    /// <param name="magnetometerHealthy">Fresh aggregate health when known.</param>
    /// <returns>Configuration, detection, and health evidence for the compass subsystem.</returns>
    public static CompassDiagnostics Evaluate(IReadOnlyDictionary<string, float> parameters, bool? magnetometerHealthy)
    {
        float? Read(string name) => parameters.TryGetValue(name, out var value) && float.IsFinite(value) ? value : null;
        var evidence = new List<string>();
        var enabled = Read("COMPASS_ENABLE");
        var sources = Enumerable.Range(1, 3).Select(index => Read($"EK3_SRC{index}_YAW")).ToArray();
        bool? required = sources.Any(value => value is 1 or 3)
            ? true
            : sources.All(value => value is 0 or 2 or 6 or 8) ? false : null;
        evidence.Add($"COMPASS_ENABLE={enabled?.ToString() ?? "unknown"}");
        for (var index = 0; index < sources.Length; index++)
        {
            evidence.Add($"EK3_SRC{index + 1}_YAW={sources[index]?.ToString() ?? "unknown"}");
        }

        var uses = new List<bool?>();
        var detections = new List<bool?>();
        for (var slot = 1; slot <= 3; slot++)
        {
            var suffix = slot == 1 ? string.Empty : slot.ToString();
            var use = Read($"COMPASS_USE{suffix}");
            var raw = Read($"COMPASS_DEV_ID{suffix}");
            var external = Read(slot == 1 ? "COMPASS_EXTERNAL" : $"COMPASS_EXTERN{slot}");
            bool? configured = enabled == 0 || use == 0 ? false : enabled is null || use is null ? null : true;
            uint? id = raw is >= 0 and <= 16777215 && raw == MathF.Truncate(raw.Value) ? (uint)raw.Value : null;
            bool? detected = id is null ? null : id != 0;
            bool? slotRequired = required == false || configured == false ? false : required;
            uses.Add(configured);
            detections.Add(detected);
            var decoded = id is > 0 ? DecodeDeviceId(id.Value) : "no decoded device";
            evidence.Add($"Compass {slot}: Configured {Label(configured)} · Detected {Label(detected)} · Required {Label(slotRequired)} · " +
                $"Healthy {(configured == true && detected == false ? "No" : "unknown per instance")} · " +
                $"COMPASS_USE{suffix}={use?.ToString() ?? "unknown"} · COMPASS_DEV_ID{suffix}={raw?.ToString() ?? "unknown"} · " +
                $"{decoded} · External {Label(external is null ? null : external != 0)}");
        }
        bool? anyConfigured = uses.Any(value => value == true) ? true : uses.All(value => value == false) ? false : null;
        bool? anyDetected = detections.Any(value => value == true) ? true : detections.All(value => value == false) ? false : null;
        var configuredDevice = uses.Zip(detections).Any(pair => pair.First == true && pair.Second == true);
        bool? healthy = required == false && anyConfigured == false
            ? true
            : required == true && (anyConfigured == false || anyDetected == false)
                ? false
                : anyConfigured == true && (anyDetected == false || (!configuredDevice && detections.All(value => value is not null)))
                    ? false
                    : configuredDevice ? magnetometerHealthy : null;
        return new(anyConfigured, anyDetected, required, healthy, evidence);
    }

    /// <summary>Decodes ArduPilot's 24-bit bus type, bus number, address, and device-class type fields.</summary>
    public static string DecodeDeviceId(uint id)
    {
        var bus = (id & 7) switch
        {
            1 => "I2C",
            2 => "SPI",
            3 => "UAVCAN",
            4 => "SITL",
            5 => "MSP",
            6 => "Serial",
            7 => "WSPI",
            _ => "Unknown"
        };
        return $"Bus {bus} #{(id >> 3) & 31} · Address {(id >> 8) & 255} · Device type {(id >> 16) & 255}";
    }

    private static string Label(bool? value) => value is null ? "Unknown" : value.Value ? "Yes" : "No";
}
