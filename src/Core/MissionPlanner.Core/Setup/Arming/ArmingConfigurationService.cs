using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Arming;

/// <summary>Owns arming parameter semantics; the registry remains the only confirmed value store.</summary>
public sealed partial class ArmingConfigurationService(IActiveVehicleContext active, IVehicleParameterRegistry parameters,
    IVehicleParameterMetadataService metadata, IVehicleParameterLoadStatusContext loads,
    RadioArmingConfiguration radio, IDomainFactory factory, IVehicleOperationGate gate) : IArmingConfigurationService
{
    private static readonly (ArmingSetting Key, string Name, string Label)[] definitions =
    [
        (ArmingSetting.Checks, "ARMING_CHECK", "Pre-arm checks"),
        (ArmingSetting.Stick, "ARMING_RUDDER", "Stick arming"),
        (ArmingSetting.Location, "ARMING_NEED_LOC", "Require location before arming"),
        (ArmingSetting.Requirement, "ARMING_REQUIRE", "Arming requirement")
    ];

    private VehicleState Require(VehicleId id)
    {
        if (active.VehicleId != id || !active.IsOnline || active.State is not { } state)
        {
            throw new InvalidOperationException("Connect the selected vehicle before reading arming configuration.");
        }
        return state;
    }

    /// <inheritdoc />
    public async Task<ArmingSetupState> ReadAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        var identity = Require(id).Identity.Firmware;
        var connection = active.ConnectionCancellationToken;
        var supported = identity.Autopilot == 3 && identity.MavType != 6 && identity.Family != MissionPlanner.Firmware.FirmwareFamily.Unknown;
        IReadOnlyDictionary<string, ParameterMetadata> descriptions = new Dictionary<string, ParameterMetadata>();
        var status = "Current FC values; changes require reviewed Apply.";
        try
        {
            if (supported)
            {
                descriptions = await metadata.GetAllMetadataAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            status = "Metadata unavailable: " + ex.Message;
        }
        cancellationToken.ThrowIfCancellationRequested();
        connection.ThrowIfCancellationRequested();
        if (Require(id).Identity.Firmware != identity || active.ConnectionCancellationToken != connection)
        {
            throw new InvalidOperationException("Connection or firmware changed; reload configuration.");
        }
        var ready = loads.Get(id)?.State == ParameterLoadState.Completed;
        var values = parameters.GetAllParameters(id);
        double? Value(string name) => ready && values.TryGetValue(name, out var p) && float.IsFinite(p.Value) ? p.Value : null;
        var current = new ArmingConfiguration();
        var settings = new List<ArmingSettingDefinition>();
        foreach (var (key, name, label) in definitions)
        {
            var value = Value(name);
            descriptions.TryGetValue(name, out var description);
            var choices = description?.GetValueOptions().OrderBy(p => p.Key).Select(p => new ArmingChoice(p.Key, p.Value)).ToArray() ?? [];
            var bits = description?.GetBitmaskOptions().Where(p => p.Key is > 0 and < 24)
                .OrderBy(p => p.Key).Select(p => new ArmingChoice(1 << p.Key, p.Value)).ToArray() ?? [];
            if (key == ArmingSetting.Checks)
            {
                choices = bits.Length > 0 ? [new(1, "All checks (recommended)"), new(0, "Disabled"), new(-1, "Custom")] : [new(1, "All checks (recommended)"), new(0, "Disabled")];
            }
            var reason = !supported ? "Requires ArduPilot firmware." : !ready ? "Waiting for the existing parameter load to complete." :
                value is null ? "Not reported by this firmware." : description?.ReadOnly == true ? "Read-only in firmware metadata." :
                choices.Length == 0 ? "Choice metadata unavailable." : null;
            settings.Add(new(key, label, name, value, description?.DefaultValue, choices, bits,
                reason is null, description?.RebootRequired == true, reason));
            if (value is { } number && number == Math.Truncate(number) && number >= 0 && number <= int.MaxValue &&
                (key != ArmingSetting.Stick || number <= 2) && (key != ArmingSetting.Location || number <= 1))
            {
                current = current.With(key, number);
            }
        }
        var assignments = Enumerable.Range(1, 16).Where(c => Value($"RC{c}_OPTION") == RadioArmingConfiguration.ArmDisarmOption).ToArray();
        var known = ready && Enumerable.Range(1, 16).Any(c => Value($"RC{c}_OPTION") is not null);
        current = current with { ArmSwitch = !known || assignments.Length > 1 ? null : assignments.FirstOrDefault() };
        var evidence = ready ? values.Where(p => IsRelevant(p.Key) && float.IsFinite(p.Value.Value)).ToDictionary(p => p.Key, p => (double)p.Value.Value) : [];
        return new(id, current, settings, assignments, known, supported, ready, evidence,
            !supported ? "Arming configuration requires ArduPilot firmware." : !ready ? loads.Get(id)?.Message ?? "Parameters not loaded." : status);
    }

    private static bool IsRelevant(string name) => name.StartsWith("ARMING_", StringComparison.Ordinal) ||
        name.StartsWith("RCMAP_", StringComparison.Ordinal) || name == "FLTMODE_CH" ||
        Enumerable.Range(1, 16).Any(c => name == $"RC{c}_OPTION");
}
