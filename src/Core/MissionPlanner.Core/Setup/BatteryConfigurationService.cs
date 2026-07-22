using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup;

/// <summary>Discovers battery monitor instances and applies guarded, readback-confirmed battery edits.</summary>
public sealed class BatteryConfigurationService : IBatteryConfigurationService
{
    private static readonly TimeSpan staleWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private const int MaximumInstances = 9;
    private static readonly IReadOnlyDictionary<BatterySetting, string> suffixes = new Dictionary<BatterySetting, string>
    {
        [BatterySetting.Monitor] = "MONITOR",
        [BatterySetting.Capacity] = "CAPACITY",
        [BatterySetting.LowVoltage] = "LOW_VOLT",
        [BatterySetting.CriticalVoltage] = "CRT_VOLT",
        [BatterySetting.LowCapacity] = "LOW_MAH",
        [BatterySetting.CriticalCapacity] = "CRT_MAH",
        [BatterySetting.VoltageMultiplier] = "VOLT_MULT",
        [BatterySetting.CurrentPerVolt] = "AMP_PERVLT",
        [BatterySetting.CurrentOffset] = "AMP_OFFSET",
        [BatterySetting.LowAction] = "FS_LOW_ACT",
        [BatterySetting.CriticalAction] = "FS_CRT_ACT"
    };
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<BatteryConfigurationService> logger;

    /// <summary>Initializes the battery-configuration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public BatteryConfigurationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        IDateTimeProvider clock,
        ILogger<BatteryConfigurationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.clock = clock;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatteryConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var values = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var monitorOptions = Options(metadata, "BATT_MONITOR");
        var monitorLookup = monitorOptions.ToDictionary(option => (int)option.Value, option => option.Name);

        var instances = new List<BatteryMonitorInstance>();
        for (var index = 1; index <= MaximumInstances; index++)
        {
            if (!values.TryGetValue(Name(index, BatterySetting.Monitor), out var monitorParameter))
            {
                continue;
            }

            var settings = new Dictionary<BatterySetting, double>();
            foreach (var setting in suffixes.Keys)
            {
                if (values.TryGetValue(Name(index, setting), out var parameter))
                {
                    settings[setting] = parameter.Value;
                }
            }

            var monitorType = (int)Math.Round(monitorParameter.Value);
            instances.Add(new BatteryMonitorInstance(
                index,
                monitorType,
                monitorLookup.TryGetValue(monitorType, out var name) ? name : $"Monitor {monitorType}",
                settings,
                ReadLive(state, index)));
        }

        var issues = instances.SelectMany(Validate).ToArray();
        return new BatteryConfiguration(
            vehicleId,
            instances,
            monitorOptions,
            Options(metadata, "BATT_FS_LOW_ACT"),
            Options(metadata, "BATT_FS_CRT_ACT"),
            issues);
    }

    /// <inheritdoc />
    public async Task<BatteryApplyResult> SetValueAsync(VehicleId vehicleId, int instance, BatterySetting setting, double value, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var name = Name(instance, setting);
        if (parameterRegistry.GetParameter(vehicleId, name) is not { } parameter)
        {
            return new BatteryApplyResult(false, $"{name} is not available on the connected vehicle.");
        }

        var values = parameterRegistry.GetAllParameters(vehicleId);
        if (Reject(setting, value, instance, values) is { } rejection)
        {
            return new BatteryApplyResult(false, rejection);
        }

        var reboot = (await GetMetadataAsync(vehicleId, name, cancellationToken).ConfigureAwait(false))?.RebootRequired ?? false;
        logger.LogInformation("Applying battery setting {Setting} on instance {Instance} for {VehicleId}.", setting, instance, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, name, (float)value, parameter.Type, cancellationToken).ConfigureAwait(false))
        {
            return new BatteryApplyResult(true, $"Confirmed {name} by vehicle readback.", reboot);
        }

        return new BatteryApplyResult(false, $"Readback did not confirm {name}. Reconnect, refresh, and verify before flying.");
    }

    /// <inheritdoc />
    public Task<BatteryApplyResult> CalibrateVoltageAsync(VehicleId vehicleId, int instance, double measuredVolts, double referenceVolts, CancellationToken cancellationToken = default) =>
        CalibrateAsync(vehicleId, instance, BatterySetting.VoltageMultiplier, measuredVolts, referenceVolts, "voltage", cancellationToken);

    /// <inheritdoc />
    public Task<BatteryApplyResult> CalibrateCurrentAsync(VehicleId vehicleId, int instance, double measuredAmps, double referenceAmps, CancellationToken cancellationToken = default) =>
        CalibrateAsync(vehicleId, instance, BatterySetting.CurrentPerVolt, measuredAmps, referenceAmps, "current", cancellationToken);

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        for (var index = 1; index <= MaximumInstances; index++)
        {
            foreach (var setting in suffixes.Keys)
            {
                var name = Name(index, setting);
                if (parameterRegistry.GetParameter(vehicleId, name) is not null)
                {
                    await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<BatteryApplyResult> CalibrateAsync(VehicleId vehicleId, int instance, BatterySetting setting, double measured, double reference, string quantity, CancellationToken cancellationToken)
    {
        _ = RequireActiveVehicle(vehicleId);
        if (measured <= 0 || reference <= 0)
        {
            return new BatteryApplyResult(false, $"Measured and reference {quantity} must both be greater than zero.");
        }

        var name = Name(instance, setting);
        if (parameterRegistry.GetParameter(vehicleId, name) is not { } parameter)
        {
            return new BatteryApplyResult(false, $"{name} is not available; this monitor cannot calibrate {quantity}.");
        }

        var corrected = parameter.Value * (reference / measured);
        if (corrected <= 0 || double.IsNaN(corrected) || double.IsInfinity(corrected))
        {
            return new BatteryApplyResult(false, $"The computed {quantity} scale was invalid and was not written.");
        }

        logger.LogInformation("Calibrating battery {Quantity} on instance {Instance} for {VehicleId}.", quantity, instance, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, name, (float)corrected, parameter.Type, cancellationToken).ConfigureAwait(false))
        {
            return new BatteryApplyResult(true, $"Confirmed {quantity} calibration: {name} set to {corrected:0.####}.");
        }

        return new BatteryApplyResult(false, $"Readback did not confirm the {quantity} calibration.");
    }

    private static string? Reject(BatterySetting setting, double value, int instance, IReadOnlyDictionary<string, VehicleParameter> values)
    {
        switch (setting)
        {
            case BatterySetting.Capacity or BatterySetting.VoltageMultiplier or BatterySetting.CurrentPerVolt when value <= 0:
                return $"{setting} must be greater than zero.";
            case BatterySetting.LowVoltage when value > 0 && ReadValue(values, instance, BatterySetting.CriticalVoltage) is { } critical && critical > 0 && value <= critical:
                return $"Low voltage ({value:0.##} V) must be above the critical voltage ({critical:0.##} V).";
            case BatterySetting.CriticalVoltage when value > 0 && ReadValue(values, instance, BatterySetting.LowVoltage) is { } low && low > 0 && value >= low:
                return $"Critical voltage ({value:0.##} V) must be below the low voltage ({low:0.##} V).";
            case BatterySetting.LowCapacity when value > 0 && ReadValue(values, instance, BatterySetting.CriticalCapacity) is { } critical && critical > 0 && value <= critical:
                return $"Low capacity ({value:0} mAh) must be above the critical capacity ({critical:0} mAh).";
            case BatterySetting.CriticalCapacity when value > 0 && ReadValue(values, instance, BatterySetting.LowCapacity) is { } low && low > 0 && value >= low:
                return $"Critical capacity ({value:0} mAh) must be below the low capacity ({low:0} mAh).";
            default:
                return null;
        }
    }

    private static IReadOnlyList<BatteryValidationIssue> Validate(BatteryMonitorInstance instance)
    {
        var issues = new List<BatteryValidationIssue>();
        if (instance.Get(BatterySetting.LowVoltage) is { } low && instance.Get(BatterySetting.CriticalVoltage) is { } critical &&
            low > 0 && critical > 0 && low <= critical)
        {
            issues.Add(new BatteryValidationIssue(BatteryIssueSeverity.Blocking,
                $"Battery {instance.Index}: low voltage ({low:0.##} V) must be above the critical voltage ({critical:0.##} V)."));
        }

        if (instance.Get(BatterySetting.LowCapacity) is { } lowMah && instance.Get(BatterySetting.CriticalCapacity) is { } critMah &&
            lowMah > 0 && critMah > 0 && lowMah <= critMah)
        {
            issues.Add(new BatteryValidationIssue(BatteryIssueSeverity.Blocking,
                $"Battery {instance.Index}: low capacity ({lowMah:0} mAh) must be above the critical capacity ({critMah:0} mAh)."));
        }

        if (instance.MonitorType != 0 && instance.Get(BatterySetting.Capacity) is { } capacity && capacity <= 0)
        {
            issues.Add(new BatteryValidationIssue(BatteryIssueSeverity.Warning,
                $"Battery {instance.Index}: capacity is not configured, so capacity-based failsafes cannot work."));
        }

        return issues;
    }

    private BatteryLiveReading ReadLive(VehicleState state, int index)
    {
        var now = clock.UtcNow;
        if (index == 1)
        {
            var power = state.Power;
            return new BatteryLiveReading(power.BatteryVoltageVolts, power.BatteryCurrentAmps, power.BatteryConsumedMah,
                power.BatteryRemainingPercent, power.IsStale(now, staleWindow), power.ObservedAt is not null);
        }

        if (index == 2 && state.Power.SecondaryBattery is { } secondary)
        {
            return new BatteryLiveReading(secondary.VoltageVolts, secondary.CurrentAmps, secondary.ConsumedMah,
                secondary.RemainingPercent, secondary.IsStale(now, staleWindow), true);
        }

        return new BatteryLiveReading(null, null, null, null, true, false);
    }

    private static double? ReadValue(IReadOnlyDictionary<string, VehicleParameter> values, int instance, BatterySetting setting) =>
        values.TryGetValue(Name(instance, setting), out var parameter) ? parameter.Value : null;

    private static IReadOnlyList<BatterySettingOption> Options(IReadOnlyDictionary<string, ParameterMetadata> metadata, string name) =>
        metadata.TryGetValue(name, out var definition)
            ? definition.GetValueOptions().OrderBy(option => option.Key).Select(option => new BatterySettingOption(option.Key, option.Value)).ToArray()
            : [];

    private async Task<ParameterMetadata?> GetMetadataAsync(VehicleId vehicleId, string name, CancellationToken cancellationToken)
    {
        try
        {
            return await metadataService.GetMetadataAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Could not resolve metadata for {Parameter}.", name);
            return null;
        }
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        return state;
    }

    private static string Name(int instance, BatterySetting setting) => $"{(instance == 1 ? "BATT" : $"BATT{instance}")}_{suffixes[setting]}";

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, float value, MavParamType type, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == name && NearlyEqual(parameter.Value, value))
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

    private static bool NearlyEqual(float first, float second) => Math.Abs(first - second) <= 0.0005f;
}
