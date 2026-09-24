using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Reporting;

/// <summary>Captures confirmed subsystem evidence from the existing vehicle caches, without hardware I/O.</summary>
public sealed class SetupReportService : ISetupReportService
{
    private readonly IActiveVehicleContext vehicle;
    private readonly IVehicleParameterRegistry parameters;
    private readonly IVehicleParameterLoadStatusContext loads;

    /// <summary>Initializes read-only reporting against the active connection and shared parameter cache.</summary>
    public SetupReportService(IActiveVehicleContext vehicle, IVehicleParameterRegistry parameters,
        IVehicleParameterLoadStatusContext loads)
    {
        this.vehicle = vehicle;
        this.parameters = parameters;
        this.loads = loads;
    }

    /// <inheritdoc />
    public SetupReport Capture(SetupReportTopic topic)
    {
        var definition = SetupReportDefinitions.Get(topic);
        var snapshot = vehicle.Current;
        var connectionToken = vehicle.ConnectionCancellationToken;
        SetupReport Unavailable(string message) => new(definition.Title, definition.Overview,
            null, message, [], [definition.Guidance], []);

        if (definition.Prefixes is null)
        {
            return Unavailable("This tool uses a local device, connection or data source. Its operation status is reported separately from vehicle configuration.");
        }
        if (!snapshot.IsOnline || snapshot.VehicleId is not { } id || snapshot.State is not { } state)
        {
            return Unavailable(topic == SetupReportTopic.Firmware
                ? "No vehicle telemetry connection. Firmware preparation and USB controller discovery remain available in Configuration."
                : "Disconnected. Connect a vehicle to read its confirmed configuration.");
        }

        var facts = new List<SetupReportFact>
        {
            new("Vehicle", snapshot.DisplayName),
            new("Firmware", $"{state.Identity.Firmware.Family} {state.Identity.Firmware.FlightVersion}"),
            new("Reported arming state", state.IsArmed ? "Armed" : "Disarmed"),
        };
        if (topic is SetupReportTopic.HardwareId or SetupReportTopic.Firmware)
        {
            var firmware = state.Identity.Firmware;
            facts.Add(new("Board identification", firmware.BoardVersion == 0 ? "Not reported"
                : $"Board {firmware.BoardVersion}; vendor 0x{firmware.VendorId:X4}; product 0x{firmware.ProductId:X4}"));
        }

        var all = parameters.GetAllParameters(id);
        var load = loads.Get(id);
        var expected = parameters.GetParameterCount(id);
        var complete = load?.State == ParameterLoadState.Completed && (expected is null || all.Count >= expected);
        // A count-complete cache can also be used by hosts without the load-status projection.
        complete |= load is null && expected is > 0 && all.Count >= expected;
        var status = complete ? "Confirmed flight-controller configuration. Local edits are shown only in Configuration."
            : load?.IsInProgress == true ? $"Loading vehicle parameters ({load.ReceivedCount} of {load.TotalCount}). Confirmed configuration is not yet available."
            : load?.State is ParameterLoadState.Failed or ParameterLoadState.Cancelled
                ? "Parameter loading did not complete. Configuration evidence is unavailable until a complete load succeeds."
                : "Waiting for a complete vehicle parameter load. Configuration evidence is not yet available.";
        var relevant = complete
            ? all.Where(pair => Matches(topic, definition, pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SetupReportParameter(pair.Key, double.IsFinite(pair.Value.Value) ? pair.Value.Value : null,
                    topic == SetupReportTopic.HardwareId && double.IsFinite(pair.Value.Value) &&
                    pair.Value.Value >= 0 && (double)pair.Value.Value <= uint.MaxValue
                        ? $"0x{Convert.ToUInt32(Math.Round(pair.Value.Value)):X8}" : null))
                .ToArray()
            : [];

        if (complete && topic != SetupReportTopic.Firmware)
        {
            facts.Add(new("Configuration evidence", relevant.Length == 0
                ? "No relevant parameters were reported by this firmware. This does not establish hardware presence or health."
                : $"{relevant.Length} relevant parameters are available. Reported configuration does not by itself establish hardware presence or health."));
            AddEnablement(topic, relevant, facts);
        }
        if (connectionToken.IsCancellationRequested || vehicle.VehicleId != id || !vehicle.IsOnline)
        {
            return Unavailable("The active connection changed. Waiting for configuration from the selected vehicle.");
        }
        return new(definition.Title, definition.Overview, id, status, facts.ToArray(), [definition.Guidance], relevant);
    }

    private static bool Matches(SetupReportTopic topic, SetupReportDefinition definition, string name)
    {
        if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            return false;
        }
        if (topic == SetupReportTopic.HardwareId)
        {
            return (name.Contains("_ID", StringComparison.Ordinal) || name.Contains("_DEVID", StringComparison.Ordinal)) &&
                !name.Contains("_IDX", StringComparison.Ordinal) && !name.Contains("FRSKY", StringComparison.Ordinal);
        }
        return definition.Prefixes!.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void AddEnablement(SetupReportTopic topic, IReadOnlyList<SetupReportParameter> values,
        List<SetupReportFact> facts)
    {
        var name = topic switch
        {
            SetupReportTopic.Airspeed => "ARSPD_USE",
            SetupReportTopic.OpticalFlow => "FLOW_ENABLE",
            SetupReportTopic.Parachute => "CHUTE_ENABLED",
            SetupReportTopic.Adsb => "ADSB_ENABLE",
            _ => null,
        };
        if (name is null)
        {
            return;
        }
        var value = values.FirstOrDefault(p => p.Name == name)?.Value;
        facts.Add(new("Reported use / enable flag", value switch
        {
            0 => "Disabled",
            1 => "Enabled",
            null => "Not reported",
            _ => "Firmware-specific mode; review the reported value and supported choices in Configuration.",
        }));
    }
}
