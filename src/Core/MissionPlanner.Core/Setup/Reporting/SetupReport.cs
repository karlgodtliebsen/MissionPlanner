using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Reporting;

/// <summary>A read-only subsystem report. Text is plain data, never renderer markup.</summary>
/// <param name="Title">Subsystem name.</param>
/// <param name="Overview">Purpose and scope of the subsystem.</param>
/// <param name="VehicleId">Vehicle whose confirmed configuration was captured, if available.</param>
/// <param name="Status">Availability or loading explanation.</param>
/// <param name="Facts">Observed configuration facts.</param>
/// <param name="Guidance">Workflow prerequisites and limitations.</param>
/// <param name="Parameters">Confirmed parameter observations; null values remain unavailable.</param>
public sealed record SetupReport(string Title, string Overview, VehicleId? VehicleId,
    string Status, IReadOnlyList<SetupReportFact> Facts, IReadOnlyList<string> Guidance,
    IReadOnlyList<SetupReportParameter> Parameters);

/// <summary>A named observation, separate from editable or proposed configuration.</summary>
/// <param name="Name">Friendly fact label.</param>
/// <param name="Value">Observed value or explicit availability explanation.</param>
public sealed record SetupReportFact(string Name, string Value);

/// <summary>A parameter observation safe to export without including local edits.</summary>
/// <param name="Name">Firmware parameter name.</param>
/// <param name="Value">Confirmed numeric value; null means unavailable or non-finite.</param>
/// <param name="Comment">Optional diagnostic annotation, separate from the assignment.</param>
public sealed record SetupReportParameter(string Name, double? Value, string? Comment = null);

/// <summary>Reads reporting evidence without downloading, writing, or starting a hardware operation.</summary>
public interface ISetupReportService
{
    /// <summary>Captures one subsystem from the currently selected vehicle and existing caches.</summary>
    SetupReport Capture(SetupReportTopic topic);
}
