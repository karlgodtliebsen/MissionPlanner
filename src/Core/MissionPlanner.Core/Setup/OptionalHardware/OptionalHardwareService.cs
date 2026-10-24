using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Discovers available optional-hardware modules and applies guarded, readback-confirmed edits.</summary>
public sealed class OptionalHardwareService : IOptionalHardwareService
{
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareCatalog catalog;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly ILogger<OptionalHardwareService> logger;

    /// <summary>Initializes the optional-hardware service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="catalog">The optional-hardware module catalog.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="logger">The logger.</param>
    public OptionalHardwareService(
        IActiveVehicleContext activeVehicle,
        IOptionalHardwareCatalog catalog,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        ILogger<OptionalHardwareService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.catalog = catalog;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OptionalHardwareModuleView>> GetModulesAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        return catalog.GetAvailable(parameters).Where(module => module.Key != "antenna-tracker" || state.Identity.Firmware.Family == MissionPlanner.Firmware.FirmwareFamily.AntennaTracker)
            .Select(module => module.Build(parameters, metadata)).ToArray();
    }

    /// <inheritdoc />
    public async Task<OptionalHardwareApplyResult> SetValueAsync(VehicleId vehicleId, string parameterName, double value, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var modules = await GetModulesAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var setting = modules.SelectMany(module => module.Settings).FirstOrDefault(item => item.Name == parameterName);
        if (setting is null)
        {
            return new OptionalHardwareApplyResult(false, $"{parameterName} is not an editable setting of an available module.");
        }

        var state = RequireActiveVehicle(vehicleId);
        if (state.Identity.Firmware.Family == MissionPlanner.Firmware.FirmwareFamily.AntennaTracker && AntennaTrackerModule.IsSetting(parameterName) &&
            (state.IsArmed || state.CustomMode != 1)) return new(false, "Select Stop mode and disarm tracker servos before changing configuration.");
        if (parameterName is "GPS1_CAN_OVRIDE" or "GPS2_CAN_OVRIDE")
        {
            var otherName = parameterName == "GPS1_CAN_OVRIDE" ? "GPS2_CAN_OVRIDE" : "GPS1_CAN_OVRIDE";
            if (state.IsArmed || value != 0 && parameterRegistry.GetParameter(vehicleId, otherName)?.Value == value)
            {
                return new(false, "Disarm and clear a conflicting override before assigning the same CAN GPS node to another instance.");
            }
            if (value != setting.CurrentValue && value != 0 && !setting.Options.Any(option => option.Value == value))
            {
                return new(false, "Select a CAN GPS node currently reported by the controller, or automatic assignment.");
            }
        }
        if (state.IsArmed && (parameterName.StartsWith("H_", StringComparison.Ordinal) || parameterName.StartsWith("H2_", StringComparison.Ordinal) ||
            parameterName.StartsWith("IM_STB_COL_", StringComparison.Ordinal) || parameterName.StartsWith("COMPASS_", StringComparison.Ordinal) || parameterName.StartsWith("MOT_", StringComparison.Ordinal) || parameterName.StartsWith("SERVO", StringComparison.Ordinal)))
        {
            return new OptionalHardwareApplyResult(false, "Disarm before changing rotor, motor or output configuration.");
        }
        if (setting.ReadOnly || !double.IsFinite(value) || value < -float.MaxValue || value > float.MaxValue)
        {
            return new OptionalHardwareApplyResult(false, "The setting is read-only or the proposed value is not a finite supported number.");
        }
        if (setting.ParameterType != MavParamType.Real32 && value != Math.Truncate(value))
        {
            return new OptionalHardwareApplyResult(false, "This setting requires a whole number.");
        }
        var typeRangeValid = setting.ParameterType switch
        {
            MavParamType.Uint8 => value is >= byte.MinValue and <= byte.MaxValue,
            MavParamType.Int8 => value is >= sbyte.MinValue and <= sbyte.MaxValue,
            MavParamType.Uint16 => value is >= ushort.MinValue and <= ushort.MaxValue,
            MavParamType.Int16 => value is >= short.MinValue and <= short.MaxValue,
            MavParamType.Uint32 => value is >= uint.MinValue and <= uint.MaxValue,
            MavParamType.Int32 => value is >= int.MinValue and <= int.MaxValue,
            MavParamType.Real32 => true,
            _ => false
        };
        if (!typeRangeValid || setting.Minimum is { } minimum && value < minimum || setting.Maximum is { } maximum && value > maximum)
        {
            return new OptionalHardwareApplyResult(false, "The proposed value is outside the firmware type or metadata range.");
        }

        // Never log the value of a secret setting.
        logger.LogInformation("Applying optional-hardware parameter {Parameter} on {VehicleId}.", parameterName, vehicleId);
        return await WriteAndConfirmAsync(vehicleId, setting, value, cancellationToken).ConfigureAwait(false)
            ? new OptionalHardwareApplyResult(true, $"Confirmed {parameterName} by vehicle readback.", setting.RebootRequired)
            : new OptionalHardwareApplyResult(false, $"Readback did not confirm {parameterName}. Reconnect, refresh, and verify before flying.");
    }

    /// <inheritdoc />
    public async Task RefreshAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var modules = await GetModulesAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        foreach (var name in modules.SelectMany(module => module.Settings).Select(setting => setting.Name).Distinct(StringComparer.Ordinal))
        {
            if (parameterRegistry.GetParameter(vehicleId, name) is not null)
            {
                await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        return !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state
            ? throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.")
            : state;
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, PeripheralSetting setting, double value, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == setting.Name && NearlyEqual(parameter.Value, value))
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, setting.Name, (float)value, setting.ParameterType, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, setting.Name) is { } current && NearlyEqual(current.Value, value))
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, setting.Name, cancellationToken).ConfigureAwait(false);
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

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) <= 0.0005;
    }
}
