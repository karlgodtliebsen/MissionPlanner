using System.Text.Json;
using System.Text.Json.Serialization;

namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Generates the machine-readable MAVLink domain-promotion and workflow-ownership catalog.
/// </summary>
public static class MavLinkPromotionCatalogGenerator
{
    private static readonly IReadOnlyDictionary<string, PromotionOverride> Overrides =
        new Dictionary<string, PromotionOverride>(StringComparer.Ordinal)
        {
            ["HEARTBEAT"] = State("FlightTelemetryHandler", "VehicleHeartbeatObservation", "1 Hz", "PT3S", "Vehicle selector", "Top bar", "HUD"),
            ["SYSTEM_TIME"] = State("SensorTelemetryHandler", "VehicleTimeObservation", "1 Hz", "PT5S", "Time diagnostics"),
            ["SYS_STATUS"] = State("PowerTelemetryHandler", "VehicleSystemHealthObservation", "1 Hz", "PT5S", "HUD", "Power status", "Health status"),
            ["PARAM_VALUE"] = Workflow("ControlMessageHandler / VehicleParameterStreamService", "request/response", "Full Parameter List"),
            ["GPS_RAW_INT"] = State("NavigationTelemetryHandler", "VehicleGpsObservation", "1-10 Hz", "PT3S", "HUD", "GPS status"),
            ["ATTITUDE"] = State("FlightTelemetryHandler", "VehicleAttitudeObservation", "10-100 Hz", "PT1S", "HUD"),
            ["ATTITUDE_QUATERNION"] = State("FlightTelemetryHandler", "VehicleAttitudeObservation", "10-100 Hz", "PT1S", "HUD"),
            ["SCALED_PRESSURE"] = State("SensorTelemetryHandler", "VehiclePressureObservation", "1-20 Hz", "PT3S", "Sensor status"),
            ["DISTANCE_SENSOR"] = State("SensorTelemetryHandler", "VehicleRangeObservation", "1-20 Hz", "PT2S", "Proximity status"),
            ["TERRAIN_REPORT"] = State("SensorTelemetryHandler", "VehicleTerrainObservation", "1-5 Hz", "PT5S", "Terrain status", "HUD"),
            ["SCALED_PRESSURE2"] = State("SensorTelemetryHandler", "VehiclePressureObservation", "1-20 Hz", "PT3S", "Sensor status"),
            ["ALTITUDE"] = State("SensorTelemetryHandler", "VehicleAltitudeObservation", "1-20 Hz", "PT2S", "HUD", "Altitude diagnostics"),
            ["SCALED_PRESSURE3"] = State("SensorTelemetryHandler", "VehiclePressureObservation", "1-20 Hz", "PT3S", "Sensor status"),
            ["LOCAL_POSITION_NED"] = State("NavigationTelemetryHandler", "VehicleLocalPositionObservation", "1-20 Hz", "PT2S", "HUD", "Map"),
            ["GLOBAL_POSITION_INT"] = State("NavigationTelemetryHandler", "VehicleGlobalPositionObservation", "1-20 Hz", "PT2S", "HUD", "Map"),
            ["MISSION_CURRENT"] = State("NavigationTelemetryHandler", "VehicleMissionProgressObservation", "event-driven", null, "Mission progress", "HUD"),
            ["NAV_CONTROLLER_OUTPUT"] = State("NavigationTelemetryHandler", "VehicleNavigationObservation", "1-10 Hz", "PT3S", "HUD", "Mission guidance"),
            ["RC_CHANNELS"] = State("RadioTelemetryHandler", "VehicleRadioObservation", "1-20 Hz", "PT3S", "Radio status"),
            ["RADIO_STATUS"] = State("RadioTelemetryHandler", "VehicleRadioLinkObservation", "1-10 Hz", "PT3S", "Radio status"),
            ["VFR_HUD"] = State("FlightTelemetryHandler", "VehicleHudObservation", "1-10 Hz", "PT2S", "HUD"),
            ["COMMAND_ACK"] = Workflow("ControlMessageHandler / CommandAckTracker", "event-driven", "Command status"),
            ["POWER_STATUS"] = State("PowerTelemetryHandler", "VehiclePowerRailObservation", "1 Hz", "PT5S", "Power status"),
            ["BATTERY_STATUS"] = State("PowerTelemetryHandler", "VehicleBatteryObservation", "1 Hz", "PT5S", "HUD", "Power status"),
            ["GPS2_RAW"] = State("NavigationTelemetryHandler", "VehicleGpsObservation", "1-10 Hz", "PT3S", "GPS status"),
            ["RADIO"] = State("RadioTelemetryHandler", "VehicleRadioLinkObservation", "1-10 Hz", "PT3S", "Radio status"),
            ["BATTERY2"] = State("PowerTelemetryHandler", "VehicleBatteryObservation", "1 Hz", "PT5S", "Power status"),
            ["SERVO_OUTPUT_RAW"] = State("RadioTelemetryHandler", "VehicleServoOutputObservation", "1-20 Hz", "PT3S", "Servo status"),
            ["AUTOPILOT_VERSION"] = State("FlightTelemetryHandler", "VehicleFirmwareObservation", "on connect/on request", null, "Vehicle identity", "Firmware display"),
            ["AHRS2"] = State("FlightTelemetryHandler", "VehicleAhrsObservation", "1-20 Hz", "PT2S", "HUD", "Map"),
            ["AHRS"] = State("HealthTelemetryHandler", "VehicleEstimatorDiagnosticObservation", "1-20 Hz", "PT3S", "Estimator status"),
            ["WIND"] = State("SensorTelemetryHandler", "VehicleWindObservation", "1-10 Hz", "PT3S", "HUD", "Environment status"),
            ["AHRS3"] = State("HealthTelemetryHandler", "VehicleEstimatorPoseObservation", "1-20 Hz", "PT3S", "Estimator status"),
            ["EKF_STATUS_REPORT"] = State("HealthTelemetryHandler", "VehicleEkfObservation", "1-10 Hz", "PT3S", "Health status"),
            ["HOME_POSITION"] = State("NavigationTelemetryHandler", "VehicleHomePositionObservation", "event-driven", null, "Map", "Mission planning"),
            ["WIND_COV"] = State("SensorTelemetryHandler", "VehicleWindObservation", "1-10 Hz", "PT3S", "Environment status"),
            ["VIBRATION"] = State("SensorTelemetryHandler", "VehicleVibrationObservation", "1-10 Hz", "PT3S", "Sensor status"),
            ["EXTENDED_SYS_STATE"] = State("FlightTelemetryHandler", "VehicleExtendedFlightStateObservation", "1-5 Hz", "PT3S", "HUD", "Landed state"),
            ["STATUSTEXT"] = State("ControlMessageHandler / StatusTextHandler", "VehicleStatusText", "event-driven", null, "Messages", "Notifications"),
            ["MISSION_ITEM_REACHED"] = new(
                MavLinkPromotionCategory.DomainEvent,
                "MissionTransferService",
                null,
                "event-driven",
                null,
                ["Mission progress"])
        };

    /// <summary>
    /// Generates deterministic indented JSON for every selected-dialect message.
    /// </summary>
    /// <param name="definitions">The selected-dialect message definitions.</param>
    /// <param name="sourceRevision">The pinned MAVLink revision.</param>
    /// <returns>The machine-readable catalog JSON.</returns>
    public static string Generate(IReadOnlyCollection<DialectMessageDefinition> definitions, string sourceRevision)
    {
        var entries = definitions.OrderBy(definition => definition.MessageId)
            .Select(CreateEntry)
            .ToArray();
        var document = new MavLinkPromotionCatalogDocument(
            sourceRevision,
            "ardupilotmega.xml",
            "Diagnostic/raw is the default. Promote only durable vehicle state, meaningful domain transitions, or dedicated protocol workflows with a named owner.",
            entries);
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(document, options) + Environment.NewLine;
    }

    private static MavLinkPromotionCatalogEntry CreateEntry(DialectMessageDefinition definition)
    {
        var policy = Overrides.TryGetValue(definition.Name, out var configured)
            ? configured
            : Classify(definition.Name);
        return new MavLinkPromotionCatalogEntry(
            definition.MessageId,
            definition.Name,
            policy.Category,
            policy.Owner,
            policy.ObservationType,
            policy.Frequency,
            policy.StaleTimeout,
            policy.UiConsumers);
    }

    private static PromotionOverride Classify(string name)
    {
        if (IsProtocolWorkflow(name))
        {
            return Workflow(GetWorkflowOwner(name), "request/response", "Protocol diagnostics");
        }

        if (name.StartsWith("SET_", StringComparison.Ordinal)
            || name.StartsWith("REQUEST_", StringComparison.Ordinal)
            || name is "MANUAL_CONTROL" or "RC_CHANNELS_OVERRIDE" or "HIL_ACTUATOR_CONTROLS")
        {
            return new PromotionOverride(
                MavLinkPromotionCategory.OutboundOnly,
                "Outbound command/setpoint API",
                null,
                "caller-defined",
                null,
                ["Control workflows"]);
        }

        return new PromotionOverride(
            MavLinkPromotionCategory.DiagnosticRawTelemetry,
            "MAVLink inspector / telemetry log",
            null,
            "source-defined",
            null,
            ["MAVLink inspector", "Telemetry logs"]);
    }

    private static bool IsProtocolWorkflow(string name) =>
        name.StartsWith("MISSION_", StringComparison.Ordinal)
        || name.StartsWith("PARAM_", StringComparison.Ordinal)
        || name.StartsWith("COMMAND_", StringComparison.Ordinal)
        || name.StartsWith("LOG_", StringComparison.Ordinal)
        || name.StartsWith("CAMERA_", StringComparison.Ordinal)
        || name.StartsWith("GIMBAL_", StringComparison.Ordinal)
        || name.StartsWith("MOUNT_", StringComparison.Ordinal)
        || name.StartsWith("DEVICE_OP_", StringComparison.Ordinal)
        || name.StartsWith("OPEN_DRONE_ID_", StringComparison.Ordinal)
        || name.StartsWith("UAVIONIX_ADSB_", StringComparison.Ordinal)
        || name.StartsWith("REMOTE_LOG_", StringComparison.Ordinal)
        || name.StartsWith("VIDEO_STREAM_", StringComparison.Ordinal)
        || name is "FILE_TRANSFER_PROTOCOL" or "SERIAL_CONTROL" or "TUNNEL" or "SECURE_COMMAND" or "SECURE_COMMAND_REPLY";

    private static string GetWorkflowOwner(string name)
    {
        if (name.StartsWith("MISSION_", StringComparison.Ordinal)) return "MissionTransferService";
        if (name.StartsWith("PARAM_", StringComparison.Ordinal)) return "VehicleParameterStreamService";
        if (name.StartsWith("COMMAND_", StringComparison.Ordinal)) return "MavLinkCommandService / CommandAckTracker";
        if (name == "FILE_TRANSFER_PROTOCOL") return "MavFtpClient";
        if (name.StartsWith("LOG_", StringComparison.Ordinal) || name.StartsWith("REMOTE_LOG_", StringComparison.Ordinal)) return "Log transfer service (planned)";
        if (name.StartsWith("CAMERA_", StringComparison.Ordinal) || name.StartsWith("VIDEO_STREAM_", StringComparison.Ordinal)) return "Camera protocol service (planned)";
        if (name.StartsWith("GIMBAL_", StringComparison.Ordinal) || name.StartsWith("MOUNT_", StringComparison.Ordinal)) return "Gimbal protocol service (planned)";
        return "Dedicated protocol service / inspector";
    }

    private static PromotionOverride State(
        string owner,
        string observation,
        string frequency,
        string? staleTimeout,
        params string[] consumers) =>
        new(MavLinkPromotionCategory.VehicleStateTelemetry, owner, observation, frequency, staleTimeout, consumers);

    private static PromotionOverride Workflow(string owner, string frequency, params string[] consumers) =>
        new(MavLinkPromotionCategory.ProtocolWorkflow, owner, null, frequency, null, consumers);

    private sealed record PromotionOverride(
        MavLinkPromotionCategory Category,
        string Owner,
        string? ObservationType,
        string Frequency,
        string? StaleTimeout,
        IReadOnlyList<string> UiConsumers);
}

/// <summary>
/// Defines the domain-promotion category for a MAVLink wire message.
/// </summary>
public enum MavLinkPromotionCategory
{
    /// <summary>The message updates durable or current vehicle state.</summary>
    VehicleStateTelemetry,
    /// <summary>The message represents a meaningful domain transition.</summary>
    DomainEvent,
    /// <summary>The message belongs to a dedicated protocol service or state machine.</summary>
    ProtocolWorkflow,
    /// <summary>The message remains typed/raw diagnostic telemetry without aggregate state.</summary>
    DiagnosticRawTelemetry,
    /// <summary>The message is primarily an outbound request, command, or setpoint.</summary>
    OutboundOnly
}

/// <summary>
/// Contains the complete machine-readable promotion catalog.
/// </summary>
/// <param name="SourceRevision">The pinned MAVLink source revision.</param>
/// <param name="RootDialect">The selected root dialect.</param>
/// <param name="Policy">The promotion policy summary.</param>
/// <param name="Messages">The catalog entries in message-ID order.</param>
public sealed record MavLinkPromotionCatalogDocument(
    string SourceRevision,
    string RootDialect,
    string Policy,
    IReadOnlyList<MavLinkPromotionCatalogEntry> Messages);

/// <summary>
/// Describes domain promotion and ownership for one selected-dialect message.
/// </summary>
/// <param name="MessageId">The numeric message ID.</param>
/// <param name="Name">The MAVLink message name.</param>
/// <param name="Category">The promotion category.</param>
/// <param name="Owner">The single owning handler, service, or diagnostic surface.</param>
/// <param name="ObservationType">The promoted observation or domain-model type, if any.</param>
/// <param name="IntendedUpdateFrequency">The expected update pattern.</param>
/// <param name="StaleTimeout">The ISO-8601 stale timeout, if current state can become stale.</param>
/// <param name="UiConsumers">The intended UI consumers.</param>
public sealed record MavLinkPromotionCatalogEntry(
    uint MessageId,
    string Name,
    MavLinkPromotionCategory Category,
    string Owner,
    string? ObservationType,
    string IntendedUpdateFrequency,
    string? StaleTimeout,
    IReadOnlyList<string> UiConsumers);
