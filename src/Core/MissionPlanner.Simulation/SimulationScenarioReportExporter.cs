using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Exports complete scenario evidence as versioned JSON or readable text.</summary>
public sealed class SimulationScenarioReportExporter : ISimulationScenarioReportExporter
{
    private static readonly JsonSerializerOptions jsonOptions = CreateOptions();

    /// <inheritdoc />
    public string ToJson(SimulationScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, jsonOptions);
    }

    /// <inheritdoc />
    public string ToText(SimulationScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.AppendLine($"Simulation scenario: {report.ScenarioName}");
        builder.AppendLine($"Result: {report.Result} — {report.Summary}");
        builder.AppendLine($"Run: {report.RunId:N}");
        builder.AppendLine($"Target: session {report.SessionId:N}, vehicle {report.VehicleId}");
        builder.AppendLine($"Started: {report.StartedAt:O}");
        builder.AppendLine($"Ended: {report.EndedAt:O}");
        builder.AppendLine("Steps:");
        foreach (var step in report.Steps)
        {
            builder.AppendLine($"- [{step.Result}] {step.StepId} {step.Name}: {step.Evidence}");
        }

        if (report.Validation.Capabilities.Count > 0)
        {
            builder.AppendLine("Capabilities:");
            foreach (var capability in report.Validation.Capabilities)
            {
                builder.AppendLine($"- [{(capability.Available ? "available" : "unavailable")}] {capability.Name}: {capability.Reason}");
            }
        }

        return builder.ToString();
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        result.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return result;
    }
}
