using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Aggregates setup outcomes into a consolidated, exportable summary.</summary>
public interface ISetupSummaryService
{
    /// <summary>Builds the consolidated setup summary for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The setup summary.</returns>
    SetupSummary BuildSummary(VehicleId vehicleId);

    /// <summary>Exports a summary as indented JSON.</summary>
    /// <param name="summary">The summary to export.</param>
    /// <returns>The JSON representation.</returns>
    string ExportJson(SetupSummary summary);

    /// <summary>Exports a summary as readable Markdown.</summary>
    /// <param name="summary">The summary to export.</param>
    /// <returns>The Markdown representation.</returns>
    string ExportMarkdown(SetupSummary summary);
}
