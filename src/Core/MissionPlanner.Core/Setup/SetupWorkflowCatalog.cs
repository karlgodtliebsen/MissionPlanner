using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Provides the ordered, vehicle-aware initial-setup workflow catalog.</summary>
public sealed class SetupWorkflowCatalog : ISetupWorkflowCatalog
{
    private static readonly IReadOnlySet<FirmwareFamily> activeFamilies = new HashSet<FirmwareFamily>
    {
        FirmwareFamily.ArduCopter,
        FirmwareFamily.ArduPlane,
        FirmwareFamily.Rover,
        FirmwareFamily.ArduSub,
        FirmwareFamily.Blimp
    };

    /// <inheritdoc />
    public IReadOnlyList<SetupWorkflowDescriptor> Workflows { get; } =
    [
        Descriptor(SetupWorkflowKey.Firmware, "Firmware", "Confirm firmware, board identity, and protocol capabilities."),
        Descriptor(SetupWorkflowKey.Frame, "Frame", "Choose the vehicle frame and actuator layout.", activeFamilies, [SetupWorkflowKey.Firmware], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.Accelerometer, "Accelerometer", "Calibrate level and orientation sensors.", activeFamilies, [SetupWorkflowKey.Frame]),
        Descriptor(SetupWorkflowKey.Compass, "Compass", "Calibrate compass instances and orientation.", activeFamilies, [SetupWorkflowKey.Accelerometer]),
        Descriptor(SetupWorkflowKey.Radio, "Radio", "Calibrate pilot input channels and ranges.", activeFamilies, [SetupWorkflowKey.Firmware]),
        Descriptor(SetupWorkflowKey.FlightModes, "Flight Modes", "Assign flight modes to pilot controls.", activeFamilies, [SetupWorkflowKey.Frame, SetupWorkflowKey.Radio], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.Battery, "Battery", "Configure voltage, current, and capacity monitoring.", activeFamilies, [SetupWorkflowKey.Firmware], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.Esc, "ESC", "Configure and calibrate electronic speed controllers.", activeFamilies, [SetupWorkflowKey.Battery]),
        Descriptor(SetupWorkflowKey.ServoOutput, "Servo Output", "Review actuator functions, limits, and reversal.", activeFamilies, [SetupWorkflowKey.Frame], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.OptionalHardware, "Optional Hardware", "Configure supported serial, CAN, rangefinder, and other peripherals.", null, [SetupWorkflowKey.Firmware], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.Safety, "Safety", "Review arming, failsafe, and mandatory preflight settings.", activeFamilies, [SetupWorkflowKey.Accelerometer, SetupWorkflowKey.Compass, SetupWorkflowKey.Radio], "Full Parameters List"),
        Descriptor(SetupWorkflowKey.Summary, "Summary", "Review completion, warnings, and links to advanced configuration.", null, [], "Full Parameters List")
    ];

    /// <inheritdoc />
    public IReadOnlyList<SetupWorkflowEvaluation> Evaluate(
        ActiveVehicleSnapshot snapshot,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyList<SetupCompletionEvidence> evidence)
    {
        var state = snapshot.State;
        var vehicleKey = state is null ? string.Empty : CreateVehicleKey(state);
        var firmwareSignature = state is null ? string.Empty : CreateFirmwareSignature(state);
        var parameterSignature = CreateParameterSignature(parameters);
        var completed = new HashSet<SetupWorkflowKey>();
        var result = new List<SetupWorkflowEvaluation>(Workflows.Count);

        foreach (var descriptor in Workflows)
        {
            var supported = state is null || IsSupported(descriptor, state, parameters);
            var visible = state is null || IsVisible(descriptor, state, parameters);
            SetupWorkflowState workflowState;
            string status;

            if (!snapshot.IsOnline)
            {
                workflowState = SetupWorkflowState.NotConnected;
                status = "Connect a vehicle to evaluate this workflow.";
            }
            else if (!supported)
            {
                workflowState = SetupWorkflowState.Unsupported;
                status = $"Not supported by {state!.Identity.Firmware.Family}.";
            }
            else
            {
                var local = evidence.FirstOrDefault(item => item.VehicleKey == vehicleKey && item.Workflow == descriptor.Key);
                if (local is not null && local.FirmwareSignature == firmwareSignature && local.ParameterSignature == parameterSignature)
                {
                    workflowState = SetupWorkflowState.Completed;
                    status = $"Completed locally {local.CompletedAt:yyyy-MM-dd HH:mm}.";
                    completed.Add(descriptor.Key);
                }
                else if (local is not null)
                {
                    workflowState = SetupWorkflowState.Warning;
                    status = "Vehicle firmware or parameters changed; review this workflow again.";
                }
                else if (descriptor.Prerequisites.Any(key => !completed.Contains(key)))
                {
                    workflowState = SetupWorkflowState.NotStarted;
                    status = $"Recommended first: {string.Join(", ", descriptor.Prerequisites.Where(key => !completed.Contains(key)))}.";
                }
                else
                {
                    workflowState = SetupWorkflowState.Available;
                    status = "Available.";
                }
            }

            result.Add(new SetupWorkflowEvaluation(descriptor, workflowState, status, visible));
        }

        return result;
    }

    /// <inheritdoc />
    public SetupCompletionEvidence CreateEvidence(
        SetupWorkflowKey workflow,
        VehicleState state,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        DateTimeOffset completedAt) =>
        new(CreateVehicleKey(state), workflow, CreateFirmwareSignature(state), CreateParameterSignature(parameters), completedAt);

    private static SetupWorkflowDescriptor Descriptor(
        SetupWorkflowKey key,
        string title,
        string description,
        IReadOnlySet<FirmwareFamily>? families = null,
        IReadOnlyList<SetupWorkflowKey>? prerequisites = null,
        string? configDestination = null) =>
        new(key, title, description, families ?? new HashSet<FirmwareFamily>(), prerequisites ?? [], configDestination);

    private static bool IsSupported(SetupWorkflowDescriptor descriptor, VehicleState state, IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        if (descriptor.SupportedFamilies.Count > 0 && !descriptor.SupportedFamilies.Contains(state.Identity.Firmware.Family))
        {
            return false;
        }

        return descriptor.Key != SetupWorkflowKey.OptionalHardware ||
            state.Identity.Firmware.Supports(MavProtocolCapability.Ftp) ||
            parameters.Keys.Any(IsOptionalHardwareParameter);
    }

    private static bool IsVisible(SetupWorkflowDescriptor descriptor, VehicleState state, IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        IsSupported(descriptor, state, parameters) || descriptor.Key is SetupWorkflowKey.Firmware or SetupWorkflowKey.Summary;

    private static bool IsOptionalHardwareParameter(string name) =>
        name.StartsWith("SERIAL", StringComparison.Ordinal) ||
        name.StartsWith("CAN_", StringComparison.Ordinal) ||
        name.StartsWith("RNGFND", StringComparison.Ordinal);

    private static string CreateVehicleKey(VehicleState state)
    {
        var firmware = state.Identity.Firmware;
        return firmware.HardwareUid2 ?? firmware.HardwareUid?.ToString("X16", CultureInfo.InvariantCulture) ??
            $"{state.VehicleId.SystemId}:{state.VehicleId.ComponentId}:{firmware.VendorId:X4}:{firmware.ProductId:X4}";
    }

    private static string CreateFirmwareSignature(VehicleState state)
    {
        var firmware = state.Identity.Firmware;
        return string.Join('|', firmware.Family, firmware.FlightVersion, firmware.FlightGitHash, firmware.Capabilities, firmware.BoardVersion, firmware.VendorId, firmware.ProductId);
    }

    private static string CreateParameterSignature(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        var builder = new StringBuilder();
        foreach (var parameter in parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.Append(parameter.Key).Append('=').Append(parameter.Value.Value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
