using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Parameters;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup;

/// <summary>Discovers compass instances and applies guarded, readback-confirmed compass parameter edits.</summary>
public sealed class CompassConfigurationService : ICompassConfigurationService
{
    private const string OrientationParameter = "COMPASS_ORIENT";
    private const uint MagnetometerSensorBit = 0x04; // MAV_SYS_STATUS_SENSOR_3D_MAG.
    private const int MaximumCompassSlots = 8;
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly ILogger<CompassConfigurationService> logger;

    /// <summary>Initializes the compass-configuration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="logger">The logger.</param>
    public CompassConfigurationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        ILogger<CompassConfigurationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompassInventory> GetInventoryAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var values = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var orientationOptions = BuildOrientationOptions(metadata);
        var orientationLookup = orientationOptions.ToDictionary(option => option.Value, option => option.Name);
        var priorityOrder = ReadPriorityOrder(values);
        var motorCompensation = values.TryGetValue("COMPASS_MOTCT", out var motorType) && motorType.Value != 0;
        var magnetometerHealthy = ResolveMagnetometerHealth(state);

        var compasses = new List<CompassInstance>();
        for (var slot = 1; slot <= MaximumCompassSlots; slot++)
        {
            if (!values.TryGetValue(DeviceIdName(slot), out var deviceParameter))
            {
                continue;
            }

            var deviceId = ToDeviceId(deviceParameter.Value);
            if (deviceId == 0)
            {
                continue;
            }

            var use = ReadBool(values, UseName(slot), true);
            var external = ReadOptionalBool(values, ExternalName(slot));
            var orientation = ReadInt(values, OrientationName(slot), 0);
            var priority = priorityOrder.TryGetValue(deviceId, out var rank) ? rank : 0;
            compasses.Add(new CompassInstance(
                slot,
                deviceId,
                use,
                external,
                orientation,
                orientationLookup.TryGetValue(orientation, out var name) ? name : $"Orientation {orientation}",
                priority,
                motorCompensation,
                ReadDouble(values, OffsetName(slot, "X")),
                ReadDouble(values, OffsetName(slot, "Y")),
                ReadDouble(values, OffsetName(slot, "Z")),
                priority == 1 ? magnetometerHealthy : null));
        }

        var issues = DetectIssues(compasses, priorityOrder);
        return new CompassInventory(vehicleId, compasses, orientationOptions, issues);
    }

    /// <inheritdoc />
    public Task<CompassParameterApplyResult> SetOrientationAsync(VehicleId vehicleId, int index, int orientationValue, CancellationToken cancellationToken = default) =>
        ApplyAsync(vehicleId, OrientationName(index), orientationValue, $"orientation of compass {index}", cancellationToken);

    /// <inheritdoc />
    public Task<CompassParameterApplyResult> SetUseAsync(VehicleId vehicleId, int index, bool use, CancellationToken cancellationToken = default) =>
        ApplyAsync(vehicleId, UseName(index), use ? 1 : 0, $"use flag of compass {index}", cancellationToken);

    /// <inheritdoc />
    public Task<CompassParameterApplyResult> SetExternalAsync(VehicleId vehicleId, int index, bool external, CancellationToken cancellationToken = default) =>
        ApplyAsync(vehicleId, ExternalName(index), external ? 1 : 0, $"external flag of compass {index}", cancellationToken);

    /// <inheritdoc />
    public bool WouldDisableOnlyEnabledCompass(CompassInventory inventory, int index)
    {
        var target = inventory.Compasses.FirstOrDefault(compass => compass.Index == index);
        if (target is null || !target.Use)
        {
            return false;
        }

        return inventory.Compasses.Count(compass => compass.Use) <= 1;
    }

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        for (var slot = 1; slot <= MaximumCompassSlots; slot++)
        {
            foreach (var name in new[] { DeviceIdName(slot), UseName(slot), ExternalName(slot), OrientationName(slot) })
            {
                if (parameterRegistry.GetParameter(vehicleId, name) is not null)
                {
                    await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<CompassParameterApplyResult> ApplyAsync(VehicleId vehicleId, string parameterName, float value, string description, CancellationToken cancellationToken)
    {
        _ = RequireActiveVehicle(vehicleId);
        if (parameterRegistry.GetParameter(vehicleId, parameterName) is not { } parameter)
        {
            return new CompassParameterApplyResult(false, $"{parameterName} is not available on the connected vehicle.");
        }

        logger.LogInformation("Applying compass parameter {Parameter} on {VehicleId}.", parameterName, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, parameterName, value, parameter.Type, cancellationToken).ConfigureAwait(false))
        {
            return new CompassParameterApplyResult(true, $"Confirmed {description} by vehicle readback.");
        }

        return new CompassParameterApplyResult(false, $"Readback did not confirm the {description}. Reconnect, refresh, and verify before flying.");
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, float value, MavParamType type, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter &&
                parameter.Name == name && NearlyEqual(parameter.Value, value))
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, name, value, type, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, name) is { } current && NearlyEqual(current.Value, value))
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            await readback.Task.WaitAsync(readbackTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }

    private IReadOnlyList<CompassOrientationOption> BuildOrientationOptions(IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        if (metadata.TryGetValue(OrientationParameter, out var definition))
        {
            var options = definition.GetValueOptions();
            if (options.Count > 0)
            {
                return options
                    .OrderBy(option => option.Key)
                    .Select(option => new CompassOrientationOption((int)option.Key, option.Value))
                    .ToArray();
            }
        }

        return Enum.GetValues<MavSensorOrientation>()
            .Select(orientation => new CompassOrientationOption((int)orientation, HumanizeOrientation(orientation)))
            .ToArray();
    }

    private static IReadOnlyList<CompassConfigurationIssue> DetectIssues(IReadOnlyList<CompassInstance> compasses, IReadOnlyDictionary<uint, int> priorityOrder)
    {
        var issues = new List<CompassConfigurationIssue>();
        var duplicateDeviceIds = compasses
            .GroupBy(compass => compass.DeviceId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicate in duplicateDeviceIds)
        {
            var slots = string.Join(", ", compasses.Where(compass => compass.DeviceId == duplicate).Select(compass => compass.Index));
            issues.Add(new CompassConfigurationIssue(CompassIssueSeverity.Warning,
                $"Compasses in slots {slots} report the same device ID {duplicate}. Re-detect compasses to resolve the conflict."));
        }

        foreach (var compass in compasses.Where(compass => compass.Use && compass.Priority == 0))
        {
            issues.Add(new CompassConfigurationIssue(CompassIssueSeverity.Warning,
                $"Compass {compass.Index} is enabled but not present in the priority list. Its ordering is undefined until priorities are set."));
        }

        var presentDeviceIds = compasses.Select(compass => compass.DeviceId).ToHashSet();
        foreach (var stale in priorityOrder.Keys.Where(id => !presentDeviceIds.Contains(id)))
        {
            issues.Add(new CompassConfigurationIssue(CompassIssueSeverity.Warning,
                $"The priority list references device ID {stale}, which is not currently detected."));
        }

        return issues;
    }

    private static IReadOnlyDictionary<uint, int> ReadPriorityOrder(IReadOnlyDictionary<string, VehicleParameter> values)
    {
        var order = new Dictionary<uint, int>();
        for (var rank = 1; rank <= 3; rank++)
        {
            if (values.TryGetValue($"COMPASS_PRIO{rank}_ID", out var parameter))
            {
                var deviceId = ToDeviceId(parameter.Value);
                if (deviceId != 0)
                {
                    order.TryAdd(deviceId, rank);
                }
            }
        }

        return order;
    }

    private static bool? ResolveMagnetometerHealth(VehicleState state)
    {
        if (state.Health.SensorsPresent is not { } present || (present & MagnetometerSensorBit) == 0 || state.Health.SensorsHealthy is not { } healthy)
        {
            return null;
        }

        return (healthy & MagnetometerSensorBit) != 0;
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        return state;
    }

    private static string DeviceIdName(int slot) => slot == 1 ? "COMPASS_DEV_ID" : $"COMPASS_DEV_ID{slot}";

    private static string UseName(int slot) => slot == 1 ? "COMPASS_USE" : $"COMPASS_USE{slot}";

    private static string ExternalName(int slot) => slot == 1 ? "COMPASS_EXTERNAL" : $"COMPASS_EXTERN{slot}";

    private static string OrientationName(int slot) => slot == 1 ? "COMPASS_ORIENT" : $"COMPASS_ORIENT{slot}";

    private static string OffsetName(int slot, string axis) => slot == 1 ? $"COMPASS_OFS_{axis}" : $"COMPASS_OFS{slot}_{axis}";

    private static uint ToDeviceId(float value) => value <= 0 ? 0u : (uint)Math.Round(value);

    private static bool ReadBool(IReadOnlyDictionary<string, VehicleParameter> values, string name, bool fallback) =>
        values.TryGetValue(name, out var parameter) ? parameter.Value != 0 : fallback;

    private static bool? ReadOptionalBool(IReadOnlyDictionary<string, VehicleParameter> values, string name) =>
        values.TryGetValue(name, out var parameter) ? parameter.Value != 0 : null;

    private static int ReadInt(IReadOnlyDictionary<string, VehicleParameter> values, string name, int fallback) =>
        values.TryGetValue(name, out var parameter) ? (int)Math.Round(parameter.Value) : fallback;

    private static double ReadDouble(IReadOnlyDictionary<string, VehicleParameter> values, string name) =>
        values.TryGetValue(name, out var parameter) ? parameter.Value : 0d;

    private static string HumanizeOrientation(MavSensorOrientation orientation)
    {
        var name = orientation.ToString();
        const string prefix = "MavSensorRotation";
        if (name.StartsWith(prefix, StringComparison.Ordinal))
        {
            name = name[prefix.Length..];
        }

        return name switch
        {
            "None" => "None (0°)",
            _ => name
        };
    }

    private static bool NearlyEqual(float first, float second) => Math.Abs(first - second) <= 0.0001f;
}
