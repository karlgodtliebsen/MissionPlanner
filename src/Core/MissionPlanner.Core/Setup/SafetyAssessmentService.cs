using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Assesses arming, failsafe, and safety configuration from live parameters and firmware family.</summary>
public sealed class SafetyAssessmentService : ISafetyAssessmentService
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ILogger<SafetyAssessmentService> logger;

    /// <summary>Initializes the safety-assessment service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="logger">The logger.</param>
    public SafetyAssessmentService(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry parameterRegistry, ILogger<SafetyAssessmentService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public SafetyAssessment BuildAssessment(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            return SafetyAssessment.Empty(vehicleId);
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var family = state.Identity.Firmware.Family;
        var items = new List<SafetyCheckItem>();
        var warnings = new List<string>();

        AssessArming(parameters, items, warnings);
        AssessSafetySwitch(parameters, items);
        AssessThrottleFailsafe(family, parameters, items, warnings);
        AssessBatteryFailsafe(parameters, items, warnings);
        AssessGcsFailsafe(family, parameters, items);
        AssessEkfFailsafe(family, parameters, items);
        AssessFence(parameters, items, warnings);

        logger.LogInformation("Built safety assessment for {VehicleId} with {WarningCount} warnings.", vehicleId, warnings.Count);
        return new SafetyAssessment(vehicleId, items, warnings);
    }

    private static void AssessArming(IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items, List<string> warnings)
    {
        if (TryReadInt(parameters, "ARMING_CHECK", out var value))
        {
            var status = value == 0 ? SetupAssessmentStatus.Warning : SetupAssessmentStatus.Pass;
            items.Add(new SafetyCheckItem("Arming", "Pre-arm checks", status,
                value == 0 ? "All pre-arm checks are disabled." : value == 1 ? "All pre-arm checks are enabled." : "A subset of pre-arm checks is enabled."));
            if (value == 0)
            {
                warnings.Add("Pre-arm checks are disabled (ARMING_CHECK = 0). Enable them before flight.");
            }
        }
        else
        {
            items.Add(new SafetyCheckItem("Arming", "Pre-arm checks", SetupAssessmentStatus.NotAssessed, "ARMING_CHECK was not available."));
        }
    }

    private static void AssessSafetySwitch(IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items)
    {
        if (TryReadInt(parameters, "BRD_SAFETYENABLE", out var value))
        {
            items.Add(new SafetyCheckItem("Safety switch", "Hardware safety switch",
                value != 0 ? SetupAssessmentStatus.Pass : SetupAssessmentStatus.Warning,
                value != 0 ? "The hardware safety switch is enabled." : "The hardware safety switch is disabled."));
        }
        else
        {
            items.Add(new SafetyCheckItem("Safety switch", "Hardware safety switch", SetupAssessmentStatus.Unsupported, "This board does not expose BRD_SAFETYENABLE."));
        }
    }

    private static void AssessThrottleFailsafe(FirmwareFamily family, IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items, List<string> warnings)
    {
        var name = family == FirmwareFamily.ArduPlane ? "THR_FAILSAFE" : "FS_THR_ENABLE";
        if (TryReadInt(parameters, name, out var value))
        {
            var status = value != 0 ? SetupAssessmentStatus.Pass : SetupAssessmentStatus.Warning;
            items.Add(new SafetyCheckItem("Failsafe", "Radio / throttle failsafe", status,
                value != 0 ? "The radio failsafe is enabled." : "The radio failsafe is disabled."));
            if (value == 0)
            {
                warnings.Add($"Radio/throttle failsafe is disabled ({name} = 0).");
            }
        }
        else
        {
            items.Add(new SafetyCheckItem("Failsafe", "Radio / throttle failsafe", SetupAssessmentStatus.NotAssessed, $"{name} was not available."));
        }
    }

    private static void AssessBatteryFailsafe(IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items, List<string> warnings)
    {
        if (!TryReadInt(parameters, "BATT_MONITOR", out var monitor) || monitor == 0)
        {
            items.Add(new SafetyCheckItem("Failsafe", "Battery failsafe", SetupAssessmentStatus.NotConfigured, "No battery monitor is enabled."));
            return;
        }

        var lowAction = TryReadInt(parameters, "BATT_FS_LOW_ACT", out var low) ? low : 0;
        var critAction = TryReadInt(parameters, "BATT_FS_CRT_ACT", out var crit) ? crit : 0;
        if (lowAction == 0 && critAction == 0)
        {
            items.Add(new SafetyCheckItem("Failsafe", "Battery failsafe", SetupAssessmentStatus.Warning, "A battery monitor is enabled but no failsafe action is set."));
            warnings.Add("A battery monitor is enabled but neither low nor critical failsafe action is configured.");
        }
        else
        {
            items.Add(new SafetyCheckItem("Failsafe", "Battery failsafe", SetupAssessmentStatus.Pass, "Battery failsafe actions are configured."));
        }
    }

    private static void AssessGcsFailsafe(FirmwareFamily family, IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items)
    {
        var name = family == FirmwareFamily.ArduPlane ? "FS_LONG_ACTN" : "FS_GCS_ENABLE";
        if (TryReadInt(parameters, name, out var value))
        {
            items.Add(new SafetyCheckItem("Failsafe", "GCS failsafe",
                value != 0 ? SetupAssessmentStatus.Pass : SetupAssessmentStatus.NotConfigured,
                value != 0 ? "A GCS failsafe action is configured." : "No GCS failsafe action is configured."));
        }
        else
        {
            items.Add(new SafetyCheckItem("Failsafe", "GCS failsafe", SetupAssessmentStatus.NotAssessed, $"{name} was not available."));
        }
    }

    private static void AssessEkfFailsafe(FirmwareFamily family, IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items)
    {
        if (family != FirmwareFamily.ArduCopter)
        {
            items.Add(new SafetyCheckItem("Failsafe", "EKF failsafe", SetupAssessmentStatus.Unsupported, "EKF failsafe action is Copter-specific."));
            return;
        }

        if (TryReadInt(parameters, "FS_EKF_ACTION", out var value))
        {
            items.Add(new SafetyCheckItem("Failsafe", "EKF failsafe",
                value != 0 ? SetupAssessmentStatus.Pass : SetupAssessmentStatus.Warning,
                value != 0 ? "An EKF failsafe action is configured." : "No EKF failsafe action is configured."));
        }
        else
        {
            items.Add(new SafetyCheckItem("Failsafe", "EKF failsafe", SetupAssessmentStatus.NotAssessed, "FS_EKF_ACTION was not available."));
        }
    }

    private static void AssessFence(IReadOnlyDictionary<string, VehicleParameter> parameters, List<SafetyCheckItem> items, List<string> warnings)
    {
        if (!TryReadInt(parameters, "FENCE_ENABLE", out var enable))
        {
            items.Add(new SafetyCheckItem("Geofence", "Fence", SetupAssessmentStatus.Unsupported, "This firmware does not expose FENCE_ENABLE."));
            return;
        }

        if (enable == 0)
        {
            items.Add(new SafetyCheckItem("Geofence", "Fence", SetupAssessmentStatus.NotConfigured, "The geofence is disabled. Configure it on the Config GeoFence page."));
            return;
        }

        var action = TryReadInt(parameters, "FENCE_ACTION", out var value) ? value : -1;
        if (action == 0)
        {
            items.Add(new SafetyCheckItem("Geofence", "Fence", SetupAssessmentStatus.Warning, "The geofence is enabled but its action is report-only."));
            warnings.Add("The geofence is enabled but FENCE_ACTION is report-only (0).");
        }
        else
        {
            items.Add(new SafetyCheckItem("Geofence", "Fence", SetupAssessmentStatus.Pass, "The geofence is enabled with an action. Review limits on the Config GeoFence page."));
        }
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, VehicleParameter> parameters, string name, out int value)
    {
        if (parameters.TryGetValue(name, out var parameter))
        {
            value = (int)Math.Round(parameter.Value);
            return true;
        }

        value = 0;
        return false;
    }
}
