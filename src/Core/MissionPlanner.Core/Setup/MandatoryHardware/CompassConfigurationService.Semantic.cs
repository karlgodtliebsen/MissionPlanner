using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

public sealed partial class CompassConfigurationService
{
    private static readonly (CompassSetting Key, string Name, string Label, string Description)[] settings =
    [
        (CompassSetting.Enabled, "COMPASS_ENABLE", "Enable compass", "Controls whether ArduPilot uses magnetic compass sensors."),
        (CompassSetting.PrimaryUse, "COMPASS_USE", "Use primary compass for navigation", "Allow the primary compass to contribute magnetic heading."),
        (CompassSetting.SecondaryUse, "COMPASS_USE2", "Use secondary compass for navigation", "Allow the secondary compass to contribute magnetic heading."),
        (CompassSetting.TertiaryUse, "COMPASS_USE3", "Use tertiary compass for navigation", "Allow the tertiary compass to contribute magnetic heading."),
        (CompassSetting.YawSource, "EK3_SRC1_YAW", "Yaw source", "Select the first EKF source set's heading reference. Other source sets are checked separately."),
        (CompassSetting.PrimaryOrientation, "COMPASS_ORIENT", "Primary orientation", "Physical mounting rotation of the primary compass."),
        (CompassSetting.SecondaryOrientation, "COMPASS_ORIENT2", "Secondary orientation", "Physical mounting rotation of the secondary compass."),
        (CompassSetting.TertiaryOrientation, "COMPASS_ORIENT3", "Tertiary orientation", "Physical mounting rotation of the tertiary compass."),
        (CompassSetting.PrimaryExternal, "COMPASS_EXTERNAL", "Primary compass placement", "Identify a compass mounted outside the flight controller."),
        (CompassSetting.SecondaryExternal, "COMPASS_EXTERN2", "Secondary compass placement", "Identify a compass mounted outside the flight controller."),
        (CompassSetting.TertiaryExternal, "COMPASS_EXTERN3", "Tertiary compass placement", "Identify a compass mounted outside the flight controller.")
    ];

    /// <inheritdoc />
    public async Task<CompassSetupState> ReadAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = RequireActiveVehicle(vehicleId);
        var connection = activeVehicle.ConnectionCancellationToken;
        var identity = vehicle.Identity.Firmware;
        var supported = identity.Autopilot == 3 && identity.MavType != 6 && identity.Family != MissionPlanner.Firmware.FirmwareFamily.Unknown;
        IReadOnlyDictionary<string, ParameterMetadata> metadata = new Dictionary<string, ParameterMetadata>();
        string? metadataError = null;
        if (supported)
        {
            try
            {
                metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or HttpRequestException)
            {
                metadataError = $"Metadata unavailable: {exception.Message}";
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        connection.ThrowIfCancellationRequested();
        vehicle = RequireActiveVehicle(vehicleId);
        var values = parameterRegistry.GetAllParameters(vehicleId);
        if (identity != vehicle.Identity.Firmware || connection != activeVehicle.ConnectionCancellationToken)
        {
            throw new InvalidOperationException("Vehicle identity changed while reading compass configuration. Reload before editing.");
        }
        var configuration = new CompassConfiguration();
        var definitions = new List<CompassSettingDefinition>();
        foreach (var setting in settings)
        {
            var current = values.TryGetValue(setting.Name, out var parameter) && float.IsFinite(parameter.Value) ? (double?)parameter.Value : null;
            configuration = configuration.With(setting.Key, current);
            metadata.TryGetValue(setting.Name, out var description);
            var choices = Choices(setting.Key, description);
            var reason = !supported ? "Requires supported ArduPilot firmware." : current is null ? "Parameter not reported by this firmware (or not yet loaded)." :
                description?.ReadOnly == true ? "Firmware marks this setting read-only." : choices.Count == 0 ? "Choice metadata unavailable." : null;
            definitions.Add(new(setting.Key, setting.Label, setting.Description, setting.Name, current,
                description?.DefaultValue, choices, reason is null, description?.RebootRequired == true, reason));
        }
        var devices = new List<string>();
        var evidence = new List<string>();
        if (metadataError is not null)
        {
            evidence.Add(metadataError);
        }
        for (var slot = 1; slot <= 3; slot++)
        {
            var name = DeviceIdName(slot);
            var value = values.TryGetValue(name, out var id) ? id.Value : (float?)null;
            var label = value is > 0 ? $"Compass {slot}: Device ID {value:0} · {CompassDiagnostics.DecodeDeviceId(ToDeviceId(value.Value))}" :
                $"Compass {slot}: {(value == 0 ? "Not detected" : "Detection unknown")}";
            devices.Add(label);
            evidence.Add($"{name} = {value?.ToString() ?? "Unavailable"}");
        }
        var now = clock.GetUtcNow();
        var health = vehicle.Health.IsSystemHealthStale(now, TimeSpan.FromSeconds(5)) || (vehicle.Health.SensorsEnabled & MagnetometerSensorBit) != MagnetometerSensorBit ? null : ResolveMagnetometerHealth(vehicle);
        var detected = values.TryGetValue(DeviceIdName(1), out var primary) ? primary.Value != 0 : (bool?)null;
        var healthText = configuration.Enabled == false ? "Disabled" : configuration.Enabled is null ? "Unknown" :
            detected == false ? "Not detected" : health == true ? "Healthy" : health == false ? "Unhealthy" : "Unknown";
        var errors = Validate(configuration, values);
        var validation = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : configuration.Enabled == false
            ? "Compass is disabled. The reported EKF yaw configuration does not require a compass." :
            detected == false ? "Compass is enabled, but no primary compass device is detected." :
            configuration.Enabled is null ? "Compass capability is unavailable or parameters are still loading." : "Compass configuration is valid.";
        var arming = diagnostics.GetArming(vehicleId);
        var reasons = arming.Reasons.Where(reason => reason.Contains("compass", StringComparison.OrdinalIgnoreCase) || reason.Contains("magnetometer", StringComparison.OrdinalIgnoreCase)).ToArray();
        var impact = reasons.Length > 0 ? "Current compass pre-arm issue: " + string.Join("; ", reasons) :
            arming.IsReadyToArm == true || arming.IsArmed ? "No current compass arming issue" : "Arming impact unknown — no current compass failure reported.";
        evidence.AddRange(definitions.Select(d => $"{d.ParameterName} = {d.Current?.ToString() ?? "Unavailable"} · live parameter registry · metadata {(metadata.ContainsKey(d.ParameterName) ? "available" : "unavailable")}"));
        foreach (var name in new[] { "EK3_SRC2_YAW", "EK3_SRC3_YAW" })
        {
            evidence.Add($"{name} = {(values.TryGetValue(name, out var source) ? source.Value.ToString() : "Not reported")}");
        }
        evidence.Add($"Health source: SYS_STATUS magnetometer bit · observed {vehicle.Health.SystemObservedAt:O}");
        return new(vehicleId, configuration, definitions, devices, healthText, validation, impact, supported,
            supported ? null : "Compass configuration requires an ArduPilot vehicle.", now, evidence);
    }

    /// <inheritdoc />
    public async Task<CompassChangeSet> EvaluateChangesAsync(VehicleId vehicleId, CompassConfiguration desired, CancellationToken cancellationToken = default)
    {
        var connection = activeVehicle.ConnectionCancellationToken;
        var state = await ReadAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var errors = new List<string>();
        var warnings = new List<string>();
        var changes = new List<CompassParameterChange>();
        var values = parameterRegistry.GetAllParameters(vehicleId);
        var normalized = desired;
        if (desired.Enabled == false && state.Current.Enabled != false)
        {
            foreach (var key in new[] { CompassSetting.PrimaryUse, CompassSetting.SecondaryUse, CompassSetting.TertiaryUse })
            {
                if (state.Current.Get(key) is not null)
                {
                    normalized = normalized.With(key, 0);
                }
            }
            if (RequiresCompass(normalized.YawSource))
            {
                normalized = normalized with
                {
                    YawSource = CompassYawSource.None
                };
            }
        }
        if (!state.IsSupported)
        {
            errors.Add(state.UnsupportedReason!);
        }
        errors.AddRange(Validate(normalized, values));
        foreach (var definition in state.Settings)
        {
            var pending = normalized.Get(definition.Setting);
            if (pending == definition.Current)
            {
                continue;
            }
            if (!definition.CanEdit || pending is null || !definition.Choices.Any(choice => choice.Value == pending))
            {
                errors.Add($"{definition.Label}: {definition.UnavailableReason ?? "Select a supported value."}");
                continue;
            }
            changes.Add(new(definition.ParameterName, definition.Current!.Value, pending.Value, definition.RequiresReboot,
                desired.Get(definition.Setting) != pending ? $"Required dependency when disabling compass: {definition.Label}" : definition.Label));
        }
        if (normalized.Enabled == true && !values.Any(pair => pair.Key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) && pair.Value.Value > 0))
        {
            warnings.Add("Compass enabled without a detected device; this does not establish healthy hardware.");
        }
        // Remove yaw dependency before disabling usage/subsystem. Enable subsystem before dependent usage.
        changes = changes.OrderBy(change => normalized.Enabled == false
            ? change.Name == "EK3_SRC1_YAW" ? 0 : change.Name == "COMPASS_ENABLE" ? 2 : 1
            : change.Name == "COMPASS_ENABLE" ? 0 : change.Name == "EK3_SRC1_YAW" ? 2 : 1).ToList();
        return new(new(vehicleId, RequireActiveVehicle(vehicleId).Identity.Firmware), connection,
            state.Current, normalized, changes, errors, warnings);
    }

    /// <inheritdoc />
    public async Task<CompassApplyResult> ApplyAsync(VehicleId vehicleId, CompassChangeSet changes, CancellationToken cancellationToken = default)
    {
        if (!changes.CanApply || changes.Scope.VehicleId != vehicleId)
        {
            return new(false, null, false, [], "No valid reviewed changes to apply.");
        }
        void Guard()
        {
            cancellationToken.ThrowIfCancellationRequested();
            changes.Connection.ThrowIfCancellationRequested();
            var vehicle = RequireActiveVehicle(vehicleId);
            if (vehicle.IsArmed || vehicle.Identity.Firmware != changes.Scope.FirmwareIdentity || activeVehicle.ConnectionCancellationToken != changes.Connection)
            {
                throw new InvalidOperationException("Vehicle, connection or safety state changed. Reload and review before applying.");
            }
        }
        Guard();
        var current = await EvaluateChangesAsync(vehicleId, changes.Desired, cancellationToken).ConfigureAwait(false);
        if (current.Original != changes.Original || !current.Changes.Select(c => (c.Name, c.OldValue, c.NewValue, c.RequiresReboot)).SequenceEqual(changes.Changes.Select(c => (c.Name, c.OldValue, c.NewValue, c.RequiresReboot))) || current.Errors.Count > 0)
        {
            return new(false, await ReadAsync(vehicleId, cancellationToken).ConfigureAwait(false), false, [], "Conflict: live values or capabilities changed since review. Discard or review again.");
        }
        if (!operationGate.TryAcquire(vehicleId, "Compass configuration", out var lease))
        {
            return new(false, null, false, [], "Another vehicle operation is active. Finish it before applying compass changes.");
        }
        using (lease)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, changes.Connection))
        using (var session = factory.Create<IParameterEditSession, ParameterEditScope>(changes.Scope))
        {
            await session.LoadAsync(changes.Changes.Select(c => c.Name).ToArray(), linked.Token).ConfigureAwait(false);
            var confirmed = new List<string>();
            var reboot = false;
            var message = "Changes applied and verified.";
            try
            {
                foreach (var change in changes.Changes)
                {
                    Guard();
                    var field = session.GetField(change.Name);
                    if (field is null || field.LiveValue != change.OldValue || !session.TrySetPending(change.Name, change.NewValue, out _))
                    {
                        message = $"Stopped before {change.Name}: live value or validation changed. {confirmed.Count} changes confirmed.";
                        break;
                    }
                    var result = await session.ApplyAsync(session.CreateWritePlan([change.Name]), cancellationToken: linked.Token).ConfigureAwait(false);
                    reboot |= result.RebootRequired;
                    if (!result.Success)
                    {
                        message = $"Stopped at {change.Name}: write/readback failed. {confirmed.Count} changes confirmed; refresh and review the remaining changes.";
                        break;
                    }
                    confirmed.Add(change.Name);
                }
                var actual = await ReadAsync(vehicleId, linked.Token).ConfigureAwait(false);
                var success = confirmed.Count == changes.Changes.Count && actual.Current == changes.Desired;
                return new(success, actual, reboot, confirmed, success ? message : message + " Actual values remain authoritative; pending changes were retained.");
            }
            catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or IOException)
            {
                return new(false, null, reboot, confirmed,
                    $"Apply interrupted after {confirmed.Count} confirmed changes: {exception.Message}. Reconnect, refresh and review remaining values.");
            }
        }
    }

    private static bool RequiresCompass(CompassYawSource? source)
    {
        return source is CompassYawSource.Compass or CompassYawSource.GpsWithCompassFallback;
    }

    private static List<string> Validate(CompassConfiguration desired, IReadOnlyDictionary<string, VehicleParameter> values)
    {
        var errors = new List<string>();
        if (desired.Enabled == false)
        {
            if (RequiresCompass(desired.YawSource))
            {
                errors.Add("Compass is disabled, but the selected EKF yaw source requires a compass.");
            }
            foreach (var name in new[] { "EK3_SRC2_YAW", "EK3_SRC3_YAW" })
            {
                if (values.TryGetValue(name, out var source))
                {
                    if (!float.IsFinite(source.Value) || source.Value != MathF.Truncate(source.Value) || !Enum.IsDefined((CompassYawSource)(int)source.Value))
                    {
                        errors.Add($"{name} has an unknown yaw dependency. Review that alternate source set before disabling compass.");
                    }
                    else if (RequiresCompass((CompassYawSource)(int)source.Value))
                    {
                        errors.Add($"{name} still requires a compass. Review that alternate EKF source set in Parameters Editor before disabling compass.");
                    }
                }
            }
            if (desired.YawSource is null || !Enum.IsDefined(desired.YawSource.Value))
            {
                errors.Add("Yaw dependency is unknown. Load supported yaw-source parameters before disabling compass.");
            }
        }
        if (RequiresCompass(desired.YawSource) && desired.Enabled != false && desired.UsePrimary != true && desired.UseSecondary != true && desired.UseTertiary != true)
        {
            errors.Add("The selected yaw source requires at least one compass enabled for navigation.");
        }
        return errors;
    }

    private static IReadOnlyList<CompassChoice> Choices(CompassSetting key, ParameterMetadata? metadata)
    {
        var choices = metadata?.GetValueOptions();
        if (choices is { Count: > 0 })
        {
            return choices.OrderBy(pair => pair.Key).Select(pair => new CompassChoice(pair.Key, pair.Value)).ToArray();
        }
        if (key is CompassSetting.Enabled or CompassSetting.PrimaryUse or CompassSetting.SecondaryUse or CompassSetting.TertiaryUse)
        {
            return [new(0, "Off"), new(1, "On")];
        }
        // Unknown enum capability is not expanded from guessed firmware defaults.
        return [];
    }
}
